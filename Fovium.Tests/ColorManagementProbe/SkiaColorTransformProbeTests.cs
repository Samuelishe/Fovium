using Fovium.ColorManagementProbe;
using SkiaSharp;

namespace Fovium.Tests.ColorManagementProbe;

public sealed class SkiaColorTransformProbeTests
{
    [Fact]
    public void OpaqueIdentityTransformPreservesChannelsAndAlpha()
    {
        using var srgb = SKColorSpace.CreateSrgb();
        var source = new ProbePixel(196, 83, 41, 255);

        var output = SkiaColorTransformProbe.TransformPixel(source, srgb, srgb);

        Assert.Equal(source, output);
    }

    [Fact]
    public void PartialAlphaTransformsColorAndPreservesAlphaWithoutDoublePremultiplication()
    {
        using var sourceSpace = SKColorSpace.CreateRgb(
            SKColorSpaceTransferFn.Srgb,
            SKColorSpaceXyz.DisplayP3);
        using var destinationSpace = SKColorSpace.CreateSrgb();
        var opaque = new ProbePixel(196, 83, 41, 255);
        var partial = opaque with { Alpha = 128 };

        var opaqueOutput = SkiaColorTransformProbe.TransformPixel(opaque, sourceSpace, destinationSpace);
        var partialOutput = SkiaColorTransformProbe.TransformPixel(partial, sourceSpace, destinationSpace);

        Assert.Equal(new ProbePixel(212, 73, 19, 255), opaqueOutput);
        Assert.Equal(new ProbePixel(211, 74, 22, 128), partialOutput);
        Assert.True(partialOutput.Red > partialOutput.Alpha);
    }

    [Fact]
    public void TransparentOutputIsCanonicalZeroBgra()
    {
        using var sourceSpace = SKColorSpace.CreateRgb(
            SKColorSpaceTransferFn.Srgb,
            SKColorSpaceXyz.DisplayP3);
        using var destinationSpace = SKColorSpace.CreateSrgb();

        var output = SkiaColorTransformProbe.TransformPixel(
            new ProbePixel(196, 83, 41, 0),
            sourceSpace,
            destinationSpace);

        Assert.Equal(new ProbePixel(0, 0, 0, 0), output);
    }

    [Fact]
    public void TwoDestinationsProduceDifferentOutputWithoutMutatingSourceInput()
    {
        using var sourceSpace = SKColorSpace.CreateSrgb();
        using var displayP3 = SKColorSpace.CreateRgb(
            SKColorSpaceTransferFn.Srgb,
            SKColorSpaceXyz.DisplayP3);
        using var adobeRgb = SKColorSpace.CreateRgb(
            SKColorSpaceTransferFn.Srgb,
            SKColorSpaceXyz.AdobeRgb);
        var source = new ProbePixel(196, 83, 41, 255);
        var before = source;

        var displayP3Output = SkiaColorTransformProbe.TransformPixel(source, sourceSpace, displayP3);
        var adobeRgbOutput = SkiaColorTransformProbe.TransformPixel(source, sourceSpace, adobeRgb);

        Assert.Equal(before, source);
        Assert.NotEqual(displayP3Output, adobeRgbOutput);
        Assert.Equal((byte)255, displayP3Output.Alpha);
        Assert.Equal((byte)255, adobeRgbOutput.Alpha);
        Assert.NotEqual(source, displayP3Output);
        Assert.NotEqual(source, adobeRgbOutput);
    }
}
