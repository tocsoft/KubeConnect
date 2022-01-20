using k8s;
using KubeConnect.RunAdminProcess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
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


                return version ?? assembly.GetName().Version?.ToString() ?? string.Empty;
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

                return url ?? string.Empty;
            }
        }

        private static async Task<int> Main(string[] args)
        {
            Args parseArgs = new Args(args);
            if (parseArgs.AttachDebugger)
            {
                Debugger.Launch();
            }

            IConsole console;
            if (!string.IsNullOrWhiteSpace(parseArgs.ConsolePipeName))
            {
                console = new IPCServiceConsole(parseArgs.ConsolePipeName);
            }
            else
            {
                console = ConsoleWrapper.Instance;
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
");
            }

            var mutex = new Mutex(false, $"KubeConnect:FEC9031C-3BFD-4F5D-91D9-AC7B93074499");
            if (parseArgs.Action == Args.KubeConnectMode.Connect)
            {
                // skip the check if elivated as the host exe has already done
                if (!parseArgs.Elevated)
                {

                    // only one instance per cluster is allowed past here
                    var claimedMutex = mutex.WaitOne(1, false);
                    if (!claimedMutex)
                    {
                        console.WriteErrorLine("There is another instance of KubeConnect running exposing the cluster to your machine. Only once instance in 'connect' mode is allows at once.");
                        return -1;
                    }
                }
            }
            else if (parseArgs.Action == Args.KubeConnectMode.Bridge)
            {
                // should we not auto elevate and launch connect as required???
                var claimedMutex = mutex.WaitOne(1, false);
                if (claimedMutex)
                {
                    mutex.ReleaseMutex();
                    console.WriteErrorLine("You must also be running a separate KubeConnect session in 'connect' mode to enable bridging across a service.");
                    return -1;
                }
            }

            if (parseArgs.RequireAdmin && !RootChecker.IsRoot())
            {
                return await AdminRunner.RunProcessAsAdmin(parseArgs, console);
            }

            var config = KubernetesClientConfiguration.BuildDefaultConfig();

            var cts = new CancellationTokenSource();

            IKubernetes client = new Kubernetes(config);
            parseArgs.Namespace ??= config.Namespace ?? "default";

            var currentNamespace = parseArgs.Namespace ?? config.Namespace ?? "default";

            var manager = new ServiceManager(client, currentNamespace, console, parseArgs);
            // ensure we load up configs form k8s
            await manager.LoadBindings();
            var builder = CreateHostBuilder(manager, console, client, parseArgs);

            var serverHost = builder.Build();

            var lifetime = serverHost.Services.GetService<IHostApplicationLifetime>();
            if (parseArgs.LaunchBrowser)
            {
                lifetime?.ApplicationStarted.Register(() =>
                {
                    if (!manager.IngressConfig.Enabled) return;

                    var address = manager.IngressConfig.Addresses.FirstOrDefault();

                    if (address != null)
                    {
                        OpenUrl(address);
                    }
                });
            }

            var tcs = new TaskCompletionSource<object?>();
            console.CancelKeyPress += delegate
            {
                // cancel has been triggered, we can stop waiting
                tcs.TrySetResult(null);

                if (parseArgs.Action == Args.KubeConnectMode.Connect)
                {
                    console.WriteLine("Shutting down!");
                    cts.Cancel();
                    _ = serverHost.StopAsync();
                }
            };


            if (parseArgs.Action == Args.KubeConnectMode.Connect)
            {
                try
                {
                    console.WriteLine("Starting port forward!");

                    // we should be safe to do this here as we have a mutex claimed ensuring there is only one 'connect' running
                    // once its started we should be safe to reverse the ssh server deployments read for exposing
                    await manager.ReleaseAll();
                    await serverHost.RunAsync();
                }
                catch (IOException ex) when (ex.InnerException is Microsoft.AspNetCore.Connections.AddressInUseException ain)
                {
                    var msg = ex.Message;
                    if (manager.IngressConfig.Enabled)
                    {
                        var host = manager.IngressConfig.HostNames.FirstOrDefault();
                        if (host != null)
                        {
                            msg = msg.Replace($"{manager.IngressConfig.AssignedAddress}", host);
                        }
                    }
                    foreach (var s in manager.Services)
                    {
                        msg = msg.Replace($"{s.AssignedAddress}", $"{s.ServiceName}");
                    }
                    console.WriteErrorLine(msg);
                    return 1;
                }
                catch (Exception)
                {
                    throw;
                }
                finally
                {
                    await manager.ReleaseAll();
                }
            }
            else
            {
                if (parseArgs.BridgeMappings.Count != 1)
                {
                    throw new Exception("Exactly 1 bridged service must be supplied");
                }

                var service = manager.GetService(parseArgs.BridgeMappings[0].ServiceName);
                try
                {
                    if (parseArgs.Action == Args.KubeConnectMode.Bridge)
                    {
                        //start up the bridge stuff here
                        await manager.Intercept(service);
                    }

                    var runexe = parseArgs.Action == Args.KubeConnectMode.Run || parseArgs.UnprocessedArgs.Length > 0;

                    if (runexe)
                    {
                        if (parseArgs.UnprocessedArgs.Length == 0)
                        {
                            throw new Exception("you must specify the process to start");
                        }

                        var envVars = await manager.GetEnvironmentVariablesForServiceAsync(service);
                        var exeArgs = parseArgs.UnprocessedArgs.AsSpan().Slice(1).ToArray();
                        var proceStartInfo = new ProcessStartInfo(parseArgs.UnprocessedArgs[0]);
                        proceStartInfo.WorkingDirectory = parseArgs.WorkingDirectory;

                        foreach (var a in exeArgs)
                        {
                            proceStartInfo.ArgumentList.Add(a);
                        }

                        foreach (var a in envVars)
                        {
                            proceStartInfo.EnvironmentVariables.Add(a.Key, a.Value);
                        }

                        var process = Process.Start(proceStartInfo);
                        if (process != null)
                        {
                            ChildProcessTracker.AddProcess(process);
                            process.WaitForExit();
                            return process.ExitCode;
                        }

                        return -1;
                    }
                    else
                    {
                        console.WriteLine("\nHit Ctrl+C to stop bridging service into cluster.");
                        //wait for cancel
                        await tcs.Task;
                    }
                }
                finally
                {
                    if (parseArgs.Action == Args.KubeConnectMode.Bridge)
                    {
                        await manager.Release(service);
                    }
                }
            }

            return 0;
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        private static IHostBuilder CreateHostBuilder(ServiceManager manager, IConsole console, IKubernetes kubernetes, Args args)
            => Host.CreateDefaultBuilder(Array.Empty<string>())
                .ConfigureLogging((s, o) =>
                {
                    o.ClearProviders();
                    o.Services.AddSingleton<ILoggerProvider, IConsoleLogProvider>();
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton(args);
                    services.AddSingleton(kubernetes);
                    services.AddSingleton(manager);
                    services.AddSingleton(console);
                    services.AddPortForwarder();
                    services.AddHostedService<HostsFileUpdater>();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.PreferHostingUrls(false);
                    webBuilder.UseUrls(Array.Empty<string>());

                    webBuilder.UseStartup<Startup>();
                });
    }
}
