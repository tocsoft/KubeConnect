using k8s;
using KubeConnect.RunAdminProcess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
            var parseArgs = new Args(args);
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
{RepositoryUrl}");
            }

            if (!RootChecker.IsRoot())
            {
                return await AdminRunner.RunProcessAsAdmin(parseArgs, console);
            }

            var cts = new CancellationTokenSource();

            var config = KubernetesClientConfiguration.BuildDefaultConfig();
            IKubernetes client = new Kubernetes(config);
            console.WriteLine("Starting port forward!");
            parseArgs.Namespace ??= config.Namespace ?? "default";

            var currentNamespace = parseArgs.Namespace ?? config.Namespace ?? "default";

            var manager = new ServiceManager(client, currentNamespace, console);
            // ensure we load up configs form k8s
            await manager.LoadBindings();

            var serverHost = CreateHostBuilder(manager, console, client).Build();

            var lifetime = serverHost.Services.GetService<IHostApplicationLifetime>();
            if (parseArgs.LaunchBrowser)
            {
                lifetime.ApplicationStarted.Register(() =>
                {
                    var host = manager.IngressHostNames.FirstOrDefault();

                    if (host != null)
                    {
                        OpenUrl($"https://{host}");
                    }
                });
            }

            console.CancelKeyPress += delegate
            {
                console.WriteLine("Shutting down!");
                cts.Cancel();
                _ = serverHost.StopAsync();
            };
            await serverHost.RunAsync(cts.Token);

            return 0;
        }

        private static IHostBuilder CreateHostBuilder(ServiceManager manager, IConsole console, IKubernetes kubernetes) =>
            Host.CreateDefaultBuilder(Array.Empty<string>())
                .ConfigureLogging((s, o) =>
                {
                    o.ClearProviders();
                    o.Services.AddSingleton<ILoggerProvider, IConsoleLogProvider>();
                })
                .ConfigureServices(services =>
                {
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
    }
}
