using Microsoft.Win32;
namespace FreeNetworkMonitor.App;
public static class Startup
{
    public static void Set(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if(enabled){var exe=Environment.ProcessPath??throw new InvalidOperationException("Chemin du programme indisponible.");key.SetValue("FreeNetworkMonitor", "\""+exe+"\" --minimized");}
        else key.DeleteValue("FreeNetworkMonitor",false);
    }
}
