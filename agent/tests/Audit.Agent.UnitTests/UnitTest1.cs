using Audit.Agent.Storage;

namespace Audit.Agent.UnitTests;

public sealed class ChunkQueueTests
{
    [Fact]
    public async Task Enqueue_Lease_And_MarkUploaded_Works()
    {
        var dbPath = CreateDbPath();
        var queue = new SqliteChunkQueue(dbPath);
        var chunk = NewChunk(0);

        await queue.EnqueueAsync(chunk, CancellationToken.None);
        var leased = await queue.LeaseBatchAsync(10, TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.Single(leased);
        Assert.Equal(chunk.ChunkId, leased[0].ChunkId);

        await queue.MarkUploadedAsync(chunk.ChunkId, CancellationToken.None);
        var depth = await queue.GetDepthAsync(CancellationToken.None);
        Assert.Equal(0, depth);
    }

    [Fact]
    public async Task Enqueue_Duplicate_WithDifferentHash_Throws()
    {
        var dbPath = CreateDbPath();
        var queue = new SqliteChunkQueue(dbPath);
        var chunk = NewChunk(1);

        await queue.EnqueueAsync(chunk, CancellationToken.None);

        var mismatched = chunk with { Sha256Hex = "different-hash" };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            queue.EnqueueAsync(mismatched, CancellationToken.None));
    }

    private static ChunkArtifact NewChunk(int segmentIndex)
    {
        return new ChunkArtifact(
            Guid.NewGuid(),
            Guid.NewGuid(),
            segmentIndex,
            DateTimeOffset.UtcNow.AddSeconds(-5),
            DateTimeOffset.UtcNow,
            $"C:\\spool\\{Guid.NewGuid():N}.enc",
            Guid.NewGuid().ToString("N"),
            1024);
    }

    private static string CreateDbPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "audit-agent-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return Path.Combine(root, "queue.db");
    }
}
