using System.Collections.ObjectModel;
using System.Windows.Input;
using RayLite.Services;

namespace RayLite.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private const int MaxResults = 8;

    private readonly SearchEngine _engine;
    private string _query = string.Empty;
    private int _selectedIndex = -1;

    public MainViewModel(SearchEngine engine)
    {
        _engine = engine;
        MoveDownCommand = new RelayCommand(() => MoveSelection(1));
        MoveUpCommand = new RelayCommand(() => MoveSelection(-1));
        ExecuteCommand = new RelayCommand(ExecuteSelected);
        HideCommand = new RelayCommand(() => HideRequested?.Invoke());
    }

    public event Action? HideRequested;

    public ObservableCollection<ResultItem> Results { get; } = new();

    public ICommand MoveDownCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand ExecuteCommand { get; }
    public ICommand HideCommand { get; }

    public string Query
    {
        get => _query;
        set
        {
            if (SetProperty(ref _query, value)) RunSearch();
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetProperty(ref _selectedIndex, value);
    }

    public bool HasResults => Results.Count > 0;

    public bool HasNoMatches => Results.Count == 0 && _query.Trim().Length > 0;

    public void Reset() => Query = string.Empty;

    /// <summary>Re-runs the current query (used after the index has been rebuilt).</summary>
    public void Refresh() => RunSearch();

    public void Open(ResultItem item)
    {
        HideRequested?.Invoke();
        ProcessLauncher.Open(item.Entry);
    }

    private void RunSearch()
    {
        Results.Clear();

        string q = _query.Trim();
        if (q.Length > 0)
        {
            foreach (var entry in _engine.Search(q, MaxResults))
                Results.Add(new ResultItem(entry));
        }

        _selectedIndex = Results.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(SelectedIndex));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasNoMatches));
    }

    private void MoveSelection(int delta)
    {
        if (Results.Count == 0) return;
        SelectedIndex = (Math.Max(_selectedIndex, 0) + delta + Results.Count) % Results.Count;
    }

    private void ExecuteSelected()
    {
        if (_selectedIndex >= 0 && _selectedIndex < Results.Count)
            Open(Results[_selectedIndex]);
    }
}
