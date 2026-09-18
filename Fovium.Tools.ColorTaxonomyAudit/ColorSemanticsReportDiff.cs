using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal sealed record ColorSemanticsDiff(
    string Schema,
    ReportSignatures BeforeSignatures,
    ReportSignatures AfterSignatures,
    IReadOnlyList<string> AddedTerms,
    IReadOnlyList<string> RemovedTerms,
    IReadOnlyList<ReportChangedItem> ChangedTerms,
    IReadOnlyList<string> AddedRegions,
    IReadOnlyList<string> RemovedRegions,
    IReadOnlyList<ReportChangedItem> ChangedRegions,
    IReadOnlyList<ReportClassificationChange> ClassificationChanges,
    IReadOnlyList<string> AddedWarnings,
    IReadOnlyList<string> RemovedWarnings,
    IReadOnlyList<ReportDispositionChange> ResearchDispositionChanges)
{
    public bool IsEmpty => AddedTerms.Count == 0 && RemovedTerms.Count == 0 && ChangedTerms.Count == 0 &&
                           AddedRegions.Count == 0 && RemovedRegions.Count == 0 && ChangedRegions.Count == 0 &&
                           ClassificationChanges.Count == 0 && AddedWarnings.Count == 0 && RemovedWarnings.Count == 0 &&
                           ResearchDispositionChanges.Count == 0;
}

internal sealed record ReportChangedItem(string Id, IReadOnlyList<string> Fields);

internal sealed record ReportClassificationChange(
    string SampleId,
    ReportClassificationOutcome Before,
    ReportClassificationOutcome After);

internal sealed record ReportDispositionChange(string Candidate, string Before, string After);

