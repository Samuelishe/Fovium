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
    TimeSpan LastPreparationDuration,
    long CurrentAmbientCacheHitCount,
    long CurrentAmbientPrepareCount,
    long AdjacentAmbientPreparedCount,
    TimeSpan LastCurrentAmbientPreparationDuration,
    TimeSpan? LastPhotoToAmbientPresentationGap,
    bool LastCurrentAmbientWasCacheHit);

internal sealed class StagePresentation : IDisposable
{
    private DecodedImage.AmbientLease? _ambient;

    public StagePresentation(
        StageSettings stage,
        long? imageIdentity,
        DecodedImage.AmbientLease? ambient)
    {
        Stage = stage;
        ImageIdentity = imageIdentity;
        _ambient = ambient;
    }

    public StageSettings Stage { get; }

    public long? ImageIdentity { get; }

    public DecodedImage.AmbientLease? Ambient => Volatile.Read(ref _ambient);

    public DecodedImage.AmbientLease? TakeAmbient() => Interlocked.Exchange(ref _ambient, null);

    public void Dispose() => Interlocked.Exchange(ref _ambient, null)?.Dispose();
}

internal sealed class AmbientStageCoordinator : IAsyncDisposable
{
    internal static readonly TimeSpan BlurDebounce = TimeSpan.FromMilliseconds(150);

    private readonly object _sync = new();
    private readonly IAmbientImageRepository _repository;
    private readonly IAmbientStagePreparer _preparer;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _adjacentPreparationGate = new(1, 1);
    private readonly HashSet<Task> _runningTasks = [];
    private CancellationTokenSource? _workCancellation;
    private string? _currentPath;
    private long? _currentIdentity;
    private long _sourceGeneration;
    private long? _transitionalAmbientIdentity;
    private StageSettings _stage;
    private AmbientStageDiagnostic? _lastDiagnostic;
    private long _preparedCount;
    private long _scheduledWorkCount;
    private long _cacheHitCount;
    private long _staleDisposalCount;
    private long _preparationFailureCount;
    private long _preparedRetainedBytes;
    private long _lastPreparationTicks;
    private long _currentAmbientCacheHitCount;
    private long _currentAmbientPrepareCount;
    private long _adjacentAmbientPreparedCount;
    private long _lastCurrentAmbientPreparationTicks;
    private long _lastPhotoToAmbientPresentationGapTicks = -1;
    private long _photoPublicationTimestamp;
    private long _photoPublicationGeneration;
    private long _ambientAvailabilityGeneration;
    private long _currentReadyGeneration;
    private long _startedSelectionGeneration;
    private int _lastCurrentAmbientWasCacheHit;
    private bool _disposed;

    public AmbientStageCoordinator(
        IAmbientImageRepository repository,
        IAmbientStagePreparer preparer,
        StageSettings initialStage)
    {
        _repository = repository;
        _preparer = preparer;
        _stage = initialStage.Normalize();
        _repository.AdjacentImageAvailable += OnAdjacentImageAvailable;
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
        TimeSpan.FromTicks(Interlocked.Read(ref _lastPreparationTicks)),
        Interlocked.Read(ref _currentAmbientCacheHitCount),
        Interlocked.Read(ref _currentAmbientPrepareCount),
        Interlocked.Read(ref _adjacentAmbientPreparedCount),
        TimeSpan.FromTicks(Interlocked.Read(ref _lastCurrentAmbientPreparationTicks)),
        ReadPresentationGap(),
        Volatile.Read(ref _lastCurrentAmbientWasCacheHit) != 0);

    public StagePresentation BeginImageSelection(string path, long imageIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        lock (_sync)
        {
            ThrowIfDisposed();
            CancelWorkUnsafe();
            _currentPath = path;
            _currentIdentity = imageIdentity;
            _transitionalAmbientIdentity = null;
            _sourceGeneration++;
            _startedSelectionGeneration = 0;
            _photoPublicationTimestamp = 0;
            _photoPublicationGeneration = 0;
            _ambientAvailabilityGeneration = 0;
            _currentReadyGeneration = 0;
            Interlocked.Exchange(ref _lastPhotoToAmbientPresentationGapTicks, -1);
            Interlocked.Exchange(ref _lastCurrentAmbientPreparationTicks, 0);
            Volatile.Write(ref _lastCurrentAmbientWasCacheHit, 0);
        }

        return AcquirePresentation();
    }

