using System.Text.Json.Serialization;

namespace RayLite.Models;

public sealed class AppSettings
{
    public string Hotkey { get; set; } = "Ctrl+Space";
    public bool RunOnStartup { get; set; }

    /// <summary>Ignore the global hotkey while a fullscreen app/game/video is in the foreground.</summary>
    public bool IgnoreFullscreen { get; set; } = true;

    /// <summary>Index Documents, Desktop and Downloads automatically.</summary>
    public bool IndexDefaultFolders { get; set; } = true;

    public List<string> IncludedFolders { get; set; } = new();

    /// <summary>Entries with a path separator are treated as full paths; others as folder names.</summary>
    public List<string> ExcludedFolders { get; set; } = new() { "node_modules", ".git", ".vs", "__pycache__" };

    /// <summary>Hard cap on indexed files/folders; keeps memory use predictable.</summary>
    public int MaxIndexedFiles { get; set; } = 15000;

    public int MaxDepth { get; set; } = 6;

    public AppSettings Clone() => new()
    {
        Hotkey = Hotkey,
        RunOnStartup = RunOnStartup,
        IgnoreFullscreen = IgnoreFullscreen,
        IndexDefaultFolders = IndexDefaultFolders,
        IncludedFolders = new List<string>(IncludedFolders),
        ExcludedFolders = new List<string>(ExcludedFolders),
        MaxIndexedFiles = MaxIndexedFiles,
        MaxDepth = MaxDepth
    };

    public void Normalize()
    {
        Hotkey = string.IsNullOrWhiteSpace(Hotkey) ? "Ctrl+Space" : Hotkey.Trim();
        IncludedFolders ??= new List<string>();
        ExcludedFolders ??= new List<string>();
        MaxIndexedFiles = Math.Clamp(MaxIndexedFiles, 1000, 200000);
        MaxDepth = Math.Clamp(MaxDepth, 1, 12);
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SettingsJsonContext : JsonSerializerContext
{
}
