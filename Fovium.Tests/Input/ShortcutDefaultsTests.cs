using Fovium.Input;

namespace Fovium.Tests.Input;

public sealed class ShortcutDefaultsTests
{
    [Theory]
    [InlineData((int)ViewerCommand.PreviousImage, "Left", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.NextImage, "Right", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.ZoomIn, "Plus", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.ZoomOut, "Minus", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.Fit, "0", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.ActualSize, "1", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.ToggleMatte, "M", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.Fullscreen, "F11", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.Open, "O", (int)ShortcutModifiers.Control)]
    [InlineData((int)ViewerCommand.Settings, "Comma", (int)ShortcutModifiers.Control)]
    [InlineData((int)ViewerCommand.Peek100, "Z", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.BlinkCompare, "C", (int)ShortcutModifiers.Shift)]
    [InlineData((int)ViewerCommand.ToggleHighlight, "H", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.ToggleMarkupTools, "P", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.MarkupUndo, "Z", (int)ShortcutModifiers.Control)]
    [InlineData((int)ViewerCommand.MarkupRedo, "Y", (int)ShortcutModifiers.Control)]
    [InlineData((int)ViewerCommand.ClearMarkup, "C", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.DecreaseMarkupThickness, "OpenBracket", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.IncreaseMarkupThickness, "CloseBracket", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.DecreaseMarkupOpacity, "OpenBracket", (int)ShortcutModifiers.Control)]
    [InlineData((int)ViewerCommand.IncreaseMarkupOpacity, "CloseBracket", (int)ShortcutModifiers.Control)]
    public void ExactDefaultBindingsUseProjectOwnedGestures(
        int commandValue,
        string key,
        int modifierValue)
    {
        var command = (ViewerCommand)commandValue;

        Assert.Equal(
            new ShortcutGesture(key, (ShortcutModifiers)modifierValue),
            ShortcutSettings.Default.Get(command));
    }

    [Fact]
    public void CommandIdentifiersAreStableAndLocaleIndependent()
    {
        Assert.Equal("viewer.previous", ViewerCommands.GetId(ViewerCommand.PreviousImage));
        Assert.Equal("viewer.next", ViewerCommands.GetId(ViewerCommand.NextImage));
        Assert.Equal("viewer.zoomIn", ViewerCommands.GetId(ViewerCommand.ZoomIn));
        Assert.Equal("viewer.zoomOut", ViewerCommands.GetId(ViewerCommand.ZoomOut));
        Assert.Equal("viewer.fit", ViewerCommands.GetId(ViewerCommand.Fit));
        Assert.Equal("viewer.actualSize", ViewerCommands.GetId(ViewerCommand.ActualSize));
        Assert.Equal("viewer.toggleMatte", ViewerCommands.GetId(ViewerCommand.ToggleMatte));
        Assert.Equal("viewer.fullscreen", ViewerCommands.GetId(ViewerCommand.Fullscreen));
        Assert.Equal("viewer.open", ViewerCommands.GetId(ViewerCommand.Open));
        Assert.Equal("viewer.settings", ViewerCommands.GetId(ViewerCommand.Settings));
        Assert.Equal("viewer.peek100", ViewerCommands.GetId(ViewerCommand.Peek100));
        Assert.Equal("viewer.blinkCompare", ViewerCommands.GetId(ViewerCommand.BlinkCompare));
        Assert.Equal("viewer.toggleHighlight", ViewerCommands.GetId(ViewerCommand.ToggleHighlight));
        Assert.Equal("viewer.toggleMarkupTools", ViewerCommands.GetId(ViewerCommand.ToggleMarkupTools));
        Assert.Equal("viewer.markupUndo", ViewerCommands.GetId(ViewerCommand.MarkupUndo));
        Assert.Equal("viewer.markupRedo", ViewerCommands.GetId(ViewerCommand.MarkupRedo));
        Assert.Equal("viewer.clearMarkup", ViewerCommands.GetId(ViewerCommand.ClearMarkup));
        Assert.Equal(
            "viewer.markupThicknessDown",
            ViewerCommands.GetId(ViewerCommand.DecreaseMarkupThickness));
        Assert.Equal(
            "viewer.markupThicknessUp",
            ViewerCommands.GetId(ViewerCommand.IncreaseMarkupThickness));
        Assert.Equal(
            "viewer.markupOpacityDown",
            ViewerCommands.GetId(ViewerCommand.DecreaseMarkupOpacity));
        Assert.Equal(
            "viewer.markupOpacityUp",
            ViewerCommands.GetId(ViewerCommand.IncreaseMarkupOpacity));
        Assert.Equal(ViewerCommandTrigger.Hold, ViewerCommands.GetDefinition(ViewerCommand.Peek100).Trigger);
        Assert.Equal(ViewerCommandTrigger.Hold, ViewerCommands.GetDefinition(ViewerCommand.BlinkCompare).Trigger);
        Assert.All(
            ViewerCommands.Definitions.Where(definition =>
                definition.Command is not ViewerCommand.Peek100 and not ViewerCommand.BlinkCompare),
            definition => Assert.Equal(ViewerCommandTrigger.Press, definition.Trigger));
    }

    [Fact]
    public void RuntimeNormalizationDoesNotRewriteDeliberatelyAssignedPreviousPair()
    {
        var settings = ShortcutSettings.Default
            .WithBinding(ViewerCommand.BlinkCompare, ShortcutDefaults.PreviousBlinkCompare)
            .WithBinding(ViewerCommand.ClearMarkup, ShortcutDefaults.PreviousClearMarkup);

        var normalized = settings.Normalize();

        Assert.Equal(
            ShortcutDefaults.PreviousBlinkCompare,
            normalized.Get(ViewerCommand.BlinkCompare));
        Assert.Equal(
            ShortcutDefaults.PreviousClearMarkup,
            normalized.Get(ViewerCommand.ClearMarkup));
    }
}
