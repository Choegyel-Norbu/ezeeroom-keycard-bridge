using System.Runtime.InteropServices;
using System.Text;

namespace Ezeeroom.KeycardBridge.Encoder.ProRfl;

/// <summary>
/// P/Invoke declarations for the vendor proRFL.dll.
///
/// ⚠️ EVERY SIGNATURE BELOW IS PROVISIONAL — TODO(Gate 1/Gate 2).
/// The exported names are documented (proUSB_Integration_Guide.md, Appendix A) but the
/// parameter lists, calling convention, and return-code meanings are NOT. They must be
/// confirmed against the supplier's SDK docs and the pan-goofy/proUsb-py-32-dll-api
/// reference during Gate 1, and the observed error codes recorded in
/// KEYCARD_IMPLEMENTATION_GUIDE.md §4 (that table is our only error documentation).
///
/// Hard constraints (guide §1.1):
///  - proRFL.dll is 32-bit; this process MUST be x86.
///  - Companion DLLs (d12.dll, ...) must sit in the same folder as proRFL.dll,
///    and the working directory must be set to that folder before the first call
///    (see ProRflEncoder constructor).
/// </summary>
internal static partial class ProRflNative
{
    private const string Dll = "proRFL.dll";

    // TODO(Gate 1): confirm parameter meaning (port? mode?) and success return value.
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern int initializeUSB(int mode);

    // TODO(Gate 1): confirm whether version comes back as return value or out-buffer.
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern int GetDLLVersion(StringBuilder buffer, int bufferLen);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern int Buzzer(int beeps);

    // TODO(Gate 2): full parameter list unknown — room encoding, time format, hotel id,
    // and flags must be decoded from vendor-issued cards before this can be declared.
    // Intentionally NOT declared yet so nobody can call a guessed signature:
    // internal static extern int GuestCard(...);

    // TODO(Gate 1): confirm buffer size and content format of the card-data string.
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern int ReadCard(StringBuilder cardData, int bufferLen);

    // TODO(Gate 1): confirm parameters (card type? confirmation flag?).
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern int CardErase(int flag);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern int GetCardTypeByCardDataStr(string cardData);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern int GetGuestLockNoByCardDataStr(string cardData, StringBuilder lockNo, int bufferLen);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    internal static extern int GetGuestETimeByCardDataStr(string cardData, StringBuilder eTime, int bufferLen);
}
