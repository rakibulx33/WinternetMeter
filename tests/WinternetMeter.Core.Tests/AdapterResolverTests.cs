namespace WinternetMeter.Core.Tests;

public class AdapterResolverTests
{
    [Fact]
    public void Auto_ignores_wsl_and_hyperv_adapters_even_though_they_report_10_gbps()
    {
        var picked = AdapterResolver.AutoSelect([Adapters.Wsl(), Adapters.WiFi(), Adapters.Loopback()]);
        Assert.Equal(new[] { "{wifi}" }, picked.Select(a => a.Id));
    }

    [Fact]
    public void Auto_prefers_adapters_with_a_default_gateway()
    {
        var picked = AdapterResolver.AutoSelect([Adapters.WiFi(gateway: false), Adapters.Ethernet(gateway: true)]);
        Assert.Equal(new[] { "{eth}" }, picked.Select(a => a.Id));
    }

    [Fact]
    public void Auto_sums_when_several_adapters_are_routed()
    {
        var picked = AdapterResolver.AutoSelect([Adapters.WiFi(), Adapters.Ethernet()]);
        Assert.Equal(2, picked.Count);
    }

    [Fact]
    public void Auto_falls_back_to_real_adapters_when_none_has_a_gateway()
    {
        var picked = AdapterResolver.AutoSelect([Adapters.WiFi(gateway: false), Adapters.Wsl()]);
        Assert.Equal(new[] { "{wifi}" }, picked.Select(a => a.Id));
    }

    [Fact]
    public void Auto_skips_adapters_that_are_down()
    {
        var picked = AdapterResolver.AutoSelect([Adapters.Ethernet(up: false), Adapters.WiFi()]);
        Assert.Equal(new[] { "{wifi}" }, picked.Select(a => a.Id));
    }

    [Fact]
    public void Manual_uses_exactly_the_chosen_adapter()
    {
        var picked = AdapterResolver.Resolve([Adapters.WiFi(), Adapters.Wsl()], AdapterMode.Manual, "{wsl}");
        Assert.Equal(new[] { "{wsl}" }, picked.Select(a => a.Id));
    }

    [Fact]
    public void Manual_falls_back_to_auto_when_the_chosen_adapter_is_gone()
    {
        var picked = AdapterResolver.Resolve([Adapters.WiFi()], AdapterMode.Manual, "{usb-tether}");
        Assert.Equal(new[] { "{wifi}" }, picked.Select(a => a.Id));
    }

    [Theory]
    [InlineData("docker0")]
    [InlineData("br-1a2b3c")]
    [InlineData("veth9f1e")]
    [InlineData("virbr0")]
    [InlineData("tailscale0")]
    [InlineData("wg0")]
    [InlineData("tun0")]
    [InlineData("lo")]
    public void Linux_virtual_interfaces_are_virtual(string name) =>
        Assert.True(AdapterResolver.IsVirtual(new AdapterInfo(name, name, name, AdapterKind.Other, true, false)));

    [Theory]
    [InlineData("wlp2s0")]
    [InlineData("enp3s0")]
    [InlineData("eth0")]
    [InlineData("wlan0")]
    [InlineData("usb0")]
    public void Linux_physical_interfaces_are_not_virtual(string name) =>
        Assert.False(AdapterResolver.IsVirtual(new AdapterInfo(name, name, name, AdapterKind.Ethernet, true, true)));
}
