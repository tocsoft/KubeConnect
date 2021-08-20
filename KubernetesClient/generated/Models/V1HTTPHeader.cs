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
    /// HTTPHeader describes a custom header to be used in HTTP probes
    /// </summary>
    public partial class V1HTTPHeader
    {
        /// <summary>
        /// Initializes a new instance of the V1HTTPHeader class.
        /// </summary>
        public V1HTTPHeader()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1HTTPHeader class.
        /// </summary>
        /// <param name="name">
        /// The header field name
        /// </param>
        /// <param name="value">
        /// The header field value
        /// </param>
        public V1HTTPHeader(string name, string value)
        {
            Name = name;
            Value = value;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// The header field name
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// The header field value
        /// </summary>
        [JsonProperty(PropertyName = "value")]
        public string Value { get; set; }

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