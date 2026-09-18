using System.Collections.Immutable;
using Fovium.Rendering;
using Fovium.Stage;

namespace Fovium.PhotoStyling;

internal static class NotableColorSelector
{
    internal const int MaximumColors = 3;
    internal const double GroupingDistance = 0.075;
    internal const double DuplicateDistance = 0.08;
    internal const double MinimumSupportFraction = 0.012;
    internal const double MinimumLargestComponentFraction = 0.006;
    internal const double MinimumRepeatedSupportFraction = 0.02;
    internal const double MinimumRepresentativeDistance = 0.09;
    internal const double MinimumSalience = 0.30;
    private const double MinimumComponentFraction = 0.0008;

    public static ImmutableArray<PhotoNotableColor> Select(
        PixelSize size,
        ReadOnlySpan<ushort> sampleBins,
        ReadOnlySpan<byte> sampleAlpha,
        IReadOnlyList<PhotoColorCluster> clusters,
        StageColor representative,
        StageColor average)
    {
        ArgumentNullException.ThrowIfNull(clusters);
        var sampleCount = checked(size.Width * size.Height);
        if (sampleBins.Length != sampleCount || sampleAlpha.Length != sampleCount || clusters.Count < 2)
        {
            return [];
        }

        var groups = Consolidate(clusters);
        if (groups.Count < 2)
        {
            return [];
        }

        var binToGroup = new int[4096];
        Array.Fill(binToGroup, -1);
        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            foreach (var bin in groups[groupIndex].Bins)
            {
                binToGroup[bin] = groupIndex;
            }
        }

        var spatial = MeasureSpatialSupport(
            size,
            sampleBins,
            sampleAlpha,
            binToGroup,
            groups.Count);
        var representativeLab = PhotoStylingOklab.FromSrgb(representative);
        var averageLab = PhotoStylingOklab.FromSrgb(average);
        var candidates = new List<PhotoNotableColor>(groups.Count);
        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            var support = group.Support;
            var largest = spatial[index].LargestComponent;
            var coherent = spatial[index].CoherentSupport;
            if (support < MinimumSupportFraction ||
                (largest < MinimumLargestComponentFraction && coherent < MinimumRepeatedSupportFraction))
            {
                continue;
            }

            var lab = group.Center;
            var representativeDistance = Distance(lab, representativeLab);
            if (representativeDistance < MinimumRepresentativeDistance)
            {
                continue;
            }

            var contrast = Math.Clamp((representativeDistance - 0.04) / 0.28, 0, 1);
            var chroma = Math.Clamp((lab.Chroma - 0.02) / 0.16, 0, 1);
            var lightnessContrast = Math.Clamp(Math.Abs(lab.L - averageLab.L) / 0.35, 0, 1);
            var salience = (0.50 * contrast) + (0.30 * chroma) + (0.20 * lightnessContrast);
            if (salience < MinimumSalience)
            {
                continue;
            }

