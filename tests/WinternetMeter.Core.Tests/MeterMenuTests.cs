namespace WinternetMeter.Core.Tests;

public class MeterMenuTests
{
    private readonly FakeNetwork _net = new();

    private FakeHost CreateHost()
    {
        _net.Adapters.AddRange([Adapters.WiFi(), Adapters.Wsl(), Adapters.Loopback()]);
        _net.Counters["{wifi}"] = (0, 0);
        var monitor = new NetworkSpeedMonitor(_net, new FakeTime(), watchSystemChanges: false);
        monitor.Tick();
        return new FakeHost(monitor);
    }

    private static MenuNode Find(IEnumerable<MenuNode> nodes, params string[] path)
    {
        MenuNode? node = null;
        foreach (var part in path)
        {
            node = nodes.First(n => n.Text.StartsWith(part, StringComparison.Ordinal));
            nodes = node.Children ?? [];
        }
        return node!;
    }

    [Fact]
    public void Header_shows_version()
    {
        var menu = MeterMenu.Build(CreateHost(), "2.0.0");
        Assert.Equal("Winternet Meter 2.0.0", menu[0].Text);
    }

    [Fact]
    public void Choosing_mbps_switches_units_and_notifies_appearance_change()
    {
        var host = CreateHost();
        Find(MeterMenu.Build(host, "x"), "Units", "Mbps").OnClick!();

        Assert.Equal(SpeedUnit.Bits, host.Settings.Unit);
        Assert.Equal(new[] { SettingsChange.Appearance }, host.Changes);
        Assert.True(Find(MeterMenu.Build(host, "x"), "Units", "Mbps").IsChecked);
    }

    [Fact]
    public void Adapter_menu_lists_real_adapters_first_and_marks_virtual_ones()
    {
        var host = CreateHost();
        var adapters = Find(MeterMenu.Build(host, "x"), "Network adapter").Children!;

        Assert.True(adapters[0].IsChecked); // Auto
        Assert.StartsWith("Measuring: Wi-Fi", adapters[1].Text);

        var choices = adapters.Skip(3).Select(n => n.Text).ToList();
        Assert.Equal("Wi-Fi", choices[0]);
        Assert.EndsWith("(virtual)", choices[1]);
        Assert.DoesNotContain(choices, c => c.Contains("Loopback"));
    }

    [Fact]
    public void Picking_an_adapter_switches_to_manual()
    {
        var host = CreateHost();
        Find(MeterMenu.Build(host, "x"), "Network adapter", "Wi-Fi").OnClick!();

        Assert.Equal(AdapterMode.Manual, host.Settings.AdapterMode);
        Assert.Equal("{wifi}", host.Settings.AdapterId);
        Assert.Equal(new[] { SettingsChange.Adapter }, host.Changes);
    }

    [Fact]
    public void Fullscreen_option_only_where_supported()
    {
        var host = CreateHost();
        host.SupportsFullscreenHide = false;
        Assert.DoesNotContain(MeterMenu.Build(host, "x"), n => n.Text.StartsWith("Hide in fullscreen"));
    }

    [Fact]
    public void Run_on_startup_toggles_autostart()
    {
        var host = CreateHost();
        Find(MeterMenu.Build(host, "x"), "Run on startup").OnClick!();
        Assert.True(host.Autostart.IsEnabled);
    }

    [Fact]
    public void Box_color_turns_the_background_on()
    {
        var host = CreateHost();
        Find(MeterMenu.Build(host, "x"), "Appearance", "Background", "Box color: Navy").OnClick!();

        Assert.Equal(BackgroundStyle.Pill, host.Settings.Background);
        Assert.Equal("#14213D", host.Settings.BackgroundColor);
    }

    [Fact]
    public void Exit_calls_host()
    {
        var host = CreateHost();
        MeterMenu.Build(host, "x").Last().OnClick!();
        Assert.True(host.Exited);
    }
}
