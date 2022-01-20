using k8s;
using k8s.Models;
using System;
using System.Collections.Generic;
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
        //private V1DeploymentList deploymentsList;
        //private V1ServiceList? serviceList;
        //private V1IngressList? ingressList;

        //public IEnumerable<V1Ingress> IngressList => ingressList?.Items ?? Enumerable.Empty<V1Ingress>();
        //public IEnumerable<string> IngressHostNames => IngressList?.SelectMany(x => x.Spec.Rules).Select(x => x.Host).Distinct() ?? Enumerable.Empty<string>();

        //public IEnumerable<string> IngressAddresses
        //{
        //    get
        //    {
        //        foreach (var ingress in IngressList)
        //        {
        //            foreach (var r in ingress.Spec.Rules)
        //            {
        //                foreach (var p in r.Http.Paths)
        //                {
        //                    var ssl = ingress.Spec.Tls?.Any(x => x.Hosts.Contains(r.Host)) == true;
        //                    var protocol = ssl ? "https" : "http";
        //                    yield return $"{protocol}://{r.Host}{p.Path}";
        //                }
        //            }
        //        }
        //    }
        //}

        //public bool HasIngressesDefined => IngressList?.Any() == true;

        //public IPAddress IngressIPAddress { get; }

        //public IEnumerable<(V1Service Service, IPAddress IPAddress)> ServiceAddresses => serviceAddresses ?? Enumerable.Empty<(V1Service Service, IPAddress IPAddress)>();
        //public IEnumerable<V1Service> Services => serviceList?.Items ?? Enumerable.Empty<V1Service>();
        //public IEnumerable<V1Deployment> Deployments => deploymentsList?.Items ?? Enumerable.Empty<V1Deployment>();

        public ServiceManager(IKubernetes kubernetesClient, string @namespace, IConsole console, Args args)
        {
            this.kubernetesClient = kubernetesClient;
            this.@namespace = @namespace;
            this.console = console;
            this.args = args;
        }

        public IngressDetails IngressConfig { get; private set; } = new IngressDetails();
        public List<ServiceDetails> Services { get; private set; }

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

            var serviceTomap = new List<V1Service>(serviceList.Items);
            var serviceAddresses = new List<(V1Service Service, IPAddress IPAddress)>();
            var services = new List<ServiceDetails>();
            // deal with mapped addresses first
            foreach (var s in serviceTomap.ToArray())
            {
                var mapping = args.Mappings.FirstOrDefault(x => x.ServiceName.Equals(s.Name(), StringComparison.OrdinalIgnoreCase));
                var bridge = args.BridgeMappings.Where(x => x.ServiceName.Equals(s.Name(), StringComparison.OrdinalIgnoreCase)).ToList();
                if (mapping == null && !bridge.Any())
                {
                    continue;
                }

                var address = mapping?.Address ?? NextAddress();
                serviceTomap.Remove(s);
                services.Add(Create(bridge, address, s));
            }

            if (args.AllServices)
            {
                foreach (var s in serviceTomap)
                {
                    var address = NextAddress();
                    services.Add(Create(null, address, s));
                }
            }

            this.Services = services;
        }
    }

    public class IngressDetails
    {
        public bool Enabled => Ingresses?.Any() == true;

        public IPAddress AssignedAddress { get; init; }

        public bool UseSsl { get; init; }

        public IReadOnlyList<IngressEntry> Ingresses { get; init; }

        public IEnumerable<string> HostNames => Ingresses.Select(X => X.HostName).Distinct();

        public IEnumerable<string> Addresses => Ingresses.Select(X => X.Address).Distinct();
    }

    public class IngressEntry
    {
        public string HostName { get; init; }
        public string Address { get; init; }
        public string ServiceName { get; init; }
        public int Port { get; init; }
        public string Path { get; internal set; }
    }

    public class ServiceDetails
    {
        public string ServiceName { get; init; }

        public string Namespace { get; init; }

        public IReadOnlyDictionary<string, string> Selector { get; init; }

        public IPAddress AssignedAddress { get; init; }

        public IReadOnlyList<(int listenPort, int destinationPort)> TcpPorts { get; init; }

        public bool UpdateHostsFile { get; init; }

        // mean that an additional portforward is added 
        // the deployment is 
        public bool Bridge { get; init; }

        public IReadOnlyList<(int remotePort, int localPort)> BridgedPorts { get; init; }
    }
}