            var mass = SmoothStep(Math.Clamp(
                (support - MinimumSupportFraction) / 0.10,
                0,
                1));
            var coherence = Math.Clamp(
                Math.Max(
                    largest / 0.08,
                    coherent / 0.04),
                0,
                1);
            var score = salience * (0.65 + 0.35 * mass) * (0.60 + 0.40 * coherence);
            candidates.Add(new PhotoNotableColor(
                lab.ToSrgb(),
                support,
                largest,
                coherent,
                score));
        }

        var selected = new List<PhotoNotableColor>(MaximumColors);
        foreach (var candidate in candidates
                     .OrderByDescending(candidate => Math.Round(candidate.Score, 12))
                     .ThenByDescending(candidate => Math.Round(candidate.SupportFraction, 12))
                     .ThenBy(candidate => candidate.Color.Red)
                     .ThenBy(candidate => candidate.Color.Green)
                     .ThenBy(candidate => candidate.Color.Blue))
        {
            var lab = PhotoStylingOklab.FromSrgb(candidate.Color);
            if (IsAchromatic(lab) && selected.Any(existing =>
                    IsAchromatic(PhotoStylingOklab.FromSrgb(existing.Color))))
            {
                continue;
            }

            if (selected.Any(existing =>
                    Distance(lab, PhotoStylingOklab.FromSrgb(existing.Color)) < DuplicateDistance))
            {
                continue;
            }

            selected.Add(candidate);
            if (selected.Count == MaximumColors)
            {
                break;
            }
        }

        return selected.ToImmutableArray();
    }

    private static List<ColorGroup> Consolidate(IReadOnlyList<PhotoColorCluster> clusters)
    {
        var groups = new List<ColorGroup>();
        foreach (var cluster in clusters
                     .OrderByDescending(cluster => cluster.Weight)
                     .ThenBy(cluster => cluster.StableKey))
        {
            var lab = PhotoStylingOklab.FromSrgb(cluster.Color);
            var nearestIndex = -1;
            var nearestDistance = double.MaxValue;
            for (var index = 0; index < groups.Count; index++)
            {
                var distance = Distance(lab, groups[index].Center);
                if (BelongsToSamePerceptualFamily(lab, groups[index].Center, distance) &&
                    distance < nearestDistance)
                {
                    nearestIndex = index;
                    nearestDistance = distance;
                }
            }

            if (nearestIndex < 0)
            {
                groups.Add(new ColorGroup(cluster.StableKey, lab, cluster.Weight));
            }
            else
            {
                groups[nearestIndex].Add(cluster.StableKey, lab, cluster.Weight);
            }
        }

        return groups;
    }

    private static SpatialSupport[] MeasureSpatialSupport(
        PixelSize size,
        ReadOnlySpan<ushort> sampleBins,
        ReadOnlySpan<byte> sampleAlpha,
        int[] binToGroup,
        int groupCount)
    {
        var support = new SpatialSupport[groupCount];
        var visited = new bool[sampleBins.Length];
        var queue = new int[sampleBins.Length];
        var totalWeight = 0d;
        for (var index = 0; index < sampleAlpha.Length; index++)
        {
            totalWeight += sampleAlpha[index] / 255d;
        }

        if (totalWeight <= 0)
        {
            return support;
        }

        var minimumComponentWeight = totalWeight * MinimumComponentFraction;
        for (var start = 0; start < sampleBins.Length; start++)
        {
            if (visited[start] || sampleBins[start] == ushort.MaxValue)
            {
                continue;
            }

            var group = binToGroup[sampleBins[start]];
            if (group < 0)
            {
                continue;
            }

            var head = 0;
            var tail = 0;
            var componentWeight = 0d;
            visited[start] = true;
            queue[tail++] = start;
            while (head < tail)
            {
                var current = queue[head++];
                componentWeight += sampleAlpha[current] / 255d;
                var x = current % size.Width;
                var y = current / size.Width;
                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (offsetX == 0 && offsetY == 0)
                        {
                            continue;
                        }

                        var neighborX = x + offsetX;
                        var neighborY = y + offsetY;
                        if (neighborX < 0 || neighborX >= size.Width ||
                            neighborY < 0 || neighborY >= size.Height)
                        {
                            continue;
                        }

                        var neighbor = (neighborY * size.Width) + neighborX;
                        if (!visited[neighbor] && sampleBins[neighbor] != ushort.MaxValue &&
                            binToGroup[sampleBins[neighbor]] == group)
                        {
                            visited[neighbor] = true;
                            queue[tail++] = neighbor;
                        }
                    }
                }
            }

            var componentFraction = componentWeight / totalWeight;
            var currentSupport = support[group];
            support[group] = new SpatialSupport(
                Math.Max(currentSupport.LargestComponent, componentFraction),
                currentSupport.CoherentSupport +
                (componentWeight >= minimumComponentWeight ? componentFraction : 0));
        }

        return support;
    }

    private static double Distance(PhotoStylingOklab first, PhotoStylingOklab second)
    {
        var deltaL = first.L - second.L;
        var deltaA = first.A - second.A;
        var deltaB = first.B - second.B;
        return Math.Sqrt((deltaL * deltaL) + (deltaA * deltaA) + (deltaB * deltaB));
    }

    private static bool BelongsToSamePerceptualFamily(
        PhotoStylingOklab first,
        PhotoStylingOklab second,
        double distance)
    {
        if (distance < GroupingDistance)
        {
            return true;
        }

        if (first.Chroma < 0.04 || second.Chroma < 0.04 ||
            Math.Abs(first.L - second.L) > 0.30 ||
            Math.Abs(first.Chroma - second.Chroma) > 0.10)
        {
            return false;
        }

        var hueDifference = Math.Abs(
            Math.Atan2(first.B, first.A) - Math.Atan2(second.B, second.A));
        hueDifference = Math.Min(hueDifference, 2 * Math.PI - hueDifference);
        return hueDifference <= 0.28;
    }

    private static bool IsAchromatic(PhotoStylingOklab color) => color.Chroma <= 0.04;

    private static double SmoothStep(double value) => value * value * (3 - (2 * value));

    private sealed class ColorGroup
    {
        private double _lightness;
        private double _a;
        private double _b;

        public ColorGroup(int bin, PhotoStylingOklab lab, double weight)
        {
            Bins = [bin];
            Support = weight;
            _lightness = lab.L * weight;
            _a = lab.A * weight;
            _b = lab.B * weight;
        }

        public List<int> Bins { get; }

        public double Support { get; private set; }

        public PhotoStylingOklab Center => new(
            _lightness / Support,
            _a / Support,
            _b / Support);

        public void Add(int bin, PhotoStylingOklab lab, double weight)
        {
            Bins.Add(bin);
            Support += weight;
            _lightness += lab.L * weight;
            _a += lab.A * weight;
            _b += lab.B * weight;
        }
    }

    private readonly record struct SpatialSupport(
        double LargestComponent,
        double CoherentSupport);
}