using Microsoft.Data.Sqlite;

namespace Audit.Agent.Storage;

public sealed record ChunkArtifact(
    Guid ChunkId,
    Guid SessionId,
    int SegmentIndex,
    DateTimeOffset CaptureStartUtc,
    DateTimeOffset CaptureEndUtc,
    string LocalEncryptedPath,
    string Sha256Hex,
    long Bytes,
    string ContentType = "video/mp4");

public sealed record QueuedChunk(
    Guid ChunkId,
    Guid SessionId,
    int SegmentIndex,
    string Sha256Hex,
    long Bytes,
    DateTimeOffset CaptureStartUtc,
    DateTimeOffset CaptureEndUtc,
    string EncryptedPath,
    int Attempts);

public interface IChunkQueue
{
    Task EnqueueAsync(ChunkArtifact chunk, CancellationToken ct);
    Task<IReadOnlyList<QueuedChunk>> LeaseBatchAsync(int max, TimeSpan lease, CancellationToken ct);
    Task MarkUploadedAsync(Guid chunkId, CancellationToken ct);
    Task MarkFailedAsync(Guid chunkId, string reason, DateTimeOffset nextRetryUtc, CancellationToken ct);
    Task MoveToDeadLetterAsync(Guid chunkId, string reason, CancellationToken ct);
    Task<int> GetDepthAsync(CancellationToken ct);
}

