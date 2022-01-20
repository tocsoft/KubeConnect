using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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

            var binding = connection.Features.Get<PortBinding>();
            // establish connection to cluster/pod
            // sync data to/from pod

            var pods = await kubernetesClient.ListNamespacedPodAsync(binding.Namespace, labelSelector: binding.Selector);

            // random pod here???
            var pod = pods.Items[0];

            logger.LogInformation("[{ConnectionID}] Opening connection for {ServiceName}:{ServicePort} to {PodName}:{PodPort}", connection.ConnectionId, binding.Name, (connection.LocalEndPoint as IPEndPoint)?.Port, pod.Name(), binding.TargetPort);

            try
            {
                var webSocket = await kubernetesClient.WebSocketNamespacedPodPortForwardAsync(pod.Name(), binding.Namespace, new int[] { binding.TargetPort }, "v4.channel.k8s.io");

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
                logger.LogInformation("[{ConnectionID}] Closing connection for {ServiceName}:{ServicePort} to {PodName}:{PodPort}", connection.ConnectionId, binding.Name, (connection.LocalEndPoint as IPEndPoint)?.Port, pod.Name(), binding.TargetPort);
            }
        }
    }

    public class PortBinding
    {
        public string Name { get; init; }
        public string Namespace { get; init; }
        public string Selector { get; init; }
        public int TargetPort { get; init; }
    }

}
