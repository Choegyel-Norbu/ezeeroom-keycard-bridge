namespace Ezeeroom.KeycardBridge.Config;

public sealed class BridgeOptions
{
    public const string SectionName = "Bridge";

    /// <summary>Localhost port the bridge listens on. Never exposed beyond 127.0.0.1.</summary>
    public int Port { get; set; } = 17800;

    /// <summary>
    /// Exact ezeeroom-web origin allowed to call this bridge, e.g. "https://app.ezeeroom.bt".
    /// Requests with any other Origin header are rejected.
    /// </summary>
    public string AllowedOrigin { get; set; } = "";

    /// <summary>"stub" (default, no hardware) or "prorfl" (real encoder — Gate 1/2 must have passed).</summary>
    public string EncoderMode { get; set; } = "stub";

    /// <summary>Folder containing proRFL.dll and its companion DLLs (d12.dll etc.).</summary>
    public string DllDirectory { get; set; } = @"C:\ProUSB\SDK";

    /// <summary>ProUSB hotel ID as registered in the vendor software. Fill during Gate 2.</summary>
    public int ProUsbHotelId { get; set; }

    /// <summary>SQLite journal path. Empty = ProgramData\EzeeroomKeycardBridge\journal.db.</summary>
    public string JournalPath { get; set; } = "";

    /// <summary>Rate limit for POST /v1/cards/issue (guide §1.4).</summary>
    public int MaxIssuesPerMinute { get; set; } = 6;

    public ApiOptions Api { get; set; } = new();

    public sealed class ApiOptions
    {
        /// <summary>ezeeroom-api base URL, e.g. "https://api.ezeeroom.bt". Empty disables journal sync.</summary>
        public string BaseUrl { get; set; } = "";

        /// <summary>ezeeroom hotel id (cloud side), attached to every synced event.</summary>
        public long HotelId { get; set; }

        /// <summary>Base poll interval for the journal sync worker.</summary>
        public int SyncIntervalSeconds { get; set; } = 15;
    }
}
