using k8s.Models;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace KubeConnect.PortForwarding
{
    public class PortForwarderHandlerSetup : IConfigureOptions<KestrelServerOptions>
    {
        private readonly ServiceManager manager;
        private readonly ILogger<PortForwarderHandlerSetup> logger;
        private readonly IServiceProvider serviceProvider;
        private readonly Args args;

        public PortForwarderHandlerSetup(ServiceManager manager, ILogger<PortForwarderHandlerSetup> logger, IServiceProvider serviceProvider, Args args)
        {
            this.manager = manager;
            this.logger = logger;
            this.serviceProvider = serviceProvider;
            this.args = args;
        }

        public void Configure(KestrelServerOptions options)
        {
            options.ApplicationServices = serviceProvider;

            if (manager.IngressConfig.Enabled)
            {
                options.Listen(manager.IngressConfig.AssignedAddress, 80);

                if (manager.IngressConfig.UseSsl)
                {
                    options.Listen(manager.IngressConfig.AssignedAddress, 443, builder =>
                    {
                        builder.UseHttps(CertificateHelper.CreateCertificate(manager.IngressConfig.HostNames));
                        builder.Protocols = HttpProtocols.Http1AndHttp2;
                    });
                }

                if (args.UpdateHosts)
                {
                    foreach (var address in manager.IngressConfig.Addresses)
                    {

                        logger.LogInformation($"Listening to {address}");
                    }
                }
                else
                {
                    logger.LogInformation($"Listening to http://{manager.IngressConfig.AssignedAddress}");
                }
            }

            foreach (var service in manager.Services)
            {
                var serviceSelector = service.StringSelector;
                
                // no real harm in always doing this
                // add in the ssh port to allow for bridge injection
                var sshendpoint = new IPEndPoint(service.AssignedAddress, 22);
                options.Listen(sshendpoint, builder =>
                {
                    var binding = new PortBinding()
                    {
                        Name = service.ServiceName,
                        Namespace = service.Namespace,
                        TargetPort = 2222,
                        Selector = $"{serviceSelector},kubeconnect.bridge/ssh=true" // onlly support finding ssh server for this particular port forward
                    };

                    builder.Use(next =>
                    {
                        return async (context) =>
                        {
                            context.Features.Set(binding);
                            await next(context);
                        };
                    });

                    builder.UseConnectionHandler<PortForwardingConnectionHandler>();
                });

                foreach (var portDetails in service.TcpPorts)
                {
                    var endpoint = new IPEndPoint(service.AssignedAddress, portDetails.listenPort);

                    options.Listen(endpoint, builder =>
                    {
                        if (args.UpdateHosts)
                        {
                            logger.LogInformation("Forwarding {Service} to tcp://{Service}:{Port}", service.ServiceName, service.ServiceName, portDetails.destinationPort);
                        }
                        else
                        {
                            logger.LogInformation("Forwarding {Service} to tcp://{Endpoint}", service.ServiceName, endpoint);
                        }

                        var binding = new PortBinding()
                        {
                            Name = service.ServiceName,
                            Namespace = service.Namespace,
                            TargetPort = portDetails.destinationPort,
                            Selector = serviceSelector
                        };

                        builder.Use(next =>
                        {
                            return async (context) =>
                            {
                                context.Features.Set(binding);
                                await next(context);
                            };
                        });

                        builder.UseConnectionHandler<PortForwardingConnectionHandler>();
                    });
                }
            }
        }

    }
}
