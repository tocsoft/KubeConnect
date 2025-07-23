using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.KubeConnect;

public class KubeConnectBridgeResource : Resource, IResourceWithParent
{
    public KubeConnectBridgeResource(ProjectResource parent, string serviceName)
        : base($"{serviceName}-bridge")
    {
        ServiceName = serviceName;
        Parent = parent;
    }

    public string ServiceName { get; }

    public ProjectResource Parent { get; }

    IResource IResourceWithParent.Parent => Parent;
}
