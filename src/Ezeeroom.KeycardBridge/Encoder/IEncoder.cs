namespace Ezeeroom.KeycardBridge.Encoder;

/// <summary>
/// Abstraction over the card encoder. Exactly two implementations:
/// <see cref="StubEncoder"/> (no hardware, Gate 2 browser-path testing and development)
/// and <see cref="ProRfl.ProRflEncoder"/> (real proRFL.dll — only after Gates 1/2 pass).
///
/// All methods are synchronous and NOT thread-safe by themselves; callers must
/// serialize access through <see cref="EncoderGate"/> (one physical device, one queue).
/// Exception: <see cref="GetStatus"/> must be cheap and non-blocking so
/// GET /v1/status never queues behind a slow card write.
/// </summary>
public interface IEncoder
{
    /// <summary>Cheap, non-blocking snapshot. Must not take the encoder mutex.</summary>
    EncoderStatus GetStatus();

    /// <summary>Writes a guest card. Throws <see cref="EncoderException"/> on failure.</summary>
    void WriteGuestCard(string lockRoomNo, DateTime validFrom, DateTime validUntil);

    /// <summary>Reads whatever card is on the encoder. Null = no card / blank card.</summary>
    CardInfo? ReadCard();

    /// <summary>Erases the card currently on the encoder.</summary>
    void EraseCard();
}

public sealed record EncoderStatus(bool Connected, string DllVersion, string Mode);

public sealed record CardInfo(
    /// <summary>Opaque card data string from the DLL; hashed to card_ref for the cloud event.</summary>
    string CardRef,
    string CardType,
    string? LockRoomNo,
    DateTime? ValidUntil,
    string? Raw);

/// <summary>Encoder failure with a stable machine-readable code the UI can map to operator instructions.</summary>
public sealed class EncoderException : Exception
{
    public string Code { get; }

    public EncoderException(string code, string message) : base(message) => Code = code;

    public static class Codes
    {
        public const string NoCard = "NO_CARD";
        public const string Disconnected = "ENCODER_DISCONNECTED";
        public const string DllError = "DLL_ERROR";
        public const string NotValidated = "NOT_IMPLEMENTED_PENDING_GATE2";
    }
}
