namespace WinternetMeter.Core;

public enum AdapterKind
{
    Other,
    Ethernet,
    Wireless,
    Cellular,
    Loopback,
    Tunnel,
}

/// <summary>A snapshot of one network adapter, independent of the OS API that produced it.</summary>
/// <param name="Id">Stable identifier: a GUID on Windows, the interface name (e.g. <c>wlp2s0</c>) on Linux.</param>
/// <param name="Name">Friendly name ("Wi-Fi", "Ethernet", "wlp2s0").</param>
/// <param name="Description">Driver/hardware description ("Intel(R) Wi-Fi 6 AX201", "Hyper-V Virtual Ethernet Adapter").</param>
/// <param name="HasDefaultGateway">True when traffic to the internet is routed through this adapter.</param>
public sealed record AdapterInfo(
    string Id,
    string Name,
    string Description,
    AdapterKind Kind,
    bool IsUp,
    bool HasDefaultGateway);
