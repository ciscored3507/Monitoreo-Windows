using System.Collections.Concurrent;
using Audit.Backend.Contracts;

namespace Audit.Backend.Api.Services;

public interface IEnrollmentStore
{
    EnrollResponse Enroll(EnrollRequest request);
    PolicyDto GetPolicy(Guid deviceId);
    void Heartbeat(Guid deviceId, HeartbeatRequest request);
    IReadOnlyList<DeviceSummary> ListDevices(string? q, string? status);
    IReadOnlyList<object> ListSessions(Guid deviceId, DateTimeOffset fromUtc, DateTimeOffset toUtc);
}

public sealed class InMemoryEnrollmentStore : IEnrollmentStore
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly ConcurrentDictionary<Guid, DeviceSummary> _devices = new();
    private readonly ConcurrentDictionary<string, Guid> _fingerprints = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastHeartbeat = new();
    private readonly Guid _policyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public EnrollResponse Enroll(EnrollRequest request)
    {
        var deviceId = _fingerprints.GetOrAdd(request.DeviceFingerprint, _ => Guid.NewGuid());
        _devices[deviceId] = new DeviceSummary(deviceId, request.Hostname, "active", DateTimeOffset.UtcNow);
        return new EnrollResponse(TenantId, deviceId, _policyId, null);
    }

    public PolicyDto GetPolicy(Guid deviceId)
    {
        _devices.TryAdd(deviceId, new DeviceSummary(deviceId, $"device-{deviceId:N}", "active", DateTimeOffset.UtcNow));
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
        _lastHeartbeat[deviceId] = DateTimeOffset.UtcNow;
        if (_devices.TryGetValue(deviceId, out var existing))
        {
            _devices[deviceId] = existing with { LastSeenUtc = DateTimeOffset.UtcNow, Status = "active" };
        }
    }

    public IReadOnlyList<DeviceSummary> ListDevices(string? q, string? status)
    {
        IEnumerable<DeviceSummary> query = _devices.Values;
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(d => d.Hostname.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(d => d.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderBy(d => d.Hostname).ToList();
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
