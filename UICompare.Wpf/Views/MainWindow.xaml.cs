using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using UICompare.Wpf.Models;
using UICompare.Wpf.Services;

namespace UICompare.Wpf.Views;

public partial class MainWindow : Window
{
    public WorkspaceSession Session { get; } = new();
    private bool _rendering, _scrollSync, _dragging, _busy, _closed;
    private string _view = "design";
    private Point _lastPoint, _gripPoint;
    private ComparisonGroup? _gripSource;
    private CancellationTokenSource? _comparison;
    private Action? _decision;
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    public MainWindow()
    {
        InitializeComponent();
        ResponsiveResources(1380);
        PreviewHost.LayoutUpdated += (_, _) => UpdateAnchorMarker();
        SourceInitialized += (_, _) => {
            var source=(System.Windows.Interop.HwndSource)PresentationSource.FromVisual(this);
            source.AddHook((IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled) => {
                if(message is 0x0005 or 0x0047 or 0x02E0) QueueWindowShape();
                return IntPtr.Zero;
            });
        };
        DpiChanged += (_, _) => QueueWindowShape();
        DesignList.ItemsSource = ShotList.ItemsSource = Session.Groups;
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); Toast.Visibility = Visibility.Collapsed; };
        StateChanged += (_, _) => {
            MaximizeIcon.Data = (Geometry)FindResource(WindowState == WindowState.Maximized ? "RestoreGeometry" : "MaximizeGeometry");
            MaxHeight = SystemParameters.WorkArea.Height;
            QueueWindowShape();
        };
        var area = SystemParameters.WorkArea;
        MinWidth = Math.Min(940, area.Width * .9); MinHeight = Math.Min(580, area.Height * .9);
        Width = Math.Min(1380, area.Width * .94); Height = Math.Min(800, area.Height * .92);
        Render();
    }
    private void Window_Loaded(object sender, RoutedEventArgs e) { ResponsiveLayout(); Render(); QueueWindowShape(); }
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) { if (IsLoaded) { ResponsiveLayout(); FitPreview(); QueueWindowShape(); } }
    private bool _shapeQueued;
    private void QueueWindowShape()
    {
        if (_shapeQueued || _closed) return;
        _shapeQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(() => {
            _shapeQueued = false;
            if (!_closed && IsLoaded) WindowShapeService.Apply(this,Root);
        }));
    }
    private void ResponsiveLayout()
    {
        var w = Math.Max(ActualWidth, 1);
        ResponsiveResources(w);
        var side = w <= 900 ? 176 : w < 1100 ? 210 : Math.Clamp((w - 780) / 2, 210, 300);
        LeftColumn.Width = RightColumn.Width = new GridLength(side);
        var h = ActualHeight;
        var view = h <= 560 ? 100 : h <= 760 ? 130 : 170;
        var footer = h <= 560 ? 118 : h <= 760 ? 128 : 158;
        ViewRow.Height = new GridLength(view); FooterRow.Height = new GridLength(footer);
        TabBar.Margin = new Thickness(12, h <= 560 ? 28 : h <= 760 ? 44 : 70, 12, 0);
        TabBar.Width = Math.Min(400, Math.Max(100, Center.ActualWidth - 24));
        Feedback.Margin = new Thickness(12, view - 47, 12, 0);
        var narrow = ActualWidth - side * 2 <= 710;
        Grid.SetRow(CompareActions, narrow ? 1 : 0);
        CompareActions.Margin = new Thickness(0, narrow ? 12 : 0, 0, 0);
        ControlsGrid.Margin = new Thickness(14, narrow ? 16 : h <= 760 ? 20 : 26, 14, 0);
        OpacitySlider.Width = ScaleSlider.Width = w <= 900 ? 65 : 88;
    }
    private void ResponsiveResources(double width)
    {
        var thumb = width <= 900 ? 40d : width < 1100 ? 46d : 64d;
        Resources["ThumbColumn"] = new GridLength(thumb);
        Resources["ThumbWidth"] = thumb; Resources["ThumbHeight"] = width <= 900 ? 48d : width < 1100 ? 52d : 58d;
        Resources["RowTextMargin"] = new Thickness(width <= 900 ? 5 : width < 1100 ? 7 : 10, 0, 4, 0);
        Resources["RowNameSize"] = width <= 900 ? 12d : width < 1100 ? 13d : 14d;
        Resources["RowSizeFont"] = width <= 900 ? 10d : width < 1100 ? 11d : 12d;
        Resources["FooterFontSize"] = width <= 900 ? 12d : width < 1100 ? 13d : 14d;
        Resources["FooterButtonPadding"] = new Thickness(width <= 900 ? 6 : width < 1100 ? 7 : 12, 0, width <= 900 ? 6 : width < 1100 ? 7 : 12, 0);
        Resources["EmptyShotWidth"] = width <= 900 ? 71d : width < 1100 ? 84d : 100d;
    }
    private static Visibility Visible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public void RefreshWorkspace() { Render(); }
    private void Render()
    {
        if (PixelFrame == null || _closed) return;
        _rendering = true;
        var group = Session.Selected;
        if (group == null || !group.Ready) _view = "design";
        if (_view == "diff" && group?.Result == null) _view = group?.Ready == true ? "overlay" : "design";
        DesignList.SelectedItem = ShotList.SelectedItem = group;
        DesignCount.Text = $"{Session.Groups.Count(g => g.HasDesign)} 张";
        ShotCount.Text = $"{Session.Groups.Count(g => g.HasShot)} 张";
        var empty = Session.Groups.Count == 0;
        DesignList.Visibility = ShotList.Visibility = Visible(!empty);
        DesignEmpty.Visibility = ShotEmpty.Visibility = Visible(empty);
        DesignFooter.Visibility = ShotFooter.Visibility = Visible(!empty);
        CenterEmpty.Visibility = Visible(group?.Design == null);
        CenterEmptyTitle.Text = !empty && group == null ? "请选择设计稿" : "从一张设计稿开始";
        PixelFrame.Visibility = Visible(group?.Design != null);
        CompareActions.Visibility = Visible(!empty);
        AlignmentControls.Visibility = Visible(group?.HasDesign == true);
        ResetButton.IsEnabled = CanAlign;
        CompareButton.IsEnabled = group?.Ready == true;
        CompareButton.Content = _busy ? "取消对比" : group?.Result != null ? "重新对比" : "开始对比";
        OpacitySlider.IsEnabled = group?.Ready == true && !_busy && _view == "overlay";
        ScaleSlider.IsEnabled = CanAlign;
        DesignTab.IsChecked = _view == "design"; ShotTab.IsChecked = _view == "shot";
        OverlayTab.IsChecked = _view == "overlay"; DiffTab.IsChecked = _view == "diff";
        ShotTab.Foreground = OverlayTab.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(group?.Ready == true ? "#94A6C2" : "#6C7A91"));
        var w = group?.Design?.Width ?? group?.Shot?.Width ?? 1;
        var h = group?.Design?.Height ?? group?.Shot?.Height ?? 1;
        PixelFrame.Width = w; PixelFrame.Height = h;
        LogicalWorkspace.Width = w * 1.5; LogicalWorkspace.Height = h * 1.5;
        DesignImage.Source = group?.Design?.Image; DesignImage.Width = w; DesignImage.Height = h;
        DesignImage.Visibility = Visible(_view != "shot");
        ScreenshotImage.Source = group?.Shot?.Image;
        ScreenshotImage.Visibility = Visible(_view != "design");
        if (group?.Shot != null)
        {
            ScreenshotImage.Width = group.Shot.Width * group.Scale;
            ScreenshotImage.Height = group.Shot.Height * group.Scale;
            Canvas.SetLeft(ScreenshotImage, group.X); Canvas.SetTop(ScreenshotImage, group.Y);
        }
        ScreenshotImage.Opacity = _view == "overlay" ? group?.Opacity ?? .5 : 1;
        DifferenceImage.Source = group?.Result?.Overlay; DifferenceImage.Width = w; DifferenceImage.Height = h;
        DifferenceImage.Visibility = Visible(_view == "diff");
        PixelFrame.Cursor = CanAlign ? Cursors.Hand : Cursors.Arrow;
        OpacitySlider.Value = (group?.Opacity ?? .5) * 100;
        ScaleSlider.Value = (group?.Scale ?? 1) * 100;
        OpacityValue.Text = $"{OpacitySlider.Value:0}%"; ScaleValue.Text = $"{ScaleSlider.Value:0}%";
        Feedback.Text = _busy ? "正在对比…" : group?.Result != null && _view == "diff"
            ? group.Result.HasDifference ? $"差异像素 {group.Result.DifferencePercent:0.00}% · 红色区域为差异" : "未发现差异"
            : group != null && !group.Ready ? group.Design != null ? "当前设计稿暂无对应截图，请导入截图或去截屏" : "当前截图暂无对应设计稿，请导入设计稿" : "";
        _rendering = false;
        FitPreview();
        UpdateAnchorMarker();
    }
    private bool CanAlign => !_busy && Session.Selected?.Ready == true && _view is "shot" or "overlay";
    private void FitPreview()
    {
        if (PreviewHost == null || PixelFrame == null || PixelFrame.Width <= 0) return;
        var fit = Math.Max(.001, Math.Min(PreviewHost.ActualWidth / PixelFrame.Width, PreviewHost.ActualHeight / PixelFrame.Height));
        PixelFrame.LayoutTransform = new ScaleTransform(fit, fit);
        // The 1.5x workspace is logical only; the visible comparison boundary is the design.
        LogicalWorkspace.LayoutTransform = new ScaleTransform(fit, fit);
        UpdateAnchorMarker();
    }
    private void Preview_SizeChanged(object sender, SizeChangedEventArgs e) => FitPreview();
    private bool _movingAnchor;
    private void UpdateAnchorMarker()
    {
        if(AnchorMarker==null)return;
        var g=Session.Selected;
        AnchorLayer.Visibility=Visible(g?.Ready==true && _view is "shot" or "overlay");
        AnchorMarker.IsEnabled=!_busy;
        if(g?.Ready!=true||!IsLoaded)return;
        var center=PixelFrame.TranslatePoint(new Point(g.AnchorX,g.AnchorY),PreviewHost);
        Canvas.SetLeft(AnchorMarker,center.X-12);Canvas.SetTop(AnchorMarker,center.Y-12);
    }
    private void Anchor_Down(object sender,MouseButtonEventArgs e)
    { if(!CanAlign)return;_movingAnchor=true;AnchorMarker.CaptureMouse();e.Handled=true; }
    private void Anchor_Move(object sender,MouseEventArgs e)
    {
        if(!_movingAnchor||!CanAlign||Session.Selected==null)return;
        var point=e.GetPosition(PixelFrame);
        Session.Selected.SetAnchor(Math.Clamp(point.X,0,PixelFrame.Width),Math.Clamp(point.Y,0,PixelFrame.Height));
        UpdateAnchorMarker();e.Handled=true;
    }
    private void Anchor_Up(object sender,MouseButtonEventArgs e){_movingAnchor=false;AnchorMarker.ReleaseMouseCapture();e.Handled=true;}
    private void Anchor_Lost(object sender,MouseEventArgs e){_movingAnchor=false;}
    private void DesignList_PreviewDown(object sender,MouseButtonEventArgs e)
    {
        var original=e.OriginalSource as DependencyObject;
        for(var node=original;node!=null&&node!=DesignList;node=VisualTreeHelper.GetParent(node))
            if(node is Button)return;
        var group=GroupAt(original);
        if(group!=null&&Session.Selected==group){CancelComparison();Session.Select(null);Render();e.Handled=true;}
    }
    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_rendering) return;
        if (((ListBox)sender).SelectedItem is not ComparisonGroup group) return;
        CancelComparison(); Session.Select(group); Render();
        DesignList.ScrollIntoView(group); ShotList.ScrollIntoView(group);
    }
    private void List_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_scrollSync || e.VerticalChange == 0) return;
        _scrollSync = true;
        try
        {
            var target = ReferenceEquals(sender, DesignList) ? ShotList : DesignList;
            FindVisual<ScrollViewer>(target)?.ScrollToVerticalOffset(e.VerticalOffset);
        }
        finally { _scrollSync = false; }
    }
    private static T? FindVisual<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null) return null;
        if (parent is T match) return match;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var found = FindVisual<T>(VisualTreeHelper.GetChild(parent, i)); if (found != null) return found; }
        return null;
    }
    private static ComparisonGroup? GroupAt(DependencyObject? obj)
    {
        while (obj != null)
        {
            if (obj is ListBoxItem row) return row.DataContext as ComparisonGroup;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return null;
    }
    private void ImportDesign_Click(object sender, RoutedEventArgs e) => ImportFiles(true);
    private void ImportShot_Click(object sender, RoutedEventArgs e) => ImportFiles(false);
    private void ImportFiles(bool design)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp", Title = design ? "导入设计稿" : "导入截图" };
        if (dialog.ShowDialog(this) != true) return;
        var images = new List<ImageAsset>(); var failed = 0;
        foreach (var file in dialog.FileNames)
            try { images.Add(new ImageAsset(System.IO.Path.GetFileName(file), file, ImageService.Load(file))); }
            catch { failed++; }
        if (images.Count == 0) { ShowToast("图片读取失败，请选择有效的图片"); return; }
        var selected = Session.Selected;
        var single = dialog.FileNames.Length == 1;
        void Import()
        {
            CancelComparison(); Session.Import(images, design, single);
            _view = Session.Selected?.Ready == true ? "overlay" : design ? "design" : "shot";
            Render(); if (Session.Selected != null) { DesignList.ScrollIntoView(Session.Selected); ShotList.ScrollIntoView(Session.Selected); }
            ShowToast(failed == 0 ? $"已导入 {images.Count} 张图片" : $"已导入 {images.Count} 张，{failed} 张读取失败");
        }
        if (single && (design ? selected?.HasDesign == true : selected?.HasShot == true))
            Ask("替换当前图片？", "将替换当前组的图片并清除该组对齐和对比结果，磁盘原文件不会删除。", "替换", Import);
        else Import();
    }
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ComparisonGroup group) return;
        e.Handled = true;
        var shot=((FrameworkElement)sender).Tag as string=="shot";
        Ask(shot?"删除当前截图？":"删除设计稿及对应截图？",
            shot?"移除当前截图和对比结果，保留设计稿，返回待截屏状态。磁盘原文件不会删除。":"同时移除设计稿、对应截图和对比结果。磁盘原文件不会删除。","删除",()=>{
                CancelComparison();if(shot)Session.ClearShot(group);else Session.Delete(group);_view="design";Render();
            });
    }
    private void Ask(string title, string text, string confirm, Action action)
    {
        _decision = action; DecisionTitle.Text = title; DecisionText.Text = text; DecisionConfirm.Content = confirm;
        DecisionLayer.Visibility = Visibility.Visible; DecisionConfirm.Focus();
    }
    private void DecisionCancel_Click(object sender, RoutedEventArgs e) { _decision = null; DecisionLayer.Visibility = Visibility.Collapsed; }
    private void DecisionConfirm_Click(object sender, RoutedEventArgs e)
    {
        var action = _decision; _decision = null; DecisionLayer.Visibility = Visibility.Collapsed; action?.Invoke();
    }
    private void Grip_Down(object sender, MouseButtonEventArgs e)
    {
        _gripSource = ((FrameworkElement)sender).DataContext as ComparisonGroup;
        _gripPoint = e.GetPosition(ShotList); e.Handled = true;
    }
    private void Grip_Move(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _gripSource?.Shot == null) return;
        var p = e.GetPosition(ShotList);
        if (Math.Abs(p.X - _gripPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(p.Y - _gripPoint.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        try { DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(ComparisonGroup), _gripSource), DragDropEffects.Move); }
        finally { _gripSource = null; }
        e.Handled = true;
    }
    private void Shot_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(ComparisonGroup)) && GroupAt(e.OriginalSource as DependencyObject) != null ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
        var viewer = FindVisual<ScrollViewer>(ShotList); var y = e.GetPosition(ShotList).Y;
        if (viewer != null && y < 24) viewer.ScrollToVerticalOffset(viewer.VerticalOffset - 12);
        else if (viewer != null && y > ShotList.ActualHeight - 24) viewer.ScrollToVerticalOffset(viewer.VerticalOffset + 12);
    }
    private void Shot_Drop(object sender, DragEventArgs e)
    {
        var target = GroupAt(e.OriginalSource as DependencyObject);
        if (target != null && e.Data.GetData(typeof(ComparisonGroup)) is ComparisonGroup source)
        { CancelComparison(); Session.MoveShot(source, target); Render(); }
        e.Handled = true;
    }
    private void View_Checked(object sender, RoutedEventArgs e)
    {
        if (_rendering || !IsLoaded) return;
        var requested = (string)((RadioButton)sender).Tag;
        if (requested != "design" && Session.Selected?.Ready != true)
        { ShowToast("请继续上传设计稿或者去截屏"); Render(); return; }
        if (requested == "diff" && Session.Selected?.Result == null)
        { _ = CompareSelectedAsync(); return; }
        _view = requested; Render();
    }
    private void Frame_Down(object sender, MouseButtonEventArgs e)
    {
        if (!CanAlign) return;
        _dragging = true; _lastPoint = e.GetPosition(PixelFrame); PixelFrame.CaptureMouse(); e.Handled = true;
    }
    private void Frame_Move(object sender, MouseEventArgs e)
    {
        if (!_dragging || !CanAlign || Session.Selected == null) return;
        var p = e.GetPosition(PixelFrame); var g = Session.Selected;
        g.SetTransform(g.X + p.X - _lastPoint.X, g.Y + p.Y - _lastPoint.Y, g.Scale);
        _lastPoint = p; Render(); e.Handled = true;
    }
    private void Frame_Up(object sender, MouseButtonEventArgs e) { _dragging = false; PixelFrame.ReleaseMouseCapture(); }
    private void Frame_LostCapture(object sender, MouseEventArgs e) { _dragging = false; }
    private void Frame_Wheel(object sender, MouseWheelEventArgs e)
    {
        if (!CanAlign || Session.Selected == null) return;
        var p = e.GetPosition(PixelFrame); var g = Session.Selected;
        g.Zoom(g.Scale + (e.Delta > 0 ? .05 : -.05)); Render(); e.Handled = true;
    }
    private void Opacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_rendering || !IsLoaded || Session.Selected?.Ready != true) return;
        Session.Selected.Opacity = e.NewValue / 100; Render();
    }
    private void Scale_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_rendering || !IsLoaded || Session.Selected?.Ready != true || _busy) return;
        var g = Session.Selected; g.Zoom(e.NewValue / 100);
        Render();
    }
    private void Reset_Click(object sender, RoutedEventArgs e) { if (Session.Selected?.Ready == true) { CancelComparison(); Session.Selected.Reset(); Render(); } }
    private async void Compare_Click(object sender, RoutedEventArgs e) => await CompareSelectedAsync();
    public async Task CompareSelectedAsync()
    {
        if (_busy) { CancelComparison(); Render(); ShowToast("已取消对比"); return; }
        var group = Session.Selected; if (group?.Ready != true) return;
        var tokenSource = new CancellationTokenSource(); _comparison = tokenSource;
        var revision = group.Revision; _busy = true; Render();
        try
        {
            // Allow the busy state to paint, then freeze an immutable, original-pixel snapshot.
            await Dispatcher.Yield(DispatcherPriority.Background);
            tokenSource.Token.ThrowIfCancellationRequested();
            var design = AlignmentService.RenderDesign(group.Design!);
            var shot = AlignmentService.RenderShot(group);
            var result = await Task.Run(() => DiffService.Compare(design, shot, cancellationToken: tokenSource.Token), tokenSource.Token);
            if (_closed || tokenSource.IsCancellationRequested || group.Revision != revision ||
                Session.Selected != group || !Session.Groups.Contains(group)) return;
            group.Result = result; _view = "diff";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_closed && !tokenSource.IsCancellationRequested) ShowToast($"对比失败：{ex.Message}"); }
        finally
        {
            if (_comparison == tokenSource) { _comparison = null; _busy = false; if (!_closed) Render(); }
            tokenSource.Dispose();
        }
    }
    private void CancelComparison() { _comparison?.Cancel(); _comparison = null; _busy = false; }
    private void Capture_Click(object sender,RoutedEventArgs e)
    {
        if(Session.Selected?.Design==null){ShowToast("请先选择设计稿");return;}
        CaptureDesignSize.Text=Session.Selected.Design.Size;
        CaptureModeLayer.Visibility=Visibility.Visible;
    }
    private void CancelCaptureMode_Click(object s,RoutedEventArgs e)=>CaptureModeLayer.Visibility=Visibility.Collapsed;
    private void FixedCapture_Click(object s,RoutedEventArgs e)=>PrepareCapture(true);
    private void FreeCapture_Click(object s,RoutedEventArgs e)=>PrepareCapture(false);
    private void PrepareCapture(bool fixedMode)
    {
        CaptureModeLayer.Visibility=Visibility.Collapsed;
        var group=Session.Selected;if(group?.Design==null)return;
        async void Capture()
        {
            CancelComparison();Hide();
            var previousShutdown=Application.Current.ShutdownMode;
            Application.Current.ShutdownMode=ShutdownMode.OnExplicitShutdown;
            try {
                await Dispatcher.Yield(DispatcherPriority.Render);await Task.Delay(120);ScreenCaptureService.DwmFlush();
                var capture=new CaptureCoordinator(group.Design.Width,group.Design.Height);
                if(fixedMode&&!capture.Selection.Fixed){Show();Activate();ShowToast("固定选框超出屏幕范围，请选择自由框选");return;}
                capture.Selection.SetMode(fixedMode);
                if(fixedMode){capture.Selection.Begin(new Point(capture.Bounds.Width/2d,capture.Bounds.Height/2d),0);capture.Selection.End();}
                var image=await capture.RunAsync();
                if(image!=null&&Session.Groups.Contains(group)){
                    Session.Select(group);Session.Import([new ImageAsset($"截图_{DateTime.Now:HHmmss}.png",null,ImageService.FromSource(image))],false,true);_view="overlay";
                }
            }catch(Exception ex){ShowToast($"截图失败：{ex.Message}");}
            finally{Application.Current.ShutdownMode=previousShutdown;if(!_closed){Show();Activate();Render();}}
        }
        if(group.HasShot)Ask("替换当前截图？","确认截取后替换当前截图并清除旧对齐和结果，取消截屏则保留原图。","继续截屏",Capture);else Capture();
    }
    private void ShowToast(string text)
    { if (_closed) return; ToastText.Text = text; Toast.Visibility = Visibility.Visible; _toastTimer.Stop(); _toastTimer.Start(); }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_Closing(object? sender, CancelEventArgs e) { _closed = true; CancelComparison(); _toastTimer.Stop(); }
}
