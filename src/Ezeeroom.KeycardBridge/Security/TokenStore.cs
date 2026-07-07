using System.Security.Cryptography;
using System.Text;

namespace Ezeeroom.KeycardBridge.Security;

/// <summary>
/// Stores the shared X-Bridge-Token via Windows DPAPI (guide §1.4: "not plaintext config").
///
/// Scope is LocalMachine, NOT CurrentUser: the bridge runs as a Windows service
/// (LocalSystem / service account) while the token is provisioned from an installer or
/// admin shell running as the interactive user — user-scoped DPAPI keys are not portable
/// across those accounts. Trade-off: any process on the machine can unprotect the blob,
/// so the installer must also restrict the file ACL to the service account + admins
/// (TODO: installer step).
///
/// Non-Windows (dev on macOS/Linux, stub mode only): falls back to a plaintext file.
/// </summary>
public static class TokenStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ezeeroom-keycard-bridge/v1");

    private static string TokenPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "EzeeroomKeycardBridge", "token.bin");

    public static void Save(string token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(TokenPath)!);
        var plain = Encoding.UTF8.GetBytes(token);

        if (OperatingSystem.IsWindows())
        {
            File.WriteAllBytes(TokenPath,
                ProtectedData.Protect(plain, Entropy, DataProtectionScope.LocalMachine));
        }
        else
        {
            File.WriteAllBytes(TokenPath, plain); // dev fallback — stub mode only
        }
    }

    /// <summary>Null if no token has been provisioned yet (bridge refuses requests with 503).</summary>
    public static byte[]? Load()
    {
        if (!File.Exists(TokenPath)) return null;
        var stored = File.ReadAllBytes(TokenPath);

        return OperatingSystem.IsWindows()
            ? ProtectedData.Unprotect(stored, Entropy, DataProtectionScope.LocalMachine)
            : stored;
    }
}
