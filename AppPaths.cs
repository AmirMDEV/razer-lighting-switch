namespace RazerLightingSwitch;

internal static class AppPaths
{
    internal const string ProductName = "Razor Lightweight Keyboard Lighting Control";
    internal const string FollowUrl = "https://followamir.com";
    internal const string DonateUrl = "https://www.paypal.com/donate/?hosted_button_id=2U2GXSKFJKJCA";
    internal static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Amir", "RazerLightingSwitch");
    internal static readonly string LogPath = Path.Combine(DataDirectory, "controller.log");
    internal static readonly string SettingsPath = Path.Combine(DataDirectory, "settings.json");
    internal static readonly string SuppressExternalLaunchPath = Path.Combine(DataDirectory, ".suppress-external-launch");

    internal static void EnsureCreated() => Directory.CreateDirectory(DataDirectory);

    internal static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch { }
    }
}
