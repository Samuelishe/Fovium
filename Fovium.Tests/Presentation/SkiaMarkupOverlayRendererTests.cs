using Fovium.Presentation;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Tests.Presentation;

public sealed class SkiaMarkupOverlayRendererTests
{
    [Theory]
    [InlineData((int)MarkupTool.Brush, 20, 10)]
    [InlineData((int)MarkupTool.Line, 20, 10)]
    [InlineData((int)MarkupTool.Rectangle, 10, 20)]
    [InlineData((int)MarkupTool.Arrow, 20, 10)]
    public void EachPrimitiveHasAnObservableRenderPath(int toolValue, int sampleX, int sampleY)
    {
        using var surface = SKSurface.Create(new SKImageInfo(40, 40));
        surface.Canvas.Clear(SKColors.Black);
        var color = new PresentationColor(0xEE, 0x44, 0x22);
        var start = new PointD(10, 10);
        var end = new PointD(30, 30);
        MarkupElement element = (MarkupTool)toolValue switch
        {
            MarkupTool.Brush => new BrushMarkup(color, 4, [start, new PointD(30, 10)]),
            MarkupTool.Line => new LineMarkup(color, 4, start, new PointD(30, 10)),
            MarkupTool.Rectangle => new RectangleMarkup(color, 4, start, end),
            MarkupTool.Arrow => new ArrowMarkup(color, 4, start, new PointD(30, 10)),
            _ => throw new ArgumentOutOfRangeException(nameof(toolValue)),
        };

        SkiaMarkupOverlayRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 40, 40),
            new PixelSize(40, 40),
            new MarkupRenderSnapshot([element], null));
        using var image = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(image);

        Assert.True(pixels.GetPixel(sampleX, sampleY).Red > 100);
    }

    [Fact]
    public void EveryBoundedPrimitiveRendersInSelectedColorWithoutChangingOutsidePhoto()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        surface.Canvas.Clear(SKColors.Black);
        var red = new PresentationColor(0xFF, 0x20, 0x10);
        MarkupElement[] elements =
        [
            new BrushMarkup(red, 5, [new PointD(10, 10), new PointD(30, 20), new PointD(40, 30)]),
            new LineMarkup(red, 4, new PointD(10, 40), new PointD(70, 40)),
            new RectangleMarkup(red, 4, new PointD(15, 50), new PointD(45, 80)),
            new ArrowMarkup(red, 4, new PointD(50, 75), new PointD(85, 55)),
        ];

        SkiaMarkupOverlayRenderer.Draw(
            surface.Canvas,
            new RectD(10, 10, 80, 80),
            new PixelSize(100, 100),
            new MarkupRenderSnapshot(elements, null));
        using var image = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(image);

        var coloredPixels = 0;
        for (var y = 0; y < 100; y++)
        {
            for (var x = 0; x < 100; x++)
            {
                if (pixels.GetPixel(x, y).Red > 100)
                {
                    coloredPixels++;
                }
            }
        }

        Assert.True(coloredPixels > 100);
        Assert.Equal(SKColors.Black, pixels.GetPixel(2, 2));
        Assert.Equal(SKColors.Black, pixels.GetPixel(98, 98));
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
        using var pixels = SKBitmap.FromImage(image);

        Assert.Equal(SKColors.Blue, pixels.GetPixel(4, 4));
    }
}
