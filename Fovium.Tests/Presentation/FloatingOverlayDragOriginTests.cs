using Avalonia;
using Avalonia.Controls;
using Fovium.Views;

namespace Fovium.Tests.Presentation;

public sealed class FloatingOverlayDragOriginTests
{
    [Fact]
    public void OrdinaryPanelContentAndNestedTextMayInitiateDrag()
    {
        var nestedText = new TextBlock();
        var value = new Border { Child = nestedText };
        var title = new TextBlock();
        var panelContent = new StackPanel { Children = { title, value } };
        var panel = new Border { Child = panelContent };
        IReadOnlySet<Visual> interactiveChildren = new HashSet<Visual>();

        Assert.True(FloatingOverlayDragOrigin.MayInitiate(panel, panel, interactiveChildren));
        Assert.True(FloatingOverlayDragOrigin.MayInitiate(title, panel, interactiveChildren));
        Assert.True(FloatingOverlayDragOrigin.MayInitiate(nestedText, panel, interactiveChildren));
    }

    [Fact]
    public void InteractiveControlsAndTheirDescendantsCannotInitiateDrag()
    {
        var icon = new TextBlock();
        var interactiveContainer = new Border { Child = icon };
        var closeButton = new Button();
        var slider = new Slider();
        var panelContent = new StackPanel { Children = { interactiveContainer, closeButton, slider } };
        var panel = new Border { Child = panelContent };
        var interactiveChildren = new HashSet<Visual> { interactiveContainer, closeButton, slider };

        Assert.False(FloatingOverlayDragOrigin.MayInitiate(icon, panel, interactiveChildren));
        Assert.False(FloatingOverlayDragOrigin.MayInitiate(closeButton, panel, interactiveChildren));
        Assert.False(FloatingOverlayDragOrigin.MayInitiate(slider, panel, interactiveChildren));
    }

    [Fact]
    public void VisualOutsidePanelCannotInitiateDrag()
    {
        var panel = new Border();

        Assert.False(FloatingOverlayDragOrigin.MayInitiate(
            new TextBlock(),
            panel,
            new HashSet<Visual>()));
    }
}
