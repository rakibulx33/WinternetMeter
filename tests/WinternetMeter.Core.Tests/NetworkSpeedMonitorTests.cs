namespace WinternetMeter.Core.Tests;

public class NetworkSpeedMonitorTests
{
    private readonly FakeNetwork _net = new();
    private readonly FakeTime _time = new();

    private NetworkSpeedMonitor Create() => new(_net, _time, watchSystemChanges: false);

    [Fact]
    public void Measures_the_auto_selected_adapter()
    {
        _net.Adapters.AddRange([Adapters.WiFi(), Adapters.Wsl()]);
        _net.Counters["{wifi}"] = (0, 0);
        _net.Counters["{wsl}"] = (0, 0);
        using var monitor = Create();

        monitor.Tick();
        _time.Advance(TimeSpan.FromSeconds(1));
        _net.Add(2_048, 1_024, "{wifi}");
        _net.Add(999_999, 999_999, "{wsl}"); // must be ignored
        var r = monitor.Tick();

        Assert.Equal(2_048, r.DownloadBytesPerSecond, 3);
        Assert.Equal(1_024, r.UploadBytesPerSecond, 3);
    }

    [Fact]
    public void Sums_all_routed_adapters()
    {
        _net.Adapters.AddRange([Adapters.WiFi(), Adapters.Ethernet()]);
        _net.Counters["{wifi}"] = (0, 0);
        _net.Counters["{eth}"] = (0, 0);
        using var monitor = Create();

        monitor.Tick();
        _time.Advance(TimeSpan.FromSeconds(1));
        _net.Add(100, 0, "{wifi}");
        _net.Add(200, 0, "{eth}");

        Assert.Equal(300, monitor.Tick().DownloadBytesPerSecond, 3);
    }

    [Fact]
    public void Recovers_when_the_adapter_disappears_and_comes_back()
    {
        _net.Adapters.Add(Adapters.WiFi());
        _net.Counters["{wifi}"] = (1_000, 0);
        using var monitor = Create();
        monitor.Tick();

        // Wi-Fi drops: counters unreadable → zero, and the monitor re-resolves next tick.
        _net.Counters.Remove("{wifi}");
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(SpeedReading.Zero, monitor.Tick());

        // Wi-Fi back with counters reset to a lower value.
        _net.Counters["{wifi}"] = (10, 0);
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(SpeedReading.Zero, monitor.Tick()); // new baseline

        _net.Add(500, 0, "{wifi}");
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(500, monitor.Tick().DownloadBytesPerSecond, 3);
    }

    [Fact]
    public void Re_resolves_periodically_to_catch_unannounced_changes()
    {
        _net.Adapters.Add(Adapters.WiFi());
        _net.Counters["{wifi}"] = (0, 0);
        using var monitor = Create();

        monitor.Tick();
        int calls = _net.GetAdaptersCalls;
        _time.Advance(TimeSpan.FromSeconds(1));
        monitor.Tick();
        Assert.Equal(calls, _net.GetAdaptersCalls); // not every tick

        _time.Advance(NetworkSpeedMonitor.PeriodicResolve);
        monitor.Tick();
        Assert.Equal(calls + 1, _net.GetAdaptersCalls);
    }

    [Fact]
    public void Raises_AdaptersChanged_when_the_measured_set_changes()
    {
        _net.Adapters.Add(Adapters.WiFi());
        _net.Counters["{wifi}"] = (0, 0);
        _net.Counters["{eth}"] = (0, 0);
        using var monitor = Create();
        int raised = 0;
        monitor.AdaptersChanged += (_, _) => raised++;

        monitor.Tick();
        Assert.Equal(1, raised);

        _net.Adapters.Add(Adapters.Ethernet());
        monitor.Invalidate();
        monitor.Tick();
        Assert.Equal(2, raised);
        Assert.Equal(2, monitor.ActiveAdapters.Count);
    }

    [Fact]
    public void Switching_to_manual_measures_only_that_adapter()
    {
        _net.Adapters.AddRange([Adapters.WiFi(), Adapters.Ethernet()]);
        _net.Counters["{wifi}"] = (0, 0);
        _net.Counters["{eth}"] = (0, 0);
        using var monitor = Create();

        monitor.Configure(AdapterMode.Manual, "{eth}");
        monitor.Tick();
        _time.Advance(TimeSpan.FromSeconds(1));
        _net.Add(100, 0, "{wifi}");
        _net.Add(700, 0, "{eth}");

        Assert.Equal(700, monitor.Tick().DownloadBytesPerSecond, 3);
    }

    [Fact]
    public void No_adapters_gives_zero()
    {
        using var monitor = Create();
        Assert.Equal(SpeedReading.Zero, monitor.Tick());
        Assert.Empty(monitor.ActiveAdapters);
    }
}
