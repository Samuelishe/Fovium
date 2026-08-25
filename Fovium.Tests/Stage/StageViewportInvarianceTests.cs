using Fovium.Rendering;
using Fovium.Stage;

namespace Fovium.Tests.Stage;

public sealed class StageViewportInvarianceTests
{
    public static TheoryData<int, int> BackgroundAndStyleCases => new()
    {
        { (int)StageBackgroundMode.Black, (int)MatteStyle.Solid },
        { (int)StageBackgroundMode.Black, (int)MatteStyle.Rounded },
        { (int)StageBackgroundMode.Black, (int)MatteStyle.Soft },
        { (int)StageBackgroundMode.Black, (int)MatteStyle.Angular },
        { (int)StageBackgroundMode.Neutral, (int)MatteStyle.Solid },
        { (int)StageBackgroundMode.Neutral, (int)MatteStyle.Rounded },
        { (int)StageBackgroundMode.Neutral, (int)MatteStyle.Soft },
        { (int)StageBackgroundMode.Neutral, (int)MatteStyle.Angular },
        { (int)StageBackgroundMode.Custom, (int)MatteStyle.Solid },
        { (int)StageBackgroundMode.Custom, (int)MatteStyle.Rounded },
        { (int)StageBackgroundMode.Custom, (int)MatteStyle.Soft },
        { (int)StageBackgroundMode.Custom, (int)MatteStyle.Angular },
        { (int)StageBackgroundMode.Ambient, (int)MatteStyle.Solid },
        { (int)StageBackgroundMode.Ambient, (int)MatteStyle.Rounded },
        { (int)StageBackgroundMode.Ambient, (int)MatteStyle.Soft },
        { (int)StageBackgroundMode.Ambient, (int)MatteStyle.Angular },
    };

    [Theory]
    [MemberData(nameof(BackgroundAndStyleCases))]
    public void EveryBackgroundAndMatteStyleKeepsResolvedPhotoGeometry(
        int backgroundValue,
        int styleValue)
    {
        var background = (StageBackgroundMode)backgroundValue;
        var style = (MatteStyle)styleValue;
        var stage = StageSettings.Default with
        {
            BackgroundMode = background,
            MatteEnabled = true,
            MatteStyle = style,
            MatteWidthPhysicalPixels = 96,
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
        var matte = Assert.IsType<MatteRenderGeometry>(result.Matte);
        Assert.Equal(destination, matte.BackingDestination);
        Assert.Equal(style, matte.Style);
        Assert.Equal(64, matte.WidthDip);
        Assert.Equal(background == StageBackgroundMode.Ambient, result.AmbientDestination is not null);
    }

    [Theory]
    [InlineData((int)MatteStyle.Solid, 8)]
    [InlineData((int)MatteStyle.Rounded, 24)]
    [InlineData((int)MatteStyle.Soft, 64)]
    [InlineData((int)MatteStyle.Angular, 128)]
    public void ChangingOnlyMattePresentationLeavesPhotoDestinationIdentical(
        int styleValue,
        double width)
    {
        var destination = new RectD(40.25, -18.5, 1024.75, 683.5);
        var result = StageGeometry.CalculateRenderGeometry(
            StageSettings.Default with
            {
                BackgroundMode = StageBackgroundMode.Ambient,
                MatteEnabled = true,
                MatteStyle = (MatteStyle)styleValue,
                MatteWidthPhysicalPixels = width,
            },
            destination,
            new PixelSize(384, 256),
            new LogicalSize(1200, 800),
            2);

        Assert.Equal(destination, result.PhotoDestination);
        Assert.Equal(destination, result.Matte?.BackingDestination);
    }
}
