using Fovium.Settings;
using Fovium.Slideshow;
using Fovium.Viewer;

namespace Fovium.Tests.Slideshow;

public sealed class SlideshowSessionTests
{
    [Fact]
    public async Task TimerRestartsForFullDurationOnlyAfterActualPublication()
    {
        var scheduler = new ControlledScheduler();
        var navigator = new ControlledNavigator(Slide(0, 1, "A"));
        var settings = SlideshowSettings.Default;
        var presentation = new PhotoPresentationViewSession();
        using var session = new SlideshowSession(
            navigator,
            presentation,
            () => settings,
            scheduler);

        session.Start();
        Assert.Equal(TimeSpan.FromSeconds(5), scheduler.Delays[0].Duration);
        scheduler.Complete(0);
        await navigator.AdvanceStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Single(scheduler.Delays);
        Assert.True(session.IsRunning);
        navigator.Presented = Slide(1, 2, "B");
        session.NotifyPresented(navigator.Presented.Value);

        Assert.Equal(2, scheduler.Delays.Count);
        Assert.Equal(TimeSpan.FromSeconds(5), scheduler.Delays[1].Duration);
        Assert.True(scheduler.Delays[0].Completed);
    }

    [Fact]
    public async Task ExpiredTimerWaitsForPreparedNextWithoutQueueingAnotherAdvance()
    {
        var scheduler = new ControlledScheduler();
        var preparation = new TaskCompletionSource<SlideshowPreparationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var navigator = new ControlledNavigator(Slide(0, 1, "A"))
        {
            Preparation = preparation.Task,
        };
        using var session = CreateSession(navigator, scheduler);

        session.Start();
        scheduler.Complete(0);
        await Task.Yield();

        Assert.Equal(0, navigator.AdvanceCount);
        Assert.Single(scheduler.Delays);
        preparation.SetResult(new SlideshowPreparationResult(SlideshowPreparationStatus.Ready));
        await navigator.AdvanceStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, navigator.AdvanceCount);
        Assert.Single(scheduler.Delays);
        Assert.Equal(1, session.Metrics.PreparedNextHits);
    }

    [Fact]
    public async Task RejectedSpeculativeNextStillAdvancesAndGetsFullTimeAfterPublication()
    {
        var scheduler = new ControlledScheduler();
        var navigator = new ControlledNavigator(Slide(0, 1, "A"))
        {
            Preparation = Task.FromResult(new SlideshowPreparationResult(
                SlideshowPreparationStatus.RejectedByMemory,
                200_000_000)),
        };
        using var session = CreateSession(navigator, scheduler);

        session.Start();
        scheduler.Complete(0);
        await navigator.AdvanceStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, navigator.AdvanceCount);
        Assert.Single(scheduler.Delays);
        Assert.Equal(1, session.Metrics.PreparedNextRejectedByMemory);
        Assert.Equal(1, session.Metrics.PreparedNextMisses);
        navigator.Presented = Slide(1, 2, "B");
        session.NotifyPresented(navigator.Presented.Value);

        Assert.Equal(2, scheduler.Delays.Count);
        Assert.Equal(TimeSpan.FromSeconds(5), scheduler.Delays[1].Duration);
    }

    [Fact]
    public void StartDuringPendingNavigationWaitsForAuthoritativePresentation()
    {
        var scheduler = new ControlledScheduler();
        var navigator = new ControlledNavigator(Slide(0, 1, "A"))
        {
            IsNavigationPending = true,
        };
        using var session = CreateSession(navigator, scheduler);

        session.Start();
        Assert.Empty(scheduler.Delays);

        navigator.IsNavigationPending = false;
        navigator.Presented = Slide(1, 2, "B");
        session.NotifyPresented(navigator.Presented.Value);

        Assert.Single(scheduler.Delays);
        Assert.Equal(TimeSpan.FromSeconds(5), scheduler.Delays[0].Duration);
    }

    [Fact]
    public void LiveDurationChangeRestartsCountdownFromCurrentPresentedImage()
    {
        var scheduler = new ControlledScheduler();
        var navigator = new ControlledNavigator(Slide(0, 1, "A"));
        var settings = SlideshowSettings.Default;
        var presentation = new PhotoPresentationViewSession();
        using var session = new SlideshowSession(
            navigator,
            presentation,
            () => settings,
            scheduler);

        session.Start();
        settings = settings with { SlideDurationSeconds = 3 };
        session.NotifyDurationChanged();

        Assert.Equal(2, scheduler.Delays.Count);
        Assert.True(scheduler.Delays[0].Canceled);
        Assert.Equal(TimeSpan.FromSeconds(3), scheduler.Delays[1].Duration);
    }

    [Fact]
    public void LiveEndBehaviorChangeKeepsCurrentDeadlineAndUsesNewBoundaryPolicy()
    {
        var scheduler = new ControlledScheduler();
        var navigator = new ControlledNavigator(Slide(0, 1, "A"));
        var settings = SlideshowSettings.Default;
        var presentation = new PhotoPresentationViewSession();
        using var session = new SlideshowSession(
            navigator,
            presentation,
            () => settings,
            scheduler);

        session.Start();
        settings = settings with { EndBehavior = SlideshowEndBehavior.Loop };
        session.NotifyEndBehaviorChanged();

        Assert.Equal(
            [SlideshowEndBehavior.StopAtEnd, SlideshowEndBehavior.Loop],
            navigator.PreparationEndBehaviors);
        Assert.True(scheduler.Delays[0].Canceled);
        Assert.InRange(
            scheduler.Delays[1].Duration,
            TimeSpan.FromSeconds(4.5),
            TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ManualNavigationDiscardsOldCycleAndRestartsFromPresentedTarget()
    {
        var scheduler = new ControlledScheduler();
        var navigator = new ControlledNavigator(Slide(0, 1, "A"));
        using var session = CreateSession(navigator, scheduler);

        session.Start();
        session.NotifyManualNavigationStarted();
        navigator.Presented = Slide(2, 3, "C");
        session.NotifyPresented(navigator.Presented.Value);

        Assert.True(scheduler.Delays[0].Canceled);
        Assert.Equal(TimeSpan.FromSeconds(5), scheduler.Delays[1].Duration);
        Assert.Equal(1, session.Metrics.ManualNavigationResets);
    }

    [Fact]
    public async Task PreparedOldTargetCannotAdvanceAfterManualNavigationSelectsNewSlide()
    {
        var scheduler = new ControlledScheduler();
        var oldPreparation = new TaskCompletionSource<SlideshowPreparationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var navigator = new ControlledNavigator(Slide(0, 1, "A"))
        {
            Preparation = oldPreparation.Task,
            PreparationHonorsCancellation = false,
        };
        using var session = CreateSession(navigator, scheduler);

        session.Start();
        session.NotifyManualNavigationStarted();
        navigator.Preparation = Task.FromResult(
            new SlideshowPreparationResult(SlideshowPreparationStatus.Ready));
        navigator.Presented = Slide(2, 3, "C");
        session.NotifyPresented(navigator.Presented.Value);
        scheduler.Complete(0);
        oldPreparation.TrySetResult(
            new SlideshowPreparationResult(SlideshowPreparationStatus.Ready));
        await Task.Yield();

        Assert.Equal(0, navigator.AdvanceCount);
        Assert.True(scheduler.Delays[0].Canceled);
        Assert.Equal(TimeSpan.FromSeconds(5), scheduler.Delays[1].Duration);
    }

    [Fact]
    public void RapidManualNavigationLetsOnlyLatestPresentedTargetRestartTimer()
    {
        var scheduler = new ControlledScheduler();
        var navigator = new ControlledNavigator(Slide(0, 1, "A"));
        using var session = CreateSession(navigator, scheduler);

        session.Start();
        var requestB = session.NotifyManualNavigationStarted();
        session.NotifyManualNavigationStarted();
        session.NotifyManualNavigationCompletedWithoutPresentation(requestB);

        Assert.Single(scheduler.Delays);
        navigator.Presented = Slide(3, 4, "D");
        session.NotifyPresented(navigator.Presented.Value);

        Assert.Equal(2, scheduler.Delays.Count);
        Assert.True(scheduler.Delays[0].Canceled);
        Assert.Equal(TimeSpan.FromSeconds(5), scheduler.Delays[1].Duration);
    }

    [Fact]
    public async Task ManualStopWinsTimerAdvanceRaceAndRestoresOwnedPresentation()
    {
        var scheduler = new ControlledScheduler();
        var advance = new TaskCompletionSource<SlideshowAdvanceStatus>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var navigator = new ControlledNavigator(Slide(0, 1, "A"))
        {
            Advance = advance.Task,
        };
        var presentation = new PhotoPresentationViewSession();
        using var session = new SlideshowSession(
            navigator,
            presentation,
            () => SlideshowSettings.Default,
            scheduler);

        session.Start();
        scheduler.Complete(0);
        await navigator.AdvanceStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        session.Stop();
        advance.SetResult(SlideshowAdvanceStatus.PresentationPending);
        await Task.Yield();

        Assert.False(session.IsRunning);
        Assert.False(presentation.IsEnabled);
        Assert.Equal(1, navigator.CancelCount);
        Assert.Single(scheduler.Delays);
    }

    [Fact]
    public async Task StopAtEndNaturallyStopsWithoutFinalLayoutSnap()
    {
        var scheduler = new ControlledScheduler();
        var navigator = new ControlledNavigator(Slide(2, 3, "C"))
        {
            Preparation = Task.FromResult(new SlideshowPreparationResult(
                SlideshowPreparationStatus.NoOtherViableImage)),
        };
        var presentation = new PhotoPresentationViewSession();
        using var session = new SlideshowSession(
            navigator,
            presentation,
            () => SlideshowSettings.Default,
            scheduler);

        session.Start();
        scheduler.Complete(0);
        await WaitUntilAsync(() => !session.IsRunning);

        Assert.True(presentation.IsEnabled);
        Assert.Equal(1, session.Metrics.NaturalStops);
        Assert.Equal(0, navigator.AdvanceCount);
    }

    [Fact]
    public async Task LoopWithSingleViableImageBecomesQuiescentWithoutChurn()
    {
        var scheduler = new ControlledScheduler();
        var navigator = new ControlledNavigator(Slide(0, 1, "Only"))
        {
            Preparation = Task.FromResult(new SlideshowPreparationResult(
                SlideshowPreparationStatus.NoOtherViableImage)),
        };
        var settings = SlideshowSettings.Default with { EndBehavior = SlideshowEndBehavior.Loop };
        var presentation = new PhotoPresentationViewSession();
        using var session = new SlideshowSession(
            navigator,
            presentation,
            () => settings,
            scheduler);

        session.Start();
        scheduler.Complete(0);
        await WaitUntilAsync(() => session.Metrics.Quiescent);

        Assert.True(session.IsRunning);
        Assert.True(presentation.IsEnabled);
        Assert.Equal(0, navigator.AdvanceCount);
        Assert.Single(scheduler.Delays);
    }

    [Fact]
    public async Task LoopPreservesNaturalOrderAndCountsOnlyActualWrapPublication()
    {
        var scheduler = new ControlledScheduler();
        var navigator = new ControlledNavigator(Slide(1, 2, "B"));
        var settings = SlideshowSettings.Default with { EndBehavior = SlideshowEndBehavior.Loop };
        var presentation = new PhotoPresentationViewSession();
        using var session = new SlideshowSession(
            navigator,
            presentation,
            () => settings,
            scheduler);

        session.Start();
        scheduler.Complete(0);
        await navigator.AdvanceStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        navigator.Presented = Slide(2, 3, "C");
        session.NotifyPresented(navigator.Presented.Value);
        scheduler.Complete(1);
        await WaitUntilAsync(() => navigator.AdvanceCount == 2);
        navigator.Presented = Slide(0, 1, "A");
        session.NotifyPresented(navigator.Presented.Value);
        scheduler.Complete(2);
        await WaitUntilAsync(() => navigator.AdvanceCount == 3);
        navigator.Presented = Slide(1, 2, "B");
        session.NotifyPresented(navigator.Presented.Value);

        Assert.Equal(3, navigator.AdvanceCount);
        Assert.Equal(1, session.Metrics.Loops);
        Assert.Equal(3, session.Metrics.PresentedSlideCount);
        Assert.Equal(TimeSpan.FromSeconds(5), scheduler.Delays[3].Duration);
    }

    [Fact]
    public void PresentationThatWasAlreadyEnabledRemainsEnabledOnManualStop()
    {
        var presentation = new PhotoPresentationViewSession();
        presentation.SetEnabled(true);
        using var session = new SlideshowSession(
            new ControlledNavigator(Slide(0, 1, "A")),
            presentation,
            () => SlideshowSettings.Default,
            new ControlledScheduler());

        session.Start();
        session.Stop();

        Assert.True(presentation.IsEnabled);
    }

    [Fact]
    public void TurningPresentationOffWhileRunningStopsSlideshowAndKeepsItOff()
    {
        var presentation = new PhotoPresentationViewSession();
        presentation.SetEnabled(true);
        using var session = new SlideshowSession(
            new ControlledNavigator(Slide(0, 1, "A")),
            presentation,
            () => SlideshowSettings.Default,
            new ControlledScheduler());

        session.Start();
        presentation.SetEnabled(false);

        Assert.False(session.IsRunning);
        Assert.False(presentation.IsEnabled);
    }

    private static SlideshowSession CreateSession(
        ControlledNavigator navigator,
        ControlledScheduler scheduler) => new(
        navigator,
        new PhotoPresentationViewSession(),
        () => SlideshowSettings.Default,
        scheduler);

    private static SlideshowPresentedSlide Slide(int index, long identity, string path) =>
        new(index, identity, path);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline);
            await Task.Yield();
        }
    }

    private sealed class ControlledNavigator(SlideshowPresentedSlide presented) : ISlideshowNavigator
    {
        public bool IsNavigationPending { get; set; }

        public SlideshowPresentedSlide? Presented { get; set; } = presented;

        public Task<SlideshowPreparationResult> Preparation { get; set; } =
            Task.FromResult(new SlideshowPreparationResult(SlideshowPreparationStatus.Ready));

        public Task<SlideshowAdvanceStatus> Advance { get; set; } =
            Task.FromResult(SlideshowAdvanceStatus.PresentationPending);

        public bool PreparationHonorsCancellation { get; set; } = true;

        public List<SlideshowEndBehavior> PreparationEndBehaviors { get; } = [];

        public TaskCompletionSource AdvanceStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int AdvanceCount { get; private set; }

        public int CancelCount { get; private set; }

        SlideshowPresentedSlide? ISlideshowNavigator.PresentedSlide => Presented;

        public Task<SlideshowPreparationResult> PrepareNextAsync(
            SlideshowPresentedSlide expectedCurrent,
            SlideshowEndBehavior endBehavior,
            CancellationToken cancellationToken)
        {
            PreparationEndBehaviors.Add(endBehavior);
            return PreparationHonorsCancellation
                ? Preparation.WaitAsync(cancellationToken)
                : Preparation;
        }

        public Task<SlideshowAdvanceStatus> AdvanceAsync(
            SlideshowPresentedSlide expectedCurrent,
            SlideshowEndBehavior endBehavior,
            CancellationToken cancellationToken)
        {
            AdvanceCount++;
            AdvanceStarted.TrySetResult();
            return Advance.WaitAsync(cancellationToken);
        }

        public void CancelAutomaticAdvance(SlideshowPresentedSlide presentedSlide) => CancelCount++;
    }

    private sealed class ControlledScheduler : ISlideshowTimerScheduler
    {
        public List<ControlledDelay> Delays { get; } = [];

        public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            var delay = new ControlledDelay(duration, cancellationToken);
            Delays.Add(delay);
            return delay.Task;
        }

        public void Complete(int index) => Delays[index].Complete();
    }

    private sealed class ControlledDelay
    {
        private readonly TaskCompletionSource _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public ControlledDelay(TimeSpan duration, CancellationToken cancellationToken)
        {
            Duration = duration;
            cancellationToken.Register(() =>
            {
                Canceled = true;
                _completion.TrySetCanceled(cancellationToken);
            });
        }

        public TimeSpan Duration { get; }

        public bool Canceled { get; private set; }

        public bool Completed { get; private set; }

        public Task Task => _completion.Task;

        public void Complete()
        {
            Completed = true;
            _completion.TrySetResult();
        }
    }
}
