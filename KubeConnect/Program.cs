using KubeConnect.KubeModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect
{
    partial class Program
    {
        static async Task<int> Main(string[] args)
        {
            // simple arg parsing
            var parseArgs = new Args(args);
            if (parseArgs.AttachDebugger)
            {
                Debugger.Launch();
            }
            IConsole console = new ConsoleWrapper();
            if (!string.IsNullOrWhiteSpace(parseArgs.ConsolePipeName))
            {
                console = new PipeConsoleWriter(parseArgs.ConsolePipeName);
            }

            // -n|--namespace {namespace} (target a specific, alternative namespace)
            // --context {context name} : context name to use
            // -i|--ingress : (forward the ingresses)
            // -
            if (!parseArgs.NoLogo)
            {
                console.WriteLine($@"
  _  __     _           ____                            _   
 | |/ /   _| |__   ___ / ___|___  _ __  _ __   ___  ___| |_ 
 | ' / | | | '_ \ / _ \ |   / _ \| '_ \| '_ \ / _ \/ __| __|
 | . \ |_| | |_) |  __/ |__| (_) | | | | | | |  __/ (__| |_ 
 |_|\_\__,_|_.__/ \___|\____\___/|_| |_|_| |_|\___|\___|\__|
                                                            
Version {CurrentVersion}
{RepositoryUrl}");
            }

            if (!RootChecker.IsRoot())
            {
                return await RunProcessAsAdmin(parseArgs, console);
            }

            var cts = new CancellationTokenSource();
            console.CancelKeyPress += delegate
            {
                cts.Cancel();
            };

            var cmdRunner = new KubeCtl(parseArgs.Context, parseArgs.Namespace, parseArgs.KubeconfigFile);
            var ipAddressLookup = new Dictionary<(string Name, string Namespace), string>();

            // lookup mappings
            var services = await cmdRunner.ListServiceAsync(cts.Token);

            var forwards = services
                .Where(x => x.Spec.Selector != null)
                .SelectMany(x => x.Spec.Ports.Select(p => new
                {
                    service = (Name: x.Metadata.Name.ToLowerInvariant(), Namespace: x.Metadata.Namespace.ToLowerInvariant()),
                    p.Port
                })).GroupBy(x => x.service)
                .ToList();

            var ingressServices = await cmdRunner.FindIngressServices(cts.Token);

            List<Task> tasks = new List<Task>();
            int counter = 0;
            foreach (var s in forwards)
            {
                counter++;
                var ip = $"127.1.127.{counter}";
                ipAddressLookup[s.Key] = ip;
                foreach (var port in s)
                {
                    var lookup = parseArgs.Mappings.LastOrDefault(x => x.ServiceName.Equals(port.service.Name, StringComparison.OrdinalIgnoreCase) && x.RemotePort == port.Port);

                    var localport = port.Port;

                    if (lookup != null)
                    {
                        localport = lookup.LocalPort;
                    }
                    tasks.Add(cmdRunner
                                .WithNamespace(port.service.Namespace)
                                .PortForward(port.service.Name, ip, localport, port.Port, new LinePrefixingConsole(port.service.Name + " : ", console), cts.Token));
                }
            }

            var wrtiingHostsCompleteinSource = new TaskCompletionSource();
            new Thread(() =>
            {
                while (!cts.IsCancellationRequested)
                {
                    WriteHostsFile(ipAddressLookup, true);
                    Thread.Sleep(1000);
                }
                wrtiingHostsCompleteinSource.SetResult();
            }).Start();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                console.WriteErrorLine(ex.ToString());
            }
            try
            {
                await wrtiingHostsCompleteinSource.Task;
            }
            catch
            {
                // noop
            }

            // cleanup
            WriteHostsFile(ipAddressLookup, false);

            Environment.Exit(0);
            return 0;
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

        public static void WriteHostsFile(Dictionary<(string Name, string Namespace), string> lookup, bool writeIpAddresses)
        {
            using (var fs = File.Open(HostFilePath(), FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            using (var sr = new StreamReader(fs, leaveOpen: true))
            {
                var dnsNames = lookup.ToDictionary(X => $" {X.Key.Name}", x => x.Value);

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

        private static async Task<int> RunProcessAsAdmin(Args parseArgs, IConsole console)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (parseArgs.Elevated)
                {
                    // this should already be elevated, guess if we are not somethign went wrong and we should probably tell them to use an admin prompt
                    console.WriteErrorLine("Error: must be ran from an administrator command prompt");
                    return -1;
                }
                var forwarder = new PipeConsoleForwarder(console);
                var commandline = $" --elevated-command {forwarder.PipeName} ";
                if (Debugger.IsAttached)
                {
                    commandline += "--attach-debugger ";
                }

                commandline += Environment.CommandLine;

                var cts = new CancellationTokenSource();
                
                Console.CancelKeyPress += delegate
                {
                    cts.Cancel();
                };
                _ = forwarder.Listen(cts.Token);

                var exeToRun = Process.GetCurrentProcess().MainModule.FileName;
                var info = new ProcessStartInfo(exeToRun, commandline)
                {
                    Verb = "runas"
                };
                
                // we want a chance for the process to shutdown gracefully before we kill it
                var processCts = new CancellationTokenSource();
                cts.Token.Register(() =>
                {
                    processCts.CancelAfter(10000);// give process 10 seconds to cancels after cancerlatino is triggered via listener
                });


                var resTask = ProcessRunner.RunAsync(info,
                    console.WriteLine,
                    console.WriteErrorLine,
                processCts.Token);

                var res = await resTask;

                Environment.Exit(res.ExitCode);
                return res.ExitCode;
            }
            else
            {
                console.WriteErrorLine("Error: must be ran as root, rerun the command via 'sudo'");
            }
            return -1;
        }


        public static async Task<Process> Run(string arguments, CancellationToken cancellationToken)
        {
            Process process = null;
            try
            {
                var exeToRun = Process.GetCurrentProcess().MainModule.FileName;

                var processStartInfo = new ProcessStartInfo(exeToRun)
                {
                    Arguments = arguments,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Verb = "runas"
                };

                process = Process.Start(processStartInfo);
                ChildProcessTracker.AddProcess(process);

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (process.HasExited != false)
                    {
                        return process;
                    }

                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                // noop
            }

            if (process?.HasExited == false)
            {
                process.WaitForExit(3000);
                process.Kill();
            }

            return process;
        }

        public static string CurrentVersion
        {
            get
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                return assembly.GetName().Version.ToString();
            }
        }

        public static string RepositoryUrl
        {
            get
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                var url = assembly.GetCustomAttributesData()
                    .Where(x => x.AttributeType == typeof(AssemblyMetadataAttribute))
                    .Where(x => x.ConstructorArguments[0].Value?.ToString() == "RepositoryUrl")
                    .Select(x => x.ConstructorArguments[1].Value?.ToString())
                    .FirstOrDefault();

                if (url != null)
                {
                    var idx = url.LastIndexOf('.');
                    if (idx >= 0)
                    {
                        return url.Substring(0, url.LastIndexOf('.'));
                    }
                }

                return url;
            }
        }
    }
}