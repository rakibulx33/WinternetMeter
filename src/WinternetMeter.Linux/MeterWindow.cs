using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using WinternetMeter.Core;

namespace WinternetMeter.Linux;

/// <summary>
/// The floating widget: borderless, transparent, always on top, not in the taskbar.
/// Two rows (↑ upload, ↓ download) with the numbers right-aligned in a column sized
/// once for the widest possible text, so nothing moves while numbers change.
/// </summary>
internal sealed class MeterWindow : Window
{
    private static readonly FontFamily DefaultFont = new("Ubuntu Mono, DejaVu Sans Mono, Noto Sans Mono, monospace");

    private readonly AppSettings _settings;
    private readonly Border _box;
    private readonly TextBlock[] _arrows;
    private readonly TextBlock _up;
    private readonly TextBlock _down;

    public MeterWindow(AppSettings settings)
    {
        _settings = settings;

        Title = "Winternet Meter";
        SystemDecorations = SystemDecorations.None;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        CanResize = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];

        _arrows = [MakeText("↑"), MakeText("↓")];
        _up = MakeText(string.Empty, TextAlignment.Right);
        _down = MakeText(string.Empty, TextAlignment.Right);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,5,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
        };
        Place(grid, _arrows[0], 0, 0);
        Place(grid, _up, 0, 2);
        Place(grid, _arrows[1], 1, 0);
        Place(grid, _down, 1, 2);

        _box = new Border
        {
            Child = grid,
            Padding = new Thickness(8, 3),
            CornerRadius = new CornerRadius(6),
        };
        _box.PointerPressed += OnPointerPressed;
        Content = _box;

        ApplyAppearance();
    }

    /// <summary>The right-click menu (rebuilt by the controller).</summary>
    public ContextMenu? Menu
    {
        get => _box.ContextMenu;
        set => _box.ContextMenu = value;
    }

    public void SetSpeeds(string upload, string download)
    {
        _up.Text = upload.Trim();
        _down.Text = download.Trim();
    }

    public void ApplyAppearance()
    {
        var family = string.IsNullOrWhiteSpace(_settings.FontFamily) ? DefaultFont : new FontFamily(_settings.FontFamily);
        double size = _settings.FontSize * 96.0 / 72.0; // points → device-independent pixels
        var foreground = new SolidColorBrush(ParseColor(_settings.TextColor, Colors.White));
        bool pill = _settings.Background == BackgroundStyle.Pill;

        // Soft dark outline keeps transparent text readable on light panels/wallpapers.
        IEffect? shadow = pill ? null : new DropShadowEffect { OffsetX = 1, OffsetY = 1, BlurRadius = 2, Color = Colors.Black, Opacity = 0.75 };

        foreach (var t in _arrows.Concat([_up, _down]))
        {
            t.FontFamily = family;
            t.FontSize = size;
            t.FontWeight = FontWeight.Bold;
            t.Foreground = foreground;
            t.Effect = shadow;
        }

        var widest = new FormattedText(SpeedFormatter.WidestSample(_settings.Unit), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface(family, FontStyle.Normal, FontWeight.Bold), size, foreground);
        _up.MinWidth = _down.MinWidth = Math.Ceiling(widest.WidthIncludingTrailingWhitespace) + 1;

        // Alpha 1 when "none": invisible, but still receives clicks so the widget can be dragged.
        _box.Background = pill
            ? new SolidColorBrush(ParseColor(_settings.BackgroundColor, Color.FromRgb(31, 31, 31)), _settings.BackgroundOpacity)
            : new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_settings.FixPosition && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e); // the window manager moves it; PositionChanged reports the result
    }

    private static TextBlock MakeText(string text, TextAlignment alignment = TextAlignment.Left) => new()
    {
        Text = text,
        TextAlignment = alignment,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static void Place(Grid grid, Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    internal static Color ParseColor(string hex, Color fallback) =>
        ColorHex.TryParse(hex, out var r, out var g, out var b) ? Color.FromRgb(r, g, b) : fallback;
}
