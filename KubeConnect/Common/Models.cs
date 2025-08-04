using System;
using System.Collections.Generic;

namespace KubeConnect.Common;


public class BridgeStartupRequest
{
    public IEnumerable<int> RemotePorts { get; set; } = Array.Empty<int>();
    public int LocalPort { get; set; }
    public int? Pid { get; set; } = null;
}

public record StatusResult(string Version, IEnumerable<ServiceDetailResult> Services, IEnumerable<string> IngressUrls);
public record ServiceDetailResult(string ServiceName, string Status, bool Bridged, int BridgedPort, IEnumerable<string> Urls, IDictionary<string, string> EnvironmentVariables);
public record BridgeStatusResult(string ServiceName, bool Enabled, string Status, int LocalPort);
public record BridgeStatusResultLogMessage(DateTime Date, string Message);