using System.Windows;
namespace UICompare.Wpf.Models;
// All points and rectangles are physical pixels relative to the virtual desktop.
public sealed class CaptureSelection
{
 public Rect Bounds { get; }
 public Rect Rectangle { get; private set; } = Rect.Empty;
 public bool Fixed { get; private set; }
 public Size FixedSize { get; }
 private Point start; private Rect original; private string operation="";
 public CaptureSelection(int width,int height,int fixedWidth,int fixedHeight)
 { Bounds=new Rect(0,0,width,height);FixedSize=new Size(fixedWidth,fixedHeight);Fixed=fixedWidth<=width&&fixedHeight<=height; }
 public void SetMode(bool fixedMode) { Fixed=fixedMode&&FixedSize.Width<=Bounds.Width&&FixedSize.Height<=Bounds.Height;Rectangle=Rect.Empty;operation=""; }
 public string Hit(Point p,double tolerance)
 {
  if(Rectangle.IsEmpty)return "";
  var expanded=Rectangle;expanded.Inflate(tolerance,tolerance);if(!expanded.Contains(p))return "";
  if(Fixed)return Rectangle.Contains(p)?"move":"";
  string x=Math.Abs(p.X-Rectangle.Left)<=tolerance?"l":Math.Abs(p.X-Rectangle.Right)<=tolerance?"r":"";
  string y=Math.Abs(p.Y-Rectangle.Top)<=tolerance?"t":Math.Abs(p.Y-Rectangle.Bottom)<=tolerance?"b":"";
  return x+y is {Length:>0} edge?edge:Rectangle.Contains(p)?"move":"";
 }
 public void Begin(Point p,double tolerance)
 {
  p=Clamp(p);start=p;original=Rectangle;operation=Hit(p,tolerance);
  if(operation.Length>0)return;
  if(Fixed){Rectangle=Fit(new Rect(p.X-FixedSize.Width/2,p.Y-FixedSize.Height/2,FixedSize.Width,FixedSize.Height));original=Rectangle;operation="move";}
  else {Rectangle=new Rect(p,p);operation="new";}
 }
 public void Update(Point p)
 {
  p=Clamp(p);
  if(operation=="new")Rectangle=new Rect(start,p);
  else if(operation=="move")Rectangle=Fit(new Rect(original.X+p.X-start.X,original.Y+p.Y-start.Y,original.Width,original.Height));
  else if(operation.Length>0){
   var l=operation.Contains('l')?p.X:original.Left;var rr=operation.Contains('r')?p.X:original.Right;
   var t=operation.Contains('t')?p.Y:original.Top;var b=operation.Contains('b')?p.Y:original.Bottom;
   Rectangle=new Rect(new Point(l,t),new Point(rr,b));
  }
 }
 public void End(){operation="";}
 private Point Clamp(Point p)=>new(Math.Clamp(Math.Round(p.X),0,Bounds.Width),Math.Clamp(Math.Round(p.Y),0,Bounds.Height));
 private Rect Fit(Rect r)=>new(Math.Clamp(r.X,0,Bounds.Width-r.Width),Math.Clamp(r.Y,0,Bounds.Height-r.Height),r.Width,r.Height);
 public Int32Rect Crop => Rectangle.IsEmpty?Int32Rect.Empty:new((int)Math.Round(Rectangle.X),(int)Math.Round(Rectangle.Y),(int)Math.Round(Rectangle.Width),(int)Math.Round(Rectangle.Height));
}
