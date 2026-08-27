using Fovium.ColorManagement;
using Fovium.Imaging;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Tests.ColorManagement;

public sealed class ManagedPhotoRendererGeometryTests
{
    [Theory]
    [InlineData(1.00, 100, 50)]
    [InlineData(1.25, 125, 63)]
    [InlineData(1.50, 150, 75)]
    [InlineData(2.00, 200, 100)]
    public void RasterTracksPhysicalViewportGeometryWithoutChangingPhotoDestination(
        double renderScaling,
        int expectedWidth,
        int expectedHeight)
    {
        using var image = CreateImage(400, 200);
        using var renderer = new SkiaLittleCmsPhotoRenderer(new CopyTransformEngine());
        var geometry = new ManagedPhotoGeometry(
            new RectD(0, 0, 180, 100),
            new RectD(40, 25, 100, 50),
            renderScaling,
            true);
        var key = new ManagedPhotoKey(
            image.Identity,
            new DisplayProfileIdentity("identity", false),
            image.Descriptor.EncodedSize,
            image.Descriptor.Orientation,
            geometry);
        using var request = new ManagedPhotoRenderRequest(
            key,
            image.Descriptor,
            image.AcquireRenderLease(),
            DisplayIccProfileAdmissionTests.CreateProfileHeader());

        using var surface = renderer.Render(request);

        Assert.Equal(new PixelSize(expectedWidth, expectedHeight), surface.PixelSize);
        Assert.Equal(geometry.PhotoDestination, surface.Destination);
        Assert.Equal((long)expectedWidth * expectedHeight * 4, surface.RetainedBytes);
    }

    [Fact]
    public void RasterAddsPreferredOverscanInsideThePhotographWithoutIncludingPureStageArea()
    {
        using var image = CreateImage(4000, 3000);
        using var renderer = new SkiaLittleCmsPhotoRenderer(new CopyTransformEngine());
        var geometry = new ManagedPhotoGeometry(
            new RectD(0, 0, 800, 600),
            new RectD(-200, -150, 1200, 900),
            1,
            false);
        var key = new ManagedPhotoKey(
            image.Identity,
            new DisplayProfileIdentity("identity", false),
            image.Descriptor.EncodedSize,
            image.Descriptor.Orientation,
            geometry);
        using var request = new ManagedPhotoRenderRequest(
            key,
            image.Descriptor,
            image.AcquireRenderLease(),
            DisplayIccProfileAdmissionTests.CreateProfileHeader());

        using var surface = renderer.Render(request);

        Assert.Equal(new RectD(-160, -120, 1120, 840), surface.Destination);
        Assert.Equal(new PixelSize(1120, 840), surface.PixelSize);
        Assert.Equal(ManagedPhotoCoveragePlanner.PreferredOverscanFactor, surface.Coverage.OverscanFactor);
        Assert.False(surface.Coverage.OverscanCapped);
        Assert.NotEqual(image.Descriptor.EncodedSize, surface.PixelSize);
    }

    [Fact]
    public void OverscanRasterIsCappedAtFortyEightMebibytesAfterPixelRounding()
    {
        var geometry = new ManagedPhotoGeometry(
            new RectD(0, 0, 3000, 3000),
            new RectD(-1000, -1000, 5000, 5000),
            1,
            false);

        var coverage = ManagedPhotoCoveragePlanner.Create(
            geometry,
            new PixelSize(5000, 5000));

        Assert.True(coverage.OverscanCapped);
        Assert.True(
            coverage.RetainedBytes <= ManagedPhotoCoveragePlanner.MaximumOverscanRasterBytes,
            $"Expected at most {ManagedPhotoCoveragePlanner.MaximumOverscanRasterBytes} bytes, " +
            $"but planned {coverage.RetainedBytes} bytes.");
        Assert.True(coverage.OverscanFactor > 1);
        Assert.True(coverage.OverscanFactor < ManagedPhotoCoveragePlanner.PreferredOverscanFactor);
    }

    private static DecodedImage CreateImage(int width, int height)
    {
        using var colorSpace = SKColorSpace.CreateSrgb();
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul, colorSpace);
        var bitmap = new SKBitmap(info);
        bitmap.Erase(new SKColor(30, 60, 90, 255));
        var skImage = SKImage.FromBitmap(bitmap);
        var size = new PixelSize(width, height);
        return new DecodedImage(
            [],
            new ImageDescriptor(
                "memory",
                ImageFormatId.Png,
                size,
                size,
                ExifOrientation.Normal,
                1,
                SourceColorState.NormalizedSrgb,
                false,
                "Bgra8888/Premul",
                bitmap.ByteCount,
                bitmap.ByteCount,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero),
            bitmap,
            skImage);
    }

    private sealed class CopyTransformEngine : IColorTransformEngine
    {
        public bool IsAvailable => true;

        public string RuntimeDetail => "test copy transform";

        public IColorTransform CreateTransform(ReadOnlyMemory<byte> destinationProfile) => new CopyTransform();

        public void Dispose()
        {
        }

        private sealed class CopyTransform : IColorTransform
        {
            public void Transform(ReadOnlySpan<byte> inputBgraUnpremultiplied, Span<byte> outputBgraUnpremultiplied) =>
                inputBgraUnpremultiplied.CopyTo(outputBgraUnpremultiplied);

            public void Dispose()
            {
            }
        }
    }
}
