namespace Audit.Backend.Contracts;

public sealed record EnrollRequest(
    string Hostname,
    string OsVersion,
    string DeviceFingerprint,
    string? CsrPem);

public sealed record EnrollResponse(
    Guid TenantId,
    Guid DeviceId,
    Guid PolicyId,
    string? DeviceCertPem);

public sealed record PolicyTarget(string Type, string Value);

public sealed record CapturePolicy(
    int ChunkSeconds,
    int Fps,
    string Resolution,
    IReadOnlyList<PolicyTarget> Targets,
    bool AudioEnabled,
    IReadOnlyList<string> ExcludeTitles);

public sealed record UploadPolicy(string GrpcEndpoint, int MaxParallel);

public sealed record PolicyDto(
    Guid PolicyId,
    int Version,
    CapturePolicy Capture,
    UploadPolicy Upload);

public sealed record HeartbeatRequest(
    DateTimeOffset DeviceTimeUtc,
    string AgentVersion,
    int QueueDepth,
    DateTimeOffset? LastUploadUtc,
    IReadOnlyList<string> HealthFlags);

public sealed record CertificateRotationInstructionDto(
    string NewThumbprintSha256,
    int GraceSeconds);

public sealed record HeartbeatResponse(
    DateTimeOffset ServerTimeUtc,
    CertificateRotationInstructionDto? CertificateRotation);

public sealed record DeviceSummary(Guid DeviceId, string Hostname, string Status, DateTimeOffset? LastSeenUtc);

public sealed record BindCertificateRequest(string ThumbprintSha256);
public sealed record RevokeCertificateRequest(string ThumbprintSha256, string? Reason);
public sealed record DeviceCertificateDto(
    Guid DeviceId,
    string ThumbprintSha256,
    bool Revoked,
    DateTimeOffset BoundAtUtc,
    DateTimeOffset? RevokedAtUtc,
    string? RevocationReason);

public sealed record StartRotationRequest(string NewThumbprintSha256, int GraceSeconds);
public sealed record StartRotationResponse(Guid DeviceId, string OldThumbprintSha256, string NewThumbprintSha256, DateTimeOffset RevokeAfterUtc);
public sealed record ConfirmRotationRequest(string OldThumbprintSha256, string NewThumbprintSha256);
