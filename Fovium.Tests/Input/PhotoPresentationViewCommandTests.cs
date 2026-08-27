using Fovium.Input;

namespace Fovium.Tests.Input;

public sealed class PhotoPresentationViewCommandTests
{
    [Fact]
    public void CommandHasStableIdDefaultF6AndGlobalPressAuthority()
    {
        var definition = ViewerCommands.GetDefinition(ViewerCommand.TogglePhotoPresentation);

        Assert.Equal("viewer.togglePhotoPresentation", definition.Id);
        Assert.Equal(ViewerCommand.TogglePhotoPresentation, definition.Command);
        Assert.Equal(ViewerCommandGroup.Viewing, definition.Group);
        Assert.Equal(ViewerCommandScope.Global, definition.Scope);
        Assert.Equal(ViewerCommandTrigger.Press, definition.Trigger);
        Assert.Equal(new ShortcutGesture("F6"), ShortcutSettings.Default.Get(definition.Command));
        Assert.True(ViewerCommands.TryGetById(definition.Id, out var roundTrip));
        Assert.Equal(definition.Command, roundTrip);
    }

    [Fact]
    public void ExistingGlobalF6WinsOverAdditivePresentationViewDefault()
    {
        var oldBindings = ShortcutDefaults.CreateBindings();
        oldBindings.Remove(ViewerCommands.GetId(ViewerCommand.TogglePhotoPresentation));
        oldBindings[ViewerCommands.GetId(ViewerCommand.Fit)] = new ShortcutGesture("F6");

        var normalized = new ShortcutSettings { Bindings = oldBindings }.Normalize();

        Assert.Equal(new ShortcutGesture("F6"), normalized.Get(ViewerCommand.Fit));
        Assert.Null(normalized.Get(ViewerCommand.TogglePhotoPresentation));
    }

    [Fact]
    public void ContextCheckedAuthorityTracksSessionState()
    {
        var viewport = new Fovium.Viewer.PhotoViewportControl();

        Assert.False(viewport.PhotoPresentationViewEnabled);
        viewport.SetPhotoPresentationViewEnabled(true);
        Assert.True(viewport.PhotoPresentationViewEnabled);
        viewport.SetPhotoPresentationViewEnabled(false);
        Assert.False(viewport.PhotoPresentationViewEnabled);
    }

    [Theory]
    [InlineData((int)ViewerCommand.ZoomIn)]
    [InlineData((int)ViewerCommand.ZoomOut)]
    [InlineData((int)ViewerCommand.Fit)]
    [InlineData((int)ViewerCommand.ActualSize)]
    [InlineData((int)ViewerCommand.Peek100)]
    [InlineData((int)ViewerCommand.SelectHandTool)]
    [InlineData((int)ViewerCommand.TemporaryMarkupHand)]
    public void ActiveModeSuppressesEveryGeometryMutatingCommand(int commandValue)
    {
        var command = (ViewerCommand)commandValue;

        Assert.False(PhotoPresentationInputPolicy.Allows(command, presentationEnabled: true));
        Assert.True(PhotoPresentationInputPolicy.Allows(command, presentationEnabled: false));
    }

    [Theory]
    [InlineData((int)ViewerCommand.PreviousImage)]
    [InlineData((int)ViewerCommand.NextImage)]
    [InlineData((int)ViewerCommand.TogglePhotoPresentation)]
    [InlineData((int)ViewerCommand.ToggleMatte)]
    [InlineData((int)ViewerCommand.Fullscreen)]
    [InlineData((int)ViewerCommand.ToggleColorPicker)]
    [InlineData((int)ViewerCommand.ToggleHistogram)]
    [InlineData((int)ViewerCommand.TogglePhotoInfo)]
    [InlineData((int)ViewerCommand.ToggleMarkupTools)]
    public void ActiveModeKeepsNavigationExitAndNonGeometryToolsAvailable(int commandValue)
    {
        Assert.True(PhotoPresentationInputPolicy.Allows(
            (ViewerCommand)commandValue,
            presentationEnabled: true));
    }

    [Fact]
    public void BlinkIsExplicitlySuppressedWhenIndependentComparisonLayoutIsNotRetained()
    {
        Assert.False(PhotoPresentationInputPolicy.Allows(
            ViewerCommand.BlinkCompare,
            presentationEnabled: true));
    }

    [Fact]
    public void BoundedFeatureDoesNotAddSlideshowCommands()
    {
        Assert.DoesNotContain(
            ViewerCommands.Definitions,
            definition => definition.Id.Contains("slideshow", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData((int)PhotoPresentationInteraction.WheelZoom)]
    [InlineData((int)PhotoPresentationInteraction.DoubleClickZoom)]
    [InlineData((int)PhotoPresentationInteraction.DragPan)]
    [InlineData((int)PhotoPresentationInteraction.HandPan)]
    [InlineData((int)PhotoPresentationInteraction.Peek)]
    [InlineData((int)PhotoPresentationInteraction.Blink)]
    public void ActiveModeSuppressesEveryDirectGeometryInteractionAndExitRestoresIt(
        int interactionValue)
    {
        var interaction = (PhotoPresentationInteraction)interactionValue;

        Assert.False(PhotoPresentationInputPolicy.Allows(interaction, presentationEnabled: true));
        Assert.True(PhotoPresentationInputPolicy.Allows(interaction, presentationEnabled: false));
    }

    [Theory]
    [InlineData((int)PhotoPresentationInteraction.MarkupDrawing)]
    [InlineData((int)PhotoPresentationInteraction.ColorSampling)]
    public void ActiveModeKeepsMarkupAndColorSamplingAvailable(int interactionValue)
    {
        Assert.True(PhotoPresentationInputPolicy.Allows(
            (PhotoPresentationInteraction)interactionValue,
            presentationEnabled: true));
    }
}
