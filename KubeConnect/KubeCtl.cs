using KubeConnect.KubeModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect
{
    public class KubeCtl
    {
        private string context;
        private string @namespace;
        private string kubeconfigFile;
        private readonly JsonSerializerOptions jsonOptions;

        public KubeCtl(string context, string @namespace, string kubeconfigFile)
        {
            this.context = context;
            this.@namespace = @namespace;
            this.kubeconfigFile = kubeconfigFile;
            this.jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        }

        internal async Task<IEnumerable<Service>> ListServiceAsync(CancellationToken cancellationToken)
        {
            var res = await RunKubectl<ItemList<Service>>("get services -o json", cancellationToken);
            return res.Items;
        }

        internal async Task<IEnumerable<(string Name, string Namespace, string dns)>> FindIngressServices(CancellationToken cancellationToken)
        {
            var ingressList = await RunKubectl<ItemList<Ingress>>("get ingresses -o json", cancellationToken);
            return Enumerable.Empty<(string Name, string Namespace, string dns)>();
        }

        internal KubeCtl WithNamespace(string @namespace)
        {
            if (string.IsNullOrWhiteSpace(@namespace))
            {
                return this;
            }

            if (@namespace != this.@namespace)
            {
                return new KubeCtl(context, @namespace, kubeconfigFile);
            }

            return this;
        }

        internal async Task PortForward(string serviceName, string ipAddress, int localPort, int remotePort, IConsole console, CancellationToken cancellationToken)
        {
            // we should keep trying until we tell the thing to stop
            while (!cancellationToken.IsCancellationRequested)
            {
                await RunKubectl($"port-forward services/{serviceName} --address={ipAddress} {localPort}:{remotePort}", console, cancellationToken);
            }
        }

        private async Task<T> RunKubectl<T>(string args, CancellationToken cancellationToken)
        {
            var (str, _) = await RunKubectl(args, cancellationToken);
            var result = JsonSerializer.Deserialize<T>(str, jsonOptions);
            return result;
        }

        private Task<(string output, int exitCode)> RunKubectl(string args, CancellationToken cancellationToken)
            => RunKubectl(args, null, cancellationToken);

        private async Task<(string output, int exitCode)> RunKubectl(string args, IConsole console, CancellationToken cancellationToken)
        {
            StringBuilder stringBuilder = new StringBuilder();
            int exitCode = 0;
            try
            {
                if (!string.IsNullOrWhiteSpace(@namespace))
                {
                    args = $"-n {@namespace} {args}";
                }

                if (!string.IsNullOrWhiteSpace(context))
                {
                    args = $"--context={context} {args}";
                }

                if (!string.IsNullOrWhiteSpace(kubeconfigFile))
                {
                    args = $"--kubeconfig={kubeconfigFile} {args}";
                }

                var processStartInfo = new ProcessStartInfo("kubectl")
                {
                    Arguments = args
                };

                var res = await ProcessRunner.RunAsync(processStartInfo, (s) =>
                {
                    console?.WriteLine(s);
                    stringBuilder.AppendLine(s); // append no line???
                }, (s) =>
                {
                    console?.WriteErrorLine(s);
                    stringBuilder.AppendLine(s); // append no line???
                },
                cancellationToken);
                exitCode = res.ExitCode;
            }
            catch
            {
                // whatever"""
                exitCode = 1;
            }
            return (stringBuilder.ToString(), exitCode);
        }

        private static Task ReadStream(StreamReader reader, StringBuilder sb, Action<string> writeLine, CancellationToken cancellationToken)
        => Task.Run(() =>
            {
                var buf = new char[8 * 1024];
                var memory = new Memory<char>(buf);
                var lineBuffer = new StringBuilder();

                while (!cancellationToken.IsCancellationRequested)
                {
                    var chunkLength = reader.Read(memory.Span);
                    if (chunkLength == 0)
                    {
                        if (lineBuffer.Length > 0)
                        {
                            writeLine?.Invoke(lineBuffer.ToString());
                            lineBuffer.Clear();
                        }
                        break;
                    }

                    HandleChunk(memory.Span.Slice(0, chunkLength), lineBuffer, sb, writeLine);
                }
            });

        private static void HandleChunk(Span<char> chunk, StringBuilder lineBuffer, StringBuilder sb, Action<string> writeline)
        {
            sb?.Append(chunk);

            if (writeline != null)
            {
                int lineBreakPos = 0;
                // get all the newlines
                while ((lineBreakPos = chunk.IndexOf('\n')) >= 0 && chunk.Length > 0)
                {
                    lineBuffer.Append(chunk.Slice(0, lineBreakPos));
                    writeline(lineBuffer.ToString());
                    lineBuffer.Clear();
                    chunk = chunk.Slice(lineBreakPos + 1);
                }

                lineBuffer.Append(chunk);
            }
        }

    }
}
