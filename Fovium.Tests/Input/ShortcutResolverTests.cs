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
            ViewerCommands.Definitions.Select(definition => result.Settings.Get(definition.Command))
                .Where(gesture => gesture is not null)
                .GroupBy(gesture => gesture)
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
}
