// <auto-generated>
// Code generated by https://github.com/kubernetes-client/csharp/tree/master/gen/KubernetesGenerator
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>

namespace k8s.Models
{
    using Microsoft.Rest;
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using System.Collections;
    using System.Linq;

    /// <summary>
    /// IngressRule represents the rules mapping the paths under a specified host to the
        /// related backend services. Incoming requests are first evaluated for a host
        /// match, then routed to the backend associated with the matching IngressRuleValue.
    /// </summary>
    public partial class V1IngressRule
    {
        /// <summary>
        /// Initializes a new instance of the V1IngressRule class.
        /// </summary>
        public V1IngressRule()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1IngressRule class.
        /// </summary>
        /// <param name="host">
        /// Host is the fully qualified domain name of a network host, as defined by RFC
        /// 3986. Note the following deviations from the &quot;host&quot; part of the URI as defined
        /// in RFC 3986: 1. IPs are not allowed. Currently an IngressRuleValue can only
        /// apply to
        /// the IP in the Spec of the parent Ingress.
        /// 2. The `:` delimiter is not respected because ports are not allowed.
        /// Currently the port of an Ingress is implicitly :80 for http and
        /// :443 for https.
        /// Both these may change in the future. Incoming requests are matched against the
        /// host before the IngressRuleValue. If the host is unspecified, the Ingress routes
        /// all traffic based on the specified IngressRuleValue.
        /// 
        /// Host can be &quot;precise&quot; which is a domain name without the terminating dot of a
        /// network host (e.g. &quot;foo.bar.com&quot;) or &quot;wildcard&quot;, which is a domain name prefixed
        /// with a single wildcard label (e.g. &quot;*.foo.com&quot;). The wildcard character &apos;*&apos; must
        /// appear by itself as the first DNS label and matches only a single label. You
        /// cannot have a wildcard label by itself (e.g. Host == &quot;*&quot;). Requests will be
        /// matched against the Host field in the following way: 1. If Host is precise, the
        /// request matches this rule if the http host header is equal to Host. 2. If Host
        /// is a wildcard, then the request matches this rule if the http host header is to
        /// equal to the suffix (removing the first label) of the wildcard rule.
        /// </param>
        /// <param name="http">
        /// 
        /// </param>
        public V1IngressRule(string host = null, V1HTTPIngressRuleValue http = null)
        {
            Host = host;
            Http = http;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Host is the fully qualified domain name of a network host, as defined by RFC
        /// 3986. Note the following deviations from the &quot;host&quot; part of the URI as defined
        /// in RFC 3986: 1. IPs are not allowed. Currently an IngressRuleValue can only
        /// apply to
        /// the IP in the Spec of the parent Ingress.
        /// 2. The `:` delimiter is not respected because ports are not allowed.
        /// Currently the port of an Ingress is implicitly :80 for http and
        /// :443 for https.
        /// Both these may change in the future. Incoming requests are matched against the
        /// host before the IngressRuleValue. If the host is unspecified, the Ingress routes
        /// all traffic based on the specified IngressRuleValue.
        /// 
        /// Host can be &quot;precise&quot; which is a domain name without the terminating dot of a
        /// network host (e.g. &quot;foo.bar.com&quot;) or &quot;wildcard&quot;, which is a domain name prefixed
        /// with a single wildcard label (e.g. &quot;*.foo.com&quot;). The wildcard character &apos;*&apos; must
        /// appear by itself as the first DNS label and matches only a single label. You
        /// cannot have a wildcard label by itself (e.g. Host == &quot;*&quot;). Requests will be
        /// matched against the Host field in the following way: 1. If Host is precise, the
        /// request matches this rule if the http host header is equal to Host. 2. If Host
        /// is a wildcard, then the request matches this rule if the http host header is to
        /// equal to the suffix (removing the first label) of the wildcard rule.
        /// </summary>
        [JsonProperty(PropertyName = "host")]
        public string Host { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonProperty(PropertyName = "http")]
        public V1HTTPIngressRuleValue Http { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            Http?.Validate();
        }
    }
}