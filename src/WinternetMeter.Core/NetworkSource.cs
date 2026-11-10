using System.Net;
using System.Net.NetworkInformation;

namespace WinternetMeter.Core;

/// <summary>Where adapter lists and byte counters come from. Faked in tests.</summary>
public interface INetworkSource
{
    IReadOnlyList<AdapterInfo> GetAdapters();

    /// <summary>Total bytes received/sent by the adapter since it came up. False if the adapter is gone.</summary>
    bool TryReadCounters(string adapterId, out long received, out long sent);
}

/// <summary>
/// Real implementation on top of .NET's NetworkInterface API.
/// Windows: GetIfEntry2 under the hood. Linux: /proc/net/dev and /proc/net/route.
/// </summary>
public sealed class SystemNetworkSource : INetworkSource
{
    private Dictionary<string, NetworkInterface> _byId = new();

    public IReadOnlyList<AdapterInfo> GetAdapters()
    {
        NetworkInterface[] nics;
        try
        {
            nics = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            return [];
        }

        var map = new Dictionary<string, NetworkInterface>(nics.Length);
        var list = new List<AdapterInfo>(nics.Length);
        foreach (var nic in nics)
        {
            map[nic.Id] = nic;
            list.Add(new AdapterInfo(
                nic.Id,
                nic.Name,
                SafeDescription(nic),
                MapKind(nic.NetworkInterfaceType),
                nic.OperationalStatus == OperationalStatus.Up,
                HasDefaultGateway(nic)));
        }
        _byId = map;
        return list;
    }

    public bool TryReadCounters(string adapterId, out long received, out long sent)
    {
        received = sent = 0;
        if (!_byId.TryGetValue(adapterId, out var nic)) return false;
        try
        {
            // GetIPStatistics (not GetIPv4Statistics): counts IPv6 too and is supported on Linux.
            var stats = nic.GetIPStatistics();
            received = stats.BytesReceived;
            sent = stats.BytesSent;
            return true;
        }
        catch (Exception e) when (e is NetworkInformationException or PlatformNotSupportedException or IOException or InvalidOperationException)
        {
            return false;
        }
    }

    private static string SafeDescription(NetworkInterface nic)
    {
        try { return nic.Description ?? string.Empty; }
        catch (PlatformNotSupportedException) { return string.Empty; }
    }

    private static bool HasDefaultGateway(NetworkInterface nic)
    {
        try
        {
            return nic.GetIPProperties().GatewayAddresses.Any(g =>
                g.Address is { } a && !a.Equals(IPAddress.Any) && !a.Equals(IPAddress.IPv6Any));
        }
        catch (Exception e) when (e is NetworkInformationException or PlatformNotSupportedException or IOException)
        {
            return false;
        }
    }

    private static AdapterKind MapKind(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Loopback => AdapterKind.Loopback,
        NetworkInterfaceType.Tunnel => AdapterKind.Tunnel,
        NetworkInterfaceType.Wireless80211 => AdapterKind.Wireless,
        NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2 => AdapterKind.Cellular,
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.Ethernet3Megabit or NetworkInterfaceType.FastEthernetFx
            or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.GigabitEthernet => AdapterKind.Ethernet,
        _ => AdapterKind.Other,
    };
}
