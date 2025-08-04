using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect.Common;

public class KubeConnectSSHClient
{
    public KubeConnectSSHClient(KubeConnectClient client, string serviceName, int localPort, IEnumerable<int> remotePorts, ILogger logger, int? pid = null)
    {
        connectClient = client;
        _serviceName = serviceName;
        _localPort = localPort;
        _remotePorts = remotePorts;
        _pid = pid;
        this.logger = logger;
    }

    private string _status = "Not Started";
    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            OnStatusUpdate?.Invoke(this);
        }
    }

    public Action<KubeConnectSSHClient>? OnStatusUpdate { get; set; }

    private object _handleLock = new object();
    private Task<bool>? _startup;
    private KubeConnectClient connectClient;
    private readonly string _serviceName;
    private readonly int _localPort;
    private readonly IEnumerable<int> _remotePorts;
    private readonly int? _pid;
    private readonly ILogger logger;
    private DisposableHandle? _handle;

    private string[] stoppedStatuses = ["Stopped", "Offline"];
    public Task<bool> Start(CancellationToken cancellationToken)
    {
        if (_startup is not null)
        {
            return _startup;
        }

        lock (_handleLock)
        {
            if (_startup is not null)
            {
                return _startup;
            }

            var tcs = new TaskCompletionSource<bool>();
            _startup = tcs.Task;

            Task task = Task.CompletedTask;
            CancellationTokenSource cts = new CancellationTokenSource();

            CancellationTokenSource combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            cancellationToken = combined.Token;
            // handle stop vs dispose??
            var handle = new DisposableHandle(async () =>
            {
                this.Status = "Stopping";
                _startup = null;
                _handle = null;
                await cts.CancelAsync();
                await task;
                this.Status = "Stopped";
                tcs.TrySetResult(false);
            });
            _handle = handle;
            task = Task.Run(async () =>
            {
                this.Status = "Starting";
                try
                {

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        // we see if a brige is active 
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            var bridgeStatus = await connectClient.GetBridgeStatus(_serviceName);

                            if (bridgeStatus.Status == "Running")
                            {
                                this.Status = "Running";
                                break;
                            }

                            try
                            {

                                if (stoppedStatuses.Contains(bridgeStatus.Status) || bridgeStatus.LocalPort != _localPort)
                                {
                                    if (!stoppedStatuses.Contains(bridgeStatus.Status))
                                    {
                                        await connectClient.StopBridge(_serviceName);
                                    }

                                    if (!await connectClient.StartBridge(_serviceName, _localPort, _remotePorts, _pid))
                                    {
                                        throw new Exception("Failed to start bridge");
                                    }
                                    await Task.Delay(1000);
                                }
                            }
                            catch
                            {
                                logger.LogWarning("Failed to start kubeconnect bridge, retrying...");
                                await Task.Delay(1000);
                            }
                        }
                        cancellationToken.ThrowIfCancellationRequested();

                        var stream = await connectClient.OpenBridgeLogStream(_serviceName);

                        tcs.TrySetResult(true);
                        await stream.WriteToLogger(logger, cts.Token);
                    }
                }
                catch
                {
                }
                tcs.TrySetResult(false);
            });
            return tcs.Task;
        }
    }

    public async Task Stop()
    {
        if (_handle != null)
        {
            await _handle.DisposeAsync();
        }
    }
}
public class KubeConnectClient(HttpClient httpClient, int mainPort)
{
    public async Task<StatusResult?> GetStatus()
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<StatusResult>($"http://localhost:{mainPort}/status");
            return result;
        }
        catch
        {
            return null;
        }
    }
    public async Task<BridgeStatusResult> GetBridgeStatus(string serviceName)
    {
        try
        {
            var bridgeStatus = await httpClient.GetFromJsonAsync<BridgeStatusResult>($"http://localhost:{mainPort}/bridge/{serviceName}");
            if (bridgeStatus is not null)
            {
                return bridgeStatus;
            }
        }
        catch
        {
            // hanging/closed stream, we just stop monitoring and wait for the next bind
        }
        return new BridgeStatusResult(serviceName, false, "Offline", 0);
    }

    public async Task<bool> StopBridge(string serviceName)
    {
        try
        {
            var resp = await httpClient.DeleteAsync($"http://localhost:{mainPort}/bridge/{serviceName}");
            return resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.NotFound;
        }
        catch
        {
            // hanging/closed stream, we just stop monitoring and wait for the next bind
        }

        return false;
    }

    public async Task<bool> StartBridge(string serviceName, int localPort, IEnumerable<int> remotePorts, int? pid = null)
    {
        try
        {
            var resp = await httpClient.PutAsJsonAsync($"http://localhost:{mainPort}/bridge/{serviceName}", new BridgeStartupRequest
            {
                LocalPort = localPort,
                RemotePorts = remotePorts,
                Pid = pid
            });
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            // hanging/closed stream, we just stop monitoring and wait for the next bind
        }

        return false;
    }

    public async Task<LogStream> OpenBridgeLogStream(string serviceName)
    {
        try
        {
            var stream = await httpClient.GetStreamAsync($"http://localhost:{mainPort}/bridge/{serviceName}/logs");
            return new LogStream(stream);
        }
        catch
        {
            // hanging/closed stream, we just stop monitoring and wait for the next bind
        }
        return LogStream.NoOp;
    }

    public async Task<LogStream> OpenServiceLogStream(string serviceName)
    {
        try
        {
            var stream = await httpClient.GetStreamAsync($"http://localhost:{mainPort}/service/{serviceName}/logs");
            return new LogStream(stream);
        }
        catch
        {
            // hanging/closed stream, we just stop monitoring and wait for the next bind
        }
        return LogStream.NoOp;
    }
}

public class LogStream : IDisposable
{
    internal static LogStream NoOp { get; } = new LogStream();
    private StreamReader? _sr;

    public LogStream(Stream stream)
    {
        _sr = new StreamReader(stream);
    }
    private LogStream()
    {
    }

    public async Task WriteToLogger(ILogger logger, CancellationToken cancellationToken)
    {
        if (_sr is null)
        {
            return;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _sr.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    return;
                }
                logger.LogInformation(line);
            }
        }
        catch
        {
            // stop if error happens
        }
    }

    public void Dispose()
        => _sr?.Dispose();
}

public static class KubeConnectClientExtensions
{
    public static KubeConnectClient AsKubeConnectClient(this HttpClient httpClient, int port)
        => new KubeConnectClient(httpClient, port);
}
