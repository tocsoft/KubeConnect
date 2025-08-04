using Aspire.Hosting.ApplicationModel;
using KubeConnect.Common;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Aspire.Hosting.KubeConnect;

internal class KubeConnectManager(
    DistributedApplicationModel appModel,
    ResourceNotificationService resourceNotificationService,
    ResourceLoggerService resourceLoggerService,
    KubeConnectClient connectClient) : IAsyncDisposable
{
    private KubeConnectResource _resource = null!;
    private DisposableHandle _monitor = null!;
    private ConcurrentDictionary<KubeConnectServiceResource, DisposableHandle> _logMonitors = new();

    private async Task BindLogs(KubeConnectServiceResource bound, ServiceDetailResult serviceDetail, CancellationToken cancellationToken)
    {
        if (_logMonitors.TryGetValue(bound, out var handle))
        {
            if (KnownResourceStates.TerminalStates.Contains(serviceDetail.Status) || serviceDetail.Status == "Bridged")
            {
                await handle.DisposeAsync();
            }
            return;
        }

        if (KnownResourceStates.TerminalStates.Contains(serviceDetail.Status) || serviceDetail.Status == "Bridged")
        {
            return; // we don't need to monitor logs for terminal states
        }

        var cts = new CancellationTokenSource();
        var task = Task.Run(async () =>
        {
            var stream = await connectClient.OpenServiceLogStream(bound.ServiceName);
            var logger = resourceLoggerService.GetLogger(bound);
            await stream.WriteToLogger(logger, cts.Token);
        });

        _logMonitors.TryAdd(bound,
            new DisposableHandle(async () =>
            {
                _logMonitors.TryRemove(bound, out var res);
                cts.Cancel();
                await task;
            }));
    }

    private async Task RefreshServices(CancellationToken cancellationToken)
    {
        try
        {
            var result = await connectClient.GetStatus();
            if (result is null)
            {
                throw new Exception("KubeConnect offline");
            }

            await resourceNotificationService.PublishUpdateAsync(_resource, s => s with
            {
                State = "Running",
                Urls = [.. result.IngressUrls.Select(x => new UrlSnapshot(null, x, false))]
            });

            var boundServices = appModel.Resources.OfType<KubeConnectServiceResource>().Where(x => x.Parent == _resource).ToList();

            _resource.TryGetLastAnnotation<KubeConnectResourceShowServicesAnnotation>(out var showServicesAnnotation);
            var showServices = showServicesAnnotation?.ShowServices ?? false;

            List<KubeConnectServiceResource> updatedServices = new List<KubeConnectServiceResource>();
            // configure know services and/or create new services as needed
            foreach (var svc in result.Services)
            {
                var bound = boundServices.FirstOrDefault(x => x.ServiceName.Equals(svc.ServiceName, StringComparison.OrdinalIgnoreCase));

                if (bound is null)
                {
                    string postfix = "-kubeconnect";
                    if (!appModel.Resources.Any(x => x.Name.Equals(svc.ServiceName, StringComparison.OrdinalIgnoreCase)))
                    {
                        postfix = string.Empty;
                    }
                    bound = new KubeConnectServiceResource(_resource, $"{svc.ServiceName}{postfix}", svc.ServiceName);
                    bound.Annotations.Add(ManifestPublishingCallbackAnnotation.Ignore);
                    bound.Annotations.Add(new DynamicServiceAnnotation());

                    appModel.Resources.Add(bound);
                    boundServices.Add(bound);

                    await resourceNotificationService.PublishUpdateAsync(bound, s => s with
                    {
                        State = KnownResourceStates.NotStarted,
                        Relationships = s.Relationships.RemoveAll(x => x.ResourceName == _resource.Name).Add(new RelationshipSnapshot(_resource.Name, "Parent")),
                        IsHidden = !showServices,
                        Urls = [],
                    });
                }

                var portList = svc.Urls.Select(x => new Uri(x).Port).ToList();
                if (!bound.TryGetLastAnnotation<KubeConnectServicePortsAnnotation>(out var portsAnn))
                {
                    portsAnn = new KubeConnectServicePortsAnnotation(portList);
                    bound.Annotations.Add(portsAnn);
                }
                else if (!portsAnn.Ports.SequenceEqual(portList))
                {
                    bound.Annotations.Remove(portsAnn);
                    portsAnn = new KubeConnectServicePortsAnnotation(portList);
                    bound.Annotations.Add(portsAnn);
                }

                await resourceNotificationService.PublishUpdateAsync(bound, s => s with
                {
                    State = svc.Status,
                    Urls = [.. svc.Urls.Select(x => new UrlSnapshot(null, x, false))]
                });

                if (showServices || !bound.TryGetAnnotationsOfType<DynamicServiceAnnotation>(out _))
                {
                    // all visible services need to have log monitoring
                    await BindLogs(bound, svc, cancellationToken);
                }
            }
        }
        catch
        {
            // error if the svc is down!
            await resourceNotificationService.PublishUpdateAsync(_resource, s => s with
            {
                State = KnownResourceStates.Waiting,
                Urls = [],
            });
            foreach (var s in appModel.Resources.OfType<KubeConnectServiceResource>().Where(x => x.Parent == _resource))
            {
                await resourceNotificationService.PublishUpdateAsync(s, s => s with
                {
                    State = KnownResourceStates.Waiting,
                    Urls = [],
                });
            }
        }
    }

    private record DynamicServiceAnnotation() : IResourceAnnotation
    { }

    private DisposableHandle Monitor()
    {
        var cancellation = new CancellationTokenSource();

        var t = Task.Run(async () =>
        {
            var token = cancellation.Token;
            while (!token.IsCancellationRequested)
            {
                await RefreshServices(token);
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
        });

        return new DisposableHandle(async () =>
        {
            await cancellation.CancelAsync();
            await t;
        });
    }

    public async Task InitKubeConnect(CancellationToken cancellationToken = default)
    {
        var res = appModel.Resources.OfType<KubeConnectResource>().SingleOrDefault();

        if (res is null)
        {
            return;
        }

        _resource = res;

        await resourceNotificationService.PublishUpdateAsync(_resource, s => s with
        {
            ResourceType = "KubeConnect",
            State = KnownResourceStates.Waiting,
        });
        // kube connect status api polling!!! 

        await RefreshServices(cancellationToken);
        _monitor = Monitor();
    }

    public async ValueTask DisposeAsync()
    {
        await _monitor.DisposeAsync();

        foreach (var m in _logMonitors.ToArray())
        {
            _logMonitors.TryRemove(m.Key, out _);
            await m.Value.DisposeAsync();
        }
        foreach (var m in _bridgeSessions.ToArray())
        {
            await m.Value.DisposeAsync();
        }
        _bridgeSessions.Clear();
    }

    ConcurrentDictionary<KubeConnectBridgeSession, DisposableHandle> _bridgeSessions = new();
    public async Task StartBridge(KubeConnectBridgeSession session)
    {
        if (_bridgeSessions.TryGetValue(session, out _))
        {
            return; // already started
        }
        Task task = Task.CompletedTask;
        CancellationTokenSource cts = new CancellationTokenSource();

        // handle stop vs dispose??
        var handle = new DisposableHandle(async () =>
        {
            session.Status = KnownResourceStates.Stopping;
            _bridgeSessions.TryRemove(session, out _);
            await cts.CancelAsync();
            await connectClient.StopBridge(session.Target.ServiceName);
            await task;
            session.Status = KnownResourceStates.Finished;
        });

        if (!_bridgeSessions.TryAdd(session, handle))
        {
            await handle.DisposeAsync();
            if (!_bridgeSessions.TryGetValue(session, out handle))
            {
                session.Status = KnownResourceStates.FailedToStart;
                throw new Exception("Race conditions kicked out ass we are are not able to start");
            }
            return;
        }
        task = Task.Run(async () =>
        {
            var logger = resourceLoggerService.GetLogger(session.Resource);
            session.Status = KnownResourceStates.Starting;
            int targetEndpointPort = -1;
            IEnumerable<int> svcPorts = [];
            while (!cts.IsCancellationRequested)
            {
                session.Resource.TryGetEndpoints(out var projectEndpoints);
                projectEndpoints ??= Enumerable.Empty<EndpointAnnotation>();

                targetEndpointPort = projectEndpoints.FirstOrDefault(x => x.UriScheme == "http")?.Port ?? -1;

                var svcFound = appModel.Resources.OfType<KubeConnectServiceResource>().Where(x => true == x.ServiceName?.Equals(session.Target.ServiceName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

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
                // we see if a brige is active 
                while (!cts.IsCancellationRequested)
                {
                    var bridgeStatus = await connectClient.GetBridgeStatus(session.Target.ServiceName);

                    if (bridgeStatus.Status == "Running")
                    {
                        session.Status = KnownResourceStates.Running;
                        break;
                    }

                    try
                    {

                        if (bridgeStatus.Status == "Offline" || bridgeStatus.LocalPort != targetEndpointPort)
                        {
                            if (bridgeStatus.Status != "Offline")
                            {
                                await connectClient.StopBridge(session.Target.ServiceName);
                            }

                            if (!await connectClient.StartBridge(session.Target.ServiceName, targetEndpointPort, svcPorts))
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
                cts.Token.ThrowIfCancellationRequested();

                var stream = await connectClient.OpenBridgeLogStream(session.Target.ServiceName);
                await stream.WriteToLogger(logger, cts.Token);
            }
        });
    }

    public async Task StopBridge(KubeConnectBridgeSession session)
    {
        if (_bridgeSessions.TryGetValue(session, out var handle))
        {
            await handle.DisposeAsync();
        }
    }
}
