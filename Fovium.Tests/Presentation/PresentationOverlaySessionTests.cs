using Fovium.Presentation;
using Fovium.Rendering;

namespace Fovium.Tests.Presentation;

public sealed class PresentationOverlaySessionTests
{
    [Theory]
    [InlineData((int)MarkupTool.Brush, typeof(BrushMarkup))]
    [InlineData((int)MarkupTool.Line, typeof(LineMarkup))]
    [InlineData((int)MarkupTool.Rectangle, typeof(RectangleMarkup))]
    [InlineData((int)MarkupTool.Arrow, typeof(ArrowMarkup))]
    public void DrawToolsCommitSelectedColorAndSourceStroke(
        int toolValue,
        Type expectedType)
    {
        var session = CreateReadySession("A");
        var color = new PresentationColor(0x12, 0x34, 0x56);
        session.SetActiveTool((MarkupTool)toolValue);
        session.SetActiveColor(color);
        session.SetActiveStrokePhysicalPixels(6);

        Assert.True(session.BeginDrawing(new PointD(10, 20), physicalScale: 2));
        Assert.True(session.ContinueDrawing(new PointD(30, 40)));
        Assert.True(session.EndDrawing(new PointD(50, 60)));

        var draw = Assert.IsType<DrawMarkupOperation>(
            Assert.Single(session.GetRenderSnapshot("A").Operations));
        Assert.IsType(expectedType, draw.Element);
        Assert.Equal(color, draw.Element.Color);
        Assert.Equal(3, draw.Element.StrokeWidthSource);
    }

    [Fact]
    public void DrawOperationCanUndo()
    {
        var session = CreateReadySession("A");
        DrawLine(session);

        Assert.True(session.CanUndo);
        Assert.True(session.UndoCurrent());

        Assert.Empty(session.GetRenderSnapshot("A").Operations);
        Assert.True(session.CanRedo);
    }

    [Fact]
    public void UndoCanRedo()
    {
        var session = CreateReadySession("A");
        DrawLine(session);
        var original = Assert.Single(session.GetRenderSnapshot("A").Operations);

        session.UndoCurrent();
        Assert.True(session.RedoCurrent());

        Assert.Same(original, Assert.Single(session.GetRenderSnapshot("A").Operations));
        Assert.True(session.CanUndo);
        Assert.False(session.CanRedo);
    }

    [Fact]
    public void NewOperationAfterUndoDropsRedoTail()
    {
        var session = CreateReadySession("A");
        DrawLine(session, 10);
        DrawLine(session, 20);
        session.UndoCurrent();

        DrawLine(session, 30);

        Assert.False(session.CanRedo);
        Assert.Equal(2, session.GetActiveOperationCount("A"));
        Assert.Equal(2, session.GetRetainedOperationCount("A"));
        var lines = session.GetRenderSnapshot("A").Operations
            .OfType<DrawMarkupOperation>()
            .Select(operation => Assert.IsType<LineMarkup>(operation.Element))
            .ToArray();
        Assert.Equal(10, lines[0].Start.Y);
        Assert.Equal(30, lines[1].Start.Y);
    }

    [Fact]
    public void NewOperationAfterMultipleUndosDropsEntireRedoTail()
    {
        var session = CreateReadySession("A");
        DrawLine(session, 10);
        DrawLine(session, 20);
        DrawLine(session, 30);
        session.UndoCurrent();
        session.UndoCurrent();

        DrawLine(session, 40);

        Assert.False(session.CanRedo);
        Assert.Equal(2, session.GetRetainedOperationCount("A"));
        Assert.Equal(4, session.GetRetainedPointCount("A"));
        Assert.Equal(4, session.TotalCommittedPoints);
    }

    [Fact]
    public void ClearIsUndoable()
    {
        var session = CreateReadySession("A");
        DrawLine(session);

        Assert.True(session.ClearCurrent());
        Assert.False(session.CanClear);
        Assert.IsType<ClearMarkupOperation>(session.GetRenderSnapshot("A").Operations[^1]);

        Assert.True(session.UndoCurrent());
        Assert.True(session.CanClear);
        Assert.Single(session.GetRenderSnapshot("A").Operations);
    }

