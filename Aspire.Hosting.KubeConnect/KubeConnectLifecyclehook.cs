using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;

namespace Aspire.Hosting.KubeConnect;

internal class KubeConnectLifecyclehook(
    KubeConnectManager manager,
    DistributedApplicationExecutionContext executionContext,
    ResourceNotificationService resourceNotificationService,
    ResourceLoggerService resourceLoggerService) : IDistributedApplicationEventingSubscriber
{
    private DistributedApplicationEventSubscription? _beforeStart;
    private DistributedApplicationEventSubscription? _resAllocated;

    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        _beforeStart = eventing.Subscribe<BeforeStartEvent>(BeforeStartAsync);
        _resAllocated = eventing.Subscribe<ResourceEndpointsAllocatedEvent>(ResourceEndpointsAllocated);
        return Task.CompletedTask;
    }

    public Task BeforeStartAsync(BeforeStartEvent e, CancellationToken cancellationToken = default)
    {
        var appModel = e.Model;
        if (executionContext.IsPublishMode)
        {
            return Task.CompletedTask;
        }
        return manager.InitKubeConnect(cancellationToken);
    }

    public async Task ResourceEndpointsAllocated(ResourceEndpointsAllocatedEvent e, CancellationToken cancellationToken = default)
    {
        if (e.Resource is not ProjectResource r)
        {
            return;
        }

        if (r.TryGetAnnotationsOfType<KubeConnectBridgeAnnotation>(out var targets))
        {
            var logger = resourceLoggerService.GetLogger(r);

            await resourceNotificationService.PublishUpdateAsync(r, s =>
            {
                return s with
                {
                    Urls = [],
                };
            });
            var session = new KubeConnectBridgeSessionSet(manager, r, targets)
            {
                OnStatusUpdate = (session) =>
                {
                    resourceNotificationService.PublishUpdateAsync(session.Resource, s =>
                    {
                        var startCommand = s.Commands.First(x => x.Name == $"start-kubeconnect-bridge-{session.Resource.Name}");
                        var stopCommand = s.Commands.First(x => x.Name == $"stop-kubeconnect-bridge-{session.Resource.Name}");
                        var startCommandUpdated = startCommand with
                        {
                            State = session.Status switch
                            {
                                "Finished" => ResourceCommandState.Enabled,
                                "Stopped" => ResourceCommandState.Enabled,
                                "Starting" => ResourceCommandState.Disabled,
                                _ => ResourceCommandState.Hidden,
                            }
                        };
                        var stopCommandUpdated = stopCommand with
                        {
                            State = session.Status switch
                            {
                                "Running" => ResourceCommandState.Enabled,
                                "Stopping" => ResourceCommandState.Disabled,
                                _ => ResourceCommandState.Hidden,
                            },
                        };

                        return s with
                        {
                            Commands = [.. s.Commands.Except([startCommand, stopCommand]), startCommandUpdated, stopCommandUpdated]
                        };
                    });
                }
            };

            r.Annotations.Add(session);

            var autoStart = targets.Any(x => x.AutoStart);

            if (r.TryGetLastAnnotation<ExplicitStartupAnnotation>(out _))
            {
                autoStart = false;
            }

            if (autoStart)
            {
                await session.Start();
            }
        }
    }
}