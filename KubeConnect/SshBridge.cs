using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect
{
    public class SshBridge : IAsyncDisposable
    {
        public SshBridge(ServiceManager serviceManager, ILogger logger, string serviceName, IEnumerable<int> remotePorts, int localPort)
        {
            ServiceName = serviceName;
            RemotePorts = remotePorts;
            LocalPort = localPort;
            _logger = logger;
            _serviceManager = serviceManager;
        }

        public string ServiceName { get; init; }
        public IEnumerable<int> RemotePorts { get; }
        public int LocalPort { get; }
        public string Status { get; private set; } = "Stopped";

        private ConcurrentDictionary<int, bool> _monitoredPids = new ConcurrentDictionary<int, bool>();

        public IEnumerable<int> MonitoredPids => _monitoredPids.Keys;

        private ILogger _logger;
        private ServiceManager _serviceManager;
        private DisposableHandle? handle = null;
        private TaskCompletionSource<bool> onStarted = new TaskCompletionSource<bool>();

        public Task<bool> Start(int? monitoredPid, CancellationToken cancellationToken)
        {
            if (monitoredPid.HasValue)
            {
                _monitoredPids.TryAdd(monitoredPid.Value, true);
            }
            return Start(cancellationToken);
        }

        public Task<bool> Start(CancellationToken cancellationToken)
        {
            if (handle is not null)
            {
                return onStarted.Task;
            }

            this.Status = "Starting";

            var cts2 = new CancellationTokenSource();
            var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts2.Token);

            cancellationToken = combinedCts.Token;
            var taskMonitor = Task.Run(async () =>
            {
                List<int> deadProcesses = new List<int>();
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_monitoredPids.Any())
                    {
                        deadProcesses.Clear();
                        foreach (var pid in _monitoredPids)
                        {
                            try
                            {
                                var p = Process.GetProcessById(pid.Key);
                                if (p.HasExited)
                                {
                                    deadProcesses.Add(pid.Key);
                                }
                            }
                            catch
                            {
                                // we failed to get the process treat it as dead
                                deadProcesses.Add(pid.Key);
                            }
                        }
                        foreach (var pid in deadProcesses)
                        {
                            _monitoredPids.TryRemove(pid, out _);
                        }

                        if (!_monitoredPids.Any())
                        {
                            await this.Stop();
                        }
                    }
                    await Task.Delay(1000, cancellationToken);
                }
            });
            var task = Task.Run(async () =>
            {
                SshClient sshClient = null!;
                cancellationToken.Register(() =>
                {
                    sshClient?.Disconnect();
                    sshClient?.Dispose();
                });

                int targetEndpointPort = LocalPort;
                List<int> listeningPorts = new();
                try
                {
                    // log still wating after x counts
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        this.Status = "Starting";

                        var retryConnectCounter = 0;
                        const int retryConnectCounterLogLimit = 3;
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            retryConnectCounter++;
                            sshClient = new SshClient(ServiceName, 2222, "linuxserver.io", "password");
                            try
                            {
                                sshClient.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
                                sshClient.Connect();

                                // we go find the upstream k8s 
                                // find the service details from k8s
                                foreach (var port in RemotePorts)
                                {
                                    var targetPort = targetEndpointPort == -1 ? port : targetEndpointPort;
                                    var portFwd = new ForwardedPortRemote(IPAddress.Any, (uint)port, IPAddress.Loopback, (uint)targetPort);
                                    sshClient.AddForwardedPort(portFwd);
                                    portFwd.RequestReceived += (object? sender, Renci.SshNet.Common.PortForwardEventArgs e) =>
                                    {
                                        try
                                        {
                                            _logger.LogInformation($"Traffic redirected from {ServiceName}:{port} to localhost:{targetPort}");
                                        }
                                        catch
                                        {

                                        }
                                    };
                                    portFwd.Start();
                                    _logger.LogInformation($"Redirecting traffic from {ServiceName}:{port} to localhost:{targetPort}");
                                    listeningPorts.Add(port);
                                }
                                this.Status = "Running";
                                onStarted.TrySetResult(true);// signal that we are ready only the first time!
                                // in the monitoring loop now
                                break;
                            }
                            catch (Exception ex)
                            {
                                sshClient?.Disconnect();
                                if (ex is SshOperationTimeoutException or SshConnectionException)
                                {
                                    if (retryConnectCounterLogLimit < retryConnectCounter)
                                    {
                                        retryConnectCounter = 0;
                                        _logger.LogWarning($"Failed to connect to ssh server, waiting for kubeconnect and/or ssh server to startup.");
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning(ex, $"Failed to connect to ssh server, kubeconnect might not be running, or ssh server is still starting.");
                                }
                            }
                        }

                        while (!cancellationToken.IsCancellationRequested)
                        {
                            if (!sshClient!.IsConnected)
                            {
                                break;
                            }
                            await Task.Delay(100, cancellationToken);
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // ignore these as we have just cancelled the task.
                }

                foreach (var port in listeningPorts)
                {
                    var targetPort = targetEndpointPort == -1 ? port : targetEndpointPort;
                    _logger.LogInformation($"Stopped redirecting traffic from {ServiceName}:{port} to localhost:{targetPort}");
                }


                onStarted.TrySetResult(false); // already set so this'll no op!
            });

            this.handle = new DisposableHandle(async () =>
            {
                this.Status = "Stopping";
                this.handle = null;
                await cts2.CancelAsync();
                await task;

                // do not await `taskMonitor` as its a caller of this and will cause a deadlock!

                var svc = _serviceManager.GetRequiredService(this.ServiceName);
                await _serviceManager.Release(svc);
                this.Status = "Stopped";
            });

            return onStarted.Task;
        }

        public async Task Stop()
        {
            if (this.handle is not null)
            {
                await this.handle.DisposeAsync();
                this.handle = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Stop();
        }

        private class DisposableHandle : IAsyncDisposable
        {
            private Func<ValueTask> _dispose;

            public DisposableHandle(Func<ValueTask> dispose)
            {
                _dispose = dispose;
            }

            public ValueTask DisposeAsync()
            {
                return _dispose();
            }
        }
    }

}
