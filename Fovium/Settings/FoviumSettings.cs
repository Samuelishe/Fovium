namespace Fovium.Settings;

internal enum ImageChangeViewPolicy
{
    KeepCurrentScale,
    FitEachImage,
}

internal sealed record FoviumSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public ImageChangeViewPolicy ImageChangeViewPolicy { get; init; } =
        ImageChangeViewPolicy.KeepCurrentScale;

    public static FoviumSettings Default { get; } = new();
}
