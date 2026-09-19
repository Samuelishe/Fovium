using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fovium.ColorSemantics;
using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Localization;
using Fovium.Metadata;
using Fovium.PhotoStyling;
using Fovium.Rendering;
using Fovium.Stage;
using SkiaSharp;
using Xunit.Abstractions;

namespace Fovium.Tests.PhotoStyling;

public sealed class PhotoColorProfilePerformanceSmokeTests(ITestOutputHelper output)
{
    private const string ImagePathsVariable = "FOVIUM_PHOTO_COLOR_PROFILE_IMAGES";
    private const string OutputDirectoryVariable = "FOVIUM_PHOTO_COLOR_PROFILE_OUTPUT";
    private const string IdentifiersVariable = "FOVIUM_PHOTO_COLOR_PROFILE_IDENTIFIERS";
    private const string ResolutionsVariable = "FOVIUM_PHOTO_COLOR_PROFILE_RESOLUTIONS";
    private const int RowsPerContactSheet = 8;
    private const int RowsPerDiagnosticsSheet = 5;

    [Fact]
    public async Task OptInRealPhotosReportProjectionCostAndRenderContactSheet()
    {
        var paths = (Environment.GetEnvironmentVariable(ImagePathsVariable) ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (paths.Length == 0)
        {
            output.WriteLine($"Set {ImagePathsVariable} to enable local Photo Color Profile evidence.");
            return;
        }

        Assert.All(paths, path => Assert.True(File.Exists(path), path));
        var identifiers = ParseIdentifiers(paths);
        var resolutions = ParseResolutions();
        var outputDirectory = Environment.GetEnvironmentVariable(OutputDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            PrepareOutputDirectory(outputDirectory);
        }

        using var decoder = ImageDecoder.CreateDefault();
        var evidence = new List<EvidenceRow>();
        try
        {
            foreach (var path in paths)
            {
                var decodeClock = Stopwatch.StartNew();
                var loaded = await decoder.LoadAsync(
                    path,
                    new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                    CancellationToken.None);
                decodeClock.Stop();
                Assert.True(loaded.IsSuccess, loaded.Error?.TechnicalDetail);
                var image = Assert.IsType<DecodedImage>(loaded.Image);
                var analysis = Assert.IsType<PhotoStyleAnalysis>(image.GetPhotoStyleAnalysis());
                var profile = Assert.IsType<PhotoColorProfile>(image.GetPhotoColorProfile());
                var diagnosticResult = new PhotoStyleAnalyzer().AnalyzeWithDiagnostics(
                    image,
                    CancellationToken.None);
                var projectionMicroseconds = MeasureProjection(analysis);
                output.WriteLine(
                    "Photo {0:D2} ({1}): decode+analysis+profile={2:F2} ms, analysis={3:F2} ms, " +
                    "profileProjection={4:F2} us, analysisRetained={5:N0} B, " +
                    "profileRetained={6:N0} B, palette={7}, notable={8}, visibleSamples={9:N0}; " +
                    "diagnostic grouping/components/local/admission/ranking/full={10:F2}/{11:F2}/{12:F2}/" +
                    "{13:F2}/{14:F2}/{15:F2} ms.",
                    evidence.Count + 1,
                    Path.GetFileName(path),
                    decodeClock.Elapsed.TotalMilliseconds,
                    analysis.AnalysisDuration.TotalMilliseconds,
                    projectionMicroseconds,
                    analysis.RetainedBytes,
                    profile.RetainedBytes,
                    profile.Palette.Length,
                    profile.NotableColors.Length,
                    analysis.VisibleSampleCount,
                    diagnosticResult.Diagnostics.Notable.GroupingDuration.TotalMilliseconds,
                    diagnosticResult.Diagnostics.Notable.ComponentDuration.TotalMilliseconds,
                    diagnosticResult.Diagnostics.Notable.LocalContrastDuration.TotalMilliseconds,
                    diagnosticResult.Diagnostics.Notable.AdmissionDuration.TotalMilliseconds,
                    diagnosticResult.Diagnostics.Notable.RankingDuration.TotalMilliseconds,
                    diagnosticResult.Diagnostics.Notable.TotalDuration.TotalMilliseconds);
                var resolutionEvidence = resolutions
                    .Select(resolution => CreateResolutionEvidence(image, resolution, diagnosticResult))
                    .ToArray();
                evidence.Add(new EvidenceRow(
                    image,
                    identifiers[evidence.Count],
                    profile,
                    diagnosticResult.Diagnostics.Notable,
                    resolutionEvidence,
                    CreateAlgorithmEvidence(resolutionEvidence)));
            }

            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                WriteContactSheets(evidence, outputDirectory);
                WriteDiagnosticsSheets(evidence, outputDirectory);
                if (resolutions.Length > 0)
                {
                    WriteResolutionComparisonSheets(evidence, outputDirectory);
                }

                if (evidence.Any(row => row.Algorithms.Length > 0))
                {
                    WriteAlgorithmComparisonSheets(evidence, outputDirectory);
                }

                WriteDiagnosticsJson(evidence, Path.Combine(outputDirectory, "notable-diagnostics.json"));
            }
        }
        finally
        {
            foreach (var row in evidence)
            {
                row.Image.Dispose();
            }
        }
    }

