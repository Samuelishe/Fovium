using Fovium.ColorPicking;

namespace Fovium.Tests.ColorPicking;

public sealed class ColorNameMatcherTests
{
    [Fact]
    public void ExactAnchorReturnsItsCanonicalEntry()
    {
        var matcher = CreateMatcher(
            new("red", 255, 0, 0, "Red"),
            new("blue", 0, 0, 255, "Blue"));

        var match = matcher.FindNearest(255, 0, 0);

        Assert.Equal("red", match.StableId);
        Assert.Equal("Red", match.CanonicalName);
        Assert.Equal(0, match.DistanceSquared);
    }

    [Fact]
    public void EqualDistanceTieUsesFirstCatalogEntryDeterministically()
    {
        var matcher = CreateMatcher(
            new("first", 20, 20, 20, "First"),
            new("second", 20, 20, 20, "Second"),
            allowDuplicateRgb: true);

        var results = Enumerable.Range(0, 100)
            .Select(_ => matcher.FindNearest(20, 20, 20).StableId)
            .Distinct()
            .ToArray();

        Assert.Equal(["first"], results);
    }

    private static ColorNameMatcher CreateMatcher(
        ColorNameEntry first,
        ColorNameEntry second,
        bool allowDuplicateRgb = false)
    {
        if (!allowDuplicateRgb)
        {
            return new ColorNameMatcher(ColorNameCatalog.CreateForTests([first, second]));
        }

        return new ColorNameMatcher(ColorNameCatalog.CreateForTests(
            [first, second],
            allowDuplicateRgb: true));
    }
}
