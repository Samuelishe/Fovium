using System.Globalization;
using Fovium.Localization;

namespace Fovium.Tests.Localization;

public sealed class HomeLocalizationTests
{
    [Theory]
    [InlineData("en-US", "Open photos", "Open files", "Open folder", "Recent")]
    [InlineData("ru-RU", "Открыть фотографии", "Открыть файлы", "Открыть папку", "Недавние")]
    public void HomeActionsAndRecentChromeAreLocalized(
        string cultureName,
        string title,
        string openFiles,
        string openFolder,
        string recent)
    {
        var localizer = Localizer.Create(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(title, localizer[UiStrings.HomeTitle]);
        Assert.Equal(openFiles, localizer[UiStrings.HomeOpenFiles]);
        Assert.Equal(openFolder, localizer[UiStrings.HomeOpenFolder]);
        Assert.Equal(recent, localizer[UiStrings.HomeRecent]);
        Assert.NotEqual(UiStrings.HomeDropHint, localizer[UiStrings.HomeDropHint]);
        Assert.NotEqual(UiStrings.SettingsShowRecentItems,
            localizer[UiStrings.SettingsShowRecentItems]);
        Assert.NotEqual(UiStrings.ErrorFolderEmpty, localizer[UiStrings.ErrorFolderEmpty]);
    }
}