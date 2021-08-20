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
    /// A scoped-resource selector requirement is a selector that contains values, a
        /// scope name, and an operator that relates the scope name and values.
    /// </summary>
    public partial class V1ScopedResourceSelectorRequirement
    {
        /// <summary>
        /// Initializes a new instance of the V1ScopedResourceSelectorRequirement class.
        /// </summary>
        public V1ScopedResourceSelectorRequirement()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1ScopedResourceSelectorRequirement class.
        /// </summary>
        /// <param name="operatorProperty">
        /// Represents a scope&apos;s relationship to a set of values. Valid operators are In,
        /// NotIn, Exists, DoesNotExist.
        /// </param>
        /// <param name="scopeName">
        /// The name of the scope that the selector applies to.
        /// </param>
        /// <param name="values">
        /// An array of string values. If the operator is In or NotIn, the values array must
        /// be non-empty. If the operator is Exists or DoesNotExist, the values array must
        /// be empty. This array is replaced during a strategic merge patch.
        /// </param>
        public V1ScopedResourceSelectorRequirement(string operatorProperty, string scopeName, IList<string> values = null)
        {
            OperatorProperty = operatorProperty;
            ScopeName = scopeName;
            Values = values;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Represents a scope&apos;s relationship to a set of values. Valid operators are In,
        /// NotIn, Exists, DoesNotExist.
        /// </summary>
        [JsonProperty(PropertyName = "operator")]
        public string OperatorProperty { get; set; }

        /// <summary>
        /// The name of the scope that the selector applies to.
        /// </summary>
        [JsonProperty(PropertyName = "scopeName")]
        public string ScopeName { get; set; }

        /// <summary>
        /// An array of string values. If the operator is In or NotIn, the values array must
        /// be non-empty. If the operator is Exists or DoesNotExist, the values array must
        /// be empty. This array is replaced during a strategic merge patch.
        /// </summary>
        [JsonProperty(PropertyName = "values")]
        public IList<string> Values { get; set; }

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
