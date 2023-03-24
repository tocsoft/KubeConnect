using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace KubeConnect
{
    public class IngressDetails
    {
        public bool Enabled => Ingresses?.Any() == true;

        public IPAddress AssignedAddress { get; init; } = IPAddress.None;

        public bool UseSsl { get; init; }

        public IReadOnlyList<IngressEntry> Ingresses { get; init; } = Array.Empty<IngressEntry>();

        public IEnumerable<string> HostNames => Ingresses.Select(X => X.HostName).Distinct();

        public IEnumerable<string> Addresses => Ingresses.Select(X => X.Address).Distinct();

        public static bool operator ==(IngressDetails obj1, IngressDetails obj2)
        {
            if (ReferenceEquals(obj1, obj2))
                return true;
            if (ReferenceEquals(obj1, null))
                return false;
            if (ReferenceEquals(obj2, null))
                return false;
            return obj1.Equals(obj2);
        }
        public static bool operator !=(IngressDetails obj1, IngressDetails obj2) => !(obj1 == obj2);

        public bool Equals(IngressDetails? other)
        {
            if (ReferenceEquals(other, null))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return Enabled == other.Enabled
                   && AssignedAddress.Equals(other.AssignedAddress)
                   && UseSsl == other.UseSsl
                   && Ingresses.Count == other.Ingresses.Count
                   && Ingresses.Intersect(other.Ingresses).Count() == other.Ingresses.Count();
        }

        public override bool Equals(object? obj) => Equals(obj as IngressDetails);

        public override int GetHashCode()
            => HashCode.Combine(Enabled, AssignedAddress, UseSsl, Ingresses);
    }
}
