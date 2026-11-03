using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
namespace UICompare.Wpf.Services;
public static class ScreenCaptureService
{
 [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
 [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
 [DllImport("dwmapi.dll")] public static extern int DwmFlush();
 [DllImport("user32.dll")] private static extern bool GetPhysicalCursorPos(out NativePoint p);
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr insert,int x,int y,int w,int height,uint flags);
 private delegate bool MonitorCallback(IntPtr monitor,IntPtr dc,ref NativeRect rect,IntPtr data);
 [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorCallback callback,IntPtr data);
 [StructLayout(LayoutKind.Sequential)] private struct NativePoint {public int X,Y;}
 [StructLayout(LayoutKind.Sequential)] private struct NativeRect {public int Left,Top,Right,Bottom;}
 public static System.Windows.Point CursorPixels(){if(!GetPhysicalCursorPos(out var p))throw new InvalidOperationException("无法读取鼠标位置");return new(p.X,p.Y);}
 public static Rectangle GetVirtualDesktopBounds()=>new(GetSystemMetrics(76),GetSystemMetrics(77),GetSystemMetrics(78),GetSystemMetrics(79));
 public static IReadOnlyList<Rectangle> Monitors()
 {
  var list=new List<Rectangle>();
  MonitorCallback callback=(IntPtr h,IntPtr dc,ref NativeRect r,IntPtr data)=>{list.Add(Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom));return true;};
  if(!EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,callback,IntPtr.Zero)||list.Count==0)throw new InvalidOperationException("无法读取显示器");
  return list;
 }
 public static BitmapSource CaptureVirtualDesktop()=>Capture(GetVirtualDesktopBounds());
 public static BitmapSource Capture(Rectangle bounds)
 {
  using var bitmap=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppPArgb);
  using(var graphics=Graphics.FromImage(bitmap))graphics.CopyFromScreen(bounds.Left,bounds.Top,0,0,bitmap.Size,CopyPixelOperation.SourceCopy);
  var handle=bitmap.GetHbitmap();
  try{var result=Imaging.CreateBitmapSourceFromHBitmap(handle,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());result.Freeze();return result;}
  finally{DeleteObject(handle);}
 }
 public static BitmapSource Crop(BitmapSource source,Int32Rect crop)
 {
  if(crop.Width<=0||crop.Height<=0||crop.X<0||crop.Y<0||(long)crop.X+crop.Width>source.PixelWidth||(long)crop.Y+crop.Height>source.PixelHeight)
   throw new ArgumentOutOfRangeException(nameof(crop),"截取区域超出桌面边界");
  var result=new CroppedBitmap(source,crop);result.Freeze();return result;
 }
}
