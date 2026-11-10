using WinternetMeter.Core;

namespace WinternetMeter.Core.Tests;

internal sealed class FakeTime : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;

    public void Advance(TimeSpan by) => _ticks += by.Ticks;
}

internal sealed class FakeNetwork : INetworkSource
{
    public List<AdapterInfo> Adapters { get; } = [];

    public Dictionary<string, (long Received, long Sent)> Counters { get; } = [];

    public int GetAdaptersCalls { get; private set; }

    public IReadOnlyList<AdapterInfo> GetAdapters()
    {
        GetAdaptersCalls++;
        return Adapters.ToList();
    }

    public bool TryReadCounters(string adapterId, out long received, out long sent)
    {
        if (Counters.TryGetValue(adapterId, out var c))
        {
            received = c.Received;
            sent = c.Sent;
            return true;
        }
        received = sent = 0;
        return false;
    }

    public void Add(long receivedBy, long sentBy, string id)
    {
        var c = Counters[id];
        Counters[id] = (c.Received + receivedBy, c.Sent + sentBy);
    }
}

internal sealed class FakeAutostart : IAutostart
{
    public bool IsEnabled { get; private set; }

    public void SetEnabled(bool enabled) => IsEnabled = enabled;

    public void Repair()
    {
    }
}

internal sealed class FakeHost : IMeterHost
{
    public FakeHost(NetworkSpeedMonitor monitor) => Monitor = monitor;

    public AppSettings Settings { get; } = new();

    public NetworkSpeedMonitor Monitor { get; }

    public IAutostart Autostart { get; } = new FakeAutostart();

    public bool SupportsFullscreenHide { get; set; } = true;

    public List<SettingsChange> Changes { get; } = [];

    public bool Exited { get; private set; }

    public void OnSettingsChanged(SettingsChange change) => Changes.Add(change);

    public void PickFont() { }

    public void PickTextColor() { }

    public void PickBackgroundColor() { }

    public void ResetPosition() { }

    public void OpenUrl(string url) { }

    public void Exit() => Exited = true;
}

internal static class Adapters
{
    public static AdapterInfo WiFi(bool gateway = true) =>
        new("{wifi}", "Wi-Fi", "Intel(R) Wi-Fi 6 AX201 160MHz", AdapterKind.Wireless, IsUp: true, gateway);

    public static AdapterInfo Ethernet(bool gateway = true, bool up = true) =>
        new("{eth}", "Ethernet", "Realtek PCIe GbE Family Controller", AdapterKind.Ethernet, up, gateway);

    public static AdapterInfo Wsl() =>
        new("{wsl}", "vEthernet (WSL (Hyper-V firewall))", "Hyper-V Virtual Ethernet Adapter", AdapterKind.Ethernet, IsUp: true, HasDefaultGateway: false);

    public static AdapterInfo Loopback() =>
        new("{lo}", "Loopback Pseudo-Interface 1", "Software Loopback Interface 1", AdapterKind.Loopback, IsUp: true, HasDefaultGateway: false);
}
