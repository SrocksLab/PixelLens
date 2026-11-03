using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
namespace PixelLens.Setup;
public enum InstallStage {Ready,Upgrade,Progress,Success,Failure,CancelConfirmation}
public partial class InstallerWindow:Window
{
 private readonly InstallationEngine engine;
 private CancellationTokenSource? cancellation;
 private bool running,allowClose;
 private double lastProgress;
 public InstallStage Stage {get;private set;}
 public InstallerWindow():this(new InstallationEngine()){}
 public InstallerWindow(InstallationEngine engine)
 {
  this.engine=engine;InitializeComponent();
  Height=Math.Min(520,SystemParameters.WorkArea.Height-24);MinHeight=Math.Min(420,Height);
  InstallPath.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","PixelLens");
  string? version=null;
  foreach(var id in new[]{"{B7925F07-0C7A-4A4C-89D7-123456789001}_is1","{B7925F07-0C7A-4A4C-89D7-123456789001}}_is1"}){
   using var installed=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\"+id);
   version??=installed?.GetValue("DisplayVersion") as string;
   if(installed?.GetValue("InstallLocation") is string previous && Path.IsPathRooted(previous))InstallPath.Text=previous;
  }
  ShowStage(version==null?InstallStage.Ready:InstallStage.Upgrade,version==null?null:"已安装 V"+version);
 }
 public void ShowStage(InstallStage stage,string? detail=null)
 {
  Stage=stage;
  PathPanel.Visibility=stage is InstallStage.Ready or InstallStage.Upgrade?Visibility.Visible:Visibility.Collapsed;
  ProgressPanel.Visibility=stage==InstallStage.Progress?Visibility.Visible:Visibility.Collapsed;
  SuccessMark.Visibility=stage==InstallStage.Success?Visibility.Visible:Visibility.Collapsed;
  BrandRow.Visibility=stage==InstallStage.CancelConfirmation?Visibility.Collapsed:Visibility.Visible;
  StageTitle.Text=stage switch {InstallStage.Ready=>"",InstallStage.Upgrade=>detail??"已安装版本",InstallStage.Progress=>"正在安装，请稍候…",InstallStage.Success=>"安装完成",InstallStage.Failure=>"安装未完成",_=>"取消安装？"};
  StageTitle.Visibility=string.IsNullOrEmpty(StageTitle.Text)?Visibility.Collapsed:Visibility.Visible;
  StageDetail.Text=stage switch {InstallStage.Ready=>"",InstallStage.Upgrade=>"可安装当前版本覆盖程序文件。",InstallStage.Progress=>"",InstallStage.Success=>"“像素之眼”已安装完成，现在开始界面走查。",InstallStage.Failure=>detail??"无法写入安装目录，请选择可写入的位置后重试。",_=>"安装尚未完成，取消后可重新启动安装器。"};
  StageDetail.Visibility=string.IsNullOrEmpty(StageDetail.Text)?Visibility.Collapsed:Visibility.Visible;
  PrimaryButton.Content=stage switch {InstallStage.Ready=>"开始安装",InstallStage.Upgrade=>"更新安装",InstallStage.Progress=>"正在安装…",InstallStage.Success=>"启动软件",InstallStage.Failure=>"重新选择路径",_=>"取消安装"};
  PrimaryButton.IsEnabled=stage!=InstallStage.Progress;
  SecondaryButton.Content=stage switch {InstallStage.Success=>"完成",InstallStage.Progress=>"取消安装",InstallStage.CancelConfirmation=>"继续安装",_=>"取消"};
  SecondaryButton.IsEnabled=true;
 }
 private async void Primary_Click(object sender,RoutedEventArgs e)
 {
  if(Stage is InstallStage.Ready or InstallStage.Upgrade){await StartInstallAsync();return;}
  if(Stage==InstallStage.Failure){ShowStage(InstallStage.Ready);return;}
  if(Stage==InstallStage.CancelConfirmation){
   cancellation?.Cancel();ShowStage(InstallStage.Progress);StageTitle.Text="正在取消安装…";SecondaryButton.IsEnabled=false;return;
  }
  if(Stage==InstallStage.Success){
   try{Process.Start(new ProcessStartInfo(Path.Combine(InstallPath.Text,"UICompare.exe")){UseShellExecute=true});allowClose=true;Close();}
   catch{ShowStage(InstallStage.Failure,"无法启动软件，请检查安装位置后重试。");}
  }
 }
 public async Task<int> StartInstallAsync(bool noIcons=false)
 {
  if(running)return -1;
  string path;
  try{if(!Path.IsPathRooted(InstallPath.Text))throw new IOException();path=Path.GetFullPath(InstallPath.Text.Trim());}
  catch{ShowStage(InstallStage.Failure,"请填写有效的完整安装路径。");return -1;}
  InstallPath.Text=path;cancellation=new CancellationTokenSource();running=true;lastProgress=0;UpdateProgress(0);ShowStage(InstallStage.Progress);
  try{
   var result=await engine.RunAsync(path,new Progress<double>(UpdateProgress),cancellation.Token,noIcons);
   running=false;
   if(result==0){UpdateProgress(1);ShowStage(InstallStage.Success);}
   else if(cancellation.IsCancellationRequested){allowClose=true;Close();}
   else ShowStage(InstallStage.Failure);
   return result;
  }catch(Exception){running=false;ShowStage(InstallStage.Failure,"安装文件无法读取或无法写入目标位置，请检查路径后重试。");return -1;}
  finally{cancellation.Dispose();cancellation=null;running=false;}
 }
 private void UpdateProgress(double progress)
 {
  lastProgress=Math.Max(lastProgress,Math.Clamp(progress,0,1));
  ProgressPercent.Text=(lastProgress*100).ToString("0")+"%";
  ProgressFill.Width=lastProgress*Math.Max(0,InstallCard.Width-54);
  if(running&&lastProgress>=1&&Stage==InstallStage.Progress){StageTitle.Text="正在完成安装…";SecondaryButton.IsEnabled=false;}
 }
 private void Secondary_Click(object sender,RoutedEventArgs e)
 {
  if(Stage==InstallStage.CancelConfirmation){ShowStage(InstallStage.Progress);return;}
  if(running){ShowStage(InstallStage.CancelConfirmation);return;}
  allowClose=true;Close();
 }
 private void Close_Click(object sender,RoutedEventArgs e){if(running&&lastProgress>=1)return;if(running)ShowStage(InstallStage.CancelConfirmation);else{allowClose=true;Close();}}
 private void Window_Closing(object? sender,CancelEventArgs e){if(running&&!allowClose){e.Cancel=true;if(lastProgress<1)ShowStage(InstallStage.CancelConfirmation);}}
 private void Title_Down(object sender,MouseButtonEventArgs e){if(e.ButtonState==MouseButtonState.Pressed)DragMove();}
}
