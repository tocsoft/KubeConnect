using k8s;
using k8s.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect
{
    public class ServiceManager
    {
        private readonly IKubernetes kubernetesClient;
        private readonly string @namespace;
        private readonly IConsole console;

        public ServiceManager(IKubernetes kubernetesClient, string @namespace, IConsole console)
        {
            this.kubernetesClient = kubernetesClient;
            this.@namespace = @namespace;
            this.console = console;
        }

        private Dictionary<string, IPAddress> serviceIpAddressLookup = new Dictionary<string, IPAddress>(StringComparer.OrdinalIgnoreCase);
        public async Task RunPortForwardingAsync(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource();
            cancellationToken.Register(() =>
            {
                tcs.TrySetResult();
            });

            var serviceList = await kubernetesClient.ListNamespacedServiceAsync(@namespace);

            // assign IP Addresses
            int ipCounter = 1;
            foreach (var s in serviceList.Items)
            {
                serviceIpAddressLookup[s.Name()] = IPAddress.Parse($"127.2.2.{ipCounter++}");
            }
            // update hosts file
            console.WriteLine("Adding services to HOSTS file");
            WriteHostsFile(serviceIpAddressLookup, true);

            // forward ports until cancelled
            List<Task> forwards = new List<Task>();
            foreach (var s in serviceList.Items)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    forwards.Add(Forward(s, serviceIpAddressLookup[s.Name()], cancellationToken));
                }
            }
            if (!cancellationToken.IsCancellationRequested)
            {
                console.WriteLine("All services now listening");
                try
                {
                    var r = Task.WhenAll(forwards);
                    await Task.WhenAny(tcs.Task, r);
                }
                catch
                {

                }
            }

            // cleanup IP addresses (update hosts file)
            Cleanup();
        }
        bool cleaned = false;
        public void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;

            console.WriteLine("Cleaning up HOSTS file - removing services");
            WriteHostsFile(serviceIpAddressLookup, false);
        }

        static string hostPath = null;
        public static string HostFilePath()
        {
            if (hostPath == null)
            {
                hostPath = "/etc/hosts";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    hostPath = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\drivers\etc\hosts");
                }
            }

            return hostPath;
        }

        public static void WriteHostsFile(Dictionary<string, IPAddress> lookup, bool writeIpAddresses)
        {
            using (var fs = File.Open(HostFilePath(), FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            using (var sr = new StreamReader(fs, leaveOpen: true))
            {
                var dnsNames = lookup.ToDictionary(X => $" {X.Key}", x => x.Value.ToString());

                var sb = ReadWithoutServices(sr, dnsNames.Keys);

                if (writeIpAddresses)
                {
                    foreach (var n in dnsNames)
                    {
                        sb.Append(n.Value);
                        sb.Append(" ");
                        sb.AppendLine(n.Key);
                    }
                }

                fs.SetLength(0); //clear
                fs.Position = 0;
                using (var sw = new StreamWriter(fs, leaveOpen: true))
                {
                    sw.Write(sb.ToString());
                    sw.Flush();
                }

                fs.Close();
            }
        }

        public static StringBuilder ReadWithoutServices(StreamReader reader, IEnumerable<string> hosts)
        {
            StringBuilder sb = new StringBuilder();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!hosts.Any(s => line.Contains(s, StringComparison.OrdinalIgnoreCase)))
                {
                    sb.AppendLine(line);
                }
            }
            return sb;
        }

        private async Task Forward(V1Service service, IPAddress ipAddress, CancellationToken cancellationToken)
        {
            List<Task> tasks = new List<Task>();

            // foreach port on the service
            foreach (var p in service.Spec.Ports)
            {
                if (cancellationToken.IsCancellationRequested) return;

                var task = ForwardPort(service, p, ipAddress, cancellationToken);
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        private async Task ForwardPort(V1Service service, V1ServicePort port, IPAddress ipAddress, CancellationToken cancellationToken)
        {
            await Task.Yield();
            if (cancellationToken.IsCancellationRequested) return;

            var targetPort = int.Parse(port.TargetPort?.Value ?? port.Port.ToString());
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, targetPort);

            console.WriteLine($"{service.Name()}:{port.Port} : forward from {localEndPoint}");

            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(localEndPoint);
            listener.Listen(100);

            using var r = cancellationToken.Register(() =>
            {
                listener.Shutdown(SocketShutdown.Both);
                listener.Close();
                listener.Dispose();
            });

            int connectionId = 0;
            async Task HandleConnection(Socket handler)
            {
                connectionId++;
                //TODO limit connection counnt ???

                // force onto thread pool
                await Task.Yield();
                console.WriteLine($"[{connectionId}] {service.Name()}:{port.Port} : connected");

                var r = string.Join(",", service.Spec.Selector.Select((s) => $"{s.Key}={s.Value}"));
                var pods = await kubernetesClient.ListNamespacedPodAsync(service.Namespace(), labelSelector: r);
                var pod = pods.Items[0];

                var webSocket = await kubernetesClient.WebSocketNamespacedPodPortForwardAsync(pod.Name(), service.Namespace(), new int[] { port.Port }, "v4.channel.k8s.io");

                var demux = new StreamDemuxer(webSocket, StreamType.PortForward);
                demux.Start();

                var stream = demux.GetStream((byte?)0, (byte?)0);

                using var r1 = cancellationToken.Register(() =>
                {
                    stream.Close();
                    demux.Dispose();
                });

                var copy = Task.Run(() =>
                {
                    try
                    {
                        var buff = new byte[4096];
                        while (!cancellationToken.IsCancellationRequested && handler.Connected)
                        {
                            if (handler == null) continue;
                            var read = stream.Read(buff, 0, 4096);
                            handler.Send(buff, 0, read, SocketFlags.None);
                        }
                    }
                    finally
                    {
                        stream.Close();
                        handler?.Close();
                    }
                });

                var accept = Task.Run(() =>
                {
                    try
                    {
                        var bytes = new byte[4096];
                        while (!cancellationToken.IsCancellationRequested && handler.Connected)
                        {
                            int bytesRec = handler.Receive(bytes, 4096, SocketFlags.None);
                            stream.Write(bytes, 0, bytesRec);
                            if (bytesRec == 0 || Encoding.ASCII.GetString(bytes, 0, bytesRec).IndexOf("<EOF>") > -1)
                            {
                                break;
                            }
                        }
                    }
                    finally
                    {
                        stream.Close();
                        handler?.Close();
                    }
                });

                await accept;
                await copy;

                handler?.Close();
                console.WriteLine($"[{connectionId}] {service.Name()}:{port.Port} : closed");
            }



            while (!cancellationToken.IsCancellationRequested)
            {
                var handler = listener.Accept();
                // TODO for this port and IP/service name we should try and discover the 'best' pod to connect to.
                _ = HandleConnection(handler); // process connection in own task
            }

            listener.Close();
        }
    }
}
