using System.Windows;
using FreeNetworkMonitor.Infrastructure;
namespace FreeNetworkMonitor.App;
public partial class App : System.Windows.Application
{
    Mutex? instance;
    public static string DataFolder {get;private set;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FreeNetworkMonitor");
    protected override void OnStartup(StartupEventArgs e)
    {
        var culture=System.Globalization.CultureInfo.GetCultureInfo("fr-FR");System.Globalization.CultureInfo.DefaultThreadCurrentCulture=culture;System.Globalization.CultureInfo.DefaultThreadCurrentUICulture=culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage("fr-FR")));
        base.OnStartup(e);
        int index=Array.IndexOf(e.Args,"--data-dir");if(index>=0&&index+1<e.Args.Length)DataFolder=Path.GetFullPath(e.Args[index+1]);
        var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(DataFolder.ToUpperInvariant())))[..16];
        instance=new Mutex(true,"Local\\FreeNetworkMonitor-"+hash,out bool created);
        if(!created){System.Windows.MessageBox.Show("Free Network Monitor est déjà ouvert pour ce dossier de données. Consultez la zone de notification.");Shutdown();return;}
        DispatcherUnhandledException+=(_,args)=>{Log.Write(args.Exception);System.Windows.MessageBox.Show(args.Exception.Message,"Free Network Monitor — erreur");args.Handled=true;};
        new MainWindow().Show();
    }
    protected override void OnExit(ExitEventArgs e){instance?.Dispose();base.OnExit(e);}
}
