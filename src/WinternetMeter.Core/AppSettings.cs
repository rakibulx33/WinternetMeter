namespace WinternetMeter.Core;

public enum BackgroundStyle
{
    /// <summary>Text only, transparent — sits naturally on the taskbar.</summary>
    None,

    /// <summary>Text on a rounded, semi-transparent box — readable on any wallpaper.</summary>
    Pill,
}

/// <summary>Everything the user can change. Saved as JSON (see <see cref="JsonSettingsStore"/>).</summary>
public sealed class AppSettings
{
    public const int MinFontSize = 7;
    public const int MaxFontSize = 99;
    public const int MinRefreshMs = 250;
    public const int MaxRefreshMs = 5000;

    public int SchemaVersion { get; set; } = 1;

    // Network
    public AdapterMode AdapterMode { get; set; } = AdapterMode.Auto;
    public string? AdapterId { get; set; }
    public SpeedUnit Unit { get; set; } = SpeedUnit.Bytes;
    public bool Smoothing { get; set; }
    public int RefreshIntervalMs { get; set; } = 1000;

    // Appearance
    /// <summary>Null = the platform's default (a monospace font, so digits don't shift).</summary>
    public string? FontFamily { get; set; }
    public int FontSize { get; set; } = 10;
    public string TextColor { get; set; } = "#FFFFFF";
    public BackgroundStyle Background { get; set; } = BackgroundStyle.None;
    public string BackgroundColor { get; set; } = "#1F1F1F";
    /// <summary>Opacity of the pill background (0.2–1). Text is always fully opaque.</summary>
    public double BackgroundOpacity { get; set; } = 0.85;

    // Window
    /// <summary>Top-left of the widget in screen pixels; null = default spot next to the clock.</summary>
    public int? PositionX { get; set; }
    public int? PositionY { get; set; }
    public bool FixPosition { get; set; }
    /// <summary>Hide while a fullscreen app/game/video is in front (Windows only).</summary>
    public bool HideInFullscreen { get; set; } = true;

    public bool HasPosition => PositionX is not null && PositionY is not null;

    /// <summary>Clamps out-of-range values from a hand-edited or old file.</summary>
    public AppSettings Normalize()
    {
        if (!Enum.IsDefined(AdapterMode)) AdapterMode = AdapterMode.Auto;
        if (!Enum.IsDefined(Unit)) Unit = SpeedUnit.Bytes;
        if (!Enum.IsDefined(Background)) Background = BackgroundStyle.None;
        if (AdapterMode == AdapterMode.Manual && string.IsNullOrEmpty(AdapterId)) AdapterMode = AdapterMode.Auto;

        FontSize = Math.Clamp(FontSize, MinFontSize, MaxFontSize);
        RefreshIntervalMs = Math.Clamp(RefreshIntervalMs, MinRefreshMs, MaxRefreshMs);
        BackgroundOpacity = double.IsFinite(BackgroundOpacity) ? Math.Clamp(BackgroundOpacity, 0.2, 1.0) : 0.85;
        TextColor = ColorHex.Normalize(TextColor, "#FFFFFF");
        BackgroundColor = ColorHex.Normalize(BackgroundColor, "#1F1F1F");
        if (string.IsNullOrWhiteSpace(FontFamily)) FontFamily = null;
        return this;
    }
}
