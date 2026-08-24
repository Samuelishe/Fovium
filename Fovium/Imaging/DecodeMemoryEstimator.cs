namespace Fovium.Imaging;

internal static class DecodeMemoryEstimator
{
    public const int BgraBytesPerPixel = 4;
    public const int ConservativePixelRepresentations = 2;

    public static long EstimateWorkingBytes(
        int width,
        int height,
        long encodedBytes = 0,
        int bytesPerPixel = BgraBytesPerPixel,
        int simultaneousRepresentations = ConservativePixelRepresentations)
    {
        Validate(width, height, encodedBytes, bytesPerPixel, simultaneousRepresentations);
        checked
        {
            return (long)width * height * bytesPerPixel * simultaneousRepresentations + encodedBytes;
        }
    }

    public static long EstimateRetainedBytes(
        int width,
        int height,
        long encodedBytes = 0,
        int bytesPerPixel = BgraBytesPerPixel)
    {
        Validate(width, height, encodedBytes, bytesPerPixel, 1);
        checked
        {
            return (long)width * height * bytesPerPixel + encodedBytes;
        }
    }

    private static void Validate(
        int width,
        int height,
        long encodedBytes,
        int bytesPerPixel,
        int simultaneousRepresentations)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegative(encodedBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesPerPixel);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(simultaneousRepresentations);
    }
}
