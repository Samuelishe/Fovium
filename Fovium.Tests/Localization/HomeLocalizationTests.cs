using System.Globalization;
using Fovium.Localization;

namespace Fovium.Tests.Localization;

public sealed class HomeLocalizationTests
{
    [Theory]
    [InlineData("en-US", "Open photos", "Choose images or a folder of photos.",
        "Or simply drop them here.", "Open files", "Open folder", "Recent")]
    [InlineData("ru-RU", "Открыть фотографии", "Выберите изображения или папку с фотографиями.",
        "Или просто перетащите их сюда.", "Открыть файлы", "Открыть папку", "Недавние")]
    public void HomeActionsAndRecentChromeAreLocalized(
        string cultureName,
        string title,
        string subtitle,
        string dropHint,
        string openFiles,
        string openFolder,
        string recent)
    {
        var localizer = Localizer.Create(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(title, localizer[UiStrings.HomeTitle]);
        Assert.Equal(subtitle, localizer[UiStrings.HomeSubtitle]);
        Assert.Equal(dropHint, localizer[UiStrings.HomeDropHint]);
        Assert.Equal(openFiles, localizer[UiStrings.HomeOpenFiles]);
        Assert.Equal(openFolder, localizer[UiStrings.HomeOpenFolder]);
        Assert.Equal(recent, localizer[UiStrings.HomeRecent]);
        Assert.NotEqual(UiStrings.HomeDropHint, localizer[UiStrings.HomeDropHint]);
        Assert.NotEqual(UiStrings.HomeShortcutAction, localizer[UiStrings.HomeShortcutAction]);
        Assert.NotEqual(UiStrings.HomeRecentUnavailable,
            localizer[UiStrings.HomeRecentUnavailable]);
        Assert.NotEqual(UiStrings.HomeRemoveRecent, localizer[UiStrings.HomeRemoveRecent]);
        Assert.NotEqual(UiStrings.CommandClosePhoto, localizer[UiStrings.CommandClosePhoto]);
        Assert.NotEqual(UiStrings.MenuExitFovium, localizer[UiStrings.MenuExitFovium]);
        Assert.NotEqual(UiStrings.SettingsRememberRecentPhotos,
            localizer[UiStrings.SettingsRememberRecentPhotos]);
        Assert.NotEqual(UiStrings.ErrorFolderEmpty, localizer[UiStrings.ErrorFolderEmpty]);
    }
}