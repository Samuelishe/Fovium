using Fovium.Rendering;

namespace Fovium.Tests.Rendering;

public sealed class PhotoSourceSamplingGeometryTests
{
    [Fact]
    public void ContainingPixelRuleMapsInteriorPhotoCoordinates()
    {
        var destination = new RectD(100, 50, 400, 200);
        var source = new PixelSize(40, 20);

        var mapped = PhotoSourceSamplingGeometry.TryMapViewportToOrientedPixel(
            destination,
            source,
            new PointD(235.9, 125.1),
            out var pixel);

        Assert.True(mapped);
        Assert.Equal(new PixelPoint(13, 7), pixel);
    }

    [Fact]
    public void HalfOpenEdgesMapOnlyFirstAndLastValidPixels()
    {
        var destination = new RectD(10, 20, 100, 50);
        var source = new PixelSize(10, 5);

        Assert.True(PhotoSourceSamplingGeometry.TryMapViewportToOrientedPixel(
            destination, source, new PointD(10, 20), out var first));
        Assert.True(PhotoSourceSamplingGeometry.TryMapViewportToOrientedPixel(
            destination, source, new PointD(109.999, 69.999), out var last));
        Assert.False(PhotoSourceSamplingGeometry.TryMapViewportToOrientedPixel(
            destination, source, new PointD(110, 69), out _));
        Assert.False(PhotoSourceSamplingGeometry.TryMapViewportToOrientedPixel(
            destination, source, new PointD(109, 70), out _));
        Assert.Equal(new PixelPoint(0, 0), first);
        Assert.Equal(new PixelPoint(9, 4), last);
    }

    [Theory]
    [InlineData(1.00)]
    [InlineData(1.25)]
    [InlineData(1.50)]
    [InlineData(2.00)]
    public void MappingIsStableForRenderedDestinationAtFractionalScaling(double scaling)
    {
        var destination = new RectD(12.5 / scaling, 7.5 / scaling, 800 / scaling, 600 / scaling);
        var source = new PixelSize(800, 600);
        var point = new PointD(destination.X + (321.25 / scaling), destination.Y + (222.75 / scaling));

        var mapped = PhotoSourceSamplingGeometry.TryMapViewportToOrientedPixel(
            destination,
            source,
            point,
            out var pixel);

        Assert.True(mapped);
        Assert.Equal(new PixelPoint(321, 222), pixel);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.PositiveInfinity)]
    [InlineData(-1, 0)]
    public void InvalidOrOutsideCoordinatesDoNotSample(double x, double y)
    {
        Assert.False(PhotoSourceSamplingGeometry.TryMapViewportToOrientedPixel(
            new RectD(0, 0, 100, 100),
            new PixelSize(10, 10),
            new PointD(x, y),
            out _));
    }
}
