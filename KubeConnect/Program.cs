using k8s;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace KubeConnect
{
    class Program
    {
        public static string CurrentVersion
        {
            get
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetCustomAttributesData()
                    .Where(x => x.AttributeType == typeof(AssemblyInformationalVersionAttribute))
                    .Select(x => x.ConstructorArguments[0].Value?.ToString())
                    .FirstOrDefault();


                return version ?? assembly.GetName().Version.ToString();
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

        private static int Main(string[] args)
        {
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
                return RunProcessAsAdmin(parseArgs, console).GetAwaiter().GetResult();
            }

            var cts = new CancellationTokenSource();
            console.CancelKeyPress += delegate
            {
                console.WriteLine("Shutting down");

                cts.Cancel();
            };

            var config = KubernetesClientConfiguration.BuildDefaultConfig();
            IKubernetes client = new Kubernetes(config);
            console.WriteLine("Starting port forward!");

            var currentNamespace = config.Namespace ?? "default";

            var manager = new ServiceManager(client, currentNamespace, console);
            manager.RunPortForwardingAsync(cts.Token).GetAwaiter().GetResult();

            return 0;
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
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
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
    }
}
