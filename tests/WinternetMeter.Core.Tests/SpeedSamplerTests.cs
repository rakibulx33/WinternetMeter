namespace WinternetMeter.Core.Tests;

public class SpeedSamplerTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void First_reading_is_zero_because_there_is_nothing_to_compare_with()
    {
        var sampler = new SpeedSampler();
        Assert.Equal(SpeedReading.Zero, sampler.Add(5_000, 1_000, S(1)));
    }

    [Fact]
    public void Speed_is_byte_difference_divided_by_elapsed_seconds()
    {
        var sampler = new SpeedSampler();
        sampler.Add(1_000, 500, S(1));
        var r = sampler.Add(3_000, 1_500, S(3)); // +2000 / +1000 over 2 s

        Assert.Equal(1_000, r.DownloadBytesPerSecond, 3);
        Assert.Equal(500, r.UploadBytesPerSecond, 3);
    }

    [Fact]
    public void Counter_going_down_resets_instead_of_reporting_negative_speed()
    {
        var sampler = new SpeedSampler();
        sampler.Add(10_000, 10_000, S(1));
        Assert.Equal(SpeedReading.Zero, sampler.Add(100, 100, S(2)));

        // Next reading uses the new baseline.
        var r = sampler.Add(1_100, 600, S(3));
        Assert.Equal(1_000, r.DownloadBytesPerSecond, 3);
        Assert.Equal(500, r.UploadBytesPerSecond, 3);
    }

    [Fact]
    public void Long_gap_such_as_sleep_gives_zero_not_a_meaningless_average()
    {
        var sampler = new SpeedSampler();
        sampler.Add(0, 0, S(1));
        Assert.Equal(SpeedReading.Zero, sampler.Add(1_000_000, 0, S(1) + SpeedSampler.MaxGap + S(1)));
    }

    [Fact]
    public void Same_timestamp_gives_zero_instead_of_dividing_by_zero()
    {
        var sampler = new SpeedSampler();
        sampler.Add(0, 0, S(1));
        Assert.Equal(SpeedReading.Zero, sampler.Add(100, 100, S(1)));
    }

    [Fact]
    public void Smoothing_averages_the_last_n_readings()
    {
        var sampler = new SpeedSampler { SmoothingWindow = 3 };
        sampler.Add(0, 0, S(0));
        sampler.Add(300, 0, S(1));   // 300
        sampler.Add(600, 0, S(2));   // 300
        var r = sampler.Add(1_500, 0, S(3)); // 900 → avg(300,300,900) = 500

        Assert.Equal(500, r.DownloadBytesPerSecond, 3);

        r = sampler.Add(1_500, 0, S(4)); // 0 → avg(300,900,0) = 400
        Assert.Equal(400, r.DownloadBytesPerSecond, 3);
    }

    [Fact]
    public void Reset_forgets_the_baseline()
    {
        var sampler = new SpeedSampler();
        sampler.Add(0, 0, S(1));
        sampler.Reset();
        Assert.Equal(SpeedReading.Zero, sampler.Add(1_000, 0, S(2)));
    }
}
