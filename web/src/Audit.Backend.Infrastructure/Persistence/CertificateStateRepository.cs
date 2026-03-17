using Microsoft.Data.Sqlite;

namespace Audit.Backend.Infrastructure.Persistence;

public sealed class CertificateStateRepository
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initGate = new(1, 1);
    private bool _initialized;

    public CertificateStateRepository(BackendStorageOptions options)
    {
        var path = Path.IsPathRooted(options.DatabasePath)
            ? options.DatabasePath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, options.DatabasePath));
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = $"Data Source={path}";
    }

    public Guid GetOrCreateDeviceIdByFingerprint(string fingerprint)
    {
        EnsureInitialized();
        using var conn = Open();
        var query = conn.CreateCommand();
        query.CommandText = "SELECT device_id FROM fingerprints WHERE fingerprint = $fingerprint";
        query.Parameters.AddWithValue("$fingerprint", fingerprint);
        var found = query.ExecuteScalar() as string;
        if (!string.IsNullOrWhiteSpace(found))
        {
            return Guid.Parse(found);
        }

        var deviceId = Guid.NewGuid();
        var insert = conn.CreateCommand();
        insert.CommandText = "INSERT INTO fingerprints(fingerprint, device_id) VALUES($fingerprint, $device_id)";
        insert.Parameters.AddWithValue("$fingerprint", fingerprint);
        insert.Parameters.AddWithValue("$device_id", deviceId.ToString());
        insert.ExecuteNonQuery();
        return deviceId;
    }

    public void UpsertDevice(Guid deviceId, string hostname, string status, DateTimeOffset? lastSeenUtc)
    {
        EnsureInitialized();
        using var conn = Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO devices(device_id, hostname, status, last_seen_utc)
            VALUES($device_id, $hostname, $status, $last_seen_utc)
            ON CONFLICT(device_id) DO UPDATE SET
              hostname = excluded.hostname,
              status = excluded.status,
              last_seen_utc = excluded.last_seen_utc;
            """;
        cmd.Parameters.AddWithValue("$device_id", deviceId.ToString());
        cmd.Parameters.AddWithValue("$hostname", hostname);
        cmd.Parameters.AddWithValue("$status", status);
        cmd.Parameters.AddWithValue("$last_seen_utc", (object?)lastSeenUtc?.UtcDateTime.ToString("O") ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<(Guid DeviceId, string Hostname, string Status, DateTimeOffset? LastSeenUtc)> ListDevices(string? q, string? status)
    {
        EnsureInitialized();
        using var conn = Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT device_id, hostname, status, last_seen_utc
            FROM devices
            WHERE ($q IS NULL OR hostname LIKE '%' || $q || '%')
              AND ($status IS NULL OR status = $status)
            ORDER BY hostname;
            """;
        cmd.Parameters.AddWithValue("$q", (object?)q ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$status", (object?)status ?? DBNull.Value);

        var list = new List<(Guid, string, string, DateTimeOffset?)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add((
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : DateTimeOffset.Parse(reader.GetString(3))));
        }

        return list;
    }

    public void BindCertificate(Guid deviceId, string thumbprintSha256)
    {
        EnsureInitialized();
        using var conn = Open();
        var now = DateTimeOffset.UtcNow;
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO device_certificates(thumbprint_sha256, device_id, bound_at_utc, revoked, revoked_at_utc, revocation_reason)
            VALUES($thumbprint_sha256, $device_id, $bound_at_utc, 0, NULL, NULL)
            ON CONFLICT(thumbprint_sha256) DO UPDATE SET
              device_id = excluded.device_id,
              revoked = 0,
              revoked_at_utc = NULL,
              revocation_reason = NULL;
            """;
        cmd.Parameters.AddWithValue("$thumbprint_sha256", Normalize(thumbprintSha256));
        cmd.Parameters.AddWithValue("$device_id", deviceId.ToString());
        cmd.Parameters.AddWithValue("$bound_at_utc", now.UtcDateTime.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public bool TryResolveByCertificate(string thumbprintSha256, out Guid deviceId)
    {
        EnsureInitialized();
        using var conn = Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT device_id
            FROM device_certificates
            WHERE thumbprint_sha256 = $thumbprint AND revoked = 0
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$thumbprint", Normalize(thumbprintSha256));
        var found = cmd.ExecuteScalar() as string;
        if (!string.IsNullOrWhiteSpace(found))
        {
            deviceId = Guid.Parse(found);
            return true;
        }

        deviceId = Guid.Empty;
        return false;
    }

    public bool RevokeCertificate(Guid deviceId, string thumbprintSha256, string? reason)
    {
        EnsureInitialized();
        using var conn = Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE device_certificates
            SET revoked = 1, revoked_at_utc = $revoked_at_utc, revocation_reason = $reason
            WHERE thumbprint_sha256 = $thumbprint AND device_id = $device_id;
            """;
        cmd.Parameters.AddWithValue("$thumbprint", Normalize(thumbprintSha256));
        cmd.Parameters.AddWithValue("$device_id", deviceId.ToString());
        cmd.Parameters.AddWithValue("$revoked_at_utc", DateTimeOffset.UtcNow.UtcDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("$reason", string.IsNullOrWhiteSpace(reason) ? "admin_revoked" : reason);
        var affected = cmd.ExecuteNonQuery();
        return affected > 0;
    }

    public IReadOnlyList<(Guid DeviceId, string ThumbprintSha256, bool Revoked, DateTimeOffset BoundAtUtc, DateTimeOffset? RevokedAtUtc, string? Reason)> ListCertificates(Guid deviceId)
    {
        EnsureInitialized();
        using var conn = Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT device_id, thumbprint_sha256, revoked, bound_at_utc, revoked_at_utc, revocation_reason
            FROM device_certificates
            WHERE device_id = $device_id
            ORDER BY bound_at_utc DESC;
            """;
        cmd.Parameters.AddWithValue("$device_id", deviceId.ToString());

        var list = new List<(Guid, string, bool, DateTimeOffset, DateTimeOffset?, string?)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add((
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetInt32(2) == 1,
                DateTimeOffset.Parse(reader.GetString(3)),
                reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4)),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return list;
    }

    public void SetRotationInstruction(Guid deviceId, string newThumbprintSha256, int graceSeconds)
    {
        EnsureInitialized();
        using var conn = Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO rotation_instructions(device_id, new_thumbprint_sha256, grace_seconds, created_utc)
            VALUES($device_id, $new_thumbprint_sha256, $grace_seconds, $created_utc)
            ON CONFLICT(device_id) DO UPDATE SET
              new_thumbprint_sha256 = excluded.new_thumbprint_sha256,
              grace_seconds = excluded.grace_seconds,
              created_utc = excluded.created_utc;
            """;
        cmd.Parameters.AddWithValue("$device_id", deviceId.ToString());
        cmd.Parameters.AddWithValue("$new_thumbprint_sha256", Normalize(newThumbprintSha256));
        cmd.Parameters.AddWithValue("$grace_seconds", Math.Max(10, graceSeconds));
        cmd.Parameters.AddWithValue("$created_utc", DateTimeOffset.UtcNow.UtcDateTime.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public (string NewThumbprintSha256, int GraceSeconds)? GetRotationInstruction(Guid deviceId)
    {
        EnsureInitialized();
        using var conn = Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT new_thumbprint_sha256, grace_seconds
            FROM rotation_instructions
            WHERE device_id = $device_id
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$device_id", deviceId.ToString());
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return (reader.GetString(0), reader.GetInt32(1));
    }

    public (string OldThumbprintSha256, string NewThumbprintSha256, DateTimeOffset RevokeAfterUtc) BeginRotation(Guid deviceId, string oldThumbprintSha256, string newThumbprintSha256, int graceSeconds)
    {
        EnsureInitialized();
        using var conn = Open();
        var oldNorm = Normalize(oldThumbprintSha256);
        var newNorm = Normalize(newThumbprintSha256);
        var revokeAfter = DateTimeOffset.UtcNow.AddSeconds(Math.Max(10, graceSeconds));

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO rotations(device_id, old_thumbprint_sha256, new_thumbprint_sha256, revoke_after_utc, confirmed, processed)
            VALUES($device_id, $old_thumbprint_sha256, $new_thumbprint_sha256, $revoke_after_utc, 0, 0)
            ON CONFLICT(device_id) DO UPDATE SET
              old_thumbprint_sha256 = excluded.old_thumbprint_sha256,
              new_thumbprint_sha256 = excluded.new_thumbprint_sha256,
              revoke_after_utc = excluded.revoke_after_utc,
              confirmed = 0,
              processed = 0;
            """;
        cmd.Parameters.AddWithValue("$device_id", deviceId.ToString());
        cmd.Parameters.AddWithValue("$old_thumbprint_sha256", oldNorm);
        cmd.Parameters.AddWithValue("$new_thumbprint_sha256", newNorm);
        cmd.Parameters.AddWithValue("$revoke_after_utc", revokeAfter.UtcDateTime.ToString("O"));
        cmd.ExecuteNonQuery();

        return (oldNorm, newNorm, revokeAfter);
    }

    public bool ConfirmRotation(Guid deviceId, string oldThumbprintSha256, string newThumbprintSha256)
    {
        EnsureInitialized();
        using var conn = Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE rotations
            SET confirmed = 1
            WHERE device_id = $device_id
              AND old_thumbprint_sha256 = $old_thumbprint
              AND new_thumbprint_sha256 = $new_thumbprint;
            """;
        cmd.Parameters.AddWithValue("$device_id", deviceId.ToString());
        cmd.Parameters.AddWithValue("$old_thumbprint", Normalize(oldThumbprintSha256));
        cmd.Parameters.AddWithValue("$new_thumbprint", Normalize(newThumbprintSha256));
        return cmd.ExecuteNonQuery() > 0;
    }

    public IReadOnlyList<(Guid DeviceId, string OldThumbprintSha256, DateTimeOffset RevokeAfterUtc)> GetDueConfirmedRotations(DateTimeOffset nowUtc)
    {
        EnsureInitialized();
        using var conn = Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT device_id, old_thumbprint_sha256, revoke_after_utc
            FROM rotations
            WHERE confirmed = 1 AND processed = 0 AND revoke_after_utc <= $now;
            """;
        cmd.Parameters.AddWithValue("$now", nowUtc.UtcDateTime.ToString("O"));
        var list = new List<(Guid, string, DateTimeOffset)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add((Guid.Parse(reader.GetString(0)), reader.GetString(1), DateTimeOffset.Parse(reader.GetString(2))));
        }

        return list;
    }

    public void MarkRotationProcessed(Guid deviceId)
    {
        EnsureInitialized();
        using var conn = Open();

        var mark = conn.CreateCommand();
        mark.CommandText = "UPDATE rotations SET processed = 1 WHERE device_id = $device_id";
        mark.Parameters.AddWithValue("$device_id", deviceId.ToString());
        mark.ExecuteNonQuery();

        var clear = conn.CreateCommand();
        clear.CommandText = "DELETE FROM rotation_instructions WHERE device_id = $device_id";
        clear.Parameters.AddWithValue("$device_id", deviceId.ToString());
        clear.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _initGate.Wait();
        try
        {
            if (_initialized)
            {
                return;
            }

            using var conn = Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = """
                PRAGMA journal_mode=WAL;

                CREATE TABLE IF NOT EXISTS fingerprints (
                  fingerprint TEXT PRIMARY KEY,
                  device_id TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS devices (
                  device_id TEXT PRIMARY KEY,
                  hostname TEXT NOT NULL,
                  status TEXT NOT NULL,
                  last_seen_utc TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS device_certificates (
                  thumbprint_sha256 TEXT PRIMARY KEY,
                  device_id TEXT NOT NULL,
                  bound_at_utc TEXT NOT NULL,
                  revoked INTEGER NOT NULL DEFAULT 0,
                  revoked_at_utc TEXT NULL,
                  revocation_reason TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS rotation_instructions (
                  device_id TEXT PRIMARY KEY,
                  new_thumbprint_sha256 TEXT NOT NULL,
                  grace_seconds INTEGER NOT NULL,
                  created_utc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS rotations (
                  device_id TEXT PRIMARY KEY,
                  old_thumbprint_sha256 TEXT NOT NULL,
                  new_thumbprint_sha256 TEXT NOT NULL,
                  revoke_after_utc TEXT NOT NULL,
                  confirmed INTEGER NOT NULL DEFAULT 0,
                  processed INTEGER NOT NULL DEFAULT 0
                );
                """;
            cmd.ExecuteNonQuery();
            _initialized = true;
        }
        finally
        {
            _initGate.Release();
        }
    }

    private static string Normalize(string value)
    {
        return value.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }
}
