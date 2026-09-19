namespace Fovium.Settings;

internal enum SettingsSection
{
    General,
    Viewing,
    Color,
    Stage,
    Presentation,
    Controls,
    About,
}

internal static class SettingsSectionCatalog
{
    public static IReadOnlyList<SettingsSection> Ordered { get; } =
    [
        SettingsSection.General,
        SettingsSection.Viewing,
        SettingsSection.Color,
        SettingsSection.Stage,
        SettingsSection.Presentation,
        SettingsSection.Controls,
        SettingsSection.About,
    ];
}