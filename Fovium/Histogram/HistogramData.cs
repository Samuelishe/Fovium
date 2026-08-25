using System.Collections.ObjectModel;

namespace Fovium.Histogram;

internal sealed class HistogramData
{
    public const int BinCount = 256;

    private readonly long[] _red;
    private readonly long[] _green;
    private readonly long[] _blue;

    public HistogramData(
        IReadOnlyList<long> red,
        IReadOnlyList<long> green,
        IReadOnlyList<long> blue,
        long sourcePixelCount,
        long sampledLocationCount,
        long sampleCount,
        bool wasSampled)
    {
        _red = CopyBins(red, nameof(red));
        _green = CopyBins(green, nameof(green));
        _blue = CopyBins(blue, nameof(blue));
        if (sourcePixelCount < 0 || sampledLocationCount < 0 || sampleCount < 0 ||
            sampledLocationCount > sourcePixelCount || sampleCount > sampledLocationCount)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        }

        SourcePixelCount = sourcePixelCount;
        SampledLocationCount = sampledLocationCount;
        SampleCount = sampleCount;
        WasSampled = wasSampled;
        Red = Array.AsReadOnly(_red);
        Green = Array.AsReadOnly(_green);
        Blue = Array.AsReadOnly(_blue);
    }

    public ReadOnlyCollection<long> Red { get; }

    public ReadOnlyCollection<long> Green { get; }

    public ReadOnlyCollection<long> Blue { get; }

    public long SourcePixelCount { get; }

    public long SampledLocationCount { get; }

    public long SampleCount { get; }

    public bool WasSampled { get; }

    public long CommonMaximum => Math.Max(_red.Max(), Math.Max(_green.Max(), _blue.Max()));

    private static long[] CopyBins(IReadOnlyList<long> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        if (values.Count != BinCount)
        {
            throw new ArgumentException($"A histogram channel must contain {BinCount} bins.", parameterName);
        }

        var copy = new long[BinCount];
        for (var index = 0; index < copy.Length; index++)
        {
            if (values[index] < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            copy[index] = values[index];
        }

        return copy;
    }
}

internal enum HistogramReadStatus
{
    Success,
    UnsupportedPixelLayout,
    Failed,
}

internal sealed record HistogramReadResult(HistogramReadStatus Status, HistogramData? Data)
{
    public static HistogramReadResult Success(HistogramData data) =>
        new(HistogramReadStatus.Success, data ?? throw new ArgumentNullException(nameof(data)));

    public static HistogramReadResult Unsupported { get; } =
        new(HistogramReadStatus.UnsupportedPixelLayout, null);

    public static HistogramReadResult Failed { get; } = new(HistogramReadStatus.Failed, null);
}
