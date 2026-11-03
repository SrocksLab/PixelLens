using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using UICompare.Wpf.Services;
namespace UICompare.Wpf.Views;
public partial class CaptureWindow:Window
{
 private readonly CaptureCoordinator capture;
 private readonly System.Drawing.Rectangle monitor;
 private bool dragging;
 private double sx=1,sy=1;
 public CaptureWindow(CaptureCoordinator coordinator,System.Drawing.Rectangle monitor)
 {
  InitializeComponent();capture=coordinator;this.monitor=monitor;
  ScreenImage.Source=ScreenCaptureService.Crop(capture.Desktop,new Int32Rect(monitor.X-capture.Bounds.X,monitor.Y-capture.Bounds.Y,monitor.Width,monitor.Height));
  SourceInitialized+=(_,_)=>Place();
  Loaded+=(_,_)=>{Place();RefreshSelection();};
  DpiChanged+=(_,_)=>Dispatcher.BeginInvoke(new Action(()=>{Place();RefreshSelection();}));
  SizeChanged+=(_,_)=>RefreshSelection();
  Closed+=(_,_)=>{if(!capture.IsFinished)capture.Cancel();};
 }
 private void Place()
 {
  var hwnd=new WindowInteropHelper(this).Handle;
  ScreenCaptureService.SetWindowPos(hwnd,new IntPtr(-1),monitor.X,monitor.Y,monitor.Width,monitor.Height,0x10);
  var dpi=VisualTreeHelper.GetDpi(this);sx=dpi.DpiScaleX;sy=dpi.DpiScaleY;
 }
 private Point Pointer(){var p=ScreenCaptureService.CursorPixels();return new(p.X-capture.Bounds.X,p.Y-capture.Bounds.Y);}
 private Point Local(Point p)=>new((p.X+capture.Bounds.X-monitor.X)/sx,(p.Y+capture.Bounds.Y-monitor.Y)/sy);
 public void RefreshSelection()
 {
  if(!IsLoaded)return;
  var selection=capture.Selection.Rectangle;
  var outer=new Rect(0,0,ActualWidth,ActualHeight);
  var geometry=new GeometryGroup{FillRule=FillRule.EvenOdd};geometry.Children.Add(new RectangleGeometry(outer));
  Handles.Children.Clear();
  var valid=!selection.IsEmpty&&selection.Width>=2&&selection.Height>=2;
  SelectionBorder.Visibility=SizeText.Visibility=valid?Visibility.Visible:Visibility.Collapsed;
  var p=valid?Local(selection.TopLeft):new Point();
  if(valid){
   var local=new Rect(p,new Size(selection.Width/sx,selection.Height/sy));
   geometry.Children.Add(new RectangleGeometry(local));
   Canvas.SetLeft(SelectionBorder,p.X);Canvas.SetTop(SelectionBorder,p.Y);SelectionBorder.Width=local.Width;SelectionBorder.Height=local.Height;
   if(!capture.Selection.Fixed)foreach(var x in new[]{local.Left,local.Left+local.Width/2,local.Right})foreach(var y in new[]{local.Top,local.Top+local.Height/2,local.Bottom}){
    if(x==local.Left+local.Width/2&&y==local.Top+local.Height/2)continue;
    var dot=new Rectangle{Width=11,Height=11,Fill=new SolidColorBrush(Color.FromRgb(244,247,255)),Stroke=new SolidColorBrush(Color.FromRgb(113,151,255)),StrokeThickness=2};
    Canvas.SetLeft(dot,x-5.5);Canvas.SetTop(dot,y-5.5);Handles.Children.Add(dot);
   }
   SizeText.Text=$"{capture.Selection.Crop.Width} × {capture.Selection.Crop.Height}";
   Canvas.SetLeft(SizeText,Math.Clamp(p.X,4,Math.Max(4,ActualWidth-160)));Canvas.SetTop(SizeText,Math.Clamp(p.Y+5,4,Math.Max(4,ActualHeight-32)));
  }
  Shade.Data=geometry;
  CaptureModeText.Text=capture.Selection.Fixed?"按设计稿尺寸截取":"自由框选";
  FreeHint.Visibility=!valid&&!capture.Selection.Fixed?Visibility.Visible:Visibility.Collapsed;
  // Only the monitor nearest the lower-right corner owns the action panel.
  var anchor=valid?new Point(selection.Right+capture.Bounds.X-1,selection.Bottom+capture.Bounds.Y-1):ScreenCaptureService.CursorPixels();
  var nearest=capture.Monitors.OrderBy(m=>Distance(anchor,m)).First();
  ActionPanel.Visibility=valid&&nearest==monitor?Visibility.Visible:Visibility.Collapsed;
  if(ActionPanel.Visibility==Visibility.Visible){
   ActionPanel.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
   var w=ActionPanel.DesiredSize.Width;var h=ActionPanel.DesiredSize.Height;
   var a=Local(selection.BottomRight);
   Canvas.SetLeft(ActionPanel,Math.Clamp(a.X-w,4,Math.Max(4,ActualWidth-w-4)));
   var y=a.Y+8;if(y+h>ActualHeight-4)y=Math.Max(4,a.Y-h-8);
   Canvas.SetTop(ActionPanel,Math.Clamp(y,4,Math.Max(4,ActualHeight-h-4)));
  }
  // Keep mode choices away from an active selection and available before drawing.
  ModePanel.Visibility=Visibility.Visible;
 }
 private static double Distance(Point p,System.Drawing.Rectangle m){var x=Math.Max(m.Left-p.X,Math.Max(0,p.X-m.Right));var y=Math.Max(m.Top-p.Y,Math.Max(0,p.Y-m.Bottom));return x*x+y*y;}
 private void Canvas_Down(object s,MouseButtonEventArgs e){capture.Selection.Begin(Pointer(),8*Math.Max(sx,sy));dragging=true;OverlayCanvas.CaptureMouse();capture.Refresh();e.Handled=true;}
 private void Canvas_Move(object s,MouseEventArgs e){
  if(dragging){capture.Selection.Update(Pointer());capture.Refresh();}
  else{var hit=capture.Selection.Hit(Pointer(),8*Math.Max(sx,sy));Cursor=hit=="move"?Cursors.Hand:hit is "lt" or "rb"?Cursors.SizeNWSE:hit is "rt" or "lb"?Cursors.SizeNESW:hit is "l" or "r"?Cursors.SizeWE:hit is "t" or "b"?Cursors.SizeNS:Cursors.Cross;}
 }
 private void Canvas_Up(object s,MouseButtonEventArgs e){dragging=false;capture.Selection.End();OverlayCanvas.ReleaseMouseCapture();capture.Refresh();}
 private void Canvas_Lost(object s,MouseEventArgs e){dragging=false;capture.Selection.End();}
 private void Fixed_Click(object s,RoutedEventArgs e){capture.Selection.SetMode(true);capture.Refresh();}
 private void Free_Click(object s,RoutedEventArgs e){capture.Selection.SetMode(false);capture.Refresh();}
 private void Confirm_Click(object s,RoutedEventArgs e){e.Handled=true;capture.Confirm();}
 private void Cancel_Click(object s,RoutedEventArgs e){e.Handled=true;capture.Cancel();}
}
