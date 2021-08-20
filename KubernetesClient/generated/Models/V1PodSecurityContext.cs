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
    /// PodSecurityContext holds pod-level security attributes and common container
        /// settings. Some fields are also present in container.securityContext.  Field
        /// values of container.securityContext take precedence over field values of
        /// PodSecurityContext.
    /// </summary>
    public partial class V1PodSecurityContext
    {
        /// <summary>
        /// Initializes a new instance of the V1PodSecurityContext class.
        /// </summary>
        public V1PodSecurityContext()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1PodSecurityContext class.
        /// </summary>
        /// <param name="fsGroup">
        /// A special supplemental group that applies to all containers in a pod. Some
        /// volume types allow the Kubelet to change the ownership of that volume to be
        /// owned by the pod:
        /// 
        /// 1. The owning GID will be the FSGroup 2. The setgid bit is set (new files
        /// created in the volume will be owned by FSGroup) 3. The permission bits are OR&apos;d
        /// with rw-rw----
        /// 
        /// If unset, the Kubelet will not modify the ownership and permissions of any
        /// volume.
        /// </param>
        /// <param name="fsGroupChangePolicy">
        /// fsGroupChangePolicy defines behavior of changing ownership and permission of the
        /// volume before being exposed inside Pod. This field will only apply to volume
        /// types which support fsGroup based ownership(and permissions). It will have no
        /// effect on ephemeral volume types such as: secret, configmaps and emptydir. Valid
        /// values are &quot;OnRootMismatch&quot; and &quot;Always&quot;. If not specified, &quot;Always&quot; is used.
        /// </param>
        /// <param name="runAsGroup">
        /// The GID to run the entrypoint of the container process. Uses runtime default if
        /// unset. May also be set in SecurityContext.  If set in both SecurityContext and
        /// PodSecurityContext, the value specified in SecurityContext takes precedence for
        /// that container.
        /// </param>
        /// <param name="runAsNonRoot">
        /// Indicates that the container must run as a non-root user. If true, the Kubelet
        /// will validate the image at runtime to ensure that it does not run as UID 0
        /// (root) and fail to start the container if it does. If unset or false, no such
        /// validation will be performed. May also be set in SecurityContext.  If set in
        /// both SecurityContext and PodSecurityContext, the value specified in
        /// SecurityContext takes precedence.
        /// </param>
        /// <param name="runAsUser">
        /// The UID to run the entrypoint of the container process. Defaults to user
        /// specified in image metadata if unspecified. May also be set in SecurityContext. 
        /// If set in both SecurityContext and PodSecurityContext, the value specified in
        /// SecurityContext takes precedence for that container.
        /// </param>
        /// <param name="seLinuxOptions">
        /// The SELinux context to be applied to all containers. If unspecified, the
        /// container runtime will allocate a random SELinux context for each container. 
        /// May also be set in SecurityContext.  If set in both SecurityContext and
        /// PodSecurityContext, the value specified in SecurityContext takes precedence for
        /// that container.
        /// </param>
        /// <param name="seccompProfile">
        /// The seccomp options to use by the containers in this pod.
        /// </param>
        /// <param name="supplementalGroups">
        /// A list of groups applied to the first process run in each container, in addition
        /// to the container&apos;s primary GID.  If unspecified, no groups will be added to any
        /// container.
        /// </param>
        /// <param name="sysctls">
        /// Sysctls hold a list of namespaced sysctls used for the pod. Pods with
        /// unsupported sysctls (by the container runtime) might fail to launch.
        /// </param>
        /// <param name="windowsOptions">
        /// The Windows specific settings applied to all containers. If unspecified, the
        /// options within a container&apos;s SecurityContext will be used. If set in both
        /// SecurityContext and PodSecurityContext, the value specified in SecurityContext
        /// takes precedence.
        /// </param>
        public V1PodSecurityContext(long? fsGroup = null, string fsGroupChangePolicy = null, long? runAsGroup = null, bool? runAsNonRoot = null, long? runAsUser = null, V1SELinuxOptions seLinuxOptions = null, V1SeccompProfile seccompProfile = null, IList<long?> supplementalGroups = null, IList<V1Sysctl> sysctls = null, V1WindowsSecurityContextOptions windowsOptions = null)
        {
            FsGroup = fsGroup;
            FsGroupChangePolicy = fsGroupChangePolicy;
            RunAsGroup = runAsGroup;
            RunAsNonRoot = runAsNonRoot;
            RunAsUser = runAsUser;
            SeLinuxOptions = seLinuxOptions;
            SeccompProfile = seccompProfile;
            SupplementalGroups = supplementalGroups;
            Sysctls = sysctls;
            WindowsOptions = windowsOptions;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// A special supplemental group that applies to all containers in a pod. Some
        /// volume types allow the Kubelet to change the ownership of that volume to be
        /// owned by the pod:
        /// 
        /// 1. The owning GID will be the FSGroup 2. The setgid bit is set (new files
        /// created in the volume will be owned by FSGroup) 3. The permission bits are OR&apos;d
        /// with rw-rw----
        /// 
        /// If unset, the Kubelet will not modify the ownership and permissions of any
        /// volume.
        /// </summary>
        [JsonProperty(PropertyName = "fsGroup")]
        public long? FsGroup { get; set; }

        /// <summary>
        /// fsGroupChangePolicy defines behavior of changing ownership and permission of the
        /// volume before being exposed inside Pod. This field will only apply to volume
        /// types which support fsGroup based ownership(and permissions). It will have no
        /// effect on ephemeral volume types such as: secret, configmaps and emptydir. Valid
        /// values are &quot;OnRootMismatch&quot; and &quot;Always&quot;. If not specified, &quot;Always&quot; is used.
        /// </summary>
        [JsonProperty(PropertyName = "fsGroupChangePolicy")]
        public string FsGroupChangePolicy { get; set; }

        /// <summary>
        /// The GID to run the entrypoint of the container process. Uses runtime default if
        /// unset. May also be set in SecurityContext.  If set in both SecurityContext and
        /// PodSecurityContext, the value specified in SecurityContext takes precedence for
        /// that container.
        /// </summary>
        [JsonProperty(PropertyName = "runAsGroup")]
        public long? RunAsGroup { get; set; }

        /// <summary>
        /// Indicates that the container must run as a non-root user. If true, the Kubelet
        /// will validate the image at runtime to ensure that it does not run as UID 0
        /// (root) and fail to start the container if it does. If unset or false, no such
        /// validation will be performed. May also be set in SecurityContext.  If set in
        /// both SecurityContext and PodSecurityContext, the value specified in
        /// SecurityContext takes precedence.
        /// </summary>
        [JsonProperty(PropertyName = "runAsNonRoot")]
        public bool? RunAsNonRoot { get; set; }

        /// <summary>
        /// The UID to run the entrypoint of the container process. Defaults to user
        /// specified in image metadata if unspecified. May also be set in SecurityContext. 
        /// If set in both SecurityContext and PodSecurityContext, the value specified in
        /// SecurityContext takes precedence for that container.
        /// </summary>
        [JsonProperty(PropertyName = "runAsUser")]
        public long? RunAsUser { get; set; }

        /// <summary>
        /// The SELinux context to be applied to all containers. If unspecified, the
        /// container runtime will allocate a random SELinux context for each container. 
        /// May also be set in SecurityContext.  If set in both SecurityContext and
        /// PodSecurityContext, the value specified in SecurityContext takes precedence for
        /// that container.
        /// </summary>
        [JsonProperty(PropertyName = "seLinuxOptions")]
        public V1SELinuxOptions SeLinuxOptions { get; set; }

        /// <summary>
        /// The seccomp options to use by the containers in this pod.
        /// </summary>
        [JsonProperty(PropertyName = "seccompProfile")]
        public V1SeccompProfile SeccompProfile { get; set; }

        /// <summary>
        /// A list of groups applied to the first process run in each container, in addition
        /// to the container&apos;s primary GID.  If unspecified, no groups will be added to any
        /// container.
        /// </summary>
        [JsonProperty(PropertyName = "supplementalGroups")]
        public IList<long?> SupplementalGroups { get; set; }

        /// <summary>
        /// Sysctls hold a list of namespaced sysctls used for the pod. Pods with
        /// unsupported sysctls (by the container runtime) might fail to launch.
        /// </summary>
        [JsonProperty(PropertyName = "sysctls")]
        public IList<V1Sysctl> Sysctls { get; set; }

        /// <summary>
        /// The Windows specific settings applied to all containers. If unspecified, the
        /// options within a container&apos;s SecurityContext will be used. If set in both
        /// SecurityContext and PodSecurityContext, the value specified in SecurityContext
        /// takes precedence.
        /// </summary>
        [JsonProperty(PropertyName = "windowsOptions")]
        public V1WindowsSecurityContextOptions WindowsOptions { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            SeLinuxOptions?.Validate();
            SeccompProfile?.Validate();
            foreach(var obj in Sysctls)
            {
                obj.Validate();
            }
            WindowsOptions?.Validate();
        }
    }
}