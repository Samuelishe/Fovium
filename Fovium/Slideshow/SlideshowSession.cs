using System.Diagnostics;
using Fovium.Settings;
using Fovium.Viewer;

namespace Fovium.Slideshow;

internal readonly record struct SlideshowPresentedSlide(
    int SequenceIndex,
    long ImageIdentity,
    string PresentationIdentity);

internal enum SlideshowPreparationStatus
{
    Ready,
    NotRequired,
    RejectedByMemory,
    NoOtherViableImage,
    Unavailable,
    Stale,
}

internal readonly record struct SlideshowPreparationResult(
    SlideshowPreparationStatus Status,
    long RetainedManagedBytes = 0,
    TimeSpan PreparationDuration = default);

internal enum SlideshowAdvanceStatus
{
    PresentationPending,
    NoOtherViableImage,
    Canceled,
}

internal interface ISlideshowNavigator
{
    bool IsNavigationPending { get; }

    SlideshowPresentedSlide? PresentedSlide { get; }

    Task<SlideshowPreparationResult> PrepareNextAsync(
        SlideshowPresentedSlide expectedCurrent,
        SlideshowEndBehavior endBehavior,
        CancellationToken cancellationToken);

    Task<SlideshowAdvanceStatus> AdvanceAsync(
        SlideshowPresentedSlide expectedCurrent,
        SlideshowEndBehavior endBehavior,
        CancellationToken cancellationToken);

    void CancelAutomaticAdvance(SlideshowPresentedSlide presentedSlide);
}

internal interface ISlideshowTimerScheduler
{
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken);
}

internal sealed class MonotonicSlideshowTimerScheduler : ISlideshowTimerScheduler
{
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) =>
        Task.Delay(duration, cancellationToken);
}

internal readonly record struct SlideshowMetrics(
    bool Running,
    bool Quiescent,
    long Starts,
    long Stops,
    long NaturalStops,
    long Loops,
    long TimerExpirations,
    long ManualNavigationResets,
    long PresentedSlideCount,
    long PreparedNextHits,
    long PreparedNextMisses,
    long PreparedNextRejectedByMemory,
    long PreparedNextStale,
    TimeSpan LastPresentedDuration,
    TimeSpan LastTransitionWait,
    long LastPreparedManagedBytes);

internal sealed class SlideshowSession : IDisposable
{
    private readonly ISlideshowNavigator _navigator;
    private readonly PhotoPresentationViewSession _photoPresentationView;
    private readonly Func<SlideshowSettings> _settings;
    private readonly ISlideshowTimerScheduler _timerScheduler;
    private CancellationTokenSource? _cycleCancellation;
    private Task<SlideshowPreparationResult>? _preparationTask;
    private SlideshowPresentedSlide? _currentSlide;
    private long _cycleGeneration;
    private long _manualNavigationGeneration;
    private long _presentedTimestamp;
    private long _transitionStartedTimestamp;
    private bool _ownsPhotoPresentation;
    private bool _awaitingPresentation;
    private bool _automaticAdvancePending;
    private bool _suppressPhotoPresentationObservation;
    private bool _disposed;
    private bool _quiescent;
    private long _starts;
    private long _stops;
    private long _naturalStops;
    private long _loops;
    private long _timerExpirations;
    private long _manualNavigationResets;
    private long _presentedSlideCount;
    private long _preparedNextHits;
    private long _preparedNextMisses;
    private long _preparedNextRejectedByMemory;
    private long _preparedNextStale;
    private TimeSpan _lastPresentedDuration;
    private TimeSpan _lastTransitionWait;
    private long _lastPreparedManagedBytes;

    public SlideshowSession(
        ISlideshowNavigator navigator,
        PhotoPresentationViewSession photoPresentationView,
        Func<SlideshowSettings> settings,
        ISlideshowTimerScheduler? timerScheduler = null)
    {
        _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _photoPresentationView = photoPresentationView
            ?? throw new ArgumentNullException(nameof(photoPresentationView));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _timerScheduler = timerScheduler ?? new MonotonicSlideshowTimerScheduler();
        _photoPresentationView.Changed += OnPhotoPresentationViewChanged;
    }

    public event EventHandler? Changed;

    public bool IsRunning { get; private set; }

    public SlideshowMetrics Metrics => new(
        IsRunning,
        _quiescent,
        _starts,
        _stops,
        _naturalStops,
        _loops,
        _timerExpirations,
        _manualNavigationResets,
        _presentedSlideCount,
        _preparedNextHits,
        _preparedNextMisses,
        _preparedNextRejectedByMemory,
        _preparedNextStale,
        _lastPresentedDuration,
        _lastTransitionWait,
        _lastPreparedManagedBytes);

