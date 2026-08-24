using Fovium.RenderProbe;

namespace Fovium.Tests.RenderProbe;

public sealed class ViewportModelTests
{
    [Theory]
    [InlineData(1.00, 1.00)]
    [InlineData(1.25, 0.80)]
    [InlineData(1.50, 2.0 / 3.0)]
    [InlineData(2.00, 0.50)]
    public void Photographic100MapsOneSourcePixelToOnePhysicalPixel(
        double renderScaling,
        double expectedDipScale)
    {
        var viewport = CreateViewport(new ImageSize(4000, 3000), new LogicalSize(800, 600), renderScaling);

        viewport.SetPhotographic100();

        Assert.Equal(1, viewport.PhysicalScale, 12);
        Assert.Equal(expectedDipScale, viewport.DipScale, 12);
        Assert.Equal(4000, viewport.DestinationDip.Width * renderScaling, 9);
        Assert.Equal(3000, viewport.DestinationDip.Height * renderScaling, 9);
    }

    [Theory]
    [InlineData(1600, 900, 800, 600, 0.5)]
    [InlineData(900, 1600, 800, 600, 0.375)]
    [InlineData(1000, 1000, 800, 600, 0.6)]
    [InlineData(300, 200, 800, 600, 1.0)]
    public void FitShowsEntireImageWithoutCropOrUpscale(
        int sourceWidth,
        int sourceHeight,
        double viewportWidth,
        double viewportHeight,
        double expectedPhysicalScale)
    {
        var viewport = CreateViewport(
            new ImageSize(sourceWidth, sourceHeight),
            new LogicalSize(viewportWidth, viewportHeight),
            1);

        viewport.Fit();

        Assert.Equal(expectedPhysicalScale, viewport.PhysicalScale, 12);
        Assert.True(viewport.DestinationDip.Width <= viewportWidth + 1e-9);
        Assert.True(viewport.DestinationDip.Height <= viewportHeight + 1e-9);
        Assert.Equal((double)sourceWidth / sourceHeight,
            viewport.DestinationDip.Width / viewport.DestinationDip.Height,
            12);
    }

    [Fact]
    public void CursorAnchoredZoomKeepsSourcePointInvariant()
    {
        var viewport = CreateViewport(new ImageSize(4000, 3000), new LogicalSize(800, 600), 1.5);
        viewport.SetPhotographic100();
        var cursor = new PointD(173.25, 421.75);
        var before = viewport.SourcePointAt(cursor);

        viewport.ZoomAt(cursor, 1.75);

        var after = viewport.SourcePointAt(cursor);
        Assert.Equal(before.X, after.X, 9);
        Assert.Equal(before.Y, after.Y, 9);
    }

    [Fact]
    public void RepeatedFitResizeRecomputesWithoutAccumulatedDrift()
    {
        var viewport = CreateViewport(new ImageSize(6016, 4016), new LogicalSize(1200, 800), 1.25);
        var expected = viewport.DestinationDip;

        for (var iteration = 0; iteration < 100; iteration++)
        {
            viewport.SetViewport(new LogicalSize(777.25, 555.5), 1.5);
            viewport.SetViewport(new LogicalSize(1200, 800), 1.25);
        }

        Assert.Equal(expected.X, viewport.DestinationDip.X, 12);
        Assert.Equal(expected.Y, viewport.DestinationDip.Y, 12);
        Assert.Equal(expected.Width, viewport.DestinationDip.Width, 12);
        Assert.Equal(expected.Height, viewport.DestinationDip.Height, 12);
    }

    [Theory]
    [InlineData(1.00)]
    [InlineData(1.25)]
    [InlineData(1.50)]
    [InlineData(2.00)]
    public void LogicalAndPhysicalCoordinateConversionRoundTrips(double renderScaling)
    {
        var viewport = CreateViewport(new ImageSize(4000, 3000), new LogicalSize(900, 700), renderScaling);
        viewport.SetPhotographic100();
        var source = new PointD(1234.5, 987.25);

        var logical = viewport.ViewportPointFor(source);
        var roundTrip = viewport.SourcePointAt(logical);

        Assert.Equal(source.X, roundTrip.X, 9);
        Assert.Equal(source.Y, roundTrip.Y, 9);
    }

    [Fact]
    public void PanIsClampedToAvoidBlankSpaceForOversizedImage()
    {
        var viewport = CreateViewport(new ImageSize(2000, 1200), new LogicalSize(800, 600), 1);
        viewport.SetPhotographic100();

        viewport.PanBy(new PointD(10_000, 10_000));

        Assert.Equal(0, viewport.OriginDip.X, 12);
        Assert.Equal(0, viewport.OriginDip.Y, 12);
    }

    [Theory]
    [InlineData(1.00)]
    [InlineData(1.25)]
    [InlineData(1.50)]
    [InlineData(2.00)]
    public void PhysicalAlignmentProducesIntegralBackingPixelOrigin(double renderScaling)
    {
        var viewport = CreateViewport(new ImageSize(401, 301), new LogicalSize(800, 600), renderScaling);
        viewport.SetPhotographic100();

        var aligned = viewport.PhysicalAlignedOrigin();

        Assert.Equal(Math.Round(aligned.X * renderScaling), aligned.X * renderScaling, 9);
        Assert.Equal(Math.Round(aligned.Y * renderScaling), aligned.Y * renderScaling, 9);
    }

    private static ViewportModel CreateViewport(
        ImageSize sourceSize,
        LogicalSize viewportSize,
        double renderScaling)
    {
        var viewport = new ViewportModel();
        viewport.SetViewport(viewportSize, renderScaling);
        viewport.SetImage(sourceSize);
        return viewport;
    }
}
