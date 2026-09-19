using System.Xml.Linq;

namespace Fovium.Tests.Views;

public sealed class SettingsEcosystemStructureTests
{
    [Fact]
    public void ColorPickerUsesProjectChromeCircularWheelValueStripAndExactRepresentations()
    {
        var document = XDocument.Load(RepositoryPath("Fovium", "Views", "ColorPickerWindow.axaml"));
        var controls = XDocument.Load(RepositoryPath("Fovium", "Themes", "FoviumControls.axaml"));
        var source = File.ReadAllText(RepositoryPath("Fovium", "Views", "ColorPickerWindow.axaml.cs"));
        var window = document.Root!;

        Assert.Contains("fovium-secondary-window", Attribute(window, "Classes"));
        Assert.Single(window.Descendants(), element => Name(element) == "ColorWheel");
        Assert.Single(window.Descendants(), element => Name(element) == "ValueStrip");
        Assert.Single(window.Descendants(), element => Name(element) == "RgbPanel");
        Assert.Single(window.Descendants(), element => Name(element) == "HsvPanel");
        Assert.Single(window.Descendants(), element => Name(element) == "HexPanel");
        Assert.Equal(3, window.Descendants().Count(element =>
            Attribute(element, "Classes").Split(' ').Contains("segmented")));
        Assert.Single(ElementsNamed(controls, "Style"),
            style => Attribute(style, "Selector") == "Button.segmented.selected");
        Assert.Equal(3, source.Split(".Classes.Set(\"selected\"").Length - 1);
        Assert.DoesNotContain(window.Descendants(), element => Name(element)?.Contains("Alpha") == true);
        Assert.DoesNotContain(window.Descendants(), element => Name(element)?.Contains("Eyedropper") == true);
    }

