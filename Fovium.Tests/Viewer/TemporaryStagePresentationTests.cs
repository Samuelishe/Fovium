using Fovium.Stage;
using Fovium.Tests.Stage;
using Fovium.Viewer;

namespace Fovium.Tests.Viewer;

public sealed class TemporaryStagePresentationTests
{
    [Fact]
    public void AmbientComparisonUsesOnlyMatchingAmbientOwnedByComparisonImage()
    {
        using var current = StageTestImages.CreateDecoded("current.png");
        using var comparison = StageTestImages.CreateDecoded("previous.png");
        Assert.True(current.TryAttachAmbient(StageTestImages.CreateAmbient(blur: 18)));
        Assert.True(comparison.TryAttachAmbient(StageTestImages.CreateAmbient(blur: 18)));
        using var currentAmbient = current.TryAcquireAmbient();
        using var comparisonAmbient = comparison.TryAcquireAmbient();
        var stage = StageSettings.Default with { BackgroundMode = StageBackgroundMode.Ambient };

        using var presentation = TemporaryStagePresentation.Create(stage, comparison);

        Assert.NotNull(presentation.Ambient);
        Assert.Same(comparisonAmbient!.Image, presentation.Ambient!.Image);
        Assert.NotSame(currentAmbient!.Image, presentation.Ambient.Image);
    }

    [Fact]
    public void MissingOrWrongBlurComparisonAmbientUsesBlackFallbackContract()
    {
        using var missing = StageTestImages.CreateDecoded("missing.png");
        using var wrongBlur = StageTestImages.CreateDecoded("wrong.png");
        Assert.True(wrongBlur.TryAttachAmbient(StageTestImages.CreateAmbient(blur: 24)));
        var stage = StageSettings.Default with
        {
            BackgroundMode = StageBackgroundMode.Ambient,
            AmbientBlur = 18,
        };

        using var missingPresentation = TemporaryStagePresentation.Create(stage, missing);
        using var wrongPresentation = TemporaryStagePresentation.Create(stage, wrongBlur);

        Assert.Equal(StageBackgroundMode.Ambient, missingPresentation.Stage.BackgroundMode);
        Assert.Null(missingPresentation.Ambient);
        Assert.Null(wrongPresentation.Ambient);
    }

    [Theory]
    [InlineData((int)StageBackgroundMode.Black)]
    [InlineData((int)StageBackgroundMode.Neutral)]
    [InlineData((int)StageBackgroundMode.Custom)]
    public void SolidStageAndMatteSettingsPassThroughUnchangedWithoutAmbient(int modeValue)
    {
        using var comparison = StageTestImages.CreateDecoded("previous.png");
        Assert.True(comparison.TryAttachAmbient(StageTestImages.CreateAmbient()));
        var stage = StageSettings.Default with
        {
            BackgroundMode = (StageBackgroundMode)modeValue,
            MatteEnabled = true,
            MatteStyle = MatteStyle.Angular,
            MatteWidthPhysicalPixels = 73,
            MatteColor = new StageColor(0x12, 0x34, 0x56),
        };

        using var presentation = TemporaryStagePresentation.Create(stage, comparison);

        Assert.Equal(stage, presentation.Stage);
        Assert.Null(presentation.Ambient);
        Assert.True(presentation.Stage.MatteEnabled);
        Assert.Equal(MatteStyle.Angular, presentation.Stage.MatteStyle);
        Assert.Equal(73, presentation.Stage.MatteWidthPhysicalPixels);
        Assert.Equal(new StageColor(0x12, 0x34, 0x56), presentation.Stage.MatteColor);
    }
}
