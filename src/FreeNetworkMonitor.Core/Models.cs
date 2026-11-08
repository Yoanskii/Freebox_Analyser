namespace FreeNetworkMonitor.Core;
public enum ProbeOutcome { Success, Timeout, Unreachable, DnsError, LocalError, Offline, Cancelled }
public enum TargetKind { Gateway, External, Personal }
public sealed record Target(string Id, string Name, string Host, TargetKind Kind = TargetKind.External, bool Enabled = true);
public sealed record Probe(DateTimeOffset At, string TargetId, string TargetName, string Address, string InterfaceId,
    ProbeOutcome Outcome, double? RttMs = null, string? Detail = null, string Protocol = "ICMP", int IpVersion = 4)
{
    public bool Eligible => Outcome is ProbeOutcome.Success or ProbeOutcome.Timeout;
}
public sealed record NetworkState(string Id, string Name, string Kind, string? Gateway, bool Online);
public sealed record NetworkEvent(DateTimeOffset At, string Kind, string Message);
public sealed record Game(string Id, string Name, string ProcessName, string? Path = null, bool Enabled = true);
public sealed record Session(string Id, string Game, DateTimeOffset Start, DateTimeOffset? End,
    DateTimeOffset BaselineStart, string State = "En cours", string? Executable = null, string? GameId = null);
public sealed record Incident(string Id, string TargetId, DateTimeOffset Start, DateTimeOffset? End, string Reason,
    int Eligible, int Failed, double? P95Ms);
public sealed record Statistics(int Total, int Sent, int Received, int Failed, int Invalid, double? LossPercent,
    double? MeanMs, double? MedianMs, double? P95Ms, double? MinMs, double? MaxMs, double? JitterMs,
    int MaxConsecutiveFailures, string Quality, DateTimeOffset? First, DateTimeOffset? Last);
public sealed class Settings
{
    public int IntervalMs { get; set; } = 1000;
    public int TimeoutMs { get; set; } = 800;
    public int WindowSeconds { get; set; } = 30;
    public int RetentionDays { get; set; } = 30;
    public int CloseGraceSeconds { get; set; } = 20;
    public double LossThreshold { get; set; } = 5;
    public double P95ThresholdMs { get; set; } = 100;
    public int ConsecutiveFailures { get; set; } = 3;
    public bool Notifications { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool AutoReplaceUnmeasurable { get; set; }
    public string ExportFolder { get; set; } = "";
    public List<Target> Targets { get; set; } = RecommendedTargets();
    public List<Game> Games { get; set; } = [new("arc-raiders", "ARC Raiders", "PioneerGame-Win64-Shipping")];
    public static List<Target> RecommendedTargets() => [new("gateway", "Passerelle locale (Freebox si applicable)", "auto", TargetKind.Gateway),
        new("cloudflare", "Cloudflare", "1.1.1.1"), new("google", "Google", "8.8.8.8"), new("quad9", "Quad9", "9.9.9.9")];
    public void Validate()
    {
        if (IntervalMs is < 500 or > 60000 || TimeoutMs is < 100 or > 5000 || TimeoutMs > IntervalMs || WindowSeconds is < 10 or > 300 ||
            RetentionDays is < 1 or > 3650 || CloseGraceSeconds is < 0 or > 300 || LossThreshold is < 0 or > 100 || !double.IsFinite(LossThreshold) ||
            P95ThresholdMs < 1 || !double.IsFinite(P95ThresholdMs) || ConsecutiveFailures is < 2 or > 100)
            throw new ArgumentException("Réglages hors limites : intervalle 500–60000 ms, délai 100–5000 ms ≤ intervalle, fenêtre 10–300 s, rétention 1–3650 j.");
        if (Targets.Count(x => x.Enabled) is < 1 or > 8 || Targets.Select(t => t.Id).Distinct().Count() != Targets.Count)
            throw new ArgumentException("Choisissez de 1 à 8 cibles actives, avec des identifiants uniques.");
        foreach (var t in Targets) if (string.IsNullOrWhiteSpace(t.Name) ||
            (t.Kind != TargetKind.Gateway && Uri.CheckHostName(t.Host) == UriHostNameType.Unknown))
            throw new ArgumentException("Adresse IP ou nom DNS de cible invalide.");
        if (Games.Any(g => string.IsNullOrWhiteSpace(g.Name) || string.IsNullOrWhiteSpace(g.ProcessName))) throw new ArgumentException("Nom et exécutable du jeu requis.");
    }
}
public interface IProbeClient { Task<Probe> SendAsync(Target target, NetworkState network, int timeoutMs, CancellationToken cancellationToken); }
public interface INetworkProvider { NetworkState Get(); }
