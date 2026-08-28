using Fovium.Imaging;

namespace Fovium.Rendering;

internal static class BoundedImageSize
{
    public static PixelSize Calculate(PixelSize orientedSize, int longEdgePixels)
    {
        if (!orientedSize.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(orientedSize));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(longEdgePixels);
        var scale = Math.Min(1d, (double)longEdgePixels / Math.Max(orientedSize.Width, orientedSize.Height));
        return new PixelSize(
            Math.Max(1, (int)Math.Round(orientedSize.Width * scale)),
            Math.Max(1, (int)Math.Round(orientedSize.Height * scale)));
    }
}
