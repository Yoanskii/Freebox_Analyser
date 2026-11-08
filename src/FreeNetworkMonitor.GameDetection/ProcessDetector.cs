using System.Diagnostics;
using FreeNetworkMonitor.Core;
namespace FreeNetworkMonitor.GameDetection;
public sealed class ProcessDetector
{
    public HashSet<string> Running(IReadOnlyList<Game> games)
    {
        var found = new HashSet<string>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var candidates = games.Where(g => g.Enabled && string.Equals(System.IO.Path.GetFileNameWithoutExtension(g.ProcessName), process.ProcessName, StringComparison.OrdinalIgnoreCase));
                    foreach (var g in candidates)
                    {
                        if (string.IsNullOrWhiteSpace(g.Path)) { found.Add(g.Id); continue; }
                        try { if (string.Equals(System.IO.Path.GetFullPath(g.Path), process.MainModule?.FileName, StringComparison.OrdinalIgnoreCase)) found.Add(g.Id); }
                        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                        { /* Chemin demandé mais inaccessible : pas de faux positif. Nom seul configurable en secours. */ }
                    }
                }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
            }
        }
        return found;
    }
}
