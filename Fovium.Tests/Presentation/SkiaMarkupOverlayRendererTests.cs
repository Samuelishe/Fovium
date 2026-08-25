using Fovium.Presentation;
using Fovium.Rendering;
using SkiaSharp;
using Xunit.Abstractions;

namespace Fovium.Tests.Presentation;

public sealed class SkiaMarkupOverlayRendererTests(ITestOutputHelper output)
{
    private static readonly PresentationColor Red = new(0xFF, 0x10, 0x10);
    private static readonly PresentationColor Blue = new(0x10, 0x40, 0xFF);
    private static readonly SKColor PhotoColor = new(0x17, 0x45, 0x73);

    [Fact]
    public void LinePartialEraseLeavesBothSegmentsAndRevealsPhoto()
    {
        var bitmap = Render(
            Draw(new LineMarkup(Red, 8, new PointD(10, 40), new PointD(110, 40))),
            Erase(14, new PointD(60, 15), new PointD(60, 65)));

        AssertMarkupRed(bitmap, 25, 40);
        Assert.Equal(PhotoColor, bitmap.GetPixel(60, 40));
        AssertMarkupRed(bitmap, 95, 40);
    }

    [Fact]
    public void RectanglePartialEraseRemovesOnlyCrossedEdgeSection()
    {
        var bitmap = Render(
            Draw(new RectangleMarkup(Red, 7, new PointD(20, 20), new PointD(100, 70))),
            Erase(14, new PointD(60, 8), new PointD(60, 34)));

        AssertMarkupRed(bitmap, 35, 20);
        Assert.Equal(PhotoColor, bitmap.GetPixel(60, 20));
        AssertMarkupRed(bitmap, 85, 20);
        AssertMarkupRed(bitmap, 60, 70);
        AssertMarkupRed(bitmap, 20, 50);
        AssertMarkupRed(bitmap, 100, 50);
    }

    [Fact]
    public void ArrowPartialEraseRemovesOnlyCrossedShaftPortion()
    {
        var bitmap = Render(
            Draw(new ArrowMarkup(Red, 7, new PointD(15, 40), new PointD(105, 40))),
            Erase(14, new PointD(58, 18), new PointD(58, 62)));

        AssertMarkupRed(bitmap, 30, 40);
        Assert.Equal(PhotoColor, bitmap.GetPixel(58, 40));
        AssertMarkupRed(bitmap, 82, 40);
        AssertMarkupRed(bitmap, 104, 40);
    }

    [Fact]
    public void BrushPartialEraseLeavesUncrossedStrokeRegions()
    {
        var bitmap = Render(
            Draw(new BrushMarkup(
                Red,
                9,
                MarkupStrokePoints.From(
                    new PointD(10, 40),
                    new PointD(35, 32),
                    new PointD(60, 40),
                    new PointD(85, 48),
                    new PointD(110, 40)))),
            Erase(16, new PointD(60, 15), new PointD(60, 65)));

        AssertMarkupRed(bitmap, 25, 35);
        Assert.Equal(PhotoColor, bitmap.GetPixel(60, 40));
        AssertMarkupRed(bitmap, 95, 45);
    }

    [Fact]
    public void SinglePointEraserClearsOneRoundSpot()
    {
        var bitmap = Render(
            Draw(new LineMarkup(Red, 8, new PointD(10, 40), new PointD(110, 40))),
            Erase(16, new PointD(60, 40)));

        AssertMarkupRed(bitmap, 35, 40);
        Assert.Equal(PhotoColor, bitmap.GetPixel(60, 40));
        AssertMarkupRed(bitmap, 85, 40);
    }

    [Fact]
    public void EraserCannotDamagePhotoStageOrMarkupClip()
    {
        using var surface = SKSurface.Create(new SKImageInfo(140, 100));
        surface.Canvas.Clear(SKColors.Green);
        using (var photoPaint = new SKPaint { Color = PhotoColor })
        {
            surface.Canvas.DrawRect(new SKRect(20, 10, 120, 90), photoPaint);
        }

        var operations = new MarkupOperation[]
        {
            Draw(new LineMarkup(Red, 8, new PointD(0, 40), new PointD(100, 40))),
            Erase(18, new PointD(50, -30), new PointD(50, 130)),
        };
        SkiaMarkupOverlayRenderer.Draw(
            surface.Canvas,
            new RectD(20, 10, 100, 80),
            new PixelSize(100, 80),
            new MarkupRenderSnapshot(operations, null),
            new RectD(0, 0, 140, 100));
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);

