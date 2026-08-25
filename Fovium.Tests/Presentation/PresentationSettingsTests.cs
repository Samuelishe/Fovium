using Fovium.Presentation;

namespace Fovium.Tests.Presentation;

public sealed class PresentationSettingsTests
{
    [Fact]
    public void DefaultsArePracticalAndEnabled()
    {
        var settings = PresentationSettings.Default;

        Assert.True(settings.MarkupToolsEnabled);
        Assert.Equal(0.30, settings.HighlightOpacity);
        Assert.Equal(42, settings.HighlightRadiusPhysicalPixels);
        Assert.Equal(4, settings.DefaultMarkupStrokePhysicalPixels);
        Assert.Equal(1, settings.DefaultMarkupOpacity);
        Assert.Equal("#FFD54F", settings.HighlightColor.ToHex());
        Assert.Equal("#FF4545", settings.DefaultMarkupColor.ToHex());
    }

    [Fact]
    public void NormalizeClampsFiniteValuesAndRepairsNonFiniteValues()
    {
        var normalized = (PresentationSettings.Default with
        {
            HighlightOpacity = 5,
            HighlightRadiusPhysicalPixels = -10,
            DefaultMarkupStrokePhysicalPixels = double.NaN,
            DefaultMarkupOpacity = double.PositiveInfinity,
        }).Normalize();

        Assert.Equal(PresentationSettings.MaximumHighlightOpacity, normalized.HighlightOpacity);
        Assert.Equal(
            PresentationSettings.MinimumHighlightRadiusPhysicalPixels,
            normalized.HighlightRadiusPhysicalPixels);
        Assert.Equal(
            PresentationSettings.Default.DefaultMarkupStrokePhysicalPixels,
            normalized.DefaultMarkupStrokePhysicalPixels);
        Assert.Equal(PresentationSettings.Default.DefaultMarkupOpacity, normalized.DefaultMarkupOpacity);
    }

    [Fact]
    public void NormalizeClampsOppositePresentationBoundaries()
    {
        var normalized = (PresentationSettings.Default with
        {
            HighlightOpacity = -1,
            HighlightRadiusPhysicalPixels = 999,
            DefaultMarkupStrokePhysicalPixels = 999,
            DefaultMarkupOpacity = -1,
        }).Normalize();

        Assert.Equal(PresentationSettings.MinimumHighlightOpacity, normalized.HighlightOpacity);
        Assert.Equal(
            PresentationSettings.MaximumHighlightRadiusPhysicalPixels,
            normalized.HighlightRadiusPhysicalPixels);
        Assert.Equal(
            PresentationSettings.MaximumMarkupStrokePhysicalPixels,
            normalized.DefaultMarkupStrokePhysicalPixels);
        Assert.Equal(PresentationSettings.MinimumMarkupOpacity, normalized.DefaultMarkupOpacity);
    }
}
