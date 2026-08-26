namespace Fovium.Rendering;

internal readonly record struct PixelPoint(int X, int Y);

internal static class PhotoSourceSamplingGeometry
{
    public static bool TryMapViewportToOrientedPixel(
        RectD destination,
        PixelSize orientedSourceSize,
        PointD viewportPoint,
        out PixelPoint pixel)
    {
        pixel = default;
        if (!orientedSourceSize.IsValid ||
            !double.IsFinite(destination.X) ||
            !double.IsFinite(destination.Y) ||
            !double.IsFinite(destination.Width) ||
            !double.IsFinite(destination.Height) ||
            destination.Width <= 0 ||
            destination.Height <= 0 ||
            !double.IsFinite(viewportPoint.X) ||
            !double.IsFinite(viewportPoint.Y) ||
            viewportPoint.X < destination.X ||
            viewportPoint.X >= destination.X + destination.Width ||
            viewportPoint.Y < destination.Y ||
            viewportPoint.Y >= destination.Y + destination.Height)
        {
            return false;
        }

        var continuousX = (viewportPoint.X - destination.X) *
            orientedSourceSize.Width / destination.Width;
        var continuousY = (viewportPoint.Y - destination.Y) *
            orientedSourceSize.Height / destination.Height;
        pixel = new PixelPoint(
            Math.Clamp((int)Math.Floor(continuousX), 0, orientedSourceSize.Width - 1),
            Math.Clamp((int)Math.Floor(continuousY), 0, orientedSourceSize.Height - 1));
        return true;
    }
}
