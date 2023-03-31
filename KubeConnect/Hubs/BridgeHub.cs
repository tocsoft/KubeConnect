using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace KubeConnect.Hubs
{
    public class BridgeHub : Hub
    {
        private readonly ServiceManager serviceManager;
        private readonly IHostApplicationLifetime hostApplicationLifetime;

        public BridgeHub(ServiceManager serviceManager, IHostApplicationLifetime hostApplicationLifetime)
        {
            this.serviceManager = serviceManager;
            this.hostApplicationLifetime = hostApplicationLifetime;
        }

        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (!hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
            {
                await serviceManager.Release(Context.ConnectionId);
            }

            // when disconnecting kill bridge
            await base.OnDisconnectedAsync(exception);
        }

        public async Task<ServiceSettings?> GetServiceSettings(string serviceName)
        {
            var service = serviceManager.GetService(serviceName);
            if (service == null)
            {
                return null;
            }

            var envVars = await serviceManager.GetEnvironmentVariablesForServiceAsync(service);
            return new ServiceSettings
            {
                ServiceName = serviceName,
                EnvironmentVariables = envVars,
            };
        }

        public async Task StartServiceBridge(string serviceName, Dictionary<int, int> ports)
        {
            var service = serviceManager.GetService(serviceName);

            // trigger a message back to the client for logging!!!
            if (service == null)
            {
                throw new Exception($"Unable to find the service '{serviceName}'");
            }

            // bridge logging should be a write to the group `$"Bridge:{service.ServiceName}:{service.Namespace}"`
            await serviceManager.Intercept(service, ports.Select(x => (x.Key, x.Value)).ToList(), this.Context.ConnectionId, this.Clients.Client(this.Context.ConnectionId));
        }
    }

    public class ServiceSettings
    {
        public string ServiceName { get; set; } = string.Empty;

        public IEnumerable<KeyValuePair<string, string>> EnvironmentVariables { get; set; } = Enumerable.Empty<KeyValuePair<string, string>>();
    }
}
