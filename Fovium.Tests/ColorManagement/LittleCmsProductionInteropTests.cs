using System.Security.Cryptography;
using Fovium.ColorManagement;
using Fovium.Imaging;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Tests.ColorManagement;

public sealed class LittleCmsProductionInteropTests
{
    private const string RequireRuntimeEnvironmentVariable = "FOVIUM_REQUIRE_LCMS_TEST_RUNTIME";

    [Fact]
    public void AppLocalRuntimeTransformsMatrixAndLutDestinationsWithProductionBgraInterop()
    {
        var availability = new LittleCmsRuntimeLocator().TryLoad();
        if (!availability.IsAvailable)
        {
            AssertRuntimeWasOptional(availability.Detail);
            return;
        }

        using var engine = new LittleCmsColorTransformEngine(availability);
        Assert.True(engine.IsAvailable, engine.RuntimeDetail);
        Assert.Contains(
            Path.Combine("runtimes", LittleCmsRuntimeLocator.GetSupportedCurrentRid()!, "native"),
            availability.Runtime!.LoadedLibraryPath,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("2.19", availability.Runtime.Version);

        AssertTransform(
            engine,
            "fovium-linear-rgb-display.icc",
            [
                0, 0, 0, 255,
                129, 63, 17, 255,
                240, 192, 128, 128,
                255, 255, 255, 1,
            ],
            [
                0, 0, 0, 255,
                56, 13, 1, 255,
                222, 134, 55, 128,
                255, 255, 255, 1,
            ],
            "d8e7c46b6e3dc939b957144f02b09f82260428b41468bf99a9f75f37fb63eae1");
        AssertTransform(
            engine,
            "fovium-lut-rgb-display.icc",
            [
                0, 0, 0, 255,
                129, 63, 17, 255,
                240, 192, 128, 128,
                255, 255, 255, 1,
            ],
            [
                0, 127, 128, 255,
                211, 187, 165, 255,
                218, 169, 113, 128,
                255, 255, 255, 1,
            ],
            "f3cb26284bdbf95906d99194a61547c93f44560e40dbff623f46a72d060c365a");

        Assert.Equal(1u, LittleCmsColorTransform.RelativeColorimetricIntent);
        Assert.Equal(0u, LittleCmsColorTransform.CopyAlphaFlags & 0x2000u);
    }

    [Fact]
    public void ManagedRendererNormalizesDisplayP3ThenPublishesUntaggedPremultipliedDevicePixels()
    {
        var availability = new LittleCmsRuntimeLocator().TryLoad();
        if (!availability.IsAvailable)
        {
            AssertRuntimeWasOptional(availability.Detail);
            return;
        }

        var profile = ReadFixture("fovium-lut-rgb-display.icc");
        using var displayP3 = SKColorSpace.CreateRgb(SKColorSpaceTransferFn.Srgb, SKColorSpaceXyz.DisplayP3);
        using var source = CreateImage(displayP3, [0, 40, 100, 128]);
        using var renderer = new SkiaLittleCmsPhotoRenderer(
            new LittleCmsColorTransformEngine(availability));
        var key = new ManagedPhotoKey(
            source.Identity,
            DisplayProfileIdentity.FromBytes(profile, false),
            source.Descriptor.EncodedSize,
            source.Descriptor.Orientation);
        using var request = new ManagedPhotoRenderRequest(
            key,
            source.Descriptor,
            source.AcquireRenderLease(),
            profile);

        using var surface = renderer.Render(request);

        Assert.Null(surface.Image.ColorSpace);
        Assert.Equal(new PixelSize(1, 1), surface.PixelSize);
        Assert.Equal(4, surface.RetainedBytes);
        var pixel = surface.CopyPixelBytes();
        Assert.Equal(128, pixel[3]);
        Assert.True(pixel[0] <= pixel[3]);
        Assert.True(pixel[1] <= pixel[3]);
        Assert.True(pixel[2] <= pixel[3]);
        Assert.NotEqual([0, 40, 100, 128], pixel);

        using var canonical = source.AcquirePixelLease();
        Assert.Equal([0, 40, 100, 128], canonical.PixelBytes.ToArray());
    }

    [Fact]
    public void HeaderAdmittedButSemanticallyIncompleteProfileIsRejectedWithoutNativeEscape()
    {
        var availability = new LittleCmsRuntimeLocator().TryLoad();
        if (!availability.IsAvailable)
        {
            AssertRuntimeWasOptional(availability.Detail);
            return;
        }

        var incomplete = DisplayIccProfileAdmissionTests.CreateProfileHeader();
        Assert.True(DisplayIccProfileAdmissionPolicy.Inspect(incomplete).IsValid);
        using var engine = new LittleCmsColorTransformEngine(availability);

        var exception = Assert.Throws<InvalidDataException>(() => engine.CreateTransform(incomplete));

        Assert.Contains("Little CMS", exception.Message, StringComparison.Ordinal);
    }

    private static void AssertTransform(
        IColorTransformEngine engine,
        string fixtureName,
        byte[] input,
        byte[] expected,
        string expectedSha256)
    {
        var profile = ReadFixture(fixtureName);
        Assert.Equal(expectedSha256, Convert.ToHexString(SHA256.HashData(profile)).ToLowerInvariant());
        var admission = DisplayIccProfileAdmissionPolicy.Inspect(profile);
        Assert.True(admission.IsValid, admission.Detail);

        using var transform = engine.CreateTransform(profile);
        var output = new byte[input.Length];
        transform.Transform(input, output);

        Assert.Equal(expected, output);
        Assert.Equal(input.Where((_, index) => index % 4 == 3), output.Where((_, index) => index % 4 == 3));
    }

    private static byte[] ReadFixture(string fixtureName) => File.ReadAllBytes(Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "ColorManagement",
        fixtureName));

    private static DecodedImage CreateImage(SKColorSpace colorSpace, byte[] pixel)
    {
        var info = new SKImageInfo(1, 1, SKColorType.Bgra8888, SKAlphaType.Premul, colorSpace);
        var bitmap = new SKBitmap(info);
        pixel.CopyTo(bitmap.GetPixelSpan());
        var image = SKImage.FromBitmap(bitmap);
        var size = new PixelSize(1, 1);
        return new DecodedImage(
            [],
            new ImageDescriptor(
                "memory",
                ImageFormatId.Png,
                size,
                size,
                ExifOrientation.Normal,
                1,
                SourceColorState.NormalizedNonSrgb,
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

    private static void AssertRuntimeWasOptional(string detail) =>
        Assert.False(
            string.Equals(
                Environment.GetEnvironmentVariable(RequireRuntimeEnvironmentVariable),
                "1",
                StringComparison.Ordinal),
            $"{RequireRuntimeEnvironmentVariable}=1 requires the materialized Fovium-owned runtime. {detail}");
}
