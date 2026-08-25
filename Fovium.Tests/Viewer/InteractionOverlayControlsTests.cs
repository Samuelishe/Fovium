using Avalonia.Media;
using Fovium.Presentation;
using Fovium.Rendering;
using Fovium.Viewer;

namespace Fovium.Tests.Viewer;

public sealed class InteractionOverlayControlsTests
{
    [Fact]
    public void PhotoPresentationUsesCompositorBitmapIsolation()
    {
        var viewport = new PhotoViewportControl();

        var cache = Assert.IsType<BitmapCache>(viewport.CacheMode);
        Assert.True(cache.SnapsToDevicePixels);
        Assert.Equal(1, cache.RenderAtScale);
    }

    [Fact]
    public void PointerMovementChangesOnlyRenderTranslation()
    {
        var overlay = new PointerFeedbackOverlayControl();
        var presentation = DrawingCursorPresentation.Resolve(
            markupToolsVisible: true,
            highlightEnabled: false,
            MarkupTool.Brush,
            strokePhysicalPixels: 32,
            new PresentationColor(12, 34, 56),
            opacity: 0.5,
            highlightRadiusPhysicalPixels: 42,
            renderScaling: 1.25);
        overlay.SetPresentation(presentation);
        var transform = Assert.IsType<TranslateTransform>(overlay.RenderTransform);
        var originalWidth = overlay.Width;
        var originalHeight = overlay.Height;

        overlay.SetPointerPosition(new PointD(100, 120));
        var first = (transform.X, transform.Y);
        overlay.SetPointerPosition(new PointD(140, 165));

        Assert.Same(transform, overlay.RenderTransform);
        Assert.Equal(originalWidth, overlay.Width);
        Assert.Equal(originalHeight, overlay.Height);
        Assert.Equal(40, transform.X - first.X, 8);
        Assert.Equal(45, transform.Y - first.Y, 8);
        Assert.True(overlay.IsVisible);
    }

    [Fact]
    public void PointerLeavingHidesFeedbackWithoutChangingPresentation()
    {
        var overlay = new PointerFeedbackOverlayControl();
        var presentation = DrawingCursorPresentation.Resolve(
            markupToolsVisible: false,
            highlightEnabled: true,
            MarkupTool.Brush,
            strokePhysicalPixels: 4,
            new PresentationColor(255, 213, 79),
            opacity: 0.3,
            highlightRadiusPhysicalPixels: 42,
            renderScaling: 1);
        overlay.SetPresentation(presentation);
        overlay.SetPointerPosition(new PointD(50, 60));

        overlay.SetPointerPosition(null);

        Assert.False(overlay.IsVisible);
        Assert.Equal(presentation, overlay.Presentation);
    }
}
