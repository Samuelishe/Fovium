namespace Fovium.Loading;

internal sealed record AutomaticMemoryPolicy(
    long AvailableMemoryBytes,
    long CacheBudgetBytes,
    long ForegroundDecodeBudgetBytes,
    long SpeculativeDecodeBudgetBytes)
{
    public const long MinimumCacheBudgetBytes = 256L * 1024 * 1024;
    public const long MaximumCacheBudgetBytes = 1024L * 1024 * 1024;
    public const long MinimumForegroundDecodeBudgetBytes = 256L * 1024 * 1024;
    public const long MaximumForegroundDecodeBudgetBytes = 2L * 1024 * 1024 * 1024;

    public static AutomaticMemoryPolicy Detect()
    {
        var available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (available <= 0)
        {
            available = 1024L * 1024 * 1024;
        }

        return FromAvailableMemory(available);
    }

    public static AutomaticMemoryPolicy FromAvailableMemory(long availableMemoryBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(availableMemoryBytes);
        var cache = Math.Clamp(
            availableMemoryBytes / 8,
            MinimumCacheBudgetBytes,
            MaximumCacheBudgetBytes);
        var foreground = Math.Clamp(
            availableMemoryBytes / 4,
            MinimumForegroundDecodeBudgetBytes,
            MaximumForegroundDecodeBudgetBytes);

        // Only one speculative decode is planned at a time in R1. Its working
        // allowance is capped at half of the foreground allowance so the active
        // selection keeps priority without any machine benchmark.
        var speculative = Math.Min(cache, foreground / 2);
        return new AutomaticMemoryPolicy(availableMemoryBytes, cache, foreground, speculative);
    }
}
