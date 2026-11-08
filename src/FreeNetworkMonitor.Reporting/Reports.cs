using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreeNetworkMonitor.Core;
using FreeNetworkMonitor.Persistence;
namespace FreeNetworkMonitor.Reporting;
public sealed record TargetComparison(string TargetId, string Name, string Address, string InterfaceId, string Protocol, int IpVersion, Statistics Before, Statistics During);
public sealed record Report(Session Session, DateTimeOffset GeneratedAt, double BaselineAvailableMinutes, bool BaselineComplete,
    string Summary, string Methodology, List<TargetComparison> Targets, List<Probe> Probes, List<Incident> Incidents, List<NetworkEvent> Events, AnalysisConfiguration Analysis);
public sealed class Reports(Store store)
{
    public Report Create(Session session, Settings settings)
    {
        settings=session.Analysis?.ToSettings()??settings;
        var end = session.End ?? DateTimeOffset.UtcNow;
        var probes = store.ReadProbes(session.BaselineStart, end).OrderBy(p => p.At).ToList();
        var targets = probes.GroupBy(p => (p.TargetId, p.TargetName, p.Address, p.InterfaceId, p.Protocol, p.IpVersion)).Select(g =>
        {
            var before = g.Where(p => p.At < session.Start).ToArray();
            return new TargetComparison(g.Key.TargetId, g.Key.TargetName, g.Key.Address, g.Key.InterfaceId, g.Key.Protocol, g.Key.IpVersion,
                Metrics.Calculate(before), Metrics.Calculate(g.Where(p => p.At >= session.Start), before.Count(p => p.Outcome == ProbeOutcome.Success) >= 3));
        }).ToList();
        var baseline = probes.Where(p => p.At < session.Start).ToArray();
        var available = baseline.Length == 0 ? 0 : Math.Min(15,(session.Start - baseline[0].At).TotalMinutes);
        // Couverture complète : chaque cible observée en jeu doit avoir 15 min, sans trou > 3 intervalles.
        bool complete = targets.Count > 0 && targets.All(t =>
        {
            var b = baseline.Where(p => p.TargetId == t.TargetId && p.Address == t.Address && p.InterfaceId == t.InterfaceId).ToArray();
            return b.Length > 0 && b[0].At <= session.BaselineStart.AddMilliseconds(settings.IntervalMs*2) &&
                session.Start-b[^1].At <= TimeSpan.FromMilliseconds(settings.IntervalMs*3) &&
                b.Zip(b.Skip(1)).All(pair => pair.Second.At-pair.First.At <= TimeSpan.FromMilliseconds(settings.IntervalMs*3));
        });
        var incidents = store.ReadIncidents(session.Start,end);
        string summary = "Aucun incident enregistré selon les seuils configurés. Les métriques par destination restent la référence.";
        // Une seule fenêtre temporelle commune pour toute inférence multi-cibles.
        foreach(var incident in incidents)
        {
            var endWindow = incident.Start.AddSeconds(settings.WindowSeconds);
            var rows = probes.Where(p => p.At >= incident.Start && p.At < endWindow && p.At >= session.Start).GroupBy(p => p.TargetId)
                .Select(g => {var latest=g.Last();var endpoint=g.Where(p=>p.Address==latest.Address&&p.InterfaceId==latest.InterfaceId);var validated=probes.Count(p=>p.TargetId==g.Key&&p.Address==latest.Address&&p.InterfaceId==latest.InterfaceId&&p.At<incident.Start&&p.Outcome==ProbeOutcome.Success)>=3;
                    return (new Target(g.Key,latest.TargetName,latest.Address,latest.Kind),Metrics.Calculate(endpoint,validated));}).ToList();
            var text = Diagnosis.Describe(rows,settings);
            if (!text.Contains("insuffisantes") && !text.StartsWith("Aucune")) { summary = $"Fenêtre commune {incident.Start:O} / {endWindow:O} : " + text; break; }
            summary = "Incidents ICMP observés. Données insuffisantes pour localiser leur origine ; voir les fenêtres et destinations détaillées.";
        }
        return new(session,DateTimeOffset.UtcNow,available,complete,summary,Diagnosis.Limits,targets,probes,incidents,store.ReadEvents(session.BaselineStart,end),AnalysisConfiguration.From(settings));
    }
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public static void Export(Report report, string basePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(basePath))!);
        File.WriteAllText(basePath+".json",JsonSerializer.Serialize(report,JsonOptions),new UTF8Encoding(false));
        File.WriteAllText(basePath+".csv",Csv(report),new UTF8Encoding(true));
        File.WriteAllText(basePath+".html",Html(report),new UTF8Encoding(false));
    }
    static string Csv(Report r)
    {
        static string Q(string? value) { value ??= ""; if (value.Length>0 && "=+-@".Contains(value[0])) value="'"+value; return "\""+value.Replace("\"","\"\"")+"\""; }
        var b = new StringBuilder("timestamp_utc,period,session_id,target_id,target_name,address,protocol,ip_version,interface_id,outcome,eligible,rtt_ms,detail\r\n");
        foreach(var p in r.Probes) b.AppendLine(string.Join(",",new[]{Q(Store.Stamp(p.At)),Q(p.At < r.Session.Start ? "baseline" : "session"),Q(r.Session.Id),Q(p.TargetId),Q(p.TargetName),Q(p.Address),Q(p.Protocol),p.IpVersion.ToString(CultureInfo.InvariantCulture),Q(p.InterfaceId),Q(p.Outcome.ToString()),p.Eligible ? "true":"false",p.RttMs?.ToString("R",CultureInfo.InvariantCulture)??"",Q(p.Detail)}));
        return b.ToString();
    }
    public static string Html(Report r)
    {
        static string H(string? v)=>WebUtility.HtmlEncode(v??"");
        static string V(double? v)=>v?.ToString("F2",CultureInfo.GetCultureInfo("fr-FR"))??"—";
        var b = new StringBuilder("<!doctype html><html lang='fr'><meta charset='utf-8'><title>Free Network Monitor — rapport</title><style>body{font:15px system-ui;background:#101723;color:#edf5ff;margin:40px;max-width:1400px}h1,h2{color:#50dfd2}table{border-collapse:collapse;width:100%;font-size:13px}td,th{border:1px solid #52647a;padding:9px;text-align:left}p{line-height:1.6}.note{color:#b7c9dd}svg{width:100%;height:240px;background:#1c2535}@media print{body{background:white;color:#111;margin:15mm}h1,h2{color:#163b4a}svg{background:#eee}table{break-inside:auto}tr{break-inside:avoid}.note{color:#333}}</style><h1>Free Network Monitor</h1>");
        b.Append($"<h2>{H(r.Session.Game)}</h2><p>{H(r.Session.State)} · ID {H(r.Session.Id)}<br>Début local : {H(r.Session.Start.ToLocalTime().ToString("F",CultureInfo.GetCultureInfo("fr-FR")))}<br>UTC : {H(Store.Stamp(r.Session.Start))} → {H(r.Session.End is null ? "En cours" : Store.Stamp(r.Session.End.Value))}<br>Durée de session : {V(((r.Session.End??r.GeneratedAt)-r.Session.Start).TotalMinutes)} min (les périodes sans sondes sont exclues du taux de pertes).</p>");
        b.Append($"<p>Baseline réellement disponible : {V(r.BaselineAvailableMinutes)} / 15 min · {(r.BaselineComplete ? "couverture complète" : "couverture incomplète ou interruptions — voir chaque cible")}.</p><p>{H(r.Summary)}</p><p class='note'>{H(r.Methodology)}</p>");
        foreach(var t in r.Targets)
        {
            b.Append($"<h2>{H(t.Name)} — {H(t.Address)}</h2><p>IPv{t.IpVersion} · {H(t.Protocol)} · interface observée {H(t.InterfaceId)}</p><table><tr><th>Période UTC</th><th>Envoyées / reçues / échouées / invalides</th><th>Pertes ICMP %</th><th>RTT moy. / méd. / p95 / min / max (ms)</th><th>Gigue (ms)</th><th>Fiabilité</th></tr>");
            foreach(var row in new[]{("Avant jeu",t.Before),("Pendant jeu",t.During)}) { var s=row.Item2;
                b.Append($"<tr><td>{row.Item1}<br>{H(s.First?.ToString("O"))}<br>{H(s.Last?.ToString("O"))}</td><td>{s.Sent} / {s.Received} / {s.Failed} / {s.Invalid} sur {s.Total}</td><td>{V(s.LossPercent)}</td><td>{V(s.MeanMs)} / {V(s.MedianMs)} / {V(s.P95Ms)} / {V(s.MinMs)} / {V(s.MaxMs)}</td><td>{V(s.JitterMs)}</td><td>{H(s.Quality)}</td></tr>"); }
            b.Append("</table>");
            b.Append(Graph(r.Probes.Where(p=>p.TargetId==t.TargetId && p.Address==t.Address && p.InterfaceId==t.InterfaceId).ToArray(),r.Session.Start));
        }
        b.Append($"<p>Réglages d’analyse au début : intervalle {r.Analysis.IntervalMs} ms, fenêtre {r.Analysis.WindowSeconds} s, seuil pertes {V(r.Analysis.LossThreshold)} %, RTT p95 {V(r.Analysis.P95ThresholdMs)} ms, {r.Analysis.ConsecutiveFailures} échecs consécutifs. Les changements pendant le jeu sont tracés dans la chronologie. Les incidents conservent les seuils appliqués au moment de leur ouverture.</p>");
        b.Append("<h2>Incidents</h2><table><tr><th>Début / fin UTC</th><th>Destination</th><th>Preuves à l’ouverture</th></tr>");
        foreach(var i in r.Incidents) b.Append($"<tr><td>{i.Start:O}<br>{H(i.End?.ToString("O")??"ouvert")}</td><td>{H(i.TargetId)}</td><td>{H(i.Reason)} · {i.Failed}/{i.Eligible} échecs · p95 {V(i.P95Ms)} ms</td></tr>");
        b.Append("</table><h2>Chronologie</h2><ul>"); foreach(var e in r.Events) b.Append($"<li>{e.At:O} · {H(e.Kind)} · {H(e.Message)}</li>");
        b.Append("</ul><h2>Méthode</h2><p>RTT : seuls les succès. Médiane usuelle, p95 au rang plafond(0,95×n). Gigue : moyenne des écarts absolus entre RTT réussis consécutifs de même adresse et interface ; un échec coupe la série. Ce n’est pas la gigue RTP. Invalides : DNS, erreur locale, injoignable ambiguë, hors ligne, annulation. Confiance limitée sous 20 sondes ; cible validée après 3 succès par adresse/interface. Le contexte d’interface est observé, sans forcer le routage système. Le CSV et le JSON contiennent les sondes individuelles.</p><p>Imprimer depuis le navigateur → Microsoft Print to PDF. Aucun service distant nécessaire.</p></html>");
        return b.ToString();
    }
    static string Graph(Probe[] probes,DateTimeOffset sessionStart)
    {
        if(probes.Length==0)return "<p>Aucune sonde disponible.</p>";
        double max=Math.Max(1,probes.Max(p=>p.RttMs??0));var first=probes[0].At;double span=Math.Max(1,(probes[^1].At-first).TotalSeconds);
        string N(double x)=>x.ToString("F2",CultureInfo.InvariantCulture);
        double X(Probe p)=>55+900*(p.At-first).TotalSeconds/span;
        var b=new StringBuilder($"<svg viewBox='0 0 1000 240' role='img' aria-label='RTT en millisecondes et échecs ICMP dans le temps'><text x='5' y='18' fill='#7bb5bb'>{N(max)} ms</text><text x='5' y='200' fill='#7bb5bb'>0 ms</text><path d='M55 25 V200 H960' fill='none' stroke='#657b90'/><text x='55' y='230' fill='#7bb5bb'>{first:HH:mm:ss} UTC</text><text x='800' y='230' fill='#7bb5bb'>{probes[^1].At:HH:mm:ss} UTC</text>");
        double sessionX=55+900*(sessionStart-first).TotalSeconds/span;if(sessionX>=55&&sessionX<=955)b.Append($"<path d='M{N(sessionX)} 25 V200' stroke='#dcb85a' stroke-dasharray='4 4'/><text x='{N(sessionX)}' y='18' fill='#dcb85a'>Début jeu</text>");
        // Décimation purement visuelle, les exports bruts restent complets.
        foreach(var bucket in probes.GroupBy(p=>(int)X(p)))
        {
            foreach(var p in bucket.Where(p=>p.RttMs.HasValue).OrderBy(p=>p.RttMs).TakeLast(1)) b.Append($"<circle cx='{N(X(p))}' cy='{N(195-165*p.RttMs!.Value/max)}' r='2' fill='#3ac4b7'><title>{p.At:O} : {N(p.RttMs.Value)} ms</title></circle>");
            if(bucket.Any(p=>p.Eligible&&p.Outcome!=ProbeOutcome.Success)) b.Append($"<path d='M{N(bucket.Key)} 202 v8' stroke='#e87575'/>");
        }
        b.Append("<text x='320' y='230' fill='#3ac4b7'>● RTT (ms)</text><text x='510' y='230' fill='#e87575'>| Sonde sans réponse</text></svg>");return b.ToString();
    }
}
