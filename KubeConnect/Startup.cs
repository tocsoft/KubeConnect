using k8s.KubeConfigModels;
using KubeConnect.Ingress;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

            if (args.UseSsl)
            {
                app.UseHttpsRedirection();
            }

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapReverseProxy();

                endpoints.MapGet("/status", () =>
                {
                    return new StatusResult(true);
                })
                .RequireHost("localhost");
            });
        }
    }

    public record StatusResult(bool running);
}
