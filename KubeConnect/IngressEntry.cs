using System;

namespace KubeConnect
{
    public class IngressEntry
    {
        public string HostName { get; init; } = "";
        public string Address { get; init; } = "";
        public string ServiceName { get; init; } = "";
        public int Port { get; init; }
        public string Path { get; internal set; } = "";

        public static bool operator ==(IngressEntry obj1, IngressEntry obj2)
        {
            if (ReferenceEquals(obj1, obj2))
                return true;
            if (ReferenceEquals(obj1, null))
                return false;
            if (ReferenceEquals(obj2, null))
                return false;
            return obj1.Equals(obj2);
        }

        public static bool operator !=(IngressEntry obj1, IngressEntry obj2) => !(obj1 == obj2);

        public bool Equals(IngressEntry? other)
        {
            if (ReferenceEquals(other, null))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return HostName == other.HostName
                && Address == other.Address
                && ServiceName == other.ServiceName
                && Port == other.Port
                && Path == other.Path;
        }

        public override bool Equals(object? obj) => Equals(obj as IngressDetails);

        public override int GetHashCode()
            => HashCode.Combine(HostName, Address, ServiceName, Port, Path);
    }
}
