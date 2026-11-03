using System.Drawing;
using System.Windows.Media.Imaging;

namespace UICompare.Wpf.Models;

public sealed class DiffResult
{
    public int DifferencePixels { get; init; }
    public double DifferencePercent { get; init; }
    public IReadOnlyList<Rectangle> Regions { get; init; } = Array.Empty<Rectangle>();
    public BitmapSource? Overlay { get; init; }
    public bool HasDifference => Regions.Count > 0 || DifferencePixels > 0;
}
