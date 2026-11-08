using System.Collections.Concurrent;
using FreeNetworkMonitor.Core;
using FreeNetworkMonitor.Network;
using FreeNetworkMonitor.Persistence;
using FreeNetworkMonitor.GameDetection;
namespace FreeNetworkMonitor.Infrastructure;
public sealed class Runtime : IAsyncDisposable
{
    public Store Store { get; }
    public string DataFolder => Path.GetDirectoryName(Store.DatabasePath)!;
    public Settings Settings { get; private set; }
    public NetworkState Network { get; private set; } = new("", "Initialisation", "", null, false);
    public Session? Active => tracker.Active;
    public bool Paused { get; private set; }
    public bool Ready { get; private set; }
    public string? Error { get; private set; }
    public event Action? Changed;
    public event Action<Session>? SessionEnded;
    public event Action<Incident>? IncidentOpened;
    readonly Dictionary<string, IncidentDetector> detectors = [];
    readonly Dictionary<string,string> endpoints = [];
    readonly SessionTracker tracker = new();
    readonly INetworkProvider networkProvider;
    readonly IProbeClient probeClient;
    readonly ProcessDetector detector = new();
    readonly CancellationTokenSource stop = new();
    readonly ConcurrentQueue<Action> commands = new();
    readonly List<Probe> recent = [];
    readonly List<Probe> pending = [];
    readonly Dictionary<string, Task<Probe>> flights = [];
    readonly Dictionary<string, DateTimeOffset> due = [];
    Task? worker;
    Task<(Target Original, Target Replacement, List<Probe> Probes)>? replacement;
    readonly HashSet<string> replacementAttempted = [];
    int disposed;
    async Task<(Target,Target,List<Probe>)> ValidateReplacement(Target original)
    {
        var candidate = new Target(Guid.NewGuid().ToString("N"), original.Name + " (repli OpenDNS)", "208.67.222.222", original.Kind);
        var samples = new List<Probe>();
        for(int i=0;i<3;i++) { samples.Add(await probeClient.SendAsync(candidate,Network,Settings.TimeoutMs,stop.Token)); await Task.Delay(Settings.IntervalMs,stop.Token); }
        return (original,candidate,samples);
    }
    DateTimeOffset lastFlush, lastDetection, lastPurge, lastTick, started;
    public Runtime(Store store, INetworkProvider? provider = null, IProbeClient? client = null)
    { Store = store; networkProvider = provider ?? new NetworkProvider(); probeClient = client ?? new IcmpProbeClient(); Settings = new(); }
    public Task StartAsync() { worker = Task.Run(Run); return Task.CompletedTask; }
    public void Configure(Settings settings) { settings.Validate(); commands.Enqueue(() => { Store.SaveSettings(settings); Settings = settings; due.Clear(); CloseIncidents(DateTimeOffset.UtcNow); detectors.Clear(); endpoints.Clear(); Record("Paramètres", $"Configuration modifiée : intervalle {settings.IntervalMs} ms, fenêtre {settings.WindowSeconds} s, pertes > {settings.LossThreshold} %, p95 > {settings.P95ThresholdMs} ms, {settings.ConsecutiveFailures} échecs consécutifs ; validation renouvelée."); }); }
    public void TogglePause() => commands.Enqueue(() => { Paused = !Paused; Record("Surveillance", Paused ? "Surveillance suspendue" : "Surveillance reprise"); });
    public void ManualStart(string name) => commands.Enqueue(() => { if (tracker.Active is null) { var s = tracker.Begin(name, null, DateTimeOffset.UtcNow, true); s=tracker.AttachAnalysis(Settings); Flush(); Store.Save(s); Record("Session", "Session manuelle démarrée : " + name); } });
    public void ManualEnd() => commands.Enqueue(() => Finish(tracker.End(DateTimeOffset.UtcNow, suppressAuto: true)));
    public void PowerEvent(string kind) { var at=DateTimeOffset.UtcNow; commands.Enqueue(() => { Store.Save(new NetworkEvent(at,"Alimentation",kind)); }); }
    public void Record(string kind, string message) => Store.Save(new NetworkEvent(DateTimeOffset.UtcNow, kind, message));
    public void Flush() { if (pending.Count == 0) return; Store.WriteProbes(pending); pending.Clear(); lastFlush = DateTimeOffset.UtcNow; }
    void Finish(Session? session) { if (session is null) return; Flush(); Store.Save(session); Record("Session", "Session terminée : " + session.Game);
        var report = new FreeNetworkMonitor.Reporting.Reports(Store).Create(session, Settings);
        var folder = string.IsNullOrWhiteSpace(Settings.ExportFolder) ? Path.Combine(DataFolder, "reports") : Settings.ExportFolder;
        FreeNetworkMonitor.Reporting.Reports.Export(report, Path.Combine(folder, session.Id));
        SessionEnded?.Invoke(session); }
    async Task Run()
    {
        try
        {
            Store.Initialize(); Store.Recover(DateTimeOffset.UtcNow); Settings = Store.LoadSettings(); Settings.Validate();
            started = DateTimeOffset.UtcNow; lastTick = started;
            recent.AddRange(Store.ReadProbes(started.AddMinutes(-15), started));
            Record("Application", "Surveillance démarrée"); Ready = true;
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
            while (await timer.WaitForNextTickAsync(stop.Token))
            {
                try
                {
                    var now = DateTimeOffset.UtcNow;
                    if (now - lastTick > TimeSpan.FromSeconds(5)) Record("Interruption", "Absence de sondes (veille ou retard d’ordonnancement) ; aucune perte ajoutée.");
                    lastTick = now;
                    while (commands.TryDequeue(out var command)) command();
                    foreach (var id in flights.Keys.ToArray()) if (flights[id].IsCompleted)
                    {
                        var p = await flights[id]; flights.Remove(id); recent.Add(p); pending.Add(p); Accept(p);
                    }
                    if(replacement is not null && replacement.IsCompleted)
                    {
                        try {
                            var checkedTarget = await replacement; recent.AddRange(checkedTarget.Probes); pending.AddRange(checkedTarget.Probes);
                            if(checkedTarget.Probes.All(p=>p.Outcome==ProbeOutcome.Success) && Settings.Targets.Any(t=>t.Id==checkedTarget.Original.Id))
                            { Settings.Targets = Settings.Targets.Select(t=>t.Id==checkedTarget.Original.Id?checkedTarget.Replacement:t).ToList(); Store.SaveSettings(Settings);
                              Record("Remplacement", $"{checkedTarget.Original.Name} ({checkedTarget.Original.Host}) remplacée par {checkedTarget.Replacement.Host} après 3 réponses. Aucune panne Internet inférée."); }
                            else Record("Remplacement", "Cible de repli non validée ; configuration conservée.");
                        } finally { replacement=null; }
                    }
                    if(Settings.AutoReplaceUnmeasurable && !Paused && Network.Online && replacement is null && now-started>TimeSpan.FromSeconds(60))
                    {
                        var target=Settings.Targets.FirstOrDefault(t=>t.Enabled && t.Kind==TargetKind.External && t.Host!="208.67.222.222" && !replacementAttempted.Contains(t.Id) && !Settings.Targets.Any(other=>other.Enabled&&other.Host=="208.67.222.222") && recent.Count(p=>p.TargetId==t.Id && p.Eligible)>=30 && !recent.Any(p=>p.TargetId==t.Id&&p.Outcome==ProbeOutcome.Success));
                        if(target is not null){replacementAttempted.Add(target.Id);replacement=ValidateReplacement(target);}
                    }
                    recent.RemoveAll(p => p.At < now.AddMinutes(-15));
                    if (now - lastDetection >= TimeSpan.FromSeconds(2))
                    {
                        lastDetection = now;
                        var nextNetwork = networkProvider.Get();
                        if (nextNetwork != Network) { Network = nextNetwork; Record("Réseau", $"{Network.Name} • {Network.Kind} • {(Network.Online ? "connecté" : "hors ligne")} • passerelle {Network.Gateway ?? "absente"}"); OnNetworkChanged(now); }
                        var observed = tracker.Observe(Settings.Games, detector.Running(Settings.Games), now, TimeSpan.FromSeconds(Settings.CloseGraceSeconds));
                        if (observed.Started is not null) { Flush(); Store.Save(tracker.AttachAnalysis(Settings)); Record("Session", "Jeu détecté : " + observed.Started.Game); }
                        Finish(observed.Ended);
                    }
                    if (!Paused && pending.Count < 50000)
                    {
                        var targets = Settings.Targets.Where(t => t.Enabled).ToArray();
                        for (int i = 0; i < targets.Length; i++)
                        {
                            var t = targets[i];
                            if (!due.ContainsKey(t.Id)) { due[t.Id] = now.AddMilliseconds(i * Settings.IntervalMs / targets.Length); continue; }
                            if (now < due[t.Id] || flights.ContainsKey(t.Id)) continue;
                            due[t.Id] = now.AddMilliseconds(Settings.IntervalMs);
                            // Le provider est une photographie contextuelle, pas une preuve de routage par socket.
                            if (Network.Online) flights[t.Id] = probeClient.SendAsync(t, Network, Settings.TimeoutMs, stop.Token);
                        }
                    }
                    if (now - lastFlush > TimeSpan.FromSeconds(2)) Flush();
                    if (now - lastPurge > TimeSpan.FromHours(1)) { Store.Purge(now.AddDays(-Settings.RetentionDays)); lastPurge = now; }
                    Error = null;
                    Changed?.Invoke();
                }
                catch (Exception e) when (e is not OperationCanceledException) { Error = e.Message; Log.Write(e); Changed?.Invoke(); await Task.Delay(1000, stop.Token); }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception e) { Error = "Surveillance arrêtée : " + e.Message; Log.Write(e); Changed?.Invoke(); }
        finally
        {
            try { if(replacement is not null) {try {var r=await replacement;pending.AddRange(r.Probes);}catch(OperationCanceledException){}} foreach (var f in flights.Values) { var p = await f; if (p.Outcome != ProbeOutcome.Cancelled) pending.Add(p); }
                Finish(tracker.End(DateTimeOffset.UtcNow, "Interrompue — fermeture de l’application")); Flush(); CloseIncidents(DateTimeOffset.UtcNow); }
            catch (Exception e) { Log.Write(e); }
        }
    }
    // Extensions d’analyse ajoutées dans la phase suivante.
    void Accept(Probe p)
    {
        string endpoint = p.Address + "|" + p.InterfaceId;
        if (!endpoints.TryGetValue(p.TargetId, out var old) || old != endpoint)
        {
            if (detectors.TryGetValue(p.TargetId, out var prior) && prior.Close(p.At) is Incident closed) Store.Save(closed);
            detectors[p.TargetId] = new(); endpoints[p.TargetId] = endpoint;
            Record("Destination", $"{p.TargetName} : {endpoint} ; validation ICMP renouvelée.");
        }
        var result = detectors[p.TargetId].Observe(p, Settings);
        if (result.Opened is not null) { Store.Save(result.Opened); IncidentOpened?.Invoke(result.Opened); }
        if (result.Closed is not null) Store.Save(result.Closed);
    }
    void OnNetworkChanged(DateTimeOffset now) { CloseIncidents(now); detectors.Clear(); endpoints.Clear(); }
    void CloseIncidents(DateTimeOffset now) { foreach(var d in detectors.Values) if(d.Close(now) is Incident i) Store.Save(i); }
    public async ValueTask DisposeAsync() { if(Interlocked.Exchange(ref disposed,1)!=0)return; stop.Cancel(); if (worker is not null) await worker; stop.Dispose(); }
}
public static class Log
{
    public static string Folder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FreeNetworkMonitor", "logs");
    static readonly object gate = new();
    public static void Write(Exception e)
    {
        lock (gate) try { Directory.CreateDirectory(Folder); var p = Path.Combine(Folder,"monitor.log");
            if (File.Exists(p) && new FileInfo(p).Length > 1024 * 1024) File.Move(p,p+".1",true);
            File.AppendAllText(p, $"{DateTimeOffset.UtcNow:O} {e}\n"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
