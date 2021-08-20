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
    /// NamespaceStatus is information about the current status of a Namespace.
    /// </summary>
    public partial class V1NamespaceStatus
    {
        /// <summary>
        /// Initializes a new instance of the V1NamespaceStatus class.
        /// </summary>
        public V1NamespaceStatus()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1NamespaceStatus class.
        /// </summary>
        /// <param name="conditions">
        /// Represents the latest available observations of a namespace&apos;s current state.
        /// </param>
        /// <param name="phase">
        /// Phase is the current lifecycle phase of the namespace. More info:
        /// https://kubernetes.io/docs/tasks/administer-cluster/namespaces/
        /// </param>
        public V1NamespaceStatus(IList<V1NamespaceCondition> conditions = null, string phase = null)
        {
            Conditions = conditions;
            Phase = phase;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Represents the latest available observations of a namespace&apos;s current state.
        /// </summary>
        [JsonProperty(PropertyName = "conditions")]
        public IList<V1NamespaceCondition> Conditions { get; set; }

        /// <summary>
        /// Phase is the current lifecycle phase of the namespace. More info:
        /// https://kubernetes.io/docs/tasks/administer-cluster/namespaces/
        /// </summary>
        [JsonProperty(PropertyName = "phase")]
        public string Phase { get; set; }

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
        }
    }
}
