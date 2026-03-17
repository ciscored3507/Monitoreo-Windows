using System.Security.Cryptography.X509Certificates;

namespace Audit.Agent.Transport;

public static class TransportSecurity
{
    public static SocketsHttpHandler CreateHttpHandler(AgentTransportOptions options)
    {
        var handler = new SocketsHttpHandler();

        var clientCert = LoadClientCertificate(options);
        if (clientCert is not null)
        {
            handler.SslOptions.ClientCertificates ??= [];
            handler.SslOptions.ClientCertificates.Add(clientCert);
        }

        handler.SslOptions.RemoteCertificateValidationCallback = (_, cert, _, errors) =>
        {
            if (options.AllowInvalidServerCertificate)
            {
                return true;
            }

            if (cert is null)
            {
                return false;
            }

            if (options.TrustedServerThumbprints.Count == 0)
            {
                return errors == System.Net.Security.SslPolicyErrors.None;
            }

            var normalized = NormalizeThumbprint(cert.GetCertHashString());
            return options.TrustedServerThumbprints
                .Select(NormalizeThumbprint)
                .Any(t => t == normalized);
        };

        return handler;
    }

    public static X509Certificate2? LoadClientCertificate(AgentTransportOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ClientCertificatePfxPath))
        {
            return X509CertificateLoader.LoadPkcs12FromFile(
                options.ClientCertificatePfxPath,
                options.ClientCertificatePfxPassword);
        }

        if (string.IsNullOrWhiteSpace(options.ClientCertificateThumbprint))
        {
            return null;
        }

        var storeName = Enum.TryParse<StoreName>(options.ClientCertificateStoreName, true, out var sName) ? sName : StoreName.My;
        var storeLocation = Enum.TryParse<StoreLocation>(options.ClientCertificateStoreLocation, true, out var sLoc) ? sLoc : StoreLocation.CurrentUser;
        using var store = new X509Store(storeName, storeLocation);
        store.Open(OpenFlags.ReadOnly);

        var target = NormalizeThumbprint(options.ClientCertificateThumbprint);
        return store.Certificates
            .Cast<X509Certificate2>()
            .FirstOrDefault(c => NormalizeThumbprint(c.Thumbprint) == target);
    }

    private static string NormalizeThumbprint(string? value)
    {
        return (value ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }
}
