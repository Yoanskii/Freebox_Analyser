using Xunit;
using FreeNetworkMonitor.Core;
namespace FreeNetworkMonitor.Tests;
public class IncidentTests
{
    [Fact] public void ThreeFailuresOpenOneIncidentAndRecoveryClosesIt()
    {
        var d=new IncidentDetector();var settings=new Settings();for(int i=0;i<30;i++)d.Observe(MetricsTests.P(i),settings);
        int opens=0;for(int i=30;i<36;i++)if(d.Observe(MetricsTests.P(i,ProbeOutcome.Timeout),settings).Opened is not null)opens++;
        Assert.Equal(1,opens);Assert.NotNull(d.Active);int closes=0;for(int i=36;i<90;i++)if(d.Observe(MetricsTests.P(i),settings).Closed is not null)closes++;
        Assert.Equal(1,closes);Assert.Null(d.Active);
    }
    [Fact] public void SilentTargetAndLocalErrorsNeverOpenIncident()
    { var d=new IncidentDetector();var s=new Settings();for(int i=0;i<60;i++)Assert.Null(d.Observe(MetricsTests.P(i,ProbeOutcome.Timeout),s).Opened);for(int i=60;i<65;i++)d.Observe(MetricsTests.P(i),s);for(int i=65;i<100;i++)Assert.Null(d.Observe(MetricsTests.P(i,ProbeOutcome.LocalError),s).Opened); }
    [Fact] public void MultiDestinationDiagnosisRequiresMeasuredGateway()
    {var s=new Settings();var good=Metrics.Calculate(Enumerable.Range(0,30).Select(i=>MetricsTests.P(i)));var bad=Metrics.Calculate(Enumerable.Range(0,30).Select(i=>MetricsTests.P(i,ProbeOutcome.Timeout)),true);Assert.Contains("au-delà",Diagnosis.Describe([(s.Targets[0],good),(s.Targets[1],bad),(s.Targets[2],bad)],s));Assert.DoesNotContain("au-delà",Diagnosis.Describe([(s.Targets[1],bad),(s.Targets[2],bad)],s));}
}
