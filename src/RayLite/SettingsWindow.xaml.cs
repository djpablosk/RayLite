using System.Windows;
using System.Windows.Input;
using RayLite.Models;
using RayLite.ViewModels;
using Microsoft.Win32;

namespace RayLite;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(AppSettings settings, Func<AppSettings, string?> apply)
    {
        InitializeComponent();
        _viewModel = new SettingsViewModel(settings, apply, PickFolder, Close);
        DataContext = _viewModel;
    }

    private static string? PickFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select a folder" };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.None or Key.ImeProcessed or Key.DeadCharProcessed
            or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        _viewModel.CaptureHotkey(Keyboard.Modifiers, key);
    }
}
