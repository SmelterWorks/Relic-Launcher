using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using RelicLauncher.Core.Server;

namespace RelicLauncher.Infrastructure.Server;

public static class ServerListenEndpointResolver
{
    public static IReadOnlyList<string> Resolve(string serverDataPath)
    {
        var port = VintageStoryServerConfigReader.TryReadPort(serverDataPath) ?? VintageStoryServerConfigReader.DefaultPort;
        var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"127.0.0.1:{port}",
            $"[::1]:{port}",
        };

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up
                    || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                foreach (var address in nic.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
                    {
                        continue;
                    }

                    if (IPAddress.IsLoopback(address.Address))
                    {
                        continue;
                    }

                    if (address.Address.IsIPv6LinkLocal)
                    {
                        continue;
                    }

                    addresses.Add(FormatAddress(address.Address, port));
                }
            }
        }
        catch (NetworkInformationException)
        {
        }

        return addresses.OrderBy(static a => a, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string FormatAddress(IPAddress address, int port)
    {
        return address.AddressFamily == AddressFamily.InterNetworkV6
            ? $"[{address}]:{port}"
            : $"{address}:{port}";
    }
}
