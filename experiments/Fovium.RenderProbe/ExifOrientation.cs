namespace Fovium.RenderProbe;

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
    public static ImageSize GetOrientedSize(ImageSize encodedSize, ExifOrientation orientation) =>
        orientation is ExifOrientation.Transpose or ExifOrientation.Rotate90 or
            ExifOrientation.Transverse or ExifOrientation.Rotate270
            ? new ImageSize(encodedSize.Height, encodedSize.Width)
            : encodedSize;

    // Coordinates describe pixel edges in [0, width] x [0, height].
    public static PointD ToOriented(PointD encoded, ImageSize size, ExifOrientation orientation) =>
        orientation switch
        {
            ExifOrientation.Normal => encoded,
            ExifOrientation.MirrorHorizontal => new PointD(size.Width - encoded.X, encoded.Y),
            ExifOrientation.Rotate180 => new PointD(size.Width - encoded.X, size.Height - encoded.Y),
            ExifOrientation.MirrorVertical => new PointD(encoded.X, size.Height - encoded.Y),
            ExifOrientation.Transpose => new PointD(encoded.Y, encoded.X),
            ExifOrientation.Rotate90 => new PointD(size.Height - encoded.Y, encoded.X),
            ExifOrientation.Transverse => new PointD(size.Height - encoded.Y, size.Width - encoded.X),
            ExifOrientation.Rotate270 => new PointD(encoded.Y, size.Width - encoded.X),
            _ => throw new ArgumentOutOfRangeException(nameof(orientation)),
        };
}
