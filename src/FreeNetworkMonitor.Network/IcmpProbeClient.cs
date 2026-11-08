using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using FreeNetworkMonitor.Core;
namespace FreeNetworkMonitor.Network;
public sealed class IcmpProbeClient : IProbeClient
{
    public async Task<Probe> SendAsync(Target target, NetworkState network, int timeoutMs, CancellationToken cancellationToken)
    {
        var at = DateTimeOffset.UtcNow;
        string host = target.Kind == TargetKind.Gateway ? network.Gateway ?? "" : target.Host;
        IPAddress? address = null;
        Probe Result(ProbeOutcome outcome, double? rtt = null, string? detail = null) => new(at, target.Id, target.Name,
            address?.ToString() ?? host, network.Id, outcome, rtt, detail, "ICMP", address?.AddressFamily == AddressFamily.InterNetworkV6 ? 6 : 4, target.Kind);
        if (!network.Online || string.IsNullOrEmpty(host)) return Result(ProbeOutcome.Offline, detail: "Interface ou passerelle indisponible");
        try
        {
            if (!IPAddress.TryParse(host, out address))
            {
                using var dnsTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                dnsTimeout.CancelAfter(timeoutMs);
                try { address = (await Dns.GetHostAddressesAsync(host, dnsTimeout.Token)).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ??
                    (await Dns.GetHostAddressesAsync(host, dnsTimeout.Token)).FirstOrDefault(); }
                catch (Exception e) when (e is SocketException || e is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                { return Result(ProbeOutcome.DnsError, detail: "Résolution DNS impossible ou expirée"); }
            }
            if (address is null) return Result(ProbeOutcome.DnsError, detail: "Aucune adresse DNS");
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, TimeSpan.FromMilliseconds(timeoutMs), new byte[32], cancellationToken: cancellationToken);
            return reply.Status switch
            {
                IPStatus.Success => Result(ProbeOutcome.Success, reply.RoundtripTime),
                IPStatus.TimedOut => Result(ProbeOutcome.Timeout, detail: reply.Status.ToString()),
                IPStatus.DestinationHostUnreachable or IPStatus.DestinationNetworkUnreachable or IPStatus.DestinationUnreachable =>
                    Result(ProbeOutcome.Unreachable, detail: reply.Status.ToString()),
                _ => Result(ProbeOutcome.LocalError, detail: reply.Status.ToString())
            };
        }
        catch (OperationCanceledException) { return Result(ProbeOutcome.Cancelled); }
        catch (Exception e) when (e is PingException or SocketException or InvalidOperationException or PlatformNotSupportedException or System.Security.SecurityException)
        { return Result(ProbeOutcome.LocalError, detail: e.GetType().Name + ": " + e.Message); }
    }
}
