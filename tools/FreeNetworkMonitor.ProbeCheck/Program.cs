using System.Text.Json;
using FreeNetworkMonitor.Core;
using FreeNetworkMonitor.Network;
using FreeNetworkMonitor.Persistence;
var target = args.Length > 0 ? args[0] : "127.0.0.1";
var path = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "fnm-real-probes.db");
var store = new Store(path); store.Initialize();
var client = new IcmpProbeClient();
var detected = new NetworkProvider().Get();
// Loopback est une vérification du moteur, jamais une preuve de connectivité Internet.
var network = target == "127.0.0.1" ? new NetworkState("loopback", "Loopback de vérification", "Loopback", null, true) : detected;
var probes = new List<Probe>();
for (int i=0;i<5;i++) { var p=await client.SendAsync(new("live-check", "Vérification réelle",target),network,800,CancellationToken.None); probes.Add(p); Console.WriteLine(JsonSerializer.Serialize(p)); await Task.Delay(1000); }
store.WriteProbes(probes);
Console.WriteLine(JsonSerializer.Serialize(Metrics.Calculate(probes)));
return probes.Any(p=>p.Outcome==ProbeOutcome.Success) ? 0 : 2;
