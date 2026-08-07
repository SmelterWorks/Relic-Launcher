using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace RelicLauncher.Infrastructure.Servers;

internal static class LanSubnetAddressEnumerator
{
    public static IEnumerable<string> Collect(int port, int maxAddresses)
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up
                || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback)
            {
                continue;
            }

            foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork
                    || unicast.IPv4Mask is null)
                {
                    continue;
                }

                foreach (var host in EnumerateSubnetHosts(unicast.Address, unicast.IPv4Mask))
                {
                    results.Add($"{host}:{port}");
                    if (results.Count >= maxAddresses)
                    {
                        return results;
                    }
                }
            }
        }

        return results;
    }

    private static IEnumerable<IPAddress> EnumerateSubnetHosts(IPAddress address, IPAddress mask)
    {
        var ipBytes = address.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        if (ipBytes.Length != 4 || maskBytes.Length != 4)
        {
            return [];
        }

        var network = new byte[4];
        var broadcast = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            network[i] = (byte)(ipBytes[i] & maskBytes[i]);
            broadcast[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
        }

        var hosts = new List<IPAddress>(254);
        var current = (uint)network[0] << 24 | (uint)network[1] << 16 | (uint)network[2] << 8 | network[3];
        var last = (uint)broadcast[0] << 24 | (uint)broadcast[1] << 16 | (uint)broadcast[2] << 8 | broadcast[3];
        for (var value = current + 1; value < last && hosts.Count < 254; value++)
        {
            hosts.Add(new IPAddress([
                (byte)(value >> 24),
                (byte)(value >> 16),
                (byte)(value >> 8),
                (byte)value,
            ]));
        }

        return hosts;
    }
}
