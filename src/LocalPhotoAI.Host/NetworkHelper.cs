using System.Net;
using System.Net.Sockets;

namespace LocalPhotoAI.Host;

public static class NetworkHelper
{
    public static string? GetLanIpAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                ?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
