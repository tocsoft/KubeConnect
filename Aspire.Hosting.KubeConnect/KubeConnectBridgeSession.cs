using Aspire.Hosting.ApplicationModel;
using Humanizer.Localisation;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Collections.Generic;
using System.Net;

namespace Aspire.Hosting.KubeConnect;

public class KubeConnectBridgeSessionSet : IAsyncDisposable, IResourceAnnotation
{
    private List<KubeConnectBridgeSession> sessions = new List<KubeConnectBridgeSession>();

    public Action<KubeConnectBridgeSessionSet>? OnStatusUpdate { get; set; }

    public ProjectResource Resource { get; init; }

    public string Status
    {
        get
        {
            if (sessions.All(x => x.Status == "Running"))
            {
                return "Running";
            }
            else if (sessions.All(x => x.Status == "Stopped"))
            {
                return "Stopped";
            }
            else
            {
                return "Starting";
            }
        }
    }

    public KubeConnectBridgeSessionSet(DistributedApplicationModel appModel, ILogger logger, ProjectResource project, IEnumerable<KubeConnectBridgeAnnotation> targets)
    {
        Resource = project;
        foreach (var t in targets)
        {
            var session = new KubeConnectBridgeSession(appModel, logger, project, t);
            session.OnStatusUpdate = (s) =>
            {
                OnStatusUpdate?.Invoke(this);
            };
            sessions.Add(session);
        }
    }

    public Task Start()
        => Task.WhenAll(sessions.Select(s => s.Start()));

    public Task Stop()
        => Task.WhenAll(sessions.Select(s => s.Stop()));

    public async ValueTask DisposeAsync()
    {
        foreach (var s in sessions)
        {
            await s.DisposeAsync();
        }

        sessions.Clear();
    }
}

public class KubeConnectBridgeSession : IAsyncDisposable
{
    public KubeConnectBridgeSession(DistributedApplicationModel appModel, ILogger logger, ProjectResource project, KubeConnectBridgeAnnotation target)
    {
        Resource = project;
        Target = target;
        _logger = logger;
        _appModel = appModel;
    }

    public ProjectResource Resource { get; init; }

    public KubeConnectBridgeAnnotation Target { get; init; }

    private ILogger _logger;
    private string _status = "Stopped";
    private DisposableHandle? handle = null;
    private DistributedApplicationModel _appModel;

    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            OnStatusUpdate?.Invoke(this);
        }
    }

    public Action<KubeConnectBridgeSession>? OnStatusUpdate { get; set; }

    public Task Start()
    {
        if (handle is not null)
        {
            return Task.CompletedTask;
        }

        this.Status = "Starting";


        var cts = new CancellationTokenSource();
        var task = Task.Run(async () =>
        {
            SshClient sshClient = null!;
            cts.Token.Register(() =>
            {
                sshClient?.Disconnect();
                sshClient?.Dispose();
            });

            int targetEndpointPort = -1;
            List<int> listeningPorts = new();
            try
            {
                IEnumerable<int> svcPorts = Enumerable.Empty<int>();
                while (!cts.IsCancellationRequested)
                {
                    this.Status = "Starting";

                    // wait for service details
                    while (!cts.IsCancellationRequested)
                    {
                        Resource.TryGetEndpoints(out var projectEndpoints);
                        projectEndpoints ??= Enumerable.Empty<EndpointAnnotation>();

                        targetEndpointPort = projectEndpoints.FirstOrDefault(x => x.UriScheme == "http")?.Port ?? -1;

                        var svcFound = _appModel.Resources.OfType<KubeConnectServiceResource>().Where(x => true == x.ServiceName?.Equals(Target.ServiceName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                        // we wait for a svc to bve availible
                        if (svcFound is null)
                        {
                            await Task.Delay(100);
                            continue;
                        }

                        if (!svcFound.TryGetLastAnnotation<KubeConnectServicePortsAnnotation>(out var svcPortsAnnotation))
                        {
                            await Task.Delay(100);
                            continue;
                        }

                        svcPorts = svcPortsAnnotation.Ports;
                        if (!svcPorts.Any())
                        {
                            // no ports to forward!
                            await Task.Delay(100);
                            continue;
                        }
                        break;
                    }

                    while (!cts.IsCancellationRequested)
                    {
                        sshClient = new SshClient(Target.ServiceName, 2222, "linuxserver.io", "password");
                        try
                        {
                            sshClient.ConnectionInfo.Timeout = TimeSpan.FromSeconds(5);
                            sshClient.Connect();

                            // we go find the upstream k8s 
                            // find the service details from k8s
                            foreach (var port in svcPorts)
                            {
                                var targetPort = targetEndpointPort == -1 ? port : targetEndpointPort;
                                var portFwd = new ForwardedPortRemote(IPAddress.Any, (uint)port, IPAddress.Loopback, (uint)targetPort);
                                sshClient.AddForwardedPort(portFwd);
                                portFwd.RequestReceived += (object? sender, Renci.SshNet.Common.PortForwardEventArgs e) =>
                                {
                                    try
                                    {
                                        _logger.LogInformation($"Traffic redirected from {Target.ServiceName}:{port} to localhost:{targetPort}");
                                    }
                                    catch
                                    {

                                    }
                                };
                                portFwd.Start();
                                _logger.LogInformation($"Redirecting traffic from {Target.ServiceName}:{port} to localhost:{targetPort}");
                                listeningPorts.Add(port);
                            }
                            this.Status = "Running";
                            // in the monitoring loop now
                            break;
                        }
                        catch (SshOperationTimeoutException)
                        {
                            sshClient?.Disconnect();
                            _logger.LogWarning($"Failed to connect to ssh server, waiting for kubeconnect and/or ssh server to startup.");
                        }
                        catch (SshConnectionException)
                        {
                            sshClient?.Disconnect();
                            _logger.LogWarning($"Failed to connect to ssh server, waiting for kubeconnect and/or ssh server to startup.");
                        }
                        catch (Exception ex)
                        {
                            sshClient?.Disconnect();
                            _logger.LogWarning(ex, $"Failed to connect to ssh server, kubeconnect might not be running, or ssh server is still starting.");
                        }
                    }

                    while (!cts.IsCancellationRequested)
                    {
                        if (!sshClient!.IsConnected)
                        {
                            break;
                        }
                        await Task.Delay(100, cts.Token);
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
                _logger.LogInformation($"Stopped redirecting traffic from {Target.ServiceName}:{port} to localhost:{targetPort}");
            }
        });

        this.handle = new DisposableHandle(async () =>
        {
            this.Status = "Stopping";
            this.handle = null;
            await cts.CancelAsync();
            await task;
            this.Status = "Stopped";
        });

        return Task.CompletedTask;
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
