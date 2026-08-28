using Fovium.Stage;

namespace Fovium.PhotoStyling;

internal readonly record struct PhotoColorCluster(
    int StableKey,
    StageColor Color,
    double Weight);

internal readonly record struct RepresentativeColorSelection(
    StageColor Color,
    double SupportFraction,
    double Lightness,
    double Chroma,
    double Score,
    int FamilyIndex);

internal static class RepresentativeColorSelector
{
    internal const int ChromaticFamilyCount = 12;
    internal const int NeutralFamilyCount = 8;
    internal const double MinimumSupportFraction = 0.08;
    internal const double LargestFamilySupportFraction = 0.25;
    internal const double NeutralChromaFloor = 0.015;
    internal const double FullColorChroma = 0.065;
    internal const double NeutralLightnessRadius = 0.25;
    internal const double ChromaScoreFloor = 0.010;
    internal const double ChromaScoreRange = 0.100;
    internal const double ChromaWeightBase = 0.65;
    internal const double ChromaWeightRange = 2.65;
    internal const double LightnessWeightBase = 0.55;
    internal const double LightnessWeightRange = 0.45;

    public static RepresentativeColorSelection Select(
        IReadOnlyList<PhotoColorCluster> clusters,
        StageColor fallback)
    {
        ArgumentNullException.ThrowIfNull(clusters);
        if (clusters.Count == 0)
        {
            var fallbackLab = PhotoStylingOklab.FromSrgb(fallback);
            return new RepresentativeColorSelection(
                fallback,
                1,
                fallbackLab.L,
                fallbackLab.Chroma,
                1,
                0);
        }

        var prepared = clusters
            .OrderBy(cluster => cluster.StableKey)
            .Select(cluster => new PreparedCluster(
                cluster,
                PhotoStylingOklab.FromSrgb(cluster.Color)))
            .ToArray();
        var candidates = Enumerable.Range(0, ChromaticFamilyCount)
            .Select(index => CreateCandidate(index, isChromatic: true, prepared))
            .Concat(Enumerable.Range(0, NeutralFamilyCount)
                .Select(index => CreateCandidate(index, isChromatic: false, prepared)))
            .Where(candidate => candidate.SupportFraction > 0)
            .ToArray();
        var largestSupport = candidates.Max(candidate => candidate.SupportFraction);
        var admission = Math.Max(
            MinimumSupportFraction,
            largestSupport * LargestFamilySupportFraction);
        var admitted = candidates
            .Where(candidate => candidate.SupportFraction >= admission)
            .ToArray();
        var selectionPool = admitted.Length > 0 ? admitted : candidates;
        return selectionPool
            .OrderByDescending(candidate => Math.Round(candidate.Score, 12))
            .ThenByDescending(candidate => Math.Round(candidate.SupportFraction, 12))
            .ThenBy(candidate => candidate.FamilyIndex)
            .First();
    }

    private static RepresentativeColorSelection CreateCandidate(
        int familyIndex,
        bool isChromatic,
        PreparedCluster[] clusters)
    {
        var center = isChromatic
            ? 2 * Math.PI * familyIndex / ChromaticFamilyCount
            : (familyIndex + 0.5) / NeutralFamilyCount;
        var support = 0d;
        var lightness = 0d;
        var a = 0d;
        var b = 0d;
        foreach (var cluster in clusters)
        {
            var colorfulness = SmoothStep(Math.Clamp(
                (cluster.Lab.Chroma - NeutralChromaFloor) /
                (FullColorChroma - NeutralChromaFloor),
                0,
                1));
            double affinity;
            if (isChromatic)
            {
                var delta = Math.Abs(Math.Atan2(
                    Math.Sin(cluster.Lab.Hue - center),
                    Math.Cos(cluster.Lab.Hue - center)));
                var alignment = Math.Max(0, Math.Cos(delta));
                affinity = colorfulness * alignment * alignment * alignment * alignment;
            }
            else
            {
                var distance = Math.Abs(cluster.Lab.L - center);
                affinity = (1 - colorfulness) * SmoothStep(
                    1 - Math.Clamp(distance / NeutralLightnessRadius, 0, 1));
            }

            var weight = cluster.Cluster.Weight * affinity;
            support += weight;
            lightness += cluster.Lab.L * weight;
            a += cluster.Lab.A * weight;
            b += cluster.Lab.B * weight;
        }

        var stableFamilyIndex = isChromatic
            ? familyIndex
            : ChromaticFamilyCount + familyIndex;
        if (support <= 0)
        {
            return new RepresentativeColorSelection(
                default,
                0,
                0,
                0,
                0,
                stableFamilyIndex);
        }

        var lab = new PhotoStylingOklab(
            lightness / support,
            a / support,
            b / support);
        var chromaPosition = Math.Clamp(
            (lab.Chroma - ChromaScoreFloor) / ChromaScoreRange,
            0,
            1);
        var chromaWeight = ChromaWeightBase +
            (ChromaWeightRange * SmoothStep(chromaPosition));
        var lightnessWeight = LightnessWeightBase +
            (LightnessWeightRange * Math.Sin(Math.PI * Math.Clamp(lab.L, 0, 1)));
        var score = support * chromaWeight * lightnessWeight;
        return new RepresentativeColorSelection(
            lab.ToSrgb(),
            support,
            lab.L,
            lab.Chroma,
            score,
            stableFamilyIndex);
    }

    private static double SmoothStep(double value) => value * value * (3 - (2 * value));

    private readonly record struct PreparedCluster(
        PhotoColorCluster Cluster,
        PhotoStylingOklab Lab);
}
