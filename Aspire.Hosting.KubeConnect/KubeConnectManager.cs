using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Humanizer.Localisation;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Collections.Concurrent;
using System.Net;

namespace Aspire.Hosting.KubeConnect;

internal class KubeConnectManager(
    DistributedApplicationExecutionContext executionContext,
    ResourceNotificationService resourceNotificationService,
    ResourceLoggerService resourceLoggerService) : IAsyncDisposable
{
    // private string _namespace;
    private KubeConnectResource _resource = null!;
    private DisposableHandle _monitor = null!;
    private ConcurrentDictionary<KubeConnectServiceResource, (V1Pod pod, DisposableHandle handle)> _logMonitors = new();

    // monitor loop

    // foreach service see if we are bound!
    private async Task<V1Pod?> TargetPod(ICoreV1Operations client, V1Service service, CancellationToken cancellationToken)
    {
        var sel = string.Join(",", service?.Spec?.Selector?.Select((s) => $"{s.Key}={s.Value}") ?? []);
        if (string.IsNullOrEmpty(sel))
        {
            return null;
        }

        var pods = await client.ListNamespacedPodAsync(service.Namespace(), labelSelector: sel);
        var pod = pods.Items.Where(x => x.Status.Phase == "Running").FirstOrDefault();
        pod ??= pods.Items.FirstOrDefault();
        return pod;
    }

    private async Task BindLogs(ICoreV1Operations client, KubeConnectServiceResource bound, V1Pod? pod, CancellationToken cancellationToken)
    {
        if (_logMonitors.TryGetValue(bound, out var handle))
        {
            if (handle.pod.Name() == pod?.Name())
            {
                //already bound!
                return;
            }
            await handle.handle.DisposeAsync();
        }

        if (pod is null)
        {
            if (_logMonitors.TryRemove(bound, out var e))
            {
                await e.handle.DisposeAsync();
            }
            return;
        }

        var cts = new CancellationTokenSource();
        var task = Task.Run(async () =>
        {
            var stream = await client.ReadNamespacedPodLogAsync(pod.Name(), pod.Namespace(), follow: true);
            using var sr = new StreamReader(stream);
            var logger = resourceLoggerService.GetLogger(bound);
            while (!cts.Token.IsCancellationRequested)
            {
                var line = await sr.ReadLineAsync(cts.Token);
                logger.LogInformation(line);
            }
        });

        _logMonitors.TryAdd(bound,
            (
                pod,
                new DisposableHandle(async () =>
                {
                    if (_logMonitors.TryRemove(bound, out var res))
                    {
                        if (handle.pod.Name() != pod?.Name())
                        {
                            _logMonitors.TryAdd(bound, res); // readd as it wasn't my entry
                        }
                    }
                    cts.Cancel();
                    await task;
                }))
                );
    }

    private async Task RefreshServices(DistributedApplicationModel appModel, CancellationToken cancellationToken)
    {
        var (kubernetesClient, ns) = await Connect(cancellationToken);
        if (kubernetesClient is null)
        {
            await resourceNotificationService.PublishUpdateAsync(_resource, s => s with
            {
                State = "Stopped",
                // Urls = [.. urls.Select(u => new UrlSnapshot(u, u, IsInternal: false))]
            });
            foreach (var s in appModel.Resources.OfType<KubeConnectServiceResource>().Where(x => x.Parent == _resource))
            {
                await resourceNotificationService.PublishUpdateAsync(s, s => s with
                {
                    State = "Stopped",
                    // Urls = [.. urls.Select(u => new UrlSnapshot(u, u, IsInternal: false))]
                });
            }
            return;
        }

        bool isRunning = false;
        try
        {
            var response = await new HttpClient()
                .GetAsync($"http://localhost:{_resource.MainPort}/status");
            isRunning = response.IsSuccessStatusCode;
        }
        catch
        {
            isRunning = false;
        }
        if (!isRunning)
        {
            await resourceNotificationService.PublishUpdateAsync(_resource, s => s with
            {
                State = "Waiting",
                // Urls = [.. urls.Select(u => new UrlSnapshot(u, u, IsInternal: false))]
            });
        }
        else
        {
            var ingressList = await kubernetesClient.ListNamespacedIngressAsync(ns);

            var urls = ingressList.Items
                .SelectMany(ingress => ingress.Spec.Rules.SelectMany(r =>
                    r.Http.Paths.Select(p => new UrlSnapshot(null, $"https://{r.Host}{p.Path}", false))
                ));

            await resourceNotificationService.PublishUpdateAsync(_resource, s => s with
            {
                State = "Running",
                Urls = [.. urls]
            });
        }

        var serviceList = await kubernetesClient.ListNamespacedServiceAsync(ns, cancellationToken: cancellationToken);
        var filteredServices = serviceList.Items.Where(x => x.GetAnnotation("provider") != "kubernetes"); // hide systemish services

        // look for matching services in the 
        var boundServices = appModel.Resources.OfType<KubeConnectServiceResource>().Where(x => x.Parent == _resource).ToList();

        _resource.TryGetLastAnnotation<KubeConnectResourceShowServicesAnnotation>(out var showServicesAnnotation);
        var showServices = showServicesAnnotation?.ShowServices ?? false;

        List<KubeConnectServiceResource> updatedServices = new List<KubeConnectServiceResource>();
        // configure know services and/or create new services as needed
        foreach (var svc in filteredServices)
        {
            var bound = boundServices.FirstOrDefault(x => x.ServiceName.Equals(svc.Name(), StringComparison.OrdinalIgnoreCase));

            if (bound is null)
            {
                bound = new KubeConnectServiceResource(_resource, $"{svc.Name()}-k8s", svc.Name());
                bound.Annotations.Add(ManifestPublishingCallbackAnnotation.Ignore);
                bound.Annotations.Add(new DynamicServiceAnnotation());

                appModel.Resources.Add(bound);
                boundServices.Add(bound);

                await resourceNotificationService.PublishUpdateAsync(bound, s => s with
                {
                    State = !isRunning ? "Waiting" : "Stopped",
                    Relationships = s.Relationships.RemoveAll(x => x.ResourceName == _resource.Name).Add(new RelationshipSnapshot(_resource.Name, "Parent")),
                    IsHidden = !showServices,
                    Urls = [],
                });
            }
            updatedServices.Add(bound);

            var pod = await TargetPod(kubernetesClient, svc, cancellationToken);

            await BindLogs(kubernetesClient, bound, pod, cancellationToken);

            var ports = svc.Spec.Ports.Select(x => new UrlSnapshot(x.Name, $"{(x.Port == 80 ? "http://" : "")}{svc.Name()}:{x.Port}", false));

            if (!bound.Annotations.OfType<KubeConnectServicePortsAnnotation>().Any())
            {
                bound.Annotations.Add(new KubeConnectServicePortsAnnotation(svc.Spec.Ports.Select(x => x.Port).ToList()));
            }

            // we need to proxy the services and when we do we then can update the urls!
            await resourceNotificationService.PublishUpdateAsync(bound, s => s with
            {
                State = !isRunning ? "Waiting" : pod?.Status?.Phase ?? "Stopped",
                Relationships = s.Relationships.RemoveAll(x => x.ResourceName == _resource.Name).Add(new RelationshipSnapshot(_resource.Name, "Parent")),
                Urls = [.. ports]
            });
        }

        foreach (var svc in boundServices.Except(updatedServices))
        {
            await resourceNotificationService.PublishUpdateAsync(svc, s => s with
            {
                State = "Stopped",
                Relationships = s.Relationships.RemoveAll(x => x.ResourceName == _resource.Name).Add(new RelationshipSnapshot(_resource.Name, "Parent")),
                //IsHidden = svc.HasAnnotationOfType<DynamicServiceAnnotation>(),
                // Urls = [.. urls.Select(u => new UrlSnapshot(u, u, IsInternal: false))]
            });
        }
    }
    private record DynamicServiceAnnotation() : IResourceAnnotation
    { }

    private DisposableHandle Monitor(DistributedApplicationModel appModel)
    {
        var cancellation = new CancellationTokenSource();

        var t = Task.Run(async () =>
        {
            var token = cancellation.Token;
            while (!token.IsCancellationRequested)
            {
                await RefreshServices(appModel, token);
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
        });

        return new DisposableHandle(async () =>
        {
            await cancellation.CancelAsync();
            await t;
        });
    }

    public async Task<(Kubernetes? client, string ns)> Connect(CancellationToken cancellationToken)
    {
        try
        {
            var configPath = executionContext.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<string?>("KUBECONFIG", _resource.KubeConfigFile);

            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(_resource.KubeConfigFile, _resource.ContextName);
            if (_resource.Namespace != null)
            {
                config.Namespace = _resource.Namespace ?? config.Namespace;
            }
            if (string.IsNullOrWhiteSpace(config.Namespace))
            {
                config.Namespace = "default";
            }

            Kubernetes kubernetesClient = new Kubernetes(config);

            var resource = await kubernetesClient.CoreV1.GetAPIResourcesAsync(cancellationToken);
            return (kubernetesClient, config.Namespace);
        }
        catch
        {
            return (null, "");
        }
    }

    public async Task InitKubeConnect(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
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
            State = "Starting"
        });
        // kube connect status api polling!!! 

        await RefreshServices(appModel, cancellationToken);
        _monitor = Monitor(appModel);


        // fix up the kubeconnectbridge resources
        // we need to drop the https urls for projects (as only http work for now)

        // we need to monitor the status of the project, and while its running we need to ensure we stop/start the ssh port forawrding as required.




        // validate kubeconnect is running!
        // we have 2/3 things to tackle here!

        // how do we handle multiple processes all wanting to proxy at once, do we just require localdev/kubeconnect to already be running and this just manages the ssh/bridging bit?
    }

    public async ValueTask DisposeAsync()
    {
        await _monitor.DisposeAsync();

        foreach (var m in _logMonitors.ToArray())
        {
            _logMonitors.TryRemove(m.Key, out _);
            await m.Value.handle.DisposeAsync();
        }
    }


    //ConcurrentDictionary<ProjectResource, DisposableHandle> bridgeSessionHandles = new();

    //public async Task StartBridge(ProjectResource resource, KubeConnectBridgeAnnotation target)
    //{
    //    if (bridgeSessionHandles.TryGetValue((resource, target), out _))
    //    {
    //        //already running;
    //        return;
    //    }

    //    var logger = resourceLoggerService.GetLogger(resource);

    //    var cts = new CancellationTokenSource();
    //    var task = Task.Run(async () =>
    //    {
    //        SshClient sshClient = null!;
    //        cts.Token.Register(() =>
    //        {
    //            sshClient?.Disconnect();
    //            sshClient?.Dispose();
    //        });

    //        while (!cts.IsCancellationRequested)
    //        {
    //            await resourceNotificationService.PublishUpdateAsync(resource, s => s with
    //            {
    //                ResourceType = "KubeConnect",
    //                State = "Starting"
    //            });

    //            int targetEndpointPort = -1;
    //            IEnumerable<int> svcPorts = Enumerable.Empty<int>();
    //            // wait for service details
    //            while (!cts.IsCancellationRequested)
    //            {
    //                resource.Parent.TryGetEndpoints(out var projectEndpoints);
    //                projectEndpoints ??= Enumerable.Empty<EndpointAnnotation>();

    //                targetEndpointPort = projectEndpoints.FirstOrDefault(x => x.UriScheme == "http")?.Port ?? -1;

    //                var svcFound = distributedApplicationModel.Resources.OfType<KubeConnectServiceResource>().Where(x => true == x.ServiceName?.Equals(resource.ServiceName, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

    //                // we wait for a svc to bve availible
    //                if (svcFound is null)
    //                {
    //                    await Task.Delay(100);
    //                    continue;
    //                }

    //                if (!svcFound.TryGetLastAnnotation<KubeConnectServicePortsAnnotation>(out var svcPortsAnnotation))
    //                {
    //                    await Task.Delay(100);
    //                    continue;
    //                }

    //                svcPorts = svcPortsAnnotation.Ports;
    //                if (!svcPorts.Any())
    //                {
    //                    // no ports to forward!
    //                    await Task.Delay(100);
    //                    continue;
    //                }
    //                break;
    //            }

    //            while (!cts.IsCancellationRequested)
    //            {
    //                sshClient = new SshClient(resource.ServiceName, 2222, "linuxserver.io", "password");
    //                try
    //                {
    //                    sshClient.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
    //                    sshClient.Connect();

    //                    // we go find the upstream k8s 
    //                    // find the service details from k8s
    //                    foreach (var port in svcPorts)
    //                    {
    //                        var targetPort = targetEndpointPort == -1 ? port : targetEndpointPort;
    //                        var portFwd = new ForwardedPortRemote(IPAddress.Any, (uint)port, IPAddress.Loopback, (uint)targetPort);
    //                        sshClient.AddForwardedPort(portFwd);
    //                        portFwd.RequestReceived += (object? sender, Renci.SshNet.Common.PortForwardEventArgs e) =>
    //                        {
    //                            try
    //                            {
    //                                logger.LogInformation($"Traffic redirected from {resource.ServiceName}:{port} to localhost:{targetPort}");
    //                            }
    //                            catch
    //                            {

    //                            }
    //                        };
    //                        portFwd.Start();
    //                        logger.LogInformation($"Redirecting traffic from {resource.ServiceName}:{port} to localhost:{targetPort}");
    //                    }

    //                    await resourceNotificationService.PublishUpdateAsync(resource, s => s with
    //                    {
    //                        ResourceType = "KubeConnect",
    //                        State = "Running"
    //                    });
    //                    // in the monitoring loop now
    //                    break;
    //                }
    //                catch (SshOperationTimeoutException)
    //                {
    //                    sshClient?.Disconnect();
    //                    logger.LogWarning($"Failed to connect to ssh server, waiting for kubeconnect and/or ssh server to startup.");
    //                }
    //                catch (Exception ex)
    //                {
    //                    sshClient?.Disconnect();
    //                    logger.LogWarning(ex, $"Failed to connect to ssh server, kubeconnect might not be running, or ssh server is still starting.");
    //                }
    //            }

    //            while (!cts.IsCancellationRequested)
    //            {
    //                if (!sshClient.IsConnected)
    //                {
    //                    break;
    //                }
    //                await Task.Delay(100, cts.Token);
    //            }
    //        }
    //    });

    //    bridgeSessionHandles.TryAdd(resource, new DisposableHandle(async () =>
    //    {
    //        await resourceNotificationService.PublishUpdateAsync(resource, s => s with
    //        {
    //            ResourceType = "KubeConnect",
    //            State = "Stopped"
    //        });

    //        bridgeSessionHandles.TryRemove(resource, out _);
    //        await cts.CancelAsync();
    //        await task;
    //    }));
    //}

    //public async Task StopBridge(KubeConnectBridgeResource resource)
    //{
    //    if (bridgeSessionHandles.TryGetValue(resource, out var handle))
    //    {
    //        await handle.DisposeAsync();
    //    }
    //}

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
