using System.Diagnostics;
using System.Windows.Automation;
using FreeNetworkMonitor.Core;
using FreeNetworkMonitor.Network;
using FreeNetworkMonitor.Persistence;
using FreeNetworkMonitor.Reporting;
namespace FreeNetworkMonitor.WindowsSmoke;
public static class Program
{
    static string folder="";
    static void Check(bool pass,string text){Console.WriteLine((pass?"OK ":"ÉCHEC ")+text);File.AppendAllText(Path.Combine(folder,"results.log"),(pass?"OK ":"ÉCHEC ")+text+Environment.NewLine);if(!pass)throw new InvalidOperationException(text);}
    static async Task Wait(Func<bool> condition,string description,int seconds=30){var until=DateTime.UtcNow.AddSeconds(seconds);while(DateTime.UtcNow<until){if(condition()){Check(true,description);return;}await Task.Delay(500);}Check(false,description);}
    static AutomationElement Find(AutomationElement root,string name)=>root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,name))??throw new InvalidOperationException("Élément UI absent : "+name);
    static void Invoke(AutomationElement root,string name)=>((InvokePattern)Find(root,name).GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    public static async Task<int> Main(string[] args)
    {
        folder=Path.GetFullPath(args[1]);Directory.CreateDirectory(folder);
        Process? app=null;Process? game=null;
        try
        {
            var data=Path.Combine(folder,"data");var db=new Store(Path.Combine(data,"monitor.db"));db.Initialize();
            var executable=Path.Combine(folder,"FNM-TestGame.exe");File.Copy(Path.Combine(Environment.SystemDirectory,"ping.exe"),executable,true);
            var settings=new Settings {CloseGraceSeconds=2,Notifications=false,ExportFolder=Path.Combine(folder,"reports"),Targets=[new("loopback","Loopback réel — pas Internet","127.0.0.1")],Games=[new("test-game","Exécutable de test","FNM-TestGame",executable)]};db.SaveSettings(settings);
            var client=new IcmpProbeClient();var real=await client.SendAsync(settings.Targets[0],new("loopback","Test","Loopback",null,true),800,CancellationToken.None);
            Check(real.Outcome==ProbeOutcome.Success&&real.RttMs.HasValue,"Sonde ICMP loopback réelle sur Windows (ne prouve pas Internet)");
            app=Process.Start(new ProcessStartInfo(args[0],"--data-dir \""+data+"\""){UseShellExecute=false})??throw new Exception("Démarrage impossible");
            await Wait(()=>{app.Refresh();return app.MainWindowHandle!=IntPtr.Zero;},"Fenêtre WPF créée");
            var root=AutomationElement.FromHandle(app.MainWindowHandle);Check(root.Current.Name=="Free Network Monitor","Titre de fenêtre");
            await Wait(()=>db.ReadProbes(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow).Count(p=>p.Outcome==ProbeOutcome.Success)>=3,"Sondes réelles persistées par l’application");
            Invoke(root,"Démarrer une session manuelle");await Wait(()=>db.Sessions().Any(s=>s.End is null),"Démarrage manuel via interface");
            Invoke(root,"Arrêter la session");await Wait(()=>db.Sessions().Any(s=>s.End is not null),"Arrêt manuel via interface");
            game=Process.Start(new ProcessStartInfo(executable,"-t 127.0.0.1"){CreateNoWindow=true,UseShellExecute=false});
            await Wait(()=>db.Sessions().Any(s=>s.Game=="Exécutable de test"&&s.End is null),"Détection automatique du processus configuré");
            await Task.Delay(4000);game!.Kill();await game.WaitForExitAsync();
            await Wait(()=>db.Sessions().Any(s=>s.Game=="Exécutable de test"&&s.End is not null),"Fermeture automatique après temporisation");
            var session=db.Sessions().First(s=>s.Game=="Exécutable de test");
            await Wait(()=>File.Exists(Path.Combine(settings.ExportFolder,session.Id+".html")),"Rapport généré automatiquement");
            var report=new Reports(db).Create(session,settings);Check(report.Probes.Count>0&&report.Targets.Single().During.Received>0,"Rapport cohérent avec SQLite et mesures réelles");Check(!report.BaselineComplete&&report.BaselineAvailableMinutes>0,"Baseline courte indiquée incomplète");
            foreach(var tab in new[]{"Sessions","Paramètres","Tableau de bord"}){((SelectionItemPattern)Find(root,tab).GetCurrentPattern(SelectionItemPattern.Pattern)).Select();Check(true,"Navigation : "+tab);}
            try{using var bmp=new System.Drawing.Bitmap(1280,960);using var graphics=System.Drawing.Graphics.FromImage(bmp);graphics.CopyFromScreen(0,0,0,0,bmp.Size);bmp.Save(Path.Combine(folder,"desktop-reel.png"));}catch(Exception e){Console.WriteLine("Capture indisponible : "+e.Message);}
            // Crash réel : doit récupérer la session au prochain démarrage.
            Invoke(root,"Démarrer une session manuelle");await Wait(()=>db.Sessions().Any(s=>s.End is null),"Session avant arrêt forcé");await Task.Delay(3000);
            app.Kill();await app.WaitForExitAsync();app=Process.Start(new ProcessStartInfo(args[0],"--data-dir \""+data+"\""){UseShellExecute=false});
            await Wait(()=>db.Sessions().Any(s=>s.State.Contains("récupérée")),"Récupération réelle après arrêt forcé");
            await Wait(()=>{app!.Refresh();return app.MainWindowHandle!=IntPtr.Zero;},"Fenêtre après récupération");root=AutomationElement.FromHandle(app!.MainWindowHandle);Invoke(root,"Quitter");await app.WaitForExitAsync();Check(app.ExitCode==0,"Fermeture propre");
            return 0;
        }
        catch(Exception e){File.AppendAllText(Path.Combine(folder,"results.log"),e.ToString());Console.Error.WriteLine(e);return 1;}
        finally{if(game is not null&&!game.HasExited)game.Kill();if(app is not null&&!app.HasExited)app.Kill();Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();}
    }
}
