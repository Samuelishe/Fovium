using Fovium.Imaging;
using SkiaSharp;

namespace Fovium.Histogram;

internal interface IImageHistogramReader
{
    Task<HistogramReadResult> ReadAsync(DecodedImage image, CancellationToken cancellationToken);
}

internal sealed class SkiaDecodedHistogramReader : IImageHistogramReader
{
    public const int MaximumHistogramSamples = 2_000_000;

    private readonly SemaphoreSlim _worker = new(1, 1);
    private readonly int _maximumSamples;
    private readonly Action<int>? _rowVisited;

    public SkiaDecodedHistogramReader(
        int maximumSamples = MaximumHistogramSamples,
        Action<int>? rowVisited = null)
    {
        _maximumSamples = maximumSamples > 0
            ? maximumSamples
            : throw new ArgumentOutOfRangeException(nameof(maximumSamples));
        _rowVisited = rowVisited;
    }

    public async Task<HistogramReadResult> ReadAsync(
        DecodedImage image,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        await _worker.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => ReadCore(image, _maximumSamples, _rowVisited, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _worker.Release();
        }
    }

    private static HistogramReadResult ReadCore(
        DecodedImage image,
        int maximumSamples,
        Action<int>? rowVisited,
        CancellationToken cancellationToken)
    {
        try
        {
            using var pixels = image.AcquirePixelLease();
            if (pixels.ColorType != SKColorType.Bgra8888 ||
                pixels.AlphaType is not (SKAlphaType.Premul or SKAlphaType.Opaque) ||
                pixels.Width <= 0 || pixels.Height <= 0 || pixels.RowBytes < pixels.Width * 4)
            {
                return HistogramReadResult.Unsupported;
            }

            return HistogramReadResult.Success(
                CountPixels(pixels, maximumSamples, rowVisited, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException)
        {
            return HistogramReadResult.Failed;
        }
        catch (InvalidOperationException)
        {
            return HistogramReadResult.Failed;
        }
    }

    private static HistogramData CountPixels(
        DecodedImage.PixelLease pixels,
        int maximumSamples,
        Action<int>? rowVisited,
        CancellationToken cancellationToken)
    {
        var sourcePixelCount = checked((long)pixels.Width * pixels.Height);
        var sampling = HistogramSamplingPlan.Create(
            pixels.Width,
            pixels.Height,
            maximumSamples);
        var red = new long[HistogramData.BinCount];
        var green = new long[HistogramData.BinCount];
        var blue = new long[HistogramData.BinCount];
        long included = 0;
        var bytes = pixels.PixelBytes;

        for (var sampleY = 0; sampleY < sampling.Rows; sampleY++)
        {
            rowVisited?.Invoke(sampleY);
            cancellationToken.ThrowIfCancellationRequested();
            var y = sampling.MapY(sampleY);
            var rowOffset = checked(y * pixels.RowBytes);
            for (var sampleX = 0; sampleX < sampling.Columns; sampleX++)
            {
                var x = sampling.MapX(sampleX);
                var offset = checked(rowOffset + (x * 4));
                var alpha = bytes[offset + 3];
                if (alpha == 0)
                {
                    continue;
                }

                var sourceBlue = Unpremultiply(bytes[offset], alpha, pixels.AlphaType);
                var sourceGreen = Unpremultiply(bytes[offset + 1], alpha, pixels.AlphaType);
                var sourceRed = Unpremultiply(bytes[offset + 2], alpha, pixels.AlphaType);
                blue[sourceBlue]++;
                green[sourceGreen]++;
                red[sourceRed]++;
                included++;
            }
        }

        return new HistogramData(
            red,
            green,
            blue,
            sourcePixelCount,
            sampling.SampleLocationCount,
            included,
            sampling.IsSampled);
    }

    private static byte Unpremultiply(byte value, byte alpha, SKAlphaType alphaType)
    {
        if (alphaType == SKAlphaType.Opaque || alpha == byte.MaxValue)
        {
            return value;
        }

        return (byte)Math.Min(byte.MaxValue, ((value * byte.MaxValue) + (alpha / 2)) / alpha);
    }
}

internal readonly record struct HistogramSamplingPlan(
    int Width,
    int Height,
    int Columns,
    int Rows,
    bool IsSampled)
{
    public long SampleLocationCount => checked((long)Columns * Rows);

    public int MapX(int sampleIndex) => Map(sampleIndex, Columns, Width);

    public int MapY(int sampleIndex) => Map(sampleIndex, Rows, Height);

    public static HistogramSamplingPlan Create(int width, int height, int maximumSamples)
    {
        if (width <= 0 || height <= 0 || maximumSamples <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        var total = checked((long)width * height);
        if (total <= maximumSamples)
        {
            return new HistogramSamplingPlan(width, height, width, height, false);
        }

        var stride = Math.Max(2, (int)Math.Ceiling(Math.Sqrt(total / (double)maximumSamples)));
        int columns;
        int rows;
        do
        {
            columns = checked((width + stride - 1) / stride);
            rows = checked((height + stride - 1) / stride);
            if ((long)columns * rows <= maximumSamples)
            {
                break;
            }

            stride++;
        }
        while (true);

        return new HistogramSamplingPlan(width, height, columns, rows, true);
    }

    private static int Map(int sampleIndex, int sampleCount, int sourceLength)
    {
        if ((uint)sampleIndex >= (uint)sampleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleIndex));
        }

        return sampleCount == 1
            ? 0
            : (int)(((long)sampleIndex * (sourceLength - 1)) / (sampleCount - 1));
    }
}
