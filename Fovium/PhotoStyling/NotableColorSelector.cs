using System.Collections.Immutable;
using System.Diagnostics;
using Fovium.Rendering;
using Fovium.Stage;

namespace Fovium.PhotoStyling;

[Flags]
internal enum NotableColorAdmissionPath
{
    None = 0,
    SubstantialCoherentMass = 1 << 0,
    CompactChromaticAccent = 1 << 1,
    LightnessContrastNeutral = 1 << 2,
    DistributedRepeatedStructure = 1 << 3,
    MutedDistinctMass = 1 << 4
}

internal sealed record NotableColorCandidateDiagnostics(
    StageColor Color,
    double OklabLightness,
    double OklabA,
    double OklabB,
    double SupportFraction,
    double LargestComponentFraction,
    double TopComponentSupportFraction,
    int ComponentCount,
    int SpatialCellOccupancy,
    double Chroma,
    double GlobalNovelty,
    double LocalContrast,
    double LocalLightnessContrast,
    double RepeatedStructureEvidence,
    double FrequentOverlap,
    double NearDuplicatePenalty,
    NotableColorAdmissionPath AdmissionPaths,
    string RejectionReason,
    double RankingScore,
    double PresentationScore,
    bool Selected,
    string SelectionReason);

internal sealed record NotableColorSelectionDiagnostics(
    ImmutableArray<NotableColorCandidateDiagnostics> Candidates,
    TimeSpan GroupingDuration,
    TimeSpan ComponentDuration,
    TimeSpan LocalContrastDuration,
    TimeSpan AdmissionDuration,
    TimeSpan RankingDuration,
    TimeSpan TotalDuration)
{
    public static NotableColorSelectionDiagnostics Empty { get; } = new(
        [],
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero);
}

internal readonly record struct NotableColorSelectionResult(
    ImmutableArray<PhotoNotableColor> Colors,
    NotableColorSelectionDiagnostics Diagnostics);

internal static class NotableColorSelector
{
    internal const int MaximumColors = 10;
    internal const int MaximumShortlist = 16;
    internal const double GroupingDistance = 0.070;
    internal const double DuplicateDistance = 0.060;

    private const int SpatialCellColumns = 6;
    private const int SpatialCellRows = 6;
    private const int FrequentShadeCount = 5;
    private const double MinimumMeasuredComponentFraction = 0.0008;
    private const double HardMinimumSupportFraction = 0.002;
    private const double MinimumPresentationScore = 0.32;

    public static ImmutableArray<PhotoNotableColor> Select(
        PixelSize size,
        ReadOnlySpan<ushort> sampleBins,
        ReadOnlySpan<byte> sampleAlpha,
        IReadOnlyList<PhotoColorCluster> clusters,
        StageColor representative,
        StageColor average) => SelectWithDiagnostics(
        size,
        sampleBins,
        sampleAlpha,
        clusters,
        representative,
        average).Colors;

