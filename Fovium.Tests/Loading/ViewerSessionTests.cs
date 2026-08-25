using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Navigation;

namespace Fovium.Tests.Loading;

public sealed class ViewerSessionTests
{
    [Fact]
    public async Task AdjacentProgressPublishesReadyNextBeforeBlockedPreviousCompletes()
    {
        var previousStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var previousCompletion = new TaskCompletionSource<ImageLoadResult<FakeImage>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new FakeImageLoader((path, allowance, _) =>
        {
            if (allowance.IsSpeculative && Path.GetFileName(path) == "A.jpg")
            {
                previousStarted.TrySetResult();
                return previousCompletion.Task;
            }

            return Task.FromResult(FakeLoadResult.Success(path));
        });
        await using var session = CreateSession(loader);
        var firstProgress = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var progressCount = 0;
        session.AdjacentPreloadProgressed += (_, _) =>
        {
            if (Interlocked.Increment(ref progressCount) == 1)
            {
                firstProgress.TrySetResult();
            }
        };

        using var opened = (await session.OpenAsync(
            new ImageSequence(["A.jpg", "B.jpg", "C.jpg"], 1))).Image;
        await firstProgress.Task;
        await previousStarted.Task;
        var fullPreload = session.WaitForAdjacentPreloadAsync(CancellationToken.None);

        try
        {
            Assert.False(fullPreload.IsCompleted);
            Assert.Equal(1, Volatile.Read(ref progressCount));
            Assert.Contains("C.jpg", loader.Calls);
        }
        finally
        {
            previousCompletion.TrySetResult(FakeLoadResult.Success("A.jpg"));
        }

        await fullPreload;
        Assert.Equal(2, Volatile.Read(ref progressCount));
    }

    [Theory]
    [InlineData((int)NavigationDirection.Next, 1, "D.jpg")]
    [InlineData((int)NavigationDirection.Previous, 2, "A.jpg")]
    public async Task NavigationDirectionPrioritizesNewUsefulNeighborPreload(
        int directionValue,
        int initialIndex,
        string expectedFirstNewPreload)
    {
        var direction = (NavigationDirection)directionValue;
        var loader = FakeImageLoader.Immediate(path => FakeLoadResult.Success(path));
        await using var session = CreateSession(loader);
        var sequence = new ImageSequence(["A.jpg", "B.jpg", "C.jpg", "D.jpg"], initialIndex);
        using var opened = (await session.OpenAsync(sequence)).Image;
        await session.WaitForAdjacentPreloadAsync(CancellationToken.None);
        var callsBeforeNavigation = loader.Calls.Count;

        using var navigated = (await session.NavigateAsync(direction)).Image;
        await session.WaitForAdjacentPreloadAsync(CancellationToken.None);

        var callsAfterNavigation = loader.Calls.Skip(callsBeforeNavigation).ToArray();
        Assert.NotEmpty(callsAfterNavigation);
        Assert.Equal(expectedFirstNewPreload, callsAfterNavigation[0]);
    }

    [Fact]
    public async Task PublishingCurrentStartsPreviousAndNextNeighborPreload()
    {
        var loader = FakeImageLoader.Immediate(path => FakeLoadResult.Success(path));
        await using var session = CreateSession(loader);
        var sequence = new ImageSequence(["A.jpg", "B.jpg", "C.jpg"], 1);

        using var opened = (await session.OpenAsync(sequence)).Image;

        Assert.Contains("A.jpg", loader.Calls);
        Assert.Contains("B.jpg", loader.Calls);
        Assert.Contains("C.jpg", loader.Calls);
    }

