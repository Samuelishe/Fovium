using Fovium.Rendering;
using Fovium.Settings;
using Fovium.Viewer;

namespace Fovium.Tests.Viewer;

public sealed class ImageChangeViewPolicyResolverTests
{
    [Fact]
    public void KeepCurrentScale_Fit_NextRemainsFit()
    {
        var transfer = ImageChangeViewPolicyResolver.ForNavigation(
            ImageChangeViewPolicy.KeepCurrentScale,
            ViewTransfer.Fit);

        Assert.Equal(ViewportMode.Fit, transfer.Mode);
    }

    [Fact]
    public void KeepCurrentScale_ActualSize_NextKeepsPhysical100()
    {
        var transfer = ResolveManual(1, new NormalizedPoint(0.5, 0.5));

        Assert.Equal(ViewportMode.Manual, transfer.Mode);
        Assert.Equal(1, transfer.PhysicalScale);
    }

    [Fact]
    public void KeepCurrentScale_ManualZoom_NextKeepsPhysicalScale()
    {
        var transfer = ResolveManual(0.45, new NormalizedPoint(0.5, 0.5));

        Assert.Equal(0.45, transfer.PhysicalScale);
    }

    [Fact]
    public void KeepCurrentScale_PreservesNormalizedPointOfInterest()
    {
        var point = new NormalizedPoint(0.78, 0.24);

        var transfer = ResolveManual(1.7, point);

        Assert.Equal(point, transfer.PointOfInterest);
    }

    [Fact]
    public void KeepCurrentScale_DoesNotUpscaleReducedViewToFit()
    {
        var target = CreateViewport(new PixelSize(800, 600), new LogicalSize(1600, 1200));
        var transfer = ResolveManual(0.2, new NormalizedPoint(0.7, 0.3));

        target.SetImage(new PixelSize(400, 300), transfer);

        Assert.Equal(ViewportMode.Manual, target.Mode);
        Assert.Equal(0.2, target.PhysicalScale);
        Assert.Equal(760, target.OriginDip.X, 9);
        Assert.Equal(570, target.OriginDip.Y, 9);
    }

    [Fact]
    public void FitEachImage_ManualZoom_NextBecomesFit()
    {
        var transfer = ImageChangeViewPolicyResolver.ForNavigation(
            ImageChangeViewPolicy.FitEachImage,
            Manual(2, new NormalizedPoint(0.8, 0.2)));

        Assert.Equal(ViewTransfer.Fit, transfer);
    }

    [Fact]
    public void FitEachImage_ActualSize_NextBecomesFit()
    {
        var transfer = ImageChangeViewPolicyResolver.ForNavigation(
            ImageChangeViewPolicy.FitEachImage,
            Manual(1, new NormalizedPoint(0.5, 0.5)));

        Assert.Equal(ViewportMode.Fit, transfer.Mode);
    }

    [Fact]
    public void FitEachImage_NextIsCentered()
    {
        var target = CreateViewport(new PixelSize(1, 1), new LogicalSize(1000, 700));
        var transfer = ImageChangeViewPolicyResolver.ForNavigation(
            ImageChangeViewPolicy.FitEachImage,
            Manual(1.8, new NormalizedPoint(0.9, 0.1)));

        target.SetImage(new PixelSize(200, 400), transfer);

        Assert.Equal(ViewportMode.Fit, target.Mode);
        Assert.Equal(400, target.OriginDip.X, 9);
        Assert.Equal(150, target.OriginDip.Y, 9);
    }

    [Fact]
    public void NewSequence_AlwaysBeginsFit()
    {
        Assert.Equal(ViewTransfer.Fit, ImageChangeViewPolicyResolver.ForNewSequence());
    }

    [Fact]
    public void KeepCurrentScale_ClampsPointForDifferentSourceDimensions()
    {
        var target = CreateViewport(new PixelSize(1, 1), new LogicalSize(800, 600));
        var transfer = ResolveManual(1, new NormalizedPoint(0.95, 0.9));

        target.SetImage(new PixelSize(3000, 1000), transfer);
        var restored = target.CaptureTransfer();

        Assert.Equal(1, restored.PhysicalScale);
        Assert.InRange(restored.PointOfInterest.X, 0, 1);
        Assert.InRange(restored.PointOfInterest.Y, 0, 1);
    }

    private static ViewTransfer ResolveManual(double scale, NormalizedPoint point) =>
        ImageChangeViewPolicyResolver.ForNavigation(
            ImageChangeViewPolicy.KeepCurrentScale,
            Manual(scale, point));

    private static ViewTransfer Manual(double scale, NormalizedPoint point) =>
        new(ViewportMode.Manual, scale, point);

    private static ViewportModel CreateViewport(PixelSize source, LogicalSize viewport)
    {
        var model = new ViewportModel();
        model.SetViewport(viewport, 1);
        model.SetImage(source);
        return model;
    }
}
