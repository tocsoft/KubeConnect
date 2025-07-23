using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.KubeConnect;

public class KubeConnectBridgeAnnotation : IResourceAnnotation
{
    public KubeConnectBridgeAnnotation(string serviceName, bool autoStart)
    {
        ServiceName = serviceName;
        AutoStart = autoStart;
    }

    public string ServiceName { get; }

    public bool AutoStart { get; }
}
