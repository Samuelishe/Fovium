using Fovium.ColorManagement;

namespace Fovium.Tests.ColorManagement;

public sealed class ActiveDisplayMonitorSelectorTests
{
    private static readonly DisplayMonitor Left = new(1, "left", new DesktopRect(0, 0, 1920, 1080));
    private static readonly DisplayMonitor Right = new(2, "right", new DesktopRect(1920, 0, 1920, 1080));

    [Fact]
    public void LargestPositiveIntersectionWins()
    {
        var selected = ActiveDisplayMonitorSelector.Select(
            new DesktopRect(1700, 100, 800, 600),
            [Left, Right]);

        Assert.Equal(Right, selected);
    }

    [Fact]
    public void ExactAreaTieRetainsCurrentMonitor()
    {
        var selected = ActiveDisplayMonitorSelector.Select(
            new DesktopRect(1720, 100, 400, 600),
            [Left, Right],
            Left.Handle);

        Assert.Equal(Left, selected);
    }

    [Fact]
    public void ExactAreaTieWithoutCurrentUsesStableIdentityRatherThanEnumerationOrder()
    {
        var selected = ActiveDisplayMonitorSelector.Select(
            new DesktopRect(1720, 100, 400, 600),
            [Right, Left]);

        Assert.Equal(Left, selected);
    }

    [Fact]
    public void OffscreenWindowHasNoNewMonitorSelection()
    {
        var selected = ActiveDisplayMonitorSelector.Select(
            new DesktopRect(-2000, -2000, 300, 300),
            [Left, Right],
            Right.Handle);

        Assert.Null(selected);
    }
}
