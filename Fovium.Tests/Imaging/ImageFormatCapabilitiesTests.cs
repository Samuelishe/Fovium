using Fovium.Imaging;
using SkiaSharp;

namespace Fovium.Tests.Imaging;

public sealed class ImageFormatCapabilitiesTests
{
    [Fact]
    public void RegistryContainsAcceptedJpegPngStaticWebpAndBoundedTiffCapabilities()
    {
        Assert.Collection(
            ImageFormatCapabilities.All,
            jpeg => AssertCapability(
                jpeg,
                ImageFormatId.Jpeg,
                "jpeg",
                "JPEG",
                [".jpg", ".jpeg"],
                ImageAlphaCapability.NotApplicable),
            png => AssertCapability(
                png,
                ImageFormatId.Png,
                "png",
                "PNG",
                [".png"],
                ImageAlphaCapability.Supported),
            webp => AssertCapability(
                webp,
                ImageFormatId.Webp,
                "webp",
                "WEBP",
                [".webp"],
                ImageAlphaCapability.Supported),
            tiff => AssertCapability(
                tiff,
                ImageFormatId.Tiff,
                "tiff",
                "TIFF",
                [".tif", ".tiff"],
                ImageAlphaCapability.Supported));
    }

    [Fact]
    public void CandidateExtensionsAndPickerHintsComeFromRegistryWithoutDuplicates()
    {
        Assert.Equal(
            [".jpg", ".jpeg", ".png", ".webp", ".tif", ".tiff"],
            ImageFormatCapabilities.CandidateExtensions);
        Assert.Equal(
            ["*.jpg", "*.jpeg", "*.png", "*.webp", "*.tif", "*.tiff"],
            ImageFormatCapabilities.FilePickerPatterns);
        Assert.Equal(
            ["image/jpeg", "image/png", "image/webp", "image/tiff"],
            ImageFormatCapabilities.FilePickerMimeTypes);
        Assert.Equal(
            ImageFormatCapabilities.CandidateExtensions.Count,
            ImageFormatCapabilities.CandidateExtensions.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            ImageFormatCapabilities.All.Count,
            ImageFormatCapabilities.All.Select(capability => capability.StableId)
                .Distinct(StringComparer.Ordinal).Count());
        Assert.True(ImageFormatCapabilities.IsCandidateExtension(".WEBP"));
        Assert.True(ImageFormatCapabilities.IsCandidateExtension(".TIF"));
        Assert.True(ImageFormatCapabilities.IsCandidateExtension(".TIFF"));
        Assert.False(ImageFormatCapabilities.IsCandidateExtension(".foo"));
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, (int)ImageFormatId.Jpeg)]
    [InlineData(SKEncodedImageFormat.Png, (int)ImageFormatId.Png)]
    [InlineData(SKEncodedImageFormat.Webp, (int)ImageFormatId.Webp)]
    public void SkiaDetectedFormatsMapToProjectOwnedIdentity(
        SKEncodedImageFormat detected,
        int expected)
    {
        var mapped = ImageFormatCapabilities.TryGetDetected(detected, out var capability);

        Assert.True(mapped);
        Assert.NotNull(capability);
        Assert.Equal((ImageFormatId)expected, capability.Id);
    }

    [Fact]
    public void UnknownDetectedFormatIsUnsupported()
    {
        Assert.False(ImageFormatCapabilities.TryGetDetected(SKEncodedImageFormat.Gif, out var capability));
        Assert.Null(capability);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(20, false)]
    public void StaticImagePolicyRejectsEveryMultiFramePayload(int frameCount, bool expected)
    {
        var webp = ImageFormatCapabilities.Get(ImageFormatId.Webp);

        Assert.Equal(expected, ImageFormatCapabilities.SupportsFrameCount(webp, frameCount));
    }

    private static void AssertCapability(
        ImageFormatCapability capability,
        ImageFormatId id,
        string stableId,
        string displayName,
        IReadOnlyList<string> extensions,
        ImageAlphaCapability alpha)
    {
        Assert.Equal(id, capability.Id);
        Assert.Equal(stableId, capability.StableId);
        Assert.Equal(displayName, capability.DisplayName);
        Assert.Equal(extensions, capability.Extensions);
        Assert.True(capability.StaticDecodeSupported);
        Assert.Equal(alpha, capability.AlphaCapability);
        Assert.Equal(ImageAnimationPolicy.StaticOnly, capability.AnimationPolicy);
    }
}