    [Fact]
    public void ColorEditingSurfacesUseTheSharedSwatchControlWithoutLegacyEllipsisControls()
    {
        var settings = XDocument.Load(RepositoryPath("Fovium", "Views", "SettingsWindow.axaml"));
        var editor = XDocument.Load(RepositoryPath("Fovium", "Views", "StageBackgroundEditor.axaml"));
        var viewer = XDocument.Load(RepositoryPath("Fovium", "Views", "ViewerWindow.axaml"));
        var expected = new Dictionary<string, XDocument>
        {
            ["CustomColorButton"] = editor,
            ["MatteColorButton"] = settings,
            ["HighlightColorButton"] = settings,
            ["DefaultMarkupColorButton"] = settings,
            ["MarkupColorButton"] = viewer,
        };

        foreach (var (name, document) in expected)
        {
            var swatch = Assert.Single(document.Descendants(), element => Name(element) == name);
            Assert.Equal("ColorSwatchButton", swatch.Name.LocalName);
        }

        Assert.DoesNotContain(settings.Descendants(), element => Attribute(element, "Content") == "…");
        Assert.DoesNotContain(settings.Descendants(),
            element => Name(element)?.StartsWith("Edit", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void SettingsPagesUseInsetViewportWithNonInteractiveDynamicFadesAndKeepOffsets()
    {
        var settings = XDocument.Load(RepositoryPath("Fovium", "Views", "SettingsWindow.axaml"));
        var pageView = XDocument.Load(RepositoryPath("Fovium", "Views", "SettingsPageView.axaml"));
        var edgeFade = XDocument.Load(RepositoryPath("Fovium", "Views", "EdgeFadeScrollViewer.axaml"));
        var source = File.ReadAllText(RepositoryPath("Fovium", "Views", "SettingsWindow.axaml.cs"));

        Assert.Equal(7, settings.Descendants().Count(element => element.Name.LocalName == "SettingsPageView"));
        Assert.Single(pageView.Descendants(), element => element.Name.LocalName == "EdgeFadeScrollViewer");
        var fades = edgeFade.Descendants()
            .Where(element => Name(element) is "TopFade" or "BottomFade")
            .ToArray();
        Assert.Equal(2, fades.Length);
        Assert.All(fades, fade =>
        {
            Assert.Equal("False", Attribute(fade, "IsHitTestVisible"));
            Assert.Equal("{StaticResource FoviumEdgeFadeDepth}", Attribute(fade, "Height"));
        });
        Assert.DoesNotContain("ScrollToHome", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SecondaryWindowsShareOneChromeAndCanonicalVectorCloseButton()
    {
        var windowNames = new[] { "SettingsWindow", "ColorPickerWindow", "ShortcutConflictWindow" };
        var controls = XDocument.Load(RepositoryPath("Fovium", "Themes", "FoviumControls.axaml"));
        var windows = XDocument.Load(RepositoryPath("Fovium", "Themes", "FoviumWindows.axaml"));
        var tokens = XDocument.Load(RepositoryPath("Fovium", "Themes", "FoviumTokens.axaml"));

        Assert.Single(ElementsNamed(controls, "Style"), style => Attribute(style, "Selector") == "Button.window-close");
        Assert.Single(tokens.Descendants(), element => Key(element) == "FoviumCloseIconGeometry");
        Assert.Single(controls.Descendants(), element =>
            element.Name.LocalName == "PathIcon" &&
            Attribute(element, "Data") == "{StaticResource FoviumCloseIconGeometry}");
        Assert.Single(ElementsNamed(windows, "Style"),
            style => Attribute(style, "Selector") == "Window.fovium-secondary-window");

        foreach (var windowName in windowNames)
        {
            var document = XDocument.Load(RepositoryPath("Fovium", "Views", $"{windowName}.axaml"));
            Assert.Contains("fovium-secondary-window", Attribute(document.Root!, "Classes"));
            var close = Assert.Single(document.Descendants(), element =>
                element.Name.LocalName == "Button" &&
                Attribute(element, "Classes").Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Contains("window-close", StringComparer.Ordinal));
            Assert.Contains("window-close", Attribute(close, "Classes"));
            Assert.Empty(Attribute(close, "Content"));
            Assert.DoesNotContain(ElementsNamed(document, "Style"),
                style => Attribute(style, "Selector").Contains("window-close", StringComparison.Ordinal));
            Assert.DoesNotContain(document.DescendantNodes().OfType<XText>(),
                text => text.Value.Contains('×'));
        }
    }

    [Fact]
    public void SharedButtonsCenterContentAndUseSemanticInteractionResources()
    {
        var controls = XDocument.Load(RepositoryPath("Fovium", "Themes", "FoviumControls.axaml"));
        var baseButton = Assert.Single(ElementsNamed(controls, "Style"),
            style => Attribute(style, "Selector") == "Button.fovium-button");

        AssertSetter(baseButton, "HorizontalContentAlignment", "Center");
        AssertSetter(baseButton, "VerticalContentAlignment", "Center");
        AssertSetter(baseButton, "MinHeight", "{StaticResource FoviumControlHeight}");
        Assert.Contains(ElementsNamed(controls, "Style"),
            style => Attribute(style, "Selector") == "Button.primary:pointerover");
        Assert.Contains(ElementsNamed(controls, "Style"),
            style => Attribute(style, "Selector") == "Button.fovium-button:disabled");
    }

    [Fact]
    public void ScrollbarSeparatesSixDipVisualThumbFromSixteenDipPointerTarget()
    {
        var scroll = XDocument.Load(RepositoryPath("Fovium", "Views", "EdgeFadeScrollViewer.axaml"));
        var tokens = XDocument.Load(RepositoryPath("Fovium", "Themes", "FoviumTokens.axaml"));
        var verticalBar = Assert.Single(ElementsNamed(scroll, "Style"),
            style => Attribute(style, "Selector") == "ScrollBar:vertical");
        var thumbTarget = Assert.Single(ElementsNamed(scroll, "Style"),
            style => Attribute(style, "Selector") == "ScrollBar:vertical /template/ Thumb");
        var visibleThumb = Assert.Single(tokens.Descendants(), element => Name(element) == "VisibleThumb");

        AssertSetter(verticalBar, "Width", "16");
        AssertSetter(verticalBar, "AllowAutoHide", "False");
        AssertSetter(thumbTarget, "Width", "16");
        Assert.Equal("6", Attribute(visibleThumb, "Width"));
        Assert.Contains(ElementsNamed(tokens, "Style"),
            style => Attribute(style, "Selector") == "^:pointerover /template/ Border#VisibleThumb");
    }

    [Fact]
    public void StageBackgroundUsesScrollableModeRailAndContextualPanels()
    {
        var editor = XDocument.Load(RepositoryPath("Fovium", "Views", "StageBackgroundEditor.axaml"));
        var modeSelector = Assert.Single(editor.Descendants(), element => Name(element) == "ModeSelector");

        Assert.Equal(9, modeSelector.Elements().Count(element => element.Name.LocalName == "ListBoxItem"));
        Assert.Single(editor.Descendants(), element => Name(element) == "ModeScroller");
        Assert.Single(editor.Descendants(), element => Name(element) == "NoModeOptions");
        Assert.Single(editor.Descendants(), element => Name(element) == "CustomModeOptions");
        Assert.Single(editor.Descendants(), element => Name(element) == "AdjustmentModeOptions");
        Assert.Single(editor.Descendants(), element => Name(element) == "BlurRow");
        Assert.DoesNotContain(editor.Descendants(), element => element.Name.LocalName == "RadioButton");
        Assert.DoesNotContain(editor.Descendants(), element => element.Name.LocalName == "WrapPanel");
    }

    [Fact]
    public void AllColorEditingCallersUseOnePickerAndLegacyEditorIsGone()
    {
        var settingsSource = File.ReadAllText(RepositoryPath("Fovium", "Views", "SettingsWindow.axaml.cs"));
        var viewerSource = File.ReadAllText(RepositoryPath("Fovium", "Views", "ViewerWindow.axaml.cs"));

        Assert.Contains("new ColorPickerWindow", settingsSource, StringComparison.Ordinal);
        Assert.Contains("new ColorPickerWindow", viewerSource, StringComparison.Ordinal);
        Assert.Contains("editor.Resolve(accepted)", settingsSource, StringComparison.Ordinal);
        Assert.Contains("editor.Resolve(accepted)", viewerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ColorEditorWindow", settingsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ColorEditorWindow", viewerSource, StringComparison.Ordinal);
        Assert.False(File.Exists(RepositoryPath("Fovium", "Views", "ColorEditorWindow.axaml")));
    }

    [Fact]
    public void ShortcutConflictUsesOwnedProjectChromeAndConsistentCancelPaths()
    {
        var document = XDocument.Load(RepositoryPath("Fovium", "Views", "ShortcutConflictWindow.axaml"));
        var window = document.Root!;
        var source = File.ReadAllText(RepositoryPath("Fovium", "Views", "ShortcutConflictWindow.axaml.cs"));

        Assert.Contains("fovium-secondary-window", Attribute(window, "Classes"));
        Assert.Contains("ShowDialog<bool>(this)",
            File.ReadAllText(RepositoryPath("Fovium", "Views", "SettingsWindow.axaml.cs")),
            StringComparison.Ordinal);
        Assert.Contains("Key.Escape", source, StringComparison.Ordinal);
        Assert.Contains("Close(false)", source, StringComparison.Ordinal);
    }

    private static string? Name(XElement element) =>
        element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == "Name")?.Value;

    private static string Attribute(XElement element, string name) =>
        element.Attribute(name)?.Value ?? string.Empty;

    private static IEnumerable<XElement> ElementsNamed(XDocument document, string localName) =>
        document.Descendants().Where(element => element.Name.LocalName == localName);

    private static string? Key(XElement element) =>
        element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == "Key")?.Value;

    private static void AssertSetter(XElement style, string property, string value)
    {
        var setter = Assert.Single(style.Elements(), element =>
            element.Name.LocalName == "Setter" && Attribute(element, "Property") == property);
        Assert.Equal(value, Attribute(setter, "Value"));
    }

    private static string RepositoryPath(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Fovium.sln")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return segments.Aggregate(current.FullName, Path.Combine);
    }
}