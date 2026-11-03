using System.Diagnostics;
using System.IO;
using System.Reflection;
namespace PixelLens.Setup;
public sealed class InstallationEngine
{
 private readonly string? overridePath;
 public string? LastLogPath {get;private set;}
 public InstallationEngine(string? enginePath=null){overridePath=enginePath;}
 private async Task<string> ExtractAsync(string directory)
 {
  if(overridePath!=null)return overridePath;
  var file=Path.Combine(directory,"InstallEngine.exe");
  using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("SetupEngine")??throw new IOException("安装文件缺失，请重新获取安装包");
  await using var output=File.Create(file);await stream.CopyToAsync(output);return file;
 }
 public async Task<int> RunAsync(string target,IProgress<double>? progress,CancellationToken token,bool noIcons=false)
 {
  var folder=Path.Combine(Path.GetTempPath(),"PixelLensSetup",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  var status=Path.Combine(folder,"progress");var cancel=Path.Combine(folder,"cancel");LastLogPath=Path.Combine(folder,"install.log");
  var executable=await ExtractAsync(folder);
  var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
  foreach(var arg in new[]{"/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART","/SP-","/DIR="+target,"/LOG="+LastLogPath,"/FLOWSTATUS="+status,"/FLOWCANCEL="+cancel})info.ArgumentList.Add(arg);
  if(noIcons){info.ArgumentList.Add("/NOICONS");info.ArgumentList.Add("/TASKS=");}
  using var registration=token.Register(()=>{try{File.WriteAllText(cancel,"cancel");}catch(IOException){}});
  using var process=Process.Start(info)??throw new IOException("无法启动安装程序");
  while(!process.HasExited){
   try{
    if(File.Exists(status)){var parts=(await File.ReadAllTextAsync(status)).Split('/');if(parts.Length==2&&double.TryParse(parts[0],out var current)&&double.TryParse(parts[1],out var maximum)&&maximum>0)progress?.Report(Math.Clamp(current/maximum,0,1));}
   }catch(IOException){}
   await Task.Delay(100);
  }
  await process.WaitForExitAsync();CleanupPayload(executable,folder);return process.ExitCode;
 }
 private void CleanupPayload(string file,string folder)
 {
  if(overridePath!=null||Path.GetDirectoryName(Path.GetFullPath(file))!=Path.GetFullPath(folder))return;
  try{File.Delete(file);}catch(IOException){}catch(UnauthorizedAccessException){}
 }
 public async Task<int> RunSilentAsync(string[] args)
 {
  var folder=Path.Combine(Path.GetTempPath(),"PixelLensSetup",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  var exe=await ExtractAsync(folder);
  var info=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
  foreach(var arg in args)info.ArgumentList.Add(arg);
  using var process=Process.Start(info)??throw new IOException("无法启动安装程序");
  await process.WaitForExitAsync();CleanupPayload(exe,folder);return process.ExitCode;
 }
}
