using System.Security.Cryptography;
using System.Text;

namespace Ezeeroom.KeycardBridge.Encoder;

/// <summary>
/// No-hardware encoder for development and the Gate 2 browser-path test
/// (guide Phase 0: "deploy a static stub bridge ... and prove the deployed
/// HTTPS ezeeroom-web → http://127.0.0.1 call works").
///
/// Behaves like an encoder that always has a card on its induction zone:
/// writes succeed, ReadCard returns whatever was last written (null if blank),
/// EraseCard blanks it.
/// </summary>
public sealed class StubEncoder : IEncoder
{
    private readonly object _lock = new();
    private CardInfo? _card;

    public EncoderStatus GetStatus() => new(Connected: true, DllVersion: "stub-0.1", Mode: "stub");

    public void WriteGuestCard(string lockRoomNo, DateTime validFrom, DateTime validUntil)
    {
        lock (_lock)
        {
            var raw = $"STUB|{lockRoomNo}|{validFrom:O}|{validUntil:O}";
            _card = new CardInfo(
                CardRef: Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16],
                CardType: "GUEST",
                LockRoomNo: lockRoomNo,
                ValidUntil: validUntil,
                Raw: raw);
        }
    }

    public CardInfo? ReadCard()
    {
        lock (_lock) return _card;
    }

    public void EraseCard()
    {
        lock (_lock) _card = null;
    }
}
