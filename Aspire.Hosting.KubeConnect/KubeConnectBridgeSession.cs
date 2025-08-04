using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.KubeConnect;

public class KubeConnectBridgeSessionSet : IAsyncDisposable, IResourceAnnotation
{
    private List<KubeConnectBridgeSession> sessions = new List<KubeConnectBridgeSession>();

    public Action<KubeConnectBridgeSessionSet>? OnStatusUpdate { get; set; }

    private KubeConnectManager _manager;

    public ProjectResource Resource { get; init; }

    public string Status
    {
        get
        {
            if (sessions.GroupBy(x => x.Status).Count() == 1)
            {
                return sessions.First().Status;
            }

            return "Starting";
        }
    }

    internal KubeConnectBridgeSessionSet(KubeConnectManager manager, ProjectResource project, IEnumerable<KubeConnectBridgeAnnotation> targets)
    {
        _manager = manager;
        Resource = project;
        foreach (var t in targets)
        {
            var session = new KubeConnectBridgeSession(project, t);
            session.OnStatusUpdate = (s) =>
            {
                OnStatusUpdate?.Invoke(this);
            };
            sessions.Add(session);
        }
    }

    public Task Start()
        => Task.WhenAll(sessions.Select(s => _manager.StartBridge(s)));

    public Task Stop()
        => Task.WhenAll(sessions.Select(s => _manager.StopBridge(s)));

    public async ValueTask DisposeAsync()
    {
        foreach (var s in sessions)
        {
            await Stop();
        }

        sessions.Clear();
    }
}

public class KubeConnectBridgeSession
{
    internal KubeConnectBridgeSession(ProjectResource project, KubeConnectBridgeAnnotation target)
    {
        Resource = project;
        Target = target;
    }

    public ProjectResource Resource { get; init; }

    public KubeConnectBridgeAnnotation Target { get; init; }

    private string _status = KnownResourceStates.NotStarted;

    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            OnStatusUpdate?.Invoke(this);
        }
    }

    public Action<KubeConnectBridgeSession>? OnStatusUpdate { get; set; }


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
