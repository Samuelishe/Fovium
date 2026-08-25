using Fovium.Input;

namespace Fovium.Tests.Input;

public sealed class ViewerCommandDisplayTests
{
    [Fact]
    public void ToolTipUsesEffectiveAssignedGesture()
    {
        Assert.Equal(
            "Brush (Shift+B)",
            ViewerCommandDisplay.FormatToolTip(
                "Brush",
                new ShortcutGesture("B", ShortcutModifiers.Shift)));
    }

    [Fact]
    public void UnassignedToolTipKeepsSemanticName()
    {
        Assert.Equal("Brush", ViewerCommandDisplay.FormatToolTip("Brush", null));
    }
}
