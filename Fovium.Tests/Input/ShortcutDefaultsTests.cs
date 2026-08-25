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
        Assert.All(ViewerCommands.Definitions, definition =>
            Assert.Equal(ViewerCommandTrigger.Press, definition.Trigger));
    }
}
