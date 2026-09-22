using Microsoft.Win32;

namespace PTBox.Launcher.Services;

public sealed class AutoStartService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "PTBox.Launcher";
    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) is string value && value == Command();
    }
    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);
        if (enabled) key.SetValue(ValueName, Command(), RegistryValueKind.String);
        else key.DeleteValue(ValueName, false);
    }
    private static string Command() => $"\"{System.IO.Path.Combine(AppContext.BaseDirectory, "PTBox.Launcher.exe")}\"";
}
