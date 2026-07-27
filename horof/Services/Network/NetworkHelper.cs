using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace horof.Services.Network;

public static class NetworkHelper
{
    public const int DefaultPort = 5050;

    public static string? GetLocalIPv4()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
                continue;

            if (networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel)
                continue;

            foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                var ip = address.Address.ToString();
                if (IPAddress.IsLoopback(address.Address))
                    continue;

                if (ip.StartsWith("169.254.", StringComparison.Ordinal))
                    continue;

                return ip;
            }
        }

        return null;
    }

    public static string FormatHostAddress(string? hostInput, int port = DefaultPort)
    {
        if (string.IsNullOrWhiteSpace(hostInput))
            return $"127.0.0.1:{port}";

        var trimmed = hostInput.Trim();
        if (trimmed.Contains(':'))
            return trimmed;

        return $"{trimmed}:{port}";
    }

    public static (string Host, int Port) ParseHostAddress(string hostAddress)
    {
        var formatted = FormatHostAddress(hostAddress);
        var separator = formatted.LastIndexOf(':');
        if (separator <= 0 || separator == formatted.Length - 1)
            return (formatted, DefaultPort);

        var host = formatted[..separator];
        if (!int.TryParse(formatted[(separator + 1)..], out var port))
            port = DefaultPort;

        return (host, port);
    }
}
