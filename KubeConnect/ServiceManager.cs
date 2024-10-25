using k8s;
using k8s.Models;
using Microsoft.AspNetCore.SignalR;
using Renci.SshNet;
using Renci.SshNet.Messages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect
{
    public class ServiceManager
    {
        private readonly IKubernetes kubernetesClient;
        private readonly string @namespace;
        private readonly IConsole console;
        private readonly Args args;

        public ServiceManager(IKubernetes kubernetesClient, string @namespace, IConsole console, Args args)
        {
            this.kubernetesClient = kubernetesClient;
            this.@namespace = @namespace;
            this.console = console;
            this.args = args;
        }

        public IngressDetails IngressConfig { get; private set; } = new IngressDetails();

        public IEnumerable<ServiceDetails> Services { get; private set; } = Array.Empty<ServiceDetails>();
        public IEnumerable<BridgeDetails> BridgedServices { get; private set; } = Array.Empty<BridgeDetails>();

        public EventHandler<IEnumerable<BridgeDetails>>? OnBridgedServicesChanged;
        public EventHandler<IEnumerable<ServiceDetails>>? OnServicesChanged;
        public EventHandler<IngressDetails>? OnIngressChanged;

        // todo call this on a schedule?
        public async Task LoadBindings()
        {
            var deploymentsList = await kubernetesClient.ListNamespacedDeploymentAsync(@namespace);
            var serviceList = await kubernetesClient.ListNamespacedServiceAsync(@namespace);
            // assign IP Addresses
            List<IPAddress> usedAddresses = new List<IPAddress>(serviceList.Items.Count + 2);
            //remaining
            IPAddress NextAddress()
            {
                var ipCounter = 0;
                while (true)
                {
                    IPAddress address = IPAddress.Parse($"127.2.2.{ipCounter++}");
                    if (usedAddresses.Contains(address))
                    {
                        continue;
                    }
                    usedAddresses.Add(address);
                    return address;
                }
            }

            if (args.AllServices)
            {
                var ingressAddress = NextAddress();
                var ingressList = await kubernetesClient.ListNamespacedIngressAsync(@namespace);

                IEnumerable<IngressEntry> GetList()
                {
                    foreach (var ingress in ingressList.Items)
                    {
                        foreach (var r in ingress.Spec.Rules)
                        {
                            foreach (var p in r.Http.Paths)
                            {
                                var protocol = args.UseSsl ? "https" : "http";
                                yield return new IngressEntry
                                {
                                    Path = p.Path.TrimEnd('/'),
                                    Address = $"{protocol}://{r.Host}{p.Path}",
                                    HostName = r.Host,
                                    Port = p.Backend.Service.Port.Number ?? -1,
                                    ServiceName = p.Backend.Service.Name
                                };
                            }
                        }
                    }
                }
                var newDetails = new IngressDetails
                {
                    AssignedAddress = ingressAddress,
                    UseSsl = args.UseSsl,
                    Ingresses = GetList().ToList(),
                };

                if (IngressConfig != newDetails)
                {
                    IngressConfig = newDetails;
                    OnIngressChanged?.Invoke(this, newDetails);
                }
            }

            ServiceDetails Create(IEnumerable<Args.BridgeMapping>? mappings, IPAddress address, V1Service service)
            {
                mappings ??= Enumerable.Empty<Args.BridgeMapping>();

                var ports = service.Spec.Ports.Select(port => (remote: int.Parse(port.TargetPort?.Value ?? port.Port.ToString()), local: port.Port)).ToList();
                var defaultPort = ports.FirstOrDefault();

                return new ServiceDetails()
                {
                    ServiceName = service.Name(),
                    Namespace = service.Namespace(),
                    AssignedAddress = address,
                    Selector = new Dictionary<string, string>(service.Spec?.Selector ?? new Dictionary<string, string>()),
                    UpdateHostsFile = args.UpdateHosts,
                    TcpPorts = ports
                };
            }

            var services = new List<ServiceDetails>();
            // deal with mapped addresses first
            foreach (var s in serviceList.Items.OrderBy(X => X.Name()))
            {

                var mapping = args.Mappings.FirstOrDefault(x => x.ServiceName.Equals(s.Name(), StringComparison.OrdinalIgnoreCase));
                var bridge = args.BridgeMappings.Where(x => x.ServiceName.Equals(s.Name(), StringComparison.OrdinalIgnoreCase)).ToList();

                // track this one

                if (mapping == null && !bridge.Any() && !args.AllServices)
                {
                    continue;
                }

                var address = mapping?.Address ?? NextAddress();
                var serice = Create(bridge, address, s);
                services.Add(serice);

                PopulateEnvironmentVariablesForServiceAsync(serice, deploymentsList);
            }

            if (this.Services == null || this.Services.Count() != services.Count || this.Services.Intersect(services).Count() != services.Count)
            {
                this.Services = services;
                OnServicesChanged?.Invoke(this, services);
            }
        }

        public ServiceDetails? GetService(string serviceName)
            => this.Services.SingleOrDefault(x => x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
        public ServiceDetails GetRequiredService(string serviceName)
            => this.Services.SingleOrDefault(x => x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase)) ?? throw new Exception($"Failed to find service details named {serviceName}");

        private async Task<V1Deployment?> FindMatchingDeployment(ServiceDetails service)
        {
            var results = await kubernetesClient.ListNamespacedDeploymentAsync(service.Namespace);
            foreach (var dep in results.Items)
            {
                if (dep.MatchTemplate(service))
                {
                    return dep;
                }
            }

            return null;
        }
        private async Task<V1Pod?> FindInterceptionPod(ServiceDetails service)
        {
            var runningPods = (await kubernetesClient.ListNamespacedPodAsync(service.Namespace, labelSelector: "kubeconnect.bridge/ssh=true")).Items;
            foreach (var dep in runningPods)
            {
                if (dep.Match(service))
                {
                    return dep;
                }
            }

            return null;
        }

        private async Task DisableDeployment(V1Deployment deployment)
        {
            if (deployment.Spec.Replicas != 0)
            {
                deployment.Metadata.Annotations["kubeconnect.bridge/original_replicas"] = deployment.Spec.Replicas.ToString();
                deployment.Spec.Replicas = 0;
                await kubernetesClient.ReplaceNamespacedDeploymentAsync(deployment, deployment.Name(), deployment.Namespace(), fieldManager: "kubeconnect:bridge");
            }
        }

        private async Task EnableDeployment(V1Deployment deployment)
        {
            if (deployment.Spec.Replicas == 0 && deployment.Metadata.Annotations.TryGetValue("kubeconnect.bridge/original_replicas", out var orgReplicaCount))
            {
                if (int.TryParse(orgReplicaCount, out var target))
                {
                    deployment.Spec.Replicas = target;
                    deployment.Metadata.Annotations.Remove("kubeconnect.bridge/original_replicas");
                    await kubernetesClient.ReplaceNamespacedDeploymentAsync(deployment, deployment.Name(), deployment.Namespace(), fieldManager: "kubeconnect:bridge");
                }
            }
        }

        Task? logWriter;
        private void EnsureLogWriterRunning()
        {
            if (!BridgedServices.Any())
            {
                return;
            }

            if (logWriter is null)
            {
                logWriter = Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            foreach (var s in BridgedServices.ToList())
                            {
                                try
                                {
                                    await s.FlushLogs();
                                }
                                catch
                                {
                                    console.WriteErrorLine($"Failed to flush logs for {s.ServiceName} bridge");
                                }
                            }
                        }
                        catch
                        {
                        }
                        await Task.Delay(250);
                    }
                });
            }
        }

        private async Task StartSshForward(BridgeDetails bridgeDetails, ServiceDetails service)
        {
            EnsureLogWriterRunning();

            var logger = (string msg) =>
            {
                console.WriteLine(msg);

                bridgeDetails.Log(msg);
            };

            var pod = await FindInterceptionPod(service);
            // clean up any old pods incase we have changes settigns
            if (pod != null)
            {
                await kubernetesClient.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace());
            }

            var ports = bridgeDetails.BridgedPorts.Select(X => new V1ContainerPort
            {
                ContainerPort = X.remotePort,
                Name = $"port-{X.remotePort}"
            }).ToList();

            ports.Add(new V1ContainerPort
            {
                ContainerPort = 2222,
                Name = "ssh"
            });

            pod = new V1Pod()
            {
                Metadata = new V1ObjectMeta()
                {
                    Labels = new Dictionary<string, string>(service.Selector)
                    {
                        ["kubeconnect.bridge/ssh"] = "true"
                    },
                    Name = $"{service.ServiceName}-{Guid.NewGuid().ToString().Substring(0, 4)}-ssh",
                    NamespaceProperty = service.Namespace
                },
                Spec = new V1PodSpec()
                {
                    Containers = new List<V1Container>()
                                {
                                    new V1Container
                                    {
                                        Name = "ssh",
                                        Image = "linuxserver/openssh-server:latest",
                                        ImagePullPolicy = "IfNotPresent",
                                        Env = new List<V1EnvVar>
                                        {

                                            new V1EnvVar{ Name = "DOCKER_MODS", Value ="linuxserver/mods:openssh-server-ssh-tunnel" },
                                            new V1EnvVar{ Name = "SUDO_ACCESS", Value ="true" },
                                            new V1EnvVar{ Name = "PASSWORD_ACCESS", Value ="true" },
                                            new V1EnvVar{ Name = "USER_PASSWORD", Value = "password" },
                                        },
                                        Ports = ports
                                    }
                                }
                }
            };

            await kubernetesClient.CreateNamespacedPodAsync(pod, pod.Namespace());
            pod = await AwaitPodRunning(pod);
            await AwaitOnlyMatchingPodRunning(service, pod);
            // wait pod count to become 1
            var cts = new CancellationTokenSource(45000);
            while (!cts.IsCancellationRequested)
            {
                var client = new SshClient(service.ServiceName, 2222, "linuxserver.io", "password");
                try
                {
                    client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(4);
                    client.Connect();
                    foreach (var mappings in bridgeDetails.BridgedPorts)
                    {
                        var port = new ForwardedPortRemote(IPAddress.Any, (uint)mappings.remotePort, IPAddress.Loopback, (uint)mappings.localPort);
                        client.AddForwardedPort(port);
                        port.RequestReceived += (object? sender, Renci.SshNet.Common.PortForwardEventArgs e) =>
                        {
                            try
                            {
                                logger($"Traffic redirected from {service.ServiceName}:{mappings.remotePort} to localhost:{mappings.localPort}");
                            }
                            catch
                            {

                            }
                        };
                        port.Start();
                    }

                    // if they all started report it to the console
                    // maybe should be handled in program??
                    foreach (var mappings in bridgeDetails.BridgedPorts)
                    {
                        logger($"Redirecting traffic from {service.ServiceName}:{mappings.remotePort} to localhost:{mappings.localPort}");
                    }
                    break;
                }
                catch when (!cts.IsCancellationRequested)
                {
                    client?.Dispose();
                }
            }
        }

        private async Task AwaitOnlyMatchingPodRunning(ServiceDetails serviceDetails, V1Pod pod)
        {
            //wait for 30 seconds for it to start
            var cts = new CancellationTokenSource(30000);
            while (!cts.IsCancellationRequested)
            {
                var runningPods = (await kubernetesClient.ListNamespacedPodAsync(pod.Namespace())).Items;

                var otherPods = runningPods.Where(x => x.Match(serviceDetails) && !x.Name().Equals(pod.Name(), StringComparison.OrdinalIgnoreCase));

                if (!otherPods.Any())
                {
                    // only single the pod of interest exists that matches the service we can carry one
                    return;
                }
            }

            throw new Exception("Failed to shut down other deployments");
        }

        private async Task<V1Pod> AwaitPodRunning(V1Pod pod)
        {
            //wait for 30 seconds for it to start
            var cts = new CancellationTokenSource(30000);
            var lastPod = pod;
            while (!cts.IsCancellationRequested)
            {
                var runningPods = (await kubernetesClient.ListNamespacedPodAsync(pod.Namespace())).Items;
                lastPod = runningPods.SingleOrDefault(x => x.Name().Equals(pod.Name(), StringComparison.OrdinalIgnoreCase));
                if (lastPod?.Status.Phase == "Running")
                {
                    return lastPod;
                }
            }

            return lastPod ?? pod;
        }

        public async Task Intercept(ServiceDetails service, IReadOnlyList<(int remotePort, int localPort)> mappings, string connectionId, IClientProxy clientProxy)
        {
            if (BridgedServices.Any(x => x.ServiceName.Equals(service.ServiceName, StringComparison.OrdinalIgnoreCase)
                                        && x.Namespace.Equals(service.Namespace, StringComparison.OrdinalIgnoreCase)))
            {
                throw new Exception("Service already bridged");
            }

            var defaultPort = service.TcpPorts.FirstOrDefault();
            var bridgeDetails = new BridgeDetails()
            {
                ServiceName = service.ServiceName,
                Namespace = service.Namespace,
                ConnectionId = connectionId,
                Client = clientProxy,
                BridgedPorts = mappings.Select(x => (
                     x.remotePort == -1 ? defaultPort.listenPort : x.remotePort,
                     x.localPort == -1 ? defaultPort.destinationPort : x.localPort)).ToList()
            };

            BridgedServices = BridgedServices.Concat(new[] { bridgeDetails }).ToArray();
            OnBridgedServicesChanged?.Invoke(this, BridgedServices);

            var dep = await FindMatchingDeployment(service);
            if (dep != null)
            {
                await DisableDeployment(dep);
            }
            await StartSshForward(bridgeDetails, service);
        }

        private SemaphoreSlim semaphore = new SemaphoreSlim(1);
        public async Task Release(string connectionId)
        {
            await semaphore.WaitAsync();
            try
            {
                var services = this.BridgedServices.Where(x => x.ConnectionId == connectionId).ToList();
                foreach (var service in services)
                {
                    await Release(service);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        public Task Release(BridgeDetails bridgeDetails)
        {
            var service = this.Services.FirstOrDefault(x => x.ServiceName == bridgeDetails.ServiceName && x.Namespace == bridgeDetails.Namespace);
            if (service == null)
            {
                throw new Exception($"Unable to find the service '{bridgeDetails.ServiceName}'");
            }

            return Release(service);
        }

        public async Task Release(ServiceDetails service)
        {
            var details = BridgedServices.Where(x => x.ServiceName.Equals(service.ServiceName, StringComparison.OrdinalIgnoreCase) && x.Namespace.Equals(service.Namespace, StringComparison.OrdinalIgnoreCase)).ToList();

            if (details.Any())
            {
                BridgedServices = BridgedServices.Where(x => !details.Contains(x)).ToArray();
                console.WriteLine($"Shutting down bridge for {service.ServiceName}");
            }

            var pod = await FindInterceptionPod(service);
            if (pod != null)
            {
                await kubernetesClient.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace());
            }

            var dep = await FindMatchingDeployment(service);
            if (dep != null)
            {
                await EnableDeployment(dep);
            }
            if (details != null)
            {
                OnBridgedServicesChanged?.Invoke(this, BridgedServices);
            }
        }

        public async Task ReleaseAll()
        {
            foreach (var s in this.Services)
            {
                await Release(s);
            }
        }

        public void PopulateEnvironmentVariablesForServiceAsync(ServiceDetails service, V1DeploymentList deployments)
        {
            var deployment = deployments?.Items.FirstOrDefault(x => x.MatchTemplate(service));
            if (deployment == null)
            {
                service.EnvVars = Enumerable.Empty<KeyValuePair<string, string>>();
                return;
            }

            // TODO handle EnvFrom
            // TODO inject 'standard' kubernetes env vars for service discovery etc

            var fromCluster = deployment.Spec.Template.Spec.Containers.Single().Env.Select(x => new KeyValuePair<string, string>(x.Name, x.Value)).ToList();

            foreach (var envVar in args.EnvVars)
            {
                if (envVar.Mode == Args.EnvVarMapping.EnvVarMappingMode.Remove || envVar.Mode == Args.EnvVarMapping.EnvVarMappingMode.Replace)
                {
                    var toRemove = fromCluster.Where(x => x.Key == envVar.Name).ToArray();
                    foreach (var extra in toRemove)
                    {
                        fromCluster.Remove(extra);
                    }
                }

                if (envVar.Mode == Args.EnvVarMapping.EnvVarMappingMode.Append || envVar.Mode == Args.EnvVarMapping.EnvVarMappingMode.Replace)
                {
                    // set this one now
                    fromCluster.Add(new KeyValuePair<string, string>(envVar.Name, envVar.Value));
                }
            }

            service.EnvVars = fromCluster;
        }
    }
}
