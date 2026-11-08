using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using FreeNetworkMonitor.Core;
using FreeNetworkMonitor.Infrastructure;
using FreeNetworkMonitor.Persistence;
namespace FreeNetworkMonitor.App;
public partial class MainWindow : Window
{
    readonly Runtime runtime;
    readonly ViewModel vm;
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};
    readonly System.Windows.Forms.NotifyIcon tray;
    bool exiting,disposing;
    DateTimeOffset lastAlert;
    public MainWindow()
    {
        InitializeComponent();
        var folder=App.DataFolder;
        Log.Folder=Path.Combine(folder,"logs");
        runtime=new(new Store(Path.Combine(folder,"monitor.db")));vm=new(runtime);DataContext=vm;
        tray=new(){Icon=System.Drawing.SystemIcons.Application,Text="Free Network Monitor",Visible=true};
        var menu=new System.Windows.Forms.ContextMenuStrip();menu.Items.Add("Ouvrir",null,(_,_)=>Dispatcher.Invoke(Restore));menu.Items.Add("Suspendre / reprendre",null,(_,_)=>runtime.TogglePause());menu.Items.Add("Quitter",null,(_,_)=>Dispatcher.Invoke(()=>{exiting=true;Close();}));tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(Restore);
        timer.Tick+=async(_,_)=>await vm.Refresh();
        runtime.IncidentOpened+=i=>Dispatcher.InvokeAsync(()=>{if(runtime.Settings.Notifications&&DateTimeOffset.UtcNow-lastAlert>TimeSpan.FromSeconds(60)){lastAlert=DateTimeOffset.UtcNow;tray.ShowBalloonTip(5000,"Incident sur sondes ICMP",$"{i.TargetId} : {i.Reason}. Origine non établie.",System.Windows.Forms.ToolTipIcon.Warning);}});
        runtime.SessionEnded+=s=>Dispatcher.InvokeAsync(async()=>{await vm.LoadHistory();vm.SelectedSession=vm.Sessions.FirstOrDefault(x=>x.Session.Id==s.Id);if(runtime.Settings.Notifications)tray.ShowBalloonTip(4000,"Rapport disponible",$"{s.Game} : consultez la rubrique Sessions.",System.Windows.Forms.ToolTipIcon.Info);});
        SystemEvents.PowerModeChanged+=Power;
        Loaded+=async(_,_)=>{await runtime.StartAsync();timer.Start();if(Environment.GetCommandLineArgs().Contains("--minimized"))Hide();};
        Closing+=async(_,e)=>{if(disposing)return;if(!exiting){e.Cancel=true;Hide();return;}e.Cancel=true;timer.Stop();IsEnabled=false;await runtime.DisposeAsync();SystemEvents.PowerModeChanged-=Power;tray.Dispose();disposing=true;Close();};
    }
    void Power(object sender,PowerModeChangedEventArgs e)=>runtime.PowerEvent(e.Mode==PowerModes.Suspend?"Mise en veille":e.Mode==PowerModes.Resume?"Reprise après veille":"Changement alimentation");
    void Restore(){Show();WindowState=WindowState.Normal;Activate();}
    void MinimizeToTray(object sender,RoutedEventArgs e)=>Hide();
    void Quit(object sender,RoutedEventArgs e){exiting=true;Close();}
    async void LastReport(object sender,RoutedEventArgs e){try{await vm.LoadHistory();vm.SelectedSession=vm.Sessions.FirstOrDefault(s=>s.Session.End is not null);vm.ShowReport.Execute(null);}catch(Exception error){ShowError(error);}}
    void SaveConfiguration(object sender,RoutedEventArgs e)
    {
        TargetGrid.CommitEdit();TargetGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row,true);GameGrid.CommitEdit();GameGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row,true);
        if(HasError(this)){System.Windows.MessageBox.Show("Corrigez les valeurs non valides avant d’enregistrer.","Paramètres");return;}vm.SaveSettings.Execute(null);
    }
    static bool HasError(DependencyObject root){if(System.Windows.Controls.Validation.GetHasError(root))return true;for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++)if(HasError(System.Windows.Media.VisualTreeHelper.GetChild(root,i)))return true;return false;}
    async void ExportAll(object sender,RoutedEventArgs e)
    {
        var dialog=new Microsoft.Win32.SaveFileDialog{FileName="FreeNetworkMonitor-donnees.zip",Filter="Archive ZIP|*.zip"};if(dialog.ShowDialog()!=true)return;
        try{await Task.Run(()=>runtime.Store.BackupArchive(dialog.FileName));System.Windows.MessageBox.Show("Export local terminé. L’archive contient une sauvegarde SQLite cohérente et les paramètres.","Export");}catch(Exception error){ShowError(error);}
    }
    async void DeleteAll(object sender,RoutedEventArgs e)
    {
        if(System.Windows.MessageBox.Show("Supprimer définitivement les mesures, sessions, rapports, paramètres et journaux locaux ? L’application va s’arrêter. Les exports enregistrés dans un dossier personnel restent à votre charge.","Suppression des données",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
        try{timer.Stop();await runtime.DisposeAsync();await Task.Run(()=>runtime.Store.DeleteAll());var folder=App.DataFolder;foreach(var name in new[]{"reports","logs"}){var p=Path.Combine(folder,name);if(Directory.Exists(p))Directory.Delete(p,true);}Startup.Set(false);disposing=true;SystemEvents.PowerModeChanged-=Power;tray.Dispose();Close();}catch(Exception error){ShowError(error);exiting=true;Close();}
    }
    static void ShowError(Exception error){Log.Write(error);System.Windows.MessageBox.Show(error.Message,"Free Network Monitor",MessageBoxButton.OK,MessageBoxImage.Warning);}
}
