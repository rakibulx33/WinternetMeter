using System.Text.RegularExpressions;

namespace WinternetMeter.Core;

public enum AdapterMode
{
    /// <summary>Measure whatever adapter(s) carry internet traffic right now.</summary>
    Auto,

    /// <summary>Measure one adapter the user picked.</summary>
    Manual,
}

/// <summary>
/// Decides which adapters to measure.
///
/// Auto mode = adapters that are up, not virtual, and have a default gateway
/// (i.e. the ones internet traffic actually leaves through). If several qualify
/// (Wi-Fi + Ethernet both connected), their traffic is summed.
///
/// Why not "fastest adapter": WSL/Hyper-V/Docker virtual adapters report 10 Gbps
/// link speed, so sorting by speed picks them and the meter shows 0.
/// </summary>
public static partial class AdapterResolver
{
    // Windows: matched against name and description.
    // Linux: interface-name prefixes for containers, VMs and VPN tunnels.
    // VPN tunnels are excluded on purpose: their traffic is also counted (encrypted)
    // on the physical adapter, so including both would double-count.
    [GeneratedRegex(
        @"vEthernet|Hyper-V|\bWSL\b|VirtualBox|VMware|Virtual Adapter|Virtual Ethernet|TAP-|Wintun|WireGuard|Npcap|Loopback|Teredo|isatap|6to4|Wi-Fi Direct|Bluetooth|Tailscale|ZeroTier" +
        // Linux names are lowercase: match them case-sensitively so a Windows adapter
        // called "Local Area Connection" isn't mistaken for "lo".
        @"|(?-i:^(lo$|docker\d*|br-|veth|virbr|vboxnet|vmnet|tun\d|tap\d|wg\d|zt|tailscale|cni|flannel|cali|lxcbr|lxdbr|podman|vnet|ifb|dummy|sit\d))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VirtualPattern();

    public static bool IsVirtual(AdapterInfo adapter) =>
        adapter.Kind is AdapterKind.Loopback or AdapterKind.Tunnel
        || VirtualPattern().IsMatch(adapter.Name)
        || VirtualPattern().IsMatch(adapter.Description);

    /// <summary>Adapters Auto mode would measure.</summary>
    public static IReadOnlyList<AdapterInfo> AutoSelect(IEnumerable<AdapterInfo> adapters)
    {
        var candidates = adapters.Where(a => a.IsUp && !IsVirtual(a)).ToList();
        var routed = candidates.Where(a => a.HasDefaultGateway).ToList();
        // No gateway anywhere (e.g. IPv6-only quirks, captive setups): measure all real adapters
        // rather than showing a permanent zero.
        return routed.Count > 0 ? routed : candidates;
    }

    /// <summary>Adapters to measure for the given mode. A missing or down manual adapter falls back to Auto.</summary>
    public static IReadOnlyList<AdapterInfo> Resolve(IEnumerable<AdapterInfo> adapters, AdapterMode mode, string? manualAdapterId)
    {
        var all = adapters as IReadOnlyCollection<AdapterInfo> ?? adapters.ToList();
        if (mode == AdapterMode.Manual && !string.IsNullOrEmpty(manualAdapterId))
        {
            var manual = all.FirstOrDefault(a => a.Id == manualAdapterId && a.IsUp);
            if (manual is not null)
                return [manual];
        }
        return AutoSelect(all);
    }
}
