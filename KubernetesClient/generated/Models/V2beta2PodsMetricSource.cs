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
    /// PodsMetricSource indicates how to scale on a metric describing each pod in the
        /// current scale target (for example, transactions-processed-per-second). The
        /// values will be averaged together before being compared to the target value.
    /// </summary>
    public partial class V2beta2PodsMetricSource
    {
        /// <summary>
        /// Initializes a new instance of the V2beta2PodsMetricSource class.
        /// </summary>
        public V2beta2PodsMetricSource()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V2beta2PodsMetricSource class.
        /// </summary>
        /// <param name="metric">
        /// metric identifies the target metric by name and selector
        /// </param>
        /// <param name="target">
        /// target specifies the target value for the given metric
        /// </param>
        public V2beta2PodsMetricSource(V2beta2MetricIdentifier metric, V2beta2MetricTarget target)
        {
            Metric = metric;
            Target = target;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// metric identifies the target metric by name and selector
        /// </summary>
        [JsonProperty(PropertyName = "metric")]
        public V2beta2MetricIdentifier Metric { get; set; }

        /// <summary>
        /// target specifies the target value for the given metric
        /// </summary>
        [JsonProperty(PropertyName = "target")]
        public V2beta2MetricTarget Target { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            if (Metric == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, "Metric");    
            }
            if (Target == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, "Target");    
            }
            Metric?.Validate();
            Target?.Validate();
        }
    }
}
