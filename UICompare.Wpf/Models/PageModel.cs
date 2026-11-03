using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace UICompare.Wpf.Models;

public sealed class PageModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private BitmapSource? _designImage;
    private BitmapSource? _screenshotImage;
    private string _designPath = string.Empty;
    private string _screenshotPath = string.Empty;
    private string _status = "未比对";

    public string Name { get => _name; set => Set(ref _name, value); }
    public BitmapSource? DesignImage { get => _designImage; set => Set(ref _designImage, value); }
    public BitmapSource? ScreenshotImage { get => _screenshotImage; set => Set(ref _screenshotImage, value); }
    public string DesignPath { get => _designPath; set => Set(ref _designPath, value); }
    public string ScreenshotPath { get => _screenshotPath; set => Set(ref _screenshotPath, value); }
    public int DesignWidth => DesignImage?.PixelWidth ?? 0;
    public int DesignHeight => DesignImage?.PixelHeight ?? 0;
    public int ScreenshotWidth => ScreenshotImage?.PixelWidth ?? 0;
    public int ScreenshotHeight => ScreenshotImage?.PixelHeight ?? 0;
    public string Status { get => _status; set => Set(ref _status, value); }
    public bool HasScreenshot => ScreenshotImage is not null;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
