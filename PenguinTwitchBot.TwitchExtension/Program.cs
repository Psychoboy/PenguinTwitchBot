using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel((context, options) =>
{
    var httpsUrl = context.Configuration["Kestrel:Endpoints:Https:Url"];
    if (string.IsNullOrWhiteSpace(httpsUrl))
    {
        options.Listen(IPAddress.Loopback, 8080, listenOptions =>
        {
            listenOptions.UseHttps(CreateSelfSignedCertificate());
        });
    }
});

builder.Services.AddHttpClient();
builder.Services.AddControllers();

var app = builder.Build();

app.UseStaticFiles();
app.MapControllers();

app.MapGet("/config", () => Results.Redirect("/config.html"));
app.MapGet("/panel", () => Results.Redirect("/panel.html"));
app.MapFallbackToFile("panel.html");

app.Run();

static X509Certificate2 CreateSelfSignedCertificate()
{
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest(
        "CN=PenguinTwitchBot.TwitchExtension",
        rsa,
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);

    var sanBuilder = new SubjectAlternativeNameBuilder();
    sanBuilder.AddDnsName("localhost");
    sanBuilder.AddIpAddress(IPAddress.Loopback);
    sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
    request.CertificateExtensions.Add(sanBuilder.Build());
    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
    request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

    using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));
    return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), ReadOnlySpan<char>.Empty, X509KeyStorageFlags.DefaultKeySet);
}