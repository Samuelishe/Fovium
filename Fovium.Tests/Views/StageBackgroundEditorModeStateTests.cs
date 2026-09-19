using Fovium.Stage;
using Fovium.Views;

namespace Fovium.Tests.Views;

public sealed class StageBackgroundEditorModeStateTests
{
    [Theory]
    [InlineData((int)StageBackgroundMode.Black)]
    [InlineData((int)StageBackgroundMode.Neutral)]
    public void FixedModesShowOnlyQuietNoAdjustmentMessage(int modeValue)
    {
        var state = StageBackgroundEditorModeState.Resolve((StageBackgroundMode)modeValue);

        Assert.Equal(new StageBackgroundEditorModeState(true, false, false, false), state);
    }

    [Fact]
    public void CustomShowsOnlySharedColorSwatch()
    {
        var state = StageBackgroundEditorModeState.Resolve(StageBackgroundMode.Custom);

        Assert.Equal(new StageBackgroundEditorModeState(false, true, false, false), state);
    }

    [Theory]
    [InlineData((int)StageBackgroundMode.Average)]
    [InlineData((int)StageBackgroundMode.Dominant)]
    [InlineData((int)StageBackgroundMode.ColorWash)]
    [InlineData((int)StageBackgroundMode.ColorGradient)]
    [InlineData((int)StageBackgroundMode.SoftGlow)]
    public void PhotoDerivedModesShowBrightnessAndSaturationWithoutBlur(int modeValue)
    {
        var state = StageBackgroundEditorModeState.Resolve((StageBackgroundMode)modeValue);

        Assert.Equal(new StageBackgroundEditorModeState(false, false, true, false), state);
    }

    [Fact]
    public void AmbientIsTheOnlyModeThatShowsBlur()
    {
        var state = StageBackgroundEditorModeState.Resolve(StageBackgroundMode.Ambient);

        Assert.Equal(new StageBackgroundEditorModeState(false, false, true, true), state);
    }
}