using Fovium.RenderProbe;

namespace Fovium.Tests.RenderProbe;

public sealed class OrientationTransformTests
{
    [Theory]
    [InlineData(1, 20, 10)]
    [InlineData(2, 80, 10)]
    [InlineData(3, 80, 50)]
    [InlineData(4, 20, 50)]
    [InlineData(5, 10, 20)]
    [InlineData(6, 50, 20)]
    [InlineData(7, 50, 80)]
    [InlineData(8, 10, 80)]
    public void MapsAllEightExifOrientations(
        int orientationValue,
        double expectedX,
        double expectedY)
    {
        var orientation = (ExifOrientation)orientationValue;
        var size = new ImageSize(100, 60);
        var point = new PointD(20, 10);

        var transformed = OrientationTransform.ToOriented(point, size, orientation);
        var affineTransformed = OrientationAffine.Create(size, orientation).Transform(point);

        Assert.Equal(expectedX, transformed.X, 12);
        Assert.Equal(expectedY, transformed.Y, 12);
        Assert.Equal(transformed, affineTransformed);
    }

    [Theory]
    [InlineData(1, 100, 60)]
    [InlineData(2, 100, 60)]
    [InlineData(3, 100, 60)]
    [InlineData(4, 100, 60)]
    [InlineData(5, 60, 100)]
    [InlineData(6, 60, 100)]
    [InlineData(7, 60, 100)]
    [InlineData(8, 60, 100)]
    public void ReportsOrientedDimensions(
        int orientationValue,
        int expectedWidth,
        int expectedHeight)
    {
        var orientation = (ExifOrientation)orientationValue;
        var oriented = OrientationTransform.GetOrientedSize(new ImageSize(100, 60), orientation);

        Assert.Equal(new ImageSize(expectedWidth, expectedHeight), oriented);
    }
}
