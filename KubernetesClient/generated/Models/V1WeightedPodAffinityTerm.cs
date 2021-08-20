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
    /// The weights of all of the matched WeightedPodAffinityTerm fields are added
        /// per-node to find the most preferred node(s)
    /// </summary>
    public partial class V1WeightedPodAffinityTerm
    {
        /// <summary>
        /// Initializes a new instance of the V1WeightedPodAffinityTerm class.
        /// </summary>
        public V1WeightedPodAffinityTerm()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1WeightedPodAffinityTerm class.
        /// </summary>
        /// <param name="podAffinityTerm">
        /// Required. A pod affinity term, associated with the corresponding weight.
        /// </param>
        /// <param name="weight">
        /// weight associated with matching the corresponding podAffinityTerm, in the range
        /// 1-100.
        /// </param>
        public V1WeightedPodAffinityTerm(V1PodAffinityTerm podAffinityTerm, int weight)
        {
            PodAffinityTerm = podAffinityTerm;
            Weight = weight;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Required. A pod affinity term, associated with the corresponding weight.
        /// </summary>
        [JsonProperty(PropertyName = "podAffinityTerm")]
        public V1PodAffinityTerm PodAffinityTerm { get; set; }

        /// <summary>
        /// weight associated with matching the corresponding podAffinityTerm, in the range
        /// 1-100.
        /// </summary>
        [JsonProperty(PropertyName = "weight")]
        public int Weight { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            if (PodAffinityTerm == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, "PodAffinityTerm");    
            }
            PodAffinityTerm?.Validate();
        }
    }
}