using Fovium.Stage;

namespace Fovium.Tests.Stage;

public sealed class StageDefaultsTests
{
    [Fact]
    public void R3F2PresentationDefaultsAreCentralizedAndExplicit()
    {
        Assert.Equal(new StageColor(0x00, 0x00, 0x00), StageDefaults.BlackColor);
        Assert.Equal(new StageColor(0x50, 0x50, 0x50), StageDefaults.NeutralColor);
        Assert.Equal(new StageColor(0x20, 0x20, 0x20), StageDefaults.CustomBackgroundColor);
        Assert.Equal(new StageColor(0x20, 0x20, 0x20), StageDefaults.MatteColor);
        Assert.Equal(384, StageDefaults.AmbientLongEdgePixels);
        Assert.Equal(18, StageDefaults.AmbientBlurSigmaPixels);
        Assert.Equal(0.85, StageDefaults.AmbientSaturation);
        Assert.Equal(0.65, StageDefaults.AmbientBrightness);
        Assert.Equal(0.30, StageDefaults.AmbientBrightnessMinimum);
        Assert.Equal(1.00, StageDefaults.AmbientBrightnessMaximum);
        Assert.Equal(0.00, StageDefaults.AmbientSaturationMinimum);
        Assert.Equal(1.25, StageDefaults.AmbientSaturationMaximum);
        Assert.Equal(8, StageDefaults.AmbientBlurMinimum);
        Assert.Equal(32, StageDefaults.AmbientBlurMaximum);
        Assert.Equal(24, StageDefaults.MatteWidthPhysicalPixels);
        Assert.Equal(4, StageDefaults.MatteWidthMinimumPhysicalPixels);
        Assert.Equal(192, StageDefaults.MatteWidthMaximumPhysicalPixels);
        Assert.Equal(MatteStyle.Solid, StageDefaults.MatteStyle);
        Assert.Equal(1.5, StageDefaults.MatteOuterShapeRatio);
        Assert.Equal(1d / 3d, StageDefaults.MatteSoftSigmaRatio);
    }
}
