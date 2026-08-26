using Fovium.ColorManagementProbe;

namespace Fovium.Tests.ColorManagementProbe;

public sealed class ActiveMonitorSelectorTests
{
    private static readonly ProbeMonitor Left = new("left", new ProbeRect(-100, 0, 100, 100));
    private static readonly ProbeMonitor Middle = new("middle", new ProbeRect(0, 0, 100, 100));
    private static readonly ProbeMonitor Right = new("right", new ProbeRect(100, 0, 100, 100));

    [Fact]
    public void LargestPositiveIntersectionWinsAcrossNegativeDesktopCoordinates()
    {
        var selected = ActiveMonitorSelector.Select(
            new ProbeRect(-25, 10, 175, 80),
            [Right, Left, Middle]);

        Assert.Equal(Middle, selected);
        Assert.Equal(2_000, Left.Bounds.IntersectionArea(new ProbeRect(-25, 10, 175, 80)));
        Assert.Equal(8_000, Middle.Bounds.IntersectionArea(new ProbeRect(-25, 10, 175, 80)));
        Assert.Equal(4_000, Right.Bounds.IntersectionArea(new ProbeRect(-25, 10, 175, 80)));
    }

    [Fact]
    public void PositiveAreaTiePreservesCurrentMonitor()
    {
        var window = new ProbeRect(50, 0, 100, 100);

        var selected = ActiveMonitorSelector.Select(window, [Left, Middle, Right], Right.StableId);

        Assert.Equal(Right, selected);
        Assert.Equal(5_000, window.IntersectionArea(Middle.Bounds));
        Assert.Equal(5_000, window.IntersectionArea(Right.Bounds));
    }

    [Fact]
    public void CurrentMonitorWithSmallerPositiveAreaCannotOverrideLargestIntersection()
    {
        var window = new ProbeRect(-25, 10, 175, 80);

        var selected = ActiveMonitorSelector.Select(
            window,
            [Left, Middle, Right],
            Left.StableId);

        Assert.Equal(Middle, selected);
        Assert.True(window.IntersectionArea(Middle.Bounds) > window.IntersectionArea(Left.Bounds));
    }

    [Fact]
    public void PositiveAreaTieWithoutCurrentUsesOrdinalStableIdRegardlessOfEnumeration()
    {
        var window = new ProbeRect(50, 0, 100, 100);

        var forward = ActiveMonitorSelector.Select(window, [Middle, Right]);
        var reverse = ActiveMonitorSelector.Select(window, [Right, Middle], "not-a-candidate");

        Assert.Equal(Middle, forward);
        Assert.Equal(Middle, reverse);
    }

    [Fact]
    public void ZeroIntersectionPreservesCurrentMonitorEvenWhenItIsNotFirst()
    {
        var selected = ActiveMonitorSelector.Select(
            new ProbeRect(500, 500, 10, 10),
            [Right, Left, Middle],
            Middle.StableId);

        Assert.Equal(Middle, selected);
    }

    [Fact]
    public void ZeroIntersectionWithoutCurrentReturnsNull()
    {
        var selected = ActiveMonitorSelector.Select(
            new ProbeRect(500, 500, 10, 10),
            [Right, Left, Middle]);

        Assert.Null(selected);
    }

    [Fact]
    public void EdgeAndCornerContactHaveZeroArea()
    {
        Assert.Equal(0, Middle.Bounds.IntersectionArea(new ProbeRect(100, 20, 40, 40)));
        Assert.Equal(0, Middle.Bounds.IntersectionArea(new ProbeRect(100, 100, 40, 40)));
        Assert.Equal(1, Middle.Bounds.IntersectionArea(new ProbeRect(99, 99, 40, 40)));
    }

    [Fact]
    public void EmptyMonitorSetReturnsNull()
    {
        Assert.Null(ActiveMonitorSelector.Select(new ProbeRect(0, 0, 10, 10), []));
    }
}
