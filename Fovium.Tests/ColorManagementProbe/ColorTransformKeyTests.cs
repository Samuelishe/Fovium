using Fovium.ColorManagementProbe;

namespace Fovium.Tests.ColorManagementProbe;

public sealed class ColorTransformKeyTests
{
    private static readonly DisplayProfileIdentity DestinationA =
        DisplayProfileIdentity.FromBytes("destination-a"u8);

    private static readonly DisplayProfileIdentity DestinationB =
        DisplayProfileIdentity.FromBytes("destination-b"u8);

    [Fact]
    public void EqualFieldValuesProduceEqualKeysAndHashCodes()
    {
        var first = CreateKey();
        var second = new ColorTransformKey(
            string.Concat("source", "-a"),
            DisplayProfileIdentity.FromBytes("destination-a"u8),
            string.Concat("BGRA", "8888"),
            string.Concat("Pre", "mul"),
            string.Concat("Percept", "ual"));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void EveryFieldParticipatesInEquality()
    {
        var baseline = CreateKey();
        var changes = new[]
        {
            baseline with { SourceColorIdentity = "source-b" },
            baseline with { DestinationIdentity = DestinationB },
            baseline with { PixelFormat = "RGBA8888" },
            baseline with { AlphaSemantics = "Unpremul" },
            baseline with { RenderingIntent = "RelativeColorimetric" },
        };

        Assert.All(changes, changed => Assert.NotEqual(baseline, changed));
        Assert.Equal(changes.Length, changes.Distinct().Count());
    }

    [Fact]
    public void SourceAndDestinationDirectionCannotAlias()
    {
        var sourceToDestination = CreateKey();
        var destinationToSource = new ColorTransformKey(
            DestinationA.Sha256,
            DisplayProfileIdentity.FromBytes("source-a"u8),
            sourceToDestination.PixelFormat,
            sourceToDestination.AlphaSemantics,
            sourceToDestination.RenderingIntent);

        Assert.NotEqual(sourceToDestination, destinationToSource);
        Assert.NotEqual(
            sourceToDestination.SourceColorIdentity,
            destinationToSource.SourceColorIdentity);
        Assert.NotEqual(
            sourceToDestination.DestinationIdentity,
            destinationToSource.DestinationIdentity);
    }

    private static ColorTransformKey CreateKey() =>
        new("source-a", DestinationA, "BGRA8888", "Premul", "Perceptual");
}
