namespace Fovium.RenderProbe;

internal readonly record struct OrientationAffine(
    double A,
    double B,
    double C,
    double D,
    double E,
    double F)
{
    public static OrientationAffine Create(ImageSize size, ExifOrientation orientation) =>
        orientation switch
        {
            ExifOrientation.Normal => new(1, 0, 0, 0, 1, 0),
            ExifOrientation.MirrorHorizontal => new(-1, 0, size.Width, 0, 1, 0),
            ExifOrientation.Rotate180 => new(-1, 0, size.Width, 0, -1, size.Height),
            ExifOrientation.MirrorVertical => new(1, 0, 0, 0, -1, size.Height),
            ExifOrientation.Transpose => new(0, 1, 0, 1, 0, 0),
            ExifOrientation.Rotate90 => new(0, -1, size.Height, 1, 0, 0),
            ExifOrientation.Transverse => new(0, -1, size.Height, -1, 0, size.Width),
            ExifOrientation.Rotate270 => new(0, 1, 0, -1, 0, size.Width),
            _ => throw new ArgumentOutOfRangeException(nameof(orientation)),
        };

    public PointD Transform(PointD point) => new(
        A * point.X + B * point.Y + C,
        D * point.X + E * point.Y + F);
}
