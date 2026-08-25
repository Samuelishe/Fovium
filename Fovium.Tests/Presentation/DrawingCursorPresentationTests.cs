using Fovium.Presentation;

namespace Fovium.Tests.Presentation;

public sealed class DrawingCursorPresentationTests
{
    private static readonly PresentationColor Color = new(10, 20, 30);

    [Theory]
    [InlineData(1.00, 32.0)]
    [InlineData(1.25, 25.6)]
    [InlineData(1.50, 21.333333333333332)]
    [InlineData(2.00, 16.0)]
    public void BrushAndEraserPhysicalDiameterMapsToDip(
        double renderScaling,
        double expectedDip)
    {
        var brush = Resolve(true, false, MarkupTool.Brush, 32, renderScaling);
        var eraser = Resolve(true, false, MarkupTool.Eraser, 32, renderScaling);

        Assert.Equal(DrawingCursorKind.Brush, brush.Kind);
        Assert.Equal(DrawingCursorKind.Eraser, eraser.Kind);
        Assert.Equal(expectedDip, brush.DiameterDip, 10);
        Assert.Equal(expectedDip, eraser.DiameterDip, 10);
        Assert.Equal(32, brush.DiameterDip * renderScaling, 10);
        Assert.Equal(32, eraser.DiameterDip * renderScaling, 10);
    }

    [Theory]
    [InlineData(1.00)]
    [InlineData(1.25)]
    [InlineData(1.50)]
    [InlineData(2.00)]
    public void PrecisionCrosshairRetainsFixedPhysicalExtent(double renderScaling)
    {
        var cursor = Resolve(true, false, MarkupTool.Line, 64, renderScaling);

        Assert.Equal(DrawingCursorKind.Precision, cursor.Kind);
        Assert.Equal(14, cursor.DiameterDip * renderScaling, 10);
        Assert.Equal(7, cursor.CrosshairHalfExtentDip * renderScaling, 10);
    }

    [Fact]
    public void MarkupCursorSuppressesHighlightAndUsesActiveBrushStyle()
    {
        var cursor = DrawingCursorPresentation.Resolve(
            markupToolsVisible: true,
            highlightEnabled: true,
            MarkupTool.Brush,
            strokePhysicalPixels: 48,
            Color,
            opacity: 0.35,
            highlightRadiusPhysicalPixels: 90,
            renderScaling: 1.5);

        Assert.Equal(DrawingCursorKind.Brush, cursor.Kind);
        Assert.Equal(32, cursor.DiameterDip);
        Assert.Equal(Color, cursor.Color);
        Assert.Equal(0.35, cursor.Opacity);
    }

    [Theory]
    [InlineData(false, false, (int)MarkupTool.Brush, (int)DrawingCursorKind.Viewer)]
    [InlineData(false, true, (int)MarkupTool.Brush, (int)DrawingCursorKind.Highlight)]
    [InlineData(true, true, (int)MarkupTool.Brush, (int)DrawingCursorKind.Brush)]
    [InlineData(true, true, (int)MarkupTool.Eraser, (int)DrawingCursorKind.Eraser)]
    [InlineData(true, true, (int)MarkupTool.Line, (int)DrawingCursorKind.Precision)]
    [InlineData(true, true, (int)MarkupTool.Rectangle, (int)DrawingCursorKind.Precision)]
    [InlineData(true, true, (int)MarkupTool.Ellipse, (int)DrawingCursorKind.Precision)]
    [InlineData(true, true, (int)MarkupTool.Arrow, (int)DrawingCursorKind.Precision)]
    [InlineData(true, true, (int)MarkupTool.Hand, (int)DrawingCursorKind.Hand)]
    public void CursorStateMatrixHasDeterministicPriority(
        bool markupVisible,
        bool highlightEnabled,
        int toolValue,
        int expectedKindValue)
    {
        var cursor = Resolve(
            markupVisible,
            highlightEnabled,
            (MarkupTool)toolValue,
            32,
            1);

        Assert.Equal((DrawingCursorKind)expectedKindValue, cursor.Kind);
    }

    [Theory]
    [InlineData(false, (int)DrawingCursorKind.Brush, (int)ViewerSystemCursorMode.Visible)]
    [InlineData(true, (int)DrawingCursorKind.Viewer, (int)ViewerSystemCursorMode.Visible)]
    [InlineData(true, (int)DrawingCursorKind.Brush, (int)ViewerSystemCursorMode.Hidden)]
    [InlineData(true, (int)DrawingCursorKind.Eraser, (int)ViewerSystemCursorMode.Hidden)]
    [InlineData(true, (int)DrawingCursorKind.Precision, (int)ViewerSystemCursorMode.Hidden)]
    [InlineData(true, (int)DrawingCursorKind.Highlight, (int)ViewerSystemCursorMode.Hidden)]
    [InlineData(true, (int)DrawingCursorKind.Hand, (int)ViewerSystemCursorMode.Hand)]
    public void SystemCursorModeChangesOnlyAcrossMeaningfulStateTransitions(
        bool pointerInside,
        int feedbackKind,
        int expectedMode)
    {
        Assert.Equal(
            (ViewerSystemCursorMode)expectedMode,
            ViewerSystemCursorPresentation.Resolve(
                pointerInside,
                (DrawingCursorKind)feedbackKind));
    }

    private static DrawingCursorPresentation Resolve(
        bool markupVisible,
        bool highlightEnabled,
        MarkupTool tool,
        double stroke,
        double scaling) => DrawingCursorPresentation.Resolve(
        markupVisible,
        highlightEnabled,
        tool,
        stroke,
        Color,
        0.5,
        42,
        scaling);
}
