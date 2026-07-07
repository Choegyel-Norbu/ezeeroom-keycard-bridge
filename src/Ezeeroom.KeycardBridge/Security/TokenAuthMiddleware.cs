using System.Security.Cryptography;
using System.Text;

namespace Ezeeroom.KeycardBridge.Security;

/// <summary>
/// Requires X-Bridge-Token on every request except CORS preflights (guide §1.4).
/// The token is DPAPI-protected at rest (see TokenStore) and compared in constant time.
/// No token provisioned yet → 503, so a half-installed bridge fails closed and loudly.
/// </summary>
public sealed class TokenAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        var expected = TokenStore.Load();
        if (expected is null)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "TOKEN_NOT_PROVISIONED",
                message = "Run: EzeeroomKeycardBridge.exe set-token <token>"
            });
            return;
        }

        var presented = Encoding.UTF8.GetBytes(context.Request.Headers["X-Bridge-Token"].ToString());
        if (!CryptographicOperations.FixedTimeEquals(presented, expected))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "INVALID_TOKEN" });
            return;
        }

        await next(context);
    }
}
