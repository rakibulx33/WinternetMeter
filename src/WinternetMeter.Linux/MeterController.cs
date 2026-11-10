using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using WinternetMeter.Core;

namespace WinternetMeter.Linux;

/// <summary>
/// Owns the widget, tray icon, timers and settings on Linux — the counterpart of the
/// Windows TrayAppContext, sharing the same Core logic and menu.
/// </summary>
internal sealed class MeterController : IMeterHost
{
    private const int EdgeMargin = 12;

    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly JsonSettingsStore _store;
    private readonly LinuxAutostart _autostart = new();
    private readonly MeterWindow _window;
    private readonly TrayIcon _tray;
    private readonly DispatcherTimer _speedTimer;
    private readonly DispatcherTimer _savePositionTimer;
    private readonly string _version;
    private bool _exiting;

    public MeterController(Application app, IClassicDesktopStyleApplicationLifetime desktop)
    {
        _desktop = desktop;
        _store = new JsonSettingsStore(JsonSettingsStore.DefaultPath());
        Settings = _store.Load();
        _version = Assembly.GetEntryAssembly()?.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "";

        Monitor = new NetworkSpeedMonitor(new SystemNetworkSource());
        Monitor.Configure(Settings.AdapterMode, Settings.AdapterId);
        Monitor.SmoothingWindow = Settings.Smoothing ? 3 : 1;
        Monitor.AdaptersChanged += (_, _) => RebuildMenus();

        _autostart.Repair();

        // Dragging fires many position updates; save once it settles.
        _savePositionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _savePositionTimer.Tick += (_, _) =>
        {
            _savePositionTimer.Stop();
            SavePosition();
        };

        var icon = LoadIcon();
        _window = new MeterWindow(Settings) { Icon = icon };
        _window.PositionChanged += (_, _) =>
        {
            _savePositionTimer.Stop();
            _savePositionTimer.Start();
        };
        _window.Opened += (_, _) => EnsureOnScreen();

        // Tray needs a StatusNotifierItem host (Ubuntu ships one: the AppIndicator extension).
        // Without it the icon is simply absent; the widget's right-click menu has everything.
        _tray = new TrayIcon { Icon = icon, ToolTipText = "Winternet Meter", IsVisible = true };
        TrayIcon.SetIcons(app, new TrayIcons { _tray });

        RebuildMenus();

        if (Settings.HasPosition)
            _window.Position = new PixelPoint(Settings.PositionX!.Value, Settings.PositionY!.Value);
        _window.Show();

        _speedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Settings.RefreshIntervalMs) };
        _speedTimer.Tick += (_, _) => UpdateSpeed();
        _speedTimer.Start();

        desktop.ShutdownRequested += (_, _) => SavePosition();
        UpdateSpeed();
    }

    public AppSettings Settings { get; }

    public NetworkSpeedMonitor Monitor { get; }

    public IAutostart Autostart => _autostart;

    // Linux has no portable "is a fullscreen app in front" API; the option is hidden.
    public bool SupportsFullscreenHide => false;

    // ---- IMeterHost -------------------------------------------------------------

    public void OnSettingsChanged(SettingsChange change)
    {
        Settings.Normalize();
        if (change.HasFlag(SettingsChange.Adapter))
            Monitor.Configure(Settings.AdapterMode, Settings.AdapterId);
        if (change.HasFlag(SettingsChange.Appearance))
            _window.ApplyAppearance();
        if (change.HasFlag(SettingsChange.Behavior))
        {
            _speedTimer.Interval = TimeSpan.FromMilliseconds(Settings.RefreshIntervalMs);
            Monitor.SmoothingWindow = Settings.Smoothing ? 3 : 1;
        }
        _store.Save(Settings);
        UpdateSpeed();
    }

    public void PickFont() => Dialogs.PickFont(Settings.FontFamily, name =>
    {
        Settings.FontFamily = name;
        OnSettingsChanged(SettingsChange.Appearance);
        RebuildMenus();
    });

    public void PickTextColor() => Dialogs.PickColor("Text color — Winternet Meter", Settings.TextColor, hex =>
    {
        Settings.TextColor = hex;
        OnSettingsChanged(SettingsChange.Appearance);
        RebuildMenus();
    });

    public void PickBackgroundColor() => Dialogs.PickColor("Box color — Winternet Meter", Settings.BackgroundColor, hex =>
    {
        Settings.BackgroundColor = hex;
        Settings.Background = BackgroundStyle.Pill;
        OnSettingsChanged(SettingsChange.Appearance);
        RebuildMenus();
    });

    public void ResetPosition()
    {
        _window.Position = DefaultPosition(CurrentPixelSize());
        SavePosition();
    }

    public void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
        }
        catch (Win32Exception)
        {
        }
    }

    public void Exit()
    {
        if (_exiting) return;
        _exiting = true;
        _speedTimer.Stop();
        SavePosition();
        _tray.IsVisible = false;
        _tray.Dispose();
        Monitor.Dispose();
        _desktop.Shutdown();
    }

    // ---- Internals --------------------------------------------------------------

    private void UpdateSpeed()
    {
        var reading = Monitor.Tick();
        _window.SetSpeeds(
            SpeedFormatter.Format(reading.UploadBytesPerSecond, Settings.Unit),
            SpeedFormatter.Format(reading.DownloadBytesPerSecond, Settings.Unit));
        _tray.ToolTipText = Monitor.ActiveAdapters.Count == 0
            ? "Winternet Meter — no active connection"
            : SpeedFormatter.Tooltip(reading, Settings.Unit);
    }

    private void RebuildMenus()
    {
        if (_exiting) return;
        var nodes = MeterMenu.Build(this, _version);
        // Rebuild after any click so check marks (incl. "Run on startup") stay true.
        void After() => Dispatcher.UIThread.Post(RebuildMenus);
        _tray.Menu = AvaloniaMenuBuilder.ToNativeMenu(nodes, After);
        _window.Menu = AvaloniaMenuBuilder.ToContextMenu(nodes, After);
    }

    private PixelSize CurrentPixelSize()
    {
        double scale = _window.DesktopScaling;
        return new PixelSize(
            (int)Math.Ceiling(_window.Bounds.Width * scale),
            (int)Math.Ceiling(_window.Bounds.Height * scale));
    }

    /// <summary>Saved spot if a screen can still show most of it, else bottom-right of the primary screen.</summary>
    private void EnsureOnScreen()
    {
        var size = CurrentPixelSize();
        var rect = new PixelRect(_window.Position, size);
        long visible = _window.Screens.All.Sum(s =>
        {
            var i = s.Bounds.Intersect(rect);
            return (long)i.Width * i.Height;
        });
        if (!Settings.HasPosition || visible * 2 < (long)size.Width * size.Height)
            _window.Position = DefaultPosition(size);
    }

    private PixelPoint DefaultPosition(PixelSize size)
    {
        var screen = _window.Screens.Primary ?? _window.Screens.All.FirstOrDefault();
        if (screen is null) return new PixelPoint(100, 100);
        var work = screen.WorkingArea;
        return new PixelPoint(work.Right - size.Width - EdgeMargin, work.Bottom - size.Height - EdgeMargin);
    }

    private void SavePosition()
    {
        Settings.PositionX = _window.Position.X;
        Settings.PositionY = _window.Position.Y;
        _store.Save(Settings);
    }

    private static WindowIcon LoadIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://winternetmeter/Assets/icon.png"));
        return new WindowIcon(stream);
    }
}