internal static class ColorSemanticsReportDiffer
{
    public static ColorSemanticsDiff Compare(ColorSemanticsReport before, ColorSemanticsReport after)
    {
        var beforeTerms = before.ProfessionalTerms.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var afterTerms = after.ProfessionalTerms.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var beforeRegions = before.Regions.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var afterRegions = after.Regions.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var beforeOutcomes =
            before.Sampling.ClassificationOutcomes.ToDictionary(item => item.SampleId, StringComparer.Ordinal);
        var afterOutcomes =
            after.Sampling.ClassificationOutcomes.ToDictionary(item => item.SampleId, StringComparer.Ordinal);
        var beforeCandidates =
            before.Research.Candidates.ToDictionary(item => item.CanonicalTerm, StringComparer.Ordinal);
        var afterCandidates =
            after.Research.Candidates.ToDictionary(item => item.CanonicalTerm, StringComparer.Ordinal);

        return new ColorSemanticsDiff(
            "fovium-color-semantics-report-diff/v1",
            before.Signatures,
            after.Signatures,
            afterTerms.Keys.Except(beforeTerms.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            beforeTerms.Keys.Except(afterTerms.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            beforeTerms.Keys.Intersect(afterTerms.Keys, StringComparer.Ordinal)
                .Select(id => ChangedTerm(id, beforeTerms[id], afterTerms[id]))
                .Where(change => change.Fields.Count > 0)
                .OrderBy(change => change.Id, StringComparer.Ordinal)
                .ToArray(),
            afterRegions.Keys.Except(beforeRegions.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal)
                .ToArray(),
            beforeRegions.Keys.Except(afterRegions.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal)
                .ToArray(),
            beforeRegions.Keys.Intersect(afterRegions.Keys, StringComparer.Ordinal)
                .Select(id => ChangedRegion(id, beforeRegions[id], afterRegions[id]))
                .Where(change => change.Fields.Count > 0)
                .OrderBy(change => change.Id, StringComparer.Ordinal)
                .ToArray(),
            beforeOutcomes.Keys.Intersect(afterOutcomes.Keys, StringComparer.Ordinal)
                .Where(id => beforeOutcomes[id] != afterOutcomes[id])
                .Order(StringComparer.Ordinal)
                .Select(id => new ReportClassificationChange(id, beforeOutcomes[id], afterOutcomes[id]))
                .ToArray(),
            after.Warnings.Select(item => item.Id)
                .Except(before.Warnings.Select(item => item.Id), StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray(),
            before.Warnings.Select(item => item.Id)
                .Except(after.Warnings.Select(item => item.Id), StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray(),
            beforeCandidates.Keys.Intersect(afterCandidates.Keys, StringComparer.Ordinal)
                .Where(id => beforeCandidates[id].Disposition != afterCandidates[id].Disposition)
                .Order(StringComparer.Ordinal)
                .Select(id => new ReportDispositionChange(
                    id,
                    beforeCandidates[id].Disposition,
                    afterCandidates[id].Disposition))
                .ToArray());
    }

    private static ReportChangedItem ChangedTerm(
        string id,
        ReportProfessionalTerm before,
        ReportProfessionalTerm after)
    {
        var fields = new List<string>();
        Add("identity", before.Identity, after.Identity);
        Add("domain", before.Domain, after.Domain);
        Add("regionIds", before.RegionIds, after.RegionIds);
        Add("parentFamilyIds", before.ParentFamilyIds, after.ParentFamilyIds);
        Add("representativeCore", before.RepresentativeCore, after.RepresentativeCore);
        Add("reachabilityWitness", before.ReachabilityWitness, after.ReachabilityWitness);
        return new ReportChangedItem(id, fields);

        void Add<T>(string field, T left, T right)
        {
            if (CanonicalSemanticIdentity.Hash(left) != CanonicalSemanticIdentity.Hash(right))
            {
                fields.Add(field);
            }
        }
    }

    private static ReportChangedItem ChangedRegion(string id, ReportRegion before, ReportRegion after)
    {
        var fields = new List<string>();
        Add("termId", before.TermId, after.TermId);
        Add("roles", before.Roles, after.Roles);
        Add("parentFamilyIds", before.ParentFamilyIds, after.ParentFamilyIds);
        Add("lightness", before.Lightness, after.Lightness);
        Add("chroma", before.Chroma, after.Chroma);
        Add("hue", before.Hue, after.Hue);
        Add("priority", before.Priority, after.Priority);
        Add("representativeCore", before.RepresentativeCore, after.RepresentativeCore);
        Add("reachabilityWitness", before.ReachabilityWitness, after.ReachabilityWitness);
        return new ReportChangedItem(id, fields);

        void Add<T>(string field, T left, T right)
        {
            if (CanonicalSemanticIdentity.Hash(left) != CanonicalSemanticIdentity.Hash(right))
            {
                fields.Add(field);
            }
        }
    }
}

internal static class ColorSemanticsReportDiffApplication
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (!TryParse(args, out var beforePath, out var afterPath, out var outputDirectory, out var parseError))
        {
            error.WriteLine(parseError);
            error.WriteLine("Usage: compare --before <taxonomy.json> --after <taxonomy.json> --output <dir>");
            return 2;
        }

        try
        {
            var before = Read(beforePath!);
            var after = Read(afterPath!);
            var diff = ColorSemanticsReportDiffer.Compare(before, after);
            Directory.CreateDirectory(outputDirectory!);
            var jsonPath = Path.Combine(outputDirectory!, "report-diff.json");
            var markdownPath = Path.Combine(outputDirectory!, "report-diff.md");
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(diff, JsonOptions) + "\n", new UTF8Encoding(false));
            File.WriteAllText(markdownPath, BuildMarkdown(diff), new UTF8Encoding(false));
            output.WriteLine(
                $"Color Semantics report diff: {(diff.IsEmpty ? "no semantic changes" : "changes detected")}");
            output.WriteLine($"Artifacts: {jsonPath}; {markdownPath}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
        {
            error.WriteLine(exception.Message);
            return 1;
        }
    }

    internal static string BuildMarkdown(ColorSemanticsDiff diff)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Fovium Color Semantics report diff");
        builder.AppendLine();
        builder.AppendLine(
            $"- Definition changed: {diff.BeforeSignatures.ProductionDefinition != diff.AfterSignatures.ProductionDefinition}");
        builder.AppendLine(
            $"- Classification outcomes changed: {diff.BeforeSignatures.ClassificationOutcomes != diff.AfterSignatures.ClassificationOutcomes}");
        builder.AppendLine($"- Added/removed terms: {diff.AddedTerms.Count}/{diff.RemovedTerms.Count}");
        builder.AppendLine($"- Added/removed lobes: {diff.AddedRegions.Count}/{diff.RemovedRegions.Count}");
        builder.AppendLine($"- Changed term/lobe definitions: {diff.ChangedTerms.Count}/{diff.ChangedRegions.Count}");
        builder.AppendLine($"- Changed fixed classification outcomes: {diff.ClassificationChanges.Count}");
        builder.AppendLine();
        foreach (var change in diff.ChangedRegions)
        {
            builder.AppendLine($"- `{change.Id}`: {string.Join(", ", change.Fields)}");
        }

        return builder.ToString();
    }

    private static ColorSemanticsReport Read(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Report does not exist: {path}");
        }

        return JsonSerializer.Deserialize<ColorSemanticsReport>(File.ReadAllText(path), JsonOptions)
               ?? throw new InvalidDataException($"Report is invalid: {path}");
    }

    private static bool TryParse(
        IReadOnlyList<string> args,
        out string? before,
        out string? after,
        out string? output,
        out string? error)
    {
        before = null;
        after = null;
        output = null;
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if ((argument is "--before" or "--after" or "--output") && index + 1 < args.Count)
            {
                var value = args[++index];
                if (argument == "--before") before = value;
                else if (argument == "--after") after = value;
                else output = value;
            }
            else
            {
                error = $"Unknown or incomplete compare option: {argument}";
                return false;
            }
        }

        error = before is null || after is null || output is null
            ? "Compare requires --before, --after, and --output."
            : null;
        return error is null;
    }
}