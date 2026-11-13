using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ExifMultimediaMetadataViewer.Controls;

public sealed class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth), typeof(double), typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(150d, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight), typeof(double), typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(175d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private Size _extent;
    private Size _viewport;
    private Point _offset;

    public double ItemWidth { get => (double)GetValue(ItemWidthProperty); set => SetValue(ItemWidthProperty, value); }
    public double ItemHeight { get => (double)GetValue(ItemHeightProperty); set => SetValue(ItemHeightProperty, value); }
    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; } = true;
    public double ExtentWidth => _extent.Width;
    public double ExtentHeight => _extent.Height;
    public double ViewportWidth => _viewport.Width;
    public double ViewportHeight => _viewport.Height;
    public double HorizontalOffset => _offset.X;
    public double VerticalOffset => _offset.Y;
    public ScrollViewer? ScrollOwner { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        var count = owner?.Items.Count ?? 0;
        var width = double.IsInfinity(availableSize.Width) ? Math.Max(ItemWidth, 1) : Math.Max(availableSize.Width, 1);
        var height = double.IsInfinity(availableSize.Height) ? Math.Max(ItemHeight, 1) : Math.Max(availableSize.Height, 1);
        var itemWidth = Math.Max(ItemWidth, 1);
        var itemHeight = Math.Max(ItemHeight, 1);
        var columns = Math.Max(1, (int)Math.Floor(width / itemWidth));
        var rows = (int)Math.Ceiling(count / (double)columns);
        UpdateScrollInfo(new Size(width, rows * itemHeight), new Size(width, height));

        var firstRow = Math.Max(0, (int)Math.Floor(VerticalOffset / itemHeight) - 1);
        var visibleRows = Math.Max(1, (int)Math.Ceiling(height / itemHeight) + 2);
        var firstIndex = Math.Min(count, firstRow * columns);
        var lastIndex = Math.Min(count - 1, (firstRow + visibleRows) * columns - 1);
        RealizeRange(firstIndex, lastIndex, new Size(itemWidth, itemHeight));
        CleanupRange(firstIndex, lastIndex);
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Math.Max(1, (int)Math.Floor(Math.Max(finalSize.Width, 1) / Math.Max(ItemWidth, 1)));
        foreach (UIElement child in InternalChildren)
        {
            var index = ItemsControl.GetItemsOwner(this)?.ItemContainerGenerator.IndexFromContainer(child) ?? -1;
            if (index < 0)
                continue;
            var row = index / columns;
            var column = index % columns;
            child.Arrange(new Rect(column * ItemWidth, row * ItemHeight - VerticalOffset, ItemWidth, ItemHeight));
        }
        return finalSize;
    }

    private void RealizeRange(int firstIndex, int lastIndex, Size childSize)
    {
        if (firstIndex > lastIndex)
            return;
        var generator = ItemContainerGenerator;
        var startPosition = generator.GeneratorPositionFromIndex(firstIndex);
        var childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;
        using (generator.StartAt(startPosition, GeneratorDirection.Forward, true))
        {
            for (var itemIndex = firstIndex; itemIndex <= lastIndex; itemIndex++, childIndex++)
            {
                var child = (UIElement)generator.GenerateNext(out var newlyRealized)!;
                if (newlyRealized)
                {
                    if (childIndex >= InternalChildren.Count)
                        AddInternalChild(child);
                    else
                        InsertInternalChild(childIndex, child);
                    generator.PrepareItemContainer(child);
                }
                child.Measure(childSize);
            }
        }
    }

    private void CleanupRange(int firstIndex, int lastIndex)
    {
        var generator = ItemContainerGenerator;
        var ownerGenerator = ItemsControl.GetItemsOwner(this)?.ItemContainerGenerator;
        for (var childIndex = InternalChildren.Count - 1; childIndex >= 0; childIndex--)
        {
            var child = InternalChildren[childIndex];
            var itemIndex = ownerGenerator?.IndexFromContainer(child) ?? -1;
            if (itemIndex >= firstIndex && itemIndex <= lastIndex)
                continue;
            var position = new GeneratorPosition(childIndex, 0);
            generator.Remove(position, 1);
            RemoveInternalChildRange(childIndex, 1);
        }
    }

    private void UpdateScrollInfo(Size extent, Size viewport)
    {
        if (_extent == extent && _viewport == viewport)
            return;
        _extent = extent;
        _viewport = viewport;
        _offset.Y = Math.Clamp(_offset.Y, 0, Math.Max(0, ExtentHeight - ViewportHeight));
        ScrollOwner?.InvalidateScrollInfo();
    }

    public void LineUp() => SetVerticalOffset(VerticalOffset - 32);
    public void LineDown() => SetVerticalOffset(VerticalOffset + 32);
    public void LineLeft() { }
    public void LineRight() { }
    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - 96);
    public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + 96);
    public void MouseWheelLeft() { }
    public void MouseWheelRight() { }
    public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);
    public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);
    public void PageLeft() { }
    public void PageRight() { }
    public void SetHorizontalOffset(double offset) { }

    public void SetVerticalOffset(double offset)
    {
        var value = Math.Clamp(offset, 0, Math.Max(0, ExtentHeight - ViewportHeight));
        if (Math.Abs(value - _offset.Y) < 0.1)
            return;
        _offset.Y = value;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        if (visual is UIElement element)
        {
            var index = ItemsControl.GetItemsOwner(this)?.ItemContainerGenerator.IndexFromContainer(element) ?? -1;
            if (index >= 0)
            {
                var columns = Math.Max(1, (int)Math.Floor(Math.Max(ViewportWidth, 1) / Math.Max(ItemWidth, 1)));
                var top = index / columns * ItemHeight;
                if (top < VerticalOffset)
                    SetVerticalOffset(top);
                else if (top + ItemHeight > VerticalOffset + ViewportHeight)
                    SetVerticalOffset(top + ItemHeight - ViewportHeight);
            }
        }
        return rectangle;
    }
}
