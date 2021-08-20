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
    /// ReplicaSet ensures that a specified number of pod replicas are running at any
        /// given time.
    /// </summary>
    public partial class V1ReplicaSet
    {
        /// <summary>
        /// Initializes a new instance of the V1ReplicaSet class.
        /// </summary>
        public V1ReplicaSet()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1ReplicaSet class.
        /// </summary>
        /// <param name="apiVersion">
        /// APIVersion defines the versioned schema of this representation of an object.
        /// Servers should convert recognized schemas to the latest internal value, and may
        /// reject unrecognized values. More info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#resources
        /// </param>
        /// <param name="kind">
        /// Kind is a string value representing the REST resource this object represents.
        /// Servers may infer this from the endpoint the client submits requests to. Cannot
        /// be updated. In CamelCase. More info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#types-kinds
        /// </param>
        /// <param name="metadata">
        /// If the Labels of a ReplicaSet are empty, they are defaulted to be the same as
        /// the Pod(s) that the ReplicaSet manages. Standard object&apos;s metadata. More info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#metadata
        /// </param>
        /// <param name="spec">
        /// Spec defines the specification of the desired behavior of the ReplicaSet. More
        /// info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#spec-and-status
        /// </param>
        /// <param name="status">
        /// Status is the most recently observed status of the ReplicaSet. This data may be
        /// out of date by some window of time. Populated by the system. Read-only. More
        /// info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#spec-and-status
        /// </param>
        public V1ReplicaSet(string apiVersion = null, string kind = null, V1ObjectMeta metadata = null, V1ReplicaSetSpec spec = null, V1ReplicaSetStatus status = null)
        {
            ApiVersion = apiVersion;
            Kind = kind;
            Metadata = metadata;
            Spec = spec;
            Status = status;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// APIVersion defines the versioned schema of this representation of an object.
        /// Servers should convert recognized schemas to the latest internal value, and may
        /// reject unrecognized values. More info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#resources
        /// </summary>
        [JsonProperty(PropertyName = "apiVersion")]
        public string ApiVersion { get; set; }

        /// <summary>
        /// Kind is a string value representing the REST resource this object represents.
        /// Servers may infer this from the endpoint the client submits requests to. Cannot
        /// be updated. In CamelCase. More info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#types-kinds
        /// </summary>
        [JsonProperty(PropertyName = "kind")]
        public string Kind { get; set; }

        /// <summary>
        /// If the Labels of a ReplicaSet are empty, they are defaulted to be the same as
        /// the Pod(s) that the ReplicaSet manages. Standard object&apos;s metadata. More info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#metadata
        /// </summary>
        [JsonProperty(PropertyName = "metadata")]
        public V1ObjectMeta Metadata { get; set; }

        /// <summary>
        /// Spec defines the specification of the desired behavior of the ReplicaSet. More
        /// info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#spec-and-status
        /// </summary>
        [JsonProperty(PropertyName = "spec")]
        public V1ReplicaSetSpec Spec { get; set; }

        /// <summary>
        /// Status is the most recently observed status of the ReplicaSet. This data may be
        /// out of date by some window of time. Populated by the system. Read-only. More
        /// info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#spec-and-status
        /// </summary>
        [JsonProperty(PropertyName = "status")]
        public V1ReplicaSetStatus Status { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            Metadata?.Validate();
            Spec?.Validate();
            Status?.Validate();
        }
    }
}