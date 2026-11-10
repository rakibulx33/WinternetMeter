namespace WinternetMeter.Core;

/// <summary>Choices offered in the menu, shared so Windows and Linux stay identical.</summary>
public static class Presets
{
    public const string RepoUrl = "https://github.com/rakibulx33/WinternetMeter";

    public static readonly IReadOnlyList<(string Name, string Hex)> TextColors =
    [
        ("White", "#FFFFFF"),
        ("Lime", "#00FF00"),
        ("Cyan", "#00FFFF"),
        ("Yellow", "#FFFF00"),
        ("Orange", "#FFA500"),
        ("Red", "#FF4040"),
        ("Magenta", "#FF00FF"),
        ("Blue", "#3A96FF"),
        ("Black", "#000000"),
    ];

    public static readonly IReadOnlyList<(string Name, string Hex)> BackgroundColors =
    [
        ("Dark", "#1F1F1F"),
        ("Black", "#000000"),
        ("Light", "#F3F3F3"),
        ("Navy", "#14213D"),
    ];

    public static readonly IReadOnlyList<int> FontSizes = [8, 9, 10, 11, 12, 14, 16, 18, 20, 24];

    public static readonly IReadOnlyList<double> Opacities = [1.0, 0.85, 0.7, 0.5, 0.3];

    public static readonly IReadOnlyList<int> RefreshIntervals = [500, 1000, 2000];

    /// <summary>Monospace first: equal-width digits keep the numbers from jumping around.</summary>
    public static IReadOnlyList<string> Fonts => OperatingSystem.IsWindows()
        ? ["Cascadia Mono", "Consolas", "Segoe UI", "Segoe UI Variable Text", "Arial", "Tahoma", "Calibri"]
        : ["Ubuntu Mono", "DejaVu Sans Mono", "Noto Sans Mono", "Ubuntu", "DejaVu Sans", "Noto Sans", "Cantarell"];
}
