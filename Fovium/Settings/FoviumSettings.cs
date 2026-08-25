namespace Fovium.Settings;

using Fovium.Input;
using Fovium.Presentation;
using Fovium.Stage;

internal enum ImageChangeViewPolicy
{
    KeepCurrentScale,
    FitEachImage,
}

internal sealed record FoviumSettings
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public ImageChangeViewPolicy ImageChangeViewPolicy { get; init; } =
        ImageChangeViewPolicy.KeepCurrentScale;

    public StageSettings Stage { get; init; } = StageSettings.Default;

    public ShortcutSettings Shortcuts { get; init; } = ShortcutSettings.Default;

    public PresentationSettings Presentation { get; init; } = PresentationSettings.Default;

    public static FoviumSettings Default { get; } = new();

    public FoviumSettings Normalize() => this with
    {
        SchemaVersion = CurrentSchemaVersion,
        ImageChangeViewPolicy = Enum.IsDefined(ImageChangeViewPolicy)
            ? ImageChangeViewPolicy
            : ImageChangeViewPolicy.KeepCurrentScale,
        Stage = (Stage ?? StageSettings.Default).Normalize(),
        Shortcuts = (Shortcuts ?? ShortcutSettings.Default).Normalize(),
        Presentation = (Presentation ?? PresentationSettings.Default).Normalize(),
    };
}
