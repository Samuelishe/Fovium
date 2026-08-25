using Fovium.Presentation;
using Fovium.Rendering;

namespace Fovium.Tests.Presentation;

public sealed class PresentationOverlaySessionTests
{
    [Theory]
    [InlineData((int)MarkupTool.Brush, typeof(BrushMarkup))]
    [InlineData((int)MarkupTool.Line, typeof(LineMarkup))]
    [InlineData((int)MarkupTool.Rectangle, typeof(RectangleMarkup))]
    [InlineData((int)MarkupTool.Ellipse, typeof(EllipseMarkup))]
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
        session.SetActiveOpacity(0.65);

        Assert.True(session.BeginDrawing(new PointD(10, 20), physicalScale: 2));
        Assert.True(session.ContinueDrawing(new PointD(30, 40)));
        Assert.True(session.EndDrawing(new PointD(50, 60)));

        var draw = Assert.IsType<DrawMarkupOperation>(
            Assert.Single(session.GetRenderSnapshot("A").Operations));
        Assert.IsType(expectedType, draw.Element);
        Assert.Equal(color, draw.Element.Color);
        Assert.Equal(3, draw.Element.StrokeWidthSource);
        Assert.Equal(0.65, draw.Element.Opacity);
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

    [Fact]
    public void EllipseCreatesImageBoundOperationAndParticipatesInUndoRedo()
    {
        var session = CreateReadySession("A");
        session.SetActiveTool(MarkupTool.Ellipse);

        Assert.True(session.BeginDrawing(new PointD(10, 15), 1));
        Assert.True(session.EndDrawing(new PointD(70, 55)));

        var draw = Assert.IsType<DrawMarkupOperation>(
            Assert.Single(session.GetRenderSnapshot("A").Operations));
        var ellipse = Assert.IsType<EllipseMarkup>(draw.Element);
        Assert.Equal(new PointD(10, 15), ellipse.Start);
        Assert.Equal(new PointD(70, 55), ellipse.End);
        Assert.Equal(2, session.GetRetainedPointCount("A"));

        Assert.True(session.UndoCurrent());
        Assert.Empty(session.GetRenderSnapshot("A").Operations);
        Assert.True(session.RedoCurrent());
        Assert.Same(draw, Assert.Single(session.GetRenderSnapshot("A").Operations));
    }

    [Fact]
    public void DrawOpacityIsCapturedAndUndoRedoPreservesOriginalValues()
    {
        var session = CreateReadySession("A");
        session.SetActiveOpacity(1);
        DrawLine(session, 10);
        session.SetActiveOpacity(0.30);
        DrawLine(session, 30);
        session.SetActiveOpacity(0.75);

        var elements = session.GetRenderSnapshot("A").Operations
            .Cast<DrawMarkupOperation>()
            .Select(operation => operation.Element)
            .ToArray();
        Assert.Equal(1, elements[0].Opacity);
        Assert.Equal(0.30, elements[1].Opacity);

        Assert.True(session.UndoCurrent());
        Assert.True(session.RedoCurrent());
        elements = session.GetRenderSnapshot("A").Operations
            .Cast<DrawMarkupOperation>()
            .Select(operation => operation.Element)
            .ToArray();
        Assert.Equal([1, 0.30], elements.Select(element => element.Opacity));
    }

    [Fact]
    public void GestureCapturesColorOpacityAndStrokeAtBegin()
    {
        var session = CreateReadySession("A");
        var capturedColor = new PresentationColor(10, 20, 30);
        session.SetActiveTool(MarkupTool.Line);
        session.SetActiveColor(capturedColor);
        session.SetActiveStrokePhysicalPixels(8);
        session.SetActiveOpacity(0.60);
        session.BeginDrawing(new PointD(10, 10), physicalScale: 2);

        session.SetActiveColor(new PresentationColor(90, 80, 70));
        session.SetActiveStrokePhysicalPixels(20);
        session.SetActiveOpacity(0.20);
        session.EndDrawing(new PointD(40, 10));

        var element = Assert.IsType<LineMarkup>(
            Assert.IsType<DrawMarkupOperation>(
                Assert.Single(session.GetRenderSnapshot("A").Operations)).Element);
        Assert.Equal(capturedColor, element.Color);
        Assert.Equal(4, element.StrokeWidthSource);
        Assert.Equal(0.60, element.Opacity);
    }

    [Fact]
    public void UnrelatedPresentationSettingsDoNotResetActiveDrawingStyle()
    {
        var session = CreateReadySession("A");
        var activeColor = new PresentationColor(1, 2, 3);
        session.SetActiveColor(activeColor);
        session.SetActiveStrokePhysicalPixels(17);
        session.SetActiveOpacity(0.45);

        session.ApplySettings(PresentationSettings.Default with
        {
            HighlightRadiusPhysicalPixels = 100,
            HighlightColor = new PresentationColor(9, 8, 7),
        });

        Assert.Equal(activeColor, session.ActiveColor);
        Assert.Equal(17, session.ActiveStrokePhysicalPixels);
        Assert.Equal(0.45, session.ActiveOpacity);
    }

