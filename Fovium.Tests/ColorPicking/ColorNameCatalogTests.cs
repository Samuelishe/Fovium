using Fovium.ColorPicking;

namespace Fovium.Tests.ColorPicking;

public sealed class ColorNameCatalogTests
{
    [Fact]
    public void EmbeddedCatalogContainsExactly1800UniqueStableEntries()
    {
        var entries = ColorNameCatalog.LoadEmbedded().Entries;

        Assert.Equal(ColorNameCatalog.ExpectedCount, entries.Count);
        Assert.Equal(entries.Count, entries.Select(entry => entry.StableId).Distinct().Count());
        Assert.Equal(
            entries.Count,
            entries.Select(entry => entry.CanonicalName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            entries.Count,
            entries.Select(entry => (entry.Red, entry.Green, entry.Blue)).Distinct().Count());
        Assert.All(entries, entry =>
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.StableId));
            Assert.False(string.IsNullOrWhiteSpace(entry.CanonicalName));
            var oklab = OklabColor.FromSrgb(entry.Red, entry.Green, entry.Blue);
            Assert.True(double.IsFinite(oklab.L));
            Assert.True(double.IsFinite(oklab.A));
            Assert.True(double.IsFinite(oklab.B));
        });
    }

    [Fact]
    public void EmbeddedCatalogContainsProtectedBasicAnchors()
    {
        var names = ColorNameCatalog.LoadEmbedded().Entries
            .Select(entry => entry.CanonicalName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Subset(
            names,
            new HashSet<string>(
                ["Black", "White", "Red", "Green", "Blue", "Yellow", "Orange", "Purple",
                    "Pink", "Brown", "Grey", "Cyan", "Magenta", "Teal", "Navy Blue", "Olive", "Beige"],
                StringComparer.OrdinalIgnoreCase));
    }
}