    public void Toggle()
    {
        if (IsRunning)
        {
            Stop();
        }
        else
        {
            Start();
        }
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        _quiescent = false;
        _awaitingPresentation = _navigator.IsNavigationPending;
        _automaticAdvancePending = false;
        _currentSlide = _navigator.PresentedSlide;
        _ownsPhotoPresentation = !_photoPresentationView.IsEnabled;
        if (_ownsPhotoPresentation)
        {
            SetPhotoPresentationEnabled(true);
        }

        _starts++;
        Changed?.Invoke(this, EventArgs.Empty);
        if (!_awaitingPresentation && _currentSlide is { } current)
        {
            BeginPresentedCycle(current);
        }
    }

    public void Stop() => StopCore(restorePhotoPresentation: true, natural: false);

    public void NotifyPresented(SlideshowPresentedSlide presented)
    {
        if (!IsRunning)
        {
            return;
        }

        if (_presentedTimestamp != 0)
        {
            _lastPresentedDuration = Stopwatch.GetElapsedTime(_presentedTimestamp);
        }

        if (_transitionStartedTimestamp != 0)
        {
            _lastTransitionWait = Stopwatch.GetElapsedTime(_transitionStartedTimestamp);
        }

        if (_currentSlide is { } previous &&
            _settings().Normalize().EndBehavior == SlideshowEndBehavior.Loop &&
            presented.SequenceIndex < previous.SequenceIndex)
        {
            _loops++;
        }

        _presentedSlideCount++;
        _awaitingPresentation = false;
        _automaticAdvancePending = false;
        _transitionStartedTimestamp = 0;
        BeginPresentedCycle(presented);
    }

    public long NotifyManualNavigationStarted()
    {
        if (!IsRunning)
        {
            return 0;
        }

        CancelCycle();
        if (_automaticAdvancePending && _navigator.PresentedSlide is { } presented)
        {
            _navigator.CancelAutomaticAdvance(presented);
        }

        _automaticAdvancePending = false;
        _awaitingPresentation = true;
        _quiescent = false;
        _manualNavigationResets++;
        return ++_manualNavigationGeneration;
    }

    public void NotifyManualNavigationCompletedWithoutPresentation(long navigationGeneration)
    {
        if (!IsRunning ||
            !_awaitingPresentation ||
            navigationGeneration == 0 ||
            navigationGeneration != _manualNavigationGeneration)
        {
            return;
        }

        _awaitingPresentation = false;
        if (_navigator.PresentedSlide is { } presented)
        {
            BeginPresentedCycle(presented);
        }
    }

    public void NotifyDurationChanged()
    {
        if (IsRunning && !_awaitingPresentation && _currentSlide is { } current)
        {
            BeginPresentedCycle(current);
        }
    }

    public void NotifyEndBehaviorChanged()
    {
        if (!IsRunning || _awaitingPresentation || _currentSlide is not { } current)
        {
            return;
        }

        _quiescent = false;
        RestartPreparation(current);
    }

    public void NotifyDestinationChanged()
    {
        if (!IsRunning || _awaitingPresentation || _currentSlide is not { } current)
        {
            return;
        }

        RestartPreparation(current);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopCore(restorePhotoPresentation: true, natural: false);
        _disposed = true;
        _photoPresentationView.Changed -= OnPhotoPresentationViewChanged;
    }

    private void BeginPresentedCycle(SlideshowPresentedSlide presented)
    {
        CancelCycle();
        _currentSlide = presented;
        _presentedTimestamp = Stopwatch.GetTimestamp();
        _quiescent = false;
        var cancellation = new CancellationTokenSource();
        _cycleCancellation = cancellation;
        var generation = ++_cycleGeneration;
        var settings = _settings().Normalize();
        _preparationTask = PrepareNextAsync(presented, settings.EndBehavior, cancellation.Token);
        _ = RunCycleAsync(
            presented,
            TimeSpan.FromSeconds(settings.SlideDurationSeconds),
            generation,
            cancellation.Token);
    }

    private void RestartPreparation(SlideshowPresentedSlide current)
    {
        var previousCancellation = _cycleCancellation;
        if (previousCancellation is null)
        {
            return;
        }

        previousCancellation.Cancel();
        previousCancellation.Dispose();
        var cancellation = new CancellationTokenSource();
        _cycleCancellation = cancellation;
        var generation = ++_cycleGeneration;
        var settings = _settings().Normalize();
        _preparationTask = PrepareNextAsync(current, settings.EndBehavior, cancellation.Token);
        var elapsed = Stopwatch.GetElapsedTime(_presentedTimestamp);
        var remaining = TimeSpan.FromSeconds(settings.SlideDurationSeconds) - elapsed;
        _ = RunCycleAsync(
            current,
            duration: remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
            generation,
            cancellation.Token);
    }

