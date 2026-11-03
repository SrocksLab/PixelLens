using System.ComponentModel;
using System.Windows.Media.Imaging;
namespace UICompare.Wpf.Models;
public sealed record ImageAsset(string Name, string? Path, BitmapSource Image)
{
    public int Width => Image.PixelWidth;
    public int Height => Image.PixelHeight;
    public string Size => $"{Width} × {Height}";
}
public sealed class ComparisonGroup : INotifyPropertyChanged
{
    public Guid Id { get; } = Guid.NewGuid();
    public ImageAsset? Design { get; internal set; }
    public ImageAsset? Shot { get; internal set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public double AnchorX { get; private set; }
    public double AnchorY { get; private set; }
    public double Scale { get; private set; } = 1;
    public double Opacity { get; set; } = .5;
    public long Revision { get; private set; }
    public DiffResult? Result { get; set; }
    public bool HasDesign => Design != null;
    public bool HasShot => Shot != null;
    public bool Ready => HasDesign && HasShot;
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Reset()
    {
        Scale = 1; Opacity = .5;
        X = ((Design?.Width ?? Shot?.Width ?? 0) - (Shot?.Width ?? 0)) / 2d;
        Y = ((Design?.Height ?? Shot?.Height ?? 0) - (Shot?.Height ?? 0)) / 2d;
        AnchorX = X + (Shot?.Width ?? 0) / 2d;
        AnchorY = Y + (Shot?.Height ?? 0) / 2d;
        Invalidate();
    }
    public void SetTransform(double x, double y, double scale)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(scale) || scale < .25 || scale > 3)
            throw new ArgumentOutOfRangeException(nameof(scale));
        if (Design != null && Shot != null && Math.Abs(scale-Scale)<.000001)
        {
            var centerX=(Design.Width-Shot.Width*scale)/2;
            var centerY=(Design.Height-Shot.Height*scale)/2;
            x=Math.Clamp(x,centerX-Design.Width*.75,centerX+Design.Width*.75);
            y=Math.Clamp(y,centerY-Design.Height*.75,centerY+Design.Height*.75);
        }
        AnchorX += x-X; AnchorY += y-Y;
        X = x; Y = y; Scale = scale; Invalidate();
    }
    public void SetAnchor(double x,double y)
    {
        if(!double.IsFinite(x)||!double.IsFinite(y))throw new ArgumentOutOfRangeException(nameof(x));
        AnchorX=x;AnchorY=y; // Marker placement doesn't change image pixels or invalidate results.
    }
    public void Zoom(double scale)
    {
        scale=Math.Clamp(scale,.25,3);var ratio=scale/Scale;
        X=AnchorX-(AnchorX-X)*ratio;Y=AnchorY-(AnchorY-Y)*ratio;Scale=scale;Invalidate();
    }
    public void ScaleAt(double scale,double anchorX,double anchorY)
    { SetAnchor(anchorX,anchorY);Zoom(scale); }
    public void Invalidate() { Revision++; Result = null; }
    internal void AssetsChanged() { Reset(); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null)); }
}
