using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using WinternetMeter.Core;

namespace WinternetMeter.Linux;

/// <summary>Two small dialogs: choose a font from the installed list, and type a color.</summary>
internal static class Dialogs
{
    public static void PickFont(string? current, Action<string> onPicked)
    {
        var fonts = FontManager.Current.SystemFonts
            .Select(f => f.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filter = new TextBox { Watermark = "Search fonts…", Margin = new Thickness(0, 0, 0, 8) };
        var list = new ListBox { ItemsSource = fonts, SelectedItem = current };
        var preview = new TextBlock
        {
            Text = "↑ 12.3 MB/s  ↓ 456.7 KB/s",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 8),
        };
        var ok = new Button { Content = "Use font", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };

        var layout = new DockPanel { Margin = new Thickness(12) };
        DockPanel.SetDock(filter, Dock.Top);
        DockPanel.SetDock(ok, Dock.Bottom);
        DockPanel.SetDock(preview, Dock.Bottom);
        layout.Children.AddRange([filter, ok, preview, list]);

        var window = new Window
        {
            Title = "Choose font — Winternet Meter",
            Width = 380,
            Height = 480,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = layout,
        };

        filter.TextChanged += (_, _) =>
        {
            var q = filter.Text?.Trim() ?? string.Empty;
            list.ItemsSource = q.Length == 0 ? fonts : fonts.Where(f => f.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        };
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is string name) preview.FontFamily = new FontFamily(name);
        };

        void Accept()
        {
            if (list.SelectedItem is string name) onPicked(name);
            window.Close();
        }

        ok.Click += (_, _) => Accept();
        list.DoubleTapped += (_, _) => Accept();
        window.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) window.Close();
        };

        window.Show();
        window.Activate();
    }

    public static void PickColor(string title, string current, Action<string> onPicked)
    {
        var input = new TextBox { Text = current, Watermark = "#RRGGBB" };
        var swatch = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(4), Margin = new Thickness(8, 0, 0, 0) };
        var error = new TextBlock { Foreground = Brushes.IndianRed, Margin = new Thickness(0, 6, 0, 0) };
        var ok = new Button { Content = "OK", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };

        void UpdateSwatch()
        {
            bool valid = ColorHex.TryParse(input.Text, out var r, out var g, out var b);
            swatch.Background = valid ? new SolidColorBrush(Color.FromRgb(r, g, b)) : Brushes.Transparent;
            error.Text = valid ? string.Empty : "Use a hex color like #00FF00";
            ok.IsEnabled = valid;
        }

        var row = new DockPanel();
        DockPanel.SetDock(swatch, Dock.Right);
        row.Children.AddRange([swatch, input]);

        var window = new Window
        {
            Title = title,
            Width = 320,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new StackPanel { Margin = new Thickness(12), Children = { row, error, ok } },
        };

        input.TextChanged += (_, _) => UpdateSwatch();
        ok.Click += (_, _) =>
        {
            onPicked(ColorHex.Normalize(input.Text, current));
            window.Close();
        };
        window.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) window.Close();
        };

        UpdateSwatch();
        window.Show();
        window.Activate();
        input.Focus();
    }
}