    internal static NotableColorSelectionResult SelectWithDiagnostics(
        PixelSize size,
        ReadOnlySpan<ushort> sampleBins,
        ReadOnlySpan<byte> sampleAlpha,
        IReadOnlyList<PhotoColorCluster> clusters,
        StageColor representative,
        StageColor average)
    {
        ArgumentNullException.ThrowIfNull(clusters);
        var totalClock = Stopwatch.GetTimestamp();
        var sampleCount = checked(size.Width * size.Height);
        if (sampleBins.Length != sampleCount || sampleAlpha.Length != sampleCount || clusters.Count < 2)
        {
            return new NotableColorSelectionResult([], NotableColorSelectionDiagnostics.Empty);
        }

        var phaseClock = Stopwatch.GetTimestamp();
        var groups = Consolidate(clusters);
        var groupingDuration = Stopwatch.GetElapsedTime(phaseClock);
        if (groups.Count < 2)
        {
            return new NotableColorSelectionResult(
                [],
                NotableColorSelectionDiagnostics.Empty with
                {
                    GroupingDuration = groupingDuration,
                    TotalDuration = Stopwatch.GetElapsedTime(totalClock)
                });
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

        var sampleGroups = CreateSampleGroups(sampleBins, binToGroup);
        phaseClock = Stopwatch.GetTimestamp();
        var spatial = MeasureComponents(size, sampleGroups, sampleAlpha, groups.Count);
        var componentDuration = Stopwatch.GetElapsedTime(phaseClock);
        phaseClock = Stopwatch.GetTimestamp();
        MeasureLocalContrast(size, sampleGroups, sampleAlpha, groups, spatial);
        var localContrastDuration = Stopwatch.GetElapsedTime(phaseClock);

        var representativeLab = PhotoStylingOklab.FromSrgb(representative);
        var averageLab = PhotoStylingOklab.FromSrgb(average);
        var frequent = clusters
            .Take(FrequentShadeCount)
            .Select(cluster => new FrequentShadeEvidence(
                PhotoStylingOklab.FromSrgb(cluster.Color),
                cluster.Weight))
            .ToArray();

        phaseClock = Stopwatch.GetTimestamp();
        var candidates = new List<Candidate>(groups.Count);
        for (var index = 0; index < groups.Count; index++)
        {
            var group = groups[index];
            var evidence = spatial[index];
            var lab = group.Center;
            var globalNovelty = Distance(lab, representativeLab);
            var averageLightnessContrast = Math.Abs(lab.L - averageLab.L);
            var frequentOverlap = CalculateFrequentOverlap(lab, frequent);
            var routes = DetermineAdmissionPaths(
                group.Support,
                lab,
                globalNovelty,
                averageLightnessContrast,
                evidence);
            var rejectionReason = DetermineRejectionReason(group.Support, routes, evidence);
            var rankingScore = routes == NotableColorAdmissionPath.None
                ? 0
                : CalculateRankingScore(
                    routes,
                    group.Support,
                    lab,
                    globalNovelty,
                    averageLightnessContrast,
                    evidence);
            var notable = new PhotoNotableColor(
                lab.ToSrgb(),
                group.Support,
                evidence.LargestComponent,
                evidence.CoherentSupport,
                rankingScore);
            var diagnostics = new NotableColorCandidateDiagnostics(
                notable.Color,
                lab.L,
                lab.A,
                lab.B,
                group.Support,
                evidence.LargestComponent,
                evidence.TopComponentSupport,
                evidence.ComponentCount,
                evidence.SpatialCellOccupancy,
                lab.Chroma,
                globalNovelty,
                evidence.LocalContrast,
                evidence.LocalLightnessContrast,
                evidence.RepeatedStructureEvidence,
                frequentOverlap,
                CalculateRepresentativeDuplicatePenalty(globalNovelty),
                routes,
                rejectionReason,
                rankingScore,
                0,
                false,
                routes == NotableColorAdmissionPath.None ? "Rejected during admission." : "Qualified for ranking.");
            candidates.Add(new Candidate(notable, lab, diagnostics));
        }

        var admissionDuration = Stopwatch.GetElapsedTime(phaseClock);
        phaseClock = Stopwatch.GetTimestamp();
        var colors = SelectPresentation(candidates, representativeLab, frequent);
        var rankingDuration = Stopwatch.GetElapsedTime(phaseClock);
        var diagnosticsResult = new NotableColorSelectionDiagnostics(
            candidates
                .Select(candidate => candidate.Diagnostics)
                .OrderByDescending(candidate => candidate.Selected)
                .ThenByDescending(candidate => Math.Round(candidate.PresentationScore, 12))
                .ThenByDescending(candidate => Math.Round(candidate.RankingScore, 12))
                .ThenBy(candidate => candidate.Color.Red)
                .ThenBy(candidate => candidate.Color.Green)
                .ThenBy(candidate => candidate.Color.Blue)
                .ToImmutableArray(),
            groupingDuration,
            componentDuration,
            localContrastDuration,
            admissionDuration,
            rankingDuration,
            Stopwatch.GetElapsedTime(totalClock));
        return new NotableColorSelectionResult(colors, diagnosticsResult);
    }

    private static int[] CreateSampleGroups(ReadOnlySpan<ushort> sampleBins, int[] binToGroup)
    {
        var sampleGroups = new int[sampleBins.Length];
        for (var index = 0; index < sampleBins.Length; index++)
        {
            sampleGroups[index] = sampleBins[index] == ushort.MaxValue
                ? -1
                : binToGroup[sampleBins[index]];
        }

        return sampleGroups;
    }

    private static SpatialEvidence[] MeasureComponents(
        PixelSize size,
        int[] sampleGroups,
        ReadOnlySpan<byte> sampleAlpha,
        int groupCount)
    {
        var evidence = Enumerable.Range(0, groupCount)
            .Select(_ => new SpatialEvidence())
            .ToArray();
        var visited = new bool[sampleGroups.Length];
        var queue = new int[sampleGroups.Length];
        var occupiedCells = new bool[groupCount * SpatialCellColumns * SpatialCellRows];
        var totalWeight = 0d;
        for (var index = 0; index < sampleAlpha.Length; index++)
        {
            totalWeight += sampleAlpha[index] / 255d;
            var group = sampleGroups[index];
            if (group < 0)
            {
                continue;
            }

            var x = index % size.Width;
            var y = index / size.Width;
            var cellX = Math.Min(SpatialCellColumns - 1, x * SpatialCellColumns / size.Width);
            var cellY = Math.Min(SpatialCellRows - 1, y * SpatialCellRows / size.Height);
            occupiedCells[(group * SpatialCellColumns * SpatialCellRows) +
                          (cellY * SpatialCellColumns) + cellX] = true;
        }

        if (totalWeight <= 0)
        {
            return evidence;
        }

        for (var group = 0; group < groupCount; group++)
        {
            var offset = group * SpatialCellColumns * SpatialCellRows;
            evidence[group].SpatialCellOccupancy = occupiedCells
                .AsSpan(offset, SpatialCellColumns * SpatialCellRows)
                .Count(true);
        }

        var minimumComponentWeight = totalWeight * MinimumMeasuredComponentFraction;
        for (var start = 0; start < sampleGroups.Length; start++)
        {
            var group = sampleGroups[start];
            if (visited[start] || group < 0)
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
                        if (!visited[neighbor] && sampleGroups[neighbor] == group)
                        {
                            visited[neighbor] = true;
                            queue[tail++] = neighbor;
                        }
                    }
                }
            }

