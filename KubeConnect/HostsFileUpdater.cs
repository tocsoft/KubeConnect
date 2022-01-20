using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect
{
    public class HostsFileUpdater : IHostedService
    {
        private readonly ServiceManager serviceManager;
        private readonly ILogger<HostsFileUpdater> logger;
        private readonly Args args;
        private readonly string hostPath;
        private readonly Dictionary<string, IPAddress> dnsEntries;

        public HostsFileUpdater(ServiceManager serviceManager, ILogger<HostsFileUpdater> logger, Args args)
        {
            this.serviceManager = serviceManager;
            this.logger = logger;
            this.args = args;
            hostPath = "/etc/hosts";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                hostPath = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\drivers\etc\hosts");
            }

            dnsEntries = new Dictionary<string, IPAddress>();

            foreach (var host in serviceManager.IngressConfig.HostNames)
            {
                dnsEntries[host] = serviceManager.IngressConfig.AssignedAddress;
            }

            foreach (var service in serviceManager.Services)
            {
                dnsEntries[service.ServiceName] = service.AssignedAddress;
                dnsEntries[$"{service.ServiceName}.{service.Namespace}.svc.cluster.local"] = service.AssignedAddress;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!args.UpdateHosts) return Task.CompletedTask;

            logger.LogInformation("Adding services to HOSTS file");

            return Task.Run(() =>
            {
                RetryIoOP(5, () =>
                {
                    WriteHostsFile(dnsEntries, true);
                });
            });
        }

        private void RetryIoOP(int maxRetries, Action action)
        {
            int retryCounter = maxRetries;
            while (retryCounter > 0)
            {
                retryCounter--;
                try
                {
                    action();
                    return;
                }
                catch (IOException) when (retryCounter > 0)
                {
                    Thread.Sleep(500);
                }
            }
        }

        bool isStoped = false;
        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (!args.UpdateHosts) return Task.CompletedTask;

            if (!isStoped)
            {
                isStoped = true;
                logger.LogInformation("Removing services from HOSTS file");
            }

            RetryIoOP(5, () =>
            {
                WriteHostsFile(dnsEntries, false);
            });

            return Task.CompletedTask;
        }


        public void WriteHostsFile(Dictionary<string, IPAddress> lookup, bool writeIpAddresses)
        {
            using (var fs = File.Open(hostPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            using (var sr = new StreamReader(fs, leaveOpen: true))
            {
                var dnsNamesOnly = lookup.Select(x => x.Key);

                var sb = ReadWithoutServices(sr, dnsNamesOnly);

                if (writeIpAddresses)
                {
                    foreach (var grp in lookup.GroupBy(x => x.Value))
                    {
                        sb.Append(grp.Key.ToString());
                        foreach (var val in grp)
                        {
                            sb.Append(" ");
                            sb.Append(val.Key);
                        }
                        sb.AppendLine();
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

        public StringBuilder ReadWithoutServices(StreamReader reader, IEnumerable<string> hosts)
        {
            StringBuilder sb = new StringBuilder();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!hosts.Any(s => line.Contains(s, StringComparison.OrdinalIgnoreCase)))
                {
                    sb.AppendLine(line);
                }
            }
            return sb;
        }
    }
}