public sealed class SqliteChunkQueue : IChunkQueue
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initGate = new(1, 1);
    private bool _initialized;

    public SqliteChunkQueue(string databasePath)
    {
        var normalizedPath = Path.IsPathRooted(databasePath)
            ? databasePath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, databasePath));
        var directory = Path.GetDirectoryName(normalizedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = $"Data Source={normalizedPath}";
    }

    public async Task EnqueueAsync(ChunkArtifact chunk, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var ensureSession = conn.CreateCommand();
        ensureSession.CommandText = """
            INSERT OR IGNORE INTO sessions(session_id, device_id, policy_id, started_utc, state)
            VALUES ($session_id, $device_id, $policy_id, $started_utc, $state);
            """;
        ensureSession.Parameters.AddWithValue("$session_id", chunk.SessionId.ToString());
        ensureSession.Parameters.AddWithValue("$device_id", "00000000-0000-0000-0000-000000000000");
        ensureSession.Parameters.AddWithValue("$policy_id", "00000000-0000-0000-0000-000000000000");
        ensureSession.Parameters.AddWithValue("$started_utc", chunk.CaptureStartUtc.UtcDateTime.ToString("O"));
        ensureSession.Parameters.AddWithValue("$state", 1);
        await ensureSession.ExecuteNonQueryAsync(ct);

        var insert = conn.CreateCommand();
        insert.CommandText = """
            INSERT OR IGNORE INTO chunk_queue(
                chunk_id, session_id, segment_index, sha256_hex, bytes, capture_start_utc,
                capture_end_utc, encrypted_path, status, attempts, created_utc
            ) VALUES (
                $chunk_id, $session_id, $segment_index, $sha256_hex, $bytes, $capture_start_utc,
                $capture_end_utc, $encrypted_path, 0, 0, $created_utc
            );
            """;
        insert.Parameters.AddWithValue("$chunk_id", chunk.ChunkId.ToString());
        insert.Parameters.AddWithValue("$session_id", chunk.SessionId.ToString());
        insert.Parameters.AddWithValue("$segment_index", chunk.SegmentIndex);
        insert.Parameters.AddWithValue("$sha256_hex", chunk.Sha256Hex);
        insert.Parameters.AddWithValue("$bytes", chunk.Bytes);
        insert.Parameters.AddWithValue("$capture_start_utc", chunk.CaptureStartUtc.UtcDateTime.ToString("O"));
        insert.Parameters.AddWithValue("$capture_end_utc", chunk.CaptureEndUtc.UtcDateTime.ToString("O"));
        insert.Parameters.AddWithValue("$encrypted_path", chunk.LocalEncryptedPath);
        insert.Parameters.AddWithValue("$created_utc", DateTimeOffset.UtcNow.UtcDateTime.ToString("O"));

        var affected = await insert.ExecuteNonQueryAsync(ct);
        if (affected > 0)
        {
            return;
        }

        var verify = conn.CreateCommand();
        verify.CommandText = "SELECT sha256_hex FROM chunk_queue WHERE chunk_id = $chunk_id";
        verify.Parameters.AddWithValue("$chunk_id", chunk.ChunkId.ToString());
        var existing = (string?)await verify.ExecuteScalarAsync(ct);
        if (!string.Equals(existing, chunk.Sha256Hex, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("hash_mismatch");
        }
    }

    public async Task<IReadOnlyList<QueuedChunk>> LeaseBatchAsync(int max, TimeSpan lease, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var leaseUntil = now.Add(lease);

        var recover = conn.CreateCommand();
        recover.CommandText = """
            UPDATE chunk_queue
            SET status = 0, lease_until_utc = NULL
            WHERE status = 1 AND lease_until_utc IS NOT NULL AND lease_until_utc < $now;
            """;
        recover.Parameters.AddWithValue("$now", now.UtcDateTime.ToString("O"));
        await recover.ExecuteNonQueryAsync(ct);

        var result = new List<QueuedChunk>(max);
        var select = conn.CreateCommand();
        select.CommandText = """
            SELECT chunk_id, session_id, segment_index, sha256_hex, bytes,
                   capture_start_utc, capture_end_utc, encrypted_path, attempts
            FROM chunk_queue
            WHERE status = 0 AND (next_retry_utc IS NULL OR next_retry_utc <= $now)
            ORDER BY created_utc
            LIMIT $max;
            """;
        select.Parameters.AddWithValue("$now", now.UtcDateTime.ToString("O"));
        select.Parameters.AddWithValue("$max", max);

        await using var reader = await select.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var chunkId = Guid.Parse(reader.GetString(0));
            var sessionId = Guid.Parse(reader.GetString(1));
            var item = new QueuedChunk(
                chunkId,
                sessionId,
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetInt64(4),
                DateTimeOffset.Parse(reader.GetString(5)),
                DateTimeOffset.Parse(reader.GetString(6)),
                reader.GetString(7),
                reader.GetInt32(8));
            result.Add(item);
        }

        foreach (var item in result)
        {
            var leaseCmd = conn.CreateCommand();
            leaseCmd.CommandText = """
                UPDATE chunk_queue
                SET status = 1, lease_until_utc = $lease_until, attempts = attempts + 1
                WHERE chunk_id = $chunk_id AND status = 0;
                """;
            leaseCmd.Parameters.AddWithValue("$lease_until", leaseUntil.UtcDateTime.ToString("O"));
            leaseCmd.Parameters.AddWithValue("$chunk_id", item.ChunkId.ToString());
            await leaseCmd.ExecuteNonQueryAsync(ct);
        }

        return result;
    }

    public async Task MarkUploadedAsync(Guid chunkId, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE chunk_queue
            SET status = 2, lease_until_utc = NULL, next_retry_utc = NULL, last_error = NULL
            WHERE chunk_id = $chunk_id;
            """;
        cmd.Parameters.AddWithValue("$chunk_id", chunkId.ToString());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task MarkFailedAsync(Guid chunkId, string reason, DateTimeOffset nextRetryUtc, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE chunk_queue
            SET status = 0, lease_until_utc = NULL, next_retry_utc = $next_retry_utc, last_error = $reason
            WHERE chunk_id = $chunk_id;
            """;
        cmd.Parameters.AddWithValue("$next_retry_utc", nextRetryUtc.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("$reason", reason);
        cmd.Parameters.AddWithValue("$chunk_id", chunkId.ToString());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task MoveToDeadLetterAsync(Guid chunkId, string reason, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE chunk_queue
            SET status = 3, lease_until_utc = NULL, last_error = $reason
            WHERE chunk_id = $chunk_id;
            """;
        cmd.Parameters.AddWithValue("$reason", reason);
        cmd.Parameters.AddWithValue("$chunk_id", chunkId.ToString());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> GetDepthAsync(CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM chunk_queue WHERE status IN (0, 1)";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized)
        {
            return;
        }

        await _initGate.WaitAsync(ct);
        try
        {
            if (_initialized)
            {
                return;
            }

            var schemaPath = Path.Combine(AppContext.BaseDirectory, "sqlite-schema.sql");
            if (!File.Exists(schemaPath))
            {
                throw new FileNotFoundException("sqlite-schema.sql no encontrado", schemaPath);
            }

            var sql = await File.ReadAllTextAsync(schemaPath, ct);
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(ct);
            _initialized = true;
        }
        finally
        {
            _initGate.Release();
        }
    }
}
