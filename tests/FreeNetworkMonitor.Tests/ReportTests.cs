using System.Text.Json;
using Xunit;
using FreeNetworkMonitor.Core;
using FreeNetworkMonitor.Persistence;
using FreeNetworkMonitor.Reporting;
namespace FreeNetworkMonitor.Tests;
public class ReportTests
{
    [Fact] public void ExportsMatchStoredRowsAndEscapeUserText()
    {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(dir);
        try{var db=new Store(Path.Combine(dir,"m.db"));db.Initialize();var now=DateTimeOffset.UnixEpoch.AddDays(1);var session=new Session("s","<script>Jeu</script>",now,now.AddSeconds(100),now.AddMinutes(-15),"Complète");
        db.Save(session);db.WriteProbes(Enumerable.Range(0,100).Select(i=>MetricsTests.P(i,i<7?ProbeOutcome.Timeout:ProbeOutcome.Success) with{At=now.AddSeconds(i)}));
        var r=new Reports(db).Create(session,new());Assert.Equal(7,r.Targets.Single().During.LossPercent);Assert.Equal(0,r.BaselineAvailableMinutes);Assert.False(r.BaselineComplete);Reports.Export(r,Path.Combine(dir,"rapport"));
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"rapport.json")));Assert.Equal(100,json.RootElement.GetProperty("Probes").GetArrayLength());Assert.Equal(101,File.ReadAllLines(Path.Combine(dir,"rapport.csv")).Length);var html=File.ReadAllText(Path.Combine(dir,"rapport.html"));Assert.Contains("&lt;script&gt;",html);Assert.DoesNotContain("<script>",html);Assert.Contains("UDP/TCP",html);
        }finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}
    }
    [Fact] public void PurgeKeepsBaselineOfRetainedSession()
    {var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());try{var db=new Store(Path.Combine(dir,"m.db"));db.Initialize();var now=DateTimeOffset.UtcNow;var s=new Session("s","Jeu",now.AddMinutes(-1),now,now.AddMinutes(-16),"Complète");db.Save(s);db.WriteProbes([MetricsTests.P(0) with{At=now.AddMinutes(-15)},MetricsTests.P(1) with{At=now.AddDays(-40)}]);db.Purge(now.AddMinutes(-5));Assert.Single(db.ReadProbes(now.AddDays(-50),now));}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(dir,true);}}
}
