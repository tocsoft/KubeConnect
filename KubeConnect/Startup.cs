using k8s.KubeConfigModels;
using KubeConnect.Ingress;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Configuration;
using KubeConnect.Hubs;

namespace KubeConnect
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddReverseProxy();
            services.AddSingleton<IProxyConfigProvider, IngressProxyConfig>();
            services.AddSignalR();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseRouting();

            app.Use((context, nxt) =>
            {
                if (context.Request.Method == HttpMethods.Connect && context.Request.Protocol != "HTTP/1.1")
                {
                    var resetFeature = context.Features.Get<IHttpResetFeature>();
                    if (resetFeature != null)
                    {
                        //https://www.rfc-editor.org/rfc/rfc7540#page-51
                        //HTTP_1_1_REQUIRED (0xd):  The endpoint requires that HTTP/1.1 be used instead of HTTP/2.
                        resetFeature.Reset(errorCode: 0xd);
                        return Task.CompletedTask;
                    }
                }
                return nxt();
            });
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapReverseProxy();
                endpoints.MapHub<BridgeHub>("")
                    .RequireHost("localhost");
            });
        }
    }
}
