using System.IO;
using System.Text.Json;
using RayLite.Models;

namespace RayLite.Services;

public static class SettingsService
{
    public static string FolderPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RayLite");

    public static string FilePath { get; } = Path.Combine(FolderPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                using FileStream stream = File.OpenRead(FilePath);
                AppSettings? loaded = JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.AppSettings);
                if (loaded != null)
                {
                    loaded.Normalize();
                    return loaded;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            App.LogError(ex);
        }

        var defaults = new AppSettings();
        try { Save(defaults); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return defaults;
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(FolderPath);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings));
        File.Move(temp, FilePath, overwrite: true);
    }
}
