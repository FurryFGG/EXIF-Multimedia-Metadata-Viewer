using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExifMetadata.Core.Models;

namespace ExifMultimediaMetadataViewer.Controls;

public sealed class ImageViewport : FrameworkElement
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(BitmapSource), typeof(ImageViewport),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSourceChanged));

    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom), typeof(double), typeof(ImageViewport),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender, OnZoomChanged, CoerceZoom));

    public static readonly DependencyProperty RotationProperty = DependencyProperty.Register(
        nameof(Rotation), typeof(double), typeof(ImageViewport),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, OnRotationChanged));

    private Point _lastPointer;
    private double _panX;
    private double _panY;
    private bool _dragging;
    private bool _isFit = true;
    private bool _updatingZoom;

    public ImageViewport()
    {
        Focusable = true;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        Cursor = Cursors.Hand;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Fant);
    }

    public BitmapSource? Source
    {
        get => (BitmapSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double Rotation
    {
        get => (double)GetValue(RotationProperty);
        set => SetValue(RotationProperty, value);
    }

    public void FitToViewport()
    {
        _isFit = true;
        _panX = 0;
        _panY = 0;
        if (Source is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            InvalidateVisual();
            return;
        }

        SetZoom(ViewportGeometry.FitScale(
            Source.PixelWidth, Source.PixelHeight, ActualWidth, ActualHeight, Rotation));
        InvalidateVisual();
    }

    public void RotateClockwise()
    {
        Rotation = (Rotation + 90) % 360;
        FitToViewport();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (Source is null || Source.PixelWidth <= 0 || Source.PixelHeight <= 0)
            return;

        ClampPan();
        var transform = ViewportGeometry.ImageToViewport(
            Source.PixelWidth, Source.PixelHeight, ActualWidth, ActualHeight,
            Zoom, Rotation, _panX, _panY);
        var matrix = new Matrix(
            transform.M11, transform.M12, transform.M21, transform.M22,
            transform.OffsetX, transform.OffsetY);
        drawingContext.PushTransform(new MatrixTransform(matrix));
        drawingContext.DrawImage(Source, new Rect(0, 0, Source.PixelWidth, Source.PixelHeight));
        drawingContext.Pop();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton != MouseButton.Left || Source is null)
            return;

        Focus();
        _lastPointer = e.GetPosition(this);
        _dragging = true;
        Cursor = Cursors.SizeAll;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging || e.LeftButton != MouseButtonState.Pressed)
            return;

        var current = e.GetPosition(this);
        _panX += current.X - _lastPointer.X;
        _panY += current.Y - _lastPointer.Y;
        _lastPointer = current;
        ClampPan();
        InvalidateVisual();
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton != MouseButton.Left || !_dragging)
            return;

        _dragging = false;
        Cursor = Cursors.Hand;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (Source is null)
            return;

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            PanHorizontally(-e.Delta * 0.55);
        else
            ZoomAt(e.GetPosition(this), Math.Pow(1.15, e.Delta / 120d));
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _dragging = false;
        Cursor = Cursors.Hand;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (_isFit)
            Dispatcher.BeginInvoke(FitToViewport, DispatcherPriority.Loaded);
        else
        {
            ClampPan();
            InvalidateVisual();
        }
    }

    public void PanHorizontally(double amount)
    {
        if (Source is null)
            return;
        _panX += amount;
        ClampPan();
        InvalidateVisual();
    }

    private void ZoomAt(Point cursor, double factor)
    {
        if (Source is null || factor <= 0)
            return;

        var nextZoom = Math.Clamp(Zoom * factor, 0.1, 8.0);
        if (Math.Abs(nextZoom - Zoom) < 1e-9)
            return;

        var pan = ViewportGeometry.ZoomPanAt(
            new ViewportPoint(cursor.X, cursor.Y), Source.PixelWidth, Source.PixelHeight,
            ActualWidth, ActualHeight, Zoom, nextZoom, Rotation, _panX, _panY);
        _isFit = false;
        _panX = pan.X;
        _panY = pan.Y;
        SetZoom(nextZoom);
        ClampPan();
    }

    private void ClampPan()
    {
        if (Source is null)
            return;
        var pan = ViewportGeometry.ClampPan(
            _panX, _panY, Source.PixelWidth, Source.PixelHeight,
            ActualWidth, ActualHeight, Zoom, Rotation);
        _panX = pan.X;
        _panY = pan.Y;
    }

    private void SetZoom(double value)
    {
        _updatingZoom = true;
        try { SetCurrentValue(ZoomProperty, value); }
        finally { _updatingZoom = false; }
    }

    private static object CoerceZoom(DependencyObject dependencyObject, object baseValue) =>
        Math.Clamp(double.IsFinite((double)baseValue) ? (double)baseValue : 1, 0.005, 8.0);

    private static void OnSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var viewport = (ImageViewport)dependencyObject;
        viewport._panX = 0;
        viewport._panY = 0;
        viewport._isFit = true;
        viewport.Dispatcher.BeginInvoke(viewport.FitToViewport, DispatcherPriority.Loaded);
    }

    private static void OnZoomChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var viewport = (ImageViewport)dependencyObject;
        if (!viewport._updatingZoom)
            viewport._isFit = false;
        viewport.ClampPan();
    }

    private static void OnRotationChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var viewport = (ImageViewport)dependencyObject;
        if (viewport._isFit)
            viewport.Dispatcher.BeginInvoke(viewport.FitToViewport, DispatcherPriority.Loaded);
        else
        {
            viewport.ClampPan();
            viewport.InvalidateVisual();
        }
    }
}
