namespace WinternetMeter.Core;

/// <summary>One menu entry, toolkit-neutral. Each front-end converts these into its own menu items.</summary>
public sealed record MenuNode(
    string Text,
    Action? OnClick = null,
    bool? IsChecked = null,
    IReadOnlyList<MenuNode>? Children = null,
    bool IsEnabled = true,
    bool IsSeparator = false,
    bool IsBold = false,
    string? ToolTip = null)
{
    public static MenuNode Separator { get; } = new("-", IsSeparator: true);
}

[Flags]
public enum SettingsChange
{
    None = 0,

    /// <summary>Adapter mode/selection changed → reconfigure the monitor.</summary>
    Adapter = 1,

    /// <summary>Font, colors, units, background → re-layout and redraw the widget.</summary>
    Appearance = 2,

    /// <summary>Fix position, fullscreen hiding, refresh rate, smoothing.</summary>
    Behavior = 4,
}

/// <summary>What the shared menu needs from a front-end. Dialogs and window placement differ per OS.</summary>
public interface IMeterHost
{
    AppSettings Settings { get; }

    NetworkSpeedMonitor Monitor { get; }

    IAutostart Autostart { get; }

    bool SupportsFullscreenHide { get; }

    /// <summary>Called after the menu changed <see cref="Settings"/>: apply and save.</summary>
    void OnSettingsChanged(SettingsChange change);

    void PickFont();

    void PickTextColor();

    void PickBackgroundColor();

    void ResetPosition();

    void OpenUrl(string url);

    void Exit();
}

/// <summary>Builds the tray / right-click menu. Rebuild it every time it opens so checks and adapters are current.</summary>
public static class MeterMenu
{
    public static IReadOnlyList<MenuNode> Build(IMeterHost host, string version)
    {
        var s = host.Settings;

        void Set(Action change, SettingsChange kind)
        {
            change();
            host.OnSettingsChanged(kind);
        }

        var items = new List<MenuNode>
        {
            new($"Winternet Meter {version}", () => host.OpenUrl(Presets.RepoUrl), IsBold: true, ToolTip: "Open the project page"),
            MenuNode.Separator,
            new("Network adapter", Children: AdapterItems(host, Set)),
            new("Units", Children:
            [
                new("MB/s  (bytes — download managers)", () => Set(() => s.Unit = SpeedUnit.Bytes, SettingsChange.Appearance), s.Unit == SpeedUnit.Bytes),
                new("Mbps  (bits — ISP / speed tests)", () => Set(() => s.Unit = SpeedUnit.Bits, SettingsChange.Appearance), s.Unit == SpeedUnit.Bits),
            ]),
            new("Appearance", Children: AppearanceItems(host, Set)),
            new("Update speed", Children:
            [
                .. Presets.RefreshIntervals.Select(ms => new MenuNode(
                    ms < 1000 ? $"Every {ms / 1000.0:0.#} s" : $"Every {ms / 1000} s",
                    () => Set(() => s.RefreshIntervalMs = ms, SettingsChange.Behavior),
                    s.RefreshIntervalMs == ms)),
                MenuNode.Separator,
                new("Smooth (average last 3 readings)", () => Set(() => s.Smoothing = !s.Smoothing, SettingsChange.Behavior), s.Smoothing),
            ]),
            MenuNode.Separator,
        };

        if (host.SupportsFullscreenHide)
            items.Add(new("Hide in fullscreen", () => Set(() => s.HideInFullscreen = !s.HideInFullscreen, SettingsChange.Behavior), s.HideInFullscreen,
                ToolTip: "Hide while a game, video or presentation is fullscreen"));

        items.Add(new("Lock position", () => Set(() => s.FixPosition = !s.FixPosition, SettingsChange.Behavior), s.FixPosition));
        items.Add(new("Reset position", host.ResetPosition));

        bool autostart = host.Autostart.IsEnabled;
        items.Add(new("Run on startup", () => host.Autostart.SetEnabled(!autostart), autostart));

        items.Add(MenuNode.Separator);
        items.Add(new("Exit", host.Exit));
        return items;
    }

