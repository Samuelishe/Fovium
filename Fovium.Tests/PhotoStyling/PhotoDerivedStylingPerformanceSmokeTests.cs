using System.Diagnostics;
using Fovium.Imaging;
using Fovium.PhotoStyling;
using Fovium.Rendering;
using Fovium.Stage;
using SkiaSharp;
using Xunit.Abstractions;

namespace Fovium.Tests.PhotoStyling;

public sealed class PhotoDerivedStylingPerformanceSmokeTests(ITestOutputHelper output)
{
    private const string ImagePathsVariable = "FOVIUM_PHOTO_STYLE_PERF_IMAGES";
    private const string OutputDirectoryVariable = "FOVIUM_PHOTO_STYLE_SMOKE_OUTPUT";

    [Fact]
    public async Task OptInRealImagesReportBoundedAnalysisAndRenderVisualArtifacts()
    {
        var paths = (Environment.GetEnvironmentVariable(ImagePathsVariable) ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (paths.Length == 0)
        {
            output.WriteLine($"Set {ImagePathsVariable} to enable local photo-derived styling evidence.");
            return;
        }

        Assert.All(paths, path => Assert.True(File.Exists(path), path));
        var outputDirectory = Environment.GetEnvironmentVariable(OutputDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var diagnosticAnalyzer = new DiagnosticPhotoStyleAnalyzer();
        using var decoder = new ImageDecoder(
            [new HeifImageDecodeBackend(), new TiffImageDecodeBackend(), new SkiaImageDecodeBackend()],
            photoStyleAnalyzer: diagnosticAnalyzer);
        foreach (var path in paths)
        {
            var decodeClock = Stopwatch.StartNew();
            var loaded = await decoder.LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None);
            decodeClock.Stop();
            using var image = loaded.Image;
            Assert.True(loaded.IsSuccess, loaded.Error?.TechnicalDetail);
            var analysis = Assert.IsType<PhotoStyleAnalysis>(image!.GetPhotoStyleAnalysis());
            var diagnostics = diagnosticAnalyzer.LastDiagnostics;

            Assert.InRange(
                Math.Max(analysis.AnalyzedSize.Width, analysis.AnalyzedSize.Height),
                1,
                StageDefaults.PhotoStyleLongEdgePixels);
            Assert.InRange(
                analysis.VisibleSampleCount,
                1,
                StageDefaults.PhotoStyleLongEdgePixels * StageDefaults.PhotoStyleLongEdgePixels);
            Assert.InRange(analysis.Palette.Length, 1, StageDefaults.PhotoStylePaletteSize);
            Assert.Equal(
                StageDefaults.PhotoStyleFieldColumns * StageDefaults.PhotoStyleFieldRows,
                analysis.SpatialField.Colors.Length);

            output.WriteLine(
                "{0}: source={1}x{2} ({3:F2} MP), decodePlusAnalysis={4:F2} ms, " +
                "analysis={5:F2} ms, analyzed={6}x{7}, samples={8:N0}, analysisRetained={9:N0} B, " +
                "washRetained={10:N0} B, stylingTotal={11:N0} B, " +
                "rawLargestPopulation={12:P2}, rawLargest=#{13:X2}{14:X2}{15:X2}, " +
                "representativePopulation={16:P2}, representativeL={17:F4}, representativeC={18:F4}, " +
                "rawLargestDiffers={19}, representative=#{20:X2}{21:X2}{22:X2}, " +
                "average=#{23:X2}{24:X2}{25:X2}.",
                Path.GetFileName(path),
                image.Descriptor.EncodedSize.Width,
                image.Descriptor.EncodedSize.Height,
                image.Descriptor.EncodedSize.Width * image.Descriptor.EncodedSize.Height / 1_000_000d,
                decodeClock.Elapsed.TotalMilliseconds,
                analysis.AnalysisDuration.TotalMilliseconds,
                analysis.AnalyzedSize.Width,
                analysis.AnalyzedSize.Height,
                analysis.VisibleSampleCount,
                analysis.RetainedBytes,
                GetWashRetainedBytes(image),
                analysis.RetainedBytes + GetWashRetainedBytes(image),
                diagnostics.RawLargestPopulation,
                diagnostics.RawLargestColor.Red,
                diagnostics.RawLargestColor.Green,
                diagnostics.RawLargestColor.Blue,
                diagnostics.RepresentativePopulation,
                diagnostics.RepresentativeLightness,
                diagnostics.RepresentativeChroma,
                diagnostics.RawLargestDiffers,
                diagnostics.RepresentativeColor.Red,
                diagnostics.RepresentativeColor.Green,
                diagnostics.RepresentativeColor.Blue,
                analysis.AverageColor.Red,
                analysis.AverageColor.Green,
                analysis.AverageColor.Blue);

            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                WriteVisualArtifacts(image, analysis, outputDirectory);
            }
        }
    }

