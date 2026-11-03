using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
namespace UICompare.Wpf.Services;
public static class WindowShapeService
{
 public const int RadiusPixels=10;
 [StructLayout(LayoutKind.Sequential)] private struct NativeRect {public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h,out NativeRect rect);
 [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int l,int t,int r,int b,int width,int height);
 [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr window,IntPtr region,bool redraw);
 [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr region);
 [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int l,int t,int r,int b);
 [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr hwnd,IntPtr region);
 [DllImport("gdi32.dll")] private static extern bool EqualRgn(IntPtr first,IntPtr second);
 [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window,int attr,ref int value,int size);
 public static void Apply(Window window,FrameworkElement content)
 {
  var hwnd=new WindowInteropHelper(window).Handle;
  if(hwnd==IntPtr.Zero||!GetWindowRect(hwnd,out var rect)||rect.Right<=rect.Left||rect.Bottom<=rect.Top)return;
  // The supplied radius is in physical pixels, independent of per-monitor DPI.
  var dpi=VisualTreeHelper.GetDpi(window);
  content.Clip=new RectangleGeometry(new Rect(0,0,Math.Max(0,content.ActualWidth),Math.Max(0,content.ActualHeight)),RadiusPixels/dpi.DpiScaleX,RadiusPixels/dpi.DpiScaleY);
  int noAutomaticRounding=1;DwmSetWindowAttribute(hwnd,33,ref noAutomaticRounding,sizeof(int));
  var region=CreateRoundRectRgn(0,0,rect.Right-rect.Left+1,rect.Bottom-rect.Top+1,RadiusPixels*2,RadiusPixels*2);
  if(region==IntPtr.Zero)throw new InvalidOperationException("无法设置窗口圆角");
  var current=CreateRectRgn(0,0,0,0);
  try { if(GetWindowRgn(hwnd,current)!=0 && EqualRgn(region,current)){DeleteObject(region);return;} }
  finally {DeleteObject(current);}
  // Windows owns the region after a successful SetWindowRgn.
  if(SetWindowRgn(hwnd,region,true)==0){DeleteObject(region);throw new InvalidOperationException("无法应用窗口圆角");}
 }
}
