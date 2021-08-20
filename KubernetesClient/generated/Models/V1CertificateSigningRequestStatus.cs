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
    /// CertificateSigningRequestStatus contains conditions used to indicate
        /// approved/denied/failed status of the request, and the issued certificate.
    /// </summary>
    public partial class V1CertificateSigningRequestStatus
    {
        /// <summary>
        /// Initializes a new instance of the V1CertificateSigningRequestStatus class.
        /// </summary>
        public V1CertificateSigningRequestStatus()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1CertificateSigningRequestStatus class.
        /// </summary>
        /// <param name="certificate">
        /// certificate is populated with an issued certificate by the signer after an
        /// Approved condition is present. This field is set via the /status subresource.
        /// Once populated, this field is immutable.
        /// 
        /// If the certificate signing request is denied, a condition of type &quot;Denied&quot; is
        /// added and this field remains empty. If the signer cannot issue the certificate,
        /// a condition of type &quot;Failed&quot; is added and this field remains empty.
        /// 
        /// Validation requirements:
        /// 1. certificate must contain one or more PEM blocks.
        /// 2. All PEM blocks must have the &quot;CERTIFICATE&quot; label, contain no headers, and the
        /// encoded data
        /// must be a BER-encoded ASN.1 Certificate structure as described in section 4 of
        /// RFC5280.
        /// 3. Non-PEM content may appear before or after the &quot;CERTIFICATE&quot; PEM blocks and
        /// is unvalidated,
        /// to allow for explanatory text as described in section 5.2 of RFC7468.
        /// 
        /// If more than one PEM block is present, and the definition of the requested
        /// spec.signerName does not indicate otherwise, the first block is the issued
        /// certificate, and subsequent blocks should be treated as intermediate
        /// certificates and presented in TLS handshakes.
        /// 
        /// The certificate is encoded in PEM format.
        /// 
        /// When serialized as JSON or YAML, the data is additionally base64-encoded, so it
        /// consists of:
        /// 
        /// base64(
        /// -----BEGIN CERTIFICATE-----
        /// ...
        /// -----END CERTIFICATE-----
        /// )
        /// </param>
        /// <param name="conditions">
        /// conditions applied to the request. Known conditions are &quot;Approved&quot;, &quot;Denied&quot;,
        /// and &quot;Failed&quot;.
        /// </param>
        public V1CertificateSigningRequestStatus(byte[] certificate = null, IList<V1CertificateSigningRequestCondition> conditions = null)
        {
            Certificate = certificate;
            Conditions = conditions;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// certificate is populated with an issued certificate by the signer after an
        /// Approved condition is present. This field is set via the /status subresource.
        /// Once populated, this field is immutable.
        /// 
        /// If the certificate signing request is denied, a condition of type &quot;Denied&quot; is
        /// added and this field remains empty. If the signer cannot issue the certificate,
        /// a condition of type &quot;Failed&quot; is added and this field remains empty.
        /// 
        /// Validation requirements:
        /// 1. certificate must contain one or more PEM blocks.
        /// 2. All PEM blocks must have the &quot;CERTIFICATE&quot; label, contain no headers, and the
        /// encoded data
        /// must be a BER-encoded ASN.1 Certificate structure as described in section 4 of
        /// RFC5280.
        /// 3. Non-PEM content may appear before or after the &quot;CERTIFICATE&quot; PEM blocks and
        /// is unvalidated,
        /// to allow for explanatory text as described in section 5.2 of RFC7468.
        /// 
        /// If more than one PEM block is present, and the definition of the requested
        /// spec.signerName does not indicate otherwise, the first block is the issued
        /// certificate, and subsequent blocks should be treated as intermediate
        /// certificates and presented in TLS handshakes.
        /// 
        /// The certificate is encoded in PEM format.
        /// 
        /// When serialized as JSON or YAML, the data is additionally base64-encoded, so it
        /// consists of:
        /// 
        /// base64(
        /// -----BEGIN CERTIFICATE-----
        /// ...
        /// -----END CERTIFICATE-----
        /// )
        /// </summary>
        [JsonProperty(PropertyName = "certificate")]
        public byte[] Certificate { get; set; }

        /// <summary>
        /// conditions applied to the request. Known conditions are &quot;Approved&quot;, &quot;Denied&quot;,
        /// and &quot;Failed&quot;.
        /// </summary>
        [JsonProperty(PropertyName = "conditions")]
        public IList<V1CertificateSigningRequestCondition> Conditions { get; set; }

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