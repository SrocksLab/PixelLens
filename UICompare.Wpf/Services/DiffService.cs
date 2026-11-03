using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using UICompare.Wpf.Models;
namespace UICompare.Wpf.Services;
public static class DiffService
{
    // Inputs use design pixels, not preview DIPs. Alpha records screenshot coverage.
    public static DiffResult Compare(BitmapSource design, BitmapSource screenshot, double threshold = 26,
        CancellationToken cancellationToken = default)
    {
        if (design.PixelWidth != screenshot.PixelWidth || design.PixelHeight != screenshot.PixelHeight)
            throw new ArgumentException("对比画布尺寸必须一致");
        cancellationToken.ThrowIfCancellationRequested();
        using var a = Decode(design); using var b = Decode(screenshot);
        using var colorsA = new Mat(); using var colorsB = new Mat();
        Cv2.CvtColor(a, colorsA, ColorConversionCodes.BGRA2BGR);
        Cv2.CvtColor(b, colorsB, ColorConversionCodes.BGRA2BGR);
        using var delta = new Mat(); Cv2.Absdiff(colorsA, colorsB, delta);
        var channels = Cv2.Split(delta);
        using var max = new Mat();
        try { Cv2.Max(channels[0], channels[1], max); Cv2.Max(max, channels[2], max); }
        finally { foreach (var c in channels) c.Dispose(); }
        using var changed = new Mat(); Cv2.Threshold(max, changed, threshold, 255, ThresholdTypes.Binary);
        using var alpha = new Mat(); Cv2.ExtractChannel(b, alpha, 3);
        using var covered = new Mat(); Cv2.Threshold(alpha, covered, 0, 255, ThresholdTypes.Binary);
        Cv2.BitwiseAnd(changed, covered, changed);
        using var missing = new Mat(); Cv2.BitwiseNot(covered, missing);
        using var mask = new Mat(); Cv2.BitwiseOr(changed, missing, mask);
        cancellationToken.ThrowIfCancellationRequested();
        Cv2.FindContours(changed, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var regions = contours.Select(Cv2.BoundingRect).ToList();
        // Missing pixels are drawn exactly; an enclosing contour would incorrectly paint the entire frame.
        using var overlayMask = missing.Clone();
        foreach (var r in regions) { cancellationToken.ThrowIfCancellationRequested(); Cv2.Rectangle(overlayMask, r, Scalar.White, -1); }
        using var overlay = new Mat(mask.Rows, mask.Cols, MatType.CV_8UC4, Scalar.All(0));
        overlay.SetTo(new Scalar(83, 65, 255, 64), overlayMask);
        var bytes = new byte[checked(mask.Rows * mask.Cols * 4)];
        System.Runtime.InteropServices.Marshal.Copy(overlay.Data, bytes, 0, bytes.Length);
        var bitmap = BitmapSource.Create(mask.Cols, mask.Rows, 96, 96, PixelFormats.Bgra32, null, bytes, mask.Cols * 4);
        bitmap.Freeze();
        var count = Cv2.CountNonZero(mask);
        return new DiffResult { DifferencePixels = count, DifferencePercent = count * 100d / ((long)mask.Width * mask.Height),
            Regions = regions.Select(r => new System.Drawing.Rectangle(r.X, r.Y, r.Width, r.Height)).ToArray(), Overlay = bitmap };
    }
    private static Mat Decode(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream(); encoder.Save(stream);
        using var decoded = Cv2.ImDecode(stream.ToArray(), ImreadModes.Unchanged);
        var bgra = new Mat();
        if (decoded.Channels() == 4) decoded.CopyTo(bgra);
        else Cv2.CvtColor(decoded, bgra, decoded.Channels() == 3 ? ColorConversionCodes.BGR2BGRA : ColorConversionCodes.GRAY2BGRA);
        return bgra;
    }
}
