using Xunit;
using FreeNetworkMonitor.Core;
namespace FreeNetworkMonitor.Tests;
public class MetricsTests
{
    internal static Probe P(int i, ProbeOutcome outcome = ProbeOutcome.Success, double rtt = 10) => new(DateTimeOffset.UnixEpoch.AddSeconds(i), "t", "Test", "127.0.0.1", "nic", outcome, outcome == ProbeOutcome.Success ? rtt : null);
    [Fact] public void SevenTimeoutsAreSevenPercent() { var rows = Enumerable.Range(0,100).Select(i => P(i,i < 7 ? ProbeOutcome.Timeout : ProbeOutcome.Success)).Concat([P(101,ProbeOutcome.LocalError),P(102,ProbeOutcome.Offline),P(103,ProbeOutcome.Cancelled),P(104,ProbeOutcome.DnsError)]); var s = Metrics.Calculate(rows); Assert.Equal(7d,s.LossPercent); Assert.Equal(100,s.Sent); Assert.Equal(4,s.Invalid); }
    [Fact] public void NonMeasurableDoesNotImplyOutage() { var s = Metrics.Calculate(Enumerable.Range(0,30).Select(i=>P(i,ProbeOutcome.Timeout))); Assert.Null(s.LossPercent); Assert.Contains("Non mesurable",s.Quality); }
    [Fact] public void RttAndJitterUseOnlyConsecutiveSuccessfulSamples() { var s = Metrics.Calculate([P(0,rtt:10),P(1,rtt:20),P(2,ProbeOutcome.Timeout),P(3,rtt:90),P(4,rtt:100)]); Assert.Equal(55,s.MedianMs); Assert.Equal(100,s.P95Ms); Assert.Equal(10,s.JitterMs); Assert.Equal(55,s.MeanMs); }
    [Fact] public void EmptyIsUnknown() { var s=Metrics.Calculate([]); Assert.Null(s.LossPercent); Assert.Null(s.MeanMs); Assert.Null(s.JitterMs); }
    [Fact] public void ZeroRttIsValid() { Assert.Equal(0,Metrics.Calculate([P(0,rtt:0)]).MeanMs); }
    [Fact] public void ValidatedTargetCanLoseAllRecentProbes() { Assert.Equal(100,Metrics.Calculate([P(0,ProbeOutcome.Timeout)],true).LossPercent); }
    [Fact] public void NoFalseDiagnosisForSilentTargets() { var settings = new Settings(); Assert.Contains("insuffisantes",Diagnosis.Describe(settings.Targets.Select(t=>(t,Metrics.Calculate(Enumerable.Range(0,30).Select(i=>P(i,ProbeOutcome.Timeout))))),settings)); }
}