    [Fact]
    public async Task CurrentLeaseRemainsUsableWhileReplacementIsStillLoading()
    {
        var nextSource = new TaskCompletionSource<ImageLoadResult<FakeImage>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new FakeImageLoader((path, allowance, _) =>
        {
            if (Path.GetFileName(path) == "B.jpg" && !allowance.IsSpeculative)
            {
                return nextSource.Task;
            }

            return Task.FromResult(
                allowance.IsSpeculative
                    ? FakeLoadResult.Failure(ImageLoadErrorKind.ResourceLimit)
                    : FakeLoadResult.Success(path));
        });
        await using var session = CreateSession(loader);
        using var current = (await session.OpenAsync(new ImageSequence(["A.jpg", "B.jpg"], 0))).Image;

        var navigation = session.NavigateAsync(NavigationDirection.Next);

        Assert.Equal("A.jpg", current!.Value.Name);
        Assert.Equal(0, current.Value.DisposeCount);
        nextSource.SetResult(FakeLoadResult.Success("B.jpg"));
        using var replacement = (await navigation).Image;
        Assert.Equal("B.jpg", replacement!.Value.Name);
    }

    [Theory]
    [InlineData((int)ImageLoadErrorKind.Missing)]
    [InlineData((int)ImageLoadErrorKind.Corrupt)]
    [InlineData((int)ImageLoadErrorKind.ResourceLimit)]
    public async Task NavigationSkipsFailedCandidateAndPublishesNextViable(int failureValue)
    {
        var failure = (ImageLoadErrorKind)failureValue;
        var loader = FakeImageLoader.Immediate(path =>
            Path.GetFileName(path) == "B.jpg"
                ? FakeLoadResult.Failure(failure)
                : FakeLoadResult.Success(path));
        await using var session = CreateSession(loader);
        var sequence = new ImageSequence(["A.jpg", "B.jpg", "C.jpg"], 0);
        using var opened = (await session.OpenAsync(sequence)).Image;

        var navigated = await session.NavigateAsync(NavigationDirection.Next);
        using var selected = navigated.Image;

        Assert.Equal(SelectionStatus.Published, navigated.Status);
        Assert.Equal("C.jpg", Path.GetFileName(navigated.Path));
        Assert.Contains("B.jpg", loader.Calls);
        Assert.Equal(2, session.CurrentIndex);
    }

    [Fact]
    public async Task NavigationStopsAfterBoundedSearchWhenEveryCandidateFails()
    {
        var loader = FakeImageLoader.Immediate(path =>
            Path.GetFileName(path) == "A.jpg"
                ? FakeLoadResult.Success(path)
                : FakeLoadResult.Failure(ImageLoadErrorKind.Corrupt));
        await using var session = CreateSession(loader);
        var sequence = new ImageSequence(["A.jpg", "B.jpg", "C.jpg"], 0);
        using var opened = (await session.OpenAsync(sequence)).Image;

        var result = await session.NavigateAsync(NavigationDirection.Next);

        Assert.Equal(SelectionStatus.NoViableCandidate, result.Status);
        Assert.Equal(0, session.CurrentIndex);
        Assert.InRange(loader.Calls.Count(name => name is "B.jpg" or "C.jpg"), 2, 4);
    }

    [Fact]
    public async Task DelayedAAndBMayNotPublishAfterFastCAndStaleResourcesAreDisposed()
    {
        var aSource = new TaskCompletionSource<ImageLoadResult<FakeImage>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bSource = new TaskCompletionSource<ImageLoadResult<FakeImage>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cSource = new TaskCompletionSource<ImageLoadResult<FakeImage>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new FakeImageLoader((path, _, _) => Path.GetFileName(path) switch
        {
            "A.jpg" => aSource.Task,
            "B.jpg" => bSource.Task,
            "C.jpg" => cSource.Task,
            _ => throw new InvalidOperationException(),
        });
        await using var session = CreateSession(loader);
        var openA = session.OpenAsync(new ImageSequence(["A.jpg"], 0));
        var openB = session.OpenAsync(new ImageSequence(["B.jpg"], 0));
        var openC = session.OpenAsync(new ImageSequence(["C.jpg"], 0));
        var imageC = new FakeImage("C.jpg");
        cSource.SetResult(ImageLoadResult<FakeImage>.Success(imageC));
        var resultC = await openC;
        var imageA = new FakeImage("A.jpg");
        aSource.SetResult(ImageLoadResult<FakeImage>.Success(imageA));
        var resultA = await openA;
        var imageB = new FakeImage("B.jpg");
        bSource.SetResult(ImageLoadResult<FakeImage>.Success(imageB));
        var resultB = await openB;
        using var visible = resultC.Image;

        Assert.Equal(SelectionStatus.Published, resultC.Status);
        Assert.Equal("C.jpg", visible!.Value.Name);
        Assert.Equal(SelectionStatus.Stale, resultA.Status);
        Assert.Equal(SelectionStatus.Stale, resultB.Status);
        Assert.Equal(1, imageA.DisposeCount);
        Assert.Equal(1, imageB.DisposeCount);
        Assert.Equal(2, session.StaleResultDisposals);
    }

