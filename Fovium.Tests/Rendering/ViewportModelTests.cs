using Fovium.Rendering;

namespace Fovium.Tests.Rendering;

public sealed class ViewportModelTests
{
    [Theory]
    [InlineData(1600, 900, 800, 600, 0.5)]
    [InlineData(900, 1600, 800, 600, 0.375)]
    [InlineData(1000, 1000, 800, 600, 0.6)]
    [InlineData(300, 200, 800, 600, 1.0)]
    public void FitContainsLandscapePortraitAndSquareWithoutUpscale(
        int sourceWidth,
        int sourceHeight,
        double viewportWidth,
        double viewportHeight,
        double expectedPhysicalScale)
    {
        var viewport = CreateViewport(
            new PixelSize(sourceWidth, sourceHeight),
            new LogicalSize(viewportWidth, viewportHeight),
            1);

        Assert.Equal(ViewportMode.Fit, viewport.Mode);
        Assert.Equal(expectedPhysicalScale, viewport.PhysicalScale, 12);
        Assert.True(viewport.DestinationDip.Width <= viewportWidth + 1e-9);
        Assert.True(viewport.DestinationDip.Height <= viewportHeight + 1e-9);
    }

    [Theory]
    [InlineData(1.00, 1.00)]
    [InlineData(1.25, 0.80)]
    [InlineData(1.50, 2.0 / 3.0)]
    [InlineData(2.00, 0.50)]
    public void Photographic100UsesPhysicalScaleIndependentOfLogicalDpi(
        double renderScaling,
        double expectedDipScale)
    {
        var viewport = CreateViewport(new PixelSize(4000, 3000), new LogicalSize(800, 600), renderScaling);

        viewport.ZoomAt(new PointD(400, 300), 1);

        Assert.Equal(1, viewport.PhysicalScale, 12);
        Assert.Equal(expectedDipScale, viewport.DipScale, 12);
    }

    [Fact]
    public void CursorAnchoredWheelZoomKeepsSourcePointInvariant()
    {
        var viewport = CreateViewport(new PixelSize(4000, 3000), new LogicalSize(800, 600), 1.5);
        var pointer = new PointD(500, 300);
        var before = viewport.SourcePointAt(pointer);
        var initialScale = viewport.PhysicalScale;

        viewport.ZoomBySteps(pointer, 1);

        var after = viewport.SourcePointAt(pointer);
        Assert.Equal(before.X, after.X, 9);
        Assert.Equal(before.Y, after.Y, 9);
        Assert.Equal(initialScale * ViewportModel.WheelStepRatio, viewport.PhysicalScale, 12);
    }

    [Fact]
    public void DoubleClickFromFitEnters100AroundPointer()
    {
        var viewport = CreateViewport(new PixelSize(4000, 3000), new LogicalSize(800, 600), 1);
        var pointer = new PointD(500, 300);
        var before = viewport.SourcePointAt(pointer);

        viewport.ToggleFitAnd100(pointer);

        Assert.Equal(ViewportMode.Manual, viewport.Mode);
        Assert.Equal(1, viewport.PhysicalScale, 12);
        Assert.Equal(before.X, viewport.SourcePointAt(pointer).X, 9);
        Assert.Equal(before.Y, viewport.SourcePointAt(pointer).Y, 9);
    }

    [Fact]
    public void DoubleClickFromManualReturnsToFit()
    {
        var viewport = CreateViewport(new PixelSize(4000, 3000), new LogicalSize(800, 600), 1);
        viewport.ZoomBySteps(new PointD(400, 300), 3);

        viewport.ToggleFitAnd100(new PointD(200, 200));

        Assert.Equal(ViewportMode.Fit, viewport.Mode);
        Assert.Equal(0.2, viewport.PhysicalScale, 12);
    }

    [Fact]
    public void RepeatedFitResizeDoesNotAccumulateDrift()
    {
        var viewport = CreateViewport(new PixelSize(6016, 4016), new LogicalSize(1200, 800), 1.25);
        var expected = viewport.DestinationDip;

        for (var iteration = 0; iteration < 100; iteration++)
        {
            viewport.SetViewport(new LogicalSize(777.25, 555.5), 1.5);
            viewport.SetViewport(new LogicalSize(1200, 800), 1.25);
        }

        Assert.Equal(expected, viewport.DestinationDip);
    }

    [Fact]
    public void PanClampsOversizedImageAndCentersFittingAxis()
    {
        var viewport = CreateViewport(new PixelSize(2000, 300), new LogicalSize(800, 600), 1);
        viewport.ZoomAt(new PointD(400, 300), 1);

        viewport.PanBy(new PointD(10_000, 10_000));

        Assert.Equal(0, viewport.OriginDip.X, 12);
        Assert.Equal(150, viewport.OriginDip.Y, 12);
    }

    [Theory]
    [InlineData(1.0, true)]
    [InlineData(2.0, true)]
    [InlineData(1.5, false)]
    [InlineData(0.75, false)]
    public void PhysicalAlignmentIsSelectedOnlyAtIntegralPhysicalScales(double scale, bool expected)
    {
        var viewport = CreateViewport(new PixelSize(4000, 3000), new LogicalSize(800, 600), 1.25);

        viewport.ZoomAt(new PointD(400, 300), scale);

        Assert.Equal(expected, viewport.UsesExactPixelSampling);
        if (expected)
        {
            var aligned = viewport.PhysicalAlignedOrigin();
            Assert.Equal(Math.Round(aligned.X * 1.25), aligned.X * 1.25, 9);
            Assert.Equal(Math.Round(aligned.Y * 1.25), aligned.Y * 1.25, 9);
        }
    }

    [Fact]
    public void FitTransferKeepsNextImageInFit()
    {
        var viewport = CreateViewport(new PixelSize(4000, 3000), new LogicalSize(800, 600), 1);

        viewport.SetImage(new PixelSize(3000, 4000), viewport.CaptureTransfer());

        Assert.Equal(ViewportMode.Fit, viewport.Mode);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.25)]
    public void ManualTransferPreservesPhysicalScaleAndNormalizedPointOfInterest(double scale)
    {
        var viewport = CreateViewport(new PixelSize(4000, 3000), new LogicalSize(800, 600), 1);
        viewport.ZoomAt(new PointD(400, 300), scale);
        viewport.PanBy(new PointD(-300, -150));
        var transfer = viewport.CaptureTransfer();

        viewport.SetImage(new PixelSize(2000, 1500), transfer);

        var restored = viewport.CaptureTransfer();
        Assert.Equal(ViewportMode.Manual, restored.Mode);
        Assert.Equal(scale, restored.PhysicalScale, 12);
        Assert.Equal(transfer.PointOfInterest.X, restored.PointOfInterest.X, 9);
        Assert.Equal(transfer.PointOfInterest.Y, restored.PointOfInterest.Y, 9);
    }

    private static ViewportModel CreateViewport(
        PixelSize sourceSize,
        LogicalSize viewportSize,
        double renderScaling)
    {
        var viewport = new ViewportModel();
        viewport.SetViewport(viewportSize, renderScaling);
        viewport.SetImage(sourceSize);
        return viewport;
    }
}
