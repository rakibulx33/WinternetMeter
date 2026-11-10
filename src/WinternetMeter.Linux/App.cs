using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace WinternetMeter.Linux;

/// <summary>No XAML: the UI is tiny and building it in code keeps everything in one language.</summary>
internal sealed class App : Application
{
    private MeterController? _controller;

    public override void Initialize()
    {
        Name = "Winternet Meter";
        RequestedThemeVariant = ThemeVariant.Default; // follows the desktop's light/dark setting
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            _controller = new MeterController(this, desktop);
        base.OnFrameworkInitializationCompleted();
    }
}