    [Fact]
    public async Task NavigationAtSequenceBoundaryDoesNotWrap()
    {
        var loader = FakeImageLoader.Immediate(path => FakeLoadResult.Success(path));
        await using var session = CreateSession(loader);
        using var opened = (await session.OpenAsync(new ImageSequence(["A.jpg"], 0))).Image;

        var result = await session.NavigateAsync(NavigationDirection.Next);

        Assert.Equal(SelectionStatus.NoMove, result.Status);
        Assert.Equal(0, session.CurrentIndex);
    }

    [Fact]
    public async Task DisposeAsyncWaitsForInFlightDecodeAndDisposesStaleResult()
    {
        var source = new TaskCompletionSource<ImageLoadResult<FakeImage>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new FakeImageLoader((_, _, _) => source.Task);
        var session = CreateSession(loader);
        var opening = session.OpenAsync(new ImageSequence(["A.jpg"], 0));

        var disposal = session.DisposeAsync().AsTask();

        Assert.False(disposal.IsCompleted);
        var image = new FakeImage("A.jpg");
        source.SetResult(ImageLoadResult<FakeImage>.Success(image));
        var result = await opening;
        await disposal;

        Assert.Equal(SelectionStatus.Stale, result.Status);
        Assert.Equal(1, image.DisposeCount);
    }

    [Fact]
    public async Task OpeningNewSequenceReleasesObsoleteCacheOwnership()
    {
        var images = new Dictionary<string, FakeImage>();
        var loader = FakeImageLoader.Immediate(path =>
        {
            var image = new FakeImage(Path.GetFileName(path));
            images.Add(image.Name, image);
            return ImageLoadResult<FakeImage>.Success(image);
        });
        await using var session = CreateSession(loader);
        var first = await session.OpenAsync(new ImageSequence(["A.jpg"], 0));
        using var firstLease = first.Image;

        using var secondLease = (await session.OpenAsync(new ImageSequence(["B.jpg"], 0))).Image;

        Assert.Equal(0, images["A.jpg"].DisposeCount);
        firstLease!.Dispose();
        Assert.Equal(1, images["A.jpg"].DisposeCount);
        var metrics = session.GetMetrics();
        Assert.Equal(1, metrics.CacheItemCount);
        Assert.Equal(16, metrics.CacheRetainedBytes);
    }

    [Fact]
    public async Task CachedInspectionDoesNotNavigateAndFollowingNextPreviousRemainCoherent()
    {
        var loader = FakeImageLoader.Immediate(path => FakeLoadResult.Success(path));
        await using var session = CreateSession(loader);
        using var opened = (await session.OpenAsync(new ImageSequence(["A.jpg", "B.jpg", "C.jpg"], 1))).Image;
        await session.WaitForAdjacentPreloadAsync(CancellationToken.None);
        var callsBeforeInspection = loader.Calls.Count;

        var inspection = await session.AcquireNeighborForInspectionAsync(NavigationDirection.Previous);
        using (inspection.Image)
        {
            Assert.Equal(InspectionAcquisitionStatus.Acquired, inspection.Status);
            Assert.True(inspection.FromCache);
            Assert.Equal("A.jpg", inspection.Image!.Value.Name);
            Assert.Equal(1, session.CurrentIndex);
            Assert.Equal(callsBeforeInspection, loader.Calls.Count);
        }

        var next = await session.NavigateAsync(NavigationDirection.Next);
        using (next.Image)
        {
            Assert.Equal("C.jpg", next.Image!.Value.Name);
            Assert.Equal(2, session.CurrentIndex);
        }

        var previous = await session.NavigateAsync(NavigationDirection.Previous);
        using (previous.Image)
        {
            Assert.Equal("B.jpg", previous.Image!.Value.Name);
            Assert.Equal(1, session.CurrentIndex);
        }
    }

