using Fovium.Input;
using Fovium.Presentation;
using Fovium.Settings;
using Fovium.Stage;

namespace Fovium.Tests.Settings;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task ReapplyingEquivalentSettingsDoesNotRaiseEventOrAutosave()
    {
        var store = new RecordingSettingsStore();
        using var service = new SettingsService(store);
        var changeCount = 0;
        service.SettingsChanged += (_, _) => changeCount++;

        await service.SetStageAsync(StageSettings.Default);
        await service.SetShortcutsAsync(ShortcutSettings.Default);

        Assert.Equal(0, changeCount);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task PreferenceChangeTakesEffectAndPersists()
    {
        var store = new RecordingSettingsStore();
        using var service = new SettingsService(store);

        await service.SetImageChangeViewPolicyAsync(ImageChangeViewPolicy.FitEachImage);

        Assert.Equal(ImageChangeViewPolicy.FitEachImage, service.Current.ImageChangeViewPolicy);
        Assert.Equal(ImageChangeViewPolicy.FitEachImage, store.Saved?.ImageChangeViewPolicy);
    }

    [Fact]
    public async Task SaveFailureKeepsInMemoryPreferenceAndDiagnostic()
    {
        using var service = new SettingsService(new RecordingSettingsStore(failSave: true));

        await service.SetImageChangeViewPolicyAsync(ImageChangeViewPolicy.FitEachImage);

        Assert.Equal(ImageChangeViewPolicy.FitEachImage, service.Current.ImageChangeViewPolicy);
        Assert.Equal(SettingsDiagnosticKind.WriteFailed, service.LastDiagnostic?.Kind);
    }

    [Fact]
    public async Task FlushWaitsForPendingAutosave()
    {
        var store = new DelayedSettingsStore();
        using var service = new SettingsService(store);
        var change = service.SetImageChangeViewPolicyAsync(ImageChangeViewPolicy.FitEachImage);
        await store.SaveStarted.Task;

        var flush = service.FlushAsync();

        Assert.False(flush.IsCompleted);
        store.Complete();
        await Task.WhenAll(change, flush);
        Assert.Equal(ImageChangeViewPolicy.FitEachImage, store.Saved?.ImageChangeViewPolicy);
    }

    [Fact]
    public async Task RapidChangesCoalesceToOneLatestAutosave()
    {
        var store = new RecordingSettingsStore();
        using var service = new SettingsService(store);

        var first = service.SetStageAsync(StageSettings.Default with { AmbientBrightness = 0.5 });
        var second = service.SetStageAsync(StageSettings.Default with { AmbientBrightness = 0.6 });
        var third = service.SetStageAsync(StageSettings.Default with { AmbientBrightness = 0.7 });
        await Task.WhenAll(first, second, third);
        await service.FlushAsync();

        Assert.Equal(1, store.SaveCount);
        Assert.Equal(0.7, store.Saved?.Stage.AmbientBrightness);
    }

    [Fact]
    public async Task StageChangePersistsWithoutChangingViewPolicyAndPublishesSharedState()
    {
        var store = new RecordingSettingsStore();
        using var service = new SettingsService(store);
        FoviumSettings? published = null;
        service.SettingsChanged += (_, e) => published = e.Settings;
        var stage = StageSettings.Default with
        {
            BackgroundMode = StageBackgroundMode.Ambient,
            MatteEnabled = true,
            MatteColor = new StageColor(0x11, 0x22, 0x33),
            MatteStyle = MatteStyle.Soft,
            MatteWidthPhysicalPixels = 64,
        };

        await service.SetStageAsync(stage);

        Assert.Equal(stage, service.Current.Stage);
        Assert.Equal(ImageChangeViewPolicy.KeepCurrentScale, service.Current.ImageChangeViewPolicy);
        Assert.Same(service.Current, published);
        Assert.Equal(stage, store.Saved?.Stage);
        Assert.Equal(
            ShortcutSettings.Default.Get(ViewerCommand.ToggleMatte),
            service.Current.Shortcuts.Get(ViewerCommand.ToggleMatte));
    }

    [Fact]
    public async Task ToggleMatteChangesOnlyMatteEnabled()
    {
        var store = new RecordingSettingsStore();
        using var service = new SettingsService(store);
        var before = service.Current.Stage;

        await service.ToggleMatteAsync();

        Assert.True(service.Current.Stage.MatteEnabled);
        Assert.Equal(before.BackgroundMode, service.Current.Stage.BackgroundMode);
        Assert.Equal(before.CustomBackgroundColor, service.Current.Stage.CustomBackgroundColor);
        Assert.Equal(before.MatteColor, service.Current.Stage.MatteColor);
        Assert.Equal(before.MatteStyle, service.Current.Stage.MatteStyle);
        Assert.Equal(before.MatteWidthPhysicalPixels, service.Current.Stage.MatteWidthPhysicalPixels);
        Assert.Equal(before.AmbientBrightness, service.Current.Stage.AmbientBrightness);
        Assert.Equal(before.AmbientSaturation, service.Current.Stage.AmbientSaturation);
        Assert.Equal(before.AmbientBlur, service.Current.Stage.AmbientBlur);
    }

    [Fact]
    public async Task ShortcutChangePersistsStableBindingState()
    {
        var store = new RecordingSettingsStore();
        using var service = new SettingsService(store);
        var shortcuts = ShortcutSettings.Default
            .WithBinding(ViewerCommand.ToggleMatte, new ShortcutGesture("K"));

        await service.SetShortcutsAsync(shortcuts);

        Assert.Equal(new ShortcutGesture("K"), service.Current.Shortcuts.Get(ViewerCommand.ToggleMatte));
        Assert.Equal(new ShortcutGesture("K"), store.Saved?.Shortcuts.Get(ViewerCommand.ToggleMatte));
    }

    [Fact]
    public async Task ResetShortcutsRestoresR5F2Defaults()
    {
        var store = new RecordingSettingsStore();
        using var service = new SettingsService(store);
        await service.SetShortcutsAsync(ShortcutSettings.Default
            .WithBinding(ViewerCommand.MarkupUndo, null)
            .WithBinding(ViewerCommand.MarkupRedo, null)
            .WithBinding(ViewerCommand.ClearMarkup, null)
            .WithBinding(ViewerCommand.DecreaseMarkupThickness, null)
            .WithBinding(ViewerCommand.IncreaseMarkupThickness, null)
            .WithBinding(ViewerCommand.DecreaseMarkupOpacity, null)
            .WithBinding(ViewerCommand.IncreaseMarkupOpacity, null));

        await service.ResetShortcutsAsync();

        Assert.Equal(
            new ShortcutGesture("Z", ShortcutModifiers.Control),
            service.Current.Shortcuts.Get(ViewerCommand.MarkupUndo));
        Assert.Equal(
            new ShortcutGesture("Y", ShortcutModifiers.Control),
            service.Current.Shortcuts.Get(ViewerCommand.MarkupRedo));
        Assert.Equal(
            new ShortcutGesture("C"),
            service.Current.Shortcuts.Get(ViewerCommand.ClearMarkup));
        Assert.Equal(
            new ShortcutGesture("C", ShortcutModifiers.Shift),
            service.Current.Shortcuts.Get(ViewerCommand.BlinkCompare));
        Assert.Equal(
            new ShortcutGesture("OpenBracket"),
            service.Current.Shortcuts.Get(ViewerCommand.DecreaseMarkupThickness));
        Assert.Equal(
            new ShortcutGesture("CloseBracket"),
            service.Current.Shortcuts.Get(ViewerCommand.IncreaseMarkupThickness));
        Assert.Equal(
            new ShortcutGesture("OpenBracket", ShortcutModifiers.Control),
            service.Current.Shortcuts.Get(ViewerCommand.DecreaseMarkupOpacity));
        Assert.Equal(
            new ShortcutGesture("CloseBracket", ShortcutModifiers.Control),
            service.Current.Shortcuts.Get(ViewerCommand.IncreaseMarkupOpacity));
        Assert.Equal(
            new ShortcutGesture("OpenBracket"),
            service.Current.Shortcuts.Get(ViewerCommand.DecreaseHighlightRadius));
        Assert.Equal(
            new ShortcutGesture("CloseBracket"),
            service.Current.Shortcuts.Get(ViewerCommand.IncreaseHighlightRadius));
        Assert.Equal(new ShortcutGesture("V"), service.Current.Shortcuts.Get(ViewerCommand.SelectHandTool));
        Assert.Equal(new ShortcutGesture("B"), service.Current.Shortcuts.Get(ViewerCommand.SelectBrushTool));
        Assert.Equal(
            new ShortcutGesture("Space"),
            service.Current.Shortcuts.Get(ViewerCommand.TemporaryMarkupHand));
    }

    [Fact]
    public async Task PresentationSettingsRoundTripThroughSharedService()
    {
        var store = new RecordingSettingsStore();
        using var service = new SettingsService(store);
        var presentation = PresentationSettings.Default with
        {
            HighlightColor = new PresentationColor(10, 20, 30),
            HighlightOpacity = 0.62,
            HighlightRadiusPhysicalPixels = 77,
            DefaultMarkupColor = new PresentationColor(40, 50, 60),
            DefaultMarkupStrokePhysicalPixels = 7,
            DefaultMarkupOpacity = 0.35,
            MarkupDockPlacement = new FloatingOverlayPlacement(0.73, 0.21),
            PhotoInfoPlacement = new FloatingOverlayPlacement(0.14, 0.76),
            HistogramPlacement = new FloatingOverlayPlacement(0.86, 0.79),
        };

        await service.SetPresentationAsync(presentation);

        Assert.Equal(presentation, service.Current.Presentation);
        Assert.Equal(presentation, store.Saved?.Presentation);
        Assert.Equal(StageSettings.Default, service.Current.Stage);
    }

    private sealed class RecordingSettingsStore(bool failSave = false) : ISettingsStore
    {
        public FoviumSettings? Saved { get; private set; }

        public int SaveCount { get; private set; }

        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SettingsLoadResult(FoviumSettings.Default, null));

        public Task SaveAsync(FoviumSettings settings, CancellationToken cancellationToken)
        {
            if (failSave)
            {
                throw new IOException("Synthetic settings failure.");
            }

            SaveCount++;
            Saved = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class DelayedSettingsStore : ISettingsStore
    {
        private readonly TaskCompletionSource _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SaveStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public FoviumSettings? Saved { get; private set; }

        public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SettingsLoadResult(FoviumSettings.Default, null));

        public async Task SaveAsync(FoviumSettings settings, CancellationToken cancellationToken)
        {
            SaveStarted.TrySetResult();
            await _completion.Task.WaitAsync(cancellationToken);
            Saved = settings;
        }

        public void Complete() => _completion.TrySetResult();
    }
}
