using Avalonia;
using Avalonia.Controls;
using Fovium.Views;

namespace Fovium.Tests.Views;

public sealed class SettingsWindowDragOriginTests
{
    [Fact]
    public void FreeSurfaceAndNestedTextMayInitiateWindowDrag()
    {
        var nestedText = new TextBlock();
        var card = new Border { Child = nestedText };
        var content = new Grid { Children = { card } };
        var windowSurface = new Border { Child = content };

        Assert.True(SettingsWindowDragOrigin.MayInitiate(content, windowSurface));
        Assert.True(SettingsWindowDragOrigin.MayInitiate(card, windowSurface));
        Assert.True(SettingsWindowDragOrigin.MayInitiate(nestedText, windowSurface));
    }

    [Fact]
    public void InteractiveControlsAndTheirContentCannotInitiateWindowDrag()
    {
        var buttonLabel = new TextBlock();
        var button = new Button { Content = buttonLabel };
        var slider = new Slider();
        var list = new ListBox();
        var content = new StackPanel { Children = { button, slider, list } };
        var windowSurface = new Border { Child = content };

        Assert.False(SettingsWindowDragOrigin.MayInitiate(buttonLabel, windowSurface));
        Assert.False(SettingsWindowDragOrigin.MayInitiate(button, windowSurface));
        Assert.False(SettingsWindowDragOrigin.MayInitiate(slider, windowSurface));
        Assert.False(SettingsWindowDragOrigin.MayInitiate(list, windowSurface));
    }

    [Fact]
    public void VisualOutsideWindowCannotInitiateWindowDrag()
    {
        var windowSurface = new Border();

        Assert.False(SettingsWindowDragOrigin.MayInitiate(new TextBlock(), windowSurface));
    }

    [Fact]
    public void ResizeHandleCannotAlsoInitiateWindowMove()
    {
        var resizeHandle = new Border();
        resizeHandle.Classes.Add("resize-handle");
        var windowSurface = new Border { Child = resizeHandle };

        Assert.False(SettingsWindowDragOrigin.MayInitiate(resizeHandle, windowSurface));
    }
}