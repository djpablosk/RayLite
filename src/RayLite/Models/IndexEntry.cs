namespace RayLite.Models;

public enum EntryKind : byte
{
    Application,
    UwpApp,
    Folder,
    File,
    SystemCommand
}

/// <summary>One searchable item. For UWP apps <see cref="Path"/> holds the AppUserModelID; for system commands it holds the command id.</summary>
public sealed record IndexEntry(string Name, string Path, EntryKind Kind);
