using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;
using WinternetMeter.Core;

namespace WinternetMeter.Windows;

/// <summary>
/// Owns everything: tray icon, floating widget, timers, settings.
/// Implements <see cref="IMeterHost"/> so the shared menu (Core) can drive it.
/// </summary>
internal sealed class TrayAppContext : ApplicationContext, IMeterHost
{
    private const int EdgeMargin = 8;

    private readonly JsonSettingsStore _store;
    private readonly WindowsAutostart _autostart = new();
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly FloatingSpeedForm _widget;
    private readonly System.Windows.Forms.Timer _speedTimer;
    private readonly System.Windows.Forms.Timer _housekeepingTimer;
    private readonly string _version;
    private bool _hiddenForFullscreen;

    public TrayAppContext(AppSettings settings, JsonSettingsStore store)
    {
        Settings = settings;
        _store = store;
        _version = Assembly.GetEntryAssembly()?.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "";

        Monitor = new NetworkSpeedMonitor(new SystemNetworkSource());
        Monitor.Configure(settings.AdapterMode, settings.AdapterId);
        Monitor.SmoothingWindow = settings.Smoothing ? 3 : 1;

        _autostart.Repair();

        // Global renderer so submenus are themed too, not just the top-level strip.
        ToolStripManager.Renderer = MenuTheme.CreateRenderer();
        _menu = new ContextMenuStrip { ShowItemToolTips = true };
        _menu.Opening += (_, e) =>
        {
            RebuildMenu();
            e.Cancel = false; // WinForms cancels opening an empty strip; we fill it right here
        };
        RebuildMenu();

        _tray = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "Winternet Meter",
            ContextMenuStrip = _menu,
            Visible = true,
        };

        _widget = new FloatingSpeedForm(settings) { ContextMenuStrip = _menu };
        _widget.DragFinished += (_, _) => SavePosition();
        _ = _widget.Handle; // create the window now so its DPI is known before measuring
        _widget.ApplyAppearance();
        PlaceWidget(useSaved: true);
        _widget.Show();

        _speedTimer = new System.Windows.Forms.Timer { Interval = settings.RefreshIntervalMs };
        _speedTimer.Tick += (_, _) => UpdateSpeed();
        _speedTimer.Start();