    public void StartCurrentImageWork()
    {
        string? path;
        long identity;
        long generation;
        double blur;
        CancellationToken token;
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!_stage.BackgroundMode.RequiresAmbient() ||
                _currentPath is null ||
                _currentIdentity is null ||
                _startedSelectionGeneration == _sourceGeneration)
            {
                return;
            }

            path = _currentPath;
            identity = _currentIdentity.Value;
            generation = _sourceGeneration;
            blur = _stage.AmbientBlur;
            _startedSelectionGeneration = generation;
            _photoPublicationTimestamp = Stopwatch.GetTimestamp();
            _photoPublicationGeneration = generation;
            token = CreateWorkTokenUnsafe();
        }

        if (HasMatchingAmbient(path, identity, blur))
        {
            RecordCurrentAmbientAvailability(
                generation,
                path,
                identity,
                blur,
                wasCacheHit: true,
                preparationDuration: TimeSpan.Zero);
            MarkCurrentReady(generation, path, identity, blur);
        }

        StartWork(generation, path, identity, blur, debounce: false, token);
    }

    public void ClearImage()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            CancelWorkUnsafe();
            _currentPath = null;
            _currentIdentity = null;
            _transitionalAmbientIdentity = null;
            _sourceGeneration++;
            _startedSelectionGeneration = 0;
            _photoPublicationGeneration = 0;
            _ambientAvailabilityGeneration = 0;
            _currentReadyGeneration = 0;
            Interlocked.Exchange(ref _lastPhotoToAmbientPresentationGapTicks, -1);
            Interlocked.Exchange(ref _lastCurrentAmbientPreparationTicks, 0);
            Volatile.Write(ref _lastCurrentAmbientWasCacheHit, 0);
        }

        NotifyPresentationChanged();
    }

    public void SetStage(StageSettings stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        var normalized = stage.Normalize();
        string? path;
        long? identity;
        long generation = 0;
        CancellationToken token = default;
        var shouldStart = false;
        var debounce = false;
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_stage == normalized)
            {
                return;
            }

            var previouslyRequiredAmbient = _stage.BackgroundMode.RequiresAmbient();
            var nowRequiresAmbient = normalized.BackgroundMode.RequiresAmbient();
            var blurChanged = !_stage.AmbientBlur.Equals(normalized.AmbientBlur);
            _stage = normalized;
            path = _currentPath;
            identity = _currentIdentity;
            if (!nowRequiresAmbient)
            {
                CancelWorkUnsafe();
                _sourceGeneration++;
                _startedSelectionGeneration = 0;
                _transitionalAmbientIdentity = null;
            }
            else if ((!previouslyRequiredAmbient || blurChanged) && path is not null && identity is not null)
            {
                CancelWorkUnsafe();
                generation = ++_sourceGeneration;
                _startedSelectionGeneration = generation;
                _transitionalAmbientIdentity = blurChanged ? identity : null;
                _currentReadyGeneration = 0;
                token = CreateWorkTokenUnsafe();
                shouldStart = true;
                debounce = previouslyRequiredAmbient && blurChanged;
            }
        }

        NotifyPresentationChanged();
        if (shouldStart)
        {
            StartWork(
                generation,
                path!,
                identity!.Value,
                normalized.AmbientBlur,
                debounce,
                token);
        }
    }

    public StagePresentation AcquirePresentation()
    {
        string? path;
        long? identity;
        StageSettings stage;
        lock (_sync)
        {
            stage = _stage;
            path = _currentPath;
            identity = _currentIdentity;
        }

        if (!stage.BackgroundMode.RequiresAmbient() || path is null || identity is null ||
            !_repository.TryAcquire(path, out var imageLease))
        {
            return new StagePresentation(stage, identity, null);
        }

        using (imageLease)
        {
            if (imageLease!.Value.Identity != identity.Value)
            {
                return new StagePresentation(stage, identity, null);
            }

            var ambient = imageLease.Value.TryAcquireAmbient();
            if (ambient is not null &&
                !ambient.Blur.Equals(stage.AmbientBlur) &&
                identity != _transitionalAmbientIdentity)
            {
                ambient.Dispose();
                ambient = null;
            }
            if (ambient is not null)
            {
                Interlocked.Increment(ref _cacheHitCount);
            }

            return new StagePresentation(stage, identity, ambient);
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
            _repository.AdjacentImageAvailable -= OnAdjacentImageAvailable;
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
        _adjacentPreparationGate.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private void StartWork(
        long generation,
        string path,
        long identity,
        double blur,
        bool debounce,
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
                    blur,
                    debounce,
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
        double blur,
        bool debounce,
        CancellationToken cancellationToken)
    {
        try
        {
            if (debounce)
            {
                await Task.Delay(BlurDebounce, cancellationToken).ConfigureAwait(false);
            }

            if (!IsAuthorized(generation, path, identity, blur))
            {
                return;
            }

            PrepareOne(path, identity, generation, blur, publishIfCurrent: true, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await PrepareAvailableAdjacentAsync(generation, blur, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Interlocked.Increment(ref _preparationFailureCount);
            lock (_sync)
            {
                _transitionalAmbientIdentity = null;
                _lastDiagnostic = new AmbientStageDiagnostic(
                    "Ambient preparation failed; a matching previous Ambient or Black fallback remains active.",
                    exception);
            }

            Debug.WriteLine($"Fovium Ambient preparation failed: {exception}");
            NotifyPresentationChanged();
        }
    }

    private void OnAdjacentImageAvailable(object? sender, EventArgs e)
    {
        long generation;
        double blur;
        CancellationToken token;
        lock (_sync)
        {
            if (_disposed ||
                !_stage.BackgroundMode.RequiresAmbient() ||
                _startedSelectionGeneration != _sourceGeneration ||
                _currentReadyGeneration != _sourceGeneration ||
                _workCancellation is null)
            {
                return;
            }

            generation = _sourceGeneration;
            blur = _stage.AmbientBlur;
            token = _workCancellation.Token;
        }

        StartAdjacentWork(generation, blur, token);
    }

    private void StartAdjacentWork(long generation, double blur, CancellationToken cancellationToken)
    {
        Task task;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            task = Task.Run(
                () => PrepareAvailableAdjacentAsync(generation, blur, cancellationToken),
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

    private async Task PrepareAvailableAdjacentAsync(
        long generation,
        double blur,
        CancellationToken cancellationToken)
    {
        await _adjacentPreparationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsAuthorized(generation, blur))
            {
                return;
            }

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
                        blur,
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
        finally
        {
            _adjacentPreparationGate.Release();
        }
    }

    private void PrepareOne(
        string path,
        long identity,
        long generation,
        double blur,
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
            if (image.Identity != identity || image.HasAmbientForBlur(blur))
            {
                if (publishIfCurrent && image.Identity == identity && image.HasAmbientForBlur(blur))
                {
                    MarkCurrentReady(generation, path, identity, blur);
                    var becameAvailable = RecordCurrentAmbientAvailability(
                        generation,
                        path,
                        identity,
                        blur,
                        wasCacheHit: true,
                        preparationDuration: TimeSpan.Zero);
                    if (becameAvailable)
                    {
                        NotifyPresentationChanged();
                    }
                }

                return;
            }

            var prepared = _preparer.Prepare(image, blur, cancellationToken);
            if (!IsAuthorized(generation, blur) || !image.TrySetAmbient(prepared))
            {
                prepared.Dispose();
                Interlocked.Increment(ref _staleDisposalCount);
                return;
            }

            if (!IsAuthorized(generation, blur) || !_repository.RefreshRetainedCost(path, image))
            {
                image.RemoveAmbient(prepared);
                Interlocked.Increment(ref _staleDisposalCount);
                return;
            }

            Interlocked.Increment(ref _preparedCount);
            Interlocked.Add(ref _preparedRetainedBytes, prepared.RetainedBytes);
            Interlocked.Exchange(ref _lastPreparationTicks, prepared.PreparationDuration.Ticks);
            Debug.WriteLine(
                $"Fovium Ambient prepared {prepared.Size.Width}x{prepared.Size.Height}, blur {blur:F1}, " +
                $"{prepared.RetainedBytes} bytes in {prepared.PreparationDuration.TotalMilliseconds:F2} ms.");
            if (publishIfCurrent && IsAuthorized(generation, path, identity, blur))
            {
                lock (_sync)
                {
                    if (_currentIdentity == identity)
                    {
                        _transitionalAmbientIdentity = null;
                    }
                }

                MarkCurrentReady(generation, path, identity, blur);
                _ = RecordCurrentAmbientAvailability(
                    generation,
                    path,
                    identity,
                    blur,
                    wasCacheHit: false,
                    prepared.PreparationDuration);
                NotifyPresentationChanged();
            }
            else if (!publishIfCurrent)
            {
                Interlocked.Increment(ref _adjacentAmbientPreparedCount);
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

    private bool IsAuthorized(long generation, double blur)
    {
        lock (_sync)
        {
            return !_disposed &&
                generation == _sourceGeneration &&
                _stage.BackgroundMode.RequiresAmbient() &&
                _stage.AmbientBlur.Equals(blur);
        }
    }

    private bool IsAuthorized(long generation, string path, long identity, double blur)
    {
        lock (_sync)
        {
            return IsAuthorizedUnsafe(generation, path, identity, blur);
        }
    }

    private bool HasMatchingAmbient(string path, long identity, double blur)
    {
        if (!_repository.TryAcquire(path, out var imageLease))
        {
            return false;
        }

        using (imageLease)
        {
            return imageLease!.Value.Identity == identity && imageLease.Value.HasAmbientForBlur(blur);
        }
    }

    private bool RecordCurrentAmbientAvailability(
        long generation,
        string path,
        long identity,
        double blur,
        bool wasCacheHit,
        TimeSpan preparationDuration)
    {
        TimeSpan gap;
        lock (_sync)
        {
            if (!IsAuthorizedUnsafe(generation, path, identity, blur) ||
                _photoPublicationGeneration != generation ||
                _ambientAvailabilityGeneration == generation)
            {
                return false;
            }

            _ambientAvailabilityGeneration = generation;
            gap = Stopwatch.GetElapsedTime(_photoPublicationTimestamp);
            Interlocked.Exchange(ref _lastPhotoToAmbientPresentationGapTicks, gap.Ticks);
            Volatile.Write(ref _lastCurrentAmbientWasCacheHit, wasCacheHit ? 1 : 0);
            if (wasCacheHit)
            {
                Interlocked.Increment(ref _currentAmbientCacheHitCount);
                Interlocked.Exchange(ref _lastCurrentAmbientPreparationTicks, 0);
            }
            else
            {
                Interlocked.Increment(ref _currentAmbientPrepareCount);
                Interlocked.Exchange(
                    ref _lastCurrentAmbientPreparationTicks,
                    preparationDuration.Ticks);
            }
        }

        Debug.WriteLine(
            $"Fovium Ambient current {(wasCacheHit ? "cache hit" : "prepared")} identity {identity}, " +
            $"blur {blur:F1}, preparation {preparationDuration.TotalMilliseconds:F2} ms, " +
            $"photo-to-Ambient {gap.TotalMilliseconds:F2} ms.");
        return true;
    }

    private void MarkCurrentReady(long generation, string path, long identity, double blur)
    {
        lock (_sync)
        {
            if (IsAuthorizedUnsafe(generation, path, identity, blur))
            {
                _currentReadyGeneration = generation;
            }
        }
    }

    private bool IsAuthorizedUnsafe(long generation, string path, long identity, double blur) =>
        !_disposed &&
        generation == _sourceGeneration &&
        _stage.BackgroundMode.RequiresAmbient() &&
        _stage.AmbientBlur.Equals(blur) &&
        string.Equals(path, _currentPath, StringComparison.Ordinal) &&
        identity == _currentIdentity;

    private TimeSpan? ReadPresentationGap()
    {
        var ticks = Interlocked.Read(ref _lastPhotoToAmbientPresentationGapTicks);
        return ticks < 0 ? null : TimeSpan.FromTicks(ticks);
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
