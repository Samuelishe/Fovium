using System.Collections.Concurrent;
using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Metadata;
using Fovium.Navigation;
using Fovium.Presentation;
using Fovium.Rendering;
using Fovium.Settings;
using Fovium.Tests.Stage;
using Fovium.Viewer;

namespace Fovium.Tests.Viewer;

public sealed class ViewerInspectionCoordinatorTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public async Task CachedBlinkShowsPreviousThenRestoresCanonicalViewWithoutNavigation()
    {
        var loader = new DecodedImageLoader((path, _) => Task.FromResult(
            ImageLoadResult<DecodedImage>.Success(StageTestImages.CreateDecoded(path))));
        await using var session = CreateSession(loader);
        using var settings = new SettingsService(new DefaultSettingsStore());
        var viewport = new PhotoViewportControl();
        var opened = await session.OpenAsync(new ImageSequence(["A.png", "B.png", "C.png"], 1));
        var overlays = new PresentationOverlaySession(PresentationSettings.Default);
        overlays.ToggleMarkupTools();
        DrawOverlay(overlays, opened.Path!, new PresentationColor(0x11, 0x22, 0x33));
        var previousPath = Path.GetFullPath("A.png");
        DrawOverlay(overlays, previousPath, new PresentationColor(0xAA, 0xBB, 0xCC));
        EraseOverlay(overlays, previousPath);
        viewport.ConfigurePresentation(overlays);
        var canonicalIdentity = opened.Image!.Value.Identity;
        viewport.SetImage(opened.Image!, ViewTransfer.Fit, opened.Path!);
        var metadataReader = new EmptyMetadataReader();
        using var photoInfo = new PhotoInfoCoordinator(viewport, metadataReader);
        photoInfo.SetVisible(true);
        Assert.Equal(canonicalIdentity, photoInfo.CurrentState!.Base.ImageIdentity);
        viewport.SetPhotographic100AtCenter();
        var before = viewport.CaptureViewTransfer();
        await session.WaitForAdjacentPreloadAsync(CancellationToken.None);
        var callsBefore = loader.Calls.Count;
        var coordinator = new ViewerInspectionCoordinator(viewport, session, settings);

        await coordinator.BeginAsync(Fovium.Input.ViewerCommand.BlinkCompare, CancellationToken.None);

        Assert.Equal(InspectionMode.BlinkCompare, viewport.InspectionMode);
        Assert.Equal(1, session.CurrentIndex);
        Assert.Equal(callsBefore, loader.Calls.Count);
        Assert.Equal(before, viewport.CaptureViewTransfer());
        Assert.Equal(previousPath, viewport.PresentedImageIdentity);
        Assert.Equal(previousPath, photoInfo.CurrentState!.Base.SourcePath);
        var comparisonMarkup = viewport.CapturePresentedMarkup().Operations;
        Assert.Equal(2, comparisonMarkup.Count);
        Assert.Equal(
            new PresentationColor(0xAA, 0xBB, 0xCC),
            Assert.IsType<DrawMarkupOperation>(
                comparisonMarkup[0]).Element.Color);
        Assert.IsType<EraseMarkupOperation>(comparisonMarkup[1]);

        coordinator.End();

        Assert.Equal(InspectionMode.None, viewport.InspectionMode);
        Assert.Equal(before, viewport.CaptureViewTransfer());
        Assert.Equal(1, session.CurrentIndex);
        Assert.Equal(opened.Path, viewport.PresentedImageIdentity);
        Assert.Equal(opened.Path, photoInfo.CurrentState!.Base.SourcePath);
        Assert.Equal(2, metadataReader.CallCount);
        Assert.Equal(
            new PresentationColor(0x11, 0x22, 0x33),
            Assert.IsType<DrawMarkupOperation>(
                Assert.Single(viewport.CapturePresentedMarkup().Operations)).Element.Color);
        var metrics = coordinator.GetMetrics();
        Assert.True(metrics.LastCachedBlinkLatency > TimeSpan.Zero);
        Assert.True(metrics.LastReleaseLatency > TimeSpan.Zero);
        output.WriteLine(
            "Cached Blink coordinator: press-to-presentation {0:F3} ms; release-to-restore {1:F3} ms.",
            metrics.LastCachedBlinkLatency.TotalMilliseconds,
            metrics.LastReleaseLatency.TotalMilliseconds);
        viewport.ClearImage();
    }

    [Fact]
    public async Task PeekKeepsCanonicalOverlayIdentity()
    {
        var loader = new DecodedImageLoader((path, _) => Task.FromResult(
            ImageLoadResult<DecodedImage>.Success(StageTestImages.CreateDecoded(path))));
        await using var session = CreateSession(loader);
        using var settings = new SettingsService(new DefaultSettingsStore());
        var viewport = new PhotoViewportControl();
        var overlays = new PresentationOverlaySession(PresentationSettings.Default);
        overlays.ToggleMarkupTools();
        viewport.ConfigurePresentation(overlays);
        var opened = await session.OpenAsync(new ImageSequence(["A.png"], 0));
        DrawOverlay(overlays, opened.Path!, new PresentationColor(1, 2, 3));
        EraseOverlay(overlays, opened.Path!);
        viewport.SetImage(opened.Image!, ViewTransfer.Fit, opened.Path!);
        var presentedChangeCount = 0;
        viewport.PresentedImageChanged += (_, _) => presentedChangeCount++;
        var coordinator = new ViewerInspectionCoordinator(viewport, session, settings);

        await coordinator.BeginAsync(Fovium.Input.ViewerCommand.Peek100, CancellationToken.None);

        Assert.Equal(opened.Path, viewport.PresentedImageIdentity);
        var peekMarkup = viewport.CapturePresentedMarkup().Operations;
        Assert.Equal(2, peekMarkup.Count);
        Assert.IsType<EraseMarkupOperation>(peekMarkup[1]);
        coordinator.End();
        Assert.Equal(opened.Path, viewport.PresentedImageIdentity);
        Assert.Equal(0, presentedChangeCount);
        viewport.ClearImage();
    }

    private sealed class EmptyMetadataReader : IPhotoMetadataReader
    {
        public int CallCount { get; private set; }

        public Task<PhotoMetadataReadResult> ReadAsync(
            ReadOnlyMemory<byte> encodedSource,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(PhotoMetadataReadResult.FromSummary(PhotoMetadataSummary.Empty));
        }
    }

    [Fact]
    public async Task NonCachedBlinkPublishesWhileHeldAndRecordsCoordinatorLatency()
    {
        var loader = new DecodedImageLoader((path, allowance) => Task.FromResult(
            allowance.IsSpeculative
                ? ImageLoadResult<DecodedImage>.Failure(new ImageLoadError(
                    ImageLoadErrorKind.ResourceLimit,
                    "No speculative load."))
                : ImageLoadResult<DecodedImage>.Success(StageTestImages.CreateDecoded(path))));
        await using var session = CreateSession(loader);
        using var settings = new SettingsService(new DefaultSettingsStore());
        var viewport = new PhotoViewportControl();
        var opened = await session.OpenAsync(new ImageSequence(["A.png", "B.png"], 1));
        viewport.SetImage(opened.Image!, ViewTransfer.Fit, opened.Path!);
        await session.WaitForAdjacentPreloadAsync(CancellationToken.None);
        var coordinator = new ViewerInspectionCoordinator(viewport, session, settings);

        await coordinator.BeginAsync(Fovium.Input.ViewerCommand.BlinkCompare, CancellationToken.None);

        Assert.Equal(InspectionMode.BlinkCompare, viewport.InspectionMode);
        Assert.Equal(1, session.CurrentIndex);
        var heldMetrics = coordinator.GetMetrics();
        Assert.True(heldMetrics.LastNonCachedBlinkLatency > TimeSpan.Zero);

        coordinator.End();

        var releasedMetrics = coordinator.GetMetrics();
        Assert.Equal(InspectionMode.None, viewport.InspectionMode);
        Assert.True(releasedMetrics.LastReleaseLatency > TimeSpan.Zero);
        output.WriteLine(
            "Controlled non-cached Blink coordinator: acquisition-to-presentation {0:F3} ms; " +
            "release-to-restore {1:F3} ms.",
            heldMetrics.LastNonCachedBlinkLatency.TotalMilliseconds,
            releasedMetrics.LastReleaseLatency.TotalMilliseconds);

        await coordinator.BeginAsync(Fovium.Input.ViewerCommand.Peek100, CancellationToken.None);
        var peekMetrics = coordinator.GetMetrics();
        Assert.Equal(InspectionMode.Peek100, viewport.InspectionMode);
        Assert.True(peekMetrics.LastPeekBeginLatency > TimeSpan.Zero);
        coordinator.End();
        output.WriteLine(
            "Peek viewport-state transition: {0:F3} ms.",
            peekMetrics.LastPeekBeginLatency.TotalMilliseconds);
        viewport.ClearImage();
    }

    [Fact]
    public async Task ReleasedDelayedBlinkCannotPublishLateComparison()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<ImageLoadResult<DecodedImage>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new DecodedImageLoader((path, allowance) =>
        {
            if (Path.GetFileName(path) == "C.png" && !allowance.IsSpeculative)
            {
                started.TrySetResult();
                return delayed.Task;
            }

            return Task.FromResult(
                allowance.IsSpeculative
                    ? ImageLoadResult<DecodedImage>.Failure(new ImageLoadError(
                        ImageLoadErrorKind.ResourceLimit,
                        "No speculative load."))
                    : ImageLoadResult<DecodedImage>.Success(StageTestImages.CreateDecoded(path)));
        });
        await using var session = CreateSession(loader);
        using var settings = new SettingsService(new DefaultSettingsStore());
        var viewport = new PhotoViewportControl();
        var opened = await session.OpenAsync(
            new ImageSequence(["A.png", "B.png", "C.png", "D.png"], 3));
        viewport.SetImage(opened.Image!, ViewTransfer.Fit, opened.Path!);
        await session.WaitForAdjacentPreloadAsync(CancellationToken.None);
        var coordinator = new ViewerInspectionCoordinator(viewport, session, settings);

        var pending = coordinator.BeginAsync(
            Fovium.Input.ViewerCommand.BlinkCompare,
            CancellationToken.None);
        await started.Task;
        coordinator.End();
        var late = StageTestImages.CreateDecoded("C.png");
        delayed.SetResult(ImageLoadResult<DecodedImage>.Success(late));
        await pending;

        Assert.Equal(InspectionMode.None, coordinator.Mode);
        Assert.Equal(InspectionMode.None, viewport.InspectionMode);
        Assert.Equal(ViewTransfer.Fit, viewport.CaptureViewTransfer());
        Assert.Equal(3, session.CurrentIndex);
        Assert.Throws<ObjectDisposedException>(() => late.AcquireRenderLease());
        viewport.ClearImage();
    }

    private static ViewerSession<DecodedImage> CreateSession(DecodedImageLoader loader)
    {
        var policy = AutomaticMemoryPolicy.FromAvailableMemory(2L * 1024 * 1024 * 1024);
        var cache = new ByteBudgetCache<string, DecodedImage>(policy.CacheBudgetBytes, StringComparer.Ordinal);
        return new ViewerSession<DecodedImage>(loader, cache, policy);
    }

    private sealed class DecodedImageLoader(
        Func<string, ImageLoadAllowance, Task<ImageLoadResult<DecodedImage>>> load)
        : IImageLoader<DecodedImage>
    {
        private readonly ConcurrentQueue<string> _calls = new();

        public IReadOnlyList<string> Calls => _calls.ToArray();

        public Task<ImageLoadResult<DecodedImage>> LoadAsync(
            string path,
            ImageLoadAllowance allowance,
            CancellationToken cancellationToken)
        {
            _calls.Enqueue(Path.GetFileName(path));
            return load(path, allowance);
        }
    }

    private sealed class DefaultSettingsStore : ISettingsStore
    {
        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SettingsLoadResult(FoviumSettings.Default, null));

        public Task SaveAsync(FoviumSettings settings, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private static void DrawOverlay(
        PresentationOverlaySession overlays,
        string identity,
        PresentationColor color)
    {
        overlays.SelectImage(identity);
        overlays.SetActiveTool(MarkupTool.Line);
        overlays.SetActiveColor(color);
        Assert.True(overlays.BeginDrawing(new PointD(1, 1), 1));
        Assert.True(overlays.EndDrawing(new PointD(8, 8)));
    }

    private static void EraseOverlay(PresentationOverlaySession overlays, string identity)
    {
        overlays.SelectImage(identity);
        overlays.SetActiveTool(MarkupTool.Eraser);
        Assert.True(overlays.BeginDrawing(new PointD(4, 1), 1));
        Assert.True(overlays.EndDrawing(new PointD(4, 8)));
    }
}
