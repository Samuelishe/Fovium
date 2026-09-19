using Fovium.Settings;

namespace Fovium.Tests.Settings;

public sealed class SettingsSectionCatalogTests
{
    [Fact]
    public void OrderedSectionsExposeCompleteHumanFirstNavigation()
    {
        Assert.Equal(
            [
                SettingsSection.General,
                SettingsSection.Viewing,
                SettingsSection.Color,
                SettingsSection.Stage,
                SettingsSection.Presentation,
                SettingsSection.Controls,
                SettingsSection.About,
            ],
            SettingsSectionCatalog.Ordered);
        Assert.Equal(
            SettingsSectionCatalog.Ordered.Count,
            SettingsSectionCatalog.Ordered.Distinct().Count());
    }
}