using Microsoft.AspNetCore.Server.Kestrel.Core;
using Serilog;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

internal partial class Program
{
    private static void ConfigureKestrelHttps(WebHostBuilderContext context, KestrelServerOptions options)
    {
        var isDebugOrDevelopment = context.HostingEnvironment.IsDevelopment();
#if DEBUG
        isDebugOrDevelopment = true;
#endif

        if (!isDebugOrDevelopment)
        {
            return;
        }

        var httpsEndpointUrl = context.Configuration["Kestrel:Endpoints:Https:Url"];
        var aspnetUrls = context.Configuration["ASPNETCORE_URLS"] ?? context.Configuration["URLS"];
        var hasHttpsEndpoint = !string.IsNullOrWhiteSpace(httpsEndpointUrl) ||
                               (!string.IsNullOrWhiteSpace(aspnetUrls) && aspnetUrls.Contains("https://", StringComparison.OrdinalIgnoreCase));

        if (!hasHttpsEndpoint)
        {
            return;
        }

        var explicitCertificatePath = context.Configuration["Kestrel:Certificates:Default:Path"];
        if (!string.IsNullOrWhiteSpace(explicitCertificatePath) && File.Exists(explicitCertificatePath))
        {
            return;
        }

        string certsDir = Path.Combine(Directory.GetCurrentDirectory(), "Data", "certs");
        string defaultPfxPath = Path.Combine(certsDir, "localhost.pfx");
        string defaultCaPath = Path.Combine(certsDir, "ca.crt");

        var pfxPassword = context.Configuration["Kestrel:Certificates:Default:Password"] ?? "penguin";

        if (!File.Exists(defaultPfxPath))
        {
            try
            {
                EnsureLocalhostCertificates(certsDir, defaultPfxPath, defaultCaPath, pfxPassword);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to automatically generate local certificate files in {CertsDir}", certsDir);
            }
        }

        string[] searchPaths = [
            defaultPfxPath,
            Path.Combine(AppContext.BaseDirectory, "Data", "certs", "localhost.pfx")
        ];

        foreach (var pfxPath in searchPaths)
        {
            if (File.Exists(pfxPath))
            {
                options.ConfigureHttpsDefaults(httpsOptions =>
                {
                    httpsOptions.ServerCertificate = X509CertificateLoader.LoadPkcs12FromFile(pfxPath, pfxPassword);
                });
                Log.Information("Using persistent localhost certificate from {Path}", pfxPath);
                return;
            }
        }

        _generatedHttpsCertificate ??= CreateSelfSignedHttpsCertificate();
        options.ConfigureHttpsDefaults(httpsOptions =>
        {
            httpsOptions.ServerCertificate = _generatedHttpsCertificate;
        });

        Log.Information("No HTTPS certificate was configured for Kestrel. An application-generated self-signed certificate will be used for the HTTPS endpoint.");
    }

    private static void EnsureLocalhostCertificates(string certsDir, string pfxPath, string caPath, string pfxPassword)
    {
        Directory.CreateDirectory(certsDir);

        using var caRsa = RSA.Create(2048);
        var caRequest = new CertificateRequest(
            "CN=PenguinTwitchBot Local CA",
            caRsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: true, pathLengthConstraint: 1, critical: true));
        caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
        caRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(caRequest.PublicKey, critical: false));

        using var caCert = caRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(10));

        using var leafRsa = RSA.Create(2048);
        var leafRequest = new CertificateRequest(
            "CN=PenguinTwitchBot",
            leafRsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
        foreach (var address in GetLocalIpv4Addresses())
        {
            sanBuilder.AddIpAddress(address);
        }

        leafRequest.CertificateExtensions.Add(sanBuilder.Build());
        leafRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: false));
        leafRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: false));
        leafRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(leafRequest.PublicKey, critical: false));

        Span<byte> serialNumber = stackalloc byte[16];
        RandomNumberGenerator.Fill(serialNumber);

        using var leafCert = leafRequest.Create(
            caCert,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(5),
            serialNumber);

        using var leafWithKey = leafCert.CopyWithPrivateKey(leafRsa);

        File.WriteAllText(caPath, caCert.ExportCertificatePem());
        File.WriteAllBytes(pfxPath, leafWithKey.Export(X509ContentType.Pfx, pfxPassword));

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(pfxPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to set Unix owner-only file permissions on {PfxPath}", pfxPath);
            }
        }

        Log.Information("Generated persistent localhost development certificate at {PfxPath} and root CA at {CaPath}", pfxPath, caPath);
    }

    private static X509Certificate2 CreateSelfSignedHttpsCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=PenguinTwitchBot",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);

        foreach (var address in GetLocalIpv4Addresses())
        {
            sanBuilder.AddIpAddress(address);
        }

        request.CertificateExtensions.Add(sanBuilder.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), ReadOnlySpan<char>.Empty, X509KeyStorageFlags.DefaultKeySet);
    }

    private static IEnumerable<IPAddress> GetLocalIpv4Addresses()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            var ipProperties = networkInterface.GetIPProperties();
            foreach (var unicastAddress in ipProperties.UnicastAddresses)
            {
                if (unicastAddress.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(unicastAddress.Address))
                {
                    yield return unicastAddress.Address;
                }
            }
        }
    }
}
