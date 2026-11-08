using Xunit;
using FreeNetworkMonitor.Core;
using FreeNetworkMonitor.Persistence;
namespace FreeNetworkMonitor.Tests;
public class SessionTests
{
    static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch.AddDays(1);
    static readonly Game G = new("g","Jeu","game");
    [Fact] public void DetectRestartDebounceThenStop() { var t=new SessionTracker(); var start=t.Observe([G],new HashSet<string>{"g"},Now,TimeSpan.FromSeconds(20)); Assert.NotNull(start.Started); t.Observe([G],new HashSet<string>(),Now.AddSeconds(2),TimeSpan.FromSeconds(20)); Assert.Null(t.Observe([G],new HashSet<string>{"g"},Now.AddSeconds(10),TimeSpan.FromSeconds(20)).Ended); t.Observe([G],new HashSet<string>(),Now.AddSeconds(12),TimeSpan.FromSeconds(20)); Assert.NotNull(t.Observe([G],new HashSet<string>(),Now.AddSeconds(32),TimeSpan.FromSeconds(20)).Ended); }
    [Fact] public void ManualIsNotEndedByDetector() { var t=new SessionTracker(); t.Begin("Manuel",null,Now,true); t.Observe([],new HashSet<string>(),Now.AddHours(1),TimeSpan.Zero); Assert.NotNull(t.Active); Assert.NotNull(t.End(Now.AddHours(1))); }
    [Fact] public void MultipleGamesHaveDeterministicPriority() { var t=new SessionTracker(); var r=t.Observe([G,new("g2","Second","game2")],new HashSet<string>{"g","g2"},Now,TimeSpan.Zero); Assert.Equal("Jeu",r.Started?.Game); }
    [Theory] [InlineData(0)] [InlineData(3)] [InlineData(15)] public void BaselineUsesOnlyExistingData(int minutes)
    { var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".db"); try { var store=new Store(path);store.Initialize(); var t=new SessionTracker();var s=t.Begin("Test",null,Now);store.WriteProbes(Enumerable.Range(0,minutes*60).Select(i=>MetricsTests.P(i) with { At=Now.AddSeconds(-i-1) })); var baseline=store.ReadProbes(s.BaselineStart,s.Start);Assert.Equal(minutes*60,baseline.Count);Assert.All(baseline,p=>Assert.True(p.At<s.Start)); } finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); } }
    [Fact] public void DatabaseRecoversInterruptedSessionWithoutInventingEndProbes()
    { var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".db"); try { var db=new Store(path); db.Initialize();var t=new SessionTracker();var s=t.Begin("Test",null,Now);db.Save(s);db.WriteProbes([MetricsTests.P(1) with {At=Now.AddSeconds(30)}]);new Store(path).Recover(Now.AddHours(1));var recovered=db.Sessions().Single();Assert.Equal(Now.AddSeconds(30),recovered.End);Assert.Contains("récupérée",recovered.State);Assert.Single(db.ReadProbes(Now,Now.AddHours(1))); }finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);} }
}
