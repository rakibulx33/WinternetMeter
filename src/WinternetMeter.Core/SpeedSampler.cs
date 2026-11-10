namespace WinternetMeter.Core;

public readonly record struct SpeedReading(double DownloadBytesPerSecond, double UploadBytesPerSecond)
{
    public static SpeedReading Zero => default;
}

/// <summary>
/// Turns ever-growing byte counters into bytes/second.
///
/// speed = (counter now − counter before) / (seconds between the two readings)
///
/// Edge cases handled:
/// - first reading: nothing to compare with → 0
/// - counter went down (adapter reset, adapter set changed) → start over → 0
/// - huge gap (PC slept) → the average over the gap is meaningless → 0
/// </summary>
public sealed class SpeedSampler
{
    public static readonly TimeSpan MaxGap = TimeSpan.FromSeconds(30);

    private readonly Queue<SpeedReading> _history = new();
    private bool _hasBaseline;
    private long _lastReceived;
    private long _lastSent;
    private TimeSpan _lastAt;
    private int _window = 1;

    /// <summary>Number of readings to average (1 = no smoothing).</summary>
    public int SmoothingWindow
    {
        get => _window;
        set
        {
            _window = Math.Clamp(value, 1, 10);
            while (_history.Count > _window) _history.Dequeue();
        }
    }

    public void Reset()
    {
        _hasBaseline = false;
        _history.Clear();
    }

    /// <param name="received">Total bytes received so far.</param>
    /// <param name="sent">Total bytes sent so far.</param>
    /// <param name="at">Monotonic time of the reading.</param>
    public SpeedReading Add(long received, long sent, TimeSpan at)
    {
        bool usable = _hasBaseline
            && received >= _lastReceived
            && sent >= _lastSent
            && at > _lastAt
            && at - _lastAt <= MaxGap;

        SpeedReading reading = SpeedReading.Zero;
        if (usable)
        {
            double seconds = (at - _lastAt).TotalSeconds;
            reading = new SpeedReading((received - _lastReceived) / seconds, (sent - _lastSent) / seconds);
        }
        else
        {
            _history.Clear();
        }

        _hasBaseline = true;
        _lastReceived = received;
        _lastSent = sent;
        _lastAt = at;

        if (!usable) return SpeedReading.Zero;

        _history.Enqueue(reading);
        while (_history.Count > _window) _history.Dequeue();
        return new SpeedReading(
            _history.Average(r => r.DownloadBytesPerSecond),
            _history.Average(r => r.UploadBytesPerSecond));
    }
}
