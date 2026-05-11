using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

/// <summary>
/// Finds this machine's IPv4 addresses on real LAN adapters (Wi‑Fi / Ethernet).
/// </summary>
public static class LanAddressUtility
{
    /// <summary>Best guess at the address others on the same Wi‑Fi should use to reach this PC.</summary>
    public static string GetPrimaryIpv4()
    {
        foreach (var ip in GetLanIpv4Ordered())
            return ip;
        return "";
    }

    public static IReadOnlyList<string> GetAllLanIpv4()
    {
        return GetLanIpv4Ordered().ToList();
    }

    /// <summary>Human-readable lines for UI (connection popup).</summary>
    public static string BuildLanHintForUi(ushort defaultPort)
    {
        var list = GetLanIpv4Ordered().ToList();
        if (list.Count == 0)
        {
            return "This PC: no LAN IPv4 found. Check Wi‑Fi/Ethernet.\n" +
                   $"Use the same port on both computers (default {defaultPort}).";
        }

        string joined = string.Join("   •   ", list);
        return
            "On the OTHER computer: choose Client and set Address to one of these IPs:\n" +
            $"{joined}\n\n" +
            $"Keep port {defaultPort} on both unless you changed it.\n" +
            "On THIS computer: use Host (easiest for two PCs) or Server (dedicated server, no player here).";
    }

    /// <summary>
    /// UDP discovery send targets: global broadcast plus each LAN interface's subnet broadcast.
    /// Many Wi‑Fi routers drop <see cref="IPAddress.Broadcast"/> between wireless clients but still deliver subnet broadcasts.
    /// </summary>
    public static List<IPEndPoint> GetDiscoveryBroadcastEndpoints(ushort port)
    {
        var list = new List<IPEndPoint>();
        var seen = new HashSet<string>();

        void add(IPEndPoint ep)
        {
            string key = ep.Address.ToString() + ":" + ep.Port;
            if (seen.Add(key))
                list.Add(ep);
        }

        add(new IPEndPoint(IPAddress.Broadcast, port));

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;
                if (IPAddress.IsLoopback(ua.Address))
                    continue;

                IPAddress mask = null;
                try
                {
                    mask = ua.IPv4Mask;
                }
                catch
                {
                    // Some runtimes omit mask info.
                }

                if (mask == null)
                    mask = GuessIpv4Netmask(ua.Address);

                if (mask == null)
                    continue;

                IPAddress broadcast = ComputeIpv4Broadcast(ua.Address, mask);
                if (broadcast != null)
                    add(new IPEndPoint(broadcast, port));
            }
        }

        return list;
    }

    private static IPAddress GuessIpv4Netmask(IPAddress address)
    {
        byte[] b = address.GetAddressBytes();
        if (b.Length != 4)
            return null;

        if (b[0] == 192 && b[1] == 168)
            return IPAddress.Parse("255.255.255.0");
        if (b[0] == 10)
            return IPAddress.Parse("255.0.0.0");
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            return IPAddress.Parse("255.255.0.0");

        return IPAddress.Parse("255.255.255.0");
    }

    private static IPAddress ComputeIpv4Broadcast(IPAddress ip, IPAddress mask)
    {
        byte[] ipb = ip.GetAddressBytes();
        byte[] mb = mask.GetAddressBytes();
        if (ipb.Length != 4 || mb.Length != 4)
            return null;

        var bc = new byte[4];
        for (int i = 0; i < 4; i++)
            bc[i] = (byte)(ipb[i] | (~mb[i] & 0xFF));

        return new IPAddress(bc);
    }

    private static IEnumerable<string> GetLanIpv4Ordered()
    {
        var found = new List<string>();

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
                continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;
                if (IPAddress.IsLoopback(ua.Address))
                    continue;

                string s = ua.Address.ToString();
                if (!found.Contains(s))
                    found.Add(s);
            }
        }

        return found
            .OrderBy(ScoreLan)
            .ThenBy(s => s);
    }

    /// <summary>Lower = better for typical home / lab networks.</summary>
    private static int ScoreLan(string ip)
    {
        if (ip.StartsWith("192.168.")) return 0;
        if (ip.StartsWith("10.")) return 1;
        if (ip.StartsWith("172."))
        {
            var parts = ip.Split('.');
            if (parts.Length > 1 && int.TryParse(parts[1], out int second))
            {
                if (second >= 16 && second <= 31)
                    return 2;
            }
        }

        return 10;
    }
}
