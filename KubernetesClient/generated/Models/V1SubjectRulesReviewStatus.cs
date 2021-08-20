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
    /// SubjectRulesReviewStatus contains the result of a rules check. This check can be
        /// incomplete depending on the set of authorizers the server is configured with and
        /// any errors experienced during evaluation. Because authorization rules are
        /// additive, if a rule appears in a list it&apos;s safe to assume the subject has that
        /// permission, even if that list is incomplete.
    /// </summary>
    public partial class V1SubjectRulesReviewStatus
    {
        /// <summary>
        /// Initializes a new instance of the V1SubjectRulesReviewStatus class.
        /// </summary>
        public V1SubjectRulesReviewStatus()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1SubjectRulesReviewStatus class.
        /// </summary>
        /// <param name="incomplete">
        /// Incomplete is true when the rules returned by this call are incomplete. This is
        /// most commonly encountered when an authorizer, such as an external authorizer,
        /// doesn&apos;t support rules evaluation.
        /// </param>
        /// <param name="nonResourceRules">
        /// NonResourceRules is the list of actions the subject is allowed to perform on
        /// non-resources. The list ordering isn&apos;t significant, may contain duplicates, and
        /// possibly be incomplete.
        /// </param>
        /// <param name="resourceRules">
        /// ResourceRules is the list of actions the subject is allowed to perform on
        /// resources. The list ordering isn&apos;t significant, may contain duplicates, and
        /// possibly be incomplete.
        /// </param>
        /// <param name="evaluationError">
        /// EvaluationError can appear in combination with Rules. It indicates an error
        /// occurred during rule evaluation, such as an authorizer that doesn&apos;t support rule
        /// evaluation, and that ResourceRules and/or NonResourceRules may be incomplete.
        /// </param>
        public V1SubjectRulesReviewStatus(bool incomplete, IList<V1NonResourceRule> nonResourceRules, IList<V1ResourceRule> resourceRules, string evaluationError = null)
        {
            EvaluationError = evaluationError;
            Incomplete = incomplete;
            NonResourceRules = nonResourceRules;
            ResourceRules = resourceRules;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// EvaluationError can appear in combination with Rules. It indicates an error
        /// occurred during rule evaluation, such as an authorizer that doesn&apos;t support rule
        /// evaluation, and that ResourceRules and/or NonResourceRules may be incomplete.
        /// </summary>
        [JsonProperty(PropertyName = "evaluationError")]
        public string EvaluationError { get; set; }

        /// <summary>
        /// Incomplete is true when the rules returned by this call are incomplete. This is
        /// most commonly encountered when an authorizer, such as an external authorizer,
        /// doesn&apos;t support rules evaluation.
        /// </summary>
        [JsonProperty(PropertyName = "incomplete")]
        public bool Incomplete { get; set; }

        /// <summary>
        /// NonResourceRules is the list of actions the subject is allowed to perform on
        /// non-resources. The list ordering isn&apos;t significant, may contain duplicates, and
        /// possibly be incomplete.
        /// </summary>
        [JsonProperty(PropertyName = "nonResourceRules")]
        public IList<V1NonResourceRule> NonResourceRules { get; set; }

        /// <summary>
        /// ResourceRules is the list of actions the subject is allowed to perform on
        /// resources. The list ordering isn&apos;t significant, may contain duplicates, and
        /// possibly be incomplete.
        /// </summary>
        [JsonProperty(PropertyName = "resourceRules")]
        public IList<V1ResourceRule> ResourceRules { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            foreach(var obj in NonResourceRules)
            {
                obj.Validate();
            }
            foreach(var obj in ResourceRules)
            {
                obj.Validate();
            }
        }
    }
}