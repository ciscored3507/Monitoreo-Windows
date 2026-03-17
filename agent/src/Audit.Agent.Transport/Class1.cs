using Audit.Agent.Contracts;
using Audit.Agent.Storage;

namespace Audit.Agent.Transport;

public interface IIngestClient
{
    Task<UploadResultDto> UploadChunkAsync(QueuedChunk chunk, CancellationToken ct);
}

public sealed class SimulatedIngestClient : IIngestClient
{
    private readonly Dictionary<Guid, string> _seen = [];
    private readonly Lock _gate = new();

    public Task<UploadResultDto> UploadChunkAsync(QueuedChunk chunk, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_seen.TryGetValue(chunk.ChunkId, out var existingHash))
            {
                if (!existingHash.Equals(chunk.Sha256Hex, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("hash_mismatch");
                }

                return Task.FromResult(new UploadResultDto(chunk.ChunkId, false, BuildObjectKey(chunk)));
            }

            _seen[chunk.ChunkId] = chunk.Sha256Hex;
            return Task.FromResult(new UploadResultDto(chunk.ChunkId, true, BuildObjectKey(chunk)));
        }
    }

    private static string BuildObjectKey(QueuedChunk chunk)
    {
        return $"t=demo/d=demo/y={chunk.CaptureStartUtc:yyyy}/m={chunk.CaptureStartUtc:MM}/day={chunk.CaptureStartUtc:dd}/s={chunk.SessionId}/seg={chunk.SegmentIndex:D6}.mp4.enc";
    }
}
