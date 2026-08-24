namespace Fovium.Rendering;

internal readonly record struct PixelSize(int Width, int Height)
{
    public bool IsValid => Width > 0 && Height > 0;
}

internal readonly record struct LogicalSize(double Width, double Height)
{
    public bool IsValid =>
        double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0;
}

internal readonly record struct PointD(double X, double Y);

internal readonly record struct RectD(double X, double Y, double Width, double Height);

internal readonly record struct NormalizedPoint(double X, double Y)
{
    public NormalizedPoint Clamp() => new(Math.Clamp(X, 0, 1), Math.Clamp(Y, 0, 1));
}
