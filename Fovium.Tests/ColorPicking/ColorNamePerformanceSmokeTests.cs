using System.Diagnostics;
using Fovium.ColorPicking;

namespace Fovium.Tests.ColorPicking;

public sealed class ColorNamePerformanceSmokeTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public void CatalogInitializationAndThousandLookupsRemainBoundedAndObservable()
    {
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var initialization = Stopwatch.StartNew();
        var matcher = new ColorNameMatcher(ColorNameCatalog.LoadEmbedded());
        initialization.Stop();
        var initializationAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        var lookup = Stopwatch.StartNew();
        var stableIds = new HashSet<string>(StringComparer.Ordinal);
        var allDistancesFinite = true;
        for (var index = 0; index < 1000; index++)
        {
            var match = matcher.FindNearest(
                (byte)(index * 17),
                (byte)(index * 31),
                (byte)(index * 47));
            stableIds.Add(match.StableId);
            allDistancesFinite &= double.IsFinite(match.DistanceSquared);
        }

        lookup.Stop();
        Assert.True(allDistancesFinite);
        Assert.NotEmpty(stableIds);
        output.WriteLine(
            "Color catalog: 1,800-entry load+OKLab preparation {0:F3} ms; " +
            "1,000 deterministic nearest lookups {1:F3} ms; distinct results {2}; " +
            "initialization thread allocations {3:N0} bytes.",
            initialization.Elapsed.TotalMilliseconds,
            lookup.Elapsed.TotalMilliseconds,
            stableIds.Count,
            initializationAllocatedBytes);
    }

    [Fact]
    public void RepresentativeNamesAndOklabVersusEncodedRgbComparisonAreObservable()
    {
        var catalog = ColorNameCatalog.LoadEmbedded();
        var matcher = new ColorNameMatcher(catalog);
        var representative = new (string Label, byte Red, byte Green, byte Blue)[]
        {
            ("red", 255, 0, 0),
            ("dark red", 128, 0, 0),
            ("orange", 255, 128, 0),
            ("yellow", 255, 255, 0),
            ("green", 0, 128, 0),
            ("teal", 0, 128, 128),
            ("cyan", 0, 255, 255),
            ("sky blue", 80, 180, 235),
            ("navy", 0, 0, 128),
            ("purple", 128, 0, 128),
            ("magenta", 255, 0, 255),
            ("pink", 255, 192, 203),
            ("brown", 128, 64, 32),
            ("tan", 210, 180, 140),
            ("beige", 245, 245, 220),
            ("warm grey", 145, 135, 125),
            ("cool grey", 125, 135, 145),
            ("black", 0, 0, 0),
            ("white", 255, 255, 255),
        };
        foreach (var sample in representative)
        {
            var match = matcher.FindNearest(sample.Red, sample.Green, sample.Blue);
            output.WriteLine(
                "{0} ({1},{2},{3}) -> {4}",
                sample.Label,
                sample.Red,
                sample.Green,
                sample.Blue,
                match.CanonicalName);
        }

        Assert.Equal("Red", matcher.FindNearest(255, 0, 0).CanonicalName);
        Assert.Equal("Yellow", matcher.FindNearest(255, 255, 0).CanonicalName);
        Assert.Equal("Black", matcher.FindNearest(0, 0, 0).CanonicalName);
        Assert.Equal("White", matcher.FindNearest(255, 255, 255).CanonicalName);

        var differingResults = 0;
        foreach (var red in GridChannels())
            foreach (var green in GridChannels())
                foreach (var blue in GridChannels())
                {
                    var perceptual = matcher.FindNearest(red, green, blue).StableId;
                    var encodedRgb = FindNearestEncodedRgb(catalog.Entries, red, green, blue).StableId;
                    if (!string.Equals(perceptual, encodedRgb, StringComparison.Ordinal))
                    {
                        differingResults++;
                    }
                }

        output.WriteLine(
            "OKLab and naive encoded-RGB nearest selection differed for {0}/216 grid samples.",
            differingResults);
        Assert.InRange(differingResults, 1, 216);
    }

    private static IEnumerable<byte> GridChannels()
    {
        yield return 0;
        yield return 51;
        yield return 102;
        yield return 153;
        yield return 204;
        yield return 255;
    }

    private static ColorNameEntry FindNearestEncodedRgb(
        IReadOnlyList<ColorNameEntry> entries,
        byte red,
        byte green,
        byte blue)
    {
        var best = entries[0];
        var bestDistance = int.MaxValue;
        foreach (var entry in entries)
        {
            var deltaRed = red - entry.Red;
            var deltaGreen = green - entry.Green;
            var deltaBlue = blue - entry.Blue;
            var distance = (deltaRed * deltaRed) +
                (deltaGreen * deltaGreen) +
                (deltaBlue * deltaBlue);
            if (distance < bestDistance)
            {
                best = entry;
                bestDistance = distance;
            }
        }

        return best;
    }
}
