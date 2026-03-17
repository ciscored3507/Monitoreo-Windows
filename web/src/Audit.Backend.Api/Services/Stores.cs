using System.Collections.Concurrent;
using Audit.Backend.Contracts;
using Audit.Backend.Infrastructure.Persistence;

namespace Audit.Backend.Api.Services;

public interface IEnrollmentStore
{
    EnrollResponse Enroll(EnrollRequest request);
    PolicyDto GetPolicy(Guid deviceId);
    void Heartbeat(Guid deviceId, HeartbeatRequest request);
    IReadOnlyList<DeviceSummary> ListDevices(string? q, string? status);
    IReadOnlyList<object> ListSessions(Guid deviceId, DateTimeOffset fromUtc, DateTimeOffset toUtc);
    void BindCertificate(Guid deviceId, string thumbprintSha256);
    bool RevokeCertificate(Guid deviceId, string thumbprintSha256, string? reason);
    IReadOnlyList<DeviceCertificateDto> ListCertificates(Guid deviceId);
    bool TryResolveByCertificateThumbprint(string thumbprintSha256, out Guid deviceId, out Guid tenantId);
    void SetRotationInstruction(Guid deviceId, string newThumbprintSha256, int graceSeconds);
    CertificateRotationInstructionDto? GetRotationInstruction(Guid deviceId);
    StartRotationResponse BeginCertificateRotation(Guid deviceId, string oldThumbprintSha256, string newThumbprintSha256, int graceSeconds);
    bool ConfirmCertificateRotation(Guid deviceId, string oldThumbprintSha256, string newThumbprintSha256);
}

public sealed class InMemoryEnrollmentStore(CertificateStateRepository repository) : IEnrollmentStore
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _policyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public EnrollResponse Enroll(EnrollRequest request)
    {
        var deviceId = repository.GetOrCreateDeviceIdByFingerprint(request.DeviceFingerprint);
        repository.UpsertDevice(deviceId, request.Hostname, "active", DateTimeOffset.UtcNow);
        return new EnrollResponse(TenantId, deviceId, _policyId, null);
    }

    public PolicyDto GetPolicy(Guid deviceId)
    {
        repository.UpsertDevice(deviceId, $"device-{deviceId:N}", "active", DateTimeOffset.UtcNow);
        return new PolicyDto(
            _policyId,
            1,
            new CapturePolicy(
                ChunkSeconds: 5,
                Fps: 10,
                Resolution: "1080p",
                Targets: [new PolicyTarget("display", "primary")],
                AudioEnabled: true,
                ExcludeTitles: ["Password"]),
            new UploadPolicy("https://localhost:5001", 2));
    }

    public void Heartbeat(Guid deviceId, HeartbeatRequest request)
    {
        repository.UpsertDevice(deviceId, $"device-{deviceId:N}", "active", DateTimeOffset.UtcNow);
    }

    public IReadOnlyList<DeviceSummary> ListDevices(string? q, string? status)
    {
        return repository.ListDevices(q, status)
            .Select(x => new DeviceSummary(x.DeviceId, x.Hostname, x.Status, x.LastSeenUtc))
            .ToList();
    }

    public IReadOnlyList<object> ListSessions(Guid deviceId, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        return
        [
            new
            {
                session_id = Guid.NewGuid(),
                device_id = deviceId,
                started_utc = fromUtc,
                ended_utc = toUtc,
                state = "completed"
            }
        ];
    }

    public void BindCertificate(Guid deviceId, string thumbprintSha256)
    {
        repository.BindCertificate(deviceId, thumbprintSha256);
    }

    public bool RevokeCertificate(Guid deviceId, string thumbprintSha256, string? reason)
    {
        var ok = repository.RevokeCertificate(deviceId, thumbprintSha256, reason);
        if (ok)
        {
            repository.UpsertDevice(deviceId, $"device-{deviceId:N}", "revoked", DateTimeOffset.UtcNow);
        }

        return ok;
    }

    public IReadOnlyList<DeviceCertificateDto> ListCertificates(Guid deviceId)
    {
        return repository.ListCertificates(deviceId)
            .Select(x => new DeviceCertificateDto(
                x.DeviceId,
                x.ThumbprintSha256,
                x.Revoked,
                x.BoundAtUtc,
                x.RevokedAtUtc,
                x.Reason))
            .ToList();
    }

    public bool TryResolveByCertificateThumbprint(string thumbprintSha256, out Guid deviceId, out Guid tenantId)
    {
        if (repository.TryResolveByCertificate(thumbprintSha256, out deviceId))
        {
            tenantId = TenantId;
            return true;
        }

        tenantId = Guid.Empty;
        return false;
    }

    public void SetRotationInstruction(Guid deviceId, string newThumbprintSha256, int graceSeconds)
    {
        repository.SetRotationInstruction(deviceId, newThumbprintSha256, graceSeconds);
    }

    public CertificateRotationInstructionDto? GetRotationInstruction(Guid deviceId)
    {
        var instruction = repository.GetRotationInstruction(deviceId);
        return instruction is null
            ? null
            : new CertificateRotationInstructionDto(instruction.Value.NewThumbprintSha256, instruction.Value.GraceSeconds);
    }

    public StartRotationResponse BeginCertificateRotation(Guid deviceId, string oldThumbprintSha256, string newThumbprintSha256, int graceSeconds)
    {
        var rotation = repository.BeginRotation(deviceId, oldThumbprintSha256, newThumbprintSha256, graceSeconds);
        BindCertificate(deviceId, rotation.NewThumbprintSha256);
        return new StartRotationResponse(deviceId, rotation.OldThumbprintSha256, rotation.NewThumbprintSha256, rotation.RevokeAfterUtc);
    }

    public bool ConfirmCertificateRotation(Guid deviceId, string oldThumbprintSha256, string newThumbprintSha256)
    {
        return repository.ConfirmRotation(deviceId, oldThumbprintSha256, newThumbprintSha256);
    }
}

public interface IChunkStore
{
    (bool Stored, string ObjectKey) StoreChunk(string chunkId, string sha256Hex);
}

public sealed class InMemoryChunkStore : IChunkStore
{
    private readonly ConcurrentDictionary<string, string> _chunks = new(StringComparer.OrdinalIgnoreCase);

    public (bool Stored, string ObjectKey) StoreChunk(string chunkId, string sha256Hex)
    {
        if (_chunks.TryGetValue(chunkId, out var existing))
        {
            if (!existing.Equals(sha256Hex, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("hash_mismatch");
            }

            return (false, $"t=demo/d=demo/s=demo/seg={chunkId}.mp4.enc");
        }

        _chunks[chunkId] = sha256Hex;
        return (true, $"t=demo/d=demo/s=demo/seg={chunkId}.mp4.enc");
    }
}
