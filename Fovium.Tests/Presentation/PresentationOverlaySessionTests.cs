using Fovium.Presentation;
using Fovium.Rendering;

namespace Fovium.Tests.Presentation;

public sealed class PresentationOverlaySessionTests
{
    [Fact]
    public void HighlightCanBeRepeatedlyToggledWithoutChangingMarkupState()
    {
        var session = CreateSession();

        for (var cycle = 0; cycle < 100; cycle++)
        {
            Assert.True(session.ToggleHighlight());
            Assert.False(session.ToggleHighlight());
        }

        Assert.False(session.HighlightEnabled);
        Assert.Equal(0, session.GetElementCount("A"));
    }

    [Theory]
    [InlineData((int)MarkupTool.Brush, typeof(BrushMarkup))]
    [InlineData((int)MarkupTool.Line, typeof(LineMarkup))]
    [InlineData((int)MarkupTool.Rectangle, typeof(RectangleMarkup))]
    [InlineData((int)MarkupTool.Arrow, typeof(ArrowMarkup))]
    public void EveryToolCreatesAnImageBoundElementWithSelectedColorAndStroke(
        int toolValue,
        Type expectedType)
    {
        var session = CreateSession();
        session.SelectImage("A");
        Assert.True(session.ToggleMarkupTools());
        session.SetActiveTool((MarkupTool)toolValue);
        var color = new PresentationColor(0x12, 0x34, 0x56);
        session.SetActiveColor(color);
        session.SetActiveStrokePhysicalPixels(6);

        Assert.True(session.BeginDrawing(new PointD(10, 20), physicalScale: 2));
        Assert.True(session.ContinueDrawing(new PointD(30, 40)));
        Assert.True(session.EndDrawing(new PointD(50, 60)));

        var element = Assert.Single(session.GetRenderSnapshot("A").Elements);
        Assert.IsType(expectedType, element);
        Assert.Equal(color, element.Color);
        Assert.Equal(3, element.StrokeWidthSource);
    }

    [Fact]
    public void OverlayReturnsWithItsImageAndDoesNotLeakToAnotherImage()
    {
        var session = CreateSession();
        session.SelectImage("A");
        session.ToggleMarkupTools();
        DrawLine(session);

        session.SelectImage("B");

        Assert.True(session.GetRenderSnapshot("B").IsEmpty);
        Assert.False(session.GetRenderSnapshot("A").IsEmpty);

        session.SelectImage("A");
        Assert.Single(session.GetRenderSnapshot("A").Elements);
    }

    [Fact]
    public void HidingPanelKeepsCommittedDrawingButPreventsNewDrawing()
    {
        var session = CreateSession();
        session.SelectImage("A");
        session.ToggleMarkupTools();
        DrawLine(session);

        Assert.False(session.ToggleMarkupTools());

        Assert.Single(session.GetRenderSnapshot("A").Elements);
        Assert.False(session.BeginDrawing(new PointD(5, 5), physicalScale: 1));
    }

    [Fact]
    public void ClearRemovesOnlyCurrentImageOverlay()
    {
        var session = CreateSession();
        session.SelectImage("A");
        session.ToggleMarkupTools();
        DrawLine(session);
        session.SelectImage("B");
        DrawLine(session);

        Assert.True(session.ClearCurrent());

        Assert.True(session.GetRenderSnapshot("B").IsEmpty);
        Assert.Single(session.GetRenderSnapshot("A").Elements);
    }

    [Fact]
    public void NewSequenceDropsAllMemoryOnlyOverlayState()
    {
        var session = CreateSession();
        session.SelectImage("A");
        session.ToggleMarkupTools();
        DrawLine(session);

        session.StartNewSequence();

        Assert.Null(session.CurrentImageIdentity);
        Assert.True(session.GetRenderSnapshot("A").IsEmpty);
    }

    [Fact]
    public void SessionDrawingNeverWritesSourceOrCreatesSidecar()
    {
        using var directory = new TemporaryDirectory();
        var source = Path.Combine(directory.Path, "photo.jpg");
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        File.WriteAllBytes(source, bytes);
        var session = CreateSession();
        session.SelectImage(source);
        session.ToggleMarkupTools();

        DrawLine(session);

        Assert.Equal(bytes, File.ReadAllBytes(source));
        Assert.Equal([source], Directory.GetFiles(directory.Path));
    }

    [Fact]
    public void DisabledMarkupPermissionKeepsPanelClosed()
    {
        var session = new PresentationOverlaySession(
            PresentationSettings.Default with { MarkupToolsEnabled = false });
        session.SelectImage("A");

        Assert.False(session.ToggleMarkupTools());
        Assert.False(session.MarkupToolsVisible);
        Assert.False(session.BeginDrawing(new PointD(1, 1), physicalScale: 1));
    }

    [Fact]
    public void CurrentOverlayAndComparisonOverlayAreSelectedByPresentationIdentity()
    {
        var session = CreateSession();
        session.SelectImage("current");
        session.ToggleMarkupTools();
        DrawLine(session);
        session.SelectImage("previous");
        DrawLine(session);
        session.SelectImage("current");

        var current = Assert.Single(session.GetRenderSnapshot("current").Elements);
        var previous = Assert.Single(session.GetRenderSnapshot("previous").Elements);

        Assert.NotSame(current, previous);
        Assert.True(session.GetRenderSnapshot("missing").IsEmpty);
        Assert.Equal("current", session.CurrentImageIdentity);
    }

    [Fact]
    public void PerImageDocumentStopsAtBoundedElementLimit()
    {
        var session = CreateSession();
        session.SelectImage("A");
        session.ToggleMarkupTools();

        for (var index = 0; index < PresentationOverlaySession.MaximumElementsPerImage + 3; index++)
        {
            session.SetActiveTool(MarkupTool.Line);
            Assert.True(session.BeginDrawing(new PointD(index, 1), physicalScale: 1));
            session.EndDrawing(new PointD(index + 1, 2));
        }

        Assert.Equal(
            PresentationOverlaySession.MaximumElementsPerImage,
            session.GetElementCount("A"));
    }

    [Fact]
    public void BrushDraftStopsAtBoundedPointLimit()
    {
        var session = CreateSession();
        session.SelectImage("A");
        session.ToggleMarkupTools();
        session.SetActiveTool(MarkupTool.Brush);
        Assert.True(session.BeginDrawing(new PointD(0, 0), physicalScale: 1));

        for (var index = 1; index < PresentationOverlaySession.MaximumBrushPoints + 20; index++)
        {
            session.ContinueDrawing(new PointD(index, index));
        }

        var brush = Assert.IsType<BrushMarkup>(session.GetRenderSnapshot("A").Draft);
        Assert.Equal(PresentationOverlaySession.MaximumBrushPoints, brush.Points.Length);
    }

    private static PresentationOverlaySession CreateSession() =>
        new(PresentationSettings.Default);

    private static void DrawLine(PresentationOverlaySession session)
    {
        session.SetActiveTool(MarkupTool.Line);
        Assert.True(session.BeginDrawing(new PointD(10, 10), physicalScale: 1));
        Assert.True(session.EndDrawing(new PointD(30, 40)));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "FoviumPresentationTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
