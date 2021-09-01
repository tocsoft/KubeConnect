using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using System;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace KubeConnect.PortForwarding
{
    // This is the connection handler the framework uses to handle new incoming connections
    public class PortForwardingConnectionHandler : ConnectionHandler
    {
        private readonly IKubernetes kubernetesClient;
        private readonly ILogger logger;

        public PortForwardingConnectionHandler(IKubernetes kubernetesClient, ILogger<PortForwardingConnectionHandler> logger)
        {
            this.kubernetesClient = kubernetesClient;
            this.logger = logger;
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            var input = connection.Transport.Input;
            var output = connection.Transport.Output;

            var port = connection.Features.Get<V1ServicePort>();
            var service = connection.Features.Get<V1Service>();
            // establish connection to cluster/pod
            // sync data to/from pod

            var labelSelector = string.Join(",", service.Spec.Selector.Select((s) => $"{s.Key}={s.Value}"));
            var pods = await kubernetesClient.ListNamespacedPodAsync(service.Namespace(), labelSelector: labelSelector);

            // random pod here???
            var pod = pods.Items[0];
            
            logger.LogInformation("[{ConnectionID}] Opening connection for {ServiceName}:{ServicePort} to {PodName}:{PodPort}", connection.ConnectionId, service.Name(), (connection.LocalEndPoint as IPEndPoint)?.Port, pod.Name(), port.Port);

            try
            {
                var webSocket = await kubernetesClient.WebSocketNamespacedPodPortForwardAsync(pod.Name(), service.Namespace(), new int[] { port.Port }, "v4.channel.k8s.io");

                using var demux = new StreamDemuxer(webSocket, StreamType.PortForward);

                demux.Start();
                connection.ConnectionClosed.Register(() =>
                {
                    demux.Dispose();
                });
                using var stream = demux.GetStream((byte?)0, (byte?)0);

                var podOutput = PipeReader.Create(stream);
                var podInput = PipeWriter.Create(stream);

                async Task PushToPod()
                {
                    while (true)
                    {
                        var result = await input.ReadAsync();

                        foreach (var buffer in result.Buffer)
                        {
                            stream.Write(buffer.Span);
                        }
                        input.AdvanceTo(result.Buffer.End);

                        if (result.IsCompleted) { break; }
                    }
                }
                async Task PushToClient()
                {
                    while (true)
                    {
                        var result = await podOutput.ReadAsync();

                        foreach (var buffer in result.Buffer)
                        {
                            await output.WriteAsync(buffer, connection.ConnectionClosed);
                        }
                        podOutput.AdvanceTo(result.Buffer.End);

                        if (result.IsCompleted) { break; }
                    }
                }

                await Task.WhenAll(PushToPod(), PushToClient());
            }
            catch (OperationCanceledException ex)
            {
                // this will be fine, means the connection was closed by one of the 2 ends
            }
            finally
            {
                logger.LogInformation("[{ConnectionID}] Closing connection for {ServiceName}:{ServicePort} to {PodName}:{PodPort}", connection.ConnectionId, service.Name(), (connection.LocalEndPoint as IPEndPoint)?.Port, pod.Name(), port.Port);
            }
        }
    }
}
