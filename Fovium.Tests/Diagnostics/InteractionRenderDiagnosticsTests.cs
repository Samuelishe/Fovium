using Fovium.Diagnostics;

namespace Fovium.Tests.Diagnostics;

public sealed class InteractionRenderDiagnosticsTests
{
    [Fact]
    public void DisabledDiagnosticsDoNotRecordHotPathEvents()
    {
        var diagnostics = new InteractionRenderDiagnostics();

        RecordEveryEvent(diagnostics);

        Assert.Equal(default, diagnostics.GetMetrics());
    }

    [Fact]
    public void EnabledDiagnosticsDistinguishEveryRenderFrequencyLayer()
    {
        var diagnostics = new InteractionRenderDiagnostics(enabled: true);

        RecordEveryEvent(diagnostics);

        var metrics = diagnostics.GetMetrics();
        Assert.Equal(1, metrics.PointerMovedCount);
        Assert.Equal(1, metrics.PhotoPresentationRenderCount);
        Assert.Equal(1, metrics.PhotoSkiaDrawCount);
        Assert.Equal(1, metrics.MarkupOverlayDrawCount);
        Assert.Equal(1, metrics.PointerFeedbackDrawCount);
        Assert.Equal(1, metrics.FloatingDockDragUpdateCount);
        Assert.Equal(1, metrics.ViewerLayoutSizeChangeCount);
    }

    private static void RecordEveryEvent(InteractionRenderDiagnostics diagnostics)
    {
        diagnostics.RecordPointerMoved();
        diagnostics.RecordPhotoPresentationRender();
        diagnostics.RecordPhotoSkiaDraw();
        diagnostics.RecordMarkupOverlayDraw();
        diagnostics.RecordPointerFeedbackDraw();
        diagnostics.RecordFloatingDockDragUpdate();
        diagnostics.RecordViewerLayoutSizeChange();
    }
}
