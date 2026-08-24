using Fovium.Rendering;
using Fovium.Stage;

namespace Fovium.Tests.Stage;

public sealed class StageGeometryTests
{
    [Theory]
    [InlineData(1600, 900, 600, 900, -500, 0, 1600, 900)]
    [InlineData(900, 1600, 900, 600, 0, -500, 900, 1600)]
    [InlineData(1200, 800, 900, 600, 0, 0, 900, 600)]
    public void CoverFillsViewportWithoutStretchAndCentersCrop(
        int sourceWidth,
        int sourceHeight,
        double viewportWidth,
        double viewportHeight,
        double expectedX,
        double expectedY,
        double expectedWidth,
        double expectedHeight)
    {
        var result = StageGeometry.CalculateCover(
            new PixelSize(sourceWidth, sourceHeight),
            new LogicalSize(viewportWidth, viewportHeight));

        Assert.Equal(expectedX, result.X, 9);
        Assert.Equal(expectedY, result.Y, 9);
        Assert.Equal(expectedWidth, result.Width, 9);
        Assert.Equal(expectedHeight, result.Height, 9);
        Assert.Equal((double)sourceWidth / sourceHeight, result.Width / result.Height, 9);
        Assert.True(result.Width >= viewportWidth);
        Assert.True(result.Height >= viewportHeight);
    }

    [Theory]
    [InlineData(1.00, 24.0)]
    [InlineData(1.25, 19.2)]
    [InlineData(1.50, 16.0)]
    [InlineData(2.00, 12.0)]
    public void MatteWidthUsesPhysicalPixelsWithoutChangingImageRect(
        double renderScaling,
        double expectedDipWidth)
    {
        var image = new RectD(100, 80, 400, 300);

        var result = StageGeometry.CalculateMatte(
            image,
            new LogicalSize(800, 600),
            renderScaling);

        Assert.Equal(image, result.ImageDestination);
        var matte = Assert.IsType<RectD>(result.MatteDestination);
        Assert.Equal(expectedDipWidth, image.X - matte.X, 9);
        Assert.Equal(expectedDipWidth, image.Y - matte.Y, 9);
        Assert.Equal(expectedDipWidth * 2 + image.Width, matte.Width, 9);
        Assert.Equal(expectedDipWidth * 2 + image.Height, matte.Height, 9);
    }

    [Fact]
    public void MatteClipsAtViewportEdgesWithoutMovingPhoto()
    {
        var image = new RectD(0, 20, 800, 560);

        var result = StageGeometry.CalculateMatte(image, new LogicalSize(800, 600), 1);

        Assert.Equal(image, result.ImageDestination);
        Assert.Equal(new RectD(0, 0, 800, 600), result.MatteDestination);
    }
}
