using System.Net.NetworkInformation;

namespace WinternetMeter.Core;

/// <summary>
/// Glue between adapters, counters and the sampler. The UI calls <see cref="Tick"/>
/// on its timer; everything happens on the UI thread, so no locking is needed.
///
/// Adapters are re-resolved when:
/// - the OS reports a network change (Wi-Fi switch, cable, VPN, resume from sleep)
/// - a counter read fails (adapter vanished)
/// - every 10 s anyway (cheap; catches changes the OS didn't announce)
/// </summary>
public sealed class NetworkSpeedMonitor : IDisposable
{
    public static readonly TimeSpan PeriodicResolve = TimeSpan.FromSeconds(10);

    private readonly INetworkSource _source;
    private readonly TimeProvider _time;
    private readonly long _startTimestamp;
    private readonly bool _watchingSystem;
    private readonly SpeedSampler _sampler = new();

    private volatile bool _dirty = true;
    private TimeSpan _lastResolveAt = TimeSpan.Zero; // first Tick resolves anyway (_dirty starts true)
    private string _activeKey = string.Empty;
    private string _allKey = string.Empty;

    public NetworkSpeedMonitor(INetworkSource source, TimeProvider? time = null, bool watchSystemChanges = true)
    {
        _source = source;
        _time = time ?? TimeProvider.System;
        _startTimestamp = _time.GetTimestamp();

        if (watchSystemChanges)
        {
            try
            {
                NetworkChange.NetworkAddressChanged += OnSystemChange;
                NetworkChange.NetworkAvailabilityChanged += OnSystemAvailabilityChange;
                _watchingSystem = true;
            }
            catch (PlatformNotSupportedException)
            {
                // Periodic re-resolve still covers it.
            }
        }
    }

    public AdapterMode Mode { get; private set; } = AdapterMode.Auto;

    public string? ManualAdapterId { get; private set; }

    /// <summary>Every adapter the OS reported at the last resolve.</summary>
    public IReadOnlyList<AdapterInfo> AllAdapters { get; private set; } = [];

    /// <summary>The adapter(s) currently being measured.</summary>
    public IReadOnlyList<AdapterInfo> ActiveAdapters { get; private set; } = [];

    public int SmoothingWindow
    {
        get => _sampler.SmoothingWindow;
        set => _sampler.SmoothingWindow = value;
    }

    /// <summary>Raised (on the caller's thread, from <see cref="Tick"/>) when the adapter list or the measured set changes.</summary>
    public event EventHandler? AdaptersChanged;

    public void Configure(AdapterMode mode, string? manualAdapterId)
    {
        Mode = mode;
        ManualAdapterId = manualAdapterId;
        Invalidate();
    }

    /// <summary>Forces a re-resolve on the next tick (call after sleep/resume).</summary>
    public void Invalidate() => _dirty = true;

    public SpeedReading Tick()
    {
        var now = _time.GetElapsedTime(_startTimestamp);
        if (_dirty || now - _lastResolveAt >= PeriodicResolve)
            ResolveNow(now);

        if (ActiveAdapters.Count == 0)
            return SpeedReading.Zero;

        long received = 0, sent = 0;
        foreach (var adapter in ActiveAdapters)
        {
            if (!_source.TryReadCounters(adapter.Id, out var r, out var s))
            {
                _dirty = true;
                _sampler.Reset();
                return SpeedReading.Zero;
            }
            received += r;
            sent += s;
        }
        return _sampler.Add(received, sent, now);
    }

    private void ResolveNow(TimeSpan now)
    {
        _dirty = false;
        _lastResolveAt = now;

        var all = _source.GetAdapters();
        var active = AdapterResolver.Resolve(all, Mode, ManualAdapterId);
        AllAdapters = all;
        ActiveAdapters = active;

        var activeKey = string.Join('|', active.Select(a => a.Id).Order(StringComparer.Ordinal));
        var allKey = string.Join('|', all.Select(a => $"{a.Id}:{a.IsUp}:{a.HasDefaultGateway}").Order(StringComparer.Ordinal));

        bool activeChanged = activeKey != _activeKey;
        bool listChanged = allKey != _allKey;
        _activeKey = activeKey;
        _allKey = allKey;

        // Different adapters = different counters; the old baseline is meaningless.
        if (activeChanged) _sampler.Reset();
        if (activeChanged || listChanged) AdaptersChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSystemChange(object? sender, EventArgs e) => _dirty = true;

    private void OnSystemAvailabilityChange(object? sender, NetworkAvailabilityEventArgs e) => _dirty = true;

    public void Dispose()
    {
        if (!_watchingSystem) return;
        NetworkChange.NetworkAddressChanged -= OnSystemChange;
        NetworkChange.NetworkAvailabilityChanged -= OnSystemAvailabilityChange;
    }
}
