using System.Collections.ObjectModel;
using System.Windows.Input;
using RayLite.Models;
using RayLite.Services;

namespace RayLite.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly Func<AppSettings, string?> _apply;
    private readonly Func<string?> _pickFolder;
    private readonly Action _close;
    private readonly int _maxDepth;

    private string _hotkey;
    private bool _runOnStartup;
    private bool _indexDefaultFolders;
    private int _maxIndexedFiles;
    private string _newExclusion = string.Empty;
    private string? _selectedIncluded;
    private string? _selectedExcluded;
    private string _status = string.Empty;

    public SettingsViewModel(AppSettings current, Func<AppSettings, string?> apply, Func<string?> pickFolder, Action close)
    {
        _apply = apply;
        _pickFolder = pickFolder;
        _close = close;
        _maxDepth = current.MaxDepth;

        _hotkey = current.Hotkey;
        _runOnStartup = current.RunOnStartup;
        _indexDefaultFolders = current.IndexDefaultFolders;
        _maxIndexedFiles = current.MaxIndexedFiles;
        IncludedFolders = new ObservableCollection<string>(current.IncludedFolders);
        ExcludedFolders = new ObservableCollection<string>(current.ExcludedFolders);

        AddIncludedCommand = new RelayCommand(() =>
        {
            string? path = _pickFolder();
            if (!string.IsNullOrWhiteSpace(path) && !IncludedFolders.Contains(path, StringComparer.OrdinalIgnoreCase))
                IncludedFolders.Add(path);
        });
        RemoveIncludedCommand = new RelayCommand(() =>
        {
            if (SelectedIncluded != null) IncludedFolders.Remove(SelectedIncluded);
        });
        AddExclusionCommand = new RelayCommand(() =>
        {
            AddExclusion(NewExclusion);
            NewExclusion = string.Empty;
        });
        BrowseExclusionCommand = new RelayCommand(() => AddExclusion(_pickFolder()));
        RemoveExclusionCommand = new RelayCommand(() =>
        {
            if (SelectedExcluded != null) ExcludedFolders.Remove(SelectedExcluded);
        });
        ResetHotkeyCommand = new RelayCommand(() => Hotkey = "Ctrl+Space");
        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(_close);
    }

    public ObservableCollection<string> IncludedFolders { get; }
    public ObservableCollection<string> ExcludedFolders { get; }

    public ICommand AddIncludedCommand { get; }
    public ICommand RemoveIncludedCommand { get; }
    public ICommand AddExclusionCommand { get; }
    public ICommand BrowseExclusionCommand { get; }
    public ICommand RemoveExclusionCommand { get; }
    public ICommand ResetHotkeyCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public string Hotkey { get => _hotkey; set => SetProperty(ref _hotkey, value); }
    public bool RunOnStartup { get => _runOnStartup; set => SetProperty(ref _runOnStartup, value); }
    public bool IndexDefaultFolders { get => _indexDefaultFolders; set => SetProperty(ref _indexDefaultFolders, value); }
    public int MaxIndexedFiles { get => _maxIndexedFiles; set => SetProperty(ref _maxIndexedFiles, value); }
    public string NewExclusion { get => _newExclusion; set => SetProperty(ref _newExclusion, value); }
    public string? SelectedIncluded { get => _selectedIncluded; set => SetProperty(ref _selectedIncluded, value); }
    public string? SelectedExcluded { get => _selectedExcluded; set => SetProperty(ref _selectedExcluded, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }

    /// <summary>Called by the view while the hotkey box has focus.</summary>
    public void CaptureHotkey(ModifierKeys modifiers, Key key)
    {
        if (modifiers == ModifierKeys.None)
        {
            Status = "Hold at least one modifier (Alt, Ctrl, Shift or Win) while pressing the key.";
            return;
        }
        Status = string.Empty;
        Hotkey = HotKeyParser.Format(modifiers, key);
    }

    private void AddExclusion(string? value)
    {
        string text = (value ?? string.Empty).Trim();
        if (text.Length > 0 && !ExcludedFolders.Contains(text, StringComparer.OrdinalIgnoreCase))
            ExcludedFolders.Add(text);
    }

    private void Save()
    {
        if (!HotKeyParser.TryParse(Hotkey, out _, out _))
        {
            Status = "The hotkey is not valid. Click the box and press a combination such as Ctrl+Space.";
            return;
        }

        var settings = new AppSettings
        {
            Hotkey = Hotkey,
            RunOnStartup = RunOnStartup,
            IndexDefaultFolders = IndexDefaultFolders,
            IncludedFolders = IncludedFolders.ToList(),
            ExcludedFolders = ExcludedFolders.ToList(),
            MaxIndexedFiles = MaxIndexedFiles,
            MaxDepth = _maxDepth
        };
        settings.Normalize();

        string? error = _apply(settings);
        if (error != null)
        {
            Status = error;
            return;
        }
        _close();
    }
}
