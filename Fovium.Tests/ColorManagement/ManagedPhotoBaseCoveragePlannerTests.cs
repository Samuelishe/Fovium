using Fovium.ColorManagement;
using Fovium.Rendering;

namespace Fovium.Tests.ColorManagement;

public sealed class ManagedPhotoBaseCoveragePlannerTests
{
    [Theory]
    [InlineData(1920, 1080, 1620, 1080, 6_998_400L)]
    [InlineData(2560, 1440, 2160, 1440, 12_441_600L)]
    [InlineData(3840, 2160, 3240, 2160, 27_993_600L)]
    public void FullSourceBaseForCommonDisplaySizesPreservesWholeSourceWithinBudget(
        int viewportWidth,
        int viewportHeight,
        int expectedWidth,
        int expectedHeight,
        long expectedBytes)
    {
        var geometry = CreateGeometry(viewportWidth, viewportHeight, renderScaling: 1);

        var coverage = ManagedPhotoBaseCoveragePlanner.Create(
            geometry,
            new PixelSize(6000, 4000));

        Assert.Equal(new RectD(0, 0, 6000, 4000), coverage.OrientedSourceRect);
        Assert.Equal(new PixelSize(expectedWidth, expectedHeight), coverage.RasterPixelSize);
        Assert.Equal(expectedBytes, coverage.RetainedBytes);
        Assert.True(coverage.RetainedBytes <= ManagedPhotoBaseCoveragePlanner.MaximumBaseRasterBytes);
        Assert.Equal(1.5, coverage.RasterPixelSize.Width / (double)coverage.RasterPixelSize.Height, 8);
        Assert.False(coverage.OverscanCapped);
    }

    [Fact]
    public void FullSourceBasePlanningUsesPhysicalViewportSizeAcrossDpi()
    {
        var physical1080p = ManagedPhotoBaseCoveragePlanner.Create(
            CreateGeometry(1920, 1080, renderScaling: 1),
            new PixelSize(6000, 4000));
        var logical720pAt150Percent = ManagedPhotoBaseCoveragePlanner.Create(
            CreateGeometry(1280, 720, renderScaling: 1.5),
            new PixelSize(6000, 4000));

        Assert.Equal(physical1080p.RasterPixelSize, logical720pAt150Percent.RasterPixelSize);
        Assert.Equal(physical1080p.RetainedBytes, logical720pAt150Percent.RetainedBytes);
        Assert.Equal(physical1080p.OrientedSourceRect, logical720pAt150Percent.OrientedSourceRect);
    }

    [Fact]
    public void FullSourceBasePlanningClampsLargePhysicalViewportWithoutCroppingSource()
    {
        var geometry = CreateGeometry(10_000, 10_000, renderScaling: 2);

        var coverage = ManagedPhotoBaseCoveragePlanner.Create(
            geometry,
            new PixelSize(6000, 4000));

        Assert.Equal(new RectD(0, 0, 6000, 4000), coverage.OrientedSourceRect);
        Assert.True(coverage.OverscanCapped);
        Assert.True(coverage.RetainedBytes <= ManagedPhotoBaseCoveragePlanner.MaximumBaseRasterBytes);
        Assert.True(coverage.RasterPixelSize.Width > 0);
        Assert.True(coverage.RasterPixelSize.Height > 0);
        Assert.Equal(1.5, coverage.RasterPixelSize.Width / (double)coverage.RasterPixelSize.Height, 3);
    }

    private static ManagedPhotoGeometry CreateGeometry(
        double viewportWidth,
        double viewportHeight,
        double renderScaling) => new(
        new RectD(0, 0, viewportWidth, viewportHeight),
        new RectD(0, 0, viewportWidth, viewportHeight),
        renderScaling,
        false);
}
