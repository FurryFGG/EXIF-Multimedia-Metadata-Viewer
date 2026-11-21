using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Foundation;
using ExifMetadata.Core.Services;

namespace MetaLens.WinUI;
public sealed partial class PreviewView : UserControl
{
    internal event Action<int>? Step;
    internal event Action? Full;
    internal event Action? Changed;
    internal string Kind = "", Current = "";
    internal BitmapImage? Image;
    internal double Zoom = 1;
    internal int Turn;
    internal bool Fit = true;
    internal string? Error;
    internal MediaPlayer? Player;
    MediaSource? source;
    CancellationTokenSource? active;
    Point? drag;
    double offsetX, offsetY;
    public PreviewView()
    {
        InitializeComponent();
        void Button(string text, Action action)
        {
            var b = new Button
            {
                Content = text,
                Padding = new Thickness(7, 3, 7, 3)
            };
            b.Click += (_, _) => action();
            Tools.Children.Add(b);
        }

        Button("上一张", () => Step?.Invoke(-1));
        Button("下一张", () => Step?.Invoke(1));
        Button("适应", FitImage);
        foreach (var n in new[]
        {
            50,
            100,
            200,
            400
        }

        )
            Button(n + "%", () => SetZoom(n / 100d));
        Button("旋转 90°", Rotate);
        Button("全屏", () => Full?.Invoke());
        Button("视频 / 首帧", ToggleVideo);
        Scroll.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(Wheel), true);
        Surface.PointerPressed += Down;
        Surface.PointerMoved += Move;
        Surface.PointerReleased += Up;
        Surface.PointerCaptureLost += (_, _) => drag = null;
    }

    internal double RasterWidth => Photo.Width * (XamlRoot?.RasterizationScale ?? 1);

    internal void Cancel() => active?.Cancel();
    internal void Clear()
    {
        Cancel();
        ReleaseVideo();
        Image = null;
        Photo.Source = null;
        Kind = "";
        Current = "";
    }

    void ReleaseVideo()
    {
        Video.Visibility = Visibility.Collapsed;
        Scroll.Visibility = Visibility.Visible;
        if (Player != null)
        {
            Player.MediaFailed -= VideoFailed;
            Player.Pause();
            Player.Source = null;
            Video.SetMediaPlayer(null);
            Player.Dispose();
            Player = null;
        }

        source?.Dispose();
        source = null;
    }

    internal async Task Show(string file, CancellationToken stop)
    {
        Clear();
        using var job = CancellationTokenSource.CreateLinkedTokenSource(stop);
        active = job;
        Current = file;
        Fit = true;
        Turn = 0;
        Error = null;
        Caption.Text = "读取预览…";
        try
        {
            if (MediaAnalyzer.IsVideoFile(file))
            {
                try
                {
                    source = MediaSource.CreateFromUri(new Uri(file));
                    Player = new MediaPlayer
                    {
                        AutoPlay = false,
                        Source = source
                    };
                    Player.MediaFailed += VideoFailed;
                    Video.SetMediaPlayer(Player);
                }
                catch (Exception ex)
                {
                    ReleaseVideo();
                    Error = ex.Message;
                }
            }

            var r = await MediaImages.ReadPreview(file, job.Token);
            job.Token.ThrowIfCancellationRequested();
            Image = r.Image;
            Kind = r.Kind;
            Photo.Source = Image;
            Arrange();
            Changed?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!job.IsCancellationRequested)
            {
                Error = ex.Message;
                Caption.Text = "预览不可用：" + ex.Message;
                Changed?.Invoke();
            }
        }
        finally
        {
            if (active == job)
                active = null;
        }
    }

    void VideoFailed(MediaPlayer sender, MediaPlayerFailedEventArgs e)
    {
        var detail = e.ErrorMessage;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (sender != Player)
                return;
            Video.Visibility = Visibility.Collapsed;
            Scroll.Visibility = Visibility.Visible;
            Error = detail;
            Caption.Text = "系统无法播放此编码，可查看 FFmpeg 首帧 · " + detail;
        });
    }

    internal void ToggleVideo()
    {
        if (Player == null)
            return;
        bool play = Video.Visibility != Visibility.Visible;
        Video.Visibility = play ? Visibility.Visible : Visibility.Collapsed;
        Scroll.Visibility = play ? Visibility.Collapsed : Visibility.Visible;
        if (play)
            Player.Play();
        else
            Player.Pause();
    }

    internal void FitImage()
    {
        Fit = true;
        Arrange();
    }

    internal void Rotate()
    {
        Turn = (Turn + 1) % 4;
        Arrange();
    }

    void StageSize(object s, SizeChangedEventArgs e)
    {
        if (Image != null)
            Arrange();
    }

    void Arrange()
    {
        if (Image == null || Stage.ActualWidth < 1 || Stage.ActualHeight < 1)
            return;
        double r = XamlRoot?.RasterizationScale ?? 1, w = Image.PixelWidth / r, h = Image.PixelHeight / r;
        var rotated = Turn % 2 != 0;
        var boxW = rotated ? h : w;
        var boxH = rotated ? w : h;
        if (Fit)
            Zoom = Math.Min(Stage.ActualWidth / boxW, Stage.ActualHeight / boxH);
        double dw = w * Zoom, dh = h * Zoom;
        Photo.Width = dw;
        Photo.Height = dh;
        Surface.Width = boxW * Zoom;
        Surface.Height = boxH * Zoom;
        Canvas.SetLeft(Photo, (Surface.Width - dw) / 2);
        Canvas.SetTop(Photo, (Surface.Height - dh) / 2);
        Photo.RenderTransformOrigin = new Point(.5, .5);
        Photo.RenderTransform = new RotateTransform
        {
            Angle = Turn * 90
        };
        Caption.Text = $"{Kind} · {Image.PixelWidth} × {Image.PixelHeight} · {Zoom:P0} · {Turn * 90}°";
    }

    internal void SetZoom(double value, Point? center = null)
    {
        if (Image == null)
            return;
        var c = center ?? new Point(Scroll.ViewportWidth / 2, Scroll.ViewportHeight / 2);
        double old = Zoom, ox = Scroll.HorizontalOffset, oy = Scroll.VerticalOffset, marginX = Math.Max(0, (Scroll.ViewportWidth - Surface.Width) / 2), marginY = Math.Max(0, (Scroll.ViewportHeight - Surface.Height) / 2);
        Fit = false;
        Zoom = Math.Clamp(value, .02, 32);
        Arrange();
        Scroll.UpdateLayout();
        Scroll.ChangeView(Math.Max(0, (ox + c.X - marginX) * Zoom / old - c.X), Math.Max(0, (oy + c.Y - marginY) * Zoom / old - c.Y), null, true);
    }

    void Wheel(object s, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Scroll);
        e.Handled = true;
        if (point.Properties.IsHorizontalMouseWheel)
        {
            Scroll.ChangeView(Scroll.HorizontalOffset + point.Properties.MouseWheelDelta, null, null, true);
            return;
        }

        SetZoom(Zoom * Math.Pow(1.2, point.Properties.MouseWheelDelta / 120d), point.Position);
    }

    void Down(object s, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(Scroll);
        if (!p.Properties.IsLeftButtonPressed)
            return;
        drag = p.Position;
        offsetX = Scroll.HorizontalOffset;
        offsetY = Scroll.VerticalOffset;
        Surface.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    void Move(object s, PointerRoutedEventArgs e)
    {
        if (drag is not { } d)
            return;
        var p = e.GetCurrentPoint(Scroll).Position;
        Scroll.ChangeView(offsetX + d.X - p.X, offsetY + d.Y - p.Y, null, true);
        e.Handled = true;
    }

    void Up(object s, PointerRoutedEventArgs e)
    {
        drag = null;
        Surface.ReleasePointerCapture(e.Pointer);
    }
}
