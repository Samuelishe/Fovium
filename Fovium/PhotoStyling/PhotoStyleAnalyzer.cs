using System.Collections.Immutable;
using System.Diagnostics;
using Fovium.Imaging;
using Fovium.Rendering;
using Fovium.Stage;
using SkiaSharp;

namespace Fovium.PhotoStyling;

internal interface IPhotoStyleAnalyzer
{
    PhotoStyleAnalysis Analyze(DecodedImage image, CancellationToken cancellationToken);
}

internal sealed class PhotoStyleAnalyzer : IPhotoStyleAnalyzer
{
    private const int QuantizationLevels = 16;
    private const int QuantizationBinCount = QuantizationLevels * QuantizationLevels * QuantizationLevels;
    private const double BoundaryFraction = 0.15;

    public PhotoStyleAnalysis Analyze(DecodedImage image, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        var targetSize = BoundedImageSize.Calculate(
            image.Descriptor.OrientedSize,
            StageDefaults.PhotoStyleLongEdgePixels);
        using var colorSpace = SKColorSpace.CreateSrgb();
        var imageInfo = new SKImageInfo(
            targetSize.Width,
            targetSize.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul,
            colorSpace);
        using var bitmap = new SKBitmap(imageInfo);
        using (var canvas = new SKCanvas(bitmap))
        using (var source = image.AcquireRenderLease())
        {
            OrientedImageRenderer.Draw(
                canvas,
                source.Image,
                image.Descriptor.EncodedSize,
                image.Descriptor.Orientation,
                targetSize);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var accumulator = new AnalysisAccumulator(targetSize);
        var pixels = bitmap.GetPixelSpan();
        for (var y = 0; y < targetSize.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = pixels.Slice(y * bitmap.RowBytes, targetSize.Width * 4);
            for (var x = 0; x < targetSize.Width; x++)
            {
                var offset = x * 4;
                var alpha = row[offset + 3];
                if (alpha == 0)
                {
                    continue;
                }

                var red = Unpremultiply(row[offset + 2], alpha);
                var green = Unpremultiply(row[offset + 1], alpha);
                var blue = Unpremultiply(row[offset], alpha);
                accumulator.Add(x, y, red, green, blue, alpha);
            }
        }

        return accumulator.Create(stopwatch.Elapsed);
    }

    private static byte Unpremultiply(byte value, byte alpha) =>
        alpha == byte.MaxValue
            ? value
            : (byte)Math.Clamp(((value * byte.MaxValue) + (alpha / 2)) / alpha, 0, byte.MaxValue);

    private sealed class AnalysisAccumulator
    {
        private readonly PixelSize _size;
        private readonly double[] _binWeights = new double[QuantizationBinCount];
        private readonly double[] _binRed = new double[QuantizationBinCount];
        private readonly double[] _binGreen = new double[QuantizationBinCount];
        private readonly double[] _binBlue = new double[QuantizationBinCount];
        private readonly LinearAccumulator[] _field = new LinearAccumulator[
            StageDefaults.PhotoStyleFieldColumns * StageDefaults.PhotoStyleFieldRows];
        private LinearAccumulator _average;
        private LinearAccumulator _boundary;
        private int _visibleSamples;

        public AnalysisAccumulator(PixelSize size)
        {
            _size = size;
        }

        public void Add(int x, int y, byte red, byte green, byte blue, byte alpha)
        {
            var weight = alpha / 255d;
            _average.Add(red, green, blue, weight);
            if (IsBoundary(x, y))
            {
                _boundary.Add(red, green, blue, weight);
            }

            var fieldX = Math.Min(
                StageDefaults.PhotoStyleFieldColumns - 1,
                x * StageDefaults.PhotoStyleFieldColumns / _size.Width);
            var fieldY = Math.Min(
                StageDefaults.PhotoStyleFieldRows - 1,
                y * StageDefaults.PhotoStyleFieldRows / _size.Height);
            _field[(fieldY * StageDefaults.PhotoStyleFieldColumns) + fieldX]
                .Add(red, green, blue, weight);

            var bin = ((red >> 4) << 8) | ((green >> 4) << 4) | (blue >> 4);
            _binWeights[bin] += weight;
            _binRed[bin] += red * weight;
            _binGreen[bin] += green * weight;
            _binBlue[bin] += blue * weight;
            _visibleSamples++;
        }

        public PhotoStyleAnalysis Create(TimeSpan duration)
        {
            var average = _average.ToColor(StageDefaults.NeutralColor);
            var boundary = _boundary.ToColor(average);
            var rankedBins = Enumerable.Range(0, QuantizationBinCount)
                .Where(index => _binWeights[index] > 0)
                .OrderByDescending(index => _binWeights[index])
                .ThenBy(index => index)
                .Take(StageDefaults.PhotoStylePaletteSize)
                .ToArray();
            var totalWeight = _binWeights.Sum();
            var palette = rankedBins
                .Select(index => new PhotoPaletteEntry(
                    new StageColor(
                        ToByte(_binRed[index] / _binWeights[index]),
                        ToByte(_binGreen[index] / _binWeights[index]),
                        ToByte(_binBlue[index] / _binWeights[index])),
                    _binWeights[index] / totalWeight))
                .ToImmutableArray();
            if (palette.IsEmpty)
            {
                palette = [new PhotoPaletteEntry(average, 1)];
            }

            var field = _field
                .Select(cell => cell.ToColor(average))
                .ToImmutableArray();
            return new PhotoStyleAnalysis(
                average,
                palette[0].Color,
                boundary,
                palette,
                new PhotoColorField(
                    StageDefaults.PhotoStyleFieldColumns,
                    StageDefaults.PhotoStyleFieldRows,
                    field),
                _size,
                _visibleSamples,
                duration);
        }

        private bool IsBoundary(int x, int y)
        {
            var insetX = Math.Max(1, (int)Math.Ceiling(_size.Width * BoundaryFraction));
            var insetY = Math.Max(1, (int)Math.Ceiling(_size.Height * BoundaryFraction));
            return x < insetX || x >= _size.Width - insetX ||
                y < insetY || y >= _size.Height - insetY;
        }

        private static byte ToByte(double value) =>
            (byte)Math.Clamp((int)Math.Round(value), 0, byte.MaxValue);
    }

    private struct LinearAccumulator
    {
        private double _red;
        private double _green;
        private double _blue;
        private double _weight;

        public void Add(byte red, byte green, byte blue, double weight)
        {
            _red += ToLinear(red / 255d) * weight;
            _green += ToLinear(green / 255d) * weight;
            _blue += ToLinear(blue / 255d) * weight;
            _weight += weight;
        }

        public readonly StageColor ToColor(StageColor fallback) =>
            _weight <= 0
                ? fallback
                : new StageColor(
                    ToSrgbByte(_red / _weight),
                    ToSrgbByte(_green / _weight),
                    ToSrgbByte(_blue / _weight));

        private static double ToLinear(double channel) =>
            channel <= 0.04045
                ? channel / 12.92
                : Math.Pow((channel + 0.055) / 1.055, 2.4);

        private static byte ToSrgbByte(double channel)
        {
            var srgb = channel <= 0.0031308
                ? 12.92 * channel
                : (1.055 * Math.Pow(channel, 1d / 2.4)) - 0.055;
            return (byte)Math.Clamp((int)Math.Round(srgb * 255), 0, byte.MaxValue);
        }
    }
}
