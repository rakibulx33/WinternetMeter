using System.ComponentModel;
using System.Globalization;
using System.Xml.Linq;
using WinternetMeter.Core;

namespace WinternetMeter.Windows;

/// <summary>
/// One-time import of v1 settings (Properties.Settings → user.config XML) into settings.json.
///
/// v1 stored them under %LocalAppData%\&lt;something with "Winternet"&gt;\…\&lt;version&gt;\user.config.
/// We take the newest such file. Runs only when settings.json doesn't exist yet.
/// </summary>
internal static class LegacySettingsMigrator
{
    private const string SectionName = "WinternetMeter.Properties.Settings";

    public static bool TryMigrate(out AppSettings settings)
    {
        settings = new AppSettings();
        try
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!Directory.Exists(root)) return false;

            var configs = Directory.EnumerateDirectories(root)
                .Where(d => Path.GetFileName(d).Contains("Winternet", StringComparison.OrdinalIgnoreCase))
                .SelectMany(d => Directory.EnumerateFiles(d, "user.config", SearchOption.AllDirectories))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc);

            foreach (var file in configs)
            {
                var section = XDocument.Load(file.FullName).Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == SectionName);
                if (section is null) continue;

                var values = section.Elements("setting")
                    .Where(e => e.Attribute("name") is not null)
                    .GroupBy(e => (string)e.Attribute("name")!)
                    .ToDictionary(g => g.Key, g => g.First().Element("value")?.Value ?? string.Empty);

                Apply(values, settings);
                return true;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
        }
        return false;
    }

    private static void Apply(IReadOnlyDictionary<string, string> v, AppSettings s)
    {
        if (v.TryGetValue("FontStyle", out var font) && !string.IsNullOrWhiteSpace(font))
            s.FontFamily = font;

        if (v.TryGetValue("TextSize", out var size) && int.TryParse(size, NumberStyles.Integer, CultureInfo.InvariantCulture, out var px))
            s.FontSize = px;

        if (v.TryGetValue("TextColor", out var color) && TryParseColor(color, out var hex))
            s.TextColor = hex;

        if (v.TryGetValue("FixPositionChecked", out var fix) && bool.TryParse(fix, out var isFixed))
            s.FixPosition = isFixed;

        if (v.TryGetValue("FloatingLocation", out var location) && TryParsePoint(location, out var x, out var y) && (x != 0 || y != 0))
        {
            s.PositionX = x;
            s.PositionY = y;
        }

        // v1 kept the adapter by name; v2 keys it by the adapter's GUID, which survives renames.
        bool auto = !v.TryGetValue("AutoSelectAdapter", out var a) || !bool.TryParse(a, out var isAuto) || isAuto;
        if (!auto && v.TryGetValue("SelectedAdapter", out var name) && !string.IsNullOrWhiteSpace(name))
        {
            var match = new SystemNetworkSource().GetAdapters().FirstOrDefault(n => n.Name == name);
            if (match is not null)
            {
                s.AdapterMode = AdapterMode.Manual;
                s.AdapterId = match.Id;
            }
        }

        // v1 showed transparent text on the taskbar; keep that look.
        s.Background = BackgroundStyle.None;
    }

    private static bool TryParseColor(string text, out string hex)
    {
        hex = string.Empty;
        try
        {
            if (TypeDescriptor.GetConverter(typeof(Color)).ConvertFromInvariantString(text) is Color c && !c.IsEmpty)
            {
                hex = ColorHex.ToHex(c.R, c.G, c.B);
                return true;
            }
        }
        catch (Exception e) when (e is ArgumentException or FormatException or NotSupportedException)
        {
        }
        return false;
    }

    private static bool TryParsePoint(string text, out int x, out int y)
    {
        x = y = 0;
        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y);
    }
}
