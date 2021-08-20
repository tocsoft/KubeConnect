using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect
{
    public interface IConsole
    {
        void WriteErrorLine(string line);
        void WriteLine(string line);

        event ConsoleCancelEventHandler CancelKeyPress;
    }


    public class LinePrefixingConsole : IConsole
    {
        private readonly string linePrefix;
        private readonly IConsole console;

        public LinePrefixingConsole(string linePrefix, IConsole console)
        {
            this.linePrefix = linePrefix;
            this.console = console;
        }

        public event ConsoleCancelEventHandler CancelKeyPress
        {
            add => console.CancelKeyPress += value;
            remove => console.CancelKeyPress -= value;
        }

        public void WriteErrorLine(string line)
        {
            console.WriteErrorLine(linePrefix + line);
        }

        public void WriteLine(string line)
        {
            console.WriteLine(linePrefix + line);
        }
    }
    
    public delegate void ConsoleCancelEventHandler(object? sender, EventArgs e);

    public class ConsoleWrapper : IConsole
    {
        object locker = new object();
        private event ConsoleCancelEventHandler _cancelKeyPress;
        public event ConsoleCancelEventHandler CancelKeyPress
        {
            add
            {
                _cancelKeyPress += value;
                ListenToCancel();
            }
            remove
            {
                _cancelKeyPress -= value;
                ListenToCancel(); ;
            }
        }

        private void listener(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            Task.Run(() =>
            {
                _cancelKeyPress?.Invoke(sender, e);
            });
        }

        private bool listening = false;
        private void ListenToCancel()
        {
            var hasListeners = (_cancelKeyPress?.GetInvocationList()?.Length ?? 0) > 0;

            if (listening && !hasListeners)
            {
                listening = false;
                Console.CancelKeyPress -= listener;
            }
            else if (!listening && hasListeners)
            {
                listening = true;
                Console.CancelKeyPress += listener;
            }
        }

        public void WriteErrorLine(string line)
        {
            //lock (locker)
            {
                Console.Error.WriteLine(line);
            }
        }

        public void WriteLine(string line)
        {
            // lock (locker)
            {
                Console.WriteLine(line);
            }
        }
    }

    public class PipeConsoleWriter : IConsole, IDisposable
    {
        private readonly NamedPipeClientStream client;
        private StreamString simpleClient;
        private Task task;

        public PipeConsoleWriter(string pipeName)
        {
            this.client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            this.client.Connect();
            this.simpleClient = new StreamString(client);
            this.task = Task.Run(() =>
            {
                while (this.client.IsConnected)
                {
                    var (type, _) = simpleClient.ReadString();
                    if (type == "k")
                    {
                        new Thread(() =>
                        {
                            try
                            {
                                CancelKeyPress?.Invoke(this, new EventArgs());
                            }
                            catch
                            {
                                // noop
                            }
                        }).Start();
                    }
                }
            });
        }

        public event ConsoleCancelEventHandler CancelKeyPress;

        public void Dispose()
        {
            client.Dispose();
        }

        public void Write(string text)
        {
            lock (simpleClient)
            {
                simpleClient.WriteString("w", text);
            }
        }

        public void WriteErrorLine(string line)
        {
            lock (simpleClient)
            {
                simpleClient.WriteString("e", line);
            }
        }

        public void WriteLine(string line)
        {
            lock (simpleClient)
            {
                simpleClient.WriteString("l", line);
            }
        }
    }
    public class PipeConsoleForwarder
    {
        private readonly IConsole console;

        public string PipeName { get; } = Guid.NewGuid().ToString();
        public PipeConsoleForwarder(IConsole console)
        {
            this.console = console;
        }

        public Task Listen(CancellationToken cancellationToken)
        => Task.Run(async () =>
        {
            var srv = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            srv.ConfigureAwait(false);
            try
            {
                await srv.WaitForConnectionAsync(cancellationToken);

                // Read the request from the client. Once the client has
                // written to the pipe its security token will be available.
                StreamString ss = new StreamString(srv);
                cancellationToken.Register(() =>
                {
                    ss.WriteString("k", "");
                });
                // Verify our identity to the connected client using a
                // string that the client anticipates.
                while (srv.IsConnected)
                {

                    var (type, message) = ss.ReadString();
                    switch (type)
                    {
                        case "e":
                            console.WriteErrorLine(message);
                            break;
                        case "l":
                        case "w":
                        default:
                            console.WriteLine(message);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                var t = ex;
            }
        });
    }

    public class StreamString
    {
        private Stream ioStream;
        private UnicodeEncoding streamEncoding;
        public byte[] readBuffer = new byte[2];
        public StreamString(Stream ioStream)
        {
            this.ioStream = ioStream;
            streamEncoding = new UnicodeEncoding();
        }

        public (string type, string message) ReadString()
        {
            int len = ioStream.ReadByte() * 256;
            len += ioStream.ReadByte();

            if (len < 0)
            {
                throw new TaskCanceledException();
            }

            var inBuffer = new byte[len];
            ioStream.Read(inBuffer, 0, len);

            var str = streamEncoding.GetString(inBuffer);
            var idx = str.IndexOf('@');
            var type = str.Substring(0, idx);
            var msg = str.Substring(idx + 1);
            return (type, msg);
        }

        public int WriteString(string type, string message)
        {
            var finalString = type + '@' + message;
            byte[] outBuffer = streamEncoding.GetBytes(finalString);
            int len = outBuffer.Length;
            if (len > UInt16.MaxValue)
            {
                len = (int)UInt16.MaxValue;
            }
            ioStream.WriteByte((byte)(len / 256));
            ioStream.WriteByte((byte)(len & 255));
            ioStream.Write(outBuffer, 0, len);
            ioStream.Flush();

            return outBuffer.Length + 2;
        }
    }
}
