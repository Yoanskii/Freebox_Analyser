using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FreeNetworkMonitor.Core;
using FreeNetworkMonitor.Infrastructure;
using FreeNetworkMonitor.Reporting;
namespace FreeNetworkMonitor.App;
public sealed class Command(Func<Task> execute) : ICommand
{
    bool busy;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !busy;
    public async void Execute(object? parameter)
    {
        busy=true;CanExecuteChanged?.Invoke(this,EventArgs.Empty);
        try { await execute(); } catch(Exception e) { Log.Write(e); System.Windows.MessageBox.Show(e.Message,"Free Network Monitor",MessageBoxButton.OK,MessageBoxImage.Warning); }
        finally {busy=false;CanExecuteChanged?.Invoke(this,EventArgs.Empty);}
    }
}
public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}
public sealed record TargetRow(string Key, string Id, string Name, string Address, string Mean, string Loss, string Jitter, string Samples, string Quality, string Coverage);
public sealed record SessionRow(Session Session, string Game, string Start, string Duration, string State, string Baseline, int Incidents);
public sealed class ViewModel : Observable
{
    readonly Runtime runtime;
    readonly Reports reports;
    bool refreshing, loadedSettings;
    public ViewModel(Runtime runtime)
    {
        this.runtime=runtime;reports=new(runtime.Store);
        Pause=new(()=> {runtime.TogglePause();return Task.CompletedTask;});
        StartSession=new(()=> {runtime.ManualStart(string.IsNullOrWhiteSpace(ManualGame)?"Session manuelle":ManualGame);return Task.CompletedTask;});
        EndSession=new(()=> {runtime.ManualEnd();return Task.CompletedTask;});
        SaveSettings=new(Save);RefreshHistory=new(LoadHistory);ShowReport=new(OpenReport);ExportSession=new(Export);
        AddTarget=new(()=>{Targets.Add(new(Guid.NewGuid().ToString("N"),"Nouvelle cible","",TargetKind.Personal));return Task.CompletedTask;});
        RemoveTarget=new(()=>{if(SelectedTargetSetting is not null)Targets.Remove(SelectedTargetSetting);return Task.CompletedTask;});
        AddGame=new(AddGameFile);RemoveGame=new(()=>{if(SelectedGame is not null)Games.Remove(SelectedGame);return Task.CompletedTask;});
        RestoreTargets=new(()=>{Targets.Clear();foreach(var t in Settings.RecommendedTargets())Targets.Add(t);return Task.CompletedTask;});
        TestTarget=new(Test);SelectExportFolder=new(ChooseFolder);
    }
    public ObservableCollection<TargetRow> Rows { get; }=[];
    public ObservableCollection<SessionRow> Sessions { get; }=[];
    public ObservableCollection<Target> Targets { get; }=[];
    public ObservableCollection<Game> Games { get; }=[];
    public Settings Draft {get;private set;}=new();
    public string Status {get;private set;}="Initialisation de la base locale…";
    public string DiagnosisText {get;private set;}="Données insuffisantes pour conclure.";
    public string SessionText {get;private set;}="Aucune session active • Serveur de jeu non identifié";
    public string Mean {get;private set;}="—";
    public string Loss {get;private set;}="—";
    public string Jitter {get;private set;}="—";
    public string IncidentCount {get;private set;}="0";
    public string MetricContext {get;private set;}="Fenêtre glissante : 30 secondes";
    public string BaselineText {get;private set;}="Baseline : collecte en cours, jusqu’à 15 minutes.";
    public string Message {get;private set;}="";
    public string ManualGame {get;set;}="Session manuelle";
    public string Filter {get;set;}="";
    public DateTime? FromDate {get;set;}
    public DateTime? ToDate {get;set;}
    public Target? SelectedTargetSetting {get;set;}
    public Game? SelectedGame {get;set;}
    SessionRow? selectedSession;
    public SessionRow? SelectedSession {get=>selectedSession;set{selectedSession=value;Notify(); _=Detail();}}
    public string SessionDetail {get;private set;}="Sélectionnez une session pour comparer avant / pendant et exporter son rapport.";
    string? selectedKey;
    public TargetRow? SelectedRow {get=>Rows.FirstOrDefault(x=>x.Key==selectedKey);set{selectedKey=value?.Key;Notify();UpdateCards();}}
    public List<Probe> Plot {get;private set;}=[];
    List<Probe> lastProbes=[];
    public Command Pause {get;} public Command StartSession {get;} public Command EndSession {get;}
    public Command SaveSettings {get;} public Command RefreshHistory {get;} public Command ShowReport {get;}
    public Command ExportSession {get;} public Command AddTarget {get;} public Command RemoveTarget {get;}
    public Command AddGame {get;} public Command RemoveGame {get;} public Command RestoreTargets {get;}
    public Command TestTarget {get;} public Command SelectExportFolder {get;}
    static string N(double? v)=>v?.ToString("F2",CultureInfo.GetCultureInfo("fr-FR"))??"—";
    public async Task Refresh()
    {
        if(refreshing)return;refreshing=true;
        try
        {
            if(!runtime.Ready){Status=runtime.Error??"Initialisation de la base locale…";Notify(nameof(Status));return;}
            if(!loadedSettings){Draft=System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(runtime.Settings))!;foreach(var t in Draft.Targets)Targets.Add(t);foreach(var g in Draft.Games)Games.Add(g);Notify(nameof(Draft));loadedSettings=true;await LoadHistory();}
            var now=DateTimeOffset.UtcNow;
            var data=await Task.Run(()=>(runtime.Store.ReadProbes(now.AddMinutes(-15),now),runtime.Store.ReadIncidents(now.AddSeconds(-runtime.Settings.WindowSeconds),now)));
            lastProbes=data.Item1;
            Status=runtime.Error is not null ? "Erreur : "+runtime.Error : $"Surveillance {(runtime.Paused?"suspendue":runtime.Network.Online?"active":"hors ligne")} • {runtime.Network.Name} • {runtime.Network.Kind} • Passerelle {runtime.Network.Gateway??"absente"}";
            SessionText=runtime.Active is Session s?$"En jeu : {s.Game} • depuis {s.Start.ToLocalTime():HH:mm:ss} • Serveur de jeu non identifié":"Aucune session active • Serveur de jeu non identifié";
            IncidentCount=data.Item2.Count.ToString();
            var rows=new List<TargetRow>();var diagnostic=new List<(Target,Statistics)>();
            foreach(var target in runtime.Settings.Targets.Where(t=>t.Enabled))
            {
                var latest=lastProbes.LastOrDefault(p=>p.TargetId==target.Id);
                var endpoint=lastProbes.Where(p=>p.TargetId==target.Id && p.Address==latest?.Address && p.InterfaceId==latest?.InterfaceId).ToArray();
                var window=endpoint.Where(p=>p.At>=now.AddSeconds(-runtime.Settings.WindowSeconds)).ToArray();
                var stats=Metrics.Calculate(window,endpoint.Count(p=>p.Outcome==ProbeOutcome.Success)>=3);diagnostic.Add((target,stats));
                string state=stats.LossPercent.HasValue?(stats.MaxConsecutiveFailures>=runtime.Settings.ConsecutiveFailures||stats.Sent>=20&&(stats.LossPercent>runtime.Settings.LossThreshold||stats.P95Ms>runtime.Settings.P95ThresholdMs)?"Dégradé":"OK"):stats.Quality;
                rows.Add(new(target.Id+"|"+latest?.Address,target.Id,target.Name,latest?.Address??(target.Kind==TargetKind.Gateway?runtime.Network.Gateway??"Indisponible":target.Host),N(stats.MeanMs),N(stats.LossPercent),N(stats.JitterMs),$"{stats.Sent} envoyées · {stats.Received} reçues · {stats.Failed} échecs · {stats.Invalid} invalides ({N(stats.Total>0?100d*stats.Invalid/stats.Total:null)} %)",state+" · "+stats.Quality,$"{stats.First?.ToLocalTime():HH:mm:ss} → {stats.Last?.ToLocalTime():HH:mm:ss}"));
            }
            var oldKey=selectedKey;Rows.Clear();foreach(var row in rows)Rows.Add(row);selectedKey=rows.Any(x=>x.Key==oldKey)?oldKey:rows.FirstOrDefault()?.Key;Notify(nameof(SelectedRow));
            DiagnosisText=runtime.Network.Online?Diagnosis.Describe(diagnostic,runtime.Settings):"Interface hors ligne : aucune sonde non lancée n’est comptée comme perdue.";
            BaselineText=$"Baseline disponible : {N(lastProbes.Count==0?0:Math.Min(15,(now-lastProbes[0].At).TotalMinutes))} / 15 min. La couverture de chaque cible est détaillée dans le rapport.";
            foreach(var property in new[]{nameof(Status),nameof(SessionText),nameof(IncidentCount),nameof(DiagnosisText),nameof(BaselineText)})Notify(property);
            UpdateCards();
        }
        catch(Exception e){Message=e.Message;Notify(nameof(Message));Log.Write(e);}
        finally{refreshing=false;}
    }
    void UpdateCards()
    {
        var row=SelectedRow;Mean=row?.Mean??"—";Loss=row?.Loss??"—";Jitter=row?.Jitter??"—";
        MetricContext=$"{row?.Name??"Aucune cible"} • {runtime.Settings.WindowSeconds} dernières secondes • {row?.Coverage}";
        Plot=lastProbes.Where(p=>p.TargetId==row?.Id).ToList();
        foreach(var p in new[]{nameof(Mean),nameof(Loss),nameof(Jitter),nameof(MetricContext),nameof(Plot)})Notify(p);
    }
    public async Task LoadHistory()
    {
        if(!runtime.Ready)return;
        var data=await Task.Run(()=>runtime.Store.Sessions().Where(s=>(string.IsNullOrWhiteSpace(Filter)||s.Game.Contains(Filter,StringComparison.CurrentCultureIgnoreCase)||s.Start.ToLocalTime().ToString("d",CultureInfo.GetCultureInfo("fr-FR")).Contains(Filter))&&(!FromDate.HasValue||s.Start.ToLocalTime().Date>=FromDate.Value.Date)&&(!ToDate.HasValue||s.Start.ToLocalTime().Date<=ToDate.Value.Date)).Take(500).Select(s=>new SessionRow(s,s.Game,s.Start.ToLocalTime().ToString("g",CultureInfo.GetCultureInfo("fr-FR")),s.End is null?"En cours":$"{N((s.End.Value-s.Start).TotalMinutes)} min",s.State,runtime.Store.HasBaseline(s)?"Disponible (détails dans le rapport)":"Absente",runtime.Store.ReadIncidents(s.Start,s.End??DateTimeOffset.UtcNow).Count)).ToList());
        var id=selectedSession?.Session.Id;Sessions.Clear();foreach(var row in data)Sessions.Add(row);if(id is not null)SelectedSession=Sessions.FirstOrDefault(s=>s.Session.Id==id);
    }
    async Task Detail()
    {
        var selected=selectedSession;if(selected is null)return;
        try {var r=await Task.Run(()=>reports.Create(selected.Session,runtime.Settings));if(selectedSession!=selected)return;
            SessionDetail=$"{r.Summary}\nBaseline : {N(r.BaselineAvailableMinutes)}/15 min — {(r.BaselineComplete?"complète":"incomplète")}.\n"+string.Join("\n",r.Targets.Select(t=>$"{t.Name} ({t.Address}) : avant {t.Before.Sent} sondes, {N(t.Before.LossPercent)} % / pendant {t.During.Sent} sondes, {N(t.During.LossPercent)} %, RTT moy./méd./p95/max {N(t.During.MeanMs)}/{N(t.During.MedianMs)}/{N(t.During.P95Ms)}/{N(t.During.MaxMs)} ms, gigue {N(t.During.JitterMs)} ms."));Notify(nameof(SessionDetail));
        }catch(Exception e){SessionDetail=e.Message;Notify(nameof(SessionDetail));Log.Write(e);}
    }
    async Task OpenReport()
    {if(selectedSession is null)throw new InvalidOperationException("Sélectionnez une session.");var path=await ExportSelected();System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path+".html"){UseShellExecute=true});}
    async Task Export(){var path=await ExportSelected();Message="CSV, JSON et HTML exportés : "+path;Notify(nameof(Message));}
    async Task<string> ExportSelected()
    {
        var session=selectedSession?.Session??throw new InvalidOperationException("Sélectionnez une session.");
        var folder=string.IsNullOrWhiteSpace(runtime.Settings.ExportFolder)?Path.Combine(runtime.DataFolder,"reports"):runtime.Settings.ExportFolder;
        var path=Path.Combine(folder,session.Id);await Task.Run(()=>Reports.Export(reports.Create(session,runtime.Settings),path));return path;
    }
    async Task Save()
    {
        Draft.Targets=Targets.ToList();Draft.Games=Games.ToList();Draft.Validate();
        Startup.Set(Draft.StartWithWindows);
        // Sérialisation pour que la saisie UI ne modifie jamais les réglages du moteur en cours.
        var copy=System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(Draft))!;runtime.Configure(copy);
        Message="Paramètres validés. Application au prochain cycle de surveillance.";Notify(nameof(Message));await Task.CompletedTask;
    }
    Task AddGameFile()
    {var dialog=new Microsoft.Win32.OpenFileDialog{Filter="Exécutables Windows (*.exe)|*.exe",Title="Choisir le véritable exécutable du jeu (pas le launcher)"};if(dialog.ShowDialog()==true)Games.Add(new(Guid.NewGuid().ToString("N"),Path.GetFileNameWithoutExtension(dialog.FileName),Path.GetFileNameWithoutExtension(dialog.FileName),dialog.FileName));return Task.CompletedTask;}
    Task ChooseFolder(){var dialog=new Microsoft.Win32.OpenFolderDialog{Title="Dossier des rapports"};if(dialog.ShowDialog()==true){Draft.ExportFolder=dialog.FolderName;Notify(nameof(Draft));}return Task.CompletedTask;}
    async Task Test()
    {var target=SelectedTargetSetting??throw new InvalidOperationException("Sélectionnez une cible à tester.");var client=new FreeNetworkMonitor.Network.IcmpProbeClient();var network=await Task.Run(()=>new FreeNetworkMonitor.Network.NetworkProvider().Get());var p=await client.SendAsync(target,network,Draft.TimeoutMs,CancellationToken.None);Message=$"Test réel hors statistiques : {p.TargetName} ({p.Address}) : {p.Outcome}, RTT {N(p.RttMs)} ms. {p.Detail}. Un seul test ne valide pas une cible.";Notify(nameof(Message));}
}
