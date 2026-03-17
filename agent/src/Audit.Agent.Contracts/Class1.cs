namespace Audit.Agent.Contracts;

public static class ProblemCodes
{
    public const string InvalidPayload = "invalid_payload";
    public const string UnauthorizedDevice = "unauthorized_device";
    public const string HashMismatch = "hash_mismatch";
    public const string AlreadyEnrolled = "already_enrolled";
}

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

public sealed record HeartbeatDto(
    DateTimeOffset DeviceTimeUtc,
    string AgentVersion,
    int QueueDepth,
    DateTimeOffset? LastUploadUtc,
    IReadOnlyList<string> HealthFlags);

public sealed record HeartbeatResponse(DateTimeOffset ServerTimeUtc);

public sealed record UploadResultDto(Guid ChunkId, bool Stored, string ObjectKey);