    [Fact]
    public void DeliberateDefaultStyleChangesUpdateOnlyTheirActiveValues()
    {
        var session = CreateReadySession("A");
        var color = new PresentationColor(10, 20, 30);

        session.ApplySettings(PresentationSettings.Default with
        {
            DefaultMarkupColor = color,
            DefaultMarkupStrokePhysicalPixels = 12,
            DefaultMarkupOpacity = 0.55,
        });

        Assert.Equal(color, session.ActiveColor);
        Assert.Equal(12, session.ActiveStrokePhysicalPixels);
        Assert.Equal(0.55, session.ActiveOpacity);
    }

    [Theory]
    [InlineData((int)MarkupTool.Line)]
    [InlineData((int)MarkupTool.Arrow)]
    public void ShiftConstrainsLineAndArrowToNearest45Degrees(int toolValue)
    {
        var session = CreateReadySession("A");
        session.SetActiveTool((MarkupTool)toolValue);
        var start = new PointD(20, 20);

        session.BeginDrawing(start, 1, new PixelSize(100, 100));
        session.ContinueDrawing(new PointD(50, 30), MarkupDrawingModifiers.Constrain);
        var preview = Assert.IsType<DrawMarkupOperation>(session.GetRenderSnapshot("A").Draft);
        var previewEnd = preview.Element switch
        {
            LineMarkup line => line.End,
            ArrowMarkup arrow => arrow.End,
            _ => throw new Xunit.Sdk.XunitException("Expected constrained line-like draft."),
        };

        Assert.Equal(20, previewEnd.Y, 8);
        Assert.Equal(20 + Math.Sqrt(1000), previewEnd.X, 8);
        Assert.True(session.EndDrawing(
            new PointD(50, 30),
            MarkupDrawingModifiers.Constrain));
        Assert.Single(session.GetRenderSnapshot("A").Operations);
    }

    [Theory]
    [InlineData((int)MarkupTool.Rectangle)]
    [InlineData((int)MarkupTool.Ellipse)]
    public void ShiftConstrainsRectangleAndEllipseToEqualBounds(int toolValue)
    {
        var session = CreateReadySession("A");
        session.SetActiveTool((MarkupTool)toolValue);
        var start = new PointD(20, 20);

        session.BeginDrawing(start, 1, new PixelSize(100, 100));
        session.EndDrawing(new PointD(50, 30), MarkupDrawingModifiers.Constrain);

        var element = Assert.IsType<DrawMarkupOperation>(
            Assert.Single(session.GetRenderSnapshot("A").Operations)).Element;
        var end = element switch
        {
            RectangleMarkup rectangle => rectangle.End,
            EllipseMarkup ellipse => ellipse.End,
            _ => throw new Xunit.Sdk.XunitException("Expected constrained bounded shape."),
        };
        Assert.Equal(new PointD(50, 50), end);
    }

    [Fact]
    public void ConstrainedShapeClipsAlongRayAndRetainsSquareAtSourceBoundary()
    {
        var session = CreateReadySession("A");
        session.SetActiveTool(MarkupTool.Ellipse);
        session.BeginDrawing(new PointD(80, 70), 1, new PixelSize(100, 80));

        session.EndDrawing(new PointD(100, 20), MarkupDrawingModifiers.Constrain);

        var ellipse = Assert.IsType<EllipseMarkup>(
            Assert.IsType<DrawMarkupOperation>(
                Assert.Single(session.GetRenderSnapshot("A").Operations)).Element);
        Assert.Equal(new PointD(100, 50), ellipse.End);
        Assert.Equal(
            Math.Abs(ellipse.End.X - ellipse.Start.X),
            Math.Abs(ellipse.End.Y - ellipse.Start.Y));
    }

    [Fact]
    public void ConstrainedBrushPreviewAndCommitUseTwoPointSnappedBrush()
    {
        var session = CreateReadySession("A");
        session.SetActiveTool(MarkupTool.Brush);
        session.BeginDrawing(new PointD(10, 10), 1);
        session.ContinueDrawing(new PointD(18, 13));
        session.ContinueDrawing(new PointD(30, 30), MarkupDrawingModifiers.Constrain);

        var preview = Assert.IsType<BrushMarkup>(
            Assert.IsType<DrawMarkupOperation>(session.GetRenderSnapshot("A").Draft).Element);
        Assert.Equal(2, preview.Points.Count);
        Assert.Equal(new PointD(10, 10), preview.Points[0]);
        Assert.Equal(30, preview.Points[1].X, 8);
        Assert.Equal(30, preview.Points[1].Y, 8);

        Assert.True(session.EndDrawing(
            new PointD(30, 30),
            MarkupDrawingModifiers.Constrain));
        var committed = Assert.IsType<BrushMarkup>(
            Assert.IsType<DrawMarkupOperation>(
                Assert.Single(session.GetRenderSnapshot("A").Operations)).Element);
        Assert.Equal(2, committed.Points.Count);
        Assert.Equal(1, session.GetActiveOperationCount("A"));
    }

