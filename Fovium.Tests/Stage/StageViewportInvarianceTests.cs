using Fovium.Rendering;
using Fovium.Stage;

namespace Fovium.Tests.Stage;

public sealed class StageViewportInvarianceTests
{
    [Theory]
    [InlineData((int)StageBackgroundMode.Black, false)]
    [InlineData((int)StageBackgroundMode.Neutral, true)]
    [InlineData((int)StageBackgroundMode.Custom, true)]
    [InlineData((int)StageBackgroundMode.Ambient, true)]
    public void StageTransitionDoesNotChangePhotoGeometry(
        int backgroundValue,
        bool matteEnabled)
    {
        var stage = StageSettings.Default with
        {
            BackgroundMode = (StageBackgroundMode)backgroundValue,
            MatteEnabled = matteEnabled,
        };
        var destination = new RectD(-120, 75, 1800, 1200);
        var viewport = new LogicalSize(1200, 800);
        var ambient = new PixelSize(384, 256);

        var result = StageGeometry.CalculateRenderGeometry(
            stage,
            destination,
            ambient,
            viewport,
            1.5);
        Assert.Equal(destination, result.PhotoDestination);
        Assert.Equal(matteEnabled, result.MatteDestination is not null);
    }
}
