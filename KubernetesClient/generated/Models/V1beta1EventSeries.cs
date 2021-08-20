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
    /// EventSeries contain information on series of events, i.e. thing that was/is
        /// happening continuously for some time.
    /// </summary>
    public partial class V1beta1EventSeries
    {
        /// <summary>
        /// Initializes a new instance of the V1beta1EventSeries class.
        /// </summary>
        public V1beta1EventSeries()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1beta1EventSeries class.
        /// </summary>
        /// <param name="count">
        /// count is the number of occurrences in this series up to the last heartbeat time.
        /// </param>
        /// <param name="lastObservedTime">
        /// lastObservedTime is the time when last Event from the series was seen before
        /// last heartbeat.
        /// </param>
        public V1beta1EventSeries(int count, System.DateTime lastObservedTime)
        {
            Count = count;
            LastObservedTime = lastObservedTime;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// count is the number of occurrences in this series up to the last heartbeat time.
        /// </summary>
        [JsonProperty(PropertyName = "count")]
        public int Count { get; set; }

        /// <summary>
        /// lastObservedTime is the time when last Event from the series was seen before
        /// last heartbeat.
        /// </summary>
        [JsonProperty(PropertyName = "lastObservedTime")]
        public System.DateTime LastObservedTime { get; set; }

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