    [Fact]
    public void ReleasingShiftRestoresCollectedFreehandBrushDraftBeforeCommit()
    {
        var session = CreateReadySession("A");
        session.SetActiveTool(MarkupTool.Brush);
        session.BeginDrawing(new PointD(10, 10), 1);
        session.ContinueDrawing(new PointD(14, 12));
        session.ContinueDrawing(new PointD(20, 20), MarkupDrawingModifiers.Constrain);
        session.ContinueDrawing(new PointD(23, 18), MarkupDrawingModifiers.None);

        var preview = Assert.IsType<BrushMarkup>(
            Assert.IsType<DrawMarkupOperation>(session.GetRenderSnapshot("A").Draft).Element);
        Assert.Equal(4, preview.Points.Count);
        Assert.Equal(new PointD(20, 20), preview.Points[2]);

        Assert.True(session.EndDrawing(new PointD(28, 21), MarkupDrawingModifiers.None));
        var committed = Assert.IsType<BrushMarkup>(
            Assert.IsType<DrawMarkupOperation>(
                Assert.Single(session.GetRenderSnapshot("A").Operations)).Element);
        Assert.Equal(5, committed.Points.Count);
        Assert.Equal(new PointD(28, 21), committed.Points[^1]);
    }

    [Fact]
    public void EraserIgnoresShiftAndRemainsFreehand()
    {
        var session = CreateReadySession("A");
        session.SetActiveTool(MarkupTool.Eraser);
        session.BeginDrawing(new PointD(10, 10), 1);
        session.ContinueDrawing(new PointD(14, 18), MarkupDrawingModifiers.Constrain);
        session.EndDrawing(new PointD(20, 15), MarkupDrawingModifiers.Constrain);

        var erase = Assert.IsType<EraseMarkupOperation>(
            Assert.Single(session.GetRenderSnapshot("A").Operations));
        Assert.Equal(3, erase.Points.Count);
        Assert.Equal(new PointD(14, 18), erase.Points[1]);
        Assert.Equal(new PointD(20, 15), erase.Points[2]);
    }

    [Fact]
    public void MarkupStyleCommandsClampAndDoNothingWhileDockIsHidden()
    {
        var session = new PresentationOverlaySession(PresentationSettings.Default);
        session.SelectImage("A");

        Assert.False(session.AdjustActiveStrokePhysicalPixels(1));
        Assert.False(session.AdjustActiveOpacity(-0.05));
        Assert.Equal(4, session.Settings.DefaultMarkupStrokePhysicalPixels);
        Assert.Equal(1, session.Settings.DefaultMarkupOpacity);
        Assert.Equal(4, session.ActiveStrokePhysicalPixels);
        Assert.Equal(1, session.ActiveOpacity);

        Assert.True(session.ToggleMarkupTools());
        Assert.True(session.AdjustActiveStrokePhysicalPixels(1));
        Assert.True(session.AdjustActiveOpacity(-0.05));
        Assert.Equal(5, session.ActiveStrokePhysicalPixels);
        Assert.Equal(0.95, session.ActiveOpacity, 8);

        session.SetActiveStrokePhysicalPixels(PresentationSettings.MaximumMarkupStrokePhysicalPixels);
        session.SetActiveOpacity(PresentationSettings.MinimumMarkupOpacity);
        Assert.False(session.AdjustActiveStrokePhysicalPixels(1));
        Assert.False(session.AdjustActiveOpacity(-0.05));
    }

    [Fact]
    public void ClearCommandRequiresVisibleMarkupToolsButDelegatesToUndoableClear()
    {
        var session = CreateReadySession("A");
        DrawLine(session);
        Assert.False(session.ToggleMarkupTools());

        Assert.False(session.ClearCurrentFromCommand());
        Assert.Single(session.GetRenderSnapshot("A").Operations);

        Assert.True(session.ToggleMarkupTools());
        Assert.True(session.ClearCurrentFromCommand());
        Assert.IsType<ClearMarkupOperation>(session.GetRenderSnapshot("A").Operations[^1]);
        Assert.True(session.UndoCurrent());
        Assert.Single(session.GetRenderSnapshot("A").Operations);
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
