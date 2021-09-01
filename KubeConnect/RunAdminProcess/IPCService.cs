using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect.RunAdminProcess
{
    public class IPCService : IDisposable
    {
        private ConcurrentBag<Func<string, Task>> callbacks = new ConcurrentBag<Func<string, Task>>();
        private ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> sendQueue = new ConcurrentQueue<string>();

        public string PipeName { get; }

        private readonly bool isParent;

        public IPCService()
            : this(Guid.NewGuid().ToString(), true)
        { }

        public IPCService(string pipeName)
            : this(pipeName, false)
        { }

        public IPCService(string pipeName, bool parent)
        {
            var cancellationToken = CancellationToken.None;

            PipeName = pipeName;
            this.isParent = parent;


        }

        private Stream? reciever;
        private Stream? sender;
        public void Connect(CancellationToken cancellationToken)
        {
            Task recieverConnectTask;
            Task senderConnectTask;
            // switch direction when child connection
            if (isParent)
            {
                var recieverSrv = new NamedPipeServerStream($"IN_{PipeName}", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                var senderSrv = new NamedPipeServerStream($"OUT_{PipeName}", PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                recieverConnectTask = recieverSrv.WaitForConnectionAsync();
                senderConnectTask = senderSrv.WaitForConnectionAsync();
                this.reciever = recieverSrv; // new BufferedStream(recieverSrv);
                this.sender = senderSrv;// new BufferedStream(senderSrv);
            }
            else
            {
                var sender = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

                var recieverClient = new NamedPipeClientStream(".", $"OUT_{PipeName}", PipeDirection.In, PipeOptions.Asynchronous);
                var senderClient = new NamedPipeClientStream(".", $"IN_{PipeName}", PipeDirection.Out, PipeOptions.Asynchronous);
                recieverConnectTask = recieverClient.ConnectAsync();
                senderConnectTask = senderClient.ConnectAsync();

                this.reciever = recieverClient;// new BufferedStream(recieverClient);
                this.sender = senderClient; // new BufferedStream(senderClient);

            }
            SemaphoreSlim recieveSignal = new SemaphoreSlim(0);
            this.recieverTask = Task.Run(async () =>
            {
                try
                {
                    await recieverConnectTask;
                    while (true)
                    {
                        await Task.Delay(10);
                        var length = await ReadLengthAsync(reciever, default).ConfigureAwait(false);

                        var buffer = ArrayPool<byte>.Shared.Rent(length);

                        try
                        {
                            var readLength = 0;
                            while (readLength < length)
                            {
                                var read = await reciever.ReadAsync(buffer, readLength, length - readLength, default).ConfigureAwait(false);
                                if (read == -1)
                                {
                                    throw new OperationCanceledException();
                                }

                                readLength += read;
                            }

                            var payload = Encoding.UTF8.GetString(buffer, 0, length);
                            recieveSignal.Release();
                            messages.Enqueue(payload);
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw;
                }
            });

            this.processCallbacks = Task.Run(async () =>
            {
                while (true)
                {
                    await recieveSignal.WaitAsync();

                    if (messages.TryDequeue(out var payload) && !string.IsNullOrEmpty(payload))
                    {
                        foreach (var cb in callbacks)
                        {
                            await cb(payload);
                        }
                    }
                }
            });

            this.processSending = Task.Run(async () =>
            {
                try
                {
                    await senderConnectTask;
                    var lengthBuffer = new byte[2];

                    while (true)
                    {
                        await sendSignal.WaitAsync();
                        await Task.Yield();

                        if (sendQueue.TryDequeue(out var payload))
                        {
                            var length = Encoding.UTF8.GetByteCount(payload);
                            var buffer = ArrayPool<byte>.Shared.Rent(length + 2);
                            try
                            {
                                Encoding.UTF8.GetBytes(payload, buffer.AsSpan(2));
                                buffer[0] = (byte)(length / 256);
                                buffer[1] = (byte)(length & 255);

                                await sender.WriteAsync(buffer, 0, length + 2, default).ConfigureAwait(false);
                                await sender.FlushAsync(default).ConfigureAwait(false);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(buffer);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw;
                }
            });

        }

        private SemaphoreSlim sendSignal = new SemaphoreSlim(0);

        public void Send(string payload)
        {
            sendSignal.Release();
            sendQueue.Enqueue(payload);
        }

        public void RegisterHandler(Action<string> action)
        {
            lock (callbacks)
            {
                callbacks.Add((p) =>
                {
                    action(p);
                    return Task.CompletedTask;
                });
            }
        }
        public void RegisterHandler(Func<string, Task> action)
        {
            callbacks.Add(action);
        }

        private byte[] singleByteBuffer = new byte[2];
        private object recieverTask;
        private Task processCallbacks;
        private Task processSending;

        public async Task<int> ReadLengthAsync(Stream pipe, CancellationToken cancellationToken = default)
        {
            var bytesMissing = 2;
            while (bytesMissing != 0)
            {
                var bytesRead = await pipe.ReadAsync(singleByteBuffer, 0, bytesMissing, cancellationToken).ConfigureAwait(false);
                if (bytesRead == -1)
                {
                    throw new OperationCanceledException("Connection closed");
                }

                bytesMissing -= bytesRead;

                if (bytesMissing == 0)
                {
                    int length = singleByteBuffer[0] * 256;
                    length += singleByteBuffer[1];
                    return length;
                }
            }

            return -1;
        }

        public void Dispose()
        {
            reciever?.Dispose();
            sender?.Dispose();
        }
    }
}
