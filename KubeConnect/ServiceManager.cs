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
        private V1ServiceList? serviceList;
        private V1IngressList? ingressList;
        private List<(V1Service Service, IPAddress IPAddress)> serviceAddresses;

        public IEnumerable<V1Ingress> IngressList => ingressList?.Items ?? Enumerable.Empty<V1Ingress>();
        public IEnumerable<string> IngressHostNames => IngressList?.SelectMany(x => x.Spec.Rules).Select(x => x.Host).Distinct() ?? Enumerable.Empty<string>();

        public IEnumerable<string> IngressAddresses
        {
            get
            {
                foreach (var ingress in IngressList)
                {
                    foreach (var r in ingress.Spec.Rules)
                    {
                        foreach (var p in r.Http.Paths)
                        {
                            var ssl = ingress.Spec.Tls?.Any(x => x.Hosts.Contains(r.Host)) == true;
                            var protocol = ssl ? "https" : "http";
                            yield return $"{protocol}://{r.Host}{p.Path}";
                        }
                    }
                }
            }

        }
        public bool HasIngressesDefined => IngressList?.Any() == true;

        public IPAddress IngressIPAddress { get; } = IPAddress.Parse($"127.2.2.1");

        public IEnumerable<(V1Service Service, IPAddress IPAddress)> ServiceAddresses => serviceAddresses ?? Enumerable.Empty<(V1Service Service, IPAddress IPAddress)>();

        public ServiceManager(IKubernetes kubernetesClient, string @namespace, IConsole console, Args args)
        {
            this.kubernetesClient = kubernetesClient;
            this.@namespace = @namespace;
            this.console = console;
            this.args = args;
        }

        public async Task LoadBindings()
        {
            serviceList = await kubernetesClient.ListNamespacedServiceAsync(@namespace);
            if (args.AllServices)
            {
                ingressList = await kubernetesClient.ListNamespacedIngressAsync(@namespace);
            }

            // assign IP Addresses
            serviceAddresses = new List<(V1Service Service, IPAddress IPAddress)>();
            List<IPAddress> usedAddresses = new List<IPAddress>(serviceList.Items.Count + 2) {
                IngressIPAddress
            };

            List<V1Service> serviceTomap = new List<V1Service>(serviceList.Items);

            // deal with mapped addresses first
            foreach (var s in serviceTomap.ToArray())
            {
                var mapping = args.Mappings.FirstOrDefault(x => x.ServiceName.Equals(s.Name(), StringComparison.OrdinalIgnoreCase));
                if (mapping == null)
                {
                    continue;
                }
                usedAddresses.Add(mapping.Address);
                serviceTomap.Remove(s);
                serviceAddresses.Add((s, mapping.Address));
            }

            if (args.AllServices)
            {
                //remaining
                IPAddress NextAddress()
                {
                    var ipCounter = 1;
                    while (true)
                    {
                        IPAddress address = IPAddress.Parse($"127.2.2.{ipCounter++}");
                        if (usedAddresses.Contains(address))
                        {
                            continue;
                        }
                        return address;
                    }
                }

                foreach (var s in serviceTomap)
                {
                    var address = NextAddress();
                    usedAddresses.Add(address);
                    serviceAddresses.Add((s, address));
                }
            }
        }
    }
}
