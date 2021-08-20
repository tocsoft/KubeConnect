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
    /// IngressClassSpec provides information about the class of an Ingress.
    /// </summary>
    public partial class V1IngressClassSpec
    {
        /// <summary>
        /// Initializes a new instance of the V1IngressClassSpec class.
        /// </summary>
        public V1IngressClassSpec()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1IngressClassSpec class.
        /// </summary>
        /// <param name="controller">
        /// Controller refers to the name of the controller that should handle this class.
        /// This allows for different &quot;flavors&quot; that are controlled by the same controller.
        /// For example, you may have different Parameters for the same implementing
        /// controller. This should be specified as a domain-prefixed path no more than 250
        /// characters in length, e.g. &quot;acme.io/ingress-controller&quot;. This field is
        /// immutable.
        /// </param>
        /// <param name="parameters">
        /// Parameters is a link to a custom resource containing additional configuration
        /// for the controller. This is optional if the controller does not require extra
        /// parameters.
        /// </param>
        public V1IngressClassSpec(string controller = null, V1IngressClassParametersReference parameters = null)
        {
            Controller = controller;
            Parameters = parameters;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Controller refers to the name of the controller that should handle this class.
        /// This allows for different &quot;flavors&quot; that are controlled by the same controller.
        /// For example, you may have different Parameters for the same implementing
        /// controller. This should be specified as a domain-prefixed path no more than 250
        /// characters in length, e.g. &quot;acme.io/ingress-controller&quot;. This field is
        /// immutable.
        /// </summary>
        [JsonProperty(PropertyName = "controller")]
        public string Controller { get; set; }

        /// <summary>
        /// Parameters is a link to a custom resource containing additional configuration
        /// for the controller. This is optional if the controller does not require extra
        /// parameters.
        /// </summary>
        [JsonProperty(PropertyName = "parameters")]
        public V1IngressClassParametersReference Parameters { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            Parameters?.Validate();
        }
    }
}