    [Fact]
    public async Task InspectionSkipsMissingCorruptAndResourceRejectedCandidatesWithoutChangingSelection()
    {
        var loader = FakeImageLoader.Immediate(path => Path.GetFileName(path) switch
        {
            "A.jpg" or "D.jpg" => FakeLoadResult.Success(path),
            "B.jpg" => FakeLoadResult.Failure(ImageLoadErrorKind.Corrupt),
            "C.jpg" => FakeLoadResult.Failure(ImageLoadErrorKind.Missing),
            _ => FakeLoadResult.Failure(ImageLoadErrorKind.ResourceLimit),
        });
        await using var session = CreateSession(loader);
        using var opened = (await session.OpenAsync(
            new ImageSequence(["A.jpg", "B.jpg", "C.jpg", "D.jpg"], 3))).Image;
        await session.WaitForAdjacentPreloadAsync(CancellationToken.None);

        var inspection = await session.AcquireNeighborForInspectionAsync(NavigationDirection.Previous);
        using var comparison = inspection.Image;

        Assert.Equal(InspectionAcquisitionStatus.Acquired, inspection.Status);
        Assert.Equal("A.jpg", comparison!.Value.Name);
        Assert.Equal(3, session.CurrentIndex);
        Assert.Contains("B.jpg", loader.Calls);
        Assert.Contains("C.jpg", loader.Calls);
    }

    [Fact]
    public async Task ReleasingDelayedInspectionPreventsLateResultFromBeingReturned()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<ImageLoadResult<FakeImage>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new FakeImageLoader((path, allowance, _) =>
        {
            if (Path.GetFileName(path) == "C.jpg" && !allowance.IsSpeculative)
            {
                started.TrySetResult();
                return delayed.Task;
            }

            return Task.FromResult(
                allowance.IsSpeculative
                    ? FakeLoadResult.Failure(ImageLoadErrorKind.ResourceLimit)
                    : FakeLoadResult.Success(path));
        });
        await using var session = CreateSession(loader);
        using var opened = (await session.OpenAsync(
            new ImageSequence(["A.jpg", "B.jpg", "C.jpg", "D.jpg"], 3))).Image;
        await session.WaitForAdjacentPreloadAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();

        var pending = session.AcquireNeighborForInspectionAsync(
            NavigationDirection.Previous,
            cancellation.Token);
        await started.Task;
        cancellation.Cancel();
        var lateImage = new FakeImage("C.jpg");
        delayed.SetResult(ImageLoadResult<FakeImage>.Success(lateImage));
        var result = await pending;

