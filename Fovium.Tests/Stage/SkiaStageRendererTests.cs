using Fovium.Rendering;
using Fovium.Stage;
using SkiaSharp;

namespace Fovium.Tests.Stage;

public sealed class SkiaStageRendererTests
{
    [Fact]
    public void AmbientMattePlacesMatteBehindTransparentPhotoBounds()
    {
        using var surface = SKSurface.Create(new SKImageInfo(
            100,
            100,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        using var ambientBitmap = new SKBitmap(new SKImageInfo(10, 10));
        ambientBitmap.Erase(SKColors.Blue);
        using var ambient = SKImage.FromBitmap(ambientBitmap);
        var photoDestination = new RectD(25, 25, 50, 50);
        SkiaStageRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 100, 100),
            photoDestination,
            1,
            StageMode.AmbientMatte,
            ambient,
            new PixelSize(10, 10));

        using var transparentBitmap = new SKBitmap(new SKImageInfo(
            50,
            50,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        transparentBitmap.Erase(SKColors.Transparent);
        using var transparentPhoto = SKImage.FromBitmap(transparentBitmap);
        surface.Canvas.DrawImage(transparentPhoto, new SKRect(25, 25, 75, 75));
        using var result = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(result);

        Assert.Equal(new SKColor(0x20, 0x20, 0x20), pixels.GetPixel(50, 50));
        Assert.NotEqual(new SKColor(0x20, 0x20, 0x20), pixels.GetPixel(0, 0));
        Assert.Equal(0, transparentBitmap.GetPixel(25, 25).Alpha);
    }

    [Theory]
    [InlineData((int)StageMode.Black, 0x00)]
    [InlineData((int)StageMode.Neutral, 0x50)]
    public void SolidStageUsesCentralizedColor(int modeValue, byte expectedChannel)
    {
        using var surface = SKSurface.Create(new SKImageInfo(8, 8));

        SkiaStageRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 8, 8),
            new RectD(2, 2, 4, 4),
            1,
            (StageMode)modeValue,
            null,
            null);
        using var result = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(result);

        Assert.Equal(new SKColor(expectedChannel, expectedChannel, expectedChannel), pixels.GetPixel(0, 0));
    }
}
