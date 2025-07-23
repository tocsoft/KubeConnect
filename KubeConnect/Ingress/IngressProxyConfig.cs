using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

            foreach (var ingress in manager.IngressConfig.Ingresses)
            {
                var pathPrefix = ingress.Path;
                var id = $"{ingress.HostName}{pathPrefix}";
                var route = new RouteConfig
                {
                    ClusterId = id,
                    RouteId = id,
                    Match = new RouteMatch
                    {
                        Hosts = new[] { ingress.HostName },
                        Path = $"{pathPrefix}/{{*catch-all}}"
                    }
                };
                route = route.WithTransformPathRemovePrefix(pathPrefix);
                route = route.WithTransformXForwarded(xFor: ForwardedTransformActions.Set, xHost: ForwardedTransformActions.Set, xProto: ForwardedTransformActions.Set, xPrefix: ForwardedTransformActions.Off);
                route = route.WithTransformRequestHeader("X-Forwarded-Prefix", pathPrefix);

                var serviceName = ingress.ServiceName;
                var servicePort = ingress.Port;
                var serviceDetails = manager.Services.Where(x => x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase)).Single();
                var targetIP = serviceDetails.AssignedAddress;
                var localPort = serviceDetails.TcpPorts.Where(x => x.destinationPort == servicePort).Single().listenPort;
                var cluster = new ClusterConfig
                {
                    ClusterId = id,
                    HttpClient = new HttpClientConfig
                    {
                        RequestHeaderEncoding = "utf-8"
                    },
                    HttpRequest = new Yarp.ReverseProxy.Forwarder.ForwarderRequestConfig
                    {
                        AllowResponseBuffering = false,
                        ActivityTimeout = Timeout.InfiniteTimeSpan,
                    },
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        [serviceName] = new DestinationConfig
                        {
                            Address = $"http://{targetIP}:{localPort}"
                        }
                    }
                };

                routes.Add(route);
                clusters.Add(cluster);
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
