using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UICompare.Wpf.Models;
namespace UICompare.Wpf.Services;
public static class AlignmentService
{
    public static BitmapSource RenderShot(ComparisonGroup group)
    {
        if (!group.Ready) throw new InvalidOperationException("请导入设计稿和截图");
        return Render(group.Design!.Width, group.Design.Height, group.Shot!.Image,
            new Rect(group.X, group.Y, group.Shot.Width * group.Scale, group.Shot.Height * group.Scale), false);
    }
    public static BitmapSource RenderDesign(ImageAsset design) => Render(design.Width, design.Height, design.Image,
        new Rect(0, 0, design.Width, design.Height), true);
    private static BitmapSource Render(int width, int height, BitmapSource source, Rect target, bool opaque)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));
            if (opaque) dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
            dc.DrawImage(source, target); dc.Pop();
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
}
