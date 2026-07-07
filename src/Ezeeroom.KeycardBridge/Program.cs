using System.Net;
using Ezeeroom.KeycardBridge.Api;
using Ezeeroom.KeycardBridge.Config;
using Ezeeroom.KeycardBridge.Encoder;
using Ezeeroom.KeycardBridge.Encoder.ProRfl;
using Ezeeroom.KeycardBridge.Journal;
using Ezeeroom.KeycardBridge.Security;
using Ezeeroom.KeycardBridge.Services;
using Ezeeroom.KeycardBridge.Sync;
using Microsoft.Extensions.Options;

// Provisioning verb used by the installer / admin shell:
//   EzeeroomKeycardBridge.exe set-token <token>
if (args is ["set-token", var token])
{
    TokenStore.Save(token);
    Console.WriteLine("Bridge token stored.");
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService(o => o.ServiceName = "EzeeroomKeycardBridge");

builder.Services.Configure<BridgeOptions>(builder.Configuration.GetSection(BridgeOptions.SectionName));
var bridgeOptions = builder.Configuration.GetSection(BridgeOptions.SectionName).Get<BridgeOptions>()
                    ?? new BridgeOptions();

// The bridge can mint room keys — 127.0.0.1 only, never 0.0.0.0 (guide §1.4).
builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, bridgeOptions.Port));

builder.Services.AddSingleton<IEncoder>(sp => bridgeOptions.EncoderMode.ToLowerInvariant() switch
{
    "prorfl" => new ProRflEncoder(sp.GetRequiredService<IOptions<BridgeOptions>>()),
    _ => new StubEncoder(),
});
builder.Services.AddSingleton<EncoderGate>();
builder.Services.AddSingleton<JournalStore>();
builder.Services.AddSingleton<IssueRateLimiter>();
builder.Services.AddSingleton<CardOperations>();
builder.Services.AddHttpClient(nameof(JournalSyncWorker));
builder.Services.AddHostedService<JournalSyncWorker>();

var app = builder.Build();

app.UseMiddleware<CorsPrivateNetworkMiddleware>();
app.UseMiddleware<TokenAuthMiddleware>();
app.MapBridgeEndpoints();

app.Logger.LogInformation(
    "ezeeroom-keycard-bridge listening on http://127.0.0.1:{Port} (encoder mode: {Mode})",
    bridgeOptions.Port, bridgeOptions.EncoderMode);

app.Run();
