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

internal sealed class ViewerSession<T> : IDisposable, IAsyncDisposable
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
    private ImageSequence? _sequence;
    private long _sessionIdentity;
    private long _generation;
    private long _staleResultDisposals;
    private long _cacheHits;
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

    public long StaleResultDisposals => Interlocked.Read(ref _staleResultDisposals);

    public long CacheHits => Interlocked.Read(ref _cacheHits);

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

    public Task<SelectionResult<T>> OpenAsync(
        ImageSequence sequence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        long sessionIdentity;
        long generation;
        CancellationToken token;
        lock (_sync)
        {
            ThrowIfDisposed();
            CancelForegroundAndPreload();
            _cache.Clear();
            _sequence = sequence;
            _currentIndex = sequence.InitialIndex;
            _requestedIndex = sequence.InitialIndex;
            sessionIdentity = ++_sessionIdentity;
            generation = ++_generation;
            _foregroundCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token,
                cancellationToken);
            token = _foregroundCancellation.Token;
        }

        return LoadExactAsync(sequence, sequence.InitialIndex, sessionIdentity, generation, token);
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
        }

        return LoadViableAsync(
            sequence,
            requestedIndex,
            direction,
            sessionIdentity,
            generation,
            token);
    }

    public async ValueTask DisposeAsync()
    {
        Task preload;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetimeCancellation.Cancel();
            CancelForegroundAndPreload();
            preload = _preloadTask;
        }

        try
        {
            await preload.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _foregroundCancellation?.Dispose();
        _preloadCancellation?.Dispose();
        _lifetimeCancellation.Dispose();
        _cache.Dispose();
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetimeCancellation.Cancel();
            CancelForegroundAndPreload();
        }

        _lifetimeCancellation.Dispose();
        _cache.Dispose();
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
            var loaded = await LoadPathAsync(path, isSpeculative: false, cancellationToken)
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
                    StartAdjacentPreload(sequence, index, sessionIdentity, generation);
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

                var loaded = await LoadPathAsync(path, isSpeculative: false, cancellationToken)
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
                    stopwatch.Elapsed);
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

    private async Task<ImageLoadResult<T>> LoadPathAsync(
        string path,
        bool isSpeculative,
        CancellationToken cancellationToken)
    {
        var remaining = isSpeculative ? _cache.RemainingBytes : _memoryPolicy.CacheBudgetBytes;
        if (remaining <= 0)
        {
            return ImageLoadResult<T>.Failure(new ImageLoadError(
                ImageLoadErrorKind.ResourceLimit,
                "No cache budget remains for the requested image."));
        }

        var allowance = new ImageLoadAllowance(
            isSpeculative
                ? _memoryPolicy.SpeculativeDecodeBudgetBytes
                : _memoryPolicy.ForegroundDecodeBudgetBytes,
            remaining,
            isSpeculative);
        return await _loader.LoadAsync(path, allowance, cancellationToken).ConfigureAwait(false);
    }

    private SelectionResult<T> PublishLoaded(
        ImageSequence sequence,
        string path,
        int index,
        T image,
        long sessionIdentity,
        long generation,
        TimeSpan latency)
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

        StartAdjacentPreload(sequence, index, sessionIdentity, generation);
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
        long generation)
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
                _preloadCancellation.Token);
        }
    }

    private async Task PreloadAdjacentAsync(
        ImageSequence sequence,
        int currentIndex,
        long sessionIdentity,
        CancellationToken cancellationToken)
    {
        try
        {
            await PreloadViableAsync(
                sequence,
                currentIndex - 1,
                NavigationDirection.Previous,
                sessionIdentity,
                cancellationToken).ConfigureAwait(false);
            await PreloadViableAsync(
                sequence,
                currentIndex + 1,
                NavigationDirection.Next,
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
            if (_cache.TryAcquire(path, out var existing))
            {
                existing!.Dispose();
                return;
            }

            var loaded = await LoadPathAsync(path, isSpeculative: true, cancellationToken)
                .ConfigureAwait(false);
            if (!loaded.IsSuccess)
            {
                continue;
            }

            lock (_sync)
            {
                if (_disposed || !ReferenceEquals(_sequence, sequence) || sessionIdentity != _sessionIdentity)
                {
                    loaded.Image!.Dispose();
                    return;
                }

                _cache.Add(path, loaded.Image!, protect: false);
            }

            return;
        }
    }

    private bool IsCurrent(long sessionIdentity, long generation)
    {
        lock (_sync)
        {
            return IsCurrentUnsafe(sessionIdentity, generation);
        }
    }

    private bool IsCurrentUnsafe(long sessionIdentity, long generation) =>
        !_disposed && sessionIdentity == _sessionIdentity && generation == _generation;

    private void CancelForegroundAndPreload()
    {
        _foregroundCancellation?.Cancel();
        _foregroundCancellation?.Dispose();
        _foregroundCancellation = null;
        _preloadCancellation?.Cancel();
        _preloadCancellation?.Dispose();
        _preloadCancellation = null;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
