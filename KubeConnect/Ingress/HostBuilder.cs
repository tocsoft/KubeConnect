using k8s;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KubeConnect.Ingress
{
    public static class HostBuilder
    {
        public static IHost CreateHost(ServiceManager manager, IConsole console)
        {
            return CreateHostBuilder(manager, console).Build();//.RunAsync(cancellationToken);
        }

        private static IHostBuilder CreateHostBuilder(ServiceManager manager, IConsole console) =>
            Host.CreateDefaultBuilder(Array.Empty<string>())
                .ConfigureLogging((s, o) =>
                {
                    o.ClearProviders();
                    o.Services.AddSingleton<ILoggerProvider, IConsoleLogProvider>();
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton(manager);
                    services.AddSingleton(console);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(serverOptions =>
                    {
                        serverOptions.ConfigureHttpsDefaults(opts =>
                        {
                            opts.ServerCertificate = CreateCertificate(manager.IngressHostNames);
                        });
                    });

                    webBuilder.UseUrls($"http://{manager.IngressIPAddress}", $"https://{manager.IngressIPAddress}");

                    webBuilder.UseStartup<Startup>();
                });

        private const string autorityName = "KubeConnect Issuing Authority";

        private static X509Certificate2 GetSigningAuthority()
        {
            X509Certificate2 certificate = null;
            bool inStore = false;
            // if windows load from cert store and return
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser, OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                var certificateSet = store.Certificates.Find(X509FindType.FindByIssuerName, autorityName, true);
                var storeCert = certificateSet.Cast<X509Certificate2>().FirstOrDefault();
                if (storeCert?.HasPrivateKey == true)
                {
                    certificate = storeCert;
                    inStore = true;
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

            if (certificate == null)
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

                certificate = parentReq.CreateSelfSigned(
                  DateTimeOffset.UtcNow.AddDays(-45),
                  DateTimeOffset.UtcNow.AddDays(365));

                var newCert = new X509Certificate2(certificate.Export(X509ContentType.Pkcs12), (string)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

                certificate = newCert;
                if (!inStore)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser, OpenFlags.ReadWrite | OpenFlags.OpenExistingOnly | OpenFlags.MaxAllowed);

                        if (!certificate.HasPrivateKey)
                        {
                            certificate = certificate.CopyWithPrivateKey(parent);
                        }
                        store.Add(certificate);
                        store.Close();
                    }
                    else
                    {
                        var dir = Path.GetDirectoryName(rootCertificatePath);
                        Directory.CreateDirectory(dir);

                        File.WriteAllBytes(rootCertificatePath, certificate.Export(X509ContentType.Pfx));
                    }
                }
            }

            return certificate;
        }

        private static X509Certificate2 CreateCertificate(IEnumerable<string> hosts)
        {
            using var parentCert = GetSigningAuthority();

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

            var newCert = new X509Certificate2(cert.CopyWithPrivateKey(rsa).Export(X509ContentType.Pkcs12), (string)null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

            return newCert;
        }
    }

    public class IConsoleLogProvider : ILoggerProvider, ILogger
    {
        private readonly IConsole console;

        public IConsoleLogProvider(IConsole console)
        {
            this.console = console;
        }

        public IDisposable BeginScope<TState>(TState state)
            => this;

        public ILogger CreateLogger(string categoryName)
            => this;

        public void Dispose()
        {
        }

        public bool IsEnabled(LogLevel logLevel)
            => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel)){
                console.WriteLine(formatter(state, exception));
            }
        }
    }
}
