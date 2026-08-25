using Fovium.Rendering;
using Fovium.Stage;
using SkiaSharp;

namespace Fovium.Tests.Stage;

public sealed class SkiaStageRendererTests
{
    [Fact]
    public void ActualStageDrawRecordsFallbackAndMatchingAmbientFramesByIdentity()
    {
        using var surface = SKSurface.Create(new SKImageInfo(64, 48));
        using var ambientBitmap = new SKBitmap(new SKImageInfo(16, 8));
        ambientBitmap.Erase(SKColors.DarkSlateBlue);
        using var ambient = SKImage.FromBitmap(ambientBitmap);
        var diagnostics = new AmbientRenderFrameDiagnostics();
        var stage = StageSettings.Default with { BackgroundMode = StageBackgroundMode.Ambient };

        SkiaStageRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 64, 48),
            new RectD(8, 8, 48, 32),
            1,
            stage,
            null,
            null,
            imageIdentity: 41,
            ambientIdentity: null,
            frameDiagnostics: diagnostics);
        SkiaStageRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 64, 48),
            new RectD(8, 8, 48, 32),
            1,
            stage,
            ambient,
            new PixelSize(16, 8),
            imageIdentity: 42,
            ambientIdentity: 42,
            frameDiagnostics: diagnostics);

        var metrics = diagnostics.GetMetrics();
        Assert.Equal(1, metrics.BlackFallbackRenderedFrameCount);
        Assert.Equal(1, metrics.MatchingAmbientRenderedFrameCount);
        Assert.Equal(42, metrics.LastFrame.ImageIdentity);
        Assert.Equal(42, metrics.LastFrame.AmbientIdentity);
        Assert.False(metrics.LastFrame.UsedBlackFallback);
        Assert.True(metrics.LastFrame.Timestamp > 0);
    }

    [Fact]
    public void MismatchedAmbientIdentityRendersBlackFallbackInsteadOfWrongImageStage()
    {
        using var surface = SKSurface.Create(new SKImageInfo(32, 24));
        using var ambientBitmap = new SKBitmap(new SKImageInfo(8, 8));
        ambientBitmap.Erase(SKColors.Magenta);
        using var ambient = SKImage.FromBitmap(ambientBitmap);
        var diagnostics = new AmbientRenderFrameDiagnostics();

        SkiaStageRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 32, 24),
            new RectD(8, 6, 16, 12),
            1,
            StageSettings.Default with { BackgroundMode = StageBackgroundMode.Ambient },
            ambient,
            new PixelSize(8, 8),
            imageIdentity: 52,
            ambientIdentity: 51,
            frameDiagnostics: diagnostics);
        using var result = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(result);

        Assert.Equal(SKColors.Black, pixels.GetPixel(0, 0));
        Assert.Equal(1, diagnostics.GetMetrics().BlackFallbackRenderedFrameCount);
        Assert.Equal(0, diagnostics.GetMetrics().MatchingAmbientRenderedFrameCount);
    }

    [Fact]
    public void RenderPipelineDiagnosticsDistinguishSchedulingEntryAndSkiaLeaseAvailability()
    {
        var diagnostics = new AmbientRenderFrameDiagnostics();
        diagnostics.RecordViewportRender();
        Assert.Equal(0, diagnostics.GetMetrics().ViewportRenderCount);
        diagnostics.EnablePipelineTracking();

        diagnostics.RecordViewportRender();
        diagnostics.RecordCustomDrawScheduled();
        diagnostics.RecordCustomDrawEntered();
        diagnostics.RecordSkiaLeaseUnavailable();
        diagnostics.RecordViewportRender();
        diagnostics.RecordCustomDrawScheduled();
        diagnostics.RecordCustomDrawEntered();
        diagnostics.RecordSkiaLeaseAcquired();

        var metrics = diagnostics.GetMetrics();
        Assert.Equal(2, metrics.ViewportRenderCount);
        Assert.Equal(2, metrics.CustomDrawScheduledCount);
        Assert.Equal(2, metrics.CustomDrawEnteredCount);
        Assert.Equal(1, metrics.SkiaLeaseUnavailableCount);
        Assert.Equal(1, metrics.SkiaLeaseAcquiredCount);
    }

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

    [Theory]
    [InlineData((int)MatteStyle.Solid)]
    [InlineData((int)MatteStyle.Rounded)]
    [InlineData((int)MatteStyle.Soft)]
    [InlineData((int)MatteStyle.Angular)]
    public void EveryMatteStylePlacesOpaqueColorBehindTransparentPhoto(int styleValue)
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
            MatteStyle = (MatteStyle)styleValue,
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

    [Theory]
    [InlineData((int)MatteStyle.Solid, true)]
    [InlineData((int)MatteStyle.Rounded, false)]
    [InlineData((int)MatteStyle.Angular, false)]
    public void HardMatteStylesProduceExpectedDeterministicOuterCorner(
        int styleValue,
        bool cornerIsMatte)
    {
        using var surface = SKSurface.Create(new SKImageInfo(120, 120));
        var stage = StageSettings.Default with
        {
            MatteEnabled = true,
            MatteColor = new StageColor(0xCC, 0x44, 0x22),
            MatteStyle = (MatteStyle)styleValue,
            MatteWidthPhysicalPixels = 20,
        };

        SkiaStageRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 120, 120),
            new RectD(40, 40, 40, 40),
            1,
            stage,
            null,
            null);
        using var result = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(result);

        Assert.Equal(cornerIsMatte, pixels.GetPixel(21, 21).Red > 0);
        Assert.Equal(new SKColor(0xCC, 0x44, 0x22), pixels.GetPixel(60, 25));
        Assert.Equal(new SKColor(0xCC, 0x44, 0x22), pixels.GetPixel(60, 60));
    }

    [Fact]
    public void SoftMatteCreatesBoundedFeatherWithoutChangingOpaqueBacking()
    {
        using var surface = SKSurface.Create(new SKImageInfo(120, 120));
        var stage = StageSettings.Default with
        {
            MatteEnabled = true,
            MatteColor = new StageColor(0xCC, 0x44, 0x22),
            MatteStyle = MatteStyle.Soft,
            MatteWidthPhysicalPixels = 24,
        };

        SkiaStageRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 120, 120),
            new RectD(40, 40, 40, 40),
            1,
            stage,
            null,
            null);
        using var result = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(result);

        Assert.True(pixels.GetPixel(30, 60).Red > 0);
        Assert.Equal(SKColors.Black, pixels.GetPixel(0, 0));
        Assert.Equal(new SKColor(0xCC, 0x44, 0x22), pixels.GetPixel(60, 60));
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