        Assert.Equal(PhotoColor, bitmap.GetPixel(70, 50));
        Assert.Equal(SKColors.Green, bitmap.GetPixel(70, 5));
        Assert.Equal(SKColors.Green, bitmap.GetPixel(70, 95));
        Assert.Equal(SKColors.Green, bitmap.GetPixel(5, 50));
    }

    [Fact]
    public void ChronologicalDrawEraseDrawAndUndoSnapshotsComposeCorrectly()
    {
        var first = Draw(new LineMarkup(Red, 8, new PointD(10, 40), new PointD(110, 40)));
        var erase = Erase(14, new PointD(60, 15), new PointD(60, 65));
        var green = new PresentationColor(0x10, 0xFF, 0x20);
        var later = Draw(new LineMarkup(green, 6, new PointD(60, 25), new PointD(60, 55)));

        var all = Render(first, erase, later);
        Assert.True(all.GetPixel(60, 40).Green > 180);

        var undoDraw = Render(first, erase);
        Assert.Equal(PhotoColor, undoDraw.GetPixel(60, 40));

        var undoErase = Render(first);
        AssertMarkupRed(undoErase, 60, 40);
    }

    [Fact]
    public void DraftEraserIsVisibleThenCancelRestoresAndCommitPersists()
    {
        var session = new PresentationOverlaySession(PresentationSettings.Default);
        session.SelectImage("A");
        session.ToggleMarkupTools();
        session.SetActiveColor(Red);
        session.SetActiveStrokePhysicalPixels(8);
        session.SetActiveTool(MarkupTool.Line);
        session.BeginDrawing(new PointD(10, 40), 1);
        session.EndDrawing(new PointD(110, 40));
        session.SetActiveTool(MarkupTool.Eraser);
        session.SetActiveStrokePhysicalPixels(14);
        session.BeginDrawing(new PointD(60, 15), 1);
        session.ContinueDrawing(new PointD(60, 65));

        using (var draft = Render(session.GetRenderSnapshot("A")))
        {
            Assert.Equal(PhotoColor, draft.GetPixel(60, 40));
        }

        session.CancelDrawing();
        using (var canceled = Render(session.GetRenderSnapshot("A")))
        {
            AssertMarkupRed(canceled, 60, 40);
        }

        session.BeginDrawing(new PointD(60, 15), 1);
        session.EndDrawing(new PointD(60, 65));
        using var committed = Render(session.GetRenderSnapshot("A"));
        Assert.Equal(PhotoColor, committed.GetPixel(60, 40));
        Assert.Equal(2, session.GetActiveOperationCount("A"));
    }

    [Fact]
    public void ClearOperationIsLayerLocalAndLaterDrawRemainsVisible()
    {
        var later = new PresentationColor(0x20, 0xE0, 0x40);
        var bitmap = Render(
            Draw(new LineMarkup(Red, 8, new PointD(10, 40), new PointD(110, 40))),
            ClearMarkupOperation.Instance,
            Draw(new LineMarkup(later, 6, new PointD(60, 20), new PointD(60, 60))));

        Assert.Equal(PhotoColor, bitmap.GetPixel(25, 40));
        Assert.True(bitmap.GetPixel(60, 40).Green > 150);
        Assert.Equal(PhotoColor, bitmap.GetPixel(95, 40));
    }

    [Fact]
    public void EllipseRendersExpectedOutlineWithoutFill()
    {
        using var bitmap = Render(Draw(new EllipseMarkup(
            Red,
            8,
            new PointD(20, 15),
            new PointD(100, 65))));

        AssertMarkupRed(bitmap, 60, 15);
        AssertMarkupRed(bitmap, 20, 40);
        AssertMarkupRed(bitmap, 100, 40);
        AssertMarkupRed(bitmap, 60, 65);
        Assert.Equal(PhotoColor, bitmap.GetPixel(60, 40));
    }

    [Fact]
    public void EllipsePartialEraseRemovesOnlyCrossedArcAndRevealsPhoto()
    {
        using var bitmap = Render(
            Draw(new EllipseMarkup(Red, 8, new PointD(20, 15), new PointD(100, 65))),
            Erase(14, new PointD(60, 4), new PointD(60, 27)));

        Assert.Equal(PhotoColor, bitmap.GetPixel(60, 15));
        AssertMarkupRed(bitmap, 20, 40);
        AssertMarkupRed(bitmap, 100, 40);
        AssertMarkupRed(bitmap, 60, 65);
    }

    [Fact]
    public void SemiTransparentEllipseDoesNotBleedOutsidePhoto()
    {
        using var surface = SKSurface.Create(new SKImageInfo(140, 100));
        surface.Canvas.Clear(SKColors.Green);
        using (var photoPaint = new SKPaint { Color = PhotoColor })
        {
            surface.Canvas.DrawRect(new SKRect(20, 10, 120, 90), photoPaint);
        }

        SkiaMarkupOverlayRenderer.Draw(
            surface.Canvas,
            new RectD(20, 10, 100, 80),
            new PixelSize(100, 80),
            new MarkupRenderSnapshot(
                [Draw(new EllipseMarkup(Red, 12, new PointD(0, 0), new PointD(100, 80), 0.5))],
                null),
            new RectD(0, 0, 140, 100));
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);

        Assert.Equal(SKColors.Green, bitmap.GetPixel(18, 50));
        Assert.Equal(SKColors.Green, bitmap.GetPixel(122, 50));
        AssertComposite(bitmap.GetPixel(20, 50), PhotoColor, Red, 0.5);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.5)]
    [InlineData(0.1)]
    public void StoredOpacityUsesSourceOverAlpha(double opacity)
    {
        using var bitmap = Render(Draw(new LineMarkup(
            Red,
            10,
            new PointD(10, 40),
            new PointD(110, 40),
            opacity)));

        AssertComposite(bitmap.GetPixel(60, 40), PhotoColor, Red, opacity);
    }

    [Fact]
    public void EraserFullyClearsTranslucentMarkupWithoutResidualAlpha()
    {
        using var bitmap = Render(
            Draw(new LineMarkup(Red, 10, new PointD(10, 40), new PointD(110, 40), 0.30)),
            Erase(16, new PointD(60, 15), new PointD(60, 65)));

        AssertComposite(bitmap.GetPixel(30, 40), PhotoColor, Red, 0.30);
        Assert.Equal(PhotoColor, bitmap.GetPixel(60, 40));
        AssertComposite(bitmap.GetPixel(90, 40), PhotoColor, Red, 0.30);
    }

    [Fact]
    public void ChronologicalOpacitySurvivesEraseAndUndoSnapshots()
    {
        var red = Draw(new LineMarkup(Red, 10, new PointD(10, 40), new PointD(110, 40), 0.5));
        var erase = Erase(16, new PointD(60, 15), new PointD(60, 65));
        var blue = Draw(new LineMarkup(Blue, 8, new PointD(60, 20), new PointD(60, 60), 0.5));

        using (var all = Render(red, erase, blue))
        {
            AssertComposite(all.GetPixel(60, 40), PhotoColor, Blue, 0.5);
        }

        using (var undoBlue = Render(red, erase))
        {
            Assert.Equal(PhotoColor, undoBlue.GetPixel(60, 40));
        }

        using var undoErase = Render(red);
        AssertComposite(undoErase.GetPixel(60, 40), PhotoColor, Red, 0.5);
    }

    [Theory]
    [InlineData(80, 5, 6)]
    [InlineData(80, 64, 6)]
    [InlineData(3, 64, 6)]
    [InlineData(110, 32, 8)]
    public void ArrowRenderingDoesNotThrowForShortLongThickOrScaledStrokes(
        double length,
        double strokeWidth,
        double destinationScale)
    {
        using var surface = SKSurface.Create(new SKImageInfo(800, 200));
        var operation = Draw(new ArrowMarkup(
            Red,
            strokeWidth,
            new PointD(2, 10),
            new PointD(2 + length, 10)));

        var exception = Record.Exception(() => SkiaMarkupOverlayRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 120 * destinationScale, 20 * destinationScale),
            new PixelSize(120, 20),
            new MarkupRenderSnapshot([operation], null),
            new RectD(0, 0, 800, 200)));

        Assert.Null(exception);
    }

    [Fact]
    public void EmptySnapshotDoesNotMutateCanvas()
    {
        using var surface = SKSurface.Create(new SKImageInfo(8, 8));
        surface.Canvas.Clear(SKColors.Blue);

        SkiaMarkupOverlayRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 8, 8),
            new PixelSize(8, 8),
            MarkupRenderSnapshot.Empty);
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);

        Assert.Equal(SKColors.Blue, bitmap.GetPixel(4, 4));
    }

    [Fact]
    public void RenderingCostObservationCoversEmptyModestAndManyOperationPaths()
    {
        using var surface = SKSurface.Create(new SKImageInfo(320, 240));
        var modest = Enumerable.Range(0, 8)
            .Select(index => (MarkupOperation)Draw(index % 2 == 0
                ? new LineMarkup(
                    Red,
                    4,
                    new PointD(10, 20 + index * 10),
                    new PointD(300, 20 + index * 10),
                    0.64)
                : new EllipseMarkup(
                    Red,
                    4,
                    new PointD(20 + index * 10, 20),
                    new PointD(80 + index * 10, 80),
                    0.64)))
            .ToArray();
        var many = Enumerable.Range(0, 512)
            .Select(index => (MarkupOperation)Draw(index % 2 == 0
                ? new LineMarkup(
                    Red,
                    3,
                    new PointD(5, index % 220 + 10),
                    new PointD(315, index % 220 + 10),
                    0.64)
                : new EllipseMarkup(
                    Red,
                    3,
                    new PointD(index % 260 + 5, index % 180 + 5),
                    new PointD(index % 260 + 45, index % 180 + 35),
                    0.64)))
            .ToArray();

        var emptyElapsed = Measure(surface.Canvas, MarkupRenderSnapshot.Empty, 200);
        var modestElapsed = Measure(surface.Canvas, new MarkupRenderSnapshot(modest, null), 200);
        var manyElapsed = Measure(surface.Canvas, new MarkupRenderSnapshot(many, null), 20);

        Assert.True(emptyElapsed > TimeSpan.Zero);
        Assert.True(modestElapsed > TimeSpan.Zero);
        Assert.True(manyElapsed > TimeSpan.Zero);
        output.WriteLine(
            "Markup render observation: empty {0:F3} us/draw; modest(8) {1:F3} us/draw; many(512) {2:F3} us/draw.",
            emptyElapsed.TotalMicroseconds / 200,
            modestElapsed.TotalMicroseconds / 200,
            manyElapsed.TotalMicroseconds / 20);
    }

    private static DrawMarkupOperation Draw(MarkupElement element) => new(element);

    private static EraseMarkupOperation Erase(double width, params PointD[] points) =>
        new(width, MarkupStrokePoints.From(points));

    private static SKBitmap Render(params MarkupOperation[] operations) =>
        Render(new MarkupRenderSnapshot(operations, null));

    private static SKBitmap Render(MarkupRenderSnapshot snapshot)
    {
        using var surface = SKSurface.Create(new SKImageInfo(120, 80));
        surface.Canvas.Clear(PhotoColor);
        SkiaMarkupOverlayRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 120, 80),
            new PixelSize(120, 80),
            snapshot,
            new RectD(0, 0, 120, 80));
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    private static TimeSpan Measure(SKCanvas canvas, MarkupRenderSnapshot snapshot, int iterations)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            SkiaMarkupOverlayRenderer.Draw(
                canvas,
                new RectD(0, 0, 320, 240),
                new PixelSize(320, 240),
                snapshot,
                new RectD(0, 0, 320, 240));
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private static void AssertMarkupRed(SKBitmap bitmap, int x, int y)
    {
        var pixel = bitmap.GetPixel(x, y);
        Assert.True(pixel.Red > 180, $"Expected red markup at ({x}, {y}), got {pixel}.");
        Assert.True(pixel.Red > pixel.Blue, $"Expected red-dominant markup at ({x}, {y}), got {pixel}.");
    }

    private static void AssertComposite(
        SKColor actual,
        SKColor background,
        PresentationColor foreground,
        double opacity)
    {
        var alpha = Math.Round(opacity * byte.MaxValue, MidpointRounding.AwayFromZero) / byte.MaxValue;
        var expectedRed = foreground.Red * alpha + background.Red * (1 - alpha);
        var expectedGreen = foreground.Green * alpha + background.Green * (1 - alpha);
        var expectedBlue = foreground.Blue * alpha + background.Blue * (1 - alpha);
        Assert.InRange(actual.Red, Math.Floor(expectedRed) - 2, Math.Ceiling(expectedRed) + 2);
        Assert.InRange(actual.Green, Math.Floor(expectedGreen) - 2, Math.Ceiling(expectedGreen) + 2);
        Assert.InRange(actual.Blue, Math.Floor(expectedBlue) - 2, Math.Ceiling(expectedBlue) + 2);
        Assert.Equal(byte.MaxValue, actual.Alpha);
    }
}
