using Fovium.Imaging;
using Fovium.Rendering;

namespace Fovium.Tests.Imaging;

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
    public void AllEightExifOrientationsMapEncodedEdgesIntoOrientedCoordinates(
        int value,
        double expectedX,
        double expectedY)
    {
        var affine = OrientationAffine.Create(new PixelSize(100, 60), (ExifOrientation)value);
        var x = affine.A * 20 + affine.B * 10 + affine.C;
        var y = affine.D * 20 + affine.E * 10 + affine.F;

        Assert.Equal(expectedX, x, 12);
        Assert.Equal(expectedY, y, 12);
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
    public void AllEightExifOrientationsProduceCorrectVisibleDimensions(
        int value,
        int expectedWidth,
        int expectedHeight)
    {
        var oriented = OrientationTransform.GetOrientedSize(
            new PixelSize(100, 60),
            (ExifOrientation)value);

        Assert.Equal(new PixelSize(expectedWidth, expectedHeight), oriented);
    }

    [Theory]
    [InlineData(1, 0, 0, 0, 0)]
    [InlineData(2, 0, 0, 2, 0)]
    [InlineData(3, 0, 0, 2, 1)]
    [InlineData(4, 0, 0, 0, 1)]
    [InlineData(5, 0, 0, 0, 0)]
    [InlineData(6, 0, 0, 0, 1)]
    [InlineData(7, 0, 0, 2, 1)]
    [InlineData(8, 0, 0, 2, 0)]
    public void OrientedTopLeftMapsToExpectedEncodedPixelExactlyOnce(
        int value,
        int orientedX,
        int orientedY,
        int encodedX,
        int encodedY)
    {
        var actual = OrientationTransform.OrientedToEncodedPixel(
            new PixelSize(3, 2),
            (ExifOrientation)value,
            new PixelPoint(orientedX, orientedY));

        Assert.Equal(new PixelPoint(encodedX, encodedY), actual);
    }
}
