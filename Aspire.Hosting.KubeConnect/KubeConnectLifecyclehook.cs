using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;

namespace Aspire.Hosting.KubeConnect;

internal class KubeConnectLifecyclehook(
    KubeConnectManager manager,
    DistributedApplicationExecutionContext executionContext,
    ResourceNotificationService resourceNotificationService,
    ResourceLoggerService resourceLoggerService) : IDistributedApplicationLifecycleHook//, IAsyncDisposable
{

    public Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        if (executionContext.IsPublishMode)
        {
            return Task.CompletedTask;
        }
        return manager.InitKubeConnect(appModel, cancellationToken); ;
    }

    public async Task AfterEndpointsAllocatedAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        if (executionContext.IsPublishMode)
        {
            return;
        }

        // now we have endpoints we know where to redirect stuff to.
        var resources = appModel.Resources.OfType<ProjectResource>();

        foreach (var r in resources)
        {
            if (r.TryGetAnnotationsOfType<KubeConnectBridgeAnnotation>(out var targets))
            {
                var logger = resourceLoggerService.GetLogger(r);

                var session = new KubeConnectBridgeSessionSet(appModel, logger, r, targets)
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
                if (targets.Any(x => x.AutoStart))
                {
                    await session.Start();
                }
            }
        }
    }

    private class DisposableHandle : IAsyncDisposable
    {
        private Func<ValueTask> _dispose;

        public DisposableHandle(Func<ValueTask> dispose)
        {
            _dispose = dispose;
        }

        public ValueTask DisposeAsync()
        {
            return _dispose();
        }
    }
}