using System.IO;
using System.Windows;
using System.Windows.Threading;
using RayLite.Models;
using RayLite.Services;
using RayLite.ViewModels;

namespace RayLite;

public partial class App : Application
{
    private const string MutexName = "RayLite_SingleInstance_Mutex";

    private Mutex? _mutex;
    private bool _ownsMutex;
    private HotKeyManager? _hotKeys;
    private TrayIcon? _tray;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private IndexManager? _index;
    private AppSettings _settings = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out bool createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Explicit culture initialization (must run before any WPF element is created).
        var culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
        System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage("en-US")));

        _settings = SettingsService.Load();
        // The Run key is the source of truth (the installer's startup task writes it); refresh the path in case the app moved.
        try { _settings.RunOnStartup = StartupManager.IsEnabled(); if (_settings.RunOnStartup) StartupManager.Apply(true); }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or System.Security.SecurityException) { LogError(ex); }

        var engine = new SearchEngine();
        _index = new IndexManager(engine);

        var viewModel = new MainViewModel(engine);
        _mainWindow = new MainWindow(viewModel);
        _mainWindow.Prewarm();

        _index.IndexUpdated += () => Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_mainWindow is { IsVisible: true }) viewModel.Refresh();
        }));

        _hotKeys = new HotKeyManager();
        _hotKeys.HotKeyPressed += () => _mainWindow?.Toggle();
        if (!_hotKeys.TryRegister(_settings.Hotkey, out string error))
        {
            MessageBox.Show(
                error + "\n\nOpen Settings from the tray icon to choose a different hotkey.",
                "RayLite", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        _tray = new TrayIcon(_hotKeys);
        _tray.OpenRequested += () => _mainWindow?.ShowLauncher();
        _tray.SettingsRequested += ShowSettings;
        _tray.RebuildRequested += () => _index?.RebuildAsync(_settings.Clone());
        _tray.ExitRequested += () => Shutdown();

        _index.RebuildAsync(_settings.Clone());
        MemoryTrimmer.ScheduleTrim();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _hotKeys?.Dispose();
        if (_mutex != null)
        {
            if (_ownsMutex) _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
        base.OnExit(e);
    }

    private void ShowSettings()
    {
        _mainWindow?.HideLauncher();

        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, ApplySettings);
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            MemoryTrimmer.ScheduleTrim();
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>Applies and persists new settings. Returns an error message, or null on success.</summary>
    private string? ApplySettings(AppSettings updated)
    {
        string previousHotkey = _settings.Hotkey;
        bool hotkeyChanged = !string.Equals(updated.Hotkey, previousHotkey, StringComparison.OrdinalIgnoreCase);

        if (hotkeyChanged && _hotKeys != null && !_hotKeys.TryRegister(updated.Hotkey, out string error))
        {
            _hotKeys.TryRegister(previousHotkey, out _);
            return error;
        }

        try
        {
            StartupManager.Apply(updated.RunOnStartup);
            SettingsService.Save(updated);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
        {
            if (hotkeyChanged) _hotKeys?.TryRegister(previousHotkey, out _);
            return "Could not save settings: " + ex.Message;
        }

        _settings = updated;
        _index?.RebuildAsync(updated.Clone());
        return null;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogError(e.Exception);
        e.Handled = true; // keep the tray app alive
    }

    internal static void LogError(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(SettingsService.FolderPath);
            File.AppendAllText(
                Path.Combine(SettingsService.FolderPath, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception logEx) when (logEx is IOException or UnauthorizedAccessException) { }
    }
}
