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

            if (manager.IngressList.Any())
            {
                options.Listen(manager.IngressIPAddress, 80);

                if (args.UseSsl)
                {
                    options.Listen(manager.IngressIPAddress, 443, builder =>
                    {
                        builder.UseHttps(CertificateHelper.CreateCertificate(manager.IngressHostNames));
                    });
                }

                if (args.UpdateHosts)
                {
                    foreach (var ingress in manager.IngressAddresses)
                    {
                        var finalUrl = ingress;
                        if (!args.UseSsl)
                        {
                            finalUrl = finalUrl.Replace("https://", "http://");
                        }
                        logger.LogInformation($"Listening to {finalUrl}");
                    }
                }
                else
                {
                    logger.LogInformation($"Listening to http://{manager.IngressIPAddress}");
                }
            }

            foreach (var service in manager.ServiceAddresses)
            {
                foreach (var port in service.Service.Spec.Ports)
                {
                    var targetPort = int.Parse(port.TargetPort?.Value ?? port.Port.ToString());

                    var endpoint = new IPEndPoint(service.IPAddress, targetPort);
                    options.Listen(endpoint, builder =>
                    {
                        if (args.UpdateHosts)
                        {
                            logger.LogInformation("Forwarding {Service} to tcp://{Service}:{Port}", service.Service.Name(), service.Service.Name(), targetPort);
                        }
                        else
                        {
                            logger.LogInformation("Forwarding {Service} to tcp://{Endpoint}", service.Service.Name(), endpoint);
                        }

                        builder.Use(next =>
                        {
                            return async (context) =>
                            {
                                Debug.Assert(port != null);
                                context.Features.Set(port);
                                context.Features.Set(service.Service);
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
