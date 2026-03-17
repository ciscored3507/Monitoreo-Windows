using Audit.Agent.Contracts;
using Audit.Agent.Storage;
using Audit.Ingest.V1;
using Grpc.Core;
using Grpc.Net.Client;

namespace Audit.Agent.Transport;

public sealed class GrpcIngestClient(GrpcChannel channel, AgentTransportOptions options) : IIngestClient
{
    private readonly IngestService.IngestServiceClient _client = new(channel);

    public async Task<UploadResultDto> UploadChunkAsync(QueuedChunk chunk, CancellationToken ct)
    {
        byte[] data = await File.ReadAllBytesAsync(chunk.EncryptedPath, ct);

        using var call = _client.UploadChunks(cancellationToken: ct);
        await call.RequestStream.WriteAsync(new UploadChunkRequest
        {
            Meta = new ChunkMeta
            {
                TenantId = options.TenantId.ToString(),
                DeviceId = options.DeviceId.ToString(),
                SessionId = chunk.SessionId.ToString(),
                SegmentIndex = chunk.SegmentIndex,
                ChunkId = chunk.ChunkId.ToString(),
                Sha256Hex = chunk.Sha256Hex,
                Bytes = chunk.Bytes,
                CaptureStartUnixMs = chunk.CaptureStartUtc.ToUnixTimeMilliseconds(),
                CaptureEndUnixMs = chunk.CaptureEndUtc.ToUnixTimeMilliseconds(),
                ContentType = "video/mp4"
            },
            Data = Google.Protobuf.ByteString.CopyFrom(data)
        });

        await call.RequestStream.CompleteAsync();

        if (!await call.ResponseStream.MoveNext(ct))
        {
            throw new RpcException(new Status(StatusCode.Unknown, "upload_response_empty"));
        }

        var response = call.ResponseStream.Current;
        return new UploadResultDto(Guid.Parse(response.ChunkId), response.Stored, response.ObjectKey);
    }
}
