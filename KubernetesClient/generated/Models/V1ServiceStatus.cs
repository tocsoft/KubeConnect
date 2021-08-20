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
    /// ServiceStatus represents the current status of a service.
    /// </summary>
    public partial class V1ServiceStatus
    {
        /// <summary>
        /// Initializes a new instance of the V1ServiceStatus class.
        /// </summary>
        public V1ServiceStatus()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1ServiceStatus class.
        /// </summary>
        /// <param name="conditions">
        /// Current service state
        /// </param>
        /// <param name="loadBalancer">
        /// LoadBalancer contains the current status of the load-balancer, if one is
        /// present.
        /// </param>
        public V1ServiceStatus(IList<V1Condition> conditions = null, V1LoadBalancerStatus loadBalancer = null)
        {
            Conditions = conditions;
            LoadBalancer = loadBalancer;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Current service state
        /// </summary>
        [JsonProperty(PropertyName = "conditions")]
        public IList<V1Condition> Conditions { get; set; }

        /// <summary>
        /// LoadBalancer contains the current status of the load-balancer, if one is
        /// present.
        /// </summary>
        [JsonProperty(PropertyName = "loadBalancer")]
        public V1LoadBalancerStatus LoadBalancer { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            foreach(var obj in Conditions)
            {
                obj.Validate();
            }
            LoadBalancer?.Validate();
        }
    }
}
