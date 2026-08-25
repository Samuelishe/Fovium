namespace Fovium.Histogram;

internal readonly record struct HistogramPlotPoint(double X, double Y);

internal sealed class HistogramPlotModel
{
    private HistogramPlotModel(
        IReadOnlyList<HistogramPlotPoint> red,
        IReadOnlyList<HistogramPlotPoint> green,
        IReadOnlyList<HistogramPlotPoint> blue,
        long commonMaximum)
    {
        Red = red;
        Green = green;
        Blue = blue;
        CommonMaximum = commonMaximum;
    }

    public IReadOnlyList<HistogramPlotPoint> Red { get; }
    public IReadOnlyList<HistogramPlotPoint> Green { get; }
    public IReadOnlyList<HistogramPlotPoint> Blue { get; }
    public long CommonMaximum { get; }

    public static HistogramPlotModel Create(HistogramData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var maximum = data.CommonMaximum;
        return new HistogramPlotModel(
            Normalize(data.Red, maximum),
            Normalize(data.Green, maximum),
            Normalize(data.Blue, maximum),
            maximum);
    }

    private static IReadOnlyList<HistogramPlotPoint> Normalize(IReadOnlyList<long> bins, long maximum)
    {
        var points = new HistogramPlotPoint[HistogramData.BinCount];
        for (var bin = 0; bin < points.Length; bin++)
        {
            points[bin] = new HistogramPlotPoint(
                bin / (double)(HistogramData.BinCount - 1),
                maximum == 0 ? 0 : bins[bin] / (double)maximum);
        }

        return Array.AsReadOnly(points);
    }
}