    private static string[] ParseIdentifiers(IReadOnlyList<string> paths)
    {
        var configured = (Environment.GetEnvironmentVariable(IdentifiersVariable) ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (configured.Length == 0)
        {
            return paths.Select(Path.GetFileName).ToArray()!;
        }

        if (configured.Length != paths.Count)
        {
            throw new InvalidOperationException(
                $"{IdentifiersVariable} must contain exactly one identifier per image path.");
        }

        return configured;
    }

    private static int[] ParseResolutions()
    {
        var configured = Environment.GetEnvironmentVariable(ResolutionsVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return [];
        }

        return configured
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture))
            .Distinct()
            .Order()
            .ToArray();
    }

    private static ResolutionEvidence CreateResolutionEvidence(
        DecodedImage image,
        int resolution,
        PhotoStyleAnalysisResult ninetySixResult)
    {
        var result = resolution == StageDefaults.PhotoStyleLongEdgePixels
            ? ninetySixResult
            : PhotoStyleAnalyzer.AnalyzeWithDiagnostics(image, resolution, CancellationToken.None);
        var sampleCount = checked(result.Analysis.AnalyzedSize.Width * result.Analysis.AnalyzedSize.Height);
        var estimatedWorkspaceBytes = checked(160_000L + (sampleCount * 16L));
        return new ResolutionEvidence(
            resolution,
            result.Analysis.AnalyzedSize,
            result.Analysis.AnalysisDuration,
            estimatedWorkspaceBytes,
            result.Analysis.NotableColors,
            result.Diagnostics.Notable);
    }

    private static AlgorithmEvidence[] CreateAlgorithmEvidence(
        IReadOnlyList<ResolutionEvidence> resolutions)
    {
        var ninetySix =
            resolutions.FirstOrDefault(item => item.LongEdgePixels == StageDefaults.PhotoStyleLongEdgePixels);
        var oneNinetyTwo = resolutions.FirstOrDefault(item => item.LongEdgePixels == 192);
        if (ninetySix is null || oneNinetyTwo is null)
        {
            return [];
        }

        return
        [
            new AlgorithmEvidence("Current 96", ninetySix.NotableColors.Select(color => color.Color).ToArray()),
            new AlgorithmEvidence("Current 192", oneNinetyTwo.NotableColors.Select(color => color.Color).ToArray()),
            new AlgorithmEvidence("Classical salience", SelectPrototype(ninetySix.Diagnostics, regionOriented: false)),
            new AlgorithmEvidence("Region-oriented", SelectPrototype(ninetySix.Diagnostics, regionOriented: true)),
        ];
    }

    private static StageColor[] SelectPrototype(
        NotableColorSelectionDiagnostics diagnostics,
        bool regionOriented)
    {
        var candidates = diagnostics.Candidates
            .Where(candidate => candidate.SupportFraction >= 0.002)
            .Select(candidate =>
            {
                var contrastWeight = diagnostics.Candidates
                    .Where(other => other != candidate)
                    .Sum(other => other.SupportFraction);
                var globalContrast = contrastWeight <= 0
                    ? 0
                    : diagnostics.Candidates
                          .Where(other => other != candidate)
                          .Sum(other => other.SupportFraction * PerceptualDistance(candidate, other)) /
                      contrastWeight;
                var mass = Math.Sqrt(Math.Clamp(candidate.SupportFraction / 0.12, 0, 1));
                var chroma = Normalize(candidate.Chroma, 0.015, 0.16);
                var global = Normalize(globalContrast, 0.045, 0.22);
                var local = Normalize(candidate.LocalContrast, 0.055, 0.24);
                var coherence = Math.Max(
                    Normalize(candidate.LargestComponentFraction, 0.0015, 0.08),
                    Math.Max(
                        Normalize(candidate.TopComponentSupportFraction, 0.004, 0.10),
                        candidate.RepeatedStructureEvidence));
                var score = regionOriented
                    ? (0.34 * coherence) +
                      (0.28 * local) +
                      (0.20 * global) +
                      (0.10 * mass) +
                      (0.08 * chroma)
                    : (0.34 * global) +
                      (0.25 * local) +
                      (0.18 * mass) +
                      (0.13 * chroma) +
                      (0.10 * Normalize(candidate.GlobalNovelty, 0.035, 0.20));
                var eligible = regionOriented
                    ? candidate.LocalContrast >= 0.075 &&
                      (candidate.LargestComponentFraction >= 0.0015 ||
                       candidate.TopComponentSupportFraction >= 0.004)
                    : globalContrast >= 0.050 && candidate.LocalContrast >= 0.060;
                var informationFactor =
                    (0.78 + (0.22 * Normalize(candidate.GlobalNovelty, 0.03, 0.18))) *
                    (0.72 + (0.28 * (1 - candidate.FrequentOverlap)));
                return new PrototypeCandidate(
                    candidate,
                    eligible ? score * informationFactor : 0);
            })
            .Where(candidate => candidate.Score >= 0.30)
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Diagnostics.SupportFraction)
            .ToList();
        var selected = new List<PrototypeCandidate>(NotableColorSelector.MaximumColors);
        foreach (var candidate in candidates)
        {
            if (selected.Any(existing => PerceptualDistance(
                    candidate.Diagnostics,
                    existing.Diagnostics) < NotableColorSelector.DuplicateDistance))
            {
                continue;
            }

            if (candidate.Diagnostics.Chroma < 0.025)
            {
                var achromatic = selected
                    .Where(existing => existing.Diagnostics.Chroma < 0.025)
                    .ToArray();
                if (achromatic.Length >= 2 || achromatic.Any(existing =>
                        Math.Abs(existing.Diagnostics.OklabLightness -
                                 candidate.Diagnostics.OklabLightness) < 0.24))
                {
                    continue;
                }
            }

            selected.Add(candidate);
            if (selected.Count == NotableColorSelector.MaximumColors)
            {
                break;
            }
        }

        return selected.Select(candidate => candidate.Diagnostics.Color).ToArray();
    }

    private static double PerceptualDistance(
        NotableColorCandidateDiagnostics first,
        NotableColorCandidateDiagnostics second)
    {
        var deltaL = first.OklabLightness - second.OklabLightness;
        var deltaA = first.OklabA - second.OklabA;
        var deltaB = first.OklabB - second.OklabB;
        return Math.Sqrt((deltaL * deltaL) + (deltaA * deltaA) + (deltaB * deltaB));
    }

    private static double Normalize(double value, double minimum, double maximum) =>
        Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);

    private static void PrepareOutputDirectory(string outputDirectory)
    {
        const string markerName = ".fovium-photo-profile-evidence";
        var fullPath = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(fullPath))
        {
            var marker = Path.Combine(fullPath, markerName);
            if (!File.Exists(marker) && Directory.EnumerateFileSystemEntries(fullPath).Any())
            {
                throw new InvalidOperationException(
                    $"Refusing to clean unmarked non-empty evidence directory: {fullPath}");
            }

            if (File.Exists(marker))
            {
                Directory.Delete(fullPath, recursive: true);
            }
        }

        Directory.CreateDirectory(fullPath);
        File.WriteAllText(
            Path.Combine(fullPath, markerName),
            "Owned by PhotoColorProfilePerformanceSmokeTests; safe to recreate." + Environment.NewLine);
    }

    private static double MeasureProjection(PhotoStyleAnalysis analysis)
    {
        const int batches = 7;
        const int iterations = 25;
        var projector = new PhotoColorProfileProjector();
        _ = projector.Create(analysis);
        var timings = new double[batches];
        for (var batch = 0; batch < batches; batch++)
        {
            var clock = Stopwatch.StartNew();
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                _ = projector.Create(analysis);
            }

            clock.Stop();
            timings[batch] = clock.Elapsed.TotalMicroseconds / iterations;
        }

        Array.Sort(timings);
        return timings[timings.Length / 2];
    }

    private static void WriteContactSheets(IReadOnlyList<EvidenceRow> rows, string outputDirectory)
    {
        for (var offset = 0; offset < rows.Count; offset += RowsPerContactSheet)
        {
            var page = (offset / RowsPerContactSheet) + 1;
            WriteContactSheet(
                rows.Skip(offset).Take(RowsPerContactSheet).ToArray(),
                Path.Combine(outputDirectory, $"photo-color-profile-contact-sheet-{page:D2}.png"));
        }
    }

    private static void WriteContactSheet(IReadOnlyList<EvidenceRow> rows, string destination)
    {
        const int width = 1800;
        const int rowHeight = 230;
        using var surface = SKSurface.Create(new SKImageInfo(
                                width,
                                Math.Max(rowHeight, rows.Count * rowHeight),
                                SKColorType.Bgra8888,
                                SKAlphaType.Premul,
                                SKColorSpace.CreateSrgb()))
                            ?? throw new InvalidOperationException(
                                "Skia could not allocate the profile evidence sheet.");
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(24, 25, 28));
        using var typeface = SKTypeface.FromFamilyName("Segoe UI");
        using var headingFont = new SKFont(typeface, 20);
        using var labelFont = new SKFont(typeface, 15);
        using var smallFont = new SKFont(typeface, 12);
        using var textPaint = new SKPaint { IsAntialias = true, Color = new SKColor(235, 237, 240) };
        using var mutedPaint = new SKPaint { IsAntialias = true, Color = new SKColor(170, 175, 182) };
        using var separatorPaint = new SKPaint { Color = new SKColor(65, 68, 74), StrokeWidth = 1 };
        var culture = CultureInfo.GetCultureInfo("en-US");
        var localizer = Localizer.Create(culture);
        var nameResolver = new PerceptualColorNameResolver(localizer);
        var creativeNames = ColorNameDisplayCatalog.ForLocale("en");

        for (var index = 0; index < rows.Count; index++)
        {
            var top = index * rowHeight;
            var row = rows[index];
            var thumbnailBounds = new RectD(16, top + 14, 280, 160);
            var photoBounds = Fit(row.Image.Descriptor.OrientedSize, thumbnailBounds);
            using (var lease = row.Image.AcquireRenderLease())
            {
                SkiaPhotoDrawOperation.DrawPhoto(
                    canvas,
                    lease.Image,
                    row.Image.Descriptor.EncodedSize,
                    row.Image.Descriptor.Orientation,
                    photoBounds,
                    false);
            }

            var presentation = PhotoColorProfilePresenter.Format(
                row.Profile,
                culture,
                nameResolver,
                creativeNames);
            canvas.DrawText(row.Identifier, 320, top + 34, headingFont, textPaint);
            canvas.DrawText("Characteristic", 320, top + 62, smallFont, mutedPaint);
            DrawSwatch(canvas, presentation.Dominant.Color, 320, top + 74, 52, 52);
            canvas.DrawText(presentation.Dominant.StructuralName, 384, top + 98, labelFont, textPaint);
            canvas.DrawText(presentation.Dominant.Hex, 384, top + 120, smallFont, mutedPaint);

            canvas.DrawText("Frequent shades", 620, top + 34, smallFont, mutedPaint);

            for (var paletteIndex = 0; paletteIndex < presentation.Palette.Length; paletteIndex++)
            {
                var entry = presentation.Palette[paletteIndex];
                var left = 620 + (paletteIndex * 112);
                DrawSwatch(canvas, entry.Color.Color, left, top + 44, 98, 62);
                canvas.DrawText(entry.Share, left, top + 126, smallFont, mutedPaint);
                canvas.DrawText(Truncate(entry.Color.StructuralName, 15), left, top + 148, smallFont, textPaint);
            }

            canvas.DrawText(
                $"Notable colors ({presentation.NotableColors.Length})",
                1190,
                top + 34,
                smallFont,
                mutedPaint);
            for (var notableIndex = 0; notableIndex < presentation.NotableColors.Length; notableIndex++)
            {
                var color = presentation.NotableColors[notableIndex];
                var rowIndex = notableIndex / PhotoColorProfileLayout.MaximumNotableColorsPerRow;
                var columnIndex = notableIndex % PhotoColorProfileLayout.MaximumNotableColorsPerRow;
                var left = 1190 + (columnIndex * 116);
                var swatchTop = top + 44 + (rowIndex * 82);
                DrawSwatch(canvas, color.Color, left, swatchTop, 102, 38);
                canvas.DrawText(
                    Truncate(color.StructuralName, 15),
                    left,
                    swatchTop + 55,
                    smallFont,
                    textPaint);
                canvas.DrawText(color.Hex, left, swatchTop + 72, smallFont, mutedPaint);
            }

            canvas.DrawLine(16, top + rowHeight - 1, width - 16, top + rowHeight - 1, separatorPaint);
        }

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 95);
        using var stream = File.Create(destination);
        encoded.SaveTo(stream);
    }

    private static void WriteDiagnosticsSheets(IReadOnlyList<EvidenceRow> rows, string outputDirectory)
    {
        for (var offset = 0; offset < rows.Count; offset += RowsPerDiagnosticsSheet)
        {
            var page = (offset / RowsPerDiagnosticsSheet) + 1;
            WriteDiagnosticsSheet(
                rows.Skip(offset).Take(RowsPerDiagnosticsSheet).ToArray(),
                Path.Combine(outputDirectory, $"notable-diagnostics-{page:D2}.png"));
        }
    }

    private static void WriteDiagnosticsSheet(IReadOnlyList<EvidenceRow> rows, string destination)
    {
        const int width = 1800;
        const int headingHeight = 42;
        const int candidateHeight = 24;
        const int candidatesPerPhoto = 12;
        var height = Math.Max(
            120,
            rows.Sum(row => headingHeight +
                            (Math.Min(candidatesPerPhoto, row.Diagnostics.Candidates.Length) * candidateHeight)));
        using var surface = SKSurface.Create(new SKImageInfo(
                                width,
                                height,
                                SKColorType.Bgra8888,
                                SKAlphaType.Premul,
                                SKColorSpace.CreateSrgb()))
                            ?? throw new InvalidOperationException(
                                "Skia could not allocate the notable diagnostics sheet.");
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(24, 25, 28));
        using var typeface = SKTypeface.FromFamilyName("Consolas");
        using var headingFont = new SKFont(typeface, 17);
        using var detailFont = new SKFont(typeface, 12);
        using var textPaint = new SKPaint { IsAntialias = true, Color = new SKColor(225, 228, 232) };
        using var mutedPaint = new SKPaint { IsAntialias = true, Color = new SKColor(165, 171, 179) };
        var top = 0f;
        foreach (var row in rows)
        {
            canvas.DrawText(
                $"{row.Identifier}  selected={row.Profile.NotableColors.Length}",
                16,
                top + 25,
                headingFont,
                textPaint);
            top += headingHeight;
            foreach (var candidate in row.Diagnostics.Candidates.Take(candidatesPerPhoto))
            {
                DrawSwatch(canvas, candidate.Color, 16, top + 3, 38, 16);
                var routes = candidate.AdmissionPaths == NotableColorAdmissionPath.None
                    ? "none"
                    : candidate.AdmissionPaths.ToString();
                var disposition = candidate.Selected ? "SELECT" : "skip";
                var text = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{candidate.Color.ToHex()} {disposition,-6} support={candidate.SupportFraction,6:P1} " +
                    $"largest={candidate.LargestComponentFraction,6:P1} top3={candidate.TopComponentSupportFraction,6:P1} " +
                    $"components={candidate.ComponentCount,2} cells={candidate.SpatialCellOccupancy,2} " +
                    $"C={candidate.Chroma:F3} novelty={candidate.GlobalNovelty:F3} " +
                    $"local={candidate.LocalContrast:F3} dL={candidate.LocalLightnessContrast:F3} " +
                    $"rank={candidate.RankingScore:F3} present={candidate.PresentationScore:F3} routes={routes}");
                canvas.DrawText(text, 66, top + 17, detailFont, candidate.Selected ? textPaint : mutedPaint);
                top += candidateHeight;
            }
        }

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 95);
        using var stream = File.Create(destination);
        encoded.SaveTo(stream);
    }

    private static void WriteResolutionComparisonSheets(
        IReadOnlyList<EvidenceRow> rows,
        string outputDirectory)
    {
        const int rowsPerPage = 6;
        for (var offset = 0; offset < rows.Count; offset += rowsPerPage)
        {
            var page = (offset / rowsPerPage) + 1;
            WriteResolutionComparisonSheet(
                rows.Skip(offset).Take(rowsPerPage).ToArray(),
                Path.Combine(outputDirectory, $"resolution-comparison-{page:D2}.png"));
        }
    }

    private static void WriteResolutionComparisonSheet(
        IReadOnlyList<EvidenceRow> rows,
        string destination)
    {
        const int width = 1800;
        const int rowHeight = 250;
        using var surface = SKSurface.Create(new SKImageInfo(
                                width,
                                Math.Max(rowHeight, rows.Count * rowHeight),
                                SKColorType.Bgra8888,
                                SKAlphaType.Premul,
                                SKColorSpace.CreateSrgb()))
                            ?? throw new InvalidOperationException(
                                "Skia could not allocate the resolution evidence sheet.");
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(24, 25, 28));
        using var typeface = SKTypeface.FromFamilyName("Segoe UI");
        using var headingFont = new SKFont(typeface, 16);
        using var detailFont = new SKFont(typeface, 12);
        using var textPaint = new SKPaint { IsAntialias = true, Color = new SKColor(235, 237, 240) };
        using var mutedPaint = new SKPaint { IsAntialias = true, Color = new SKColor(170, 175, 182) };
        using var separatorPaint = new SKPaint { Color = new SKColor(65, 68, 74), StrokeWidth = 1 };

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var top = rowIndex * rowHeight;
            var thumbnailBounds = new RectD(16, top + 38, 260, 180);
            var photoBounds = Fit(row.Image.Descriptor.OrientedSize, thumbnailBounds);
            using (var lease = row.Image.AcquireRenderLease())
            {
                SkiaPhotoDrawOperation.DrawPhoto(
                    canvas,
                    lease.Image,
                    row.Image.Descriptor.EncodedSize,
                    row.Image.Descriptor.Orientation,
                    photoBounds,
                    false);
            }

            canvas.DrawText(row.Identifier, 16, top + 25, headingFont, textPaint);
            for (var resolutionIndex = 0; resolutionIndex < row.Resolutions.Length; resolutionIndex++)
            {
                var resolution = row.Resolutions[resolutionIndex];
                var left = 300 + (resolutionIndex * 370);
                canvas.DrawText(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{resolution.LongEdgePixels}px  {resolution.AnalyzedSize.Width}x{resolution.AnalyzedSize.Height}  " +
                        $"{resolution.NotableColors.Length} colors  {resolution.AnalysisDuration.TotalMilliseconds:F2} ms"),
                    left,
                    top + 48,
                    headingFont,
                    textPaint);
                canvas.DrawText(
                    $"bounded workspace estimate {resolution.EstimatedWorkspaceBytes / 1024d:F0} KiB",
                    left,
                    top + 70,
                    detailFont,
                    mutedPaint);
                for (var colorIndex = 0; colorIndex < resolution.NotableColors.Length; colorIndex++)
                {
                    var color = resolution.NotableColors[colorIndex].Color;
                    var colorRow = colorIndex / 5;
                    var colorColumn = colorIndex % 5;
                    var swatchLeft = left + (colorColumn * 68);
                    var swatchTop = top + 84 + (colorRow * 64);
                    DrawSwatch(canvas, color, swatchLeft, swatchTop, 58, 34);
                    canvas.DrawText(color.ToHex(), swatchLeft, swatchTop + 50, detailFont, mutedPaint);
                }
            }

            canvas.DrawLine(16, top + rowHeight - 1, width - 16, top + rowHeight - 1, separatorPaint);
        }

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 95);
        using var stream = File.Create(destination);
        encoded.SaveTo(stream);
    }

    private static void WriteAlgorithmComparisonSheets(
        IReadOnlyList<EvidenceRow> rows,
        string outputDirectory)
    {
        const int rowsPerPage = 6;
        for (var offset = 0; offset < rows.Count; offset += rowsPerPage)
        {
            var page = (offset / rowsPerPage) + 1;
            WriteAlgorithmComparisonSheet(
                rows.Skip(offset).Take(rowsPerPage).ToArray(),
                Path.Combine(outputDirectory, $"algorithm-comparison-{page:D2}.png"));
        }
    }

    private static void WriteAlgorithmComparisonSheet(
        IReadOnlyList<EvidenceRow> rows,
        string destination)
    {
        const int width = 1800;
        const int rowHeight = 250;
        using var surface = SKSurface.Create(new SKImageInfo(
                                width,
                                Math.Max(rowHeight, rows.Count * rowHeight),
                                SKColorType.Bgra8888,
                                SKAlphaType.Premul,
                                SKColorSpace.CreateSrgb()))
                            ?? throw new InvalidOperationException(
                                "Skia could not allocate the algorithm evidence sheet.");
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(24, 25, 28));
        using var typeface = SKTypeface.FromFamilyName("Segoe UI");
        using var headingFont = new SKFont(typeface, 16);
        using var detailFont = new SKFont(typeface, 12);
        using var textPaint = new SKPaint { IsAntialias = true, Color = new SKColor(235, 237, 240) };
        using var mutedPaint = new SKPaint { IsAntialias = true, Color = new SKColor(170, 175, 182) };
        using var separatorPaint = new SKPaint { Color = new SKColor(65, 68, 74), StrokeWidth = 1 };

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var top = rowIndex * rowHeight;
            var thumbnailBounds = new RectD(16, top + 38, 260, 180);
            var photoBounds = Fit(row.Image.Descriptor.OrientedSize, thumbnailBounds);
            using (var lease = row.Image.AcquireRenderLease())
            {
                SkiaPhotoDrawOperation.DrawPhoto(
                    canvas,
                    lease.Image,
                    row.Image.Descriptor.EncodedSize,
                    row.Image.Descriptor.Orientation,
                    photoBounds,
                    false);
            }

            canvas.DrawText(row.Identifier, 16, top + 25, headingFont, textPaint);
            for (var algorithmIndex = 0; algorithmIndex < row.Algorithms.Length; algorithmIndex++)
            {
                var algorithm = row.Algorithms[algorithmIndex];
                var left = 300 + (algorithmIndex * 370);
                canvas.DrawText(
                    $"{algorithm.Name}  ({algorithm.Colors.Length})",
                    left,
                    top + 50,
                    headingFont,
                    textPaint);
                for (var colorIndex = 0; colorIndex < algorithm.Colors.Length; colorIndex++)
                {
                    var color = algorithm.Colors[colorIndex];
                    var colorRow = colorIndex / 5;
                    var colorColumn = colorIndex % 5;
                    var swatchLeft = left + (colorColumn * 68);
                    var swatchTop = top + 70 + (colorRow * 64);
                    DrawSwatch(canvas, color, swatchLeft, swatchTop, 58, 34);
                    canvas.DrawText(color.ToHex(), swatchLeft, swatchTop + 50, detailFont, mutedPaint);
                }
            }

            canvas.DrawLine(16, top + rowHeight - 1, width - 16, top + rowHeight - 1, separatorPaint);
        }

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 95);
        using var stream = File.Create(destination);
        encoded.SaveTo(stream);
    }

    private static void WriteDiagnosticsJson(IReadOnlyList<EvidenceRow> rows, string destination)
    {
        var payload = rows.Select(row => new
        {
            row.Identifier,
            SelectedCount = row.Profile.NotableColors.Length,
            Timings = new
            {
                GroupingMilliseconds = row.Diagnostics.GroupingDuration.TotalMilliseconds,
                ComponentMilliseconds = row.Diagnostics.ComponentDuration.TotalMilliseconds,
                LocalContrastMilliseconds = row.Diagnostics.LocalContrastDuration.TotalMilliseconds,
                AdmissionMilliseconds = row.Diagnostics.AdmissionDuration.TotalMilliseconds,
                RankingMilliseconds = row.Diagnostics.RankingDuration.TotalMilliseconds,
                TotalMilliseconds = row.Diagnostics.TotalDuration.TotalMilliseconds
            },
            Resolutions = row.Resolutions.Select(resolution => new
            {
                resolution.LongEdgePixels,
                resolution.AnalyzedSize,
                AnalysisMilliseconds = resolution.AnalysisDuration.TotalMilliseconds,
                resolution.EstimatedWorkspaceBytes,
                SelectedColors = resolution.NotableColors,
                Candidates = resolution.Diagnostics.Candidates,
            }),
            row.Algorithms,
            row.Diagnostics.Candidates
        });
        var options = new JsonSerializerOptions { WriteIndented = true };
        options.Converters.Add(new JsonStringEnumConverter());
        File.WriteAllText(destination, JsonSerializer.Serialize(payload, options));
    }

    private static void DrawSwatch(
        SKCanvas canvas,
        StageColor color,
        float left,
        float top,
        float width,
        float height)
    {
        var rectangle = new SKRect(left, top, left + width, top + height);
        using var fill = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(color.Red, color.Green, color.Blue),
        };
        using var outline = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            Color = new SKColor(225, 228, 232, 180),
        };
        canvas.DrawRoundRect(rectangle, 4, 4, fill);
        canvas.DrawRoundRect(rectangle, 4, 4, outline);
    }

    private static RectD Fit(PixelSize image, RectD bounds)
    {
        var scale = Math.Min(bounds.Width / image.Width, bounds.Height / image.Height);
        var width = image.Width * scale;
        var height = image.Height * scale;
        return new RectD(
            bounds.X + ((bounds.Width - width) / 2),
            bounds.Y + ((bounds.Height - height) / 2),
            width,
            height);
    }

    private static string Truncate(string value, int length) => value.Length <= length
        ? value
        : value[..(length - 1)] + "…";

    private sealed record EvidenceRow(
        DecodedImage Image,
        string Identifier,
        PhotoColorProfile Profile,
        NotableColorSelectionDiagnostics Diagnostics,
        ResolutionEvidence[] Resolutions,
        AlgorithmEvidence[] Algorithms);

    private sealed record ResolutionEvidence(
        int LongEdgePixels,
        PixelSize AnalyzedSize,
        TimeSpan AnalysisDuration,
        long EstimatedWorkspaceBytes,
        ImmutableArray<PhotoNotableColor> NotableColors,
        NotableColorSelectionDiagnostics Diagnostics);

    private sealed record AlgorithmEvidence(string Name, StageColor[] Colors);

    private sealed record PrototypeCandidate(
        NotableColorCandidateDiagnostics Diagnostics,
        double Score);
}