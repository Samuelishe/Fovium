using Fovium.Rendering;
using Fovium.PhotoStyling;
using Fovium.Tests.PhotoStyling;
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
    [InlineData((int)StageBackgroundMode.Average, 0x12, 0x34, 0x56)]
    [InlineData((int)StageBackgroundMode.Dominant, 0xA1, 0xB2, 0xC3)]
    public void DerivedSolidStageUsesExactMatchingPhotoAnalysis(
        int modeValue,
        byte expectedRed,
        byte expectedGreen,
        byte expectedBlue)
    {
        using var surface = SKSurface.Create(new SKImageInfo(20, 20));
        var analysis = PhotoDerivedStylePolicyTests.CreateAnalysis(
            new StageColor(0x12, 0x34, 0x56),
            new StageColor(0xA1, 0xB2, 0xC3),
            new StageColor(0x20, 0x20, 0x20));

        SkiaStageRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 20, 20),
            new RectD(5, 5, 10, 10),
            1,
            StageSettings.Default with { BackgroundMode = (StageBackgroundMode)modeValue },
            null,
            null,
            imageIdentity: 42,
            photoStyleAnalysis: analysis,
            photoStyleIdentity: 42);
        using var result = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(result);

        Assert.Equal(
            new SKColor(expectedRed, expectedGreen, expectedBlue),
            pixels.GetPixel(0, 0));
    }

    [Fact]
    public void MismatchedDerivedIdentityUsesBlackFallbackInsteadOfStaleStyling()
    {
        using var surface = SKSurface.Create(new SKImageInfo(20, 20));
        var analysis = PhotoDerivedStylePolicyTests.CreateAnalysis(
            new StageColor(240, 10, 200),
            new StageColor(10, 240, 20),
            new StageColor(200, 200, 200));

        SkiaStageRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 20, 20),
            new RectD(5, 5, 10, 10),
            1,
            StageSettings.Default with { BackgroundMode = StageBackgroundMode.Average },
            null,
            null,
            imageIdentity: 52,
            photoStyleAnalysis: analysis,
            photoStyleIdentity: 51);
        using var result = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(result);

        Assert.Equal(SKColors.Black, pixels.GetPixel(0, 0));
    }

    [Fact]
    public void ColorWashUsesBoundedSpatialArtifactInsteadOfPhotoRaster()
    {
        using var surface = SKSurface.Create(new SKImageInfo(160, 100));
        var analysis = PhotoDerivedStylePolicyTests.CreateAnalysis(
            new StageColor(160, 80, 40),
            new StageColor(200, 40, 20),
            new StageColor(30, 80, 180));
        using var wash = PhotoDerivedStylePolicy.CreateColorWashImage(analysis);

        SkiaStageRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 160, 100),
            new RectD(30, 20, 100, 60),
            1,
            StageSettings.Default with { BackgroundMode = StageBackgroundMode.ColorWash },
            null,
            null,
            imageIdentity: 7,
            photoStyleAnalysis: analysis,
            photoStyleIdentity: 7,
            colorWashImage: wash);
        using var result = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(result);

        Assert.NotEqual(SKColors.Black, pixels.GetPixel(0, 0));
        Assert.Equal(StageDefaults.PhotoStyleWashRasterPixels, wash.Width);
        Assert.Equal(StageDefaults.PhotoStyleWashRasterPixels, wash.Height);
    }

    [Fact]
    public void AutoMatteAndHairlineRenderWithoutChangingPhotoDestination()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var analysis = PhotoDerivedStylePolicyTests.CreateAnalysis(
            new StageColor(245, 245, 245),
            new StageColor(240, 240, 240),
            new StageColor(250, 250, 250));
        var destination = new RectD(30, 30, 40, 40);
        var stage = StageSettings.Default with
        {
            MatteEnabled = true,
            MatteWidthPhysicalPixels = 12,
            MatteColorSource = MatteColorSource.Average,
            PhotoSeparation = PhotoSeparationMode.HairlineAuto,
        };

        SkiaStageRenderer.Draw(
            surface.Canvas,
            new RectD(0, 0, 100, 100),
            destination,
            1,
            stage,
            null,
            null,
            imageIdentity: 9,
            photoStyleAnalysis: analysis,
            photoStyleIdentity: 9);
        using var result = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(result);

        Assert.NotEqual(SKColors.Black, pixels.GetPixel(50, 20));
        Assert.True(pixels.GetPixel(29, 50).Red < pixels.GetPixel(20, 50).Red);
        Assert.Equal(destination, StageGeometry.CalculateRenderGeometry(
            stage,
            destination,
            null,
            new LogicalSize(100, 100),
            1).PhotoDestination);
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
