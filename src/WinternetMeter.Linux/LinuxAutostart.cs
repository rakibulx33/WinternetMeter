using System.Text;
using WinternetMeter.Core;

namespace WinternetMeter.Linux;

/// <summary>
/// "Run on startup" via the XDG autostart spec: a .desktop file in ~/.config/autostart.
/// Works on GNOME, KDE, XFCE, Cinnamon, MATE.
/// </summary>
internal sealed class LinuxAutostart : IAutostart
{
    private static string AutostartDir
    {
        get
        {
            var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(config))
                config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(config, "autostart");
        }
    }

    private static string FilePath => Path.Combine(AutostartDir, "winternetmeter.desktop");

    /// <summary>For an AppImage, ProcessPath is inside a temporary mount; $APPIMAGE is the real file.</summary>
    private static string ExecutablePath =>
        Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 } appImage ? appImage : Environment.ProcessPath ?? "winternetmeter";

    public bool IsEnabled => File.Exists(FilePath);

    public void SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                Directory.CreateDirectory(AutostartDir);
                File.WriteAllText(FilePath, DesktopEntry());
            }
            else if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void Repair()
    {
        try
        {
            if (IsEnabled && !File.ReadAllText(FilePath).Contains($"Exec={QuoteExec(ExecutablePath)}", StringComparison.Ordinal))
                SetEnabled(true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string DesktopEntry() => new StringBuilder()
        .AppendLine("[Desktop Entry]")
        .AppendLine("Type=Application")
        .AppendLine("Name=Winternet Meter")
        .AppendLine("Comment=Real-time network upload/download speed")
        .AppendLine($"Exec={QuoteExec(ExecutablePath)}")
        .AppendLine("Icon=winternetmeter")
        .AppendLine("Terminal=false")
        .AppendLine("X-GNOME-Autostart-enabled=true")
        // Give the panel's tray host a moment to start first.
        .AppendLine("X-GNOME-Autostart-Delay=5")
        .ToString();

    /// <summary>Quoting rules from the Desktop Entry spec for the Exec key.</summary>
    internal static string QuoteExec(string path)
    {
        var escaped = path.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("`", "\\`").Replace("$", "\\$");
        return $"\"{escaped}\"";
    }
}