    [Fact]
    public void ClearOnEmptyDocumentDoesNotCreateMeaninglessHistory()
    {
        var session = CreateReadySession("A");

        Assert.False(session.ClearCurrent());

        Assert.True(session.GetRenderSnapshot("A").IsEmpty);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void RedoClearClearsAgain()
    {
        var session = CreateReadySession("A");
        DrawLine(session);
        session.ClearCurrent();
        session.UndoCurrent();

        Assert.True(session.RedoCurrent());

        Assert.False(session.CanClear);
        Assert.IsType<ClearMarkupOperation>(session.GetRenderSnapshot("A").Operations[^1]);
    }

    [Fact]
    public void HistoryIsPerImage()
    {
        var session = CreateReadySession("A");
        DrawLine(session, 10);
        DrawLine(session, 20);
        session.SelectImage("B");
        DrawLine(session, 30);

        Assert.True(session.UndoCurrent());
        Assert.Empty(session.GetRenderSnapshot("B").Operations);
        Assert.Equal(2, session.GetRenderSnapshot("A").Operations.Count);

        session.SelectImage("A");
        Assert.True(session.UndoCurrent());
        Assert.Single(session.GetRenderSnapshot("A").Operations);
        Assert.Empty(session.GetRenderSnapshot("B").Operations);
    }

    [Fact]
    public void NavigationRestoresEachImagesOwnHistory()
    {
        var session = CreateReadySession("A");
        DrawLine(session, 10);
        session.SelectImage("B");
        DrawLine(session, 50);
        session.UndoCurrent();
        session.SelectImage("A");

        Assert.Single(session.GetRenderSnapshot("A").Operations);
        Assert.True(session.CanUndo);

        session.SelectImage("B");
        Assert.Empty(session.GetRenderSnapshot("B").Operations);
        Assert.True(session.CanRedo);
    }

    [Fact]
    public void NewSequenceDropsAllHistory()
    {
        var session = CreateReadySession("A");
        DrawLine(session);
        session.SelectImage("B");
        DrawLine(session);

        session.StartNewSequence();

        Assert.Null(session.CurrentImageIdentity);
        Assert.True(session.GetRenderSnapshot("A").IsEmpty);
        Assert.True(session.GetRenderSnapshot("B").IsEmpty);
        Assert.Equal(0, session.TotalCommittedPoints);
        Assert.False(session.CanUndo);
        Assert.False(session.CanRedo);
    }

    [Fact]
    public void OneEraserGestureCommitsOneOperationWithSourceWidth()
    {
        var session = CreateReadySession("A");
        session.SetActiveTool(MarkupTool.Eraser);
        session.SetActiveStrokePhysicalPixels(12);

        Assert.True(session.BeginDrawing(new PointD(10, 10), physicalScale: 2));
        for (var index = 11; index < 30; index++)
        {
            session.ContinueDrawing(new PointD(index, 10));
        }

        Assert.True(session.EndDrawing(new PointD(30, 10)));

        var erase = Assert.IsType<EraseMarkupOperation>(
            Assert.Single(session.GetRenderSnapshot("A").Operations));
        Assert.Equal(6, erase.StrokeWidthSource);
        Assert.Equal(21, erase.Points.Count);
    }

    [Fact]
    public void UndoWhileDraftExistsCancelsDraftBeforeHistory()
    {
        var session = CreateReadySession("A");
        DrawLine(session);
        session.SetActiveTool(MarkupTool.Brush);
        session.BeginDrawing(new PointD(1, 1), 1);
        session.ContinueDrawing(new PointD(2, 2));

        Assert.True(session.UndoCurrent());

        Assert.False(session.IsDrawing);
        Assert.Single(session.GetRenderSnapshot("A").Operations);
        Assert.True(session.UndoCurrent());
        Assert.Empty(session.GetRenderSnapshot("A").Operations);
    }

    [Fact]
    public void HidingPanelKeepsHistoryButPreventsNewDrawing()
    {
        var session = CreateReadySession("A");
        DrawLine(session);

        Assert.False(session.ToggleMarkupTools());

        Assert.Single(session.GetRenderSnapshot("A").Operations);
        Assert.False(session.BeginDrawing(new PointD(5, 5), physicalScale: 1));
    }

    [Fact]
    public void CurrentAndComparisonHistoriesAreSelectedByPresentationIdentity()
    {
        var session = CreateReadySession("current");
        DrawLine(session, 10);
        session.SelectImage("previous");
        DrawLine(session, 30);
        session.SelectImage("current");

        var current = Assert.Single(session.GetRenderSnapshot("current").Operations);
        var previous = Assert.Single(session.GetRenderSnapshot("previous").Operations);

        Assert.NotSame(current, previous);
        Assert.True(session.GetRenderSnapshot("missing").IsEmpty);
        Assert.Equal("current", session.CurrentImageIdentity);
    }

    [Fact]
    public void ClearAffectsOnlyCurrentImageHistory()
    {
        var session = CreateReadySession("A");
        DrawLine(session);
        session.SelectImage("B");
        DrawLine(session);

        Assert.True(session.ClearCurrent());

        Assert.IsType<ClearMarkupOperation>(session.GetRenderSnapshot("B").Operations[^1]);
        Assert.Single(session.GetRenderSnapshot("A").Operations);
    }

    [Fact]
    public void SessionDrawingNeverWritesSourceOrCreatesSidecar()
    {
        using var directory = new TemporaryDirectory();
        var source = Path.Combine(directory.Path, "photo.jpg");
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        File.WriteAllBytes(source, bytes);
        var session = CreateReadySession(source);

        DrawLine(session);
        session.SetActiveTool(MarkupTool.Eraser);
        session.BeginDrawing(new PointD(10, 10), 1);
        session.EndDrawing(new PointD(20, 10));
        session.ClearCurrent();
        session.UndoCurrent();

        Assert.Equal(bytes, File.ReadAllBytes(source));
        Assert.Equal([source], Directory.GetFiles(directory.Path));
    }

    [Fact]
    public void MaximumOperationsStopsAdditionalCommitsCleanly()
    {
        var session = CreateReadySession("A", new MarkupHistoryLimits(2, 10, 20, 20));

        DrawLine(session, 10);
        DrawLine(session, 20);
        Assert.False(TryDrawLine(session, 30));

        Assert.Equal(2, session.GetRetainedOperationCount("A"));
        Assert.Equal(4, session.GetRetainedPointCount("A"));
    }

    [Theory]
    [InlineData((int)MarkupTool.Brush)]
    [InlineData((int)MarkupTool.Eraser)]
    public void FreehandDraftStopsAtMaximumPointsPerStroke(int toolValue)
    {
        var session = CreateReadySession("A", new MarkupHistoryLimits(10, 5, 20, 20));
        session.SetActiveTool((MarkupTool)toolValue);
        session.BeginDrawing(new PointD(0, 0), 1);
        for (var index = 1; index < 20; index++)
        {
            session.ContinueDrawing(new PointD(index, index));
        }

        var draft = session.GetRenderSnapshot("A").Draft;
        var points = draft switch
        {
            DrawMarkupOperation { Element: BrushMarkup brush } => brush.Points,
            EraseMarkupOperation erase => erase.Points,
            _ => throw new Xunit.Sdk.XunitException("Expected a freehand draft."),
        };

        Assert.Equal(5, points.Count);
    }

    [Fact]
    public void PerImageTotalPointLimitRejectsOnlyAdditionalOperation()
    {
        var session = CreateReadySession("A", new MarkupHistoryLimits(10, 10, 4, 20));
        DrawLine(session, 10);
        DrawLine(session, 20);

        Assert.False(TryDrawLine(session, 30));

        Assert.Equal(2, session.GetRetainedOperationCount("A"));
        Assert.Equal(4, session.GetRetainedPointCount("A"));
    }

    [Fact]
    public void SessionPointLimitRejectsOtherImageWithoutCorruptingExistingHistory()
    {
        var session = CreateReadySession("A", new MarkupHistoryLimits(10, 10, 10, 4));
        DrawLine(session, 10);
        session.SelectImage("B");
        DrawLine(session, 20);

        Assert.False(TryDrawLine(session, 30));

        Assert.Equal(2, session.GetRetainedPointCount("A"));
        Assert.Equal(2, session.GetRetainedPointCount("B"));
        Assert.Equal(4, session.TotalCommittedPoints);
    }

    [Fact]
    public void RedoTailTruncationReleasesPointAccounting()
    {
        var session = CreateReadySession("A", new MarkupHistoryLimits(10, 10, 4, 4));
        DrawLine(session, 10);
        DrawLine(session, 20);
        session.UndoCurrent();

        Assert.True(TryDrawLine(session, 30));

        Assert.False(session.CanRedo);
        Assert.Equal(4, session.GetRetainedPointCount("A"));
        Assert.Equal(4, session.TotalCommittedPoints);
    }

    [Fact]
    public void ClearAddsNoGeometryAndUndoRedoDoNotGrowRetainedState()
    {
        var session = CreateReadySession("A");
        DrawLine(session);
        var pointsBefore = session.TotalCommittedPoints;
        session.ClearCurrent();

        for (var cycle = 0; cycle < 100; cycle++)
        {
            Assert.True(session.UndoCurrent());
            Assert.True(session.RedoCurrent());
        }

        Assert.Equal(pointsBefore, session.TotalCommittedPoints);
        Assert.Equal(2, session.GetRetainedOperationCount("A"));
    }

    [Fact]
    public void LongDraftSnapshotsShareCompletedChunksInsteadOfCopyingWholeStroke()
    {
        var session = CreateReadySession("A");
        session.SetActiveTool(MarkupTool.Brush);
        session.BeginDrawing(new PointD(0, 0), 1);
        for (var index = 1; index <= 80; index++)
        {
            session.ContinueDrawing(new PointD(index, index));
        }

        var first = Assert.IsType<BrushMarkup>(
            Assert.IsType<DrawMarkupOperation>(session.GetRenderSnapshot("A").Draft).Element).Points;
        for (var index = 81; index <= 120; index++)
        {
            session.ContinueDrawing(new PointD(index, index));
        }

        var second = Assert.IsType<BrushMarkup>(
            Assert.IsType<DrawMarkupOperation>(session.GetRenderSnapshot("A").Draft).Element).Points;

        Assert.True(first.SharesCompletedStorageWith(second));
        Assert.Equal(81, first.Count);
        Assert.Equal(121, second.Count);
    }

    [Fact]
    public void HighlightCanBeRepeatedlyToggledWithoutChangingHistory()
    {
        var session = CreateReadySession("A");
        DrawLine(session);

        for (var cycle = 0; cycle < 100; cycle++)
        {
            Assert.True(session.ToggleHighlight());
            Assert.False(session.ToggleHighlight());
        }

        Assert.False(session.HighlightEnabled);
        Assert.Single(session.GetRenderSnapshot("A").Operations);
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
    public void InvalidHistoryLimitsAreRejected()
    {
        var invalid = new MarkupHistoryLimits(0, 1, 1, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PresentationOverlaySession(PresentationSettings.Default, limits: invalid));
    }

    private static PresentationOverlaySession CreateReadySession(
        string identity,
        MarkupHistoryLimits? limits = null)
    {
        var session = new PresentationOverlaySession(
            PresentationSettings.Default,
            limits: limits);
        session.SelectImage(identity);
        Assert.True(session.ToggleMarkupTools());
        return session;
    }

    private static void DrawLine(PresentationOverlaySession session, double y = 10) =>
        Assert.True(TryDrawLine(session, y));

    private static bool TryDrawLine(PresentationOverlaySession session, double y)
    {
        session.SetActiveTool(MarkupTool.Line);
        Assert.True(session.BeginDrawing(new PointD(10, y), physicalScale: 1));
        return session.EndDrawing(new PointD(30, y));
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
