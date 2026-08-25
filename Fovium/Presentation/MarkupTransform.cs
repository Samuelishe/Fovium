using Fovium.Rendering;

namespace Fovium.Presentation;

internal readonly record struct MarkupTransform(RectD Destination, PixelSize OrientedSourceSize)
{
    public PointD SourceToViewport(PointD sourcePoint) => new(
        Destination.X + sourcePoint.X * ScaleX,
        Destination.Y + sourcePoint.Y * ScaleY);

    public double SourceStrokeToViewport(double sourceStroke) =>
        sourceStroke * (Math.Abs(ScaleX) + Math.Abs(ScaleY)) / 2;

    private double ScaleX => Destination.Width / OrientedSourceSize.Width;

    private double ScaleY => Destination.Height / OrientedSourceSize.Height;
}
