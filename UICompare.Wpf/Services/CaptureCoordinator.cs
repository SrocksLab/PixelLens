using System.Windows.Media.Imaging;
using UICompare.Wpf.Models;
using UICompare.Wpf.Views;
namespace UICompare.Wpf.Services;
public sealed class CaptureCoordinator
{
 public System.Drawing.Rectangle Bounds {get;}
 public BitmapSource Desktop {get;}
 public CaptureSelection Selection {get;}
 public IReadOnlyList<System.Drawing.Rectangle> Monitors {get;}
 public List<CaptureWindow> Windows {get;}=[];
 private readonly TaskCompletionSource<BitmapSource?> completion=new();
 private bool finished;
 public CaptureCoordinator(int width,int height)
 {
  Monitors=ScreenCaptureService.Monitors();Bounds=ScreenCaptureService.GetVirtualDesktopBounds();
  Desktop=ScreenCaptureService.Capture(Bounds); // No overlay exists until after this frozen snapshot.
  Selection=new CaptureSelection(Bounds.Width,Bounds.Height,width,height);
 }
 public Task<BitmapSource?> RunAsync()
 {
  try{foreach(var monitor in Monitors)Windows.Add(new CaptureWindow(this,monitor));foreach(var window in Windows)window.Show();}
  catch(Exception ex){Finish(null,ex);}
  return completion.Task;
 }
 public void Refresh(){foreach(var window in Windows)window.RefreshSelection();}
 public void Confirm()
 {
  if(Selection.Crop.Width<2||Selection.Crop.Height<2)return;
  try{Finish(ScreenCaptureService.Crop(Desktop,Selection.Crop));}catch(Exception ex){Finish(null,ex);}
 }
 public void Cancel()=>Finish(null);
 private void Finish(BitmapSource? image,Exception? error=null)
 {
  if(finished)return;finished=true;
  foreach(var window in Windows)window.Close();
  if(error!=null)completion.TrySetException(error);else completion.TrySetResult(image);
 }
 public bool IsFinished=>finished;
}
