using KubeConnect.Ingress;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebSockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Configuration;

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
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, Args args)
        {
            app.UseRouting();
            app.UseWebSockets();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapReverseProxy();

                endpoints.MapGet("/status", () =>
                {
                    return new StatusResult(true);
                })
                .RequireHost("localhost");

                // start/attach to bridge and stream logs!
                endpoints.MapPost("/bridge", ([FromBody] BridgeSettings settings) =>
                {
                    return new StatusResult(true);
                })
                .RequireHost("localhost");


                endpoints.Map("/bridge/{sessionId}", (string sessionId, HttpContext context) =>
                {
                })
                .RequireHost("localhost");

                endpoints.MapDelete("/bridge/{sessionId}", () =>
                {
                    return new StatusResult(true);
                })
                .RequireHost("localhost");
            });
        }
    }

    public record StatusResult(bool running);

    public record BridgeSettings(string service, int remotePort, int localPort);
}
