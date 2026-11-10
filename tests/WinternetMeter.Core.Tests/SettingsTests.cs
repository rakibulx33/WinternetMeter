namespace WinternetMeter.Core.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wm-tests-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var store = new JsonSettingsStore(FilePath);
        Assert.False(store.Exists);

        var s = store.Load();
        Assert.Equal(AdapterMode.Auto, s.AdapterMode);
        Assert.Equal(SpeedUnit.Bytes, s.Unit);
        Assert.False(s.HasPosition);
    }

    [Fact]
    public void Round_trips_every_setting()
    {
        var store = new JsonSettingsStore(FilePath);
        var saved = new AppSettings
        {
            AdapterMode = AdapterMode.Manual,
            AdapterId = "{abc}",
            Unit = SpeedUnit.Bits,
            Smoothing = true,
            RefreshIntervalMs = 500,
            FontFamily = "Consolas",
            FontSize = 14,
            TextColor = "#00FF00",
            Background = BackgroundStyle.Pill,
            BackgroundColor = "#14213D",
            BackgroundOpacity = 0.7,
            PositionX = -1200,
            PositionY = 1040,
            FixPosition = true,
            HideInFullscreen = false,
        };

        Assert.True(store.Save(saved));
        var loaded = store.Load();

        Assert.Equal(AdapterMode.Manual, loaded.AdapterMode);
        Assert.Equal("{abc}", loaded.AdapterId);
        Assert.Equal(SpeedUnit.Bits, loaded.Unit);
        Assert.True(loaded.Smoothing);
        Assert.Equal(500, loaded.RefreshIntervalMs);
        Assert.Equal("Consolas", loaded.FontFamily);
        Assert.Equal(14, loaded.FontSize);
        Assert.Equal("#00FF00", loaded.TextColor);
        Assert.Equal(BackgroundStyle.Pill, loaded.Background);
        Assert.Equal("#14213D", loaded.BackgroundColor);
        Assert.Equal(0.7, loaded.BackgroundOpacity, 3);
        Assert.Equal(-1200, loaded.PositionX); // negative = monitor left of primary; must survive
        Assert.Equal(1040, loaded.PositionY);
        Assert.True(loaded.FixPosition);
        Assert.False(loaded.HideInFullscreen);
    }

    [Fact]
    public void File_is_readable_json_with_named_enums()
    {
        var store = new JsonSettingsStore(FilePath);
        store.Save(new AppSettings { Unit = SpeedUnit.Bits });
        var json = File.ReadAllText(FilePath);
        Assert.Contains("\"unit\": \"Bits\"", json);
    }

    [Fact]
    public void Corrupt_file_gives_defaults_and_is_kept_as_bad()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ this is not json");

        var s = new JsonSettingsStore(FilePath).Load();

        Assert.Equal(new AppSettings().FontSize, s.FontSize);
        Assert.True(File.Exists(FilePath + ".bad"));
    }

    [Fact]
    public void Normalize_clamps_hand_edited_values()
    {
        var s = new AppSettings
        {
            FontSize = 500,
            RefreshIntervalMs = 1,
            BackgroundOpacity = 7,
            TextColor = "not a color",
            BackgroundColor = "#abc",
            AdapterMode = AdapterMode.Manual,
            AdapterId = null,
            FontFamily = "   ",
        }.Normalize();

        Assert.Equal(AppSettings.MaxFontSize, s.FontSize);
        Assert.Equal(AppSettings.MinRefreshMs, s.RefreshIntervalMs);
        Assert.Equal(1.0, s.BackgroundOpacity);
        Assert.Equal("#FFFFFF", s.TextColor);
        Assert.Equal("#AABBCC", s.BackgroundColor);
        Assert.Equal(AdapterMode.Auto, s.AdapterMode);
        Assert.Null(s.FontFamily);
    }

    [Fact]
    public void Default_path_is_in_the_user_config_folder()
    {
        var path = JsonSettingsStore.DefaultPath();
        Assert.EndsWith("settings.json", path);
        Assert.Contains(OperatingSystem.IsWindows() ? "WinternetMeter" : "winternetmeter", path);
    }

    [Theory]
    [InlineData("#FF8800", true, 255, 136, 0)]
    [InlineData("ff8800", true, 255, 136, 0)]
    [InlineData("#F80", true, 255, 136, 0)]
    [InlineData("#80FF8800", true, 255, 136, 0)]
    [InlineData("#GG0000", false, 0, 0, 0)]
    [InlineData("", false, 0, 0, 0)]
    public void ColorHex_parses_common_forms(string text, bool ok, byte r, byte g, byte b)
    {
        Assert.Equal(ok, ColorHex.TryParse(text, out var pr, out var pg, out var pb));
        Assert.Equal((r, g, b), (pr, pg, pb));
    }
}
