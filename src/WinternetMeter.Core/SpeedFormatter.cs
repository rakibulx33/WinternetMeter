using System.Globalization;

namespace WinternetMeter.Core;

public enum SpeedUnit
{
    /// <summary>KB/s, MB/s — what download managers show (1 KB = 1024 bytes).</summary>
    Bytes,

    /// <summary>Kbps, Mbps — what ISPs and speed tests advertise (bits, 1 Mbps = 1,000,000 bits/s).</summary>
    Bits,
}

/// <summary>
/// Formats speeds as fixed-width text ("  12.3 MB/s" is always 10 characters)
/// so the widget never changes size while numbers change.
/// </summary>
public static class SpeedFormatter
{
    private static readonly string[] ByteUnits = ["KB/s", "MB/s", "GB/s", "TB/s"];
    private static readonly string[] BitUnits = ["Kbps", "Mbps", "Gbps", "Tbps"];

    public static string Format(double bytesPerSecond, SpeedUnit unit)
    {
        if (double.IsNaN(bytesPerSecond) || double.IsInfinity(bytesPerSecond) || bytesPerSecond < 0)
            bytesPerSecond = 0;

        string[] labels;
        double step;
        double value;
        if (unit == SpeedUnit.Bits)
        {
            labels = BitUnits;
            step = 1000;
            value = bytesPerSecond * 8 / 1000;
        }
        else
        {
            labels = ByteUnits;
            step = 1024;
            value = bytesPerSecond / 1024;
        }

        // Switch unit before the number would need 4 integer digits ("1000.0"),
        // which keeps the number part at most 5 characters wide.
        int i = 0;
        while (value >= 999.95 && i < labels.Length - 1)
        {
            value /= step;
            i++;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0,5:0.0} {1}", value, labels[i]);
    }

    /// <summary>The widest text <see cref="Format"/> can produce, for sizing the widget once.</summary>
    public static string WidestSample(SpeedUnit unit) => unit == SpeedUnit.Bits ? "888.8 Mbps" : "888.8 MB/s";

    /// <summary>Short one-line summary for tray tooltips.</summary>
    public static string Tooltip(SpeedReading reading, SpeedUnit unit) =>
        $"↓ {Format(reading.DownloadBytesPerSecond, unit).Trim()}   ↑ {Format(reading.UploadBytesPerSecond, unit).Trim()}";
}
