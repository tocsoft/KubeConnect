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
using System.Threading;
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

        private async Task<V1Pod> AwaitPodRunning(V1Pod pod)
        {
            //wait for 30 seconds for it to start
            var cts = new CancellationTokenSource(30000);
            var lastPod = pod;
            while (!cts.IsCancellationRequested)
            {

                var runningPods = (await kubernetesClient.ListNamespacedPodAsync(pod.Namespace())).Items;
                lastPod = runningPods.SingleOrDefault(x => x.Name().Equals(pod.Name(), StringComparison.OrdinalIgnoreCase));
                if (lastPod?.Status.Phase == "Running")
                {
                    return lastPod;
                }
            }

            return lastPod ?? pod;
        }

        private async Task DisableDeployment(V1Deployment deployment)
        {
            if (deployment.Spec.Replicas != 0)
            {
                deployment.Metadata.Annotations["kubeconnect.bridge/original_replicas"] = deployment.Spec.Replicas.ToString();
                deployment.Spec.Replicas = 0;
                await kubernetesClient.ReplaceNamespacedDeploymentAsync(deployment, deployment.Name(), deployment.Namespace(), fieldManager: "kubeconnect:bridge");
            }
        }

        private async Task EnableDeployment(V1Deployment deployment)
        {
            if (deployment.Spec.Replicas == 0 && deployment.Metadata.Annotations.TryGetValue("kubeconnect.bridge/original_replicas", out var orgReplicaCount))
            {
                if (int.TryParse(orgReplicaCount, out var target))
                {
                    deployment.Spec.Replicas = target;
                    deployment.Metadata.Annotations.Remove("kubeconnect.bridge/original_replicas");
                    await kubernetesClient.ReplaceNamespacedDeploymentAsync(deployment, deployment.Name(), deployment.Namespace(), fieldManager: "kubeconnect:bridge");
                }
            }
        }

        private async Task<V1Pod?> StartSSHPod(PortBinding binding)
        {
            if (binding.Service == null)
            {
                return null;
            }
            var pods = (await kubernetesClient.ListNamespacedPodAsync(binding.Namespace, labelSelector: binding.Selector)).Items;

            if (pods.Count > 1)
            {
                foreach (var p in pods.Skip(1))
                {
                    await kubernetesClient.DeleteNamespacedPodAsync(p.Name(), p.Namespace());
                }

                return pods.First();
            }
            else if (pods.Count == 1)
            {
                return pods.Single();
            }

            var dep = await FindMatchingDeployment(binding.Service);
            if (dep != null)
            {
                await DisableDeployment(dep);
            }

            var ports = binding.Service.TcpPorts.Select(X => new V1ContainerPort
            {
                ContainerPort = X.listenPort,
                Name = $"port-{X.listenPort}"
            }).ToList();

            ports.Add(new V1ContainerPort
            {
                ContainerPort = 2222,
                Name = "ssh"
            });

            var pod = new V1Pod()
            {
                Metadata = new V1ObjectMeta()
                {
                    Labels = new Dictionary<string, string>(binding.Service.Selector)
                    {
                        ["kubeconnect.bridge/ssh"] = "true"
                    },
                    Name = $"{binding.Service.ServiceName}-{Guid.NewGuid().ToString().Substring(0, 4)}-ssh",
                    NamespaceProperty = binding.Service.Namespace
                },
                Spec = new V1PodSpec()
                {
                    Containers = new List<V1Container>()
                                {
                                    new V1Container
                                    {
                                        Name = "ssh",
                                        Image = "linuxserver/openssh-server:latest",
                                        ImagePullPolicy = "IfNotPresent",
                                        Env = new List<V1EnvVar>
                                        {

                                            new V1EnvVar{ Name = "DOCKER_MODS", Value ="linuxserver/mods:openssh-server-ssh-tunnel" },
                                            new V1EnvVar{ Name = "SUDO_ACCESS", Value ="true" },
                                            new V1EnvVar{ Name = "PASSWORD_ACCESS", Value ="true" },
                                            new V1EnvVar{ Name = "USER_PASSWORD", Value = "password" },
                                        },
                                        Ports = ports
                                    }
                                }
                }
            };
            logger.LogInformation("Starting bridging components in cluster for {serviceName}", binding.Name);
            await kubernetesClient.CreateNamespacedPodAsync(pod, pod.Namespace());

            return await AwaitPodRunning(pod);
        }

        private async Task<V1Pod?> FindPod(PortBinding binding)
        {
            if (binding.RequireSSHServer)
            {
                return await StartSSHPod(binding);
            }

            var pods = await kubernetesClient.ListNamespacedPodAsync(binding.Namespace, labelSelector: binding.Selector);
            var pod = pods.Items.Where(x => x.Status.Phase == "Running").FirstOrDefault();
            return pod;
        }

        private async Task<V1Deployment?> FindMatchingDeployment(ServiceDetails? service)
        {
            if (service is null)
            {
                return null;
            }

            var results = await kubernetesClient.ListNamespacedDeploymentAsync(service.Namespace);
            foreach (var dep in results.Items)
            {
                if (dep.MatchTemplate(service))
                {
                    return dep;
                }
            }

            return null;
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            var input = connection.Transport.Input;
            var output = connection.Transport.Output;

            var binding = connection.Features.Get<PortBinding>();
            if (binding is null)
            {
                throw new InvalidOperationException("PortBinding feature must be defined on the connection");
            }
            binding.AddRef();

            // establish connection to cluster/pod
            // sync data to/from pod
            var pod = await FindPod(binding);

            connection.ConnectionClosed.Register(async () =>
            {
                binding.Release();
                await Task.Delay(5000);
                if (binding.RequireSSHServer && !binding.HasRef)
                {
                    logger.LogInformation("No more connections to bridging components in cluster for {serviceName}, re-enabling deployed services", binding.Name);
                    await kubernetesClient.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace());

                    var dep = await FindMatchingDeployment(binding.Service);
                    if (dep != null)
                    {
                        await EnableDeployment(dep);
                    }
                }
            });

            if (pod == null)
            {
                connection.Abort();
                return;
            }

            logger.LogInformation("[{ConnectionID}] Opening connection for {ServiceName}:{ServicePort} to {PodName}:{PodPort}", connection.ConnectionId, binding.Name, (connection.LocalEndPoint as IPEndPoint)?.Port, pod.Name(), binding.TargetPort);

            try
            {
                using var webSocket = await kubernetesClient.WebSocketNamespacedPodPortForwardAsync(pod.Name(), binding.Namespace, new int[] { binding.TargetPort }, "v4.channel.k8s.io");

                using var demux = new StreamDemuxer(webSocket, StreamType.PortForward, ownsSocket: true);

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
                        var result = await input.ReadAsync(connection.ConnectionClosed);

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
                        var result = await podOutput.ReadAsync(connection.ConnectionClosed);

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
            catch (OperationCanceledException)
            {
                // this will be fine, means the connection was closed by one of the 2 ends
                connection.Abort();
            }
            catch (ConnectionResetException)
            {
                // connection was closed just die silently
                //throw;
                connection.Abort();
            }
            finally
            {
                logger.LogInformation("[{ConnectionID}] Closing connection for {ServiceName}:{ServicePort} to {PodName}:{PodPort}", connection.ConnectionId, binding.Name, (connection.LocalEndPoint as IPEndPoint)?.Port, pod.Name(), binding.TargetPort);
            }
        }
    }

    public class PortBinding
    {
        public string Name { get; init; } = string.Empty;
        public string Namespace { get; init; } = string.Empty;
        public string Selector { get; init; } = string.Empty;
        public int TargetPort { get; init; }
        public bool RequireSSHServer { get; init; } = false;
        public ServiceDetails? Service { get; init; } = null;

        private int SSHRefCounter = 0;

        internal void AddRef()
        {
            Interlocked.Increment(ref SSHRefCounter);
        }
        internal void Release()
        {
            Interlocked.Decrement(ref SSHRefCounter);
        }
        public bool HasRef => SSHRefCounter > 0;

    }

}
