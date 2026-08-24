using Fovium.Rendering;
using Fovium.Stage;

namespace Fovium.Tests.Stage;

public sealed class StageViewportInvarianceTests
{
    [Theory]
    [InlineData((int)StageMode.Black, (int)StageMode.Neutral)]
    [InlineData((int)StageMode.Neutral, (int)StageMode.Ambient)]
    [InlineData((int)StageMode.Ambient, (int)StageMode.AmbientMatte)]
    [InlineData((int)StageMode.AmbientMatte, (int)StageMode.Black)]
    public void StageTransitionDoesNotChangePhotoGeometry(
        int fromValue,
        int toValue)
    {
        var from = (StageMode)fromValue;
        var to = (StageMode)toValue;
        var destination = new RectD(-120, 75, 1800, 1200);
        var viewport = new LogicalSize(1200, 800);
        var ambient = new PixelSize(384, 256);

        var before = StageGeometry.CalculateRenderGeometry(
            from,
            destination,
            ambient,
            viewport,
            1.5);
        var after = StageGeometry.CalculateRenderGeometry(
            to,
            destination,
            ambient,
            viewport,
            1.5);

        Assert.Equal(destination, before.PhotoDestination);
        Assert.Equal(destination, after.PhotoDestination);
    }
}
