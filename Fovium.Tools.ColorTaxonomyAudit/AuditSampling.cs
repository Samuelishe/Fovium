namespace Fovium.Tools.ColorTaxonomyAudit;

internal readonly record struct GridKey(int Lightness, int Chroma, int Hue);

internal sealed record StructuredGrid(
    int LightnessCount,
    int ChromaCount,
    int HueCount,
    IReadOnlyDictionary<GridKey, AuditClassification> Nodes);

internal static class AuditSampling
{
    public static StructuredGrid GenerateStructured(
        AuditConfiguration configuration,
        ProductionColorAdapter adapter)
    {
        var lightnessCount = (int)Math.Round(1 / configuration.LightnessStep) + 1;
        var chromaCount = (int)Math.Round(0.32 / configuration.ChromaStep) + 1;
        var hueCount = 360 / configuration.HueStep;
        var nodes = new Dictionary<GridKey, AuditClassification>();

        for (var lightnessIndex = 0; lightnessIndex < lightnessCount; lightnessIndex++)
        {
            var lightness = Math.Min(1, lightnessIndex * configuration.LightnessStep);
            for (var chromaIndex = 0; chromaIndex < chromaCount; chromaIndex++)
            {
                var chroma = chromaIndex * configuration.ChromaStep;
                for (var hueIndex = 0; hueIndex < hueCount; hueIndex++)
                {
                    var hue = hueIndex * configuration.HueStep;
                    if (!TryOklchToSrgb(lightness, chroma, hue, out var rgb))
                    {
                        continue;
                    }

                    nodes[new GridKey(lightnessIndex, chromaIndex, hueIndex)] = adapter.Classify(rgb);
                }
            }
        }

        return new StructuredGrid(lightnessCount, chromaCount, hueCount, nodes);
    }

    public static IReadOnlyList<AuditClassification> GenerateMonteCarlo(
        int count,
        int seed,
        ProductionColorAdapter adapter)
    {
        var random = new Random(seed);
        var samples = new AuditClassification[count];
        for (var index = 0; index < count; index++)
        {
            samples[index] = adapter.Classify(new AuditRgb(
                (byte)random.Next(256),
                (byte)random.Next(256),
                (byte)random.Next(256)));
        }

        return samples;
    }

    public static IReadOnlyList<AuditClassification> GenerateRgbGrid(
        int step,
        ProductionColorAdapter adapter)
    {
        var channels = Enumerable.Range(0, 256 / step + 1)
            .Select(index => Math.Min(255, index * step))
            .Distinct()
            .Select(value => (byte)value)
            .ToArray();
        var samples = new List<AuditClassification>(channels.Length * channels.Length * channels.Length);
        foreach (var red in channels)
        {
            foreach (var green in channels)
            {
                foreach (var blue in channels)
                {
                    samples.Add(adapter.Classify(new AuditRgb(red, green, blue)));
                }
            }
        }

        return samples;
    }

    public static IReadOnlyList<AuditClassification> GenerateBoundaryRefinement(
        StructuredGrid grid,
        ProductionColorAdapter adapter)
    {
        var colors = new HashSet<int>();
        foreach (var (key, sample) in grid.Nodes)
        {
            foreach (var neighborKey in ForwardNeighbors(key, grid))
            {
                if (!grid.Nodes.TryGetValue(neighborKey, out var neighbor) || sample.Family == neighbor.Family)
                {
                    continue;
                }

                AddNeighborhood(sample.Rgb, colors);
                AddNeighborhood(neighbor.Rgb, colors);
            }
        }

        return colors.Order()
            .Select(packed => adapter.Classify(new AuditRgb(
                (byte)(packed >> 16),
                (byte)(packed >> 8),
                (byte)packed)))
            .ToArray();
    }

    public static bool TryOklchToSrgb(
        double lightness,
        double chroma,
        double hueDegrees,
        out AuditRgb rgb)
    {
        var hueRadians = hueDegrees * Math.PI / 180;
        var a = chroma * Math.Cos(hueRadians);
        var b = chroma * Math.Sin(hueRadians);

        var lRoot = lightness + 0.3963377774 * a + 0.2158037573 * b;
        var mRoot = lightness - 0.1055613458 * a - 0.0638541728 * b;
        var sRoot = lightness - 0.0894841775 * a - 1.2914855480 * b;
        var l = lRoot * lRoot * lRoot;
        var m = mRoot * mRoot * mRoot;
        var s = sRoot * sRoot * sRoot;

        var linearRed = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
        var linearGreen = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
        var linearBlue = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
        if (!IsInGamut(linearRed) || !IsInGamut(linearGreen) || !IsInGamut(linearBlue))
        {
            rgb = default;
            return false;
        }

        rgb = new AuditRgb(ToByte(linearRed), ToByte(linearGreen), ToByte(linearBlue));
        return true;
    }

    private static bool IsInGamut(double channel) => channel is >= -0.0000001 and <= 1.0000001;

    private static IEnumerable<GridKey> ForwardNeighbors(GridKey key, StructuredGrid grid)
    {
        if (key.Lightness + 1 < grid.LightnessCount)
        {
            yield return key with { Lightness = key.Lightness + 1 };
        }

        if (key.Chroma + 1 < grid.ChromaCount)
        {
            yield return key with { Chroma = key.Chroma + 1 };
        }

        yield return key with { Hue = (key.Hue + 1) % grid.HueCount };
    }

    private static void AddNeighborhood(AuditRgb rgb, ISet<int> colors)
    {
        colors.Add(rgb.Packed);
        AddOffset(rgb, -1, 0, 0, colors);
        AddOffset(rgb, 1, 0, 0, colors);
        AddOffset(rgb, 0, -1, 0, colors);
        AddOffset(rgb, 0, 1, 0, colors);
        AddOffset(rgb, 0, 0, -1, colors);
        AddOffset(rgb, 0, 0, 1, colors);
    }

    private static void AddOffset(AuditRgb rgb, int red, int green, int blue, ISet<int> colors)
    {
        var adjustedRed = rgb.Red + red;
        var adjustedGreen = rgb.Green + green;
        var adjustedBlue = rgb.Blue + blue;
        if (adjustedRed is < 0 or > 255 || adjustedGreen is < 0 or > 255 || adjustedBlue is < 0 or > 255)
        {
            return;
        }

        colors.Add((adjustedRed << 16) | (adjustedGreen << 8) | adjustedBlue);
    }

    private static byte ToByte(double linear)
    {
        var encoded = linear <= 0.0031308
            ? 12.92 * linear
            : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;
        return (byte)Math.Round(Math.Clamp(encoded, 0, 1) * 255, MidpointRounding.AwayFromZero);
    }
}