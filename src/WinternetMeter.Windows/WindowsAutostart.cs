using Microsoft.Win32;
using WinternetMeter.Core;

namespace WinternetMeter.Windows;

/// <summary>
/// "Run on startup" via HKCU\...\Run (per-user, no admin).
/// The value name matches v1 and the installer so all three manage the same entry.
/// </summary>
internal sealed class WindowsAutostart : IAutostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string ValueName = "Winternet Meter";

    // Quoted: v1 wrote the bare path, which breaks when it contains spaces.
    private static string Command => $"\"{Environment.ProcessPath}\"";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, Command);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public void Repair()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) is string current && current != Command)
                key.SetValue(ValueName, Command);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
        }
    }
}
