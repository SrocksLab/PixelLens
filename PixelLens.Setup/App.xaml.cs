using System.Windows;
namespace PixelLens.Setup;
public partial class App:Application
{
 private async void App_Startup(object sender,StartupEventArgs e)
 {
  if(e.Args.Any(a=>a.StartsWith("/VERYSILENT",StringComparison.OrdinalIgnoreCase)||a.Equals("/SILENT",StringComparison.OrdinalIgnoreCase)))
  {
   try{Shutdown(await new InstallationEngine().RunSilentAsync(e.Args));}catch{Shutdown(1);}return;
  }
  ShutdownMode=ShutdownMode.OnLastWindowClose;new InstallerWindow().Show();
 }
}