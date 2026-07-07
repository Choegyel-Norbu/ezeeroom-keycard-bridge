using System.Reflection;
using Ezeeroom.KeycardBridge.Encoder;
using Ezeeroom.KeycardBridge.Journal;
using Ezeeroom.KeycardBridge.Security;
using Ezeeroom.KeycardBridge.Services;

namespace Ezeeroom.KeycardBridge.Api;

/// <summary>HTTP surface per guide §1.2 — versioned from day one, localhost only.</summary>
public static class Endpoints
{
    public static void MapBridgeEndpoints(this WebApplication app)
    {
        var v1 = app.MapGroup("/v1");

        // Deliberately does NOT take the encoder gate: staff must be able to see
        // "is the encoder alive?" even while a slow card write is in progress.
        v1.MapGet("/status", (IEncoder encoder, JournalStore journal) =>
        {
            var status = encoder.GetStatus();
            return Results.Ok(new
            {
                encoderConnected = status.Connected,
                dllVersion = status.DllVersion,
                mode = status.Mode,
                pendingJournalCount = journal.CountPending(),
            });
        });

        v1.MapGet("/version", (IEncoder encoder) => Results.Ok(new
        {
            bridgeVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3),
            dllVersion = encoder.GetStatus().DllVersion,
        }));

        v1.MapPost("/cards/issue", async (
            IssueCardRequest request,
            CardOperations operations,
            IssueRateLimiter rateLimiter,
            CancellationToken ct) =>
        {
            if (request.ValidUntil <= request.ValidFrom)
                return Results.BadRequest(new { error = "INVALID_VALIDITY_WINDOW" });

            if (!rateLimiter.TryAcquire())
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);

            return await operations.IssueAsync(request, ct);
        });

        v1.MapPost("/cards/read", (CardActionRequest request, CardOperations operations, CancellationToken ct)
            => operations.ReadAsync(request.Operator, ct));

        v1.MapPost("/cards/erase", (CardActionRequest request, CardOperations operations, CancellationToken ct)
            => operations.EraseAsync(request.Operator, ct));
    }
}
