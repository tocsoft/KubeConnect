using KubeConnect;
using KubeConnect.Common;
using KubeConnect.Ingress;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Renci.SshNet.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Configuration;

namespace KubeConnect
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddReverseProxy();
            services.AddSingleton<IProxyConfigProvider, IngressProxyConfig>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime applicationLifetime, Args args)
        {
            Dictionary<string, (SshBridge bridge, BridgeLogger logger)> bridges = new(StringComparer.OrdinalIgnoreCase);
            applicationLifetime.ApplicationStopping.Register(() =>
            {
                var pb = bridges.ToArray();
                bridges.Clear();
                foreach (var bridge in pb)
                {
                    try
                    {
                        bridge.Value.bridge.DisposeAsync().GetAwaiter().GetResult();
                    }
                    catch
                    {
                        // Ignore exceptions during disposal
                    }
                }
            });

            SshBridge? TryGetBridge(string name)
            {
                if (bridges.TryGetValue(name, out var bridge))
                {
                    if (bridge.bridge.Status == "Stopped")
                    {
                        bridges.Remove(name);
                    }
                    else
                    {
                        return bridge.bridge;
                    }
                }
                return null;
            }

            BridgeLogger? TryGetBridgeLogger(string name)
            {
                if (bridges.TryGetValue(name, out var bridge))
                {
                    if (bridge.bridge.Status == "Stopped")
                    {
                        bridges.Remove(name);
                    }
                    else
                    {
                        return bridge.logger;
                    }
                }
                return null;
            }

            app.UseRouting();
            app.UseWebSockets();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapReverseProxy();

                endpoints.MapPut("/bridge/{serviceName}", async (string serviceName, [FromBody] BridgeStartupRequest settings, [FromServices] ServiceManager serviceManager) =>
                {
                    var service = serviceManager.GetService(serviceName);
                    if (service is null)
                    {
                        return Results.NotFound();
                    }
                    var bridge = TryGetBridge(serviceName);
                    if (bridge is not null)
                    {
                        return Results.Conflict("Service Already Bridged");
                    }

                    var logger = new BridgeLogger();
                    bridge = new SshBridge(serviceManager, logger, serviceName, settings.RemotePorts, settings.LocalPort);
                    bridges[serviceName] = (bridge, logger);
                    _ = bridge.Start(settings.Pid, default); // when running an console mode!
                    return Results.Accepted();
                })
                .RequireHost("localhost");

                endpoints.MapDelete("/bridge/{serviceName}", async (string serviceName, [FromServices] ServiceManager serviceManager) =>
                {
                    var sshBridge = TryGetBridge(serviceName);
                    if (sshBridge is not null)
                    {
                        var t = sshBridge.DisposeAsync();

                        _ = t.AsTask().ContinueWith((t) =>
                        {
                            var service = serviceManager.GetService(serviceName);
                            if (service is not null)
                            {
                                _ = serviceManager.Release(service);
                            }
                            bridges.Remove(serviceName);
                        });
                    }
                    return Results.NotFound();
                })
                .RequireHost("localhost");

                endpoints.MapGet("/bridge/{serviceName}", async ([FromRoute] string serviceName, HttpContext ctx, [FromServices] ServiceManager serviceManager) =>
                {
                    var service = serviceManager.GetService(serviceName);
                    if (service is null)
                    {
                        return Results.NotFound();
                    }

                    var sshBridge = TryGetBridge(serviceName);
                    if (sshBridge is not null)
                    {
                        return Results.Ok(new BridgeStatusResult(serviceName, true, sshBridge.Status, sshBridge.LocalPort));
                    }

                    return Results.Ok(new BridgeStatusResult(serviceName, false, "Offline", 0));
                })
                .RequireHost("localhost");

                async Task WriteLogs(BridgeLogger logger, Stream body, CancellationToken cancellationToken)
                {
                    var sem = new SemaphoreSlim(1);

                    async Task WriteMessage(string message)
                    {
                        sem.Wait();
                        var data = Encoding.UTF8.GetBytes($"{message}{Environment.NewLine}");
                        await body.WriteAsync(data);
                        sem.Release();
                    }
                    using var handle = logger.Subscribe(WriteMessage);

                    foreach (var m in logger.Logs)
                    {
                        await WriteMessage(m.message);
                    }
                    // wait untill take completed!!
                    var tcs = new TaskCompletionSource();
                    cancellationToken.Register(() =>
                    {
                        tcs.SetResult();
                    });
                    await tcs.Task;
                }

                endpoints.MapGet("/bridge/{serviceName}/logs", async ([FromRoute] string serviceName, HttpContext ctx, [FromServices] ServiceManager serviceManager, DateTime? since = null) =>
                {
                    var service = serviceManager.GetService(serviceName);
                    if (service is null)
                    {
                        return Results.NotFound();
                    }

                    var sshBridge = TryGetBridgeLogger(serviceName);
                    if (sshBridge is null)
                    {
                        return Results.Ok();
                    }

                    await WriteLogs(sshBridge, ctx.Response.Body, ctx.RequestAborted);

                    return Results.Ok();
                })
                .RequireHost("localhost");

                endpoints.MapGet("/service/{serviceName}/logs", async ([FromRoute] string serviceName, HttpContext ctx, [FromServices] ServiceManager serviceManager, DateTime? since = null) =>
                {
                    var service = serviceManager.GetService(serviceName);
                    if (service is null)
                    {
                        return Results.NotFound();
                    }

                    var sshBridge = TryGetBridgeLogger(serviceName);
                    // if bridging use the bridge logs
                    if (sshBridge is not null)
                    {
                        await WriteLogs(sshBridge, ctx.Response.Body, ctx.RequestAborted);
                        return Results.Ok();
                    }

                    // otherwise use the svc logs
                    using var logStream = await serviceManager.GetServiceLogStream(service);
                    if (logStream is null)
                    {
                        return Results.NotFound();
                    }

                    await logStream.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);

                    return Results.Ok();
                })
                .RequireHost("localhost");


                endpoints.MapGet("/status", async ([FromServices] ServiceManager serviceManager) =>
                {
                    List<ServiceDetailResult> serviceDetailResults = new();

                    foreach (var service in serviceManager.Services)
                    {
                        var bridged = false;
                        var bridgedPort = 0;
                        var sshBridge = TryGetBridge(service.ServiceName);
                        if (sshBridge is not null)
                        {
                            bridged = sshBridge.Status != "Stopped";
                            bridgedPort = sshBridge.LocalPort;
                        }

                        var status = await serviceManager.GetServiceStatus(service);
                        if (bridged)
                        {
                            status = "Bridged";
                        }

                        var envvars = new Dictionary<string, string>();
                        foreach (var e in service.EnvVars)
                        {
                            envvars[e.Key] = e.Value;
                        }
                        var r = new ServiceDetailResult(
                            service.ServiceName,
                            status,
                            bridged,
                            bridgedPort,
                            service.TcpPorts.Select(p => $"{p.listenPort switch
                            {
                                80 => "http",
                                443 => "https",
                                _ => "tcp"
                            }}://{service.ServiceName}:{p.listenPort}"),
                            envvars);
                        serviceDetailResults.Add(r);
                    }

                    var ingressUrls = serviceManager.IngressConfig.Addresses.Select(x => x);

                    return new StatusResult(Program.CurrentVersion, serviceDetailResults, ingressUrls);
                })
                .RequireHost("localhost");
            });
        }

        public class BridgeLogger : ILogger
        {
            private int _position = -1;
            private int _written = 0;
            private (DateTime date, string message)[] _logs = new (DateTime date, string message)[512];

            private ConcurrentDictionary<Func<string, Task>, IDisposable> callbacks = new();

            public IDisposable Subscribe(Func<string, Task> cb)
            {
                var h = new DisposableHandle(() =>
                {
                    callbacks.TryRemove(cb, out _);
                });
                callbacks.TryAdd(cb, h);
                return h;
            }
            private class DisposableHandle : IDisposable
            {
                private Action _dispose;

                public DisposableHandle(Action dispose)
                {
                    _dispose = dispose;
                }

                public void Dispose()
                {
                    _dispose();
                }
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
                => null;

            public bool IsEnabled(LogLevel logLevel)
                => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _position++;
                _position %= _logs.Length;
                var msg = formatter(state, exception);

                // buffer and process buffer on a background thread?
                foreach (var callback in callbacks.Keys)
                {
                    callback(msg).GetAwaiter().GetResult();
                }

                _logs[_position] = (DateTime.UtcNow, msg);
                _written = Math.Min(_logs.Length, _written + 1);
            }

            public IEnumerable<(DateTime date, string message)> Logs
            {
                get
                {
                    var start = (_position + 1 - _written) % _logs.Length;
                    for (var i = 0; i < _written; i++)
                    {
                        var pos = (start + i) % _logs.Length;
                        yield return this._logs[pos];
                    }
                }
            }
        }
    }
}