            if (componentWeight >= minimumComponentWeight)
            {
                evidence[group].AddComponent(componentWeight / totalWeight);
            }
        }

        return evidence;
    }

    private static void MeasureLocalContrast(
        PixelSize size,
        int[] sampleGroups,
        ReadOnlySpan<byte> sampleAlpha,
        IReadOnlyList<ColorGroup> groups,
        SpatialEvidence[] evidence)
    {
        for (var y = 0; y < size.Height; y++)
        {
            for (var x = 0; x < size.Width; x++)
            {
                var index = (y * size.Width) + x;
                var group = sampleGroups[index];
                if (group < 0)
                {
                    continue;
                }

                if (x + 1 < size.Width)
                {
                    AddBoundaryEvidence(
                        index,
                        index + 1,
                        sampleGroups,
                        sampleAlpha,
                        groups,
                        evidence);
                }

                if (y + 1 < size.Height)
                {
                    AddBoundaryEvidence(
                        index,
                        index + size.Width,
                        sampleGroups,
                        sampleAlpha,
                        groups,
                        evidence);
                }
            }
        }

        foreach (var item in evidence)
        {
            item.CompleteLocalContrast();
        }
    }

    private static void AddBoundaryEvidence(
        int firstIndex,
        int secondIndex,
        int[] sampleGroups,
        ReadOnlySpan<byte> sampleAlpha,
        IReadOnlyList<ColorGroup> groups,
        SpatialEvidence[] evidence)
    {
        var firstGroup = sampleGroups[firstIndex];
        var secondGroup = sampleGroups[secondIndex];
        if (firstGroup < 0 || secondGroup < 0 || firstGroup == secondGroup)
        {
            return;
        }

        var weight = Math.Min(sampleAlpha[firstIndex], sampleAlpha[secondIndex]) / 255d;
        if (weight <= 0)
        {
            return;
        }

        var first = groups[firstGroup].Center;
        var second = groups[secondGroup].Center;
        var distance = Distance(first, second);
        var lightnessDistance = Math.Abs(first.L - second.L);
        evidence[firstGroup].AddBoundary(distance, lightnessDistance, weight);
        evidence[secondGroup].AddBoundary(distance, lightnessDistance, weight);
    }

    private static NotableColorAdmissionPath DetermineAdmissionPaths(
        double support,
        PhotoStylingOklab lab,
        double globalNovelty,
        double averageLightnessContrast,
        SpatialEvidence evidence)
    {
        if (support < HardMinimumSupportFraction)
        {
            return NotableColorAdmissionPath.None;
        }

        var routes = NotableColorAdmissionPath.None;
        if (support >= 0.035 && evidence.LargestComponent >= 0.012 &&
            lab.Chroma >= 0.025 && globalNovelty >= 0.055 && evidence.LocalContrast >= 0.055)
        {
            routes |= NotableColorAdmissionPath.SubstantialCoherentMass;
        }

        var vividCompactAccent = lab.Chroma >= 0.10 &&
                                 globalNovelty >= 0.085 &&
                                 evidence.LocalContrast >= 0.12;
        var moderateCompactAccent = support >= 0.0045 &&
                                    evidence.LargestComponent >= 0.002 &&
                                    lab.Chroma >= 0.08 &&
                                    globalNovelty >= 0.10 &&
                                    evidence.LocalContrast >= 0.145;
        if (support >= 0.002 && evidence.LargestComponent >= 0.0015 &&
            (vividCompactAccent || moderateCompactAccent))
        {
            routes |= NotableColorAdmissionPath.CompactChromaticAccent;
        }

        if (support >= 0.010 && evidence.LargestComponent >= Math.Max(0.006, support * 0.55) &&
            lab.Chroma <= 0.055 && averageLightnessContrast >= 0.16 &&
            evidence.LocalLightnessContrast >= 0.22)
        {
            routes |= NotableColorAdmissionPath.LightnessContrastNeutral;
        }

        if (support >= 0.009 && evidence.ComponentCount >= 3 &&
            evidence.TopComponentSupport >= 0.003 && evidence.CoherentSupport >= 0.008 &&
            evidence.SpatialCellOccupancy >= 3 && globalNovelty >= 0.055 &&
            evidence.LocalContrast >= 0.055 && lab.Chroma >= 0.035)
        {
            routes |= NotableColorAdmissionPath.DistributedRepeatedStructure;
        }

        if (support >= 0.018 && evidence.LargestComponent >= 0.006 &&
            lab.Chroma >= 0.025 && globalNovelty >= 0.065 && evidence.LocalContrast >= 0.065)
        {
            routes |= NotableColorAdmissionPath.MutedDistinctMass;
        }

        return routes;
    }

    private static string DetermineRejectionReason(
        double support,
        NotableColorAdmissionPath routes,
        SpatialEvidence evidence)
    {
        if (routes != NotableColorAdmissionPath.None)
        {
            return string.Empty;
        }

        if (support < HardMinimumSupportFraction)
        {
            return "Support is below the hard noise floor.";
        }

        if (evidence.LargestComponent < 0.0015 && evidence.CoherentSupport < 0.008)
        {
            return "Spatial coherence is below every admission route.";
        }

        return "No admission route satisfied its combined perceptual and spatial evidence.";
    }

    private static double CalculateRankingScore(
        NotableColorAdmissionPath routes,
        double support,
        PhotoStylingOklab lab,
        double globalNovelty,
        double averageLightnessContrast,
        SpatialEvidence evidence)
    {
        var routeStrength = CalculateRouteStrength(routes, support, lab, globalNovelty, evidence);
        var novelty = Normalize(globalNovelty, 0.04, 0.24);
        var localContrast = Normalize(evidence.LocalContrast, 0.04, 0.24);
        var spatial = Math.Max(
            Normalize(evidence.LargestComponent, 0.0015, 0.055),
            Math.Max(
                Normalize(evidence.TopComponentSupport, 0.004, 0.075),
                Normalize(evidence.SpatialCellOccupancy, 1, 12)));
        var perceptualStrength = Math.Max(
            Normalize(lab.Chroma, 0.025, 0.18),
            Math.Max(
                Normalize(evidence.LocalLightnessContrast, 0.08, 0.42),
                Normalize(averageLightnessContrast, 0.08, 0.42)));
        var mass = Math.Sqrt(Math.Clamp(support / 0.12, 0, 1));
        return Math.Clamp(
            (0.28 * routeStrength) +
            (0.20 * novelty) +
            (0.20 * localContrast) +
            (0.12 * spatial) +
            (0.12 * perceptualStrength) +
            (0.08 * mass),
            0,
            1);
    }

    private static double CalculateRouteStrength(
        NotableColorAdmissionPath routes,
        double support,
        PhotoStylingOklab lab,
        double globalNovelty,
        SpatialEvidence evidence)
    {
        var strength = 0d;
        if (routes.HasFlag(NotableColorAdmissionPath.SubstantialCoherentMass))
        {
            strength = Math.Max(strength, Average(
                Normalize(support, 0.035, 0.14),
                Normalize(evidence.LargestComponent, 0.012, 0.10),
                Normalize(globalNovelty, 0.055, 0.22),
                Normalize(evidence.LocalContrast, 0.055, 0.22)));
        }

        if (routes.HasFlag(NotableColorAdmissionPath.CompactChromaticAccent))
        {
            strength = Math.Max(strength, Average(
                Normalize(lab.Chroma, 0.10, 0.24),
                Normalize(globalNovelty, 0.085, 0.28),
                Normalize(evidence.LocalContrast, 0.12, 0.32),
                Normalize(evidence.LargestComponent, 0.0015, 0.025)));
        }

        if (routes.HasFlag(NotableColorAdmissionPath.LightnessContrastNeutral))
        {
            strength = Math.Max(strength, Average(
                Normalize(evidence.LocalLightnessContrast, 0.22, 0.65),
                Normalize(evidence.LargestComponent, 0.006, 0.10),
                Normalize(support, 0.010, 0.16)));
        }

        if (routes.HasFlag(NotableColorAdmissionPath.DistributedRepeatedStructure))
        {
            strength = Math.Max(strength, Average(
                Normalize(evidence.ComponentCount, 3, 9),
                Normalize(evidence.TopComponentSupport, 0.006, 0.07),
                Normalize(evidence.SpatialCellOccupancy, 3, 18),
                Normalize(evidence.LocalContrast, 0.055, 0.24)));
        }

        if (routes.HasFlag(NotableColorAdmissionPath.MutedDistinctMass))
        {
            strength = Math.Max(strength, Average(
                Normalize(support, 0.018, 0.10),
                Normalize(lab.Chroma, 0.025, 0.12),
                Normalize(globalNovelty, 0.065, 0.20),
                Normalize(evidence.LocalContrast, 0.065, 0.22)));
        }

        return 0.45 + (0.55 * strength);
    }

    private static ImmutableArray<PhotoNotableColor> SelectPresentation(
        List<Candidate> candidates,
        PhotoStylingOklab representative,
        IReadOnlyList<FrequentShadeEvidence> frequent)
    {
        var qualified = candidates
            .Where(candidate => candidate.Diagnostics.AdmissionPaths != NotableColorAdmissionPath.None)
            .OrderByDescending(candidate => Math.Round(candidate.Notable.Score, 12))
            .ThenByDescending(candidate => Math.Round(candidate.Notable.SupportFraction, 12))
            .ThenBy(candidate => candidate.Notable.Color.Red)
            .ThenBy(candidate => candidate.Notable.Color.Green)
            .ThenBy(candidate => candidate.Notable.Color.Blue)
            .Take(MaximumShortlist)
            .ToList();
        var selected = new List<Candidate>(MaximumColors);
        while (qualified.Count > 0 && selected.Count < MaximumColors)
        {
            Candidate? best = null;
            var bestScore = double.MinValue;
            var bestPenalty = 0d;
            foreach (var candidate in qualified)
            {
                if (IsAchromatic(candidate.Lab) && selected.Any(existing => IsAchromatic(existing.Lab)))
                {
                    continue;
                }

                var nearestSelected = selected.Count == 0
                    ? double.MaxValue
                    : selected.Min(existing => Distance(candidate.Lab, existing.Lab));
                if (nearestSelected < DuplicateDistance)
                {
                    continue;
                }

                var representativeNovelty = Normalize(Distance(candidate.Lab, representative), 0.04, 0.16);
                var frequentNovelty = Normalize(
                    frequent.Min(shade => Distance(candidate.Lab, shade.Lab)),
                    0.025,
                    0.14);
                var selectedNovelty = selected.Count == 0
                    ? 1
                    : Normalize(nearestSelected, DuplicateDistance, 0.18);
                var representativeFactor = 0.68 + (0.32 * representativeNovelty);
                var frequentFactor = 0.45 + (0.55 * frequentNovelty);
                var selectedFactor = 0.62 + (0.38 * selectedNovelty);
                var presentationScore = candidate.Notable.Score *
                                        representativeFactor *
                                        frequentFactor *
                                        selectedFactor;
                if (presentationScore > bestScore)
                {
                    best = candidate;
                    bestScore = presentationScore;
                    bestPenalty = 1 - (representativeFactor * frequentFactor * selectedFactor);
                }
            }

            if (best is null || bestScore < MinimumPresentationScore)
            {
                break;
            }

            best.Diagnostics = best.Diagnostics with
            {
                NearDuplicatePenalty = bestPenalty,
                PresentationScore = bestScore,
                Selected = true,
                SelectionReason = "Selected as the strongest remaining incremental color information."
            };
            selected.Add(best);
            qualified.Remove(best);
        }

        foreach (var candidate in qualified)
        {
            var nearest = selected.Count == 0
                ? double.MaxValue
                : selected.Min(existing => Distance(candidate.Lab, existing.Lab));
            var duplicate = nearest < DuplicateDistance;
            candidate.Diagnostics = candidate.Diagnostics with
            {
                NearDuplicatePenalty = duplicate ? 1 : candidate.Diagnostics.NearDuplicatePenalty,
                SelectionReason = duplicate
                    ? "Not selected because a perceptually near-duplicate candidate is already shown."
                    : "Not selected because incremental information fell below the presentation threshold."
            };
        }

        return selected.Select(candidate => candidate.Notable).ToImmutableArray();
    }

    private static double CalculateFrequentOverlap(
        PhotoStylingOklab candidate,
        IReadOnlyList<FrequentShadeEvidence> frequent)
    {
        var totalWeight = frequent.Sum(shade => shade.Weight);
        if (totalWeight <= 0)
        {
            return 0;
        }

        var overlap = frequent.Sum(shade =>
            shade.Weight * (1 - Normalize(Distance(candidate, shade.Lab), 0.025, 0.13)));
        return Math.Clamp(overlap / totalWeight, 0, 1);
    }

    private static double CalculateRepresentativeDuplicatePenalty(double distance) =>
        1 - Normalize(distance, 0.04, 0.16);

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

    private static bool BelongsToSamePerceptualFamily(
        PhotoStylingOklab first,
        PhotoStylingOklab second,
        double distance)
    {
        if (distance < GroupingDistance)
        {
            return true;
        }

        if (distance > 0.16 || first.Chroma < 0.04 || second.Chroma < 0.04 ||
            Math.Abs(first.L - second.L) > 0.23 ||
            Math.Abs(first.Chroma - second.Chroma) > 0.08)
        {
            return false;
        }

        var hueDifference = Math.Abs(
            Math.Atan2(first.B, first.A) - Math.Atan2(second.B, second.A));
        hueDifference = Math.Min(hueDifference, 2 * Math.PI - hueDifference);
        return hueDifference <= 0.24;
    }

    private static double Distance(PhotoStylingOklab first, PhotoStylingOklab second)
    {
        var deltaL = first.L - second.L;
        var deltaA = first.A - second.A;
        var deltaB = first.B - second.B;
        return Math.Sqrt((deltaL * deltaL) + (deltaA * deltaA) + (deltaB * deltaB));
    }

    private static bool IsAchromatic(PhotoStylingOklab color) => color.Chroma <= 0.04;

    private static double Normalize(double value, double minimum, double maximum) =>
        Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);

    private static double Average(params double[] values) => values.Average();

    private sealed class Candidate(
        PhotoNotableColor notable,
        PhotoStylingOklab lab,
        NotableColorCandidateDiagnostics diagnostics)
    {
        public PhotoNotableColor Notable { get; } = notable;

        public PhotoStylingOklab Lab { get; } = lab;

        public NotableColorCandidateDiagnostics Diagnostics { get; set; } = diagnostics;
    }

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

    private sealed class SpatialEvidence
    {
        private double _secondComponent;
        private double _thirdComponent;
        private double _boundaryWeight;
        private double _weightedContrast;
        private double _weightedLightnessContrast;

        public double LargestComponent { get; private set; }

        public double TopComponentSupport => LargestComponent + _secondComponent + _thirdComponent;

        public double CoherentSupport { get; private set; }

        public int ComponentCount { get; private set; }

        public int SpatialCellOccupancy { get; set; }

        public double LocalContrast { get; private set; }

        public double LocalLightnessContrast { get; private set; }

        public double RepeatedStructureEvidence => Math.Max(
            0,
            Math.Min(CoherentSupport - LargestComponent, TopComponentSupport - LargestComponent));

        public void AddComponent(double componentFraction)
        {
            ComponentCount++;
            CoherentSupport += componentFraction;
            if (componentFraction > LargestComponent)
            {
                _thirdComponent = _secondComponent;
                _secondComponent = LargestComponent;
                LargestComponent = componentFraction;
            }
            else if (componentFraction > _secondComponent)
            {
                _thirdComponent = _secondComponent;
                _secondComponent = componentFraction;
            }
            else if (componentFraction > _thirdComponent)
            {
                _thirdComponent = componentFraction;
            }
        }

        public void AddBoundary(double contrast, double lightnessContrast, double weight)
        {
            _boundaryWeight += weight;
            _weightedContrast += contrast * weight;
            _weightedLightnessContrast += lightnessContrast * weight;
        }

        public void CompleteLocalContrast()
        {
            if (_boundaryWeight <= 0)
            {
                return;
            }

            LocalContrast = _weightedContrast / _boundaryWeight;
            LocalLightnessContrast = _weightedLightnessContrast / _boundaryWeight;
        }
    }

    private readonly record struct FrequentShadeEvidence(PhotoStylingOklab Lab, double Weight);
}