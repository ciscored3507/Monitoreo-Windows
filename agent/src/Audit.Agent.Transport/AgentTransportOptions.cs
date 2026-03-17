namespace Audit.Agent.Transport;

public sealed class AgentTransportOptions
{
    public Uri RestBaseUrl { get; set; } = new("https://localhost:7249/");
    public Uri GrpcEndpoint { get; set; } = new("https://localhost:7249");
    public Guid DeviceId { get; set; } = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public Guid TenantId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public bool UseSimulatedUpload { get; set; } = true;
    public bool AllowInvalidServerCertificate { get; set; } = true;
    public bool SimulateInProcessRotation { get; set; } = true;
    public string? ClientCertificateThumbprint { get; set; }
    public string? PreviousCertificateThumbprint { get; set; }
    public string ClientCertificateStoreName { get; set; } = "My";
    public string ClientCertificateStoreLocation { get; set; } = "CurrentUser";
    public string? ClientCertificatePfxPath { get; set; }
    public string? ClientCertificatePfxPassword { get; set; }
    public List<string> TrustedServerThumbprints { get; set; } = [];
}
