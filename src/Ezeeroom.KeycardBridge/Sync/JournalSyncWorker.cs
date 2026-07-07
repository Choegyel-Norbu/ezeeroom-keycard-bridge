using System.Net;
using Ezeeroom.KeycardBridge.Config;
using Ezeeroom.KeycardBridge.Journal;
using Microsoft.Extensions.Options;

namespace Ezeeroom.KeycardBridge.Sync;

/// <summary>
/// Background worker pushing journal events to ezeeroom-api (guide §1.3).
/// The browser also reports success (fast path for immediate UI), but THIS sync is the
/// system of record: the API deduplicates by event UUID, so a card can never exist
/// without eventually having a cloud record. Internet down → events queue locally and
/// check-in is unaffected; retry with exponential backoff, forever.
/// </summary>
public sealed class JournalSyncWorker(
    JournalStore journal,
    IHttpClientFactory httpClientFactory,
    IOptions<BridgeOptions> options,
    ILogger<JournalSyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.Api.BaseUrl))
        {
            logger.LogWarning("Bridge:Api:BaseUrl not configured — journal sync disabled. " +
                              "Events will accumulate locally until it is set.");
            return;
        }

        var baseDelay = TimeSpan.FromSeconds(opts.Api.SyncIntervalSeconds);
        var delay = baseDelay;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SyncBatchAsync(opts, ct);
                delay = baseDelay;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Journal sync failed; retrying in {Delay}", delay);
                delay = delay * 2 > MaxBackoff ? MaxBackoff : delay * 2;
            }

            await Task.Delay(delay, ct);
        }
    }

    private async Task SyncBatchAsync(BridgeOptions opts, CancellationToken ct)
    {
        var batch = journal.GetUnsynced(50);
        if (batch.Count == 0) return;

        var client = httpClientFactory.CreateClient(nameof(JournalSyncWorker));
        // TODO: bridge→api authentication (service credential) — the API contract for
        // POST /api/v1/keycards/events auth is not yet defined in the implementation guide.

        foreach (var e in batch)
        {
            var response = await client.PostAsJsonAsync(
                $"{opts.Api.BaseUrl.TrimEnd('/')}/api/v1/keycards/events",
                new
                {
                    eventUuid = e.EventUuid,
                    hotelId = opts.Api.HotelId,
                    eventType = e.EventType,
                    bookingRef = e.BookingRef,
                    lockRoomNo = e.LockRoomNo,
                    cardRef = e.CardRef,
                    validFrom = e.ValidFrom,
                    validUntil = e.ValidUntil,
                    @operator = e.Operator,
                    source = "BRIDGE",
                    detail = e.Detail,
                    occurredAt = e.CreatedAt,
                },
                ct);

            // 2xx = accepted; 409 = the browser fast-path already delivered it. Both are synced.
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict)
            {
                journal.MarkSynced(e.EventUuid);
            }
            else
            {
                throw new HttpRequestException(
                    $"API rejected event {e.EventUuid} with {(int)response.StatusCode}; will retry.");
            }
        }

        logger.LogInformation("Synced {Count} journal event(s); {Pending} pending",
            batch.Count, journal.CountPending());
    }
}
