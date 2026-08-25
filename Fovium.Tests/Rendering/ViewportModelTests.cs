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
    [InlineData(2.0)]
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

    [Theory]
    [InlineData(1.00)]
    [InlineData(1.25)]
    [InlineData(1.50)]
    [InlineData(2.00)]
    public void Peek100KeepsSourcePointUnderPointerAtEverySupportedRenderScaling(double renderScaling)
    {
        var viewport = CreateViewport(
            new PixelSize(4000, 3000),
            new LogicalSize(1200, 800),
            renderScaling);
        var pointer = new PointD(640, 420);
        var sourceBefore = viewport.SourcePointAt(pointer);

        viewport.SetPhotographic100ForInspection(pointer);

        var sourceAfter = viewport.SourcePointAt(pointer);
        Assert.Equal(1, viewport.PhysicalScale, 12);
        Assert.Equal(sourceBefore.X, sourceAfter.X, 9);
        Assert.Equal(sourceBefore.Y, sourceAfter.Y, 9);
    }

    [Theory]
    [InlineData(0.42)]
    [InlineData(1.00)]
    [InlineData(2.00)]
    public void PeekRestoresManualScaleAndOffCenterPointOfInterest(double scale)
    {
        var viewport = CreateViewport(
            new PixelSize(4000, 3000),
            new LogicalSize(1200, 800),
            1.25);
        viewport.ZoomAt(new PointD(600, 400), scale);
        viewport.PanBy(new PointD(-175, 90));
        var before = viewport.CaptureTransfer();

        viewport.SetPhotographic100ForInspection(new PointD(700, 350));
        viewport.PanBy(new PointD(-80, 40));
        viewport.SetImage(viewport.SourceSize, before);

        Assert.Equal(before, viewport.CaptureTransfer());
    }

    [Fact]
    public void PeekFromFitRestoresSemanticFitAfterViewportGeometryChanges()
    {
        var viewport = CreateViewport(
            new PixelSize(4000, 3000),
            new LogicalSize(1200, 800),
            1);
        var before = viewport.CaptureTransfer();

        viewport.SetPhotographic100ForInspection(new PointD(650, 420));
        viewport.SetViewport(new LogicalSize(900, 700), 1.5);
        viewport.SetImage(viewport.SourceSize, before);

        Assert.Equal(ViewTransfer.Fit, viewport.CaptureTransfer());
        Assert.Equal(ViewportMode.Fit, viewport.Mode);
        Assert.Equal(0.3375, viewport.PhysicalScale, 12);
    }

    [Fact]
    public void PointerOverStageUsesCurrentPointOfInterestAtViewportCenter()
    {
        var viewport = CreateViewport(
            new PixelSize(1000, 1000),
            new LogicalSize(1600, 900),
            1);
        var center = new PointD(800, 450);
        var centerSourceBefore = viewport.SourcePointAt(center);

        viewport.SetPhotographic100ForInspection(new PointD(20, 450));

        var centerSourceAfter = viewport.SourcePointAt(center);
        Assert.Equal(centerSourceBefore.X, centerSourceAfter.X, 9);
        Assert.Equal(centerSourceBefore.Y, centerSourceAfter.Y, 9);
        var transfer = viewport.CaptureTransfer();
        Assert.InRange(transfer.PointOfInterest.X, 0, 1);
        Assert.InRange(transfer.PointOfInterest.Y, 0, 1);
        Assert.True(double.IsFinite(transfer.PointOfInterest.X));
        Assert.True(double.IsFinite(transfer.PointOfInterest.Y));
    }

    [Fact]
    public void PeekNearBoundaryAppliesNormalOriginClamp()
    {
        var viewport = CreateViewport(
            new PixelSize(4000, 3000),
            new LogicalSize(800, 600),
            1);
        var pointer = new PointD(800, 600);

        viewport.SetPhotographic100ForInspection(pointer);

        Assert.Equal(800 - 4000, viewport.OriginDip.X, 12);
        Assert.Equal(600 - 3000, viewport.OriginDip.Y, 12);
        var sourceAtPointer = viewport.SourcePointAt(pointer);
        Assert.InRange(sourceAtPointer.X, 0, 4000);
        Assert.InRange(sourceAtPointer.Y, 0, 3000);
    }

    [Fact]
    public void OneHundredPeekCyclesRestoreWithoutCoordinateDrift()
    {
        var viewport = CreateViewport(
            new PixelSize(6016, 4016),
            new LogicalSize(1200, 800),
            1.5);
        viewport.ZoomAt(new PointD(600, 400), 0.42);
        viewport.PanBy(new PointD(-130, -75));
        var expected = viewport.CaptureTransfer();

        for (var iteration = 0; iteration < 100; iteration++)
        {
            viewport.SetPhotographic100ForInspection(new PointD(730, 360));
            viewport.PanBy(new PointD(-25, 12));
            viewport.SetImage(viewport.SourceSize, expected);
        }

        Assert.Equal(expected, viewport.CaptureTransfer());
    }

    [Theory]
    [InlineData(0.42)]
    [InlineData(1.00)]
    [InlineData(2.00)]
    public void BlinkTransferPreservesManualScaleAndPoiThenRestoresCurrentExactly(double scale)
    {
        var viewport = CreateViewport(
            new PixelSize(4000, 3000),
            new LogicalSize(800, 600),
            1.25);
        viewport.ZoomAt(new PointD(400, 300), scale);
        viewport.PanBy(new PointD(-120, -80));
        var current = viewport.CaptureTransfer();

        viewport.SetImage(new PixelSize(6000, 4000), current);
        var comparison = viewport.CaptureTransfer();

        Assert.Equal(ViewportMode.Manual, comparison.Mode);
        Assert.Equal(scale, comparison.PhysicalScale, 12);
        Assert.Equal(current.PointOfInterest.X, comparison.PointOfInterest.X, 9);
        Assert.Equal(current.PointOfInterest.Y, comparison.PointOfInterest.Y, 9);

        viewport.SetImage(new PixelSize(4000, 3000), current);
        Assert.Equal(current, viewport.CaptureTransfer());
    }

    [Fact]
    public void BlinkTransferMapsFitToFitAndRestoresFit()
    {
        var viewport = CreateViewport(
            new PixelSize(4000, 3000),
            new LogicalSize(800, 600),
            1.5);
        var current = viewport.CaptureTransfer();

        viewport.SetImage(new PixelSize(3000, 5000), current);

        Assert.Equal(ViewTransfer.Fit, viewport.CaptureTransfer());
        viewport.SetImage(new PixelSize(4000, 3000), current);
        Assert.Equal(ViewTransfer.Fit, viewport.CaptureTransfer());
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
