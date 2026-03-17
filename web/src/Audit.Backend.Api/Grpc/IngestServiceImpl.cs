using Audit.Backend.Api.Services;
using Audit.Ingest.V1;
using Grpc.Core;

namespace Audit.Backend.Api.Grpc;

public sealed class IngestServiceImpl(IChunkStore chunkStore, ILogger<IngestServiceImpl> logger)
    : IngestService.IngestServiceBase
{
    public override async Task UploadChunks(
        IAsyncStreamReader<UploadChunkRequest> requestStream,
        IServerStreamWriter<UploadChunkResponse> responseStream,
        ServerCallContext context)
    {
        while (await requestStream.MoveNext(context.CancellationToken))
        {
            var req = requestStream.Current;
            if (req?.Meta is null || string.IsNullOrWhiteSpace(req.Meta.ChunkId) || req.Data.IsEmpty)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "invalid_chunk"));
            }

            try
            {
                var result = chunkStore.StoreChunk(req.Meta.ChunkId, req.Meta.Sha256Hex);
                await responseStream.WriteAsync(new UploadChunkResponse
                {
                    ChunkId = req.Meta.ChunkId,
                    Stored = result.Stored,
                    ObjectKey = result.ObjectKey
                });
            }
            catch (InvalidOperationException ex) when (ex.Message == "hash_mismatch")
            {
                logger.LogWarning(ex, "hash mismatch for chunk {ChunkId}", req.Meta.ChunkId);
                throw new RpcException(new Status(StatusCode.AlreadyExists, "hash_mismatch"));
            }
        }
    }
}
