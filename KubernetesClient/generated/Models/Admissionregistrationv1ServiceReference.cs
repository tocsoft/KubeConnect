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
    /// ServiceReference holds a reference to Service.legacy.k8s.io
    /// </summary>
    public partial class Admissionregistrationv1ServiceReference
    {
        /// <summary>
        /// Initializes a new instance of the Admissionregistrationv1ServiceReference class.
        /// </summary>
        public Admissionregistrationv1ServiceReference()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the Admissionregistrationv1ServiceReference class.
        /// </summary>
        /// <param name="name">
        /// `name` is the name of the service. Required
        /// </param>
        /// <param name="namespaceProperty">
        /// `namespace` is the namespace of the service. Required
        /// </param>
        /// <param name="path">
        /// `path` is an optional URL path which will be sent in any request to this
        /// service.
        /// </param>
        /// <param name="port">
        /// If specified, the port on the service that hosting webhook. Default to 443 for
        /// backward compatibility. `port` should be a valid port number (1-65535,
        /// inclusive).
        /// </param>
        public Admissionregistrationv1ServiceReference(string name, string namespaceProperty, string path = null, int? port = null)
        {
            Name = name;
            NamespaceProperty = namespaceProperty;
            Path = path;
            Port = port;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// `name` is the name of the service. Required
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// `namespace` is the namespace of the service. Required
        /// </summary>
        [JsonProperty(PropertyName = "namespace")]
        public string NamespaceProperty { get; set; }

        /// <summary>
        /// `path` is an optional URL path which will be sent in any request to this
        /// service.
        /// </summary>
        [JsonProperty(PropertyName = "path")]
        public string Path { get; set; }

        /// <summary>
        /// If specified, the port on the service that hosting webhook. Default to 443 for
        /// backward compatibility. `port` should be a valid port number (1-65535,
        /// inclusive).
        /// </summary>
        [JsonProperty(PropertyName = "port")]
        public int? Port { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
        }
    }
}