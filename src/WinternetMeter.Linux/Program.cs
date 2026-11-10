using Avalonia;
using Avalonia.Controls;

namespace WinternetMeter.Linux;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // One meter only (autostart + manual launch must not give two widgets).
        using var instanceLock = SingleInstance.TryAcquire();
        if (instanceLock is null)
            return 0;

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    // Also used by the Avalonia previewer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions { WmClass = "winternetmeter" }) // matches the .desktop file
            .LogToTrace();
}

/// <summary>
/// A lock file opened with FileShare.None. On Linux .NET backs that with flock(), which
/// the kernel drops automatically if the process dies — no stale-lock cleanup needed.
/// </summary>
internal static class SingleInstance
{
    public static FileStream? TryAcquire()
    {
        var dir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) dir = Path.GetTempPath();
        var path = Path.Combine(dir, $"winternetmeter-{Environment.UserName}.lock");
        try
        {
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
