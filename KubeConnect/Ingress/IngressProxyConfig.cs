using k8s;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace KubeConnect.Ingress
{
    public class IngressProxyConfig : IProxyConfigProvider, IProxyConfig
    {
        private readonly ServiceManager manager;

        public IngressProxyConfig(ServiceManager manager)
        {
            this.manager = manager;

            var routes = new List<RouteConfig>();
            var clusters = new List<ClusterConfig>();

            foreach (var ingress in manager.IngressList.Items)
            {
                foreach (var r in ingress.Spec.Rules)
                {
                    foreach (var p in r.Http.Paths)
                    {
                        var id = $"{r.Host}{p.Path}";
                        var route = new RouteConfig
                        {
                            ClusterId = id,
                            RouteId = id,
                            Match = new RouteMatch
                            {
                                Hosts = new[] { r.Host },
                                Path = $"{p.Path.TrimEnd('/')}/{{*catch-all}}"
                            }
                        };
                        route.WithTransformPathRemovePrefix(p.Path);
                        route.WithTransformXForwarded();

                        var serviceName = p.Backend.Service.Name;
                        var servicePort = p.Backend.Service.Port.Number;

                        var cluster = new ClusterConfig
                        {
                            ClusterId = id,
                            Destinations = new Dictionary<string, DestinationConfig>
                            {
                                [serviceName] = new DestinationConfig
                                {
                                    Address = $"http://{serviceName}:{servicePort}"
                                }
                            }
                        };

                        routes.Add(route);
                        clusters.Add(cluster);
                    }
                }
            }

            Routes = routes;
            Clusters = clusters;
        }

        public IReadOnlyList<RouteConfig> Routes { get; }

        public IReadOnlyList<ClusterConfig> Clusters { get; }

        public IChangeToken ChangeToken
            => NullChangeToken.Singleton;

        public IProxyConfig GetConfig()
            => this;

    }
}
