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
    [InlineData((int)ViewerCommand.TogglePhotoInfo, "I", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.ToggleHistogram, "G", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.ToggleColorPicker, "K", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.MarkupUndo, "Z", (int)ShortcutModifiers.Control)]
    [InlineData((int)ViewerCommand.MarkupRedo, "Y", (int)ShortcutModifiers.Control)]
    [InlineData((int)ViewerCommand.ClearMarkup, "C", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.DecreaseMarkupThickness, "OpenBracket", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.IncreaseMarkupThickness, "CloseBracket", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.DecreaseMarkupOpacity, "OpenBracket", (int)ShortcutModifiers.Control)]
    [InlineData((int)ViewerCommand.IncreaseMarkupOpacity, "CloseBracket", (int)ShortcutModifiers.Control)]
    [InlineData((int)ViewerCommand.DecreaseHighlightRadius, "OpenBracket", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.IncreaseHighlightRadius, "CloseBracket", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.SelectHandTool, "V", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.SelectBrushTool, "B", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.SelectEraserTool, "E", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.SelectLineTool, "L", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.SelectRectangleTool, "R", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.SelectEllipseTool, "O", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.SelectArrowTool, "A", (int)ShortcutModifiers.None)]
    [InlineData((int)ViewerCommand.TemporaryMarkupHand, "Space", (int)ShortcutModifiers.None)]
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
        Assert.Equal("viewer.togglePhotoInfo", ViewerCommands.GetId(ViewerCommand.TogglePhotoInfo));
        Assert.Equal("viewer.toggleHistogram", ViewerCommands.GetId(ViewerCommand.ToggleHistogram));
        Assert.Equal("viewer.toggleColorPicker", ViewerCommands.GetId(ViewerCommand.ToggleColorPicker));
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
        Assert.Equal(
            "viewer.highlightRadiusDown",
            ViewerCommands.GetId(ViewerCommand.DecreaseHighlightRadius));
        Assert.Equal(
            "viewer.highlightRadiusUp",
            ViewerCommands.GetId(ViewerCommand.IncreaseHighlightRadius));
        Assert.Equal("viewer.markupTool.hand", ViewerCommands.GetId(ViewerCommand.SelectHandTool));
        Assert.Equal("viewer.markupTool.brush", ViewerCommands.GetId(ViewerCommand.SelectBrushTool));
        Assert.Equal("viewer.markupTool.eraser", ViewerCommands.GetId(ViewerCommand.SelectEraserTool));
        Assert.Equal("viewer.markupTool.line", ViewerCommands.GetId(ViewerCommand.SelectLineTool));
        Assert.Equal(
            "viewer.markupTool.rectangle",
            ViewerCommands.GetId(ViewerCommand.SelectRectangleTool));
        Assert.Equal("viewer.markupTool.ellipse", ViewerCommands.GetId(ViewerCommand.SelectEllipseTool));
        Assert.Equal("viewer.markupTool.arrow", ViewerCommands.GetId(ViewerCommand.SelectArrowTool));
        Assert.Equal(
            "viewer.markupTemporaryHand",
            ViewerCommands.GetId(ViewerCommand.TemporaryMarkupHand));
        Assert.Equal(ViewerCommandTrigger.Hold, ViewerCommands.GetDefinition(ViewerCommand.Peek100).Trigger);
        Assert.Equal(ViewerCommandTrigger.Hold, ViewerCommands.GetDefinition(ViewerCommand.BlinkCompare).Trigger);
        Assert.Equal(
            ViewerCommandTrigger.Hold,
            ViewerCommands.GetDefinition(ViewerCommand.TemporaryMarkupHand).Trigger);
        Assert.All(
            ViewerCommands.Definitions.Where(definition =>
                definition.Command is not ViewerCommand.Peek100 and
                    not ViewerCommand.BlinkCompare and
                    not ViewerCommand.TemporaryMarkupHand),
            definition => Assert.Equal(ViewerCommandTrigger.Press, definition.Trigger));
    }

    [Fact]
    public void EveryCommandHasExactlyOneTypedGroupAndScope()
    {
        Assert.Equal(Enum.GetValues<ViewerCommand>().Length, ViewerCommands.Definitions.Count);
        Assert.Equal(
            ViewerCommands.Definitions.Count,
            ViewerCommands.Definitions.Select(definition => definition.Command).Distinct().Count());
        Assert.All(ViewerCommands.Definitions, definition =>
        {
            Assert.True(Enum.IsDefined(definition.Group));
            Assert.True(Enum.IsDefined(definition.Scope));
            Assert.False(string.IsNullOrWhiteSpace(definition.Id));
        });

        Assert.Equal(
            ViewerCommandScope.Markup,
            ViewerCommands.GetDefinition(ViewerCommand.SelectBrushTool).Scope);
        Assert.Equal(
            ViewerCommandScope.Highlight,
            ViewerCommands.GetDefinition(ViewerCommand.IncreaseHighlightRadius).Scope);
        Assert.Equal(
            ViewerCommandGroup.Navigation,
            ViewerCommands.GetDefinition(ViewerCommand.NextImage).Group);
        Assert.Equal(
            ViewerCommandGroup.Presentation,
            ViewerCommands.GetDefinition(ViewerCommand.TogglePhotoInfo).Group);
        Assert.Equal(
            ViewerCommandScope.Global,
            ViewerCommands.GetDefinition(ViewerCommand.TogglePhotoInfo).Scope);
        Assert.Equal(
            ViewerCommandGroup.Presentation,
            ViewerCommands.GetDefinition(ViewerCommand.ToggleHistogram).Group);
        Assert.Equal(
            ViewerCommandScope.Global,
            ViewerCommands.GetDefinition(ViewerCommand.ToggleHistogram).Scope);
        Assert.Equal(
            ViewerCommandGroup.Inspection,
            ViewerCommands.GetDefinition(ViewerCommand.ToggleColorPicker).Group);
        Assert.Equal(
            ViewerCommandScope.Global,
            ViewerCommands.GetDefinition(ViewerCommand.ToggleColorPicker).Scope);
    }

    [Fact]
    public void ExistingGlobalIShortcutWinsOverAdditivePhotoInfoDefault()
    {
        var oldBindings = ShortcutDefaults.CreateBindings();
        oldBindings.Remove(ViewerCommands.GetId(ViewerCommand.TogglePhotoInfo));
        oldBindings[ViewerCommands.GetId(ViewerCommand.Fit)] = new ShortcutGesture("I");

        var normalized = new ShortcutSettings { Bindings = oldBindings }.Normalize();

        Assert.Equal(new ShortcutGesture("I"), normalized.Get(ViewerCommand.Fit));
        Assert.Null(normalized.Get(ViewerCommand.TogglePhotoInfo));
    }

    [Fact]
    public void ExistingGlobalGShortcutWinsOverAdditiveHistogramDefault()
    {
        var oldBindings = ShortcutDefaults.CreateBindings();
        oldBindings.Remove(ViewerCommands.GetId(ViewerCommand.ToggleHistogram));
        oldBindings[ViewerCommands.GetId(ViewerCommand.Fit)] = new ShortcutGesture("G");

        var normalized = new ShortcutSettings { Bindings = oldBindings }.Normalize();

        Assert.Equal(new ShortcutGesture("G"), normalized.Get(ViewerCommand.Fit));
        Assert.Null(normalized.Get(ViewerCommand.ToggleHistogram));
    }

    [Fact]
    public void ExistingGlobalKShortcutWinsOverAdditiveColorPickerDefault()
    {
        var oldBindings = ShortcutDefaults.CreateBindings();
        oldBindings.Remove(ViewerCommands.GetId(ViewerCommand.ToggleColorPicker));
        oldBindings[ViewerCommands.GetId(ViewerCommand.Fit)] = new ShortcutGesture("K");

        var normalized = new ShortcutSettings { Bindings = oldBindings }.Normalize();

        Assert.Equal(new ShortcutGesture("K"), normalized.Get(ViewerCommand.Fit));
        Assert.Null(normalized.Get(ViewerCommand.ToggleColorPicker));
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
