using System.Diagnostics;
using System.IO.Compression;
using Xunit;
using FreeNetworkMonitor.Core;
using FreeNetworkMonitor.Persistence;
using FreeNetworkMonitor.GameDetection;
using FreeNetworkMonitor.Infrastructure;
namespace FreeNetworkMonitor.Tests;
public class RobustnessTests
{
    [Theory] [InlineData(ProbeOutcome.DnsError)] [InlineData(ProbeOutcome.LocalError)] [InlineData(ProbeOutcome.Offline)] [InlineData(ProbeOutcome.Cancelled)] [InlineData(ProbeOutcome.Unreachable)]
    public void AmbiguousAndLocalStatusesAreExcluded(ProbeOutcome outcome){var s=Metrics.Calculate([MetricsTests.P(0,outcome)],true);Assert.Equal(0,s.Sent);Assert.Equal(1,s.Invalid);Assert.Null(s.LossPercent);}
    [Fact] public void JitterDoesNotCrossEndpointChange(){var a=MetricsTests.P(0,rtt:10);var b=MetricsTests.P(1,rtt:100) with{Address="::1"};Assert.Null(Metrics.Calculate([a,b]).JitterMs);}
    [Fact] public void P95UsesNearestRank(){var s=Metrics.Calculate(Enumerable.Range(1,100).Select(i=>MetricsTests.P(i,rtt:i)));Assert.Equal(95,s.P95Ms);Assert.Equal(50.5,s.MedianMs);Assert.Equal(1,s.MinMs);Assert.Equal(100,s.MaxMs);}
    [Fact] public void ManualStopSuppressesAutoUntilProcessExits(){var tracker=new SessionTracker();Game g=new("g","Jeu","game.exe");var running=new HashSet<string>{"g"};var now=DateTimeOffset.UtcNow;tracker.Observe([g],running,now,TimeSpan.Zero);Assert.Equal("game.exe",tracker.Active!.Executable);tracker.End(now,suppressAuto:true);Assert.Null(tracker.Observe([g],running,now,TimeSpan.Zero).Started);tracker.Observe([g],new HashSet<string>(),now,TimeSpan.Zero);Assert.NotNull(tracker.Observe([g],running,now,TimeSpan.Zero).Started);}
    [Fact] public void ProcessDetectorFindsCurrentProcessWithoutInjection(){using var p=Process.GetCurrentProcess();var found=new ProcessDetector().Running([new("current","Test",p.ProcessName)]);Assert.Contains("current",found);}
    [Theory] [InlineData(499,800)] [InlineData(1000,1200)] [InlineData(70000,800)] public void InvalidSettingsAreRejected(int interval,int timeout){var s=new Settings{IntervalMs=interval,TimeoutMs=timeout};Assert.Throws<ArgumentException>(()=>s.Validate());}
    [Fact] public void MoreThanEightTargetsRejected(){var s=new Settings{Targets=Enumerable.Range(0,9).Select(i=>new Target(i.ToString(),"Test","127.0.0.1")).ToList()};Assert.Throws<ArgumentException>(()=>s.Validate());}
    [Fact] public void BackupReopensAndDeletionRemovesAllRows()
    {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());try{var db=new Store(Path.Combine(dir,"m.db"));db.Initialize();db.WriteProbes([MetricsTests.P(0)]);db.SaveSettings(new());db.BackupArchive(Path.Combine(dir,"all.zip"));ZipFile.ExtractToDirectory(Path.Combine(dir,"all.zip"),Path.Combine(dir,"unzip"));var restored=new Store(Path.Combine(dir,"unzip","monitor.db"));restored.Initialize();Assert.Single(restored.ReadProbes(DateTimeOffset.MinValue,DateTimeOffset.MaxValue));db.DeleteAll();Assert.Empty(db.ReadProbes(DateTimeOffset.MinValue,DateTimeOffset.MaxValue));Assert.Empty(db.Sessions());}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
    [Fact] public void VersionOneDatabaseMigratesAndReopensIdempotently()
    {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);var path=Path.Combine(dir,"m.db");
        try{using(var c=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+path)){c.Open();using var cmd=c.CreateCommand();cmd.CommandText="CREATE TABLE sessions(id TEXT PRIMARY KEY,start TEXT NOT NULL,finish TEXT,data TEXT NOT NULL); INSERT INTO sessions VALUES('s',$start,$finish,$data)";var now=DateTimeOffset.UnixEpoch.AddDays(1);var session=new Session("s","Migration",now,now.AddMinutes(1),now.AddMinutes(-15),"Complète");cmd.Parameters.AddWithValue("$start",Store.Stamp(session.Start));cmd.Parameters.AddWithValue("$finish",Store.Stamp(session.End!.Value));cmd.Parameters.AddWithValue("$data",System.Text.Json.JsonSerializer.Serialize(session));cmd.ExecuteNonQuery();}
        var store=new Store(path);store.Initialize();store.Initialize();Assert.Single(store.Sessions());using var reopened=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+path);reopened.Open();using var check=reopened.CreateCommand();check.CommandText="SELECT baseline FROM sessions WHERE id='s'";Assert.Equal(Store.Stamp(DateTimeOffset.UnixEpoch.AddDays(1).AddMinutes(-15)),check.ExecuteScalar());
        }finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
    sealed class OnlineNetwork:INetworkProvider {public NetworkState Get()=>new("test","Sonde injectée du test","Test","127.0.0.1",true);}
    sealed class DelayedProbe:IProbeClient
    {
        public readonly TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<Probe> SendAsync(Target t,NetworkState n,int timeout,CancellationToken token){var at=DateTimeOffset.UtcNow;Started.TrySetResult();await Release.Task.WaitAsync(token);return new(at,t.Id,t.Name,t.Host,n.Id,ProbeOutcome.Success,10);}
    }
    [Fact] public async Task AutomaticReportIncludesProbeStartedBeforeSessionEnd()
    {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());var probe=new DelayedProbe();try{var db=new Store(Path.Combine(dir,"m.db"));db.Initialize();db.SaveSettings(new Settings{Targets=[new("t","Test injecté","127.0.0.1")],Games=[]});var ended=new TaskCompletionSource<Session>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using(var runtime=new Runtime(db,new OnlineNetwork(),probe)){runtime.SessionEnded+=s=>ended.TrySetResult(s);await runtime.StartAsync();for(int i=0;i<100&&!runtime.Ready;i++)await Task.Delay(20);runtime.ManualStart("Sonde en vol");await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));runtime.ManualEnd();await Task.Delay(300);Assert.False(ended.Task.IsCompleted);probe.Release.TrySetResult();var session=await ended.Task.WaitAsync(TimeSpan.FromSeconds(5));var json=File.ReadAllText(Path.Combine(dir,"reports",session.Id+".json"));using var document=System.Text.Json.JsonDocument.Parse(json);Assert.Equal(1,document.RootElement.GetProperty("Targets")[0].GetProperty("During").GetProperty("Received").GetInt32());}
        }finally{probe.Release.TrySetResult();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
    sealed class OfflineNetwork:INetworkProvider {public NetworkState Get()=>new("","Absent","Hors ligne",null,false);}
    sealed class ForbiddenProbe:IProbeClient {public int Calls;public Task<Probe> SendAsync(Target t,NetworkState n,int timeout,CancellationToken token){Interlocked.Increment(ref Calls);throw new InvalidOperationException("Ne doit pas sonder hors ligne");}}
    [Fact] public async Task OfflineRuntimeWritesEventWithoutInventingProbes()
    {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());var probe=new ForbiddenProbe();try{var db=new Store(Path.Combine(dir,"m.db"));await using(var runtime=new Runtime(db,new OfflineNetwork(),probe)){await runtime.StartAsync();for(int i=0;i<50&&!runtime.Ready;i++)await Task.Delay(50);Assert.True(runtime.Ready);await Task.Delay(700);Assert.Null(runtime.Error);runtime.ManualStart("Test hors ligne");await Task.Delay(500);runtime.ManualEnd();await Task.Delay(500);}Assert.Equal(0,probe.Calls);Assert.Empty(db.ReadProbes(DateTimeOffset.MinValue,DateTimeOffset.MaxValue));Assert.Contains(db.ReadEvents(DateTimeOffset.MinValue,DateTimeOffset.MaxValue),e=>e.Kind=="Réseau");Assert.Single(db.Sessions());}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
}
