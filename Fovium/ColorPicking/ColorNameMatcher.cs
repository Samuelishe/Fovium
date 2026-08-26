namespace Fovium.ColorPicking;

internal readonly record struct ColorNameMatch(
    string StableId,
    string CanonicalName,
    double DistanceSquared);

internal sealed class ColorNameMatcher
{
    private const double TieTolerance = 1e-15;
    private readonly PreparedEntry[] _entries;

    public ColorNameMatcher(ColorNameCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _entries = catalog.Entries
            .Select(entry => new PreparedEntry(
                entry,
                OklabColor.FromSrgb(entry.Red, entry.Green, entry.Blue)))
            .ToArray();
        if (_entries.Length == 0)
        {
            throw new ArgumentException("At least one color-name entry is required.", nameof(catalog));
        }
    }

    public ColorNameMatch FindNearest(byte red, byte green, byte blue)
    {
        var sample = OklabColor.FromSrgb(red, green, blue);
        var best = _entries[0];
        var bestDistance = sample.DistanceSquared(best.Oklab);
        for (var index = 1; index < _entries.Length; index++)
        {
            var candidate = _entries[index];
            var distance = sample.DistanceSquared(candidate.Oklab);
            if (distance < bestDistance - TieTolerance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return new ColorNameMatch(
            best.Entry.StableId,
            best.Entry.CanonicalName,
            bestDistance);
    }

    private readonly record struct PreparedEntry(ColorNameEntry Entry, OklabColor Oklab);
}
