using Fovium.Histogram;

namespace Fovium.Tests.Histogram;

public sealed class HistogramModelTests
{
    [Fact]
    public void PlotUsesOneSharedMaximumAndMapsEndpointBinsExactly()
    {
        var red = new long[256];
        var green = new long[256];
        var blue = new long[256];
        red[0] = 100;
        green[128] = 50;
        blue[255] = 25;
        var data = new HistogramData(red, green, blue, 175, 175, 175, false);

        var plot = HistogramPlotModel.Create(data);

        Assert.Equal(100, plot.CommonMaximum);
        Assert.Equal(new HistogramPlotPoint(0, 1), plot.Red[0]);
        Assert.Equal(50d / 100, plot.Green[128].Y);
        Assert.Equal(25d / 100, plot.Blue[255].Y);
        Assert.Equal(1, plot.Blue[255].X);
        Assert.All(plot.Red.Concat(plot.Green).Concat(plot.Blue), point =>
        {
            Assert.True(double.IsFinite(point.X));
            Assert.True(double.IsFinite(point.Y));
            Assert.InRange(point.X, 0, 1);
            Assert.InRange(point.Y, 0, 1);
        });
    }

    [Fact]
    public void AllZeroHistogramProducesFiniteZeroPlot()
    {
        var bins = new long[256];
        var plot = HistogramPlotModel.Create(new HistogramData(bins, bins, bins, 0, 0, 0, false));

        Assert.Equal(0, plot.CommonMaximum);
        Assert.All(plot.Red.Concat(plot.Green).Concat(plot.Blue), point => Assert.Equal(0, point.Y));
    }

    [Fact]
    public void HistogramCopiesInputBinsAndExposesReadOnlyChannels()
    {
        var red = new long[256];
        var green = new long[256];
        var blue = new long[256];
        red[10] = 3;
        var data = new HistogramData(red, green, blue, 3, 3, 3, false);

        red[10] = 99;

        Assert.Equal(3, data.Red[10]);
        Assert.Throws<NotSupportedException>(() => ((IList<long>)data.Red)[10] = 4);
    }

    [Fact]
    public void CacheIsBoundedAndEvictsLeastRecentlyUsedResult()
    {
        var bins = new long[256];
        var result = HistogramReadResult.Success(new HistogramData(bins, bins, bins, 0, 0, 0, false));
        var cache = new HistogramCache(2);
        cache.Add(1, result);
        cache.Add(2, result);
        Assert.True(cache.TryGet(1, out _));

        cache.Add(3, result);

        Assert.True(cache.TryGet(1, out _));
        Assert.False(cache.TryGet(2, out _));
        Assert.True(cache.TryGet(3, out _));
        Assert.Equal(2, cache.Count);
    }
}
