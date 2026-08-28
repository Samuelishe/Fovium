namespace Fovium.Slideshow;

internal static class SlideshowManagedPreloadPolicy
{
    public const long MaximumSpeculativeManagedBytes = 128L * 1024 * 1024;
    public const long MaximumCurrentAndNextManagedBytes = 256L * 1024 * 1024;

    public static bool IsAdmitted(long currentManagedBytes, long nextManagedBytes) =>
        currentManagedBytes >= 0 &&
        nextManagedBytes > 0 &&
        nextManagedBytes <= MaximumSpeculativeManagedBytes &&
        currentManagedBytes <= MaximumCurrentAndNextManagedBytes - nextManagedBytes;
}
