using System.Collections.Concurrent;
using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Navigation;
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
        viewport.SetImage(opened.Image!, ViewTransfer.Fit);
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

        coordinator.End();

        Assert.Equal(InspectionMode.None, viewport.InspectionMode);
        Assert.Equal(before, viewport.CaptureViewTransfer());
        Assert.Equal(1, session.CurrentIndex);
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
        viewport.SetImage(opened.Image!, ViewTransfer.Fit);
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
        viewport.SetImage(opened.Image!, ViewTransfer.Fit);
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
}
