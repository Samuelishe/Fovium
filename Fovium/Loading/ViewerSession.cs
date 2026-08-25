using System.Diagnostics;
using Fovium.Imaging;
using Fovium.Navigation;

namespace Fovium.Loading;

internal enum SelectionStatus
{
    Published,
    Failed,
    NoMove,
    NoViableCandidate,
    Stale,
}

internal sealed record SelectionResult<T>(
    SelectionStatus Status,
    string? Path,
    int? Index,
    long Generation,
    SharedResourceLease<T>? Image,
    ImageLoadError? Error,
    bool FromCache,
    TimeSpan PublicationLatency)
    where T : class, IRetainedResource
{
    public static SelectionResult<T> Simple(SelectionStatus status, long generation) =>
        new(status, null, null, generation, null, null, false, TimeSpan.Zero);
}

internal readonly record struct ViewerSessionMetrics(
    long CacheHits,
    long StaleResultDisposals,
    long CacheRetainedBytes,
    long CacheBudgetBytes,
    long CacheRemainingBytes,
    int CacheItemCount,
    long CacheEvictions,
    long CacheRejectedAdds,
    long ForegroundLoadAttempts,
    long ForegroundLoadSuccesses,
    long SpeculativeRequests,
    long SpeculativeLoadAttempts,
    long SpeculativeLoadSuccesses,
    long SpeculativeCacheHits,
    long SpeculativeResourceLimitRejections,
    long SpeculativeCancellations,
    long SpeculativeCacheAdds,
    long SpeculativeCacheAddRejections,
    int LastSpeculativeCandidateIndex,
    SpeculativeLoadOutcome LastSpeculativeOutcome);

internal enum SpeculativeLoadOutcome
{
    None,
    Requested,
    CacheHit,
    Started,
    Decoded,
    ResourceLimit,
    Failed,
    Canceled,
    Added,
    CacheRejected,
}

internal sealed record CachedResourceLease<T>(string Path, SharedResourceLease<T> Resource) : IDisposable
    where T : class, IRetainedResource
{
    public void Dispose() => Resource.Dispose();
}

internal enum InspectionAcquisitionStatus
{
    Acquired,
    Unavailable,
    Canceled,
    Stale,
}

internal sealed record InspectionAcquisitionResult<T>(
    InspectionAcquisitionStatus Status,
    string? Path,
    int? Index,
    SharedResourceLease<T>? Image,
    bool FromCache,
    TimeSpan AcquisitionLatency)
    where T : class, IRetainedResource
{
    public static InspectionAcquisitionResult<T> Simple(InspectionAcquisitionStatus status) =>
        new(status, null, null, null, false, TimeSpan.Zero);
}

