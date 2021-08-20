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
    /// CertificateSigningRequestSpec contains the certificate request.
    /// </summary>
    public partial class V1CertificateSigningRequestSpec
    {
        /// <summary>
        /// Initializes a new instance of the V1CertificateSigningRequestSpec class.
        /// </summary>
        public V1CertificateSigningRequestSpec()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the V1CertificateSigningRequestSpec class.
        /// </summary>
        /// <param name="request">
        /// request contains an x509 certificate signing request encoded in a &quot;CERTIFICATE
        /// REQUEST&quot; PEM block. When serialized as JSON or YAML, the data is additionally
        /// base64-encoded.
        /// </param>
        /// <param name="signerName">
        /// signerName indicates the requested signer, and is a qualified name.
        /// 
        /// List/watch requests for CertificateSigningRequests can filter on this field
        /// using a &quot;spec.signerName=NAME&quot; fieldSelector.
        /// 
        /// Well-known Kubernetes signers are:
        /// 1. &quot;kubernetes.io/kube-apiserver-client&quot;: issues client certificates that can be
        /// used to authenticate to kube-apiserver.
        /// Requests for this signer are never auto-approved by kube-controller-manager, can
        /// be issued by the &quot;csrsigning&quot; controller in kube-controller-manager.
        /// 2. &quot;kubernetes.io/kube-apiserver-client-kubelet&quot;: issues client certificates
        /// that kubelets use to authenticate to kube-apiserver.
        /// Requests for this signer can be auto-approved by the &quot;csrapproving&quot; controller
        /// in kube-controller-manager, and can be issued by the &quot;csrsigning&quot; controller in
        /// kube-controller-manager.
        /// 3. &quot;kubernetes.io/kubelet-serving&quot; issues serving certificates that kubelets use
        /// to serve TLS endpoints, which kube-apiserver can connect to securely.
        /// Requests for this signer are never auto-approved by kube-controller-manager, and
        /// can be issued by the &quot;csrsigning&quot; controller in kube-controller-manager.
        /// 
        /// More details are available at
        /// https://k8s.io/docs/reference/access-authn-authz/certificate-signing-requests/#kubernetes-signers
        /// 
        /// Custom signerNames can also be specified. The signer defines:
        /// 1. Trust distribution: how trust (CA bundles) are distributed.
        /// 2. Permitted subjects: and behavior when a disallowed subject is requested.
        /// 3. Required, permitted, or forbidden x509 extensions in the request (including
        /// whether subjectAltNames are allowed, which types, restrictions on allowed
        /// values) and behavior when a disallowed extension is requested.
        /// 4. Required, permitted, or forbidden key usages / extended key usages.
        /// 5. Expiration/certificate lifetime: whether it is fixed by the signer,
        /// configurable by the admin.
        /// 6. Whether or not requests for CA certificates are allowed.
        /// </param>
        /// <param name="expirationSeconds">
        /// expirationSeconds is the requested duration of validity of the issued
        /// certificate. The certificate signer may issue a certificate with a different
        /// validity duration so a client must check the delta between the notBefore and and
        /// notAfter fields in the issued certificate to determine the actual duration.
        /// 
        /// The v1.22+ in-tree implementations of the well-known Kubernetes signers will
        /// honor this field as long as the requested duration is not greater than the
        /// maximum duration they will honor per the --cluster-signing-duration CLI flag to
        /// the Kubernetes controller manager.
        /// 
        /// Certificate signers may not honor this field for various reasons:
        /// 
        /// 1. Old signer that is unaware of the field (such as the in-tree
        /// implementations prior to v1.22)
        /// 2. Signer whose configured maximum is shorter than the requested duration
        /// 3. Signer whose configured minimum is longer than the requested duration
        /// 
        /// The minimum valid value for expirationSeconds is 600, i.e. 10 minutes.
        /// 
        /// As of v1.22, this field is beta and is controlled via the CSRDuration feature
        /// gate.
        /// </param>
        /// <param name="extra">
        /// extra contains extra attributes of the user that created the
        /// CertificateSigningRequest. Populated by the API server on creation and
        /// immutable.
        /// </param>
        /// <param name="groups">
        /// groups contains group membership of the user that created the
        /// CertificateSigningRequest. Populated by the API server on creation and
        /// immutable.
        /// </param>
        /// <param name="uid">
        /// uid contains the uid of the user that created the CertificateSigningRequest.
        /// Populated by the API server on creation and immutable.
        /// </param>
        /// <param name="usages">
        /// usages specifies a set of key usages requested in the issued certificate.
        /// 
        /// Requests for TLS client certificates typically request: &quot;digital signature&quot;,
        /// &quot;key encipherment&quot;, &quot;client auth&quot;.
        /// 
        /// Requests for TLS serving certificates typically request: &quot;key encipherment&quot;,
        /// &quot;digital signature&quot;, &quot;server auth&quot;.
        /// 
        /// Valid values are:
        /// &quot;signing&quot;, &quot;digital signature&quot;, &quot;content commitment&quot;,
        /// &quot;key encipherment&quot;, &quot;key agreement&quot;, &quot;data encipherment&quot;,
        /// &quot;cert sign&quot;, &quot;crl sign&quot;, &quot;encipher only&quot;, &quot;decipher only&quot;, &quot;any&quot;,
        /// &quot;server auth&quot;, &quot;client auth&quot;,
        /// &quot;code signing&quot;, &quot;email protection&quot;, &quot;s/mime&quot;,
        /// &quot;ipsec end system&quot;, &quot;ipsec tunnel&quot;, &quot;ipsec user&quot;,
        /// &quot;timestamping&quot;, &quot;ocsp signing&quot;, &quot;microsoft sgc&quot;, &quot;netscape sgc&quot;
        /// </param>
        /// <param name="username">
        /// username contains the name of the user that created the
        /// CertificateSigningRequest. Populated by the API server on creation and
        /// immutable.
        /// </param>
        public V1CertificateSigningRequestSpec(byte[] request, string signerName, int? expirationSeconds = null, IDictionary<string, IList<string>> extra = null, IList<string> groups = null, string uid = null, IList<string> usages = null, string username = null)
        {
            ExpirationSeconds = expirationSeconds;
            Extra = extra;
            Groups = groups;
            Request = request;
            SignerName = signerName;
            Uid = uid;
            Usages = usages;
            Username = username;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// expirationSeconds is the requested duration of validity of the issued
        /// certificate. The certificate signer may issue a certificate with a different
        /// validity duration so a client must check the delta between the notBefore and and
        /// notAfter fields in the issued certificate to determine the actual duration.
        /// 
        /// The v1.22+ in-tree implementations of the well-known Kubernetes signers will
        /// honor this field as long as the requested duration is not greater than the
        /// maximum duration they will honor per the --cluster-signing-duration CLI flag to
        /// the Kubernetes controller manager.
        /// 
        /// Certificate signers may not honor this field for various reasons:
        /// 
        /// 1. Old signer that is unaware of the field (such as the in-tree
        /// implementations prior to v1.22)
        /// 2. Signer whose configured maximum is shorter than the requested duration
        /// 3. Signer whose configured minimum is longer than the requested duration
        /// 
        /// The minimum valid value for expirationSeconds is 600, i.e. 10 minutes.
        /// 
        /// As of v1.22, this field is beta and is controlled via the CSRDuration feature
        /// gate.
        /// </summary>
        [JsonProperty(PropertyName = "expirationSeconds")]
        public int? ExpirationSeconds { get; set; }

        /// <summary>
        /// extra contains extra attributes of the user that created the
        /// CertificateSigningRequest. Populated by the API server on creation and
        /// immutable.
        /// </summary>
        [JsonProperty(PropertyName = "extra")]
        public IDictionary<string, IList<string>> Extra { get; set; }

        /// <summary>
        /// groups contains group membership of the user that created the
        /// CertificateSigningRequest. Populated by the API server on creation and
        /// immutable.
        /// </summary>
        [JsonProperty(PropertyName = "groups")]
        public IList<string> Groups { get; set; }

        /// <summary>
        /// request contains an x509 certificate signing request encoded in a &quot;CERTIFICATE
        /// REQUEST&quot; PEM block. When serialized as JSON or YAML, the data is additionally
        /// base64-encoded.
        /// </summary>
        [JsonProperty(PropertyName = "request")]
        public byte[] Request { get; set; }

        /// <summary>
        /// signerName indicates the requested signer, and is a qualified name.
        /// 
        /// List/watch requests for CertificateSigningRequests can filter on this field
        /// using a &quot;spec.signerName=NAME&quot; fieldSelector.
        /// 
        /// Well-known Kubernetes signers are:
        /// 1. &quot;kubernetes.io/kube-apiserver-client&quot;: issues client certificates that can be
        /// used to authenticate to kube-apiserver.
        /// Requests for this signer are never auto-approved by kube-controller-manager, can
        /// be issued by the &quot;csrsigning&quot; controller in kube-controller-manager.
        /// 2. &quot;kubernetes.io/kube-apiserver-client-kubelet&quot;: issues client certificates
        /// that kubelets use to authenticate to kube-apiserver.
        /// Requests for this signer can be auto-approved by the &quot;csrapproving&quot; controller
        /// in kube-controller-manager, and can be issued by the &quot;csrsigning&quot; controller in
        /// kube-controller-manager.
        /// 3. &quot;kubernetes.io/kubelet-serving&quot; issues serving certificates that kubelets use
        /// to serve TLS endpoints, which kube-apiserver can connect to securely.
        /// Requests for this signer are never auto-approved by kube-controller-manager, and
        /// can be issued by the &quot;csrsigning&quot; controller in kube-controller-manager.
        /// 
        /// More details are available at
        /// https://k8s.io/docs/reference/access-authn-authz/certificate-signing-requests/#kubernetes-signers
        /// 
        /// Custom signerNames can also be specified. The signer defines:
        /// 1. Trust distribution: how trust (CA bundles) are distributed.
        /// 2. Permitted subjects: and behavior when a disallowed subject is requested.
        /// 3. Required, permitted, or forbidden x509 extensions in the request (including
        /// whether subjectAltNames are allowed, which types, restrictions on allowed
        /// values) and behavior when a disallowed extension is requested.
        /// 4. Required, permitted, or forbidden key usages / extended key usages.
        /// 5. Expiration/certificate lifetime: whether it is fixed by the signer,
        /// configurable by the admin.
        /// 6. Whether or not requests for CA certificates are allowed.
        /// </summary>
        [JsonProperty(PropertyName = "signerName")]
        public string SignerName { get; set; }

        /// <summary>
        /// uid contains the uid of the user that created the CertificateSigningRequest.
        /// Populated by the API server on creation and immutable.
        /// </summary>
        [JsonProperty(PropertyName = "uid")]
        public string Uid { get; set; }

        /// <summary>
        /// usages specifies a set of key usages requested in the issued certificate.
        /// 
        /// Requests for TLS client certificates typically request: &quot;digital signature&quot;,
        /// &quot;key encipherment&quot;, &quot;client auth&quot;.
        /// 
        /// Requests for TLS serving certificates typically request: &quot;key encipherment&quot;,
        /// &quot;digital signature&quot;, &quot;server auth&quot;.
        /// 
        /// Valid values are:
        /// &quot;signing&quot;, &quot;digital signature&quot;, &quot;content commitment&quot;,
        /// &quot;key encipherment&quot;, &quot;key agreement&quot;, &quot;data encipherment&quot;,
        /// &quot;cert sign&quot;, &quot;crl sign&quot;, &quot;encipher only&quot;, &quot;decipher only&quot;, &quot;any&quot;,
        /// &quot;server auth&quot;, &quot;client auth&quot;,
        /// &quot;code signing&quot;, &quot;email protection&quot;, &quot;s/mime&quot;,
        /// &quot;ipsec end system&quot;, &quot;ipsec tunnel&quot;, &quot;ipsec user&quot;,
        /// &quot;timestamping&quot;, &quot;ocsp signing&quot;, &quot;microsoft sgc&quot;, &quot;netscape sgc&quot;
        /// </summary>
        [JsonProperty(PropertyName = "usages")]
        public IList<string> Usages { get; set; }

        /// <summary>
        /// username contains the name of the user that created the
        /// CertificateSigningRequest. Populated by the API server on creation and
        /// immutable.
        /// </summary>
        [JsonProperty(PropertyName = "username")]
        public string Username { get; set; }

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
