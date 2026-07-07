namespace Ezeeroom.KeycardBridge.Config;

/// <summary>
/// Resolves the bridge's data directory (journal db, token blob).
///
/// Production is always Windows, where CommonApplicationData is C:\ProgramData —
/// writable by the LocalSystem/service account the bridge runs as. On non-Windows dev
/// machines CommonApplicationData maps to /usr/share, which requires root, so local
/// runs fall back to LocalApplicationData (the current user's own writable folder).
/// Dev/stub-mode only — mirrors the DPAPI dev fallback in TokenStore.
/// </summary>
public static class AppPaths
{
    public static string DataDirectory
    {
        get
        {
            var root = Environment.GetFolderPath(OperatingSystem.IsWindows()
                ? Environment.SpecialFolder.CommonApplicationData
                : Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "EzeeroomKeycardBridge");
        }
    }
}