internal sealed class ViewerSession<T> : IAsyncDisposable
    where T : class, IRetainedResource
{
    private readonly object _sync = new();
    private readonly IImageLoader<T> _loader;
    private readonly ByteBudgetCache<string, T> _cache;
    private readonly AutomaticMemoryPolicy _memoryPolicy;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private CancellationTokenSource? _foregroundCancellation;
    private CancellationTokenSource? _preloadCancellation;
    private Task _preloadTask = Task.CompletedTask;
    private readonly HashSet<Task> _runningTasks = [];
    private ImageSequence? _sequence;
    private long _sessionIdentity;
    private long _generation;
    private long _staleResultDisposals;
    private long _cacheHits;
    private long _foregroundLoadAttempts;
    private long _foregroundLoadSuccesses;
    private long _speculativeRequests;
    private long _speculativeLoadAttempts;
    private long _speculativeLoadSuccesses;
    private long _speculativeCacheHits;
    private long _speculativeResourceLimitRejections;
    private long _speculativeCancellations;
    private long _speculativeCacheAdds;
    private long _speculativeCacheAddRejections;
    private int _lastSpeculativeCandidateIndex = -1;
    private int _lastSpeculativeOutcome;
    private int _currentIndex;
    private int _requestedIndex;
    private bool _disposed;

    public ViewerSession(
        IImageLoader<T> loader,
        ByteBudgetCache<string, T> cache,
        AutomaticMemoryPolicy memoryPolicy)
    {
        _loader = loader;
        _cache = cache;
        _memoryPolicy = memoryPolicy;
    }

    public event EventHandler? AdjacentPreloadProgressed;

    public long StaleResultDisposals => Interlocked.Read(ref _staleResultDisposals);

    public long CacheHits => Interlocked.Read(ref _cacheHits);

    public ViewerSessionMetrics GetMetrics() => new(
        CacheHits,
        StaleResultDisposals,
        _cache.RetainedBytes,
        _cache.BudgetBytes,
        _cache.RemainingBytes,
        _cache.Count,
        _cache.EvictionCount,
        _cache.RejectedAddCount,
        Interlocked.Read(ref _foregroundLoadAttempts),
        Interlocked.Read(ref _foregroundLoadSuccesses),
        Interlocked.Read(ref _speculativeRequests),
        Interlocked.Read(ref _speculativeLoadAttempts),
        Interlocked.Read(ref _speculativeLoadSuccesses),
        Interlocked.Read(ref _speculativeCacheHits),
        Interlocked.Read(ref _speculativeResourceLimitRejections),
        Interlocked.Read(ref _speculativeCancellations),
        Interlocked.Read(ref _speculativeCacheAdds),
        Interlocked.Read(ref _speculativeCacheAddRejections),
        Volatile.Read(ref _lastSpeculativeCandidateIndex),
        (SpeculativeLoadOutcome)Volatile.Read(ref _lastSpeculativeOutcome));

    public int CurrentIndex
    {
        get
        {
            lock (_sync)
            {
                return _currentIndex;
            }
        }
    }

    public bool CanNavigate(NavigationDirection direction)
    {
        lock (_sync)
        {
            return _sequence?.CanMoveFrom(_requestedIndex, direction) == true;
        }
    }

    public bool TryAcquireCached(string path, out SharedResourceLease<T>? lease) =>
        _cache.TryAcquire(path, out lease);

    public bool RefreshCachedCost(string path, T expectedValue) =>
        _cache.RefreshCost(path, expectedValue);

    public Task WaitForAdjacentPreloadAsync(CancellationToken cancellationToken)
    {
        Task preload;
        lock (_sync)
        {
            preload = _preloadTask;
        }

        return preload.WaitAsync(cancellationToken);
    }

    public IReadOnlyList<CachedResourceLease<T>> AcquireCachedAdjacent()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_sequence is null)
            {
                return [];
            }

            List<CachedResourceLease<T>> result = [];
            foreach (var index in new[] { _currentIndex - 1, _currentIndex + 1 })
            {
                if (index < 0 || index >= _sequence.Paths.Count)
                {
                    continue;
                }

                var path = _sequence.Paths[index];
                if (_cache.TryAcquire(path, out var lease))
                {
                    result.Add(new CachedResourceLease<T>(path, lease!));
                }
            }

            return result;
        }
    }

    public Task<SelectionResult<T>> OpenAsync(
        ImageSequence sequence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        long sessionIdentity;
        long generation;
        CancellationToken token;
        Task cacheClear;
        TaskCompletionSource tracking;
        lock (_sync)
        {
            ThrowIfDisposed();
            CancelForegroundAndPreload();
            cacheClear = _cache.ClearAsync();
            _sequence = sequence;
            _currentIndex = sequence.InitialIndex;
            _requestedIndex = sequence.InitialIndex;
            sessionIdentity = ++_sessionIdentity;
            generation = ++_generation;
            _foregroundCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token,
                cancellationToken);
            token = _foregroundCancellation.Token;
            tracking = CreateTrackingGateUnsafe();
        }

        var task = OpenAfterCacheClearAsync(
            sequence,
            sessionIdentity,
            generation,
            token,
            cacheClear);
        _ = CompleteTrackingAsync(task, tracking);
        return task;
    }

    public Task<SelectionResult<T>> NavigateAsync(
        NavigationDirection direction,
        CancellationToken cancellationToken = default)
    {
        ImageSequence sequence;
        int requestedIndex;
        long sessionIdentity;
        long generation;
        CancellationToken token;
        TaskCompletionSource tracking;
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_sequence is null || !_sequence.CanMoveFrom(_requestedIndex, direction))
            {
                return Task.FromResult(SelectionResult<T>.Simple(SelectionStatus.NoMove, _generation));
            }

            sequence = _sequence;
            requestedIndex = _requestedIndex + (int)direction;
            _requestedIndex = requestedIndex;
            sessionIdentity = _sessionIdentity;
            CancelForegroundAndPreload();
            generation = ++_generation;
            _foregroundCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token,
                cancellationToken);
            token = _foregroundCancellation.Token;
            tracking = CreateTrackingGateUnsafe();
        }

        var task = LoadViableAsync(
            sequence,
            requestedIndex,
            direction,
            sessionIdentity,
            generation,
            token);
        _ = CompleteTrackingAsync(task, tracking);
        return task;
    }

    public Task<InspectionAcquisitionResult<T>> AcquireNeighborForInspectionAsync(
        NavigationDirection direction,
        CancellationToken cancellationToken = default)
    {
        ImageSequence sequence;
        int currentIndex;
        long sessionIdentity;
        long generation;
        CancellationTokenSource linkedCancellation;
        TaskCompletionSource tracking;
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_sequence is null ||
                _requestedIndex != _currentIndex ||
                !_sequence.CanMoveFrom(_currentIndex, direction))
            {
                return Task.FromResult(
                    InspectionAcquisitionResult<T>.Simple(InspectionAcquisitionStatus.Unavailable));
            }

            sequence = _sequence;
            currentIndex = _currentIndex;
            sessionIdentity = _sessionIdentity;
            generation = _generation;
            linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token,
                cancellationToken);
            tracking = CreateTrackingGateUnsafe();
        }

        var task = AcquireNeighborForInspectionCoreAsync(
            sequence,
            currentIndex,
            direction,
            sessionIdentity,
            generation,
            linkedCancellation.Token);
        _ = CompleteInspectionTrackingAsync(task, tracking, linkedCancellation);
        return task;
    }

    public async ValueTask DisposeAsync()
    {
        Task[] runningTasks;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetimeCancellation.Cancel();
            CancelForegroundAndPreload();
            runningTasks = _runningTasks.ToArray();
        }

        try
        {
            await Task.WhenAll(runningTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _foregroundCancellation?.Dispose();
        _preloadCancellation?.Dispose();
        _lifetimeCancellation.Dispose();
        _cache.Dispose();
    }

    private async Task<SelectionResult<T>> OpenAfterCacheClearAsync(
        ImageSequence sequence,
        long sessionIdentity,
        long generation,
        CancellationToken cancellationToken,
        Task cacheClear)
    {
        try
        {
            await cacheClear.ConfigureAwait(false);
            return await LoadExactAsync(
                sequence,
                sequence.InitialIndex,
                sessionIdentity,
                generation,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return SelectionResult<T>.Simple(SelectionStatus.Stale, generation);
        }
    }

    private async Task<SelectionResult<T>> LoadExactAsync(
        ImageSequence sequence,
        int index,
        long sessionIdentity,
        long generation,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var path = sequence.Paths[index];
            var loaded = await LoadPathAsync(path, index, isSpeculative: false, cancellationToken)
                .ConfigureAwait(false);
            if (!loaded.IsSuccess)
            {
                return IsCurrent(sessionIdentity, generation)
                    ? new SelectionResult<T>(
                        SelectionStatus.Failed,
                        path,
                        index,
                        generation,
                        null,
                        loaded.Error,
                        false,
                        stopwatch.Elapsed)
                    : SelectionResult<T>.Simple(SelectionStatus.Stale, generation);
            }

            return PublishLoaded(
                sequence,
                path,
                index,
                loaded.Image!,
                sessionIdentity,
                generation,
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return SelectionResult<T>.Simple(SelectionStatus.Stale, generation);
        }
    }

    private async Task<SelectionResult<T>> LoadViableAsync(
        ImageSequence sequence,
        int startIndex,
        NavigationDirection direction,
        long sessionIdentity,
        long generation,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            foreach (var index in sequence.EnumerateFrom(startIndex, direction))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = sequence.Paths[index];
                if (_cache.TryAcquire(path, out var cached))
                {
                    if (!TryCommitCached(
                            sequence,
                            path,
                            index,
                            cached!,
                            sessionIdentity,
                            generation))
                    {
                        cached!.Dispose();
                        return SelectionResult<T>.Simple(SelectionStatus.Stale, generation);
                    }

                    Interlocked.Increment(ref _cacheHits);
                    StartAdjacentPreload(
                        sequence,
                        index,
                        sessionIdentity,
                        generation,
                        direction);
                    return new SelectionResult<T>(
                        SelectionStatus.Published,
                        path,
                        index,
                        generation,
                        cached,
                        null,
                        true,
                        stopwatch.Elapsed);
                }

                var loaded = await LoadPathAsync(path, index, isSpeculative: false, cancellationToken)
                    .ConfigureAwait(false);
                if (!loaded.IsSuccess)
                {
                    continue;
                }

                return PublishLoaded(
                    sequence,
                    path,
                    index,
                    loaded.Image!,
                    sessionIdentity,
                    generation,
                    stopwatch.Elapsed,
                    direction);
            }

            lock (_sync)
            {
                if (!IsCurrentUnsafe(sessionIdentity, generation))
                {
                    return SelectionResult<T>.Simple(SelectionStatus.Stale, generation);
                }

                _requestedIndex = _currentIndex;
            }

            return SelectionResult<T>.Simple(SelectionStatus.NoViableCandidate, generation);
        }
        catch (OperationCanceledException)
        {
            return SelectionResult<T>.Simple(SelectionStatus.Stale, generation);
        }
    }

    private async Task<InspectionAcquisitionResult<T>> AcquireNeighborForInspectionCoreAsync(
        ImageSequence sequence,
        int currentIndex,
        NavigationDirection direction,
        long sessionIdentity,
        long generation,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var interruptedPreload = false;
        try
        {
            foreach (var index in sequence.EnumerateFrom(currentIndex + (int)direction, direction))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = sequence.Paths[index];
                if (_cache.TryAcquire(path, out var cached))
                {
                    if (!IsInspectionAuthorized(
                            sequence,
                            currentIndex,
                            sessionIdentity,
                            generation,
                            cancellationToken))
                    {
                        cached!.Dispose();
                        return InspectionAcquisitionResult<T>.Simple(
                            cancellationToken.IsCancellationRequested
                                ? InspectionAcquisitionStatus.Canceled
                                : InspectionAcquisitionStatus.Stale);
                    }

                    Interlocked.Increment(ref _cacheHits);
                    return new InspectionAcquisitionResult<T>(
                        InspectionAcquisitionStatus.Acquired,
                        path,
                        index,
                        cached,
                        true,
                        stopwatch.Elapsed);
                }

                interruptedPreload |= CancelPreloadForInspection(
                    sequence,
                    currentIndex,
                    sessionIdentity,
                    generation);
                var loaded = await LoadPathAsync(path, index, isSpeculative: false, cancellationToken)
                    .ConfigureAwait(false);
                if (!loaded.IsSuccess)
                {
                    continue;
                }

                if (!IsInspectionAuthorized(
                        sequence,
                        currentIndex,
                        sessionIdentity,
                        generation,
                        cancellationToken))
                {
                    loaded.Image!.Dispose();
                    Interlocked.Increment(ref _staleResultDisposals);
                    return InspectionAcquisitionResult<T>.Simple(
                        cancellationToken.IsCancellationRequested
                            ? InspectionAcquisitionStatus.Canceled
                            : InspectionAcquisitionStatus.Stale);
                }

                SharedResourceLease<T>? lease;
                lock (_sync)
                {
                    if (!IsInspectionAuthorizedUnsafe(
                            sequence,
                            currentIndex,
                            sessionIdentity,
                            generation) ||
                        cancellationToken.IsCancellationRequested)
                    {
                        loaded.Image!.Dispose();
                        Interlocked.Increment(ref _staleResultDisposals);
                        return InspectionAcquisitionResult<T>.Simple(
                            cancellationToken.IsCancellationRequested
                                ? InspectionAcquisitionStatus.Canceled
                                : InspectionAcquisitionStatus.Stale);
                    }

                    if (!_cache.Add(path, loaded.Image!, protect: false) ||
                        !_cache.TryAcquire(path, out lease))
                    {
                        return InspectionAcquisitionResult<T>.Simple(
                            InspectionAcquisitionStatus.Unavailable);
                    }
                }

                return new InspectionAcquisitionResult<T>(
                    InspectionAcquisitionStatus.Acquired,
                    path,
                    index,
                    lease,
                    false,
                    stopwatch.Elapsed);
            }

            return InspectionAcquisitionResult<T>.Simple(InspectionAcquisitionStatus.Unavailable);
        }
        catch (OperationCanceledException)
        {
            return InspectionAcquisitionResult<T>.Simple(InspectionAcquisitionStatus.Canceled);
        }
        finally
        {
            if (interruptedPreload)
            {
                StartAdjacentPreload(
                    sequence,
                    currentIndex,
                    sessionIdentity,
                    generation,
                    preferredDirection: null);
            }
        }
    }

    private async Task<ImageLoadResult<T>> LoadPathAsync(
        string path,
        int candidateIndex,
        bool isSpeculative,
        CancellationToken cancellationToken)
    {
        if (!isSpeculative)
        {
            Interlocked.Increment(ref _foregroundLoadAttempts);
        }

        var retainedAllowance = isSpeculative
            ? _cache.MaximumUnprotectedEntryBytes
            : _memoryPolicy.CacheBudgetBytes;
        if (retainedAllowance <= 0)
        {
            if (isSpeculative)
            {
                Interlocked.Increment(ref _speculativeResourceLimitRejections);
                RecordLastSpeculative(candidateIndex, SpeculativeLoadOutcome.ResourceLimit);
            }

            return ImageLoadResult<T>.Failure(new ImageLoadError(
                ImageLoadErrorKind.ResourceLimit,
                "No cache budget remains for the requested image."));
        }

        var allowance = new ImageLoadAllowance(
            isSpeculative
                ? _memoryPolicy.SpeculativeDecodeBudgetBytes
                : _memoryPolicy.ForegroundDecodeBudgetBytes,
            retainedAllowance,
            isSpeculative);
        if (isSpeculative)
        {
            Interlocked.Increment(ref _speculativeLoadAttempts);
            RecordLastSpeculative(candidateIndex, SpeculativeLoadOutcome.Started);
        }

        try
        {
            var result = await _loader.LoadAsync(path, allowance, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                if (isSpeculative)
                {
                    Interlocked.Increment(ref _speculativeLoadSuccesses);
                    RecordLastSpeculative(candidateIndex, SpeculativeLoadOutcome.Decoded);
                }
                else
                {
                    Interlocked.Increment(ref _foregroundLoadSuccesses);
                }
            }
            else if (isSpeculative)
            {
                if (result.Error?.Kind == ImageLoadErrorKind.ResourceLimit)
                {
                    Interlocked.Increment(ref _speculativeResourceLimitRejections);
                    RecordLastSpeculative(candidateIndex, SpeculativeLoadOutcome.ResourceLimit);
                }
                else
                {
                    RecordLastSpeculative(candidateIndex, SpeculativeLoadOutcome.Failed);
                }
            }

            return result;
        }
        catch (OperationCanceledException) when (isSpeculative)
        {
            Interlocked.Increment(ref _speculativeCancellations);
            RecordLastSpeculative(candidateIndex, SpeculativeLoadOutcome.Canceled);
            throw;
        }
    }

    private SelectionResult<T> PublishLoaded(
        ImageSequence sequence,
        string path,
        int index,
        T image,
        long sessionIdentity,
        long generation,
        TimeSpan latency,
        NavigationDirection? preferredDirection = null)
    {
        SharedResourceLease<T>? lease;
        lock (_sync)
        {
            if (!IsCurrentUnsafe(sessionIdentity, generation))
            {
                image.Dispose();
                Interlocked.Increment(ref _staleResultDisposals);
                return SelectionResult<T>.Simple(SelectionStatus.Stale, generation);
            }

            if (!_cache.Add(path, image, protect: true) || !_cache.TryAcquire(path, out lease))
            {
                _requestedIndex = _currentIndex;
                return new SelectionResult<T>(
                    SelectionStatus.Failed,
                    path,
                    index,
                    generation,
                    null,
                    new ImageLoadError(ImageLoadErrorKind.ResourceLimit, "The decoded image did not fit the cache budget."),
                    false,
                    latency);
            }

            _currentIndex = index;
            _requestedIndex = index;
        }

        StartAdjacentPreload(
            sequence,
            index,
            sessionIdentity,
            generation,
            preferredDirection);
        Debug.WriteLine($"Fovium publish {Path.GetFileName(path)} in {latency.TotalMilliseconds:F2} ms; cache {_cache.RetainedBytes} bytes.");
        return new SelectionResult<T>(
            SelectionStatus.Published,
            path,
            index,
            generation,
            lease,
            null,
            false,
            latency);
    }

    private bool TryCommitCached(
        ImageSequence sequence,
        string path,
        int index,
        SharedResourceLease<T> lease,
        long sessionIdentity,
        long generation)
    {
        lock (_sync)
        {
            if (!ReferenceEquals(_sequence, sequence) || !IsCurrentUnsafe(sessionIdentity, generation))
            {
                return false;
            }

            _cache.Protect(path);
            _currentIndex = index;
            _requestedIndex = index;
            return true;
        }
    }

    private void StartAdjacentPreload(
        ImageSequence sequence,
        int currentIndex,
        long sessionIdentity,
        long generation,
        NavigationDirection? preferredDirection)
    {
        lock (_sync)
        {
            if (_disposed ||
                !ReferenceEquals(_sequence, sequence) ||
                sessionIdentity != _sessionIdentity ||
                generation != _generation)
            {
                return;
            }

            _preloadCancellation?.Cancel();
            _preloadCancellation?.Dispose();
            _preloadCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
            _preloadTask = PreloadAdjacentAsync(
                sequence,
                currentIndex,
                sessionIdentity,
                preferredDirection,
                _preloadCancellation.Token);
            RegisterTaskUnsafe(_preloadTask);
        }
    }

    private async Task PreloadAdjacentAsync(
        ImageSequence sequence,
        int currentIndex,
        long sessionIdentity,
        NavigationDirection? preferredDirection,
        CancellationToken cancellationToken)
    {
        try
        {
            var firstDirection = preferredDirection ?? NavigationDirection.Next;
            var secondDirection = firstDirection == NavigationDirection.Next
                ? NavigationDirection.Previous
                : NavigationDirection.Next;
            await PreloadViableAsync(
                sequence,
                currentIndex + (int)firstDirection,
                firstDirection,
                sessionIdentity,
                cancellationToken).ConfigureAwait(false);
            await PreloadViableAsync(
                sequence,
                currentIndex + (int)secondDirection,
                secondDirection,
                sessionIdentity,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PreloadViableAsync(
        ImageSequence sequence,
        int startIndex,
        NavigationDirection direction,
        long sessionIdentity,
        CancellationToken cancellationToken)
    {
        foreach (var index in sequence.EnumerateFrom(startIndex, direction))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = sequence.Paths[index];
            Interlocked.Increment(ref _speculativeRequests);
            RecordLastSpeculative(index, SpeculativeLoadOutcome.Requested);
            if (_cache.TryAcquire(path, out var existing))
            {
                existing!.Dispose();
                Interlocked.Increment(ref _speculativeCacheHits);
                RecordLastSpeculative(index, SpeculativeLoadOutcome.CacheHit);
                NotifyAdjacentPreloadProgressed();
                return;
            }

            var loaded = await LoadPathAsync(path, index, isSpeculative: true, cancellationToken)
                .ConfigureAwait(false);
            if (!loaded.IsSuccess)
            {
                continue;
            }

            var added = false;
            lock (_sync)
            {
                if (_disposed || !ReferenceEquals(_sequence, sequence) || sessionIdentity != _sessionIdentity)
                {
                    loaded.Image!.Dispose();
                    return;
                }

                added = _cache.Add(path, loaded.Image!, protect: false);
            }

            if (added)
            {
                Interlocked.Increment(ref _speculativeCacheAdds);
                RecordLastSpeculative(index, SpeculativeLoadOutcome.Added);
                NotifyAdjacentPreloadProgressed();
            }
            else
            {
                Interlocked.Increment(ref _speculativeCacheAddRejections);
                RecordLastSpeculative(index, SpeculativeLoadOutcome.CacheRejected);
            }

            return;
        }
    }

    private void RecordLastSpeculative(int candidateIndex, SpeculativeLoadOutcome outcome)
    {
        Volatile.Write(ref _lastSpeculativeCandidateIndex, candidateIndex);
        Volatile.Write(ref _lastSpeculativeOutcome, (int)outcome);
    }

    private void NotifyAdjacentPreloadProgressed() =>
        AdjacentPreloadProgressed?.Invoke(this, EventArgs.Empty);

    private bool IsCurrent(long sessionIdentity, long generation)
    {
        lock (_sync)
        {
            return IsCurrentUnsafe(sessionIdentity, generation);
        }
    }

    private bool IsCurrentUnsafe(long sessionIdentity, long generation) =>
        !_disposed && sessionIdentity == _sessionIdentity && generation == _generation;

    private bool IsInspectionAuthorized(
        ImageSequence sequence,
        int currentIndex,
        long sessionIdentity,
        long generation,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            return !cancellationToken.IsCancellationRequested &&
                IsInspectionAuthorizedUnsafe(sequence, currentIndex, sessionIdentity, generation);
        }
    }

    private bool IsInspectionAuthorizedUnsafe(
        ImageSequence sequence,
        int currentIndex,
        long sessionIdentity,
        long generation) =>
        !_disposed &&
        ReferenceEquals(_sequence, sequence) &&
        sessionIdentity == _sessionIdentity &&
        generation == _generation &&
        currentIndex == _currentIndex &&
        currentIndex == _requestedIndex;

    private bool CancelPreloadForInspection(
        ImageSequence sequence,
        int currentIndex,
        long sessionIdentity,
        long generation)
    {
        lock (_sync)
        {
            if (!IsInspectionAuthorizedUnsafe(sequence, currentIndex, sessionIdentity, generation) ||
                _preloadCancellation is null)
            {
                return false;
            }

            _preloadCancellation.Cancel();
            _preloadCancellation.Dispose();
            _preloadCancellation = null;
            return true;
        }
    }

    private void CancelForegroundAndPreload()
    {
        _foregroundCancellation?.Cancel();
        _foregroundCancellation?.Dispose();
        _foregroundCancellation = null;
        _preloadCancellation?.Cancel();
        _preloadCancellation?.Dispose();
        _preloadCancellation = null;
    }

    private TaskCompletionSource CreateTrackingGateUnsafe()
    {
        var tracking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RegisterTaskUnsafe(tracking.Task);
        return tracking;
    }

    private static async Task CompleteTrackingAsync(Task operation, TaskCompletionSource tracking)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch
        {
            // The caller owns observation of the original operation task.
        }
        finally
        {
            tracking.TrySetResult();
        }
    }

    private static async Task CompleteInspectionTrackingAsync(
        Task operation,
        TaskCompletionSource tracking,
        CancellationTokenSource cancellation)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch
        {
            // The caller owns observation of the original operation task.
        }
        finally
        {
            cancellation.Dispose();
            tracking.TrySetResult();
        }
    }

    private void RegisterTaskUnsafe(Task task)
    {
        _runningTasks.Add(task);
        _ = task.ContinueWith(
            completed =>
            {
                lock (_sync)
                {
                    _runningTasks.Remove(completed);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
