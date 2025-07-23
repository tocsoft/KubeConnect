using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.KubeConnect;

public class KubeConnectServiceResource : Resource, IResourceWithParent
{
    public KubeConnectServiceResource(KubeConnectResource parent, string name, string serviceName)
        : base(name)
    {
        ServiceName = serviceName;
        Parent = parent;
    }

    public KubeConnectServiceResource(KubeConnectResource parent, string name)
        : this(parent, name, name)
    {
    }

    public string ServiceName { get; }

    public KubeConnectResource Parent { get; }

    IResource IResourceWithParent.Parent => Parent;
}

internal record KubeConnectServicePortsAnnotation(IEnumerable<int> Ports) : IResourceAnnotation
{ }