    private async Task<SlideshowPreparationResult> PrepareNextAsync(
        SlideshowPresentedSlide current,
        SlideshowEndBehavior endBehavior,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _navigator.PrepareNextAsync(
                current,
                endBehavior,
                cancellationToken);
            _lastPreparedManagedBytes = result.RetainedManagedBytes;
            if (result.Status == SlideshowPreparationStatus.RejectedByMemory)
            {
                _preparedNextRejectedByMemory++;
            }
            else if (result.Status == SlideshowPreparationStatus.Stale)
            {
                _preparedNextStale++;
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new SlideshowPreparationResult(SlideshowPreparationStatus.Stale);
        }
    }

    private async Task RunCycleAsync(
        SlideshowPresentedSlide current,
        TimeSpan duration,
        long generation,
        CancellationToken cancellationToken)
    {
        try
        {
            await _timerScheduler.DelayAsync(
                duration,
                cancellationToken);
            if (!IsCurrentCycle(current, generation))
            {
                return;
            }

            _timerExpirations++;
            var preparation = _preparationTask is null
                ? new SlideshowPreparationResult(SlideshowPreparationStatus.Unavailable)
                : await _preparationTask;
            if (!IsCurrentCycle(current, generation))
            {
                return;
            }

            if (preparation.Status is SlideshowPreparationStatus.Ready or
                SlideshowPreparationStatus.NotRequired)
            {
                _preparedNextHits++;
            }
            else
            {
                _preparedNextMisses++;
            }

            var endBehavior = _settings().Normalize().EndBehavior;
            if (preparation.Status == SlideshowPreparationStatus.NoOtherViableImage)
            {
                if (endBehavior == SlideshowEndBehavior.StopAtEnd)
                {
                    StopCore(restorePhotoPresentation: false, natural: true);
                }
                else
                {
                    _quiescent = true;
                    CancelCycle();
                    Changed?.Invoke(this, EventArgs.Empty);
                }

                return;
            }

            _transitionStartedTimestamp = Stopwatch.GetTimestamp();
            _automaticAdvancePending = true;
            _awaitingPresentation = true;
            var status = await _navigator.AdvanceAsync(current, endBehavior, cancellationToken);
            if (!IsCurrentCycle(current, generation))
            {
                return;
            }

            if (status == SlideshowAdvanceStatus.PresentationPending)
            {
                return;
            }

            _automaticAdvancePending = false;
            _awaitingPresentation = false;
            _transitionStartedTimestamp = 0;
            if (status == SlideshowAdvanceStatus.NoOtherViableImage)
            {
                if (endBehavior == SlideshowEndBehavior.StopAtEnd)
                {
                    StopCore(restorePhotoPresentation: false, natural: true);
                }
                else
                {
                    _quiescent = true;
                    CancelCycle();
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private bool IsCurrentCycle(SlideshowPresentedSlide current, long generation) =>
        IsRunning &&
        generation == _cycleGeneration &&
        _currentSlide == current;

    private void StopCore(bool restorePhotoPresentation, bool natural)
    {
        if (!IsRunning)
        {
            return;
        }

        CancelCycle();
        if (_automaticAdvancePending && _navigator.PresentedSlide is { } presented)
        {
            _navigator.CancelAutomaticAdvance(presented);
        }

        IsRunning = false;
        _quiescent = false;
        _awaitingPresentation = false;
        _automaticAdvancePending = false;
        _manualNavigationGeneration++;
        _currentSlide = null;
        _presentedTimestamp = 0;
        _transitionStartedTimestamp = 0;
        if (natural)
        {
            _naturalStops++;
            // Natural completion deliberately transfers the enabled presentation view to the
            // visible session state, avoiding an abrupt final-frame layout change.
            _ownsPhotoPresentation = false;
        }
        else
        {
            _stops++;
            if (restorePhotoPresentation && _ownsPhotoPresentation)
            {
                SetPhotoPresentationEnabled(false);
            }

            _ownsPhotoPresentation = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void CancelCycle()
    {
        _cycleGeneration++;
        var cancellation = _cycleCancellation;
        _cycleCancellation = null;
        _preparationTask = null;
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    private void SetPhotoPresentationEnabled(bool enabled)
    {
        _suppressPhotoPresentationObservation = true;
        try
        {
            _photoPresentationView.SetEnabled(enabled);
        }
        finally
        {
            _suppressPhotoPresentationObservation = false;
        }
    }

    private void OnPhotoPresentationViewChanged(object? sender, EventArgs e)
    {
        if (!_suppressPhotoPresentationObservation &&
            IsRunning &&
            !_photoPresentationView.IsEnabled)
        {
            StopCore(restorePhotoPresentation: false, natural: false);
        }
    }
}
