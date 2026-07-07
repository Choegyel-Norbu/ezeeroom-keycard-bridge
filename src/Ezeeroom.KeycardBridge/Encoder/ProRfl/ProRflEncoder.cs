using Ezeeroom.KeycardBridge.Config;
using Microsoft.Extensions.Options;

namespace Ezeeroom.KeycardBridge.Encoder.ProRfl;

/// <summary>
/// Real-hardware encoder wrapping proRFL.dll.
///
/// ⚠️ SCAFFOLD — NOT VALIDATED. Gates 1 and 2 (KEYCARD_IMPLEMENTATION_GUIDE.md, Phase 0)
/// have not been run: DLL signatures are provisional and the card room/time encoding is
/// unknown. Every card-touching method throws NOT_IMPLEMENTED_PENDING_GATE2 until the
/// Gate 2 field reference (§4) is filled in and this class is completed against it.
/// EncoderMode stays "stub" until then.
/// </summary>
public sealed class ProRflEncoder : IEncoder
{
    private readonly BridgeOptions _options;
    private volatile bool _initialized;
    private string _dllVersion = "unknown";

    public ProRflEncoder(IOptions<BridgeOptions> options)
    {
        _options = options.Value;

        if (!OperatingSystem.IsWindows())
            throw new InvalidOperationException("proRFL.dll requires Windows (x86).");

        if (Environment.Is64BitProcess)
            throw new InvalidOperationException(
                "proRFL.dll is 32-bit but this process is 64-bit. Publish/run as win-x86.");

        if (!Directory.Exists(_options.DllDirectory))
            throw new InvalidOperationException(
                $"Bridge:DllDirectory '{_options.DllDirectory}' does not exist. " +
                "It must contain proRFL.dll and its companion DLLs (d12.dll, ...).");

        // Companion DLLs are resolved relative to the working directory (guide §1.1).
        Directory.SetCurrentDirectory(_options.DllDirectory);
    }

    public EncoderStatus GetStatus() => new(_initialized, _dllVersion, "prorfl");

    public void WriteGuestCard(string lockRoomNo, DateTime validFrom, DateTime validUntil)
        => throw PendingGate2();

    public CardInfo? ReadCard() => throw PendingGate2();

    public void EraseCard() => throw PendingGate2();

    private static EncoderException PendingGate2() => new(
        EncoderException.Codes.NotValidated,
        "ProRflEncoder is a scaffold: complete Gate 1/Gate 2, fill the field reference in " +
        "KEYCARD_IMPLEMENTATION_GUIDE.md §4, then implement this class against the confirmed " +
        "DLL signatures. Use EncoderMode=stub until then.");
}
