using Fovium.Presentation;
using Fovium.Rendering;

namespace Fovium.Tests.Presentation;

public sealed class MarkupTransformTests
{
    [Theory]
    [InlineData("fit")]
    [InlineData("100")]
    [InlineData("manual")]
    [InlineData("pan")]
    [InlineData("resize")]
    public void OrientedSourcePointTracksViewportAcrossEverySupportedViewChange(string scenario)
    {
        var viewport = new ViewportModel();
        viewport.SetViewport(new LogicalSize(1200, 800), renderScaling: 1.5);
        viewport.SetImage(new PixelSize(4000, 3000));
        var source = new PointD(1900, 1200);

        switch (scenario)
        {
            case "fit":
                break;
            case "100":
                viewport.ZoomAt(new PointD(600, 400), 1);
                break;
            case "manual":
                viewport.ZoomAt(new PointD(540, 360), 0.42);
                break;
            case "pan":
                viewport.ZoomAt(new PointD(540, 360), 2);
                viewport.PanBy(new PointD(-175, 90));
                break;
            case "resize":
                viewport.ZoomAt(new PointD(540, 360), 0.75);
                viewport.SetViewport(new LogicalSize(1600, 900), renderScaling: 1.5);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        var transform = new MarkupTransform(viewport.DestinationDip, viewport.SourceSize);
        var expected = viewport.ViewportPointFor(source);
        var actual = transform.SourceToViewport(source);

        Assert.Equal(expected.X, actual.X, 9);
        Assert.Equal(expected.Y, actual.Y, 9);
    }

    [Fact]
    public void OverlayTransformDoesNotMutatePhotoGeometry()
    {
        var viewport = new ViewportModel();
        viewport.SetViewport(new LogicalSize(900, 700), renderScaling: 1.25);
        viewport.SetImage(new PixelSize(3000, 2000));
        viewport.ZoomAt(new PointD(450, 350), 1.6);
        viewport.PanBy(new PointD(-80, 45));
        var before = (viewport.DestinationDip, viewport.CaptureTransfer());

        var transform = new MarkupTransform(viewport.DestinationDip, viewport.SourceSize);
        _ = transform.SourceToViewport(new PointD(500, 600));
        _ = transform.SourceStrokeToViewport(8);

        Assert.Equal(before, (viewport.DestinationDip, viewport.CaptureTransfer()));
    }
}
