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
    /// Represents a source location of a volume to mount, managed by an external CSI
        /// driver
    /// </summary>
    public partial class V1CSIVolumeSource
    {
        /// <summary>
        /// Initializes a new instance of the V1CSIVolumeSource class.
        /// </summary>
        public V1CSIVolumeSource()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1CSIVolumeSource class.
        /// </summary>
        /// <param name="driver">
        /// Driver is the name of the CSI driver that handles this volume. Consult with your
        /// admin for the correct name as registered in the cluster.
        /// </param>
        /// <param name="fsType">
        /// Filesystem type to mount. Ex. &quot;ext4&quot;, &quot;xfs&quot;, &quot;ntfs&quot;. If not provided, the empty
        /// value is passed to the associated CSI driver which will determine the default
        /// filesystem to apply.
        /// </param>
        /// <param name="nodePublishSecretRef">
        /// NodePublishSecretRef is a reference to the secret object containing sensitive
        /// information to pass to the CSI driver to complete the CSI NodePublishVolume and
        /// NodeUnpublishVolume calls. This field is optional, and  may be empty if no
        /// secret is required. If the secret object contains more than one secret, all
        /// secret references are passed.
        /// </param>
        /// <param name="readOnlyProperty">
        /// Specifies a read-only configuration for the volume. Defaults to false
        /// (read/write).
        /// </param>
        /// <param name="volumeAttributes">
        /// VolumeAttributes stores driver-specific properties that are passed to the CSI
        /// driver. Consult your driver&apos;s documentation for supported values.
        /// </param>
        public V1CSIVolumeSource(string driver, string fsType = null, V1LocalObjectReference nodePublishSecretRef = null, bool? readOnlyProperty = null, IDictionary<string, string> volumeAttributes = null)
        {
            Driver = driver;
            FsType = fsType;
            NodePublishSecretRef = nodePublishSecretRef;
            ReadOnlyProperty = readOnlyProperty;
            VolumeAttributes = volumeAttributes;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Driver is the name of the CSI driver that handles this volume. Consult with your
        /// admin for the correct name as registered in the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "driver")]
        public string Driver { get; set; }

        /// <summary>
        /// Filesystem type to mount. Ex. &quot;ext4&quot;, &quot;xfs&quot;, &quot;ntfs&quot;. If not provided, the empty
        /// value is passed to the associated CSI driver which will determine the default
        /// filesystem to apply.
        /// </summary>
        [JsonProperty(PropertyName = "fsType")]
        public string FsType { get; set; }

        /// <summary>
        /// NodePublishSecretRef is a reference to the secret object containing sensitive
        /// information to pass to the CSI driver to complete the CSI NodePublishVolume and
        /// NodeUnpublishVolume calls. This field is optional, and  may be empty if no
        /// secret is required. If the secret object contains more than one secret, all
        /// secret references are passed.
        /// </summary>
        [JsonProperty(PropertyName = "nodePublishSecretRef")]
        public V1LocalObjectReference NodePublishSecretRef { get; set; }

        /// <summary>
        /// Specifies a read-only configuration for the volume. Defaults to false
        /// (read/write).
        /// </summary>
        [JsonProperty(PropertyName = "readOnly")]
        public bool? ReadOnlyProperty { get; set; }

        /// <summary>
        /// VolumeAttributes stores driver-specific properties that are passed to the CSI
        /// driver. Consult your driver&apos;s documentation for supported values.
        /// </summary>
        [JsonProperty(PropertyName = "volumeAttributes")]
        public IDictionary<string, string> VolumeAttributes { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            NodePublishSecretRef?.Validate();
        }
    }
}