using System.Windows;
using FreeNetworkMonitor.Core;
using FreeNetworkMonitor.Network;
using FreeNetworkMonitor.Persistence;
namespace FreeNetworkMonitor.App;
public partial class MainWindow : Window
{
    readonly CancellationTokenSource stop = new(); bool paused;
    readonly Store store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FreeNetworkMonitor", "monitor.db"));
    public MainWindow() { InitializeComponent(); Loaded += async (_, _) => { try { store.Initialize(); await Monitor(); } catch (Exception e) { Status.Text = e.Message; } }; Closed += (_, _) => stop.Cancel(); }
    void Toggle(object sender, RoutedEventArgs e) => paused = !paused;
    async Task Monitor()
    {
        var provider = new NetworkProvider(); var client = new IcmpProbeClient(); var settings = new Settings(); var recent = new List<Probe>();
        try { while (!stop.IsCancellationRequested) { var network = provider.Get(); Status.Text = paused ? "Surveillance suspendue" : $"Surveillance {(network.Online ? "active" : "hors ligne")} • {network.Name} • {network.Kind}";
            if (!paused) { foreach (var t in settings.Targets) { var p = await client.SendAsync(t, network, settings.TimeoutMs, stop.Token); recent.Add(p); await Task.Run(() => store.WriteProbes([p])); }
            recent.RemoveAll(p => p.At < DateTimeOffset.UtcNow.AddSeconds(-30)); Rows.ItemsSource = settings.Targets.Select(t => { var s = Metrics.Calculate(recent.Where(p => p.TargetId == t.Id)); return new { t.Name, Mean = s.MeanMs?.ToString("F1") ?? "—", Loss = s.LossPercent?.ToString("F1") ?? "—", Quality = $"{s.Sent} sondes • {s.Quality}" }; }); }
            await Task.Delay(1000, stop.Token); } } catch (OperationCanceledException) { }
    }
}
