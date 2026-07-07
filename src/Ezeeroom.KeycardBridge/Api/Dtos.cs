using System.ComponentModel.DataAnnotations;

namespace Ezeeroom.KeycardBridge.Api;

/// <summary>POST /v1/cards/issue — guest cards only, ever (guide §1.4).</summary>
public sealed record IssueCardRequest(
    [property: Required] string LockRoomNo,
    [property: Required] DateTime ValidFrom,
    [property: Required] DateTime ValidUntil,
    string? BookingRef,
    [property: Required] string Operator);

/// <summary>POST /v1/cards/read and /v1/cards/erase.</summary>
public sealed record CardActionRequest(string? Operator);
