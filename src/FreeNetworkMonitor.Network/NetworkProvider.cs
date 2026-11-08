using System.Net.NetworkInformation;
using System.Net.Sockets;
using FreeNetworkMonitor.Core;
namespace FreeNetworkMonitor.Network;
public sealed class NetworkProvider : INetworkProvider
{
    public NetworkState Get()
    {
        try
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up &&
                n.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
                .Select(n => (Nic: n, Props: n.GetIPProperties())).ToArray();
            var choice = candidates.OrderByDescending(n => n.Props.GatewayAddresses.Count > 0)
                .ThenBy(n => n.Props.GetIPv4Properties()?.Index ?? int.MaxValue).FirstOrDefault();
            if (choice.Nic is null) return new("", "Aucune interface active", "Hors ligne", null, false);
            var gateway = choice.Props.GatewayAddresses.Select(g => g.Address).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ??
                choice.Props.GatewayAddresses.Select(g => g.Address).FirstOrDefault();
            return new(choice.Nic.Id, choice.Nic.Name, choice.Nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" :
                choice.Nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? "Ethernet" : choice.Nic.NetworkInterfaceType.ToString(), gateway?.ToString(), gateway is not null);
        }
        catch (NetworkInformationException) { return new("", "Informations réseau indisponibles", "Inconnu", null, false); }
    }
}
