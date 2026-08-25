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
        Assert.Equal(128, PresentationSettings.MaximumMarkupStrokePhysicalPixels);
        Assert.Equal(1, settings.DefaultMarkupOpacity);
        Assert.Equal(FloatingOverlayPlacement.Default, settings.MarkupDockPlacement);
        Assert.Equal(FloatingOverlayPlacement.BottomLeft, settings.PhotoInfoPlacement);
        Assert.Equal("#FFD54F", settings.HighlightColor.ToHex());
        Assert.Equal("#FF4545", settings.DefaultMarkupColor.ToHex());
    }

    [Theory]
    [InlineData(31, 31)]
    [InlineData(32, 32)]
    [InlineData(128, 128)]
    [InlineData(200, 128)]
    public void MarkupStrokeRangePreservesExistingValuesAndAcceptsNewMaximum(
        double input,
        double expected)
    {
        var normalized = (PresentationSettings.Default with
        {
            DefaultMarkupStrokePhysicalPixels = input,
        }).Normalize();

        Assert.Equal(expected, normalized.DefaultMarkupStrokePhysicalPixels);
    }

    [Fact]
    public void HighlightRadiusAdjustmentUsesFourPixelCommandStepAndClamps()
    {
        var settings = PresentationSettings.Default;

        var decreased = settings.AdjustHighlightRadius(-4);
        var increased = decreased.AdjustHighlightRadius(4);
        var minimum = (settings with { HighlightRadiusPhysicalPixels = 8 })
            .AdjustHighlightRadius(-4);
        var maximum = (settings with { HighlightRadiusPhysicalPixels = 256 })
            .AdjustHighlightRadius(4);

        Assert.Equal(38, decreased.HighlightRadiusPhysicalPixels);
        Assert.Equal(42, increased.HighlightRadiusPhysicalPixels);
        Assert.Equal(8, minimum.HighlightRadiusPhysicalPixels);
        Assert.Equal(256, maximum.HighlightRadiusPhysicalPixels);
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
