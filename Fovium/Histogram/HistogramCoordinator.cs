using System.Diagnostics;
using Fovium.Viewer;

namespace Fovium.Histogram;

internal sealed record HistogramState(
    string PresentationIdentity,
    long ImageIdentity,
    HistogramData? Data,
    bool IsLoading);

internal readonly record struct HistogramMetrics(
    long ComputationsStarted,
    long ComputationsCompleted,
    long CacheHits,
    long Canceled,
    long StaleResults,
    long Failures,
    TimeSpan LastComputeDuration,
    long LastSampleCount,
    bool LastWasSampled);

internal sealed class HistogramCoordinator : IDisposable
{
    private readonly object _sync = new();
    private readonly IPresentedImageSource _source;
    private readonly IImageHistogramReader _reader;
    private readonly HistogramCache _cache;
    private CancellationTokenSource? _requestCancellation;
    private HistogramState? _state;
    private long _generation;
    private bool _visible;
    private bool _disposed;
    private long _started;
    private long _completed;
    private long _cacheHits;
    private long _canceled;
    private long _stale;
    private long _failures;
    private long _lastDurationTicks;
    private long _lastSampleCount;
    private int _lastWasSampled;

    public HistogramCoordinator(
        IPresentedImageSource source,
        IImageHistogramReader reader,
        HistogramCache? cache = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _cache = cache ?? new HistogramCache();
        _source.PresentedImageChanged += OnPresentedImageChanged;
    }

    public event EventHandler? StateChanged;

    public bool IsVisible
    {
        get
        {
            lock (_sync)
            {
                return _visible;
            }
        }
    }

    public HistogramState? CurrentState
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public HistogramMetrics Metrics => new(
        Interlocked.Read(ref _started),
        Interlocked.Read(ref _completed),
        Interlocked.Read(ref _cacheHits),
        Interlocked.Read(ref _canceled),
        Interlocked.Read(ref _stale),
        Interlocked.Read(ref _failures),
        TimeSpan.FromTicks(Interlocked.Read(ref _lastDurationTicks)),
        Interlocked.Read(ref _lastSampleCount),
        Volatile.Read(ref _lastWasSampled) != 0);

    public void Toggle() => SetVisible(!IsVisible);

    public void SetVisible(bool visible)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_visible == visible)
            {
                return;
            }

            _visible = visible;
            if (!visible)
            {
                CancelRequestLocked();
                _state = null;
            }
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        if (visible)
        {
            RefreshPresentedImage();
        }
    }

    public void BeginNewSequence()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _cache.Clear();
            CancelRequestLocked();
            _state = null;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
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
            _source.PresentedImageChanged -= OnPresentedImageChanged;
            CancelRequestLocked();
            _state = null;
            _cache.Clear();
        }

    }

    private void OnPresentedImageChanged(object? sender, EventArgs e)
    {
        if (IsVisible)
        {
            RefreshPresentedImage();
        }
    }

    private void RefreshPresentedImage()
    {
        if (!_source.TryAcquirePresentedImage(out var presented) || presented is null)
        {
            lock (_sync)
            {
                if (!_visible || _disposed)
                {
                    return;
                }

                CancelRequestLocked();
                _state = null;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        HistogramReadResult? cached;
        CancellationTokenSource? cancellation = null;
        long generation;
        lock (_sync)
        {
            if (!_visible || _disposed)
            {
                presented.Dispose();
                return;
            }

            CancelRequestLocked();
            generation = ++_generation;
            if (_cache.TryGet(presented.ImageIdentity, out cached))
            {
                Interlocked.Increment(ref _cacheHits);
                _state = new HistogramState(
                    presented.PresentationIdentity,
                    presented.ImageIdentity,
                    cached!.Data,
                    IsLoading: false);
            }
            else
            {
                cancellation = new CancellationTokenSource();
                _requestCancellation = cancellation;
                _state = new HistogramState(
                    presented.PresentationIdentity,
                    presented.ImageIdentity,
                    Data: null,
                    IsLoading: true);
            }
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        if (cached is not null)
        {
            presented.Dispose();
            return;
        }

        Interlocked.Increment(ref _started);
        _ = ReadAndPublishAsync(presented, generation, cancellation!);
    }

    private async Task ReadAndPublishAsync(
        PresentedImageLease lease,
        long generation,
        CancellationTokenSource cancellation)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await _reader.ReadAsync(lease.Image, cancellation.Token).ConfigureAwait(false);
            stopwatch.Stop();
            Interlocked.Exchange(ref _lastDurationTicks, stopwatch.Elapsed.Ticks);
            bool publish;
            lock (_sync)
            {
                publish = !_disposed && _visible && generation == _generation &&
                    _state?.ImageIdentity == lease.ImageIdentity &&
                    _state.PresentationIdentity == lease.PresentationIdentity;
                if (publish)
                {
                    _cache.Add(lease.ImageIdentity, result);
                    _state = _state! with { Data = result.Data, IsLoading = false };
                    Interlocked.Increment(ref _completed);
                    if (result.Data is { } data)
                    {
                        Interlocked.Exchange(ref _lastSampleCount, data.SampleCount);
                        Volatile.Write(ref _lastWasSampled, data.WasSampled ? 1 : 0);
                    }

                    if (result.Status != HistogramReadStatus.Success)
                    {
                        Interlocked.Increment(ref _failures);
                    }
                }
                else
                {
                    Interlocked.Increment(ref _stale);
                }
            }

            if (publish)
            {
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Interlocked.Increment(ref _canceled);
        }
        finally
        {
            lease.Dispose();
            lock (_sync)
            {
                if (ReferenceEquals(_requestCancellation, cancellation))
                {
                    _requestCancellation = null;
                }
            }

            cancellation.Dispose();
        }
    }

    private void CancelRequestLocked()
    {
        _generation++;
        var cancellation = _requestCancellation;
        _requestCancellation = null;
        cancellation?.Cancel();
    }
}
