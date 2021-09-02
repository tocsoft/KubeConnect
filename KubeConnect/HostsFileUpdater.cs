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
        private readonly string hostPath;
        private readonly Dictionary<string, IPAddress> dnsEntries;

        public HostsFileUpdater(ServiceManager serviceManager, ILogger<HostsFileUpdater> logger)
        {
            this.serviceManager = serviceManager;
            this.logger = logger;

            hostPath = "/etc/hosts";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                hostPath = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\drivers\etc\hosts");
            }

            dnsEntries = new Dictionary<string, IPAddress>();

            foreach (var host in serviceManager.IngressHostNames)
            {
                dnsEntries[host] = serviceManager.IngressIPAddress;
            }
            foreach (var service in serviceManager.ServiceAddresses)
            {
                dnsEntries[service.Service.Name()] = service.IPAddress;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
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
