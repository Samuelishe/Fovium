using Fovium.Rendering;

namespace Fovium.Imaging;

internal enum ExifOrientation
{
    Normal = 1,
    MirrorHorizontal = 2,
    Rotate180 = 3,
    MirrorVertical = 4,
    Transpose = 5,
    Rotate90 = 6,
    Transverse = 7,
    Rotate270 = 8,
}

internal static class OrientationTransform
{
    public static PixelSize GetOrientedSize(PixelSize encodedSize, ExifOrientation orientation) =>
        orientation is ExifOrientation.Transpose or ExifOrientation.Rotate90 or
            ExifOrientation.Transverse or ExifOrientation.Rotate270
            ? new PixelSize(encodedSize.Height, encodedSize.Width)
            : encodedSize;
}

internal readonly record struct OrientationAffine(
    double A,
    double B,
    double C,
    double D,
    double E,
    double F)
{
    public static OrientationAffine Create(PixelSize size, ExifOrientation orientation) =>
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
}
