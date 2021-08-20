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
    /// Sysctl defines a kernel parameter to be set
    /// </summary>
    public partial class V1Sysctl
    {
        /// <summary>
        /// Initializes a new instance of the V1Sysctl class.
        /// </summary>
        public V1Sysctl()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1Sysctl class.
        /// </summary>
        /// <param name="name">
        /// Name of a property to set
        /// </param>
        /// <param name="value">
        /// Value of a property to set
        /// </param>
        public V1Sysctl(string name, string value)
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
        /// Name of a property to set
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Value of a property to set
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