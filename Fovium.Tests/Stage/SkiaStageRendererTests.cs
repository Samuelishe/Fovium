using Fovium.Rendering;
using Fovium.Stage;
using SkiaSharp;

namespace Fovium.Tests.Stage;

public sealed class SkiaStageRendererTests
{
    [Theory]
    [InlineData((int)StageBackgroundMode.Black, 0x00, 0x00, 0x00)]
    [InlineData((int)StageBackgroundMode.Neutral, 0x50, 0x50, 0x50)]
    [InlineData((int)StageBackgroundMode.Custom, 0x12, 0x34, 0x56)]
    public void SolidStageUsesResolvedOpaqueColor(
        int modeValue,
        byte expectedRed,
        byte expectedGreen,
        byte expectedBlue)
    {
        using var surface = SKSurface.Create(new SKImageInfo(8, 8));
        var stage = StageSettings.Default with
        {
            BackgroundMode = (StageBackgroundMode)modeValue,
            CustomBackgroundColor = new StageColor(0x12, 0x34, 0x56),
        };

        SkiaStageRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 8, 8),
            new RectD(2, 2, 4, 4),
            1,
            stage,
            null,
            null);
        using var result = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(result);

        Assert.Equal(new SKColor(expectedRed, expectedGreen, expectedBlue), pixels.GetPixel(0, 0));
    }

    [Fact]
    public void MattePlacesConfiguredColorBehindTransparentPhotoForAnyBackground()
    {
        using var surface = SKSurface.Create(new SKImageInfo(
            100,
            100,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        var stage = StageSettings.Default with
        {
            BackgroundMode = StageBackgroundMode.Custom,
            CustomBackgroundColor = new StageColor(0x11, 0x22, 0x33),
            MatteEnabled = true,
            MatteColor = new StageColor(0x77, 0x66, 0x55),
        };
        var photoDestination = new RectD(25, 25, 50, 50);
        SkiaStageRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 100, 100),
            photoDestination,
            1,
            stage,
            null,
            null);

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

        Assert.Equal(new SKColor(0x77, 0x66, 0x55), pixels.GetPixel(50, 50));
        Assert.Equal(new SKColor(0x11, 0x22, 0x33), pixels.GetPixel(0, 0));
        Assert.Equal(0, transparentBitmap.GetPixel(25, 25).Alpha);
    }

    [Fact]
    public void BrightnessAndSaturationAreRenderTimeMatrixInputs()
    {
        var first = SkiaStageRenderer.CreateColorMatrix(0.65, 0.85);
        var second = SkiaStageRenderer.CreateColorMatrix(0.90, 0.20);

        Assert.Equal(20, first.Length);
        Assert.Equal(20, second.Length);
        Assert.NotEqual(first[0], second[0]);
        Assert.NotEqual(first[6], second[6]);
        Assert.Equal(1, first[18]);
        Assert.Equal(1, second[18]);
    }
}
