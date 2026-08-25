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
    public void MatteMetricsUsePhysicalPixelsWithoutChangingPhoto(
        double renderScaling,
        double expectedDipWidth)
    {
        var image = new RectD(100, 80, 400, 300);

        var result = StageGeometry.CalculateMatte(
            image,
            new LogicalSize(800, 600),
            renderScaling,
            MatteStyle.Rounded,
            24);

        Assert.Equal(image, result.BackingDestination);
        Assert.Equal(expectedDipWidth, result.WidthDip, 9);
        Assert.Equal(expectedDipWidth, image.X - result.OuterBounds.X, 9);
        Assert.Equal(expectedDipWidth, image.Y - result.OuterBounds.Y, 9);
        Assert.Equal(expectedDipWidth * 1.5, result.OuterRadiusDip, 9);
        Assert.Equal(expectedDipWidth * 1.5, result.ChamferDip, 9);
        Assert.Equal(expectedDipWidth / 3, result.SoftSigmaDip, 9);
    }

    [Theory]
    [InlineData((int)MatteStyle.Solid)]
    [InlineData((int)MatteStyle.Rounded)]
    [InlineData((int)MatteStyle.Soft)]
    [InlineData((int)MatteStyle.Angular)]
    public void EveryStyleKeepsCompleteRectangularPhotoBacking(int styleValue)
    {
        var image = new RectD(100, 80, 400, 300);

        var result = StageGeometry.CalculateMatte(
            image,
            new LogicalSize(800, 600),
            1,
            (MatteStyle)styleValue,
            64);

        Assert.Equal(image, result.BackingDestination);
        Assert.Equal(new RectD(36, 16, 528, 428), result.OuterBounds);
        Assert.Equal(result.OuterBounds, result.VisibleBounds);
        Assert.Equal((MatteStyle)styleValue, result.Style);
    }

    [Fact]
    public void MatteClipsAtViewportEdgesWithoutMovingPhotoOrChangingIdealShape()
    {
        var image = new RectD(0, 20, 800, 560);

        var result = StageGeometry.CalculateMatte(
            image,
            new LogicalSize(800, 600),
            1,
            MatteStyle.Solid,
            24);

        Assert.Equal(image, result.BackingDestination);
        Assert.Equal(new RectD(-24, -4, 848, 608), result.OuterBounds);
        Assert.Equal(new RectD(0, 0, 800, 600), result.VisibleBounds);
        Assert.True(result.VisibleBounds.Width >= 0);
        Assert.True(result.VisibleBounds.Height >= 0);
    }

    [Fact]
    public void RoundedRadiusIsClampedToHalfOuterBounds()
    {
        var result = StageGeometry.CalculateMatte(
            new RectD(100, 100, 4, 4),
            new LogicalSize(400, 400),
            1,
            MatteStyle.Rounded,
            192);

        Assert.Equal(194, result.OuterRadiusDip);
        Assert.Equal(result.OuterRadiusDip, result.ChamferDip);
    }

    [Fact]
    public void AngularPointsAreDeterministicAndChamferEveryOuterCorner()
    {
        var bounds = new RectD(10, 20, 100, 80);

        var points = StageGeometry.CalculateAngularPoints(bounds, 12);

        Assert.Equal(
            [
                new PointD(22, 20),
                new PointD(98, 20),
                new PointD(110, 32),
                new PointD(110, 88),
                new PointD(98, 100),
                new PointD(22, 100),
                new PointD(10, 88),
                new PointD(10, 32),
            ],
            points);
    }

    [Fact]
    public void SoftFeatherExtentIsBoundedByConfiguredWidth()
    {
        var result = StageGeometry.CalculateMatte(
            new RectD(200, 150, 400, 300),
            new LogicalSize(1000, 800),
            2,
            MatteStyle.Soft,
            128);

        Assert.Equal(64, result.WidthDip);
        Assert.Equal(64d / 3, result.SoftSigmaDip, 9);
        Assert.Equal(new RectD(136, 86, 528, 428), result.OuterBounds);
        Assert.Equal(result.OuterBounds, result.VisibleBounds);
    }

    [Fact]
    public void InvalidGeometryInputsAreRejectedAtBoundary()
    {
        var viewport = new LogicalSize(800, 600);
        var image = new RectD(100, 100, 400, 300);

        Assert.Throws<ArgumentOutOfRangeException>(() => StageGeometry.CalculateMatte(
            new RectD(double.NaN, 0, 10, 10), viewport, 1, MatteStyle.Solid, 24));
        Assert.Throws<ArgumentOutOfRangeException>(() => StageGeometry.CalculateMatte(
            image, viewport, 0, MatteStyle.Solid, 24));
        Assert.Throws<ArgumentOutOfRangeException>(() => StageGeometry.CalculateMatte(
            image, viewport, 1, (MatteStyle)999, 24));
        Assert.Throws<ArgumentOutOfRangeException>(() => StageGeometry.CalculateMatte(
            image, viewport, 1, MatteStyle.Solid, 193));
        Assert.Throws<ArgumentOutOfRangeException>(() => StageGeometry.CalculateAngularPoints(
            image, double.PositiveInfinity));
    }
}
