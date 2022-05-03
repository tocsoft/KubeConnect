using k8s;
using k8s.Models;
using Renci.SshNet;
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

                IngressConfig = new IngressDetails
                {
                    AssignedAddress = ingressAddress,
                    UseSsl = args.UseSsl,
                    Ingresses = GetList().ToList(),
                };
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
                    TcpPorts = ports,
                    Bridge = mappings.Any(),
                    BridgedPorts = mappings.Select(x => (
                        x.RemotePort == -1 ? defaultPort.remote : x.RemotePort,
                        x.LocalPort == -1 ? defaultPort.local : x.LocalPort)).ToList(),
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
                services.Add(Create(bridge, address, s));
            }

            this.Services = services;
        }

        public ServiceDetails? GetService(string serviceName)
            => this.Services.SingleOrDefault(x => x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

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

        private async Task StartSshForward(ServiceDetails service)
        {
            var pod = await FindInterceptionPod(service);
            // clean up any old pods incase we have changes settigns
            if (pod != null)
            {
                await kubernetesClient.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace());
            }

            var ports = service.BridgedPorts.Select(X => new V1ContainerPort
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
                var client = new SshClient(service.ServiceName, "linuxserver.io", "password");
                try
                {
                    client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(4);
                    client.Connect();
                    foreach (var mappings in service.BridgedPorts)
                    {
                        var port = new ForwardedPortRemote(IPAddress.Any, (uint)mappings.remotePort, IPAddress.Loopback, (uint)mappings.localPort);
                        client.AddForwardedPort(port);
                        port.RequestReceived += (object? sender, Renci.SshNet.Common.PortForwardEventArgs e) =>
                        {
                            console.WriteLine($"Traffic redirected from {service.ServiceName}:{mappings.remotePort} to localhost:{mappings.localPort}");
                        };
                        port.Start();
                    }

                    // if they all started report it to the console
                    // maybe should be handled in program??
                    foreach (var mappings in service.BridgedPorts)
                    {
                        console.WriteLine($"Redirecting traffic from {service.ServiceName}:{mappings.remotePort} to localhost:{mappings.localPort}");
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

        public async Task Intercept(ServiceDetails service)
        {
            var dep = await FindMatchingDeployment(service);
            if (dep != null)
            {
                await DisableDeployment(dep);
            }
            await StartSshForward(service);
        }

        public async Task Release(ServiceDetails service)
        {
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
        }

        public async Task ReleaseAll()
        {
            foreach (var s in this.Services)
            {
                await Release(s);
            }
        }

        public async Task<IEnumerable<KeyValuePair<string, string>>> GetEnvironmentVariablesForServiceAsync(ServiceDetails service)
        {
            var results = await kubernetesClient.ListNamespacedDeploymentAsync(service.Namespace);
            var deployment = results.Items.Single(x => x.MatchTemplate(service));

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

            return fromCluster;
        }
    }

    public class IngressDetails
    {
        public bool Enabled => Ingresses?.Any() == true;

        public IPAddress AssignedAddress { get; init; } = IPAddress.None;

        public bool UseSsl { get; init; }

        public IReadOnlyList<IngressEntry> Ingresses { get; init; } = Array.Empty<IngressEntry>();

        public IEnumerable<string> HostNames => Ingresses.Select(X => X.HostName).Distinct();

        public IEnumerable<string> Addresses => Ingresses.Select(X => X.Address).Distinct();
    }

    public class IngressEntry
    {
        public string HostName { get; init; } = "";
        public string Address { get; init; } = "";
        public string ServiceName { get; init; } = "";
        public int Port { get; init; }
        public string Path { get; internal set; } = "";
    }

    public class ServiceDetails
    {
        public string ServiceName { get; init; } = string.Empty;

        public string Namespace { get; init; } = string.Empty;

        public IReadOnlyDictionary<string, string> Selector { get; init; } = new Dictionary<string, string>();

        public string StringSelector => string.Join(",", Selector.Select((s) => $"{s.Key}={s.Value}"));

        public IPAddress AssignedAddress { get; init; } = IPAddress.Any;

        public IReadOnlyList<(int listenPort, int destinationPort)> TcpPorts { get; init; } = Array.Empty<(int, int)>();

        public bool UpdateHostsFile { get; init; }

        public bool Bridge { get; init; }

        public IReadOnlyList<(int remotePort, int localPort)> BridgedPorts { get; init; } = Array.Empty<(int, int)>();
    }
}
