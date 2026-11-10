using System.Windows.Media;
using RayLite.Models;
using RayLite.Services;

namespace RayLite.ViewModels;

public sealed class ResultItem
{
    public ResultItem(IndexEntry entry)
    {
        Entry = entry;
        Icon = IconLoader.Get(entry);
    }

    public IndexEntry Entry { get; }

    public string Name => Entry.Name;

    public string Detail => Entry.Kind == EntryKind.UwpApp ? "Windows app" : Entry.Path;

    public string TypeLabel => Entry.Kind switch
    {
        EntryKind.Application => "Application",
        EntryKind.UwpApp => "App",
        EntryKind.Folder => "Folder",
        _ => "File"
    };

    public ImageSource? Icon { get; }
}
