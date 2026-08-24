namespace Fovium.RenderProbe;

internal readonly record struct ImageSize(int Width, int Height)
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
