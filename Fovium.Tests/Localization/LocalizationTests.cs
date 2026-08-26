using System.Globalization;
using Fovium.Localization;

namespace Fovium.Tests.Localization;

public sealed class LocalizationTests
{
    [Fact]
    public void EnglishCultureResolvesToEnglish()
    {
        Assert.Equal("en", LocaleResolver.Resolve(CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void RussianCultureResolvesToRussian()
    {
        Assert.Equal("ru", LocaleResolver.Resolve(CultureInfo.GetCultureInfo("ru-RU")));
    }

    [Fact]
    public void UnsupportedCultureFallsBackToEnglish()
    {
        Assert.Equal("en", LocaleResolver.Resolve(CultureInfo.GetCultureInfo("de-DE")));
    }

    [Fact]
    public void MissingRussianKeyFallsBackToEnglishValue()
    {
        var localizer = new Localizer(
            "ru",
            new Dictionary<string, string> { ["menu.open"] = "Open" },
            new Dictionary<string, string>());

        Assert.Equal("Open", localizer["menu.open"]);
    }

    [Fact]
    public void MissingEnglishKeyReturnsVisibleKey()
    {
        var localizer = new Localizer("en", new Dictionary<string, string>());

        Assert.Equal("missing.key", localizer["missing.key"]);
    }

    [Theory]
    [InlineData("en-US", "Open…")]
    [InlineData("ru-RU", "Открыть…")]
    public void EmbeddedCatalogsLoadForSupportedLocales(string cultureName, string expected)
    {
        var localizer = Localizer.Create(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(expected, localizer[UiStrings.MenuOpen]);
    }

    [Theory]
    [InlineData("en-US", "Open image", "Supported images")]
    [InlineData("ru-RU", "Открыть изображение", "Поддерживаемые изображения")]
    public void FilePickerUsesFormatAgnosticLocalizedLabels(
        string cultureName,
        string title,
        string imageType)
    {
        var localizer = Localizer.Create(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(title, localizer[UiStrings.PickerTitle]);
        Assert.Equal(imageType, localizer[UiStrings.PickerImageType]);
    }

    [Theory]
    [InlineData("en-US", "Scale when changing images", "Keep current scale", "Fit each image")]
    [InlineData("ru-RU", "Масштаб при смене изображения", "Сохранять текущий масштаб", "Вписывать каждое изображение")]
    public void ViewingSettingsCatalogsContainPolicyLabels(
        string cultureName,
        string heading,
        string keep,
        string fit)
    {
        var localizer = Localizer.Create(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(heading, localizer[UiStrings.SettingsScaleOnImageChange]);
        Assert.Equal(keep, localizer[UiStrings.SettingsKeepCurrentScale]);
        Assert.Equal(fit, localizer[UiStrings.SettingsFitEachImage]);
    }

    [Theory]
    [InlineData("en-US", "Stage", "Black", "Neutral", "Custom", "Ambient", "Matte")]
    [InlineData("ru-RU", "Фон", "Чёрный", "Нейтральный", "Свой цвет", "Ambient", "Паспарту")]
    public void StageCatalogsContainEveryBackgroundAndIndependentMatte(
        string cultureName,
        string section,
        string black,
        string neutral,
        string custom,
        string ambient,
        string matte)
    {
        var localizer = Localizer.Create(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(section, localizer[UiStrings.SettingsStage]);
        Assert.Equal(black, localizer[UiStrings.StageBlack]);
        Assert.Equal(neutral, localizer[UiStrings.StageNeutral]);
        Assert.Equal(custom, localizer[UiStrings.StageCustom]);
        Assert.Equal(ambient, localizer[UiStrings.StageAmbient]);
        Assert.Equal(matte, localizer[UiStrings.StageMatte]);
    }

    [Theory]
    [InlineData("en-US", "Style", "Size", "Solid", "Rounded", "Soft", "Angular")]
    [InlineData("ru-RU", "Стиль", "Размер", "Классическое", "Скруглённое", "Мягкое", "Угловое")]
    public void MatteCatalogsContainGeometryControlsAndEveryStyle(
        string cultureName,
        string styleLabel,
        string sizeLabel,
        string solid,
        string rounded,
        string soft,
        string angular)
    {
        var localizer = Localizer.Create(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(styleLabel, localizer[UiStrings.StageMatteStyle]);
        Assert.Equal(sizeLabel, localizer[UiStrings.StageMatteSize]);
        Assert.Equal(solid, localizer[UiStrings.StageMatteSolid]);
        Assert.Equal(rounded, localizer[UiStrings.StageMatteRounded]);
        Assert.Equal(soft, localizer[UiStrings.StageMatteSoft]);
        Assert.Equal(angular, localizer[UiStrings.StageMatteAngular]);
    }

    [Theory]
    [InlineData("en-US", "Controls", "Press a key…", "Unassigned", "Reset shortcuts")]
    [InlineData("ru-RU", "Управление", "Нажмите клавишу…", "Не назначено", "Сбросить сочетания")]
    public void ControlsCatalogsContainShortcutInteractionStrings(
        string cultureName,
        string controls,
        string pressKey,
        string unassigned,
        string reset)
    {
        var localizer = Localizer.Create(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(controls, localizer[UiStrings.SettingsControls]);
        Assert.Equal(pressKey, localizer[UiStrings.ShortcutPressKey]);
        Assert.Equal(unassigned, localizer[UiStrings.ShortcutUnassigned]);
        Assert.Equal(reset, localizer[UiStrings.ShortcutReset]);
        Assert.NotEqual(UiStrings.CommandToggleMatte, localizer[UiStrings.CommandToggleMatte]);
    }

    [Theory]
    [InlineData("en-US", "Peek 100% (hold)", "Blink Compare (hold)")]
    [InlineData("ru-RU", "Просмотр 100% (удерживать)", "Быстрое сравнение (удерживать)")]
    public void InspectionHoldCommandsHaveLocalizedControlsLabels(
        string cultureName,
        string peek,
        string blink)
    {
        var localizer = Localizer.Create(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(peek, localizer[UiStrings.CommandPeek100]);
        Assert.Equal(blink, localizer[UiStrings.CommandBlinkCompare]);
    }

    [Theory]
    [InlineData("en-US", "Presentation", "Cursor Highlight", "Markup Tools", "Brush", "Clear")]
    [InlineData("ru-RU", "Презентация", "Подсветка курсора", "Инструменты пометок", "Кисть", "Очистить")]
    public void PresentationCatalogContainsSettingsCommandsAndDockTools(
        string cultureName,
        string section,
        string highlight,
        string markup,
        string brush,
        string clear)
    {
        var localizer = Localizer.Create(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(section, localizer[UiStrings.SettingsPresentation]);
        Assert.Equal(highlight, localizer[UiStrings.CommandToggleHighlight]);
        Assert.Equal(markup, localizer[UiStrings.CommandToggleMarkupTools]);
        Assert.Equal(brush, localizer[UiStrings.PresentationBrush]);
        Assert.Equal(clear, localizer[UiStrings.PresentationClear]);
    }

    [Theory]
    [InlineData("en-US", "Photo Info", "Photo info", "Close Photo Info")]
    [InlineData("ru-RU", "Информация о фото", "Информация о фото", "Закрыть информацию о фото")]
    public void PhotoInfoCatalogContainsCommandAndPanelChrome(
        string cultureName,
        string command,
        string title,
        string close)
    {
        var localizer = Localizer.Create(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(command, localizer[UiStrings.CommandTogglePhotoInfo]);
        Assert.Equal(title, localizer[UiStrings.PhotoInfoTitle]);
        Assert.Equal(close, localizer[UiStrings.PhotoInfoClose]);
    }

    [Theory]
    [InlineData("en-US", "Histogram", "Histogram", "Close Histogram")]
    [InlineData("ru-RU", "Гистограмма", "Гистограмма", "Закрыть гистограмму")]
    public void HistogramCatalogContainsCommandAndPanelChrome(
        string cultureName,
        string command,
        string title,
        string close)
    {
        var localizer = Localizer.Create(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(command, localizer[UiStrings.CommandToggleHistogram]);
        Assert.Equal(title, localizer[UiStrings.HistogramTitle]);
        Assert.Equal(close, localizer[UiStrings.HistogramClose]);
    }

    [Theory]
    [InlineData(
        "en-US",
        "Color Picker",
        "Click a color in the photo",
        "Recent",
        "Transparent",
        "Approximate reference-sRGB color")]
    [InlineData(
        "ru-RU",
        "Пипетка",
        "Щёлкните цвет на фотографии",
        "Недавние",
        "Прозрачный",
        "Приблизительный цвет в эталонном sRGB")]
    public void ColorPickerCatalogContainsLocalizedChromeAndAccuracySemantics(
        string cultureName,
        string title,
        string empty,
        string recent,
        string transparent,
        string approximate)
    {
        var localizer = Localizer.Create(CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(title, localizer[UiStrings.CommandToggleColorPicker]);
        Assert.Equal(title, localizer[UiStrings.ColorPickerTitle]);
        Assert.Equal(empty, localizer[UiStrings.ColorPickerEmpty]);
        Assert.Equal(recent, localizer[UiStrings.ColorPickerRecent]);
        Assert.Equal(transparent, localizer[UiStrings.ColorPickerTransparent]);
        Assert.Equal(approximate, localizer[UiStrings.ColorPickerApproximate]);
        Assert.Contains("{0}", localizer[UiStrings.ColorPickerRgb], StringComparison.Ordinal);
        Assert.Contains("{3}", localizer[UiStrings.ColorPickerRgba], StringComparison.Ordinal);
    }
}
