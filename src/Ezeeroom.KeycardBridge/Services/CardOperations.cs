using System.Text.Json;
using Ezeeroom.KeycardBridge.Api;
using Ezeeroom.KeycardBridge.Encoder;
using Ezeeroom.KeycardBridge.Journal;

namespace Ezeeroom.KeycardBridge.Services;

/// <summary>
/// Orchestrates every card operation with the invariants from the design principles:
///  - journal BEFORE and AFTER each DLL call (§1.3 — the journal is the system of record);
///  - a card never leaves the desk unverified: every write is followed by a mandatory
///    ReadCard whose room + expiry must match the request (§1.2);
///  - all encoder access is serialized through EncoderGate.
/// </summary>
public sealed class CardOperations(
    IEncoder encoder,
    EncoderGate gate,
    JournalStore journal,
    ILogger<CardOperations> logger)
{
    public async Task<IResult> IssueAsync(IssueCardRequest request, CancellationToken ct)
    {
        return await gate.RunAsync<IResult>(() =>
        {
            journal.Append(JournalEvent.Types.IssueIntent,
                request.BookingRef, request.LockRoomNo, null,
                request.ValidFrom, request.ValidUntil, request.Operator);

            CardInfo? readBack;
            try
            {
                encoder.WriteGuestCard(request.LockRoomNo, request.ValidFrom, request.ValidUntil);
                readBack = encoder.ReadCard();
            }
            catch (EncoderException ex)
            {
                journal.Append(JournalEvent.Types.IssueFailed,
                    request.BookingRef, request.LockRoomNo, null,
                    request.ValidFrom, request.ValidUntil, request.Operator,
                    JsonSerializer.Serialize(new { code = ex.Code, message = ex.Message }));
                logger.LogWarning("Issue failed for room {Room}: {Code}", request.LockRoomNo, ex.Code);
                return EncoderError(ex);
            }

            // Mandatory verify: the write only counts if the card reads back correctly.
            if (readBack is null
                || readBack.LockRoomNo != request.LockRoomNo
                || readBack.ValidUntil != request.ValidUntil)
            {
                journal.Append(JournalEvent.Types.IssueFailed,
                    request.BookingRef, request.LockRoomNo, readBack?.CardRef,
                    request.ValidFrom, request.ValidUntil, request.Operator,
                    JsonSerializer.Serialize(new
                    {
                        code = "VERIFY_MISMATCH",
                        readRoom = readBack?.LockRoomNo,
                        readValidUntil = readBack?.ValidUntil,
                    }));
                return Results.UnprocessableEntity(new
                {
                    error = "VERIFY_MISMATCH",
                    message = "Card read-back did not match the request. " +
                              "Re-place the card on the encoder and retry.",
                });
            }

            var eventUuid = journal.Append(JournalEvent.Types.Issued,
                request.BookingRef, request.LockRoomNo, readBack.CardRef,
                request.ValidFrom, request.ValidUntil, request.Operator);

            return Results.Ok(new
            {
                eventUuid,
                cardData = readBack.CardRef,
                lockRoomNo = readBack.LockRoomNo,
                validUntil = readBack.ValidUntil,
                verified = true,
            });
        }, ct);
    }

    public async Task<IResult> ReadAsync(string? operatorName, CancellationToken ct)
    {
        return await gate.RunAsync<IResult>(() =>
        {
            CardInfo? card;
            try
            {
                card = encoder.ReadCard();
            }
            catch (EncoderException ex)
            {
                return EncoderError(ex);
            }

            journal.Append(JournalEvent.Types.Read,
                lockRoomNo: card?.LockRoomNo, cardRef: card?.CardRef,
                operatorName: operatorName,
                detail: card is null ? "blank or no card" : null);

            return card is null
                ? Results.Ok(new { card = (object?)null, message = "No card / blank card on encoder." })
                : Results.Ok(new
                {
                    card = new
                    {
                        lockRoomNo = card.LockRoomNo,
                        validUntil = card.ValidUntil,
                        cardType = card.CardType,
                        cardRef = card.CardRef,
                    }
                });
        }, ct);
    }

    public async Task<IResult> EraseAsync(string? operatorName, CancellationToken ct)
    {
        return await gate.RunAsync<IResult>(() =>
        {
            try
            {
                // Read first — capture what is being erased (guide §1.2).
                var before = encoder.ReadCard();
                journal.Append(JournalEvent.Types.EraseAttempted,
                    lockRoomNo: before?.LockRoomNo, cardRef: before?.CardRef,
                    operatorName: operatorName,
                    detail: before is null ? "card already blank" : null);

                encoder.EraseCard();

                // Confirm: a successful erase reads back blank.
                var after = encoder.ReadCard();
                if (after is not null)
                {
                    return Results.UnprocessableEntity(new
                    {
                        error = "ERASE_NOT_CONFIRMED",
                        message = "Card still readable after erase. Re-place the card and retry.",
                    });
                }

                var eventUuid = journal.Append(JournalEvent.Types.Erased,
                    lockRoomNo: before?.LockRoomNo, cardRef: before?.CardRef,
                    operatorName: operatorName);

                return Results.Ok(new
                {
                    eventUuid,
                    erased = new { lockRoomNo = before?.LockRoomNo, cardRef = before?.CardRef },
                });
            }
            catch (EncoderException ex)
            {
                return EncoderError(ex);
            }
        }, ct);
    }

    private static IResult EncoderError(EncoderException ex) =>
        Results.Json(
            new { error = ex.Code, message = ex.Message },
            statusCode: ex.Code == EncoderException.Codes.Disconnected
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status422UnprocessableEntity);
}
