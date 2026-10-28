namespace ExifMetadata.Core.Models;

public readonly record struct ViewportPoint(double X, double Y);

public readonly record struct ViewportSize(double Width, double Height);

public readonly record struct ViewportTransform(
    double M11,
    double M12,
    double M21,
    double M22,
    double OffsetX,
    double OffsetY)
{
    public ViewportPoint Transform(ViewportPoint point) => new(
        point.X * M11 + point.Y * M21 + OffsetX,
        point.X * M12 + point.Y * M22 + OffsetY);
}

public static class ViewportGeometry
{
    public static ViewportSize RotatedSize(double width, double height, double rotationDegrees)
    {
        var (cosine, sine) = Rotation(rotationDegrees);
        return new(
            CleanDimension(Math.Abs(cosine) * width + Math.Abs(sine) * height),
            CleanDimension(Math.Abs(sine) * width + Math.Abs(cosine) * height));
    }

    public static double FitScale(
        double imageWidth,
        double imageHeight,
        double viewportWidth,
        double viewportHeight,
        double rotationDegrees)
    {
        if (!ValidSize(imageWidth, imageHeight) || !ValidSize(viewportWidth, viewportHeight))
            return 1;

        var rotated = RotatedSize(imageWidth, imageHeight, rotationDegrees);
        return Math.Min(viewportWidth / rotated.Width, viewportHeight / rotated.Height);
    }

    public static ViewportTransform ImageToViewport(
        double imageWidth,
        double imageHeight,
        double viewportWidth,
        double viewportHeight,
        double scale,
        double rotationDegrees,
        double panX,
        double panY)
    {
        var (cosine, sine) = Rotation(rotationDegrees);
        var m11 = cosine * scale;
        var m12 = sine * scale;
        var m21 = -sine * scale;
        var m22 = cosine * scale;
        return new(
            m11,
            m12,
            m21,
            m22,
            viewportWidth / 2 + panX - (imageWidth / 2 * m11 + imageHeight / 2 * m21),
            viewportHeight / 2 + panY - (imageWidth / 2 * m12 + imageHeight / 2 * m22));
    }

    public static ViewportPoint ViewportToImage(
        ViewportPoint point,
        double imageWidth,
        double imageHeight,
        double viewportWidth,
        double viewportHeight,
        double scale,
        double rotationDegrees,
        double panX,
        double panY)
    {
        if (scale <= 0 || !double.IsFinite(scale))
            return new(imageWidth / 2, imageHeight / 2);

        var (cosine, sine) = Rotation(rotationDegrees);
        var x = (point.X - viewportWidth / 2 - panX) / scale;
        var y = (point.Y - viewportHeight / 2 - panY) / scale;
        return new(
            imageWidth / 2 + cosine * x + sine * y,
            imageHeight / 2 - sine * x + cosine * y);
    }

    public static ViewportPoint ZoomPanAt(
        ViewportPoint cursor,
        double imageWidth,
        double imageHeight,
        double viewportWidth,
        double viewportHeight,
        double oldScale,
        double newScale,
        double rotationDegrees,
        double panX,
        double panY)
    {
        var imagePoint = ViewportToImage(
            cursor, imageWidth, imageHeight, viewportWidth, viewportHeight,
            oldScale, rotationDegrees, panX, panY);
        var (cosine, sine) = Rotation(rotationDegrees);
        var x = imagePoint.X - imageWidth / 2;
        var y = imagePoint.Y - imageHeight / 2;
        return new(
            cursor.X - viewportWidth / 2 - newScale * (cosine * x - sine * y),
            cursor.Y - viewportHeight / 2 - newScale * (sine * x + cosine * y));
    }

    public static ViewportPoint ClampPan(
        double panX,
        double panY,
        double imageWidth,
        double imageHeight,
        double viewportWidth,
        double viewportHeight,
        double scale,
        double rotationDegrees)
    {
        if (!ValidSize(imageWidth, imageHeight) || !ValidSize(viewportWidth, viewportHeight))
            return new(0, 0);

        var rotated = RotatedSize(imageWidth, imageHeight, rotationDegrees);
        var maxX = Math.Max(0, (rotated.Width * scale - viewportWidth) / 2);
        var maxY = Math.Max(0, (rotated.Height * scale - viewportHeight) / 2);
        return new(Math.Clamp(panX, -maxX, maxX), Math.Clamp(panY, -maxY, maxY));
    }

    private static (double Cosine, double Sine) Rotation(double degrees)
    {
        var radians = degrees * Math.PI / 180;
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return (Math.Abs(cosine) < 1e-12 ? 0 : cosine, Math.Abs(sine) < 1e-12 ? 0 : sine);
    }

    private static bool ValidSize(double width, double height) =>
        width > 0 && height > 0 && double.IsFinite(width) && double.IsFinite(height);

    private static double CleanDimension(double value) =>
        Math.Abs(value - Math.Round(value)) < 1e-9 ? Math.Round(value) : value;
}
