using Fovium.PhotoStyling;
using Fovium.Stage;

namespace Fovium.Tests.PhotoStyling;

public sealed class RepresentativeColorSelectorTests
{
    [Fact]
    public void SelectionIsStableAcrossRawClusterEnumerationOrder()
    {
        PhotoColorCluster[] clusters =
        [
            new(42, new StageColor(205, 35, 45), 0.35),
            new(7, new StageColor(35, 35, 38), 0.45),
            new(19, new StageColor(100, 105, 110), 0.20),
        ];

        var forward = RepresentativeColorSelector.Select(clusters, StageDefaults.NeutralColor);
        var reverse = RepresentativeColorSelector.Select(
            clusters.Reverse().ToArray(),
            StageDefaults.NeutralColor);

        Assert.Equal(forward, reverse);
        Assert.True(forward.Color.Red > 175);
        Assert.InRange(forward.SupportFraction, 0.30, 0.40);
    }

    [Fact]
    public void SelectionKeepsRawClusterPopulationDataImmutable()
    {
        PhotoColorCluster[] clusters =
        [
            new(5, new StageColor(115, 118, 120), 0.85),
            new(8, new StageColor(230, 25, 35), 0.05),
            new(3, new StageColor(80, 82, 85), 0.10),
        ];
        var original = clusters.ToArray();

        var selection = RepresentativeColorSelector.Select(
            clusters,
            StageDefaults.NeutralColor);

        Assert.Equal(original, clusters);
        Assert.True(selection.Color.Red < 150);
        Assert.InRange(selection.Chroma, 0, 0.02);
    }

    [Fact]
    public void RelativeAdmissionPreventsModerateAccentFromHijackingHighKeyMajority()
    {
        PhotoColorCluster[] clusters =
        [
            new(1, new StageColor(245, 245, 242), 0.85),
            new(2, new StageColor(225, 25, 35), 0.15),
        ];

        var selection = RepresentativeColorSelector.Select(
            clusters,
            StageDefaults.NeutralColor);

        Assert.InRange(selection.Lightness, 0.90, 1.0);
        Assert.InRange(selection.Chroma, 0, 0.02);
    }

    [Fact]
    public void SoftLightnessWeightPrefersMidtoneWhenChromaticSupportsAreEqual()
    {
        var dark = new PhotoStylingOklab(0.12, 0.04, 0).ToSrgb();
        var midtone = new PhotoStylingOklab(
            0.50,
            0.04 * Math.Cos(Math.PI / 3),
            0.04 * Math.Sin(Math.PI / 3)).ToSrgb();
        PhotoColorCluster[] clusters =
        [
            new(1, dark, 0.50),
            new(2, midtone, 0.50),
        ];

        var selection = RepresentativeColorSelector.Select(
            clusters,
            StageDefaults.NeutralColor);

        Assert.InRange(selection.Lightness, 0.40, 0.55);
        Assert.Equal(2, selection.FamilyIndex);
    }
}
