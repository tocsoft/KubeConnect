using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.KubeConnect;

public class KubeConnectResource(string name) : Resource(name)
{
    public string? KubeConfigFile { get; set; }
    public string? ContextName { get; set; }
    public string? Namespace { get; set; }
    public int MainPort { get; set; } = 10401;
}
