using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect.PortForwarding
{
    // This is the connection handler the framework uses to handle new incoming connections
    public class PortForwardingConnectionHandler : ConnectionHandler
    {
        private readonly Kubernetes kubernetesClient;
        private readonly ILogger logger;

        public PortForwardingConnectionHandler(Kubernetes kubernetesClient, ILogger<PortForwardingConnectionHandler> logger)
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

        private async Task<(bool running, bool sshPod)> CheckPod(PortBinding binding)
        {
            var pods = await kubernetesClient.ListNamespacedPodAsync(binding.Namespace, labelSelector: binding.Selector);
            var pod = pods.Items.Where(x => x.Status.Phase == "Running").FirstOrDefault();
            return (pod?.Status?.Phase == "Running", pod?.GetAnnotation("kubeconnect.bridge/ssh") == "true");
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
                await Task.Delay(15000);
                if (binding.RequireSSHServer && !binding.HasRef)
                {
                    var (isRunning, isSsh) = await CheckPod(binding);
                    if (isSsh)
                    {
                        logger.LogInformation("No more connections to bridging components in cluster for {serviceName}, re-enabling deployed services", binding.Name);
                        try
                        {
                            await kubernetesClient.DeleteNamespacedPodAsync(pod.Name(), pod.Namespace());
                        }
                        catch
                        {

                        }
                    }
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

                var proxy = new PortForwardProxy(webSocket, connection.Transport);

                await proxy.Forward(connection.ConnectionClosed);
            }
            catch (OperationCanceledException)
            {
                // this will be fine, means the connection was closed by one of the 2 ends
                connection.Abort();
            }
            catch (ConnectionResetException)
            {
                // connection was closed just die silently
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

    internal class PortForwardProxy
    {
        private const int MAXFRAMESIZE = 15 * 1024 * 1024; // 15MB
        private readonly WebSocket webSocket;
        private readonly IDuplexPipe pipe;
        private int bytesToSkip = 3;

        public PortForwardProxy(WebSocket webSocket, IDuplexPipe pipe)
        {
            this.webSocket = webSocket;
            this.pipe = pipe;
        }

        public async Task Forward(CancellationToken cancellationToken)
        {
            // everything in the pipe needs reading, and then wrting out to the websocket!
            var readTask = ReadFromWebsocket(cancellationToken);
            var writeTask = WriteOutToWebSocket(cancellationToken);
            Task[] tasks = [
                readTask.AsTask(),
                writeTask.AsTask()
            ];
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.WhenAny(tasks);

                if (readTask.IsCompleted)
                {
                    if (await readTask)
                    {
                        return;
                    }

                    readTask = ReadFromWebsocket(cancellationToken);
                    tasks[0] = readTask.AsTask();
                }

                if (writeTask.IsCompleted)
                {
                    if (await writeTask)
                    {
                        return;
                    }
                    writeTask = WriteOutToWebSocket(cancellationToken);
                    tasks[1] = writeTask.AsTask();
                }
            }


        }
        async ValueTask<bool> ReadFromWebsocket(CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);
            var segment = buffer.AsMemory();
            try
            {
                while (!cancellationToken.IsCancellationRequested && webSocket.CloseStatus == null)
                {
                    // We always get data in this format:
                    // [stream index] (1 for stdout, 2 for stderr)
                    // [payload]
                    var result = await webSocket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);

                    // Ignore empty messages
                    if (result.Count < 2)
                    {
                        continue;
                    }
                    if (result.Count == 3)
                    {
                        bytesToSkip = 1;
                        continue;
                    }

                    // When used in port-forwarding, the first 2 bytes from the web socket is port bytes, skip.
                    // https://github.com/kubernetes/kubernetes/blob/master/pkg/kubelet/server/portforward/websocket.go

                    // we read/ignore the first 3 bytes, then onbyte(for the streamindex) there after
                    var remainingBuffer = segment.Slice(bytesToSkip, result.Count - bytesToSkip);
                    bytesToSkip = 1;

                    await pipe.Output.WriteAsync(remainingBuffer, cancellationToken);

                    while (!result.EndOfMessage)
                    {
                        result = await webSocket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);
                        await pipe.Output.WriteAsync(segment.Slice(0, result.Count), cancellationToken);
                    }
                    break;
                }
            }
            catch (Exception e)
            {
                var t = e;
                throw;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return webSocket.CloseStatus != null;
        }

        async ValueTask<bool> WriteOutToWebSocket(CancellationToken cancellationToken)
        {
            var result = await pipe.Input.ReadAsync(cancellationToken);

            foreach (var buffer in result.Buffer)
            {
                await WriteAsync(buffer, cancellationToken);
            }
            pipe.Input.AdvanceTo(result.Buffer.End);

            if (result.IsCompleted)
            {
                return true;
            }

            return false;
        }

        internal async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            var writeBuffer = ArrayPool<byte>.Shared.Rent(Math.Min(buffer.Length, MAXFRAMESIZE) + 1);
            var writeBufferSpan = writeBuffer.AsMemory();
            try
            {
                writeBuffer[0] = 0;
                for (var i = 0; i < buffer.Length; i += MAXFRAMESIZE)
                {
                    var slice = buffer.Slice(i, Math.Min(buffer.Length - i, MAXFRAMESIZE));
                    slice.CopyTo(writeBufferSpan.Slice(1, slice.Length));
                    var segment = writeBufferSpan.Slice(0, slice.Length + 1);
                    await webSocket.SendAsync(segment, WebSocketMessageType.Binary, false, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                var t = e;
                throw t;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(writeBuffer);
            }
        }
    }
}
