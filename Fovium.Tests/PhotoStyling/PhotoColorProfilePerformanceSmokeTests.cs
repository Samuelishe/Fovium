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
                evidence.Add(new EvidenceRow(image, profile, diagnosticResult.Diagnostics.Notable));
            }

            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                WriteContactSheet(evidence, Path.Combine(outputDirectory, "photo-color-profile-contact-sheet.png"));
                WriteDiagnosticsSheet(evidence, Path.Combine(outputDirectory, "notable-diagnostics.png"));
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
            canvas.DrawText(Path.GetFileName(row.Image.Descriptor.SourcePath), 320, top + 34, headingFont, textPaint);
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
                $"{Path.GetFileName(row.Image.Descriptor.SourcePath)}  selected={row.Profile.NotableColors.Length}",
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

    private static void WriteDiagnosticsJson(IReadOnlyList<EvidenceRow> rows, string destination)
    {
        var payload = rows.Select(row => new
        {
            File = Path.GetFileName(row.Image.Descriptor.SourcePath),
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
        PhotoColorProfile Profile,
        NotableColorSelectionDiagnostics Diagnostics);
}