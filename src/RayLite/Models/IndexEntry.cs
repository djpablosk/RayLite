namespace RayLite.Models;

public enum EntryKind : byte
{
    Application,
    UwpApp,
    Folder,
    File
}

/// <summary>One searchable item. For UWP apps <see cref="Path"/> holds the AppUserModelID.</summary>
public sealed record IndexEntry(string Name, string Path, EntryKind Kind);
