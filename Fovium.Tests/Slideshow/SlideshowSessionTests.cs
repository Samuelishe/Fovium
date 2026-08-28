using Fovium.Loading;
using Fovium.Rendering;
using Fovium.Settings;
using Fovium.Slideshow;
using Fovium.Stage;
using Fovium.Tests.PhotoStyling;
using Fovium.Tests.Stage;
using Fovium.Viewer;

namespace Fovium.Tests.Slideshow;

public sealed class SlideshowSessionTests
{
    [Fact]
    public void ControllerBoundaryHasNoPhotoPresentationOrViewPolicyDependency()
    {
        var constructorParameterTypes = typeof(SlideshowSession)
            .GetConstructors(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic)
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.NotEmpty(constructorParameterTypes);
        Assert.DoesNotContain(typeof(PhotoPresentationViewSession), constructorParameterTypes);

        var advance = typeof(ISlideshowNavigator).GetMethod(nameof(ISlideshowNavigator.AdvanceAsync));
        Assert.NotNull(advance);
        Assert.Equal(
            [
                typeof(SlideshowPresentedSlide),
                typeof(SlideshowEndBehavior),
                typeof(CancellationToken),
            ],
            advance.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public async Task TimerRestartsForFullDurationOnlyAfterActualPublication()
    {
        var scheduler = new ControlledScheduler();
        var navigator = new ControlledNavigator(Slide(0, 1, "A"));
        var settings = SlideshowSettings.Default;
        using var session = new SlideshowSession(
            navigator,
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutomaticPublicationUsesNewPresentedImagesOwnDerivedStyle(
        bool photoPresentationEnabled)
    {
        var viewport = new PhotoViewportControl();
        viewport.SetPhotoPresentationViewEnabled(photoPresentationEnabled);
        var first = StageTestImages.CreateDecoded("A.png", new PixelSize(12, 8));
        var second = StageTestImages.CreateDecoded("B.png", new PixelSize(8, 12));
        var firstResource = new SharedResource<Fovium.Imaging.DecodedImage>(first);
        var secondResource = new SharedResource<Fovium.Imaging.DecodedImage>(second);
        Assert.True(first.TryAttachPhotoStyleAnalysis(PhotoDerivedStylePolicyTests.CreateAnalysis(
            new StageColor(210, 30, 30),
            new StageColor(210, 30, 30),
            new StageColor(210, 30, 30))));
        Assert.True(second.TryAttachPhotoStyleAnalysis(PhotoDerivedStylePolicyTests.CreateAnalysis(
            new StageColor(30, 30, 210),
            new StageColor(30, 30, 210),
            new StageColor(30, 30, 210))));
        var stage = StageSettings.Default with { BackgroundMode = StageBackgroundMode.ColorWash };
        var scheduler = new ControlledScheduler();
        var navigator = new ControlledNavigator(Slide(0, first.Identity, "A.png"));
        using var session = CreateSession(navigator, scheduler);
        try
        {
            using var firstPresentation = new StagePresentation(stage, first.Identity, null);
            viewport.SetPresentation(
                firstResource.Acquire(),
                ViewTransfer.Fit,
                "A.png",
                firstPresentation);
            session.Start();
            scheduler.Complete(0);
            await navigator.AdvanceStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            using var secondPresentation = new StagePresentation(stage, second.Identity, null);
            viewport.SetPresentation(
                secondResource.Acquire(),
                ViewTransfer.Fit,
                "B.png",
                secondPresentation);
            navigator.Presented = Slide(1, second.Identity, "B.png");
            session.NotifyPresented(navigator.Presented.Value);
            var state = viewport.CapturePhotoStylePresentationState();

            Assert.True(session.IsRunning);
            Assert.Equal(photoPresentationEnabled, viewport.PhotoPresentationViewEnabled);
            Assert.Equal(second.Identity, state.ImageIdentity);
            Assert.Equal(second.Identity, state.PhotoStyleIdentity);
            Assert.Equal(StageBackgroundMode.ColorWash, state.BackgroundMode);
            Assert.Equal(2, scheduler.Delays.Count);
        }
        finally
        {
            viewport.ClearImage();
            firstResource.ReleaseOwner();
            secondResource.ReleaseOwner();
        }
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
        using var session = new SlideshowSession(
            navigator,
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
        using var session = new SlideshowSession(
            navigator,
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
    public async Task ManualStopWinsTimerAdvanceRace()
    {
        var scheduler = new ControlledScheduler();
        var advance = new TaskCompletionSource<SlideshowAdvanceStatus>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var navigator = new ControlledNavigator(Slide(0, 1, "A"))
        {
            Advance = advance.Task,
        };
        using var session = new SlideshowSession(
            navigator,
            () => SlideshowSettings.Default,
            scheduler);

        session.Start();
        scheduler.Complete(0);
        await navigator.AdvanceStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        session.Stop();
        advance.SetResult(SlideshowAdvanceStatus.PresentationPending);
        await Task.Yield();

        Assert.False(session.IsRunning);
        Assert.Equal(1, navigator.CancelCount);
        Assert.Single(scheduler.Delays);
        Assert.Equal(1, session.Metrics.Stops);
    }

    [Fact]
    public async Task StopAtEndNaturallyStopsWithoutAdvancingPastFinalSlide()
    {
        var scheduler = new ControlledScheduler();
        var navigator = new ControlledNavigator(Slide(2, 3, "C"))
        {
            Preparation = Task.FromResult(new SlideshowPreparationResult(
                SlideshowPreparationStatus.NoOtherViableImage)),
        };
        using var session = new SlideshowSession(
            navigator,
            () => SlideshowSettings.Default,
            scheduler);

        session.Start();
        scheduler.Complete(0);
        await WaitUntilAsync(() => !session.IsRunning);

        Assert.Equal(1, session.Metrics.NaturalStops);
        Assert.Equal(0, navigator.AdvanceCount);
        Assert.Equal(Slide(2, 3, "C"), navigator.Presented);
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
        using var session = new SlideshowSession(
            navigator,
            () => settings,
            scheduler);

        session.Start();
        scheduler.Complete(0);
        await WaitUntilAsync(() => session.Metrics.Quiescent);

        Assert.True(session.IsRunning);
        Assert.Equal(0, navigator.AdvanceCount);
        Assert.Single(scheduler.Delays);
    }

    [Fact]
    public async Task LoopPreservesNaturalOrderAndCountsOnlyActualWrapPublication()
    {
        var scheduler = new ControlledScheduler();
        var navigator = new ControlledNavigator(Slide(1, 2, "B"));
        var settings = SlideshowSettings.Default with { EndBehavior = SlideshowEndBehavior.Loop };
        using var session = new SlideshowSession(
            navigator,
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SlideshowAndPhotoPresentationSupportEveryIndependentStateCombination(
        bool slideshowRunning,
        bool presentationEnabled)
    {
        var presentation = new PhotoPresentationViewSession();
        presentation.SetEnabled(presentationEnabled);
        var scheduler = new ControlledScheduler();
        using var session = new SlideshowSession(
            new ControlledNavigator(Slide(0, 1, "A")),
            () => SlideshowSettings.Default,
            scheduler);

        if (slideshowRunning)
        {
            session.Start();
        }

        Assert.Equal(slideshowRunning, session.IsRunning);
        Assert.Equal(presentationEnabled, presentation.IsEnabled);
        Assert.Equal(slideshowRunning ? 1 : 0, scheduler.Delays.Count);
    }

    [Fact]
    public void ExternalPhotoPresentationToggleWhileRunningDoesNotStopOrRestartTimer()
    {
        var presentation = new PhotoPresentationViewSession();
        presentation.SetEnabled(true);
        var scheduler = new ControlledScheduler();
        var navigator = new ControlledNavigator(Slide(0, 1, "A"));
        using var session = new SlideshowSession(
            navigator,
            () => SlideshowSettings.Default,
            scheduler);

        session.Start();
        var originalDelay = Assert.Single(scheduler.Delays);
        presentation.Toggle(); // The same Photo Presentation authority used by F6.
        presentation.Toggle();

        Assert.True(session.IsRunning);
        Assert.True(presentation.IsEnabled);
        Assert.Same(originalDelay, Assert.Single(scheduler.Delays));
        Assert.False(originalDelay.Canceled);
        Assert.Single(navigator.PreparationEndBehaviors);
        Assert.Equal(0, session.Metrics.Stops);
        Assert.Equal(0, session.Metrics.ManualNavigationResets);
        Assert.Equal(0, session.Metrics.TimerExpirations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SlideshowStartAndStopNeverMutatePhotoPresentationState(bool presentationEnabled)
    {
        var presentation = new PhotoPresentationViewSession();
        presentation.SetEnabled(presentationEnabled);
        var presentationChanges = 0;
        presentation.Changed += (_, _) => presentationChanges++;
        var scheduler = new ControlledScheduler();
        using var session = new SlideshowSession(
            new ControlledNavigator(Slide(0, 1, "A")),
            () => SlideshowSettings.Default,
            scheduler);

        session.Start();
        session.Stop();

        Assert.False(session.IsRunning);
        Assert.Equal(presentationEnabled, presentation.IsEnabled);
        Assert.Equal(0, presentationChanges);
        Assert.True(Assert.Single(scheduler.Delays).Canceled);
        Assert.Equal(1, session.Metrics.Starts);
        Assert.Equal(1, session.Metrics.Stops);
    }

    [Theory]
    [InlineData(false, (int)ImageChangeViewPolicy.KeepCurrentScale)]
    [InlineData(false, (int)ImageChangeViewPolicy.FitEachImage)]
    [InlineData(true, (int)ImageChangeViewPolicy.KeepCurrentScale)]
    [InlineData(true, (int)ImageChangeViewPolicy.FitEachImage)]
    public void NormalNavigationPolicyRemainsAuthoritativeAtSlideshowBoundary(
        bool slideshowRunning,
        int policyValue)
    {
        using var session = CreateSession(
            new ControlledNavigator(Slide(0, 1, "A")),
            new ControlledScheduler());
        if (slideshowRunning)
        {
            session.Start();
        }

        var policy = (ImageChangeViewPolicy)policyValue;
        var current = new ViewTransfer(
            ViewportMode.Manual,
            1.7,
            new NormalizedPoint(0.78, 0.24));
        var transfer = ImageChangeViewPolicyResolver.ForNavigation(policy, current);

        Assert.Equal(slideshowRunning, session.IsRunning);
        Assert.Equal(
            policy == ImageChangeViewPolicy.KeepCurrentScale ? current : ViewTransfer.Fit,
            transfer);
    }

    private static SlideshowSession CreateSession(
        ControlledNavigator navigator,
        ControlledScheduler scheduler) => new(
        navigator,
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
