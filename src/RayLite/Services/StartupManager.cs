using Microsoft.Win32;

namespace RayLite.Services;

public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "RayLite";

    public static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string;
    }

    public static void Apply(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open the Windows startup registry key.");

        if (enabled)
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) throw new InvalidOperationException("Could not determine the application path.");
            key.SetValue(ValueName, $"\"{exe}\"", RegistryValueKind.String);
        }
        else if (key.GetValue(ValueName) != null)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
