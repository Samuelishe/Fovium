using Avalonia.Controls;
using Avalonia.Media;
using Fovium.Diagnostics;
using Fovium.Presentation;
using Fovium.Rendering;
using Fovium.Viewer;

namespace Fovium.Tests.Viewer;

public sealed class InteractionOverlayControlsTests
{
    [Fact]
    public void PhotoViewportSuppressesFocusChromeWithoutDisablingKeyboardFocus()
    {
        var viewport = new PhotoViewportControl();

        Assert.True(viewport.Focusable);
        Assert.True(viewport.IsSet(Control.FocusAdornerProperty));
        Assert.Null(viewport.FocusAdorner);
    }

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

    [Fact]
    public void ThousandColorPickerMovesStayOnTransformBackedPointerLayer()
    {
        var diagnostics = new InteractionRenderDiagnostics(enabled: true);
        var overlay = new PointerFeedbackOverlayControl();
        overlay.ConfigureDiagnostics(diagnostics);
        overlay.SetPresentation(DrawingCursorPresentation.CreateColorPicker(renderScaling: 1.25));
        var transform = Assert.IsType<TranslateTransform>(overlay.RenderTransform);

        for (var index = 0; index < 1000; index++)
        {
            diagnostics.RecordPointerMoved();
            overlay.SetPointerPosition(new PointD(index, index / 2d));
        }

        var metrics = diagnostics.GetMetrics();
        Assert.Equal(1000, metrics.PointerMovedCount);
        Assert.Equal(0, metrics.PhotoPresentationRenderCount);
        Assert.Equal(0, metrics.PhotoSkiaDrawCount);
        Assert.Equal(0, metrics.MarkupOverlayDrawCount);
        Assert.Same(transform, overlay.RenderTransform);
        Assert.Equal(999, transform.X + (overlay.Width / 2), 8);
        Assert.Equal(499.5, transform.Y + (overlay.Height / 2), 8);
    }
}
