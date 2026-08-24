using Fovium.Navigation;

namespace Fovium.Tests.Navigation;

public sealed class NaturalPathComparerTests
{
    [Fact]
    public void NumericRunsSortByNumericMagnitude()
    {
        string[] paths = ["IMG_100.jpg", "IMG_10.jpg", "IMG_2.jpg", "IMG_11.jpg", "IMG_1.jpg", "IMG_9.jpg"];

        Array.Sort(paths, NaturalPathComparer.Instance);

        Assert.Equal(
            ["IMG_1.jpg", "IMG_2.jpg", "IMG_9.jpg", "IMG_10.jpg", "IMG_11.jpg", "IMG_100.jpg"],
            paths);
    }

    [Fact]
    public void ArbitrarilyLargeNumericRunsDoNotOverflow()
    {
        string[] paths =
        [
            "IMG_999999999999999999999999999999.jpg",
            "IMG_10.jpg",
            "IMG_1000000000000000000000000000000.jpg",
        ];

        Array.Sort(paths, NaturalPathComparer.Instance);

        Assert.Equal("IMG_10.jpg", paths[0]);
        Assert.Equal("IMG_999999999999999999999999999999.jpg", paths[1]);
        Assert.Equal("IMG_1000000000000000000000000000000.jpg", paths[2]);
    }

    [Fact]
    public void EqualPrimaryNamesUseFinalOrdinalTieBreak()
    {
        string[] paths = ["img_1.jpg", "IMG_1.jpg"];

        Array.Sort(paths, NaturalPathComparer.Instance);

        Assert.Equal(["IMG_1.jpg", "img_1.jpg"], paths);
    }
}
