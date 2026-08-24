using Fovium.Imaging;
using Fovium.Rendering;
using Fovium.Stage;

namespace Fovium.Tests.Stage;

public sealed class AmbientStagePreparerTests
{
    [Theory]
    [InlineData(6000, 4000, 384, 256)]
    [InlineData(4000, 6000, 256, 384)]
    [InlineData(200, 100, 200, 100)]
    public void PreparationTargetIsBoundedByLongEdge(
        int width,
        int height,
        int expectedWidth,
        int expectedHeight)
    {
        var result = AmbientStagePreparer.CalculateTargetSize(
            new PixelSize(width, height),
            StageDefaults.AmbientLongEdgePixels);

        Assert.Equal(new PixelSize(expectedWidth, expectedHeight), result);
        Assert.True(Math.Max(result.Width, result.Height) <= StageDefaults.AmbientLongEdgePixels);
    }

    [Theory]
    [InlineData((int)ExifOrientation.Normal, 12, 8)]
    [InlineData((int)ExifOrientation.MirrorHorizontal, 12, 8)]
    [InlineData((int)ExifOrientation.Rotate180, 12, 8)]
    [InlineData((int)ExifOrientation.MirrorVertical, 12, 8)]
    [InlineData((int)ExifOrientation.Rotate90, 8, 12)]
    [InlineData((int)ExifOrientation.Transpose, 8, 12)]
    [InlineData((int)ExifOrientation.Transverse, 8, 12)]
    [InlineData((int)ExifOrientation.Rotate270, 8, 12)]
    public void PreparationUsesOrientedDimensions(
        int orientationValue,
        int expectedWidth,
        int expectedHeight)
    {
        var orientation = (ExifOrientation)orientationValue;
        using var decoded = StageTestImages.CreateDecoded(orientation: orientation);

        using var prepared = new AmbientStagePreparer().Prepare(decoded, CancellationToken.None);
        using var source = decoded.AcquireRenderLease();

        Assert.Equal(new PixelSize(expectedWidth, expectedHeight), prepared.Size);
        Assert.Equal(checked((long)expectedWidth * expectedHeight * 4), prepared.RetainedBytes);
        Assert.NotSame(source.Image, prepared.Image);
    }

    [Fact]
    public void PreparedAmbientIsSeparateOwnedResource()
    {
        using var decoded = StageTestImages.CreateDecoded();
        var prepared = new AmbientStagePreparer().Prepare(decoded, CancellationToken.None);
        var width = prepared.Image.Width;

        decoded.Dispose();

        Assert.Equal(width, prepared.Image.Width);
        prepared.Dispose();
        Assert.Throws<ObjectDisposedException>(() => prepared.Image.Width);
    }
}
