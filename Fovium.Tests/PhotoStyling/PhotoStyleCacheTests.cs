using Fovium.PhotoStyling;
using Fovium.Stage;
using Fovium.Tests.Stage;
using SkiaSharp;

namespace Fovium.Tests.PhotoStyling;

public sealed class PhotoStyleCacheTests
{
    [Fact]
    public void AnalysisIsAttachedOnceAndByteAccountedWithDecodedCacheEntry()
    {
        using var decoded = StageTestImages.CreateDecoded(retainedBytes: 1024);
        var first = PhotoDerivedStylePolicyTests.CreateAnalysis(
            new StageColor(10, 20, 30),
            new StageColor(40, 50, 60),
            new StageColor(70, 80, 90),
            new StageColor(210, 95, 35));
        var rejected = PhotoDerivedStylePolicyTests.CreateAnalysis(
            new StageColor(100, 110, 120),
            new StageColor(130, 140, 150),
            new StageColor(160, 170, 180));

        Assert.True(decoded.TryAttachPhotoStyleAnalysis(first));
        var profile = Assert.IsType<PhotoColorProfile>(new PhotoColorProfileProjector().Create(first));
        Assert.True(decoded.TryAttachPhotoColorProfile(profile));
        Assert.False(decoded.TryAttachPhotoColorProfile(profile));
        Assert.False(decoded.TryAttachPhotoStyleAnalysis(rejected));
        Assert.Same(first, decoded.GetPhotoStyleAnalysis());
        Assert.Same(profile, decoded.GetPhotoColorProfile());
        Assert.Equal(new StageColor(210, 95, 35), Assert.Single(profile.NotableColors).Color);
        using var wash = Assert.IsType<Fovium.Imaging.DecodedImage.PhotoStyleRasterLease>(
            decoded.TryAcquirePhotoStyleRaster(StageBackgroundMode.ColorWash));
        using var gradient = Assert.IsType<Fovium.Imaging.DecodedImage.PhotoStyleRasterLease>(
            decoded.TryAcquirePhotoStyleRaster(StageBackgroundMode.ColorGradient));
        using var glow = Assert.IsType<Fovium.Imaging.DecodedImage.PhotoStyleRasterLease>(
            decoded.TryAcquirePhotoStyleRaster(StageBackgroundMode.SoftGlow));
        Assert.Equal(
            1024 + first.RetainedBytes + profile.RetainedBytes + wash.RetainedBytes + gradient.RetainedBytes +
            glow.RetainedBytes,
            decoded.RetainedBytes);
        Assert.Equal(
            StageDefaults.PhotoStyleWashRasterPixels * StageDefaults.PhotoStyleWashRasterPixels * 4,
            wash.RetainedBytes);
        Assert.Equal(
            StageDefaults.PhotoStyleGradientRasterPixels * StageDefaults.PhotoStyleGradientRasterPixels * 4,
            gradient.RetainedBytes);
        Assert.Equal(gradient.RetainedBytes, glow.RetainedBytes);
    }

    [Fact]
    public void GeometryAndStageChangesReuseSameImmutableAnalysisInstance()
    {
        using var decoded = StageTestImages.CreateDecoded();
        var analysis = PhotoDerivedStylePolicyTests.CreateAnalysis(
            new StageColor(20, 40, 60),
            new StageColor(80, 100, 120),
            new StageColor(140, 160, 180),
            new StageColor(210, 95, 35));
        Assert.True(decoded.TryAttachPhotoStyleAnalysis(analysis));
        var profile = Assert.IsType<PhotoColorProfile>(new PhotoColorProfileProjector().Create(analysis));
        Assert.True(decoded.TryAttachPhotoColorProfile(profile));
        var retainedBytes = decoded.RetainedBytes;

        for (var index = 0; index < 50; index++)
        {
            var modes = Enum.GetValues<StageBackgroundMode>();
            var stage = StageSettings.Default with
            {
                BackgroundMode = modes[index % modes.Length],
                MatteEnabled = index % 2 == 0,
                MatteWidthPhysicalPixels = 4 + index,
            };
            _ = StageGeometry.CalculateRenderGeometry(
                stage,
                new Fovium.Rendering.RectD(index, index, 200, 120),
                null,
                new Fovium.Rendering.LogicalSize(800 + index, 600 + index),
                1.25);
            Assert.Same(analysis, decoded.GetPhotoStyleAnalysis());
            Assert.Same(profile, decoded.GetPhotoColorProfile());
            Assert.Equal(new StageColor(210, 95, 35), Assert.Single(profile.NotableColors).Color);
            Assert.Equal(retainedBytes, decoded.RetainedBytes);
            using var wash = Assert.IsType<Fovium.Imaging.DecodedImage.PhotoStyleRasterLease>(
                decoded.TryAcquirePhotoStyleRaster(StageBackgroundMode.ColorWash));
            using var secondWash = Assert.IsType<Fovium.Imaging.DecodedImage.PhotoStyleRasterLease>(
                decoded.TryAcquirePhotoStyleRaster(StageBackgroundMode.ColorWash));
            Assert.Same(wash.Image, secondWash.Image);
        }
    }

    [Theory]
    [InlineData((int)StageBackgroundMode.ColorGradient)]
    [InlineData((int)StageBackgroundMode.SoftGlow)]
    public void ExpressiveGradientRastersAreBoundedAndByteAccounted(int modeValue)
    {
        using var decoded = StageTestImages.CreateDecoded(retainedBytes: 2048);
        var analysis = PhotoDerivedStylePolicyTests.CreateAnalysis(
            new StageColor(50, 80, 110),
            new StageColor(150, 80, 60),
            new StageColor(20, 30, 40));
        Assert.True(decoded.TryAttachPhotoStyleAnalysis(analysis));
        var retained = decoded.RetainedBytes;
        using var raster = Assert.IsType<Fovium.Imaging.DecodedImage.PhotoStyleRasterLease>(
            decoded.TryAcquirePhotoStyleRaster((StageBackgroundMode)modeValue));

        for (var index = 0; index < 50; index++)
        {
            var stage = StageSettings.Default with
            {
                BackgroundMode = (StageBackgroundMode)modeValue,
            };
            _ = PhotoDerivedStylePolicy.ResolveLinearGradient(analysis);
            _ = PhotoDerivedStylePolicy.ResolveRadialGlow(analysis);
            Assert.True(stage.RequiresPhotoStyleAnalysis());
            Assert.Same(analysis, decoded.GetPhotoStyleAnalysis());
            Assert.Equal(retained, decoded.RetainedBytes);
            Assert.Equal(
                StageDefaults.PhotoStyleGradientRasterPixels * StageDefaults.PhotoStyleGradientRasterPixels * 4,
                raster.RetainedBytes);
        }
    }

    [Theory]
    [InlineData((int)StageBackgroundMode.ColorGradient)]
    [InlineData((int)StageBackgroundMode.SoftGlow)]
    public void ExpressiveRasterLeaseMatchesSelectedModeAndRemainsStable(int modeValue)
    {
        var mode = (StageBackgroundMode)modeValue;
        using var decoded = StageTestImages.CreateDecoded();
        var analysis = PhotoDerivedStylePolicyTests.CreateAnalysis(
            new StageColor(80, 105, 130),
            new StageColor(190, 70, 45),
            new StageColor(25, 40, 70));
        Assert.True(decoded.TryAttachPhotoStyleAnalysis(analysis));
        using var first = Assert.IsType<Fovium.Imaging.DecodedImage.PhotoStyleRasterLease>(
            decoded.TryAcquirePhotoStyleRaster(mode));
        using var second = Assert.IsType<Fovium.Imaging.DecodedImage.PhotoStyleRasterLease>(
            decoded.TryAcquirePhotoStyleRaster(mode));
        using var expected = mode == StageBackgroundMode.ColorGradient
            ? PhotoDerivedStylePolicy.CreateColorGradientImage(analysis)
            : PhotoDerivedStylePolicy.CreateSoftGlowImage(analysis);
        using var actualPixels = SKBitmap.FromImage(first.Image);
        using var expectedPixels = SKBitmap.FromImage(expected);

        Assert.Same(first.Image, second.Image);
        Assert.Equal(expectedPixels.GetPixelSpan().ToArray(), actualPixels.GetPixelSpan().ToArray());
        Assert.Null(decoded.TryAcquirePhotoStyleRaster(StageBackgroundMode.Neutral));
    }
}