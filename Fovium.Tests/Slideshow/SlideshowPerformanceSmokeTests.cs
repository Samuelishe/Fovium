using System.Diagnostics;
using Fovium.ColorManagement;
using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Slideshow;
using Xunit.Abstractions;

namespace Fovium.Tests.Slideshow;

public sealed class SlideshowPerformanceSmokeTests(ITestOutputHelper output)
{
    private const string ImagePathsVariable = "FOVIUM_SLIDESHOW_PERF_IMAGES";

    [Fact]
    public async Task OptInRealImagesReportDecodeManagedCadenceAndMemoryEvidence()
    {
        var paths = (Environment.GetEnvironmentVariable(ImagePathsVariable) ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (paths.Length == 0)
        {
            output.WriteLine($"Set {ImagePathsVariable} to enable local slideshow performance evidence.");
            return;
        }

        Assert.All(paths, path => Assert.True(File.Exists(path), path));
        var availability = new LittleCmsRuntimeLocator().TryLoad();
        Assert.True(availability.IsAvailable, availability.Detail);
        var profile = File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "ColorManagement",
            "fovium-linear-rgb-display.icc"));
        var destination = DisplayProfileIdentity.FromBytes(profile, false);
        using var decoder = ImageDecoder.CreateDefault();
        using var renderer = new SkiaLittleCmsPhotoRenderer(
            new LittleCmsColorTransformEngine(availability));

        foreach (var path in paths)
        {
            var decodeStopwatch = Stopwatch.StartNew();
            var loaded = await decoder.LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None);
            var decodeReady = decodeStopwatch.Elapsed;
            using var image = loaded.Image;
            Assert.True(loaded.IsSuccess, loaded.Error?.TechnicalDetail);
            var key = new ManagedPhotoKey(
                image!.Identity,
                destination,
                image.Descriptor.EncodedSize,
                image.Descriptor.Orientation);
            using var request = new ManagedPhotoRenderRequest(
                key,
                image.Descriptor,
                image.AcquireRenderLease(),
                profile);
            var managedStopwatch = Stopwatch.StartNew();
            using var managed = renderer.Render(request);
            var managedReady = managedStopwatch.Elapsed;
            var currentAndNextBytes = checked(managed.RetainedBytes * 2);

            output.WriteLine(
                "{0}: {1}x{2} ({3:F2} MP), decodedReady={4:F2} ms, " +
                "managedPreparation={5:F2} ms (read={6:F2}, lcms={7:F2}, finalize={8:F2}), " +
                "before5sTimer={9}, speculativeBytes={10:N0}, currentPlusNextBytes={11:N0}, admitted={12}.",
                Path.GetFileName(path),
                image.Descriptor.EncodedSize.Width,
                image.Descriptor.EncodedSize.Height,
                image.Descriptor.EncodedSize.Width * image.Descriptor.EncodedSize.Height / 1_000_000d,
                decodeReady.TotalMilliseconds,
                managedReady.TotalMilliseconds,
                managed.SourceReadDuration.TotalMilliseconds,
                managed.TransformDuration.TotalMilliseconds,
                managed.FinalizationDuration.TotalMilliseconds,
                managedReady < TimeSpan.FromSeconds(5),
                managed.RetainedBytes,
                currentAndNextBytes,
                SlideshowManagedPreloadPolicy.IsAdmitted(managed.RetainedBytes, managed.RetainedBytes));

            Assert.Equal(
                checked((long)image.Descriptor.EncodedSize.Width * image.Descriptor.EncodedSize.Height * 4),
                managed.RetainedBytes);
        }
    }
}
