using Fovium.ColorManagement;
using Fovium.Imaging;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Tests.ColorManagement;

public sealed class GeometryIndependentManagedSourceTests
{
    [Fact]
    public void GeometryOnlyChangesDoNotChangeManagedRepresentationIdentity()
    {
        var destination = new DisplayProfileIdentity("A", false);
        var identity = new ManagedPhotoKey(
            17,
            destination,
            new PixelSize(6000, 4000),
            ExifOrientation.Normal);

        Assert.Equal(17, identity.ImageIdentity);
        Assert.DoesNotContain(
            typeof(ManagedPhotoKey).GetProperties(),
            property => property.Name.Contains("Geometry", StringComparison.Ordinal));
        Assert.False(ManagedPhotoRequestPolicy.ShouldRequest(identity, identity));
    }

    [Fact]
    public void SourceOrDestinationIdentityChangeRequiresNewManagedRepresentation()
    {
        var destinationA = new DisplayProfileIdentity("A", false);
        var destinationB = new DisplayProfileIdentity("B", false);
        var current = new ManagedPhotoKey(
            17,
            destinationA,
            new PixelSize(6000, 4000),
            ExifOrientation.Normal);

        Assert.True(ManagedPhotoRequestPolicy.ShouldRequest(null, current));
        Assert.True(ManagedPhotoRequestPolicy.ShouldRequest(current, current with { ImageIdentity = 18 }));
        Assert.True(ManagedPhotoRequestPolicy.ShouldRequest(
            current,
            current with { DestinationIdentity = destinationB }));
    }

    [Fact]
    public void FullSourceRendererProducesSameSizeUntaggedPixelsWithOneCmmOperation()
    {
        using var canonical = CreatePatternImage(64, 48);
        var engine = new CopyTransformEngine();
        using var renderer = new SkiaLittleCmsPhotoRenderer(engine);
        using var request = CreateRequest(canonical, new DisplayProfileIdentity("A", false));

        using var managed = renderer.Render(request);

        Assert.Equal(new PixelSize(64, 48), managed.PixelSize);
        Assert.Equal(64L * 48 * 4, managed.RetainedBytes);
        Assert.Null(managed.Image.ColorSpace);
        Assert.Equal(1, engine.TransformCalls);
        Assert.Equal(64 * 48, engine.LastPixelCount);
        using var canonicalPixels = canonical.AcquirePixelLease();
        Assert.Equal(canonicalPixels.PixelBytes.ToArray(), managed.CopyPixelBytes());
    }

    [Fact]
    public void FitZoomPanResizeAndExact100UseSamePixelsAndNeverRegenerateManagedSource()
    {
        using var canonical = CreatePatternImage(64, 48);
        var engine = new CopyTransformEngine();
        using var renderer = new SkiaLittleCmsPhotoRenderer(engine);
        using var request = CreateRequest(canonical, new DisplayProfileIdentity("A", false));
        using var managed = renderer.Render(request);
        using var canonicalLease = canonical.AcquireRenderLease();
        var destinations = Enumerable.Range(0, 50)
            .Select(index =>
            {
                var scale = 0.75 + index % 10 * 0.17;
                return new RectD(
                    80 - index % 7 * 9,
                    60 - index % 5 * 11,
                    64 * scale,
                    48 * scale);
            })
            .Append(new RectD(32, 24, 64, 48))
            .ToArray();

        foreach (var destination in destinations)
        {
            var exact = destination.Width == 64 && destination.Height == 48;
            var legacyFrame = Draw(canonicalLease.Image, destination, exact);
            var managedFrame = Draw(managed.Image, destination, exact);
            Assert.Equal(legacyFrame, managedFrame);
        }

        Assert.Equal(1, engine.TransformCalls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(2)]
    public void ManagedAndCanonicalSourcesShareOrientationAndCenterGeometry(int orientationValue)
    {
        var orientation = (ExifOrientation)orientationValue;
        using var canonical = CreatePatternImage(37, 29, orientation);
        var engine = new CopyTransformEngine();
        using var renderer = new SkiaLittleCmsPhotoRenderer(engine);
        using var request = CreateRequest(canonical, new DisplayProfileIdentity("A", false));
        using var managed = renderer.Render(request);
        using var canonicalLease = canonical.AcquireRenderLease();
        var destination = new RectD(17.25, 11.75, 185.5, 143.5);

        var legacyFrame = Draw(canonicalLease.Image, destination, false, orientation, new PixelSize(37, 29));
        var managedFrame = Draw(managed.Image, destination, false, orientation, new PixelSize(37, 29));

        Assert.Equal(legacyFrame, managedFrame);
        Assert.Equal(1, engine.TransformCalls);
    }

    private static byte[] Draw(
        SKImage source,
        RectD destination,
        bool exact,
        ExifOrientation orientation = ExifOrientation.Normal,
        PixelSize? encodedSize = null)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(240, 180, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        SkiaPhotoDrawOperation.DrawPhoto(
            canvas,
            source,
            encodedSize ?? new PixelSize(source.Width, source.Height),
            orientation,
            destination,
            exact);
        canvas.Flush();
        return bitmap.GetPixelSpan().ToArray();
    }

    private static ManagedPhotoRenderRequest CreateRequest(
        DecodedImage image,
        DisplayProfileIdentity destination) => new(
        new ManagedPhotoKey(
            image.Identity,
            destination,
            image.Descriptor.EncodedSize,
            image.Descriptor.Orientation),
        image.Descriptor,
        image.AcquireRenderLease(),
        DisplayIccProfileAdmissionTests.CreateProfileHeader());

    private static DecodedImage CreatePatternImage(
        int width,
        int height,
        ExifOrientation orientation = ExifOrientation.Normal)
    {
        using var srgb = SKColorSpace.CreateSrgb();
        var bitmap = new SKBitmap(new SKImageInfo(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            srgb));
        var pixels = bitmap.GetPixelSpan();
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            var index = offset / 4;
            pixels[offset] = (byte)(index * 17 + index / width * 11);
            pixels[offset + 1] = (byte)(index * 31 + index % width * 7);
            pixels[offset + 2] = (byte)(index * 47 + (index / width == height / 2 ? 91 : 0));
            pixels[offset + 3] = 255;
        }

        var image = SKImage.FromBitmap(bitmap);
        var encoded = new PixelSize(width, height);
        return new DecodedImage(
            [],
            new ImageDescriptor(
                "memory",
                ImageFormatId.Png,
                encoded,
                OrientationTransform.GetOrientedSize(encoded, orientation),
                orientation,
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
            image);
    }

    private sealed class CopyTransformEngine : IColorTransformEngine
    {
        public bool IsAvailable => true;

        public string RuntimeDetail => "test";

        public int TransformCalls { get; private set; }

        public int LastPixelCount { get; private set; }

        public IColorTransform CreateTransform(ReadOnlyMemory<byte> destinationProfile) =>
            new CopyTransform(this);

        public void Dispose()
        {
        }

        private sealed class CopyTransform(CopyTransformEngine owner) : IColorTransform
        {
            public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
            {
                owner.TransformCalls++;
                owner.LastPixelCount = input.Length / 4;
                input.CopyTo(output);
            }

            public void Dispose()
            {
            }
        }
    }
}
