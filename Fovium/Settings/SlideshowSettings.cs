namespace Fovium.Settings;

internal enum SlideshowEndBehavior
{
    StopAtEnd,
    Loop,
}

internal sealed record SlideshowSettings
{
    public const int DefaultSlideDurationSeconds = 5;
    public const int MinimumSlideDurationSeconds = 1;
    public const int MaximumSlideDurationSeconds = 60;

    public int SlideDurationSeconds { get; init; } = DefaultSlideDurationSeconds;

    public SlideshowEndBehavior EndBehavior { get; init; } = SlideshowEndBehavior.StopAtEnd;

    public static SlideshowSettings Default { get; } = new();

    public SlideshowSettings Normalize() => this with
    {
        SlideDurationSeconds = Math.Clamp(
            SlideDurationSeconds,
            MinimumSlideDurationSeconds,
            MaximumSlideDurationSeconds),
        EndBehavior = Enum.IsDefined(EndBehavior)
            ? EndBehavior
            : SlideshowEndBehavior.StopAtEnd,
    };
}