        _housekeepingTimer = new System.Windows.Forms.Timer { Interval = 1500 };
        _housekeepingTimer.Tick += (_, _) => Housekeeping();
        _housekeepingTimer.Start();

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.SessionEnding += OnSessionEnding;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        UpdateSpeed();
    }

    public AppSettings Settings { get; }

    public NetworkSpeedMonitor Monitor { get; }

    public IAutostart Autostart => _autostart;

    public bool SupportsFullscreenHide => true;

    // ---- IMeterHost -------------------------------------------------------------

    public void OnSettingsChanged(SettingsChange change)
    {
        Settings.Normalize();
        if (change.HasFlag(SettingsChange.Adapter))
            Monitor.Configure(Settings.AdapterMode, Settings.AdapterId);
        if (change.HasFlag(SettingsChange.Appearance))
            _widget.ApplyAppearance();
        if (change.HasFlag(SettingsChange.Behavior))
        {
            _speedTimer.Interval = Settings.RefreshIntervalMs;
            Monitor.SmoothingWindow = Settings.Smoothing ? 3 : 1;
            if (!Settings.HideInFullscreen && _hiddenForFullscreen) ShowAfterFullscreen();
        }
        _store.Save(Settings);
        UpdateSpeed();
    }

    public void PickFont()
    {
        using var dialog = new FontDialog
        {
            ShowEffects = false,
            ShowColor = false,
            AllowVerticalFonts = false,
            FontMustExist = true,
            MinSize = AppSettings.MinFontSize,
            MaxSize = AppSettings.MaxFontSize,
        };
        using (var current = FloatingSpeedForm.CreateWidgetFont(Settings, 1f))
            dialog.Font = new Font(current.FontFamily, Settings.FontSize, FontStyle.Bold);

        if (dialog.ShowDialog() != DialogResult.OK) return;
        Settings.FontFamily = dialog.Font.FontFamily.Name;
        Settings.FontSize = (int)Math.Round(dialog.Font.SizeInPoints);
        OnSettingsChanged(SettingsChange.Appearance);
    }

    public void PickTextColor()
    {
        if (TryPickColor(Settings.TextColor, out var hex))
        {
            Settings.TextColor = hex;
            OnSettingsChanged(SettingsChange.Appearance);
        }
    }

    public void PickBackgroundColor()
    {
        if (TryPickColor(Settings.BackgroundColor, out var hex))
        {
            Settings.BackgroundColor = hex;
            Settings.Background = BackgroundStyle.Pill;
            OnSettingsChanged(SettingsChange.Appearance);
        }
    }

    public void ResetPosition()
    {
        Settings.PositionX = Settings.PositionY = null;
        PlaceWidget(useSaved: false);
        _store.Save(Settings);
    }

    public void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }

    public void Exit() => ExitThread();

    // ---- Speed + housekeeping ---------------------------------------------------

    private void UpdateSpeed()
    {
        var reading = Monitor.Tick();
        _widget.SetSpeeds(
            SpeedFormatter.Format(reading.UploadBytesPerSecond, Settings.Unit),
            SpeedFormatter.Format(reading.DownloadBytesPerSecond, Settings.Unit));

        string tip = Monitor.ActiveAdapters.Count == 0
            ? "Winternet Meter — no active connection"
            : SpeedFormatter.Tooltip(reading, Settings.Unit);
        _tray.Text = tip.Length > 127 ? tip[..127] : tip;
    }

    private void Housekeeping()
    {
        if (_menu.Visible) return; // don't jump above an open menu

        if (Settings.HideInFullscreen && _widget.IsHandleCreated)
        {
            bool fullscreen = FullscreenDetector.IsFullscreenOnMonitorOf(_widget.Handle);
            if (fullscreen && !_hiddenForFullscreen)
            {
                _hiddenForFullscreen = true;
                _widget.Hide();
            }
            else if (!fullscreen && _hiddenForFullscreen)
            {
                ShowAfterFullscreen();
            }
        }

        _widget.ReassertTopmost();
    }

    private void ShowAfterFullscreen()
    {
        _hiddenForFullscreen = false;
        _widget.Show();
    }

    // ---- Position ---------------------------------------------------------------

    /// <summary>
    /// Restores the saved spot if any monitor can still show it; otherwise parks the
    /// widget on the taskbar, just left of the notification area (where v1 users keep it).
    /// Uses full screen bounds, not the working area, because the taskbar is a valid place.
    /// </summary>
    private void PlaceWidget(bool useSaved)
    {
        if (useSaved && Settings.HasPosition)
        {
            var saved = new Rectangle(Settings.PositionX!.Value, Settings.PositionY!.Value, _widget.Width, _widget.Height);
            if (IsMostlyVisible(saved))
            {
                _widget.Location = saved.Location;
                return;
            }
        }
        _widget.Location = DefaultLocation(_widget.Size, _widget.DeviceDpi / 96f);
    }

    private static bool IsMostlyVisible(Rectangle r)
    {
        long visible = 0;
        foreach (var screen in Screen.AllScreens)
        {
            var i = Rectangle.Intersect(screen.Bounds, r);
            visible += (long)i.Width * i.Height;
        }
        return visible * 2 >= (long)r.Width * r.Height;
    }

    private static Point DefaultLocation(Size size, float scale)
    {
        var screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        var bounds = screen.Bounds;
        var work = screen.WorkingArea;
        int taskbarHeight = bounds.Bottom - work.Bottom;

        if (taskbarHeight >= size.Height)
        {
            // Bottom taskbar: vertically centered on it, left of the tray/clock area.
            // ~320 logical px covers the tray icons, language switcher and clock.
            int trayWidth = Math.Min((int)(320 * scale), bounds.Width / 3);
            return new Point(
                bounds.Right - trayWidth - size.Width,
                work.Bottom + (taskbarHeight - size.Height) / 2);
        }

        // Taskbar elsewhere or auto-hidden: bottom-right of the free area.
        return new Point(work.Right - size.Width - EdgeMargin, work.Bottom - size.Height - EdgeMargin);
    }

    private void SavePosition()
    {
        Settings.PositionX = _widget.Left;
        Settings.PositionY = _widget.Top;
        _store.Save(Settings);
    }

    // ---- System events (may arrive on another thread: marshal to UI) --------------

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) RunOnUi(Monitor.Invalidate);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => RunOnUi(() =>
    {
        _widget.ApplyAppearance();
        PlaceWidget(useSaved: true); // monitor unplugged → bring it back
    });

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e) => RunOnUi(SavePosition);

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle)
            RunOnUi(() => ToolStripManager.Renderer = MenuTheme.CreateRenderer());
    }

    private void RunOnUi(Action action)
    {
        if (!_widget.IsHandleCreated || _widget.IsDisposed) return;
        if (_widget.InvokeRequired)
            _widget.BeginInvoke(action);
        else
            action();
    }

    // ---- Menu ---------------------------------------------------------------------

    private void RebuildMenu()
    {
        _menu.SuspendLayout();
        foreach (ToolStripItem old in _menu.Items) old.Dispose();
        _menu.Items.Clear();
        _menu.Items.AddRange(ToolStripMenuBuilder.Build(MeterMenu.Build(this, _version)));
        _menu.ResumeLayout();
    }

    private static bool TryPickColor(string currentHex, out string hex)
    {
        using var dialog = new ColorDialog
        {
            FullOpen = true,
            Color = FloatingSpeedForm.ToColor(currentHex, 255),
        };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            hex = ColorHex.ToHex(dialog.Color.R, dialog.Color.G, dialog.Color.B);
            return true;
        }
        hex = currentHex;
        return false;
    }

    private static Icon LoadTrayIcon()
    {
        using var stream = typeof(TrayAppContext).Assembly.GetManifestResourceStream("app.ico");
        return stream is null ? SystemIcons.Application : new Icon(stream, SystemInformation.SmallIconSize);
    }

    protected override void ExitThreadCore()
    {
        _speedTimer.Stop();
        _housekeepingTimer.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.SessionEnding -= OnSessionEnding;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

        if (!_hiddenForFullscreen) SavePosition();
        _tray.Visible = false;
        _tray.Dispose();
        _widget.Close();
        _widget.Dispose();
        Monitor.Dispose();
        _menu.Dispose();
        base.ExitThreadCore();
    }
}
