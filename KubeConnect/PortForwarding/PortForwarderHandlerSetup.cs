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

        public PortForwarderHandlerSetup(ServiceManager manager, ILogger<PortForwarderHandlerSetup> logger, IServiceProvider serviceProvider)
        {
            this.manager = manager;
            this.logger = logger;
            this.serviceProvider = serviceProvider;
        }

        public void Configure(KestrelServerOptions options)
        {
            options.ApplicationServices = serviceProvider;

            options.Listen(manager.IngressIPAddress, 80);

            options.Listen(manager.IngressIPAddress, 443, builder =>
            {
                builder.UseHttps(CertificateHelper.CreateCertificate(manager.IngressHostNames));
            });


            foreach (var ingress in manager.IngressAddresses)
            {
                logger.LogInformation($"Listening to {ingress}");
            }

            foreach (var service in manager.ServiceAddresses)
            {
                foreach (var port in service.Service.Spec.Ports)
                {
                    var targetPort = int.Parse(port.TargetPort?.Value ?? port.Port.ToString());

                    var endpoint = new IPEndPoint(service.IPAddress, targetPort);
                    options.Listen(endpoint, builder =>
                    {
                        logger.LogInformation("Forwarding tcp://{Service}:{Port}", service.Service.Name(), targetPort, endpoint);

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
