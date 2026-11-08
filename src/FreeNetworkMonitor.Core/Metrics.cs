namespace FreeNetworkMonitor.Core;
public static class Metrics
{
    public static Statistics Calculate(IEnumerable<Probe> source, bool previouslyValidated = false)
    {
        var all = source.OrderBy(p => p.At).ToArray();
        var eligible = all.Where(p => p.Eligible).ToArray();
        var rtts = eligible.Where(p => p.Outcome == ProbeOutcome.Success && p.RttMs.HasValue).Select(p => p.RttMs!.Value).Order().ToArray();
        int received = eligible.Count(p => p.Outcome == ProbeOutcome.Success), failed = eligible.Length - received;
        double sum = 0; int pairs = 0, streak = 0, maxStreak = 0; Probe? previous = null;
        foreach (var p in all)
        {
            if (p.Eligible && p.Outcome != ProbeOutcome.Success) maxStreak = Math.Max(maxStreak, ++streak); else streak = 0;
            // Une absence de réponse, un changement IP/interface coupe la série de gigue.
            if (p.Outcome == ProbeOutcome.Success && p.RttMs.HasValue)
            {
                if (previous?.RttMs is double last && previous.Address == p.Address && previous.InterfaceId == p.InterfaceId)
                { sum += Math.Abs(p.RttMs.Value - last); pairs++; }
                previous = p;
            }
            else previous = null;
        }
        bool validated = previouslyValidated || received >= 3;
        string quality = !validated ? (eligible.Length >= 10 && received == 0 ? "Non mesurable — données insuffisantes" : "Données insuffisantes") :
            eligible.Length < 20 ? "Échantillon limité" : "Cible validée — ICMP uniquement";
        return new(all.Length, eligible.Length, received, failed, all.Length - eligible.Length,
            validated && eligible.Length > 0 ? 100d * failed / eligible.Length : null,
            rtts.Length > 0 ? rtts.Average() : null, Median(rtts),
            rtts.Length > 0 ? rtts[(int)Math.Ceiling(.95 * rtts.Length) - 1] : null,
            rtts.Length > 0 ? rtts[0] : null, rtts.Length > 0 ? rtts[^1] : null,
            pairs > 0 ? sum / pairs : null, maxStreak, quality, all.FirstOrDefault()?.At, all.LastOrDefault()?.At);
    }
    static double? Median(double[] sorted) => sorted.Length == 0 ? null : sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] :
        (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
}
public static class Diagnosis
{
    public static string Describe(IEnumerable<(Target Target, Statistics Stats)> rows, Settings settings)
    {
        var data = rows.ToArray();
        bool Bad(Statistics s) => s.LossPercent is double loss && (s.MaxConsecutiveFailures >= settings.ConsecutiveFailures ||
            s.Sent >= 20 && (loss > settings.LossThreshold || s.P95Ms > settings.P95ThresholdMs));
        if (data.Any(x => x.Target.Kind == TargetKind.Gateway && Bad(x.Stats)))
            return "Anomalie observée vers la passerelle / réseau local suspect. Origine exacte indéterminée.";
        int external = data.Count(x => x.Target.Kind != TargetKind.Gateway && Bad(x.Stats));
        bool gatewayOk = data.Any(x => x.Target.Kind == TargetKind.Gateway && x.Stats.Sent >= 20 && x.Stats.LossPercent is <= 5 && !Bad(x.Stats));
        if (external >= 2 && gatewayOk) return "Anomalie au-delà de la passerelle plausible ; origine exacte indéterminée.";
        if (external == 1 && data.Any(x => x.Target.Kind != TargetKind.Gateway && x.Stats.Sent >= 20 && x.Stats.LossPercent == 0 && !Bad(x.Stats)))
            return "Anomalie limitée à une destination ou à son chemin réseau ; contrôle ICMP possible.";
        if (external > 0) return "Anomalie sur des sondes externes ; données insuffisantes pour localiser son origine.";
        return data.Any(x => x.Stats.Sent >= 20 && x.Stats.LossPercent.HasValue) ?
            "Aucune anomalie selon les seuils choisis. Cela ne prouve pas l’absence de pertes UDP/TCP du jeu." : "Données insuffisantes pour conclure.";
    }
    public const string Limits = "Pertes sur sondes ICMP : réponses absentes parmi les sondes éligibles, par cible et période. Une destination peut limiter ou déprioriser ICMP. Ces sondes ne mesurent pas les pertes UDP/TCP du jeu. Serveur de jeu non identifié.";
}
