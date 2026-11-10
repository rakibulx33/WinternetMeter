namespace WinternetMeter.Core.Tests;

public class SpeedFormatterTests
{
    [Theory]
    [InlineData(0, "  0.0 KB/s")]
    [InlineData(512, "  0.5 KB/s")]
    [InlineData(1024, "  1.0 KB/s")]
    [InlineData(1024 * 999.9, "999.9 KB/s")]
    [InlineData(1024 * 1000, "  1.0 MB/s")]          // switches before needing 4 digits
    [InlineData(1024 * 1024 * 12.34, " 12.3 MB/s")]
    [InlineData(1024d * 1024 * 1024 * 2, "  2.0 GB/s")]
    public void Bytes(double bytesPerSecond, string expected) =>
        Assert.Equal(expected, SpeedFormatter.Format(bytesPerSecond, SpeedUnit.Bytes));

    [Theory]
    [InlineData(0, "  0.0 Kbps")]
    [InlineData(1_250, " 10.0 Kbps")]                // 1,250 B/s × 8 = 10,000 bit/s
    [InlineData(125_000, "  1.0 Mbps")]              // 1,000,000 bit/s
    [InlineData(6_250_000, " 50.0 Mbps")]            // a "50 Mbps" plan at full speed
    [InlineData(125_000_000, "  1.0 Gbps")]
    public void Bits_use_decimal_prefixes_like_isps(double bytesPerSecond, string expected) =>
        Assert.Equal(expected, SpeedFormatter.Format(bytesPerSecond, SpeedUnit.Bits));

    [Theory]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_values_show_zero(double bytesPerSecond) =>
        Assert.Equal("  0.0 KB/s", SpeedFormatter.Format(bytesPerSecond, SpeedUnit.Bytes));

    [Theory]
    [InlineData(SpeedUnit.Bytes)]
    [InlineData(SpeedUnit.Bits)]
    public void Output_is_always_ten_characters_so_the_widget_never_jitters(SpeedUnit unit)
    {
        var expected = SpeedFormatter.WidestSample(unit).Length;
        for (double b = 0; b < 1e13; b = b * 1.7 + 1)
            Assert.Equal(expected, SpeedFormatter.Format(b, unit).Length);
    }

    [Fact]
    public void Tooltip_shows_download_then_upload()
    {
        var text = SpeedFormatter.Tooltip(new SpeedReading(1024 * 1024, 1024), SpeedUnit.Bytes);
        Assert.Equal("↓ 1.0 MB/s   ↑ 1.0 KB/s", text);
    }
}
