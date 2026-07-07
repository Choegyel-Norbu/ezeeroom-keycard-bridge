using Ezeeroom.KeycardBridge.Config;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Ezeeroom.KeycardBridge.Journal;

/// <summary>
/// Append-only write-ahead journal (guide §1.3). Every intent/result is written here
/// BEFORE and AFTER each DLL call; a background worker syncs rows to ezeeroom-api.
/// This file — not the browser's fast-path report — is the system of record.
/// Rows are never updated except to stamp synced_at, and never deleted.
/// </summary>
public sealed class JournalStore
{
    private readonly string _connectionString;

    public JournalStore(IOptions<BridgeOptions> options)
    {
        var path = options.Value.JournalPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "EzeeroomKeycardBridge");
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, "journal.db");
        }

        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        Initialize();
    }

    private void Initialize()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS journal_event (
              id           INTEGER PRIMARY KEY AUTOINCREMENT,
              event_uuid   TEXT NOT NULL UNIQUE,
              event_type   TEXT NOT NULL,
              booking_ref  TEXT NULL,
              lock_room_no TEXT NULL,
              card_ref     TEXT NULL,
              valid_from   TEXT NULL,
              valid_until  TEXT NULL,
              operator     TEXT NULL,
              detail       TEXT NULL,
              created_at   TEXT NOT NULL,
              synced_at    TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_journal_unsynced
              ON journal_event (id) WHERE synced_at IS NULL;
            """;
        cmd.ExecuteNonQuery();
    }

    public Guid Append(
        string eventType,
        string? bookingRef = null,
        string? lockRoomNo = null,
        string? cardRef = null,
        DateTime? validFrom = null,
        DateTime? validUntil = null,
        string? operatorName = null,
        string? detail = null)
    {
        var uuid = Guid.NewGuid();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO journal_event
              (event_uuid, event_type, booking_ref, lock_room_no, card_ref,
               valid_from, valid_until, operator, detail, created_at)
            VALUES ($uuid, $type, $booking, $lock, $card, $from, $until, $op, $detail, $created)
            """;
        cmd.Parameters.AddWithValue("$uuid", uuid.ToString());
        cmd.Parameters.AddWithValue("$type", eventType);
        cmd.Parameters.AddWithValue("$booking", (object?)bookingRef ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$lock", (object?)lockRoomNo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$card", (object?)cardRef ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$from", (object?)validFrom?.ToString("O") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$until", (object?)validUntil?.ToString("O") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$op", (object?)operatorName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$detail", (object?)detail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
        return uuid;
    }

    public IReadOnlyList<JournalEvent> GetUnsynced(int limit)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, event_uuid, event_type, booking_ref, lock_room_no, card_ref,
                   valid_from, valid_until, operator, detail, created_at, synced_at
            FROM journal_event WHERE synced_at IS NULL ORDER BY id LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$limit", limit);

        var events = new List<JournalEvent>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            events.Add(new JournalEvent(
                Id: reader.GetInt64(0),
                EventUuid: Guid.Parse(reader.GetString(1)),
                EventType: reader.GetString(2),
                BookingRef: reader.IsDBNull(3) ? null : reader.GetString(3),
                LockRoomNo: reader.IsDBNull(4) ? null : reader.GetString(4),
                CardRef: reader.IsDBNull(5) ? null : reader.GetString(5),
                ValidFrom: reader.IsDBNull(6) ? null : reader.GetString(6),
                ValidUntil: reader.IsDBNull(7) ? null : reader.GetString(7),
                Operator: reader.IsDBNull(8) ? null : reader.GetString(8),
                Detail: reader.IsDBNull(9) ? null : reader.GetString(9),
                CreatedAt: reader.GetString(10),
                SyncedAt: reader.IsDBNull(11) ? null : reader.GetString(11)));
        }
        return events;
    }

    public void MarkSynced(Guid eventUuid)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "UPDATE journal_event SET synced_at = $now WHERE event_uuid = $uuid AND synced_at IS NULL";
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$uuid", eventUuid.ToString());
        cmd.ExecuteNonQuery();
    }

    public long CountPending()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM journal_event WHERE synced_at IS NULL";
        return (long)cmd.ExecuteScalar()!;
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
