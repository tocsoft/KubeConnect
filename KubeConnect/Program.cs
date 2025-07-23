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
using System.Net.NetworkInformation;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using k8s.Util.Common;
using System.Net.Http;
using System.Net.Sockets;

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


            const string semaphoreName = $"KubeConnect:FEC9031C-3BFD-4F5D-91D9-AC7B93074499";
            if (parseArgs.Action == Args.KubeConnectMode.Connect)
            {
                if (!CheckAvailableServerPort(parseArgs.MainPort))
                {
                    console.WriteErrorLine("There is another instance of KubeConnect running exposing the cluster to your machine. Only once instance in 'connect' mode is allows at once.");
                    return -1;
                }
            }

            if (parseArgs.Action == Args.KubeConnectMode.Connect)
            {
                if (parseArgs.RequireAdmin && !RootChecker.IsRoot())
                {
                    return await AdminRunner.RunProcessAsAdmin(parseArgs, console);
                }

                var config = KubernetesClientConfigurationHelper.BuildConfig(parseArgs.KubeconfigFile, parseArgs.Context);
                if (parseArgs.KubeSkipSsl.HasValue)
                {
                    config.SkipTlsVerify = parseArgs.KubeSkipSsl.Value;
                }
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
                catch (Exception ex)
                {
                    console.WriteErrorLine(ex.ToString());
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

                var config = KubernetesClientConfigurationHelper.BuildConfig(parseArgs.KubeconfigFile, parseArgs.Context);
                var serviceName = parseArgs.BridgeMappings[0].ServiceName;
                var tcs = new TaskCompletionSource<object?>();

                IKubernetes client = new Kubernetes(config);
                parseArgs.Namespace ??= config.Namespace ?? "default";

                var currentNamespace = parseArgs.Namespace ?? config.Namespace ?? "default";

                var manager = new ServiceManager(client, currentNamespace, console, parseArgs);
                // ensure we load up configs form k8s
                await manager.LoadBindings();

                var service = manager.GetRequiredService(serviceName);

                // fail to connect then stuff not running
                if (parseArgs.Action == Args.KubeConnectMode.Bridge)
                {
                    // lets call home first to see if the conenct server is running

                    var response = await new HttpClient()
                        .GetAsync($"http://localhost:{parseArgs.MainPort}/status");

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.Error.WriteLine("Failed to connect bridge, ensure kubeconnect is running in 'connect' mode");
                        return -1;
                    }

                    var ports = parseArgs.BridgeMappings
                    .Where(x => x.ServiceName == serviceName);

                    var defaultPort = service.TcpPorts.FirstOrDefault();
                    var bridgePorts = ports.Select(x => (
                        RemotePort: x.RemotePort == -1 ? defaultPort.listenPort : x.RemotePort,
                        LocalPort: x.LocalPort == -1 ? defaultPort.destinationPort : x.LocalPort)).ToList();
                    // handle default unmapped ports???

                    var cts = new CancellationTokenSource(45000);
                    SshClient? sshClient = null;
                    while (!cts.IsCancellationRequested)
                    {
                        sshClient = new SshClient(serviceName, 2222, "linuxserver.io", "password");
                        try
                        {
                            sshClient.ConnectionInfo.Timeout = TimeSpan.FromSeconds(4);
                            sshClient.Connect();

                            foreach (var mappings in bridgePorts)
                            {
                                var port = new ForwardedPortRemote(IPAddress.Any, (uint)mappings.RemotePort, IPAddress.Loopback, (uint)mappings.LocalPort);
                                sshClient.AddForwardedPort(port);
                                port.RequestReceived += (object? sender, Renci.SshNet.Common.PortForwardEventArgs e) =>
                                {
                                    try
                                    {
                                        console.WriteLine($"Traffic redirected from {service.ServiceName}:{mappings.RemotePort} to localhost:{mappings.LocalPort}");
                                    }
                                    catch
                                    {

                                    }
                                };
                                port.Start();
                            }

                            // if they all started report it to the console
                            // maybe should be handled in program??
                            foreach (var mappings in bridgePorts)
                            {
                                console.WriteLine($"Redirecting traffic from {service.ServiceName}:{mappings.RemotePort} to localhost:{mappings.LocalPort}");
                            }
                            break;
                        }
                        catch when (!cts.IsCancellationRequested)
                        {
                            client?.Dispose();
                        }
                    }

                    if (sshClient != null)
                    {
                        sshClient.ErrorOccurred += (s, e) =>
                        {
                            tcs.TrySetResult(null);
                            Console.Error.WriteLine("Bridge connection error, shutting down");
                        };
                    }
                    else
                    {
                        Console.Error.WriteLine("Failed to connect bridge, ensure kubeconnect is running in 'connect' mode");
                        return -1;
                    }
                }

                var runexe = parseArgs.Action == Args.KubeConnectMode.Run || parseArgs.UnprocessedArgs.Length > 0;

                if (runexe)
                {
                    if (parseArgs.UnprocessedArgs.Length == 0)
                    {
                        throw new Exception("you must specify the process to start");
                    }

                    var envVars = service.EnvVars;
                    var exeArgs = parseArgs.UnprocessedArgs.AsSpan().Slice(1).ToArray();
                    var proceStartInfo = new ProcessStartInfo(parseArgs.UnprocessedArgs[0]);
                    proceStartInfo.WorkingDirectory = parseArgs.WorkingDirectory;

                    foreach (var a in exeArgs)
                    {
                        proceStartInfo.ArgumentList.Add(a);
                    }

                    foreach (var a in envVars)
                    {
                        proceStartInfo.Environment[a.Key] = a.Value;
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
                    console.CancelKeyPress += delegate
                    {
                        // cancel has been triggered, we can stop waiting
                        tcs.TrySetResult(null);
                    };

                    console.WriteLine("\nHit Ctrl+C to stop bridging service into cluster.");
                    //wait for cancel
                    await tcs.Task;
                }
            }

            return 0;
        }
        private static bool CheckAvailableServerPort(int port)
        {
            bool isAvailable = true;

            // Evaluate current system tcp connections. This is the same information provided
            // by the netstat command line application, just in .Net strongly-typed object
            // form.  We will look through the list, and if our port we would like to use
            // in our TcpClient is occupied, we will set isAvailable to false.
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();

            foreach (IPEndPoint endpoint in tcpConnInfoArray)
            {
                if (endpoint.Port == port)
                {
                    isAvailable = false;
                    break;
                }
            }

            return isAvailable;
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

    public static class KubernetesClientConfigurationHelper
    {
        public static KubernetesClientConfiguration BuildConfig(string? kubeconfigPath = null, string? currentContext = null)
        {
            kubeconfigPath ??= KubernetesClientConfiguration.KubeConfigDefaultLocation;

            if (File.Exists(kubeconfigPath))
            {
                return KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeconfigPath, currentContext: currentContext);
            }

            if (KubernetesClientConfiguration.IsInCluster())
            {
                return KubernetesClientConfiguration.InClusterConfig();
            }

            var config = new KubernetesClientConfiguration();
            config.Host = "http://localhost:8080";
            return config;
        }

    }
}