    private static long GetWashRetainedBytes(DecodedImage image)
    {
        using var wash = image.TryAcquireColorWash();
        return wash?.RetainedBytes ?? 0;
    }

    private static void WriteVisualArtifacts(
        DecodedImage image,
        PhotoStyleAnalysis analysis,
        string outputDirectory)
    {
        var variants = new (string Name, StageSettings Settings)[]
        {
            ("average", StageSettings.Default with { BackgroundMode = StageBackgroundMode.Average }),
            ("dominant", StageSettings.Default with { BackgroundMode = StageBackgroundMode.Dominant }),
            ("color-wash", StageSettings.Default with { BackgroundMode = StageBackgroundMode.ColorWash }),
            ("auto-matte-hairline", StageSettings.Default with
            {
                BackgroundMode = StageBackgroundMode.Neutral,
                MatteEnabled = true,
                MatteColorSource = MatteColorSource.Dominant,
                MatteWidthPhysicalPixels = 48,
                PhotoSeparation = PhotoSeparationMode.HairlineAuto,
            }),
        };

        var viewport = new RectD(0, 0, 1280, 800);
        var photoDestination = Fit(image.Descriptor.OrientedSize, viewport, 96);
        foreach (var variant in variants)
        {
            using var colorSpace = SKColorSpace.CreateSrgb();
            var info = new SKImageInfo(1280, 800, SKColorType.Bgra8888, SKAlphaType.Premul, colorSpace);
            using var surface = SKSurface.Create(info)
                ?? throw new InvalidOperationException("Skia could not allocate a smoke surface.");
            using var colorWash = variant.Settings.BackgroundMode == StageBackgroundMode.ColorWash
                ? PhotoDerivedStylePolicy.CreateColorWashImage(analysis)
                : null;
            SkiaStageRenderer.Draw(
                surface.Canvas,
                viewport,
                photoDestination,
                1,
                variant.Settings,
                null,
                null,
                image.Identity,
                null,
                null,
                analysis,
                image.Identity,
                colorWash);
            using (var lease = image.AcquireRenderLease())
            {
                SkiaPhotoDrawOperation.DrawPhoto(
                    surface.Canvas,
                    lease.Image,
                    image.Descriptor.EncodedSize,
                    image.Descriptor.Orientation,
                    photoDestination,
                    false);
            }

            using var snapshot = surface.Snapshot();
            using var encoded = snapshot.Encode(SKEncodedImageFormat.Png, 95);
            var stem = Path.GetFileNameWithoutExtension(image.Descriptor.SourcePath);
            var destination = Path.Combine(outputDirectory, $"{stem}-{variant.Name}.png");
            using var stream = File.Create(destination);
            encoded.SaveTo(stream);
        }
    }

    private static RectD Fit(PixelSize photo, RectD viewport, double margin)
    {
        var availableWidth = viewport.Width - (2 * margin);
        var availableHeight = viewport.Height - (2 * margin);
        var scale = Math.Min(availableWidth / photo.Width, availableHeight / photo.Height);
        var width = photo.Width * scale;
        var height = photo.Height * scale;
        return new RectD(
            viewport.X + ((viewport.Width - width) / 2),
            viewport.Y + ((viewport.Height - height) / 2),
            width,
            height);
    }

    private sealed class DiagnosticPhotoStyleAnalyzer : IPhotoStyleAnalyzer
    {
        private readonly PhotoStyleAnalyzer _inner = new();

        public PhotoStyleAnalysisDiagnostics LastDiagnostics { get; private set; }

        public PhotoStyleAnalysis Analyze(
            DecodedImage image,
            CancellationToken cancellationToken)
        {
            var result = _inner.AnalyzeWithDiagnostics(image, cancellationToken);
            LastDiagnostics = result.Diagnostics;
            return result.Analysis;
        }
    }
}
