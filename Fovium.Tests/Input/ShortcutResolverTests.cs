using Fovium.Input;

namespace Fovium.Tests.Input;

public sealed class ShortcutResolverTests
{
    [Fact]
    public void DefaultGestureResolvesCorrectCommand()
    {
        var command = ShortcutResolver.Resolve(ShortcutSettings.Default, new ShortcutGesture("Right"));

        Assert.Equal(ViewerCommand.NextImage, command);
    }

    [Fact]
    public void ModifierCombinationResolvesCorrectCommand()
    {
        var command = ShortcutResolver.Resolve(
            ShortcutSettings.Default,
            new ShortcutGesture("O", ShortcutModifiers.Control));

        Assert.Equal(ViewerCommand.Open, command);
    }

    [Fact]
    public void UnassignedCommandDoesNotResolve()
    {
        var settings = ShortcutSettings.Default.WithBinding(ViewerCommand.NextImage, null);

        Assert.Null(ShortcutResolver.Resolve(settings, new ShortcutGesture("Right")));
    }

    [Theory]
    [InlineData("Escape")]
    [InlineData("LeftCtrl")]
    [InlineData("RightShift")]
    [InlineData("LeftAlt")]
    public void ReservedOrModifierOnlyKeyIsRejected(string key)
    {
        var before = ShortcutSettings.Default;

        var result = ShortcutResolver.Assign(
            before,
            ViewerCommand.ZoomIn,
            new ShortcutGesture(key),
            replaceConflict: true);

        Assert.Equal(ShortcutAssignmentStatus.Invalid, result.Status);
        Assert.Same(before, result.Settings);
    }

    [Fact]
    public void UnsupportedSerializedKeyIsRejectedRatherThanSavedAsBrokenBinding()
    {
        var result = ShortcutResolver.Assign(
            ShortcutSettings.Default,
            ViewerCommand.Fit,
            new ShortcutGesture("NotAnAvaloniaKey"),
            replaceConflict: false);

        Assert.Equal(ShortcutAssignmentStatus.Invalid, result.Status);
        Assert.Equal(new ShortcutGesture("0"), result.Settings.Get(ViewerCommand.Fit));
    }

    [Fact]
    public void ConflictCancelLeavesBindingsUntouched()
    {
        var before = ShortcutSettings.Default;

        var result = ShortcutResolver.Assign(
            before,
            ViewerCommand.ZoomIn,
            new ShortcutGesture("Right"),
            replaceConflict: false);

        Assert.Equal(ShortcutAssignmentStatus.Conflict, result.Status);
        Assert.Equal(ViewerCommand.NextImage, result.ConflictingCommand);
        Assert.Same(before, result.Settings);
        Assert.Equal(new ShortcutGesture("Plus"), before.Get(ViewerCommand.ZoomIn));
        Assert.Equal(new ShortcutGesture("Right"), before.Get(ViewerCommand.NextImage));
    }

    [Fact]
    public void ConflictReplacementClearsOldOwnerWithoutSwapping()
    {
        var before = ShortcutSettings.Default;

        var result = ShortcutResolver.Assign(
            before,
            ViewerCommand.ZoomIn,
            new ShortcutGesture("Right"),
            replaceConflict: true);

        Assert.Equal(ShortcutAssignmentStatus.Applied, result.Status);
        Assert.Equal(new ShortcutGesture("Right"), result.Settings.Get(ViewerCommand.ZoomIn));
        Assert.Null(result.Settings.Get(ViewerCommand.NextImage));
        Assert.DoesNotContain(
            ViewerCommands.Definitions
                .Select(definition => new
                {
                    definition.Scope,
                    Gesture = result.Settings.Get(definition.Command),
                })
                .Where(item => item.Gesture is not null)
                .GroupBy(item => (item.Scope, item.Gesture))
                .Select(group => group.Count()),
            count => count > 1);
    }

    [Fact]
    public void ResetRestoresEveryDefault()
    {
        var customized = ShortcutResolver.Assign(
            ShortcutSettings.Default,
            ViewerCommand.ToggleMatte,
            new ShortcutGesture("K"),
            replaceConflict: true).Settings;

        var reset = ShortcutSettings.Default;

        Assert.NotEqual(customized.Get(ViewerCommand.ToggleMatte), reset.Get(ViewerCommand.ToggleMatte));
        Assert.All(ViewerCommands.Definitions, definition =>
            Assert.Equal(
                ShortcutDefaults.CreateBindings()[definition.Id],
                reset.Get(definition.Command)));
    }

    [Fact]
    public void MarkupScopePrecedesHighlightAndGlobalForSharedGesture()
    {
        var settings = ShortcutSettings.Default.WithBinding(
            ViewerCommand.NextImage,
            new ShortcutGesture("OpenBracket"));
        var gesture = new ShortcutGesture("OpenBracket");

        Assert.Equal(
            ViewerCommand.DecreaseMarkupThickness,
            ShortcutResolver.Resolve(settings, gesture, new ViewerShortcutContext(true, true)));
        Assert.Equal(
            ViewerCommand.DecreaseHighlightRadius,
            ShortcutResolver.Resolve(settings, gesture, new ViewerShortcutContext(false, true)));
        Assert.Equal(
            ViewerCommand.NextImage,
            ShortcutResolver.Resolve(settings, gesture, ViewerShortcutContext.Global));
    }

    [Fact]
    public void CrossScopeDuplicateAssignmentIsAllowedWithoutClearingEitherBinding()
    {
        var gesture = new ShortcutGesture("OpenBracket");

        var result = ShortcutResolver.Assign(
            ShortcutSettings.Default,
            ViewerCommand.DecreaseHighlightRadius,
            gesture,
            replaceConflict: false);

        Assert.Equal(ShortcutAssignmentStatus.Applied, result.Status);
        Assert.Null(result.ConflictingCommand);
        Assert.Equal(gesture, result.Settings.Get(ViewerCommand.DecreaseHighlightRadius));
        Assert.Equal(gesture, result.Settings.Get(ViewerCommand.DecreaseMarkupThickness));
    }

    [Fact]
    public void SameScopeDuplicateStillRequiresConflictConfirmation()
    {
        var result = ShortcutResolver.Assign(
            ShortcutSettings.Default,
            ViewerCommand.SelectEraserTool,
            new ShortcutGesture("B"),
            replaceConflict: false);

        Assert.Equal(ShortcutAssignmentStatus.Conflict, result.Status);
        Assert.Equal(ViewerCommand.SelectBrushTool, result.ConflictingCommand);
        Assert.Equal(new ShortcutGesture("E"), result.Settings.Get(ViewerCommand.SelectEraserTool));
    }

    [Fact]
    public void NormalizationKeepsSameGestureAcrossDifferentScopes()
    {
        var normalized = ShortcutSettings.Default.Normalize();

        Assert.Equal(
            normalized.Get(ViewerCommand.DecreaseMarkupThickness),
            normalized.Get(ViewerCommand.DecreaseHighlightRadius));
        Assert.Equal(
            normalized.Get(ViewerCommand.IncreaseMarkupThickness),
            normalized.Get(ViewerCommand.IncreaseHighlightRadius));
    }
}
