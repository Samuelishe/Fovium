using System.Diagnostics;
using System.Globalization;
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
            Directory.CreateDirectory(outputDirectory);
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
                var projectionMicroseconds = MeasureProjection(analysis);
                output.WriteLine(
                    "Photo {0:D2} ({1}): decode+analysis+profile={2:F2} ms, analysis={3:F2} ms, " +
                    "profileProjection={4:F2} us, analysisRetained={5:N0} B, " +
                    "profileRetained={6:N0} B, palette={7}, notable={8}, visibleSamples={9:N0}.",
                    evidence.Count + 1,
                    Path.GetFileName(path),
                    decodeClock.Elapsed.TotalMilliseconds,
                    analysis.AnalysisDuration.TotalMilliseconds,
                    projectionMicroseconds,
                    analysis.RetainedBytes,
                    profile.RetainedBytes,
                    profile.Palette.Length,
                    profile.NotableColors.Length,
                    analysis.VisibleSampleCount);
                evidence.Add(new EvidenceRow(image, profile));
            }

            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                WriteContactSheet(evidence, Path.Combine(outputDirectory, "photo-color-profile-contact-sheet.png"));
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
        const int width = 1600;
        const int rowHeight = 210;
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

            canvas.DrawText("Notable colors", 1190, top + 34, smallFont, mutedPaint);
            for (var notableIndex = 0; notableIndex < presentation.NotableColors.Length; notableIndex++)
            {
                var color = presentation.NotableColors[notableIndex];
                var left = 1190 + (notableIndex * 130);
                DrawSwatch(canvas, color.Color, left, top + 44, 116, 62);
                canvas.DrawText(Truncate(color.StructuralName, 17), left, top + 128, smallFont, textPaint);
                canvas.DrawText(color.Hex, left, top + 148, smallFont, mutedPaint);
            }

            canvas.DrawLine(16, top + rowHeight - 1, width - 16, top + rowHeight - 1, separatorPaint);
        }

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 95);
        using var stream = File.Create(destination);
        encoded.SaveTo(stream);
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

    private sealed record EvidenceRow(DecodedImage Image, PhotoColorProfile Profile);
}