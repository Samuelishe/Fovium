namespace Fovium.Settings;

using Fovium.Input;
using Fovium.Localization;
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

    public UiLanguage Language { get; init; } = UiLanguage.SystemDefault;

    public ImageChangeViewPolicy ImageChangeViewPolicy { get; init; } =
        ImageChangeViewPolicy.KeepCurrentScale;

    public bool MonitorColorManagementEnabled { get; init; } = true;

    public HomeSettings Home { get; init; } = HomeSettings.Default;

    public PhotoPresentationViewSettings PhotoPresentationView { get; init; } =
        PhotoPresentationViewSettings.Default;

    public SlideshowSettings Slideshow { get; init; } = SlideshowSettings.Default;

    public SettingsWindowSizeSettings SettingsWindowSize { get; init; } =
        SettingsWindowSizeSettings.Default;

    public StageSettings Stage { get; init; } = StageSettings.Default;

    public ShortcutSettings Shortcuts { get; init; } = ShortcutSettings.Default;

    public PresentationSettings Presentation { get; init; } = PresentationSettings.Default;

    public static FoviumSettings Default { get; } = new();

    public FoviumSettings Normalize() => this with
    {
        SchemaVersion = CurrentSchemaVersion,
        Language = Enum.IsDefined(Language) ? Language : UiLanguage.SystemDefault,
        ImageChangeViewPolicy = Enum.IsDefined(ImageChangeViewPolicy)
            ? ImageChangeViewPolicy
            : ImageChangeViewPolicy.KeepCurrentScale,
        Home = (Home ?? HomeSettings.Default).Normalize(),
        PhotoPresentationView = (PhotoPresentationView ?? PhotoPresentationViewSettings.Default).Normalize(),
        Slideshow = (Slideshow ?? SlideshowSettings.Default).Normalize(),
        SettingsWindowSize = (SettingsWindowSize ?? SettingsWindowSizeSettings.Default).Normalize(),
        Stage = (Stage ?? StageSettings.Default).Normalize(),
        Shortcuts = (Shortcuts ?? ShortcutSettings.Default).Normalize(),
        Presentation = (Presentation ?? PresentationSettings.Default).Normalize(),
    };
}