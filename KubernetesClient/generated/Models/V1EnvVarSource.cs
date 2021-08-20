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
    /// EnvVarSource represents a source for the value of an EnvVar.
    /// </summary>
    public partial class V1EnvVarSource
    {
        /// <summary>
        /// Initializes a new instance of the V1EnvVarSource class.
        /// </summary>
        public V1EnvVarSource()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1EnvVarSource class.
        /// </summary>
        /// <param name="configMapKeyRef">
        /// Selects a key of a ConfigMap.
        /// </param>
        /// <param name="fieldRef">
        /// Selects a field of the pod: supports metadata.name, metadata.namespace,
        /// `metadata.labels[&apos;&lt;KEY&gt;&apos;]`, `metadata.annotations[&apos;&lt;KEY&gt;&apos;]`, spec.nodeName,
        /// spec.serviceAccountName, status.hostIP, status.podIP, status.podIPs.
        /// </param>
        /// <param name="resourceFieldRef">
        /// Selects a resource of the container: only resources limits and requests
        /// (limits.cpu, limits.memory, limits.ephemeral-storage, requests.cpu,
        /// requests.memory and requests.ephemeral-storage) are currently supported.
        /// </param>
        /// <param name="secretKeyRef">
        /// Selects a key of a secret in the pod&apos;s namespace
        /// </param>
        public V1EnvVarSource(V1ConfigMapKeySelector configMapKeyRef = null, V1ObjectFieldSelector fieldRef = null, V1ResourceFieldSelector resourceFieldRef = null, V1SecretKeySelector secretKeyRef = null)
        {
            ConfigMapKeyRef = configMapKeyRef;
            FieldRef = fieldRef;
            ResourceFieldRef = resourceFieldRef;
            SecretKeyRef = secretKeyRef;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Selects a key of a ConfigMap.
        /// </summary>
        [JsonProperty(PropertyName = "configMapKeyRef")]
        public V1ConfigMapKeySelector ConfigMapKeyRef { get; set; }

        /// <summary>
        /// Selects a field of the pod: supports metadata.name, metadata.namespace,
        /// `metadata.labels[&apos;&lt;KEY&gt;&apos;]`, `metadata.annotations[&apos;&lt;KEY&gt;&apos;]`, spec.nodeName,
        /// spec.serviceAccountName, status.hostIP, status.podIP, status.podIPs.
        /// </summary>
        [JsonProperty(PropertyName = "fieldRef")]
        public V1ObjectFieldSelector FieldRef { get; set; }

        /// <summary>
        /// Selects a resource of the container: only resources limits and requests
        /// (limits.cpu, limits.memory, limits.ephemeral-storage, requests.cpu,
        /// requests.memory and requests.ephemeral-storage) are currently supported.
        /// </summary>
        [JsonProperty(PropertyName = "resourceFieldRef")]
        public V1ResourceFieldSelector ResourceFieldRef { get; set; }

        /// <summary>
        /// Selects a key of a secret in the pod&apos;s namespace
        /// </summary>
        [JsonProperty(PropertyName = "secretKeyRef")]
        public V1SecretKeySelector SecretKeyRef { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            ConfigMapKeyRef?.Validate();
            FieldRef?.Validate();
            ResourceFieldRef?.Validate();
            SecretKeyRef?.Validate();
        }
    }
}