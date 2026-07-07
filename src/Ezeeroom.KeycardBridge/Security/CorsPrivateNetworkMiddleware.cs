using Ezeeroom.KeycardBridge.Config;
using Microsoft.Extensions.Options;

namespace Ezeeroom.KeycardBridge.Security;

/// <summary>
/// CORS + Chrome Private Network Access handling (guide §1.4):
/// the deployed HTTPS ezeeroom-web page calls this plain-HTTP localhost service, which
/// Chrome treats as a private-network request — the preflight must be answered with
/// Access-Control-Allow-Private-Network: true or the browser blocks the call.
/// The Origin header must EXACTLY match the single configured ezeeroom origin.
/// </summary>
public sealed class CorsPrivateNetworkMiddleware(RequestDelegate next, IOptions<BridgeOptions> options)
{
    private readonly string _allowedOrigin = options.Value.AllowedOrigin;

    public async Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();

        if (HttpMethods.IsOptions(context.Request.Method))
        {
            if (origin.Length > 0 && origin == _allowedOrigin)
            {
                var headers = context.Response.Headers;
                headers["Access-Control-Allow-Origin"] = origin;
                headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
                headers["Access-Control-Allow-Headers"] = "Content-Type, X-Bridge-Token";
                headers["Access-Control-Max-Age"] = "600";
                headers["Vary"] = "Origin";
                if (context.Request.Headers["Access-Control-Request-Private-Network"] == "true")
                    headers["Access-Control-Allow-Private-Network"] = "true";
                context.Response.StatusCode = StatusCodes.Status204NoContent;
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
            }
            return;
        }

        if (origin.Length > 0)
        {
            if (origin != _allowedOrigin)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "ORIGIN_NOT_ALLOWED" });
                return;
            }
            context.Response.Headers["Access-Control-Allow-Origin"] = origin;
            context.Response.Headers["Vary"] = "Origin";
        }

        await next(context);
    }
}
