using Fovium.Stage;

namespace Fovium.Tests.Stage;

public sealed class StageDefaultsTests
{
    [Fact]
    public void R3PresentationDefaultsAreCentralizedAndExplicit()
    {
        Assert.Equal(new StageColor(0x00, 0x00, 0x00), StageDefaults.BlackColor);
        Assert.Equal(new StageColor(0x50, 0x50, 0x50), StageDefaults.NeutralColor);
        Assert.Equal(new StageColor(0x20, 0x20, 0x20), StageDefaults.MatteColor);
        Assert.Equal(384, StageDefaults.AmbientLongEdgePixels);
        Assert.Equal(18, StageDefaults.AmbientBlurSigmaPixels);
        Assert.Equal(0.55f, StageDefaults.AmbientSaturation);
        Assert.Equal(0.45f, StageDefaults.AmbientBrightness);
        Assert.Equal(24, StageDefaults.MatteWidthPhysicalPixels);
    }
}
