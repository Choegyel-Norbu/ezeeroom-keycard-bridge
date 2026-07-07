namespace Ezeeroom.KeycardBridge.Journal;

/// <summary>
/// One append-only journal row. Event types mirror the cloud table
/// room_key_card_event (KEYCARD_IMPLEMENTATION_GUIDE.md §2.1); event_uuid is the
/// idempotency key the API deduplicates on. Dates are stored as ISO-8601 strings.
/// </summary>
public sealed record JournalEvent(
    long Id,
    Guid EventUuid,
    string EventType,
    string? BookingRef,
    string? LockRoomNo,
    string? CardRef,
    string? ValidFrom,
    string? ValidUntil,
    string? Operator,
    string? Detail,
    string CreatedAt,
    string? SyncedAt)
{
    public static class Types
    {
        public const string IssueIntent = "ISSUE_INTENT";
        public const string Issued = "ISSUED";
        public const string IssueFailed = "ISSUE_FAILED";
        public const string Read = "READ";
        public const string EraseAttempted = "ERASE_ATTEMPTED";
        public const string Erased = "ERASED";
    }
}
