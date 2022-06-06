using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace KubeConnect.PortForwarding
{
    public static class CertificateHelper
    {
        private const string autorityName = "KubeConnect Issuing Authority";

        private static X509Certificate2 GetSigningAuthority(DateTimeOffset expiryDate, bool recreate = false)
        {
            static X509Certificate2 FindOrCreate(DateTimeOffset expiryDate, bool recreate = false)
            {
                X509Certificate2? certificate = null;
                // if windows load from cert store and return
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser, OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                    var certificateSet = store.Certificates.Find(X509FindType.FindByIssuerName, autorityName, true);
                    var storeCert = certificateSet.Cast<X509Certificate2>().FirstOrDefault();
                    if (storeCert?.HasPrivateKey == true)
                    {
                        certificate = storeCert;
                    }
                }

                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var rootCertificatePath = Path.Combine(appData, "KubeConnect", "trusted.cert");

                if (File.Exists(rootCertificatePath))
                {
                    try
                    {
                        certificate = (X509Certificate2)X509Certificate2.CreateFromCertFile(rootCertificatePath);
                    }
                    catch
                    {
                        File.Delete(rootCertificatePath);
                    }
                }

                if (certificate == null || recreate)
                {
                    RSA parent = RSA.Create(4096);

                    CertificateRequest parentReq = new CertificateRequest(
                      $"CN={autorityName}",
                      parent,
                      HashAlgorithmName.SHA256,
                      RSASignaturePadding.Pkcs1);

                    parentReq.CertificateExtensions.Add(
                        new X509BasicConstraintsExtension(true, false, 0, true));

                    parentReq.CertificateExtensions.Add(
                        new X509SubjectKeyIdentifierExtension(parentReq.PublicKey, false));

                    var rootExpiryDate = DateTimeOffset.UtcNow.AddDays(365);
                    if (rootExpiryDate < expiryDate)
                    {
                        rootExpiryDate = expiryDate.AddDays(90);
                    }

                    var tempCert = parentReq.CreateSelfSigned(
                      DateTimeOffset.UtcNow.AddDays(-45),
                      rootExpiryDate);

                    var newCertificate = new X509Certificate2(tempCert.Export(X509ContentType.Pkcs12), string.Empty, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
                    if (!newCertificate.HasPrivateKey)
                    {
                        newCertificate = newCertificate.CopyWithPrivateKey(parent);
                    }

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser, OpenFlags.ReadWrite | OpenFlags.OpenExistingOnly | OpenFlags.MaxAllowed);

                        // we must be forcing a recreate, delete the old cert and add the new one
                        if (certificate != null)
                        {
                            store.Remove(certificate);
                        }

                        store.Add(newCertificate);
                        store.Close();
                    }
                    else
                    {
                        var dir = Path.GetDirectoryName(rootCertificatePath);
                        if (dir != null)
                        {
                            Directory.CreateDirectory(dir);
                        }

                        File.WriteAllBytes(rootCertificatePath, newCertificate.Export(X509ContentType.Pfx));
                    }


                    certificate = newCertificate;
                }

                return certificate;
            }

            var certificate = FindOrCreate(expiryDate, false);
            if (certificate.NotAfter <= expiryDate.DateTime)
            {
                certificate = FindOrCreate(expiryDate, true);
            }

            if (certificate.NotAfter <= expiryDate.DateTime)
            {
                throw new Exception("Error generating signing authority, failed to create certificate with correct expiry date");
            }

            return certificate;
        }

        public static X509Certificate2 CreateCertificate(IEnumerable<string> hosts)
        {
            var expiryDate = DateTimeOffset.UtcNow.AddDays(90);

            using var parentCert = GetSigningAuthority(expiryDate);

            RSA rsa = RSA.Create(2048);

            CertificateRequest req = new CertificateRequest(
                $"CN=KubeConnect - SSL Certificate",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            req.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));

            req.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation,
                    false));

            req.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection
                    {
                    new Oid("1.3.6.1.5.5.7.3.8"),
                    new Oid("1.3.6.1.5.5.7.3.1")
                    },
                    true));

            req.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

            var builder = new SubjectAlternativeNameBuilder();
            foreach (var h in hosts)
            {
                builder.AddDnsName(h);
            }
            req.CertificateExtensions.Add(builder.Build(true));

            X509Certificate2 cert = req.Create(
                        parentCert,
                        DateTimeOffset.UtcNow.AddDays(-1),
                        DateTimeOffset.UtcNow.AddDays(90),
                        new byte[] { 1, 2, 3, 4 });

            var newCert = new X509Certificate2(cert.CopyWithPrivateKey(rsa).Export(X509ContentType.Pkcs12), string.Empty, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

            return newCert;
        }
    }
}
