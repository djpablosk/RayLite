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

    public string Detail => Entry.Kind switch
    {
        EntryKind.UwpApp => "Windows app",
        EntryKind.SystemCommand => SystemCommandService.GetSubtitle(Entry.Path),
        _ => Entry.Path
    };

    public string TypeLabel => Entry.Kind switch
    {
        EntryKind.Application => "Application",
        EntryKind.UwpApp => "App",
        EntryKind.Folder => "Folder",
        EntryKind.SystemCommand => "Command",
        _ => "File"
    };

    public ImageSource? Icon { get; }
}