        Assert.Equal(InspectionAcquisitionStatus.Canceled, result.Status);
        Assert.Null(result.Image);
        Assert.Equal(1, lateImage.DisposeCount);
        Assert.Equal(3, session.CurrentIndex);
    }

    [Fact]
    public async Task NewSequenceRevokesDelayedInspectionAuthorityAndDisposesOldResult()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<ImageLoadResult<FakeImage>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new FakeImageLoader((path, allowance, _) =>
        {
            if (Path.GetFileName(path) == "C.jpg" && !allowance.IsSpeculative)
            {
                started.TrySetResult();
                return delayed.Task;
            }

            return Task.FromResult(
                allowance.IsSpeculative
                    ? FakeLoadResult.Failure(ImageLoadErrorKind.ResourceLimit)
                    : FakeLoadResult.Success(path));
        });
        await using var session = CreateSession(loader);
        using var opened = (await session.OpenAsync(
            new ImageSequence(["A.jpg", "B.jpg", "C.jpg", "D.jpg"], 3))).Image;
        await session.WaitForAdjacentPreloadAsync(CancellationToken.None);

        var pending = session.AcquireNeighborForInspectionAsync(NavigationDirection.Previous);
        await started.Task;
        using var replacement = (await session.OpenAsync(new ImageSequence(["E.jpg"], 0))).Image;
        var lateImage = new FakeImage("C.jpg");
        delayed.SetResult(ImageLoadResult<FakeImage>.Success(lateImage));
        var stale = await pending;

        Assert.Equal(InspectionAcquisitionStatus.Stale, stale.Status);
        Assert.Null(stale.Image);
        Assert.Equal(1, lateImage.DisposeCount);
        Assert.Equal("E.jpg", replacement!.Value.Name);
        Assert.Equal(0, session.CurrentIndex);
    }

    [Fact]
    public async Task InspectionLeaseKeepsComparisonAliveAfterSequenceCacheRelease()
    {
        var images = new Dictionary<string, FakeImage>();
        var loader = FakeImageLoader.Immediate(path =>
        {
            var image = new FakeImage(Path.GetFileName(path));
            images[image.Name] = image;
            return ImageLoadResult<FakeImage>.Success(image);
        });
        await using var session = CreateSession(loader);
        using var opened = (await session.OpenAsync(new ImageSequence(["A.jpg", "B.jpg"], 1))).Image;
        await session.WaitForAdjacentPreloadAsync(CancellationToken.None);
        var inspection = await session.AcquireNeighborForInspectionAsync(NavigationDirection.Previous);
        var comparison = inspection.Image!;

        using var replacement = (await session.OpenAsync(new ImageSequence(["C.jpg"], 0))).Image;

        Assert.Equal(0, images["A.jpg"].DisposeCount);
        Assert.Equal("A.jpg", comparison.Value.Name);
        comparison.Dispose();
        Assert.Equal(1, images["A.jpg"].DisposeCount);
    }

    [Fact]
    public async Task InspectionDeclinesWhileCanonicalSelectionIsChanging()
    {
        var navigationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayedNavigation = new TaskCompletionSource<ImageLoadResult<FakeImage>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loader = new FakeImageLoader((path, allowance, _) =>
        {
            if (Path.GetFileName(path) == "C.jpg" && !allowance.IsSpeculative)
            {
                navigationStarted.TrySetResult();
                return delayedNavigation.Task;
            }

            return Task.FromResult(
                allowance.IsSpeculative
                    ? FakeLoadResult.Failure(ImageLoadErrorKind.ResourceLimit)
                    : FakeLoadResult.Success(path));
        });
        await using var session = CreateSession(loader);
        using var current = (await session.OpenAsync(
            new ImageSequence(["A.jpg", "B.jpg", "C.jpg"], 1))).Image;
        await session.WaitForAdjacentPreloadAsync(CancellationToken.None);

        var pendingNavigation = session.NavigateAsync(NavigationDirection.Next);
        await navigationStarted.Task;
        var inspection = await session.AcquireNeighborForInspectionAsync(NavigationDirection.Previous);

        delayedNavigation.SetResult(FakeLoadResult.Success("C.jpg"));
        using var navigated = (await pendingNavigation).Image;

        Assert.Equal(InspectionAcquisitionStatus.Unavailable, inspection.Status);
        Assert.Null(inspection.Image);
        Assert.Equal("C.jpg", navigated!.Value.Name);
        Assert.Equal(2, session.CurrentIndex);
    }

    private static ViewerSession<FakeImage> CreateSession(FakeImageLoader loader)
    {
        var policy = AutomaticMemoryPolicy.FromAvailableMemory(2L * 1024 * 1024 * 1024);
        var cache = new ByteBudgetCache<string, FakeImage>(policy.CacheBudgetBytes, StringComparer.Ordinal);
        return new ViewerSession<FakeImage>(loader, cache, policy);
    }
}
