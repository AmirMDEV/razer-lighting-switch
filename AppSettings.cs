using System.Text.Json;

namespace RazerLightingSwitch;

internal sealed class AppSettings
{
    public string LastMode { get; set; } = "white";
    public int ColorArgb { get; set; } = Color.White.ToArgb();
    public int Brightness { get; set; } = 100;
    public bool StartWithWindows { get; set; } = true;

    internal Color BaseColor => Color.FromArgb(ColorArgb);

    internal static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsPath)) ?? new();
        }
        catch (Exception ex) { AppPaths.Log($"Settings load failed: {ex.Message}"); }
        return new();
    }

    internal void Save()
    {
        try
        {
            File.WriteAllText(AppPaths.SettingsPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { AppPaths.Log($"Settings save failed: {ex.Message}"); }
    }
}
