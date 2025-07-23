using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Humanizer.Localisation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aspire.Hosting.KubeConnect;

public static class KubeConnectResourceExtensions
{
    public static IResourceBuilder<KubeConnectServiceResource> AddKubeConnectService(this IResourceBuilder<KubeConnectResource> builder, string name)
    {
        return builder.ApplicationBuilder
            .AddResource(new KubeConnectServiceResource(builder.Resource, name))
            .ExcludeFromManifest()
            .WithInitialState(new()
            {
                ResourceType = "gears",
                Properties = [
                new(CustomResourceKnownProperties.Source, "Kubernetes")
            ]
            });
    }

    public static IResourceBuilder<ProjectResource> AsKubeConnectService(this IResourceBuilder<ProjectResource> builder, bool autoStart = true)
        => builder.AsKubeConnectService(builder.Resource.Name);

    public static IResourceBuilder<ProjectResource> AsKubeConnectService(this IResourceBuilder<ProjectResource> builder, string serviceName, bool autoStart = true)
    {
        builder
            .WithAnnotation(new KubeConnectBridgeAnnotation(serviceName, autoStart));

        builder.WithCommand($"start-kubeconnect-bridge-{builder.Resource.Name}", "Start KubeConnect bridge", async (context) =>
        {
            if (builder.Resource.TryGetLastAnnotation<KubeConnectBridgeSessionSet>(out var session))
            {
                await session.Start();

                return new ExecuteCommandResult() { Success = true };
            }

            return new ExecuteCommandResult() { Success = false, ErrorMessage = "Bridging not setup for this project" };
        },
        new CommandOptions()
        {
            IsHighlighted = true,
            UpdateState = (ctx) =>
            {
                return ctx.ResourceSnapshot.Commands.FirstOrDefault(x => x.Name == $"start-kubeconnect-bridge-{builder.Resource.Name}")?.State ?? ResourceCommandState.Enabled;
            },
            IconName = "PlugConnected",
        });

        builder.WithCommand($"stop-kubeconnect-bridge-{builder.Resource.Name}", "Stop KubeConnect bridge", async (context) =>
        {
            if (builder.Resource.TryGetLastAnnotation<KubeConnectBridgeSessionSet>(out var session))
            {
                await session.Stop();

                return new ExecuteCommandResult() { Success = true };
            }

            return new ExecuteCommandResult() { Success = false, ErrorMessage = "Bridging not setup for this project" };
        },
        new CommandOptions()
        {
            IsHighlighted = true,
            UpdateState = (ctx) =>
            {
                return ctx.ResourceSnapshot.Commands.FirstOrDefault(x => x.Name == $"stop-kubeconnect-bridge-{builder.Resource.Name}")?.State ?? ResourceCommandState.Hidden;
            },
            IconName = "PlugDisconnected",
        });

        return builder;
    }

    public static IResourceBuilder<KubeConnectServiceResource> AddKubeConnectService(this IResourceBuilder<KubeConnectResource> builder, string name, string serviceName)
    {
        return builder.ApplicationBuilder.AddResource(new KubeConnectServiceResource(builder.Resource, name, serviceName)).ExcludeFromManifest().WithInitialState(new()
        {
            ResourceType = "gears",
            Properties = [
                new(CustomResourceKnownProperties.Source, "Kubernetes")
            ]
        });
    }

    public static IResourceBuilder<KubeConnectResource> WithShowDiscoveredServices(this IResourceBuilder<KubeConnectResource> builder, bool showServices = true)
        => builder.WithAnnotation(new KubeConnectResourceShowServicesAnnotation(showServices), ResourceAnnotationMutationBehavior.Replace);

    public static IResourceBuilder<KubeConnectResource> AddKubeConnect(this IDistributedApplicationBuilder builder, string name)
    {
        var kube = builder.Resources.OfType<KubeConnectResource>().SingleOrDefault();

        if (kube is not null)
        {
            // You only need one yarp resource per application
            throw new InvalidOperationException("A KubeConnect resource has already been added to this application");
        }

        builder.Services.TryAddLifecycleHook<KubeConnectLifecyclehook>();
        builder.Services.TryAddSingleton<KubeConnectManager>();

        var resource = new KubeConnectResource(name);
        return builder.AddResource(resource)
            .WithShowDiscoveredServices(false)
            .ExcludeFromManifest()
            .WithInitialState(new()
            {
                ResourceType = "gears",
                Properties = [
                    new(CustomResourceKnownProperties.Source, "Kubernetes")
                ]
            });

        // REVIEW: YARP resource type?
        //.WithManifestPublishingCallback(context =>
        // {
        //     context.Writer.WriteString("type", "yarp.v0");

        //     context.Writer.WriteStartObject("routes");
        //     // REVIEW: Make this less YARP specific
        //     foreach (var r in resource.RouteConfigs.Values)
        //     {
        //         context.Writer.WriteStartObject(r.RouteId);

        //         context.Writer.WriteStartObject("match");
        //         context.Writer.WriteString("path", r.Match.Path);

        //         if (r.Match.Hosts is not null)
        //         {
        //             context.Writer.WriteStartArray("hosts");
        //             foreach (var h in r.Match.Hosts)
        //             {
        //                 context.Writer.WriteStringValue(h);
        //             }
        //             context.Writer.WriteEndArray();
        //         }
        //         context.Writer.WriteEndObject();
        //         context.Writer.WriteString("destination", r.ClusterId);
        //         context.Writer.WriteEndObject();
        //     }
        //     context.Writer.WriteEndObject();
        // });
    }
}
internal record KubeConnectResourceShowServicesAnnotation(bool ShowServices) : IResourceAnnotation
{
}
