PRAGMA journal_mode=WAL;

CREATE TABLE IF NOT EXISTS sessions (
  session_id TEXT PRIMARY KEY,
  device_id TEXT NOT NULL,
  policy_id TEXT NOT NULL,
  started_utc TEXT NOT NULL,
  ended_utc TEXT NULL,
  state INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS chunk_queue (
  chunk_id TEXT PRIMARY KEY,
  session_id TEXT NOT NULL,
  segment_index INTEGER NOT NULL,
  sha256_hex TEXT NOT NULL,
  bytes INTEGER NOT NULL,
  capture_start_utc TEXT NOT NULL,
  capture_end_utc TEXT NOT NULL,
  encrypted_path TEXT NOT NULL,
  status INTEGER NOT NULL,
  attempts INTEGER NOT NULL DEFAULT 0,
  next_retry_utc TEXT NULL,
  lease_until_utc TEXT NULL,
  last_error TEXT NULL,
  created_utc TEXT NOT NULL,
  FOREIGN KEY(session_id) REFERENCES sessions(session_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_chunk_session_segment
  ON chunk_queue(session_id, segment_index);

CREATE INDEX IF NOT EXISTS ix_chunk_status_retry
  ON chunk_queue(status, next_retry_utc);