    private static List<MenuNode> AdapterItems(IMeterHost host, Action<Action, SettingsChange> set)
    {
        var s = host.Settings;
        var monitor = host.Monitor;
        bool auto = s.AdapterMode == AdapterMode.Auto;

        var list = new List<MenuNode>
        {
            new("Auto (recommended)", () => set(() => s.AdapterMode = AdapterMode.Auto, SettingsChange.Adapter), auto,
                ToolTip: "Measures the adapter(s) your internet traffic goes through and follows Wi-Fi/Ethernet switches"),
        };

        var active = monitor.ActiveAdapters;
        string activeText = active.Count == 0 ? "No active connection" : "Measuring: " + string.Join(" + ", active.Select(a => a.Name));
        list.Add(new(activeText, IsEnabled: false));
        list.Add(MenuNode.Separator);

        var choices = monitor.AllAdapters
            .Where(a => a.IsUp && a.Kind != AdapterKind.Loopback)
            .OrderBy(AdapterResolver.IsVirtual)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (choices.Count == 0)
            list.Add(new("(no adapters are up)", IsEnabled: false));

        foreach (var a in choices)
        {
            var label = AdapterResolver.IsVirtual(a) ? $"{a.Name}  (virtual)" : a.Name;
            bool isChecked = !auto && s.AdapterId == a.Id;
            list.Add(new(label, () => set(() =>
            {
                s.AdapterMode = AdapterMode.Manual;
                s.AdapterId = a.Id;
            }, SettingsChange.Adapter), isChecked, ToolTip: a.Description));
        }
        return list;
    }

    private static List<MenuNode> AppearanceItems(IMeterHost host, Action<Action, SettingsChange> set)
    {
        var s = host.Settings;
        var current = s.FontFamily;

        var fonts = new List<MenuNode>
        {
            new("Default (monospace)", () => set(() => s.FontFamily = null, SettingsChange.Appearance), current is null),
            MenuNode.Separator,
        };
        fonts.AddRange(Presets.Fonts.Select(f => new MenuNode(f,
            () => set(() => s.FontFamily = f, SettingsChange.Appearance),
            string.Equals(current, f, StringComparison.OrdinalIgnoreCase))));
        bool isCustomFont = current is not null && !Presets.Fonts.Contains(current, StringComparer.OrdinalIgnoreCase);
        fonts.Add(MenuNode.Separator);
        fonts.Add(new(isCustomFont ? $"Other… ({current})" : "Other…", host.PickFont, isCustomFont ? true : null));

        var sizes = Presets.FontSizes
            .Select(size => new MenuNode($"{size} pt", () => set(() => s.FontSize = size, SettingsChange.Appearance), s.FontSize == size))
            .ToList();
        if (!Presets.FontSizes.Contains(s.FontSize))
            sizes.Add(new($"{s.FontSize} pt (custom)", null, true));

        var textColors = Presets.TextColors
            .Select(c => new MenuNode(c.Name, () => set(() => s.TextColor = c.Hex, SettingsChange.Appearance),
                string.Equals(s.TextColor, c.Hex, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        bool customText = !Presets.TextColors.Any(c => string.Equals(s.TextColor, c.Hex, StringComparison.OrdinalIgnoreCase));
        textColors.Add(MenuNode.Separator);
        textColors.Add(new(customText ? $"Custom… ({s.TextColor})" : "Custom…", host.PickTextColor, customText ? true : null));

        bool pill = s.Background == BackgroundStyle.Pill;
        var background = new List<MenuNode>
        {
            new("None (transparent, for the taskbar)", () => set(() => s.Background = BackgroundStyle.None, SettingsChange.Appearance), !pill),
            new("Rounded box", () => set(() => s.Background = BackgroundStyle.Pill, SettingsChange.Appearance), pill),
            MenuNode.Separator,
        };
        background.AddRange(Presets.BackgroundColors.Select(c => new MenuNode($"Box color: {c.Name}",
            () => set(() => { s.Background = BackgroundStyle.Pill; s.BackgroundColor = c.Hex; }, SettingsChange.Appearance),
            pill && string.Equals(s.BackgroundColor, c.Hex, StringComparison.OrdinalIgnoreCase))));
        background.Add(new("Box color: Custom…", host.PickBackgroundColor));
        background.Add(MenuNode.Separator);
        background.AddRange(Presets.Opacities.Select(o => new MenuNode($"Box opacity {o:P0}",
            () => set(() => { s.Background = BackgroundStyle.Pill; s.BackgroundOpacity = o; }, SettingsChange.Appearance),
            pill && Math.Abs(s.BackgroundOpacity - o) < 0.001)));

        return
        [
            new("Font", Children: fonts),
            new("Font size", Children: sizes),
            new("Text color", Children: textColors),
            new("Background", Children: background),
        ];
    }
}
