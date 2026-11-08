namespace FreeNetworkMonitor.Core;
public sealed class IncidentDetector
{
    readonly Queue<Probe> window = new();
    int successes, healthy, badWindows;
    public Incident? Active { get; private set; }
    public bool Validated => successes >= 3;
    public (Incident? Opened, Incident? Closed) Observe(Probe probe, Settings settings)
    {
        window.Enqueue(probe);
        while (window.Count > 0 && window.Peek().At < probe.At.AddSeconds(-settings.WindowSeconds)) window.Dequeue();
        if (probe.Outcome == ProbeOutcome.Success) successes++;
        var s = Metrics.Calculate(window, Validated);
        // Erreurs locales et ICMP jamais validé ne déclenchent pas une panne.
        if (!Validated || !probe.Eligible) { healthy = 0; badWindows = 0; return (null, null); }
        bool consecutive = window.Reverse().Take(settings.ConsecutiveFailures).Count(p => p.Eligible && p.Outcome != ProbeOutcome.Success) == settings.ConsecutiveFailures;
        bool windowBad = s.Sent >= 20 && (s.LossPercent > settings.LossThreshold || s.P95Ms > settings.P95ThresholdMs);
        badWindows = windowBad ? badWindows + 1 : 0;
        bool bad = consecutive || badWindows >= 2;
        if (bad)
        {
            healthy = 0;
            if (Active is null)
            {
                var start = consecutive ? window.Reverse().Take(settings.ConsecutiveFailures).Last().At : probe.At;
                Active = new(Guid.NewGuid().ToString("N"), probe.TargetId, start, null,
                    consecutive ? "Échecs ICMP consécutifs sur cible validée" : "Seuil de pertes ICMP ou de RTT p95 dépassé (fenêtre glissante)", s.Sent, s.Failed, s.P95Ms);
                return (Active, null);
            }
        }
        else if (!windowBad && probe.Outcome == ProbeOutcome.Success && ++healthy >= 3 && Active is not null)
        { var closed = Close(probe.At); return (null, closed); }
        else if (probe.Outcome != ProbeOutcome.Success) healthy = 0;
        return (null, null);
    }
    public Incident? Close(DateTimeOffset at) { var closed = Active is null ? null : Active with { End = at }; Active = null; healthy = 0; badWindows = 0; return closed; }
}
