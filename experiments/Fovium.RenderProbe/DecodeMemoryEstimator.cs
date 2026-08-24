namespace Fovium.RenderProbe;

internal static class DecodeMemoryEstimator
{
    // The probe keeps one Avalonia and one Skia BGRA representation concurrently.
    public const long ProbeWorkingSetCapBytes = 512L * 1024 * 1024;

    public static long EstimateBytes(
        int width,
        int height,
        int bytesPerPixel = 4,
        int simultaneousRepresentations = 2)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (bytesPerPixel <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytesPerPixel));
        }

        if (simultaneousRepresentations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(simultaneousRepresentations));
        }

        checked
        {
            return (long)width * height * bytesPerPixel * simultaneousRepresentations;
        }
    }

    public static bool IsWithinProbeCap(int width, int height) =>
        EstimateBytes(width, height) <= ProbeWorkingSetCapBytes;
}
