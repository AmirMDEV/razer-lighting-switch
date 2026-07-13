namespace RazerLightingSwitch;

internal static class AppPaths
{
    internal static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Amir", "RazerLightingSwitch");
    internal static readonly string LogPath = Path.Combine(DataDirectory, "controller.log");
    internal static readonly string SettingsPath = Path.Combine(DataDirectory, "settings.json");

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
