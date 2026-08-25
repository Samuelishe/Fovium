using System.Diagnostics;
using Fovium.Imaging;
using Fovium.Viewer;

namespace Fovium.Metadata;

internal readonly record struct PhotoMetadataMetrics(
    long ReadsStarted,
    long CacheHits,
    long StaleResults,
    long Failures,
    TimeSpan LastReadDuration);

internal sealed class PhotoInfoCoordinator : IDisposable
{
    private readonly object _sync = new();
    private readonly IPresentedImageSource _source;
    private readonly IPhotoMetadataReader _reader;
    private readonly PhotoMetadataCache _cache;
    private CancellationTokenSource? _requestCancellation;
    private PhotoInfoState? _state;
    private long _generation;
    private bool _visible;
    private bool _disposed;
    private long _readsStarted;
    private long _cacheHits;
    private long _staleResults;
    private long _failures;
    private long _lastReadTicks;

    public PhotoInfoCoordinator(
        IPresentedImageSource source,
        IPhotoMetadataReader reader,
        PhotoMetadataCache? cache = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _cache = cache ?? new PhotoMetadataCache();
        source.PresentedImageChanged += OnPresentedImageChanged;
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

    public PhotoInfoState? CurrentState
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public PhotoMetadataMetrics Metrics => new(
        Interlocked.Read(ref _readsStarted),
        Interlocked.Read(ref _cacheHits),
        Interlocked.Read(ref _staleResults),
        Interlocked.Read(ref _failures),
        TimeSpan.FromTicks(Interlocked.Read(ref _lastReadTicks)));

    public void Toggle() => SetVisible(!IsVisible);

    public void SetVisible(bool visible)
    {
        bool changed;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            changed = _visible != visible;
            if (!changed)
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

        var lease = presented;
        var image = lease.Image;
        var info = new PhotoInfoBase(
            image.Identity,
            image.Descriptor.SourcePath,
            image.Descriptor.EncodedFormat,
            image.Descriptor.OrientedSize,
            image.EncodedSource.LongLength);
        PhotoMetadataReadResult? cached;
        CancellationTokenSource? cancellation = null;
        long generation;
        lock (_sync)
        {
            if (!_visible || _disposed)
            {
                lease.Dispose();
                return;
            }

            CancelRequestLocked();
            generation = ++_generation;
            if (_cache.TryGet(image.Identity, out cached))
            {
                Interlocked.Increment(ref _cacheHits);
                _state = new PhotoInfoState(info, cached!.Summary, IsMetadataLoading: false);
            }
            else
            {
                cancellation = new CancellationTokenSource();
                _requestCancellation = cancellation;
                _state = new PhotoInfoState(info, PhotoMetadataSummary.Empty, IsMetadataLoading: true);
            }
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        if (cached is not null)
        {
            lease.Dispose();
            return;
        }

        Interlocked.Increment(ref _readsStarted);
        _ = ReadAndPublishAsync(lease, generation, cancellation!);
    }

    private async Task ReadAndPublishAsync(
        PresentedImageLease lease,
        long generation,
        CancellationTokenSource cancellation)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var image = lease.Image;
            var result = await _reader.ReadAsync(image.EncodedSource, cancellation.Token).ConfigureAwait(false);
            stopwatch.Stop();
            Interlocked.Exchange(ref _lastReadTicks, stopwatch.Elapsed.Ticks);
            bool publish;
            lock (_sync)
            {
                publish = !_disposed && _visible && generation == _generation &&
                    _state?.Base.ImageIdentity == image.Identity;
                if (publish)
                {
                    _cache.Add(image.Identity, result);
                    _state = _state! with { Metadata = result.Summary, IsMetadataLoading = false };
                    if (result.Status == PhotoMetadataReadStatus.Failed)
                    {
                        Interlocked.Increment(ref _failures);
                    }
                }
                else
                {
                    Interlocked.Increment(ref _staleResults);
                }
            }

            if (publish)
            {
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
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
