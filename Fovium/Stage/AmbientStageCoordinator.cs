using System.Diagnostics;
using Fovium.Imaging;
using Fovium.Loading;

namespace Fovium.Stage;

internal sealed record AmbientStageDiagnostic(string Message, Exception? Exception = null);

internal readonly record struct AmbientStageMetrics(
    long ScheduledWorkCount,
    long PreparedCount,
    long CacheHitCount,
    long StaleDisposalCount,
    long PreparationFailureCount,
    long TotalPreparedBytes,
    TimeSpan LastPreparationDuration);

internal sealed class StagePresentation : IDisposable
{
    private DecodedImage.AmbientLease? _ambient;

    public StagePresentation(
        StageMode mode,
        long? imageIdentity,
        DecodedImage.AmbientLease? ambient)
    {
        Mode = mode;
        ImageIdentity = imageIdentity;
        _ambient = ambient;
    }

    public StageMode Mode { get; }

    public long? ImageIdentity { get; }

    public DecodedImage.AmbientLease? Ambient => Volatile.Read(ref _ambient);

    public DecodedImage.AmbientLease? TakeAmbient() => Interlocked.Exchange(ref _ambient, null);

    public void Dispose() => Interlocked.Exchange(ref _ambient, null)?.Dispose();
}

internal sealed class AmbientStageCoordinator : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly IAmbientImageRepository _repository;
    private readonly IAmbientStagePreparer _preparer;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly HashSet<Task> _runningTasks = [];
    private CancellationTokenSource? _workCancellation;
    private string? _currentPath;
    private long? _currentIdentity;
    private long _sourceGeneration;
    private StageMode _mode;
    private AmbientStageDiagnostic? _lastDiagnostic;
    private long _preparedCount;
    private long _scheduledWorkCount;
    private long _cacheHitCount;
    private long _staleDisposalCount;
    private long _preparationFailureCount;
    private long _preparedRetainedBytes;
    private long _lastPreparationTicks;
    private bool _disposed;

    public AmbientStageCoordinator(
        IAmbientImageRepository repository,
        IAmbientStagePreparer preparer,
        StageMode initialMode)
    {
        _repository = repository;
        _preparer = preparer;
        _mode = initialMode;
    }

    public event EventHandler? PresentationChanged;

    public AmbientStageDiagnostic? LastDiagnostic
    {
        get
        {
            lock (_sync)
            {
                return _lastDiagnostic;
            }
        }
    }

    public AmbientStageMetrics GetMetrics() => new(
        Interlocked.Read(ref _scheduledWorkCount),
        Interlocked.Read(ref _preparedCount),
        Interlocked.Read(ref _cacheHitCount),
        Interlocked.Read(ref _staleDisposalCount),
        Interlocked.Read(ref _preparationFailureCount),
        Interlocked.Read(ref _preparedRetainedBytes),
        TimeSpan.FromTicks(Interlocked.Read(ref _lastPreparationTicks)));

    public void SelectImage(string path, long imageIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        long generation;
        CancellationToken token;
        bool shouldPrepare;
        lock (_sync)
        {
            ThrowIfDisposed();
            CancelWorkUnsafe();
            _currentPath = path;
            _currentIdentity = imageIdentity;
            generation = ++_sourceGeneration;
            shouldPrepare = _mode.RequiresAmbient();
            token = shouldPrepare ? CreateWorkTokenUnsafe() : CancellationToken.None;
        }

        NotifyPresentationChanged();
        if (shouldPrepare)
        {
            StartWork(generation, path, imageIdentity, token);
        }
    }

    public void ClearImage()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            CancelWorkUnsafe();
            _currentPath = null;
            _currentIdentity = null;
            _sourceGeneration++;
        }

        NotifyPresentationChanged();
    }

    public void SetMode(StageMode mode)
    {
        string? path;
        long? identity;
        long generation = 0;
        CancellationToken token = default;
        var shouldStart = false;
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_mode == mode)
            {
                return;
            }

            var previouslyRequiredAmbient = _mode.RequiresAmbient();
            _mode = mode;
            path = _currentPath;
            identity = _currentIdentity;
            if (!mode.RequiresAmbient())
            {
                CancelWorkUnsafe();
                _sourceGeneration++;
            }
            else if (!previouslyRequiredAmbient && path is not null && identity is not null)
            {
                CancelWorkUnsafe();
                generation = ++_sourceGeneration;
                token = CreateWorkTokenUnsafe();
                shouldStart = true;
            }
        }

        NotifyPresentationChanged();
        if (shouldStart)
        {
            StartWork(generation, path!, identity!.Value, token);
        }
    }

    public StagePresentation AcquirePresentation()
    {
        string? path;
        long? identity;
        StageMode mode;
        lock (_sync)
        {
            mode = _mode;
            path = _currentPath;
            identity = _currentIdentity;
        }

        if (!mode.RequiresAmbient() || path is null || identity is null ||
            !_repository.TryAcquire(path, out var imageLease))
        {
            return new StagePresentation(mode, identity, null);
        }

        using (imageLease)
        {
            if (imageLease!.Value.Identity != identity.Value)
            {
                return new StagePresentation(mode, identity, null);
            }

            var ambient = imageLease.Value.TryAcquireAmbient();
            if (ambient is not null)
            {
                Interlocked.Increment(ref _cacheHitCount);
            }

            return new StagePresentation(mode, identity, ambient);
        }
    }

    public async Task WaitForIdleAsync()
    {
        while (true)
        {
            Task[] running;
            lock (_sync)
            {
                running = _runningTasks.ToArray();
            }

            if (running.Length == 0)
            {
                return;
            }

            await Task.WhenAll(running).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task[] running;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetimeCancellation.Cancel();
            CancelWorkUnsafe();
            running = _runningTasks.ToArray();
        }

        try
        {
            await Task.WhenAll(running).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _workCancellation?.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private void StartWork(
        long generation,
        string path,
        long identity,
        CancellationToken cancellationToken)
    {
        Task task;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            task = Task.Run(
                () => PrepareCurrentAndAdjacentAsync(
                    generation,
                    path,
                    identity,
                    cancellationToken),
                CancellationToken.None);
            _runningTasks.Add(task);
            Interlocked.Increment(ref _scheduledWorkCount);
        }

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

    private async Task PrepareCurrentAndAdjacentAsync(
        long generation,
        string path,
        long identity,
        CancellationToken cancellationToken)
    {
        try
        {
            // Image publication and ordinary adjacent decode always outrank decorative work.
            await _repository.WaitForAdjacentPreloadAsync(cancellationToken).ConfigureAwait(false);
            if (!IsAuthorized(generation, path, identity))
            {
                return;
            }

            PrepareOne(path, identity, generation, publishIfCurrent: true, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var adjacent = _repository.AcquireAdjacent();
            try
            {
                foreach (var candidate in adjacent)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    PrepareOne(
                        candidate.Path,
                        candidate.Resource.Value.Identity,
                        generation,
                        publishIfCurrent: false,
                        cancellationToken,
                        candidate.Resource);
                }
            }
            finally
            {
                foreach (var candidate in adjacent)
                {
                    candidate.Dispose();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Interlocked.Increment(ref _preparationFailureCount);
            lock (_sync)
            {
                _lastDiagnostic = new AmbientStageDiagnostic(
                    "Ambient preparation failed; Black fallback remains active.",
                    exception);
            }

            Debug.WriteLine($"Fovium Ambient preparation failed: {exception}");
            NotifyPresentationChanged();
        }
    }

    private void PrepareOne(
        string path,
        long identity,
        long generation,
        bool publishIfCurrent,
        CancellationToken cancellationToken,
        SharedResourceLease<DecodedImage>? existingLease = null)
    {
        var ownsLease = existingLease is null;
        var imageLease = existingLease;
        if (imageLease is null && !_repository.TryAcquire(path, out imageLease))
        {
            return;
        }

        try
        {
            var image = imageLease!.Value;
            if (image.Identity != identity || image.HasAmbient)
            {
                if (publishIfCurrent && image.Identity == identity && image.HasAmbient)
                {
                    NotifyPresentationChanged();
                }

                return;
            }

            var prepared = _preparer.Prepare(image, cancellationToken);
            if (!IsAuthorized(generation) || !image.TryAttachAmbient(prepared))
            {
                prepared.Dispose();
                Interlocked.Increment(ref _staleDisposalCount);
                return;
            }

            if (!IsAuthorized(generation) || !_repository.RefreshRetainedCost(path, image))
            {
                image.RemoveAmbient(prepared);
                Interlocked.Increment(ref _staleDisposalCount);
                return;
            }

            Interlocked.Increment(ref _preparedCount);
            Interlocked.Add(ref _preparedRetainedBytes, prepared.RetainedBytes);
            Interlocked.Exchange(ref _lastPreparationTicks, prepared.PreparationDuration.Ticks);
            Debug.WriteLine(
                $"Fovium Ambient prepared {prepared.Size.Width}x{prepared.Size.Height}, " +
                $"{prepared.RetainedBytes} bytes in {prepared.PreparationDuration.TotalMilliseconds:F2} ms.");
            if (publishIfCurrent && IsAuthorized(generation, path, identity))
            {
                NotifyPresentationChanged();
            }
        }
        finally
        {
            if (ownsLease)
            {
                imageLease?.Dispose();
            }
        }
    }

    private bool IsAuthorized(long generation)
    {
        lock (_sync)
        {
            return !_disposed && generation == _sourceGeneration && _mode.RequiresAmbient();
        }
    }

    private bool IsAuthorized(long generation, string path, long identity)
    {
        lock (_sync)
        {
            return !_disposed &&
                generation == _sourceGeneration &&
                _mode.RequiresAmbient() &&
                string.Equals(path, _currentPath, StringComparison.Ordinal) &&
                identity == _currentIdentity;
        }
    }

    private CancellationToken CreateWorkTokenUnsafe()
    {
        _workCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        return _workCancellation.Token;
    }

    private void CancelWorkUnsafe()
    {
        _workCancellation?.Cancel();
        _workCancellation?.Dispose();
        _workCancellation = null;
    }

    private void NotifyPresentationChanged() => PresentationChanged?.Invoke(this, EventArgs.Empty);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
