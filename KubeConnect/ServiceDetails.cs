using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace KubeConnect
{
    public class BridgeDetails
    {
        public string ServiceName { get; init; } = string.Empty;

        public string Namespace { get; init; } = string.Empty;

        public string ConnectionId { get; init; } = string.Empty;

        public IReadOnlyList<(int remotePort, int localPort)> BridgedPorts { get; init; } = Array.Empty<(int, int)>();

        public IClientProxy Client { get; internal set; }

        object locker = new object();
        StringBuilder builder = new StringBuilder();
        StringBuilder builderAlt = new StringBuilder();
        internal void Log(string msg)
        {
            lock (locker)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }
                builder.Append(msg);
            }

        }
        SemaphoreSlim semaphore = new SemaphoreSlim(1);
        public async Task FlushLogs()
        {
            StringBuilder RotateLogBuffers()
            {
                lock (locker)
                {
                    var current = builder;
                    builder = builderAlt;
                    builderAlt = current;
                    return current;
                }
            }

            await semaphore.WaitAsync();
            try
            {
                var currentBuffers = RotateLogBuffers();
                if (currentBuffers.Length > 0)
                {
                    await Client.SendAsync("log", currentBuffers.ToString(), false);
                    currentBuffers.Clear();
                }
            }
            finally
            {
                semaphore.Release();
            }
        }
    }

    public class ServiceDetails
    {
        public string ServiceName { get; init; } = string.Empty;

        public string Namespace { get; init; } = string.Empty;

        public IReadOnlyDictionary<string, string> Selector { get; init; } = new Dictionary<string, string>();

        public string StringSelector => string.Join(",", Selector.Select((s) => $"{s.Key}={s.Value}"));

        public IPAddress AssignedAddress { get; init; } = IPAddress.Any;

        public IReadOnlyList<(int listenPort, int destinationPort)> TcpPorts { get; init; } = Array.Empty<(int, int)>();

        public bool UpdateHostsFile { get; init; }

        public IEnumerable<KeyValuePair<string, string>> EnvVars { get; internal set; }

        public static bool operator ==(ServiceDetails? obj1, ServiceDetails? obj2)
        {
            if (ReferenceEquals(obj1, obj2))
                return true;
            if (ReferenceEquals(obj1, null))
                return false;
            if (ReferenceEquals(obj2, null))
                return false;
            return obj1.Equals(obj2);
        }
        public static bool operator !=(ServiceDetails? obj1, ServiceDetails? obj2) => !(obj1 == obj2);

        public bool Equals(ServiceDetails? other)
        {
            if (ReferenceEquals(other, null))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return ServiceName.Equals(other.ServiceName, StringComparison.OrdinalIgnoreCase)
                   && Namespace.Equals(other.Namespace, StringComparison.OrdinalIgnoreCase)
                   && StringSelector == other.StringSelector
                   && AssignedAddress.Equals(other.AssignedAddress)
                   && TcpPorts.Count == other.TcpPorts.Count
                   && TcpPorts.Intersect(other.TcpPorts).Count() == TcpPorts.Count;
        }

        public override bool Equals(object? obj) => Equals(obj as IngressDetails);

        public override int GetHashCode()
            => HashCode.Combine(ServiceName.ToLowerInvariant(), Namespace?.ToLowerInvariant(), StringSelector, AssignedAddress, TcpPorts, UpdateHostsFile);
    }
}
