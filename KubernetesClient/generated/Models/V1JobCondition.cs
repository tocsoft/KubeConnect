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
    /// JobCondition describes current state of a job.
    /// </summary>
    public partial class V1JobCondition
    {
        /// <summary>
        /// Initializes a new instance of the V1JobCondition class.
        /// </summary>
        public V1JobCondition()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1JobCondition class.
        /// </summary>
        /// <param name="status">
        /// Status of the condition, one of True, False, Unknown.
        /// </param>
        /// <param name="type">
        /// Type of job condition, Complete or Failed.
        /// </param>
        /// <param name="lastProbeTime">
        /// Last time the condition was checked.
        /// </param>
        /// <param name="lastTransitionTime">
        /// Last time the condition transit from one status to another.
        /// </param>
        /// <param name="message">
        /// Human readable message indicating details about last transition.
        /// </param>
        /// <param name="reason">
        /// (brief) reason for the condition&apos;s last transition.
        /// </param>
        public V1JobCondition(string status, string type, System.DateTime? lastProbeTime = null, System.DateTime? lastTransitionTime = null, string message = null, string reason = null)
        {
            LastProbeTime = lastProbeTime;
            LastTransitionTime = lastTransitionTime;
            Message = message;
            Reason = reason;
            Status = status;
            Type = type;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Last time the condition was checked.
        /// </summary>
        [JsonProperty(PropertyName = "lastProbeTime")]
        public System.DateTime? LastProbeTime { get; set; }

        /// <summary>
        /// Last time the condition transit from one status to another.
        /// </summary>
        [JsonProperty(PropertyName = "lastTransitionTime")]
        public System.DateTime? LastTransitionTime { get; set; }

        /// <summary>
        /// Human readable message indicating details about last transition.
        /// </summary>
        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }

        /// <summary>
        /// (brief) reason for the condition&apos;s last transition.
        /// </summary>
        [JsonProperty(PropertyName = "reason")]
        public string Reason { get; set; }

        /// <summary>
        /// Status of the condition, one of True, False, Unknown.
        /// </summary>
        [JsonProperty(PropertyName = "status")]
        public string Status { get; set; }

        /// <summary>
        /// Type of job condition, Complete or Failed.
        /// </summary>
        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }

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