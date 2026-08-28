using Fovium.Stage;
using Fovium.Tests.Stage;

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
            new StageColor(70, 80, 90));
        var rejected = PhotoDerivedStylePolicyTests.CreateAnalysis(
            new StageColor(100, 110, 120),
            new StageColor(130, 140, 150),
            new StageColor(160, 170, 180));

        Assert.True(decoded.TryAttachPhotoStyleAnalysis(first));
        Assert.False(decoded.TryAttachPhotoStyleAnalysis(rejected));
        Assert.Same(first, decoded.GetPhotoStyleAnalysis());
        using var wash = Assert.IsType<Fovium.Imaging.DecodedImage.ColorWashLease>(
            decoded.TryAcquireColorWash());
        Assert.Equal(
            1024 + first.RetainedBytes + wash.RetainedBytes,
            decoded.RetainedBytes);
        Assert.Equal(
            StageDefaults.PhotoStyleWashRasterPixels * StageDefaults.PhotoStyleWashRasterPixels * 4,
            wash.RetainedBytes);
    }

    [Fact]
    public void GeometryAndStageChangesReuseSameImmutableAnalysisInstance()
    {
        using var decoded = StageTestImages.CreateDecoded();
        var analysis = PhotoDerivedStylePolicyTests.CreateAnalysis(
            new StageColor(20, 40, 60),
            new StageColor(80, 100, 120),
            new StageColor(140, 160, 180));
        Assert.True(decoded.TryAttachPhotoStyleAnalysis(analysis));

        for (var index = 0; index < 50; index++)
        {
            var stage = StageSettings.Default with
            {
                BackgroundMode = (StageBackgroundMode)(index % 7),
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
            using var wash = Assert.IsType<Fovium.Imaging.DecodedImage.ColorWashLease>(
                decoded.TryAcquireColorWash());
            using var secondWash = Assert.IsType<Fovium.Imaging.DecodedImage.ColorWashLease>(
                decoded.TryAcquireColorWash());
            Assert.Same(wash.Image, secondWash.Image);
        }
    }
}
