using Fovium.Presentation;

namespace Fovium.Tests.Presentation;

public sealed class FloatingOverlayPlacementTests
{
    [Fact]
    public void DefaultPlacementIsTopCenterInsideClientInset()
    {
        var point = FloatingOverlayPlacement.Default.Resolve(
            new FloatingOverlaySize(1000, 700),
            new FloatingOverlaySize(400, 80));

        Assert.Equal(300, point.X);
        Assert.Equal(12, point.Y);
    }

    [Fact]
    public void PhotoInfoDefaultIsBottomLeftInsideClientInset()
    {
        var point = FloatingOverlayPlacement.BottomLeft.Resolve(
            new FloatingOverlaySize(1000, 700),
            new FloatingOverlaySize(360, 180));

        Assert.Equal(12, point.X);
        Assert.Equal(508, point.Y);
    }

    [Fact]
    public void HistogramDefaultIsBottomRightInsideClientInset()
    {
        var point = FloatingOverlayPlacement.BottomRight.Resolve(
            new FloatingOverlaySize(1000, 700),
            new FloatingOverlaySize(320, 210));

        Assert.Equal(new FloatingOverlayPoint(668, 478), point);
    }

    [Fact]
    public void DragBeyondEveryEdgeClampsNormalizedPlacement()
    {
        var client = new FloatingOverlaySize(1000, 700);
        var panel = new FloatingOverlaySize(400, 80);
        var left = FloatingOverlayPlacement.FromPosition(
            new FloatingOverlayPoint(-100, 200), client, panel);
        var right = FloatingOverlayPlacement.FromPosition(
            new FloatingOverlayPoint(2000, 200), client, panel);
        var top = FloatingOverlayPlacement.FromPosition(
            new FloatingOverlayPoint(400, -100), client, panel);
        var bottom = FloatingOverlayPlacement.FromPosition(
            new FloatingOverlayPoint(400, 2000), client, panel);

        Assert.Equal(0, left.NormalizedX);
        Assert.Equal(1, right.NormalizedX);
        Assert.Equal(0, top.NormalizedY);
        Assert.Equal(1, bottom.NormalizedY);
    }

    [Fact]
    public void NormalizedPlacementRoundTripsAcrossResizeAndFullscreenGeometry()
    {
        var placement = new FloatingOverlayPlacement(0.23, 0.81);
        var normalClient = new FloatingOverlaySize(1000, 700);
        var fullscreenClient = new FloatingOverlaySize(1920, 1080);
        var panel = new FloatingOverlaySize(420, 72);

        var normalPoint = placement.Resolve(normalClient, panel);
        var roundTrip = FloatingOverlayPlacement.FromPosition(
            normalPoint,
            normalClient,
            panel);
        var fullscreenPoint = roundTrip.Resolve(fullscreenClient, panel);

        Assert.Equal(placement.NormalizedX, roundTrip.NormalizedX, 10);
        Assert.Equal(placement.NormalizedY, roundTrip.NormalizedY, 10);
        Assert.InRange(fullscreenPoint.X, 12, 1488);
        Assert.InRange(fullscreenPoint.Y, 12, 996);
    }

    [Fact]
    public void SmallerClientAndOversizedPanelRemainFiniteAndReachable()
    {
        var point = new FloatingOverlayPlacement(double.NaN, double.PositiveInfinity).Resolve(
            new FloatingOverlaySize(320, 200),
            new FloatingOverlaySize(500, 260));

        Assert.Equal(0, point.X);
        Assert.Equal(0, point.Y);
        Assert.True(double.IsFinite(point.X));
        Assert.True(double.IsFinite(point.Y));
    }

    [Fact]
    public void LiveDragReturnsClampedTranslationWithoutChangingCanonicalBase()
    {
        var basePosition = new FloatingOverlayPoint(300, 12);

        var update = FloatingOverlayDrag.Update(
            basePosition,
            new FloatingOverlayPoint(350, 30),
            new FloatingOverlayPoint(900, 900),
            new FloatingOverlaySize(1000, 700),
            new FloatingOverlaySize(400, 80));

        Assert.Equal(new FloatingOverlayPoint(588, 608), update.Position);
        Assert.Equal(new FloatingOverlayPoint(288, 596), update.Translation);
        Assert.Equal(new FloatingOverlayPoint(300, 12), basePosition);
    }

    [Fact]
    public void LiveDragHandlesOversizedPanelWithoutNaN()
    {
        var update = FloatingOverlayDrag.Update(
            new FloatingOverlayPoint(0, 0),
            new FloatingOverlayPoint(10, 10),
            new FloatingOverlayPoint(double.PositiveInfinity, double.NaN),
            new FloatingOverlaySize(320, 200),
            new FloatingOverlaySize(500, 260));

        Assert.Equal(new FloatingOverlayPoint(0, 0), update.Position);
        Assert.Equal(new FloatingOverlayPoint(0, 0), update.Translation);
    }
}
