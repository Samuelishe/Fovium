using System.Xml.Linq;

namespace Fovium.Tests.Views;

public sealed class SettingsEcosystemStructureTests
{
    [Fact]
    public void ColorPickerUsesProjectChromeCircularWheelValueStripAndExactRepresentations()
    {
        var document = XDocument.Load(RepositoryPath("Fovium", "Views", "ColorPickerWindow.axaml"));
        var window = document.Root!;

        Assert.Equal("None", Attribute(window, "WindowDecorations"));
        Assert.Equal("False", Attribute(window, "Topmost"));
        Assert.Single(window.Descendants(), element => Name(element) == "ColorWheel");
        Assert.Single(window.Descendants(), element => Name(element) == "ValueStrip");
        Assert.Single(window.Descendants(), element => Name(element) == "RgbPanel");
        Assert.Single(window.Descendants(), element => Name(element) == "HsvPanel");
        Assert.Single(window.Descendants(), element => Name(element) == "HexPanel");
        Assert.DoesNotContain(window.Descendants(), element => Name(element)?.Contains("Alpha") == true);
        Assert.DoesNotContain(window.Descendants(), element => Name(element)?.Contains("Eyedropper") == true);
    }

    [Fact]
    public void SettingsColorSwatchesAreFocusableButtonsWithoutLegacyEllipsisControls()
    {
        var document = XDocument.Load(RepositoryPath("Fovium", "Views", "SettingsWindow.axaml"));
        var expected = new[]
        {
            "CustomColorButton",
            "MatteColorButton",
            "HighlightColorButton",
            "DefaultMarkupColorButton",
        };

        foreach (var name in expected)
        {
            var button = Assert.Single(document.Descendants(), element => Name(element) == name);
            Assert.Equal("Button", button.Name.LocalName);
            Assert.Contains("color-swatch", Attribute(button, "Classes"));
            Assert.Single(button.Elements(), element => element.Name.LocalName == "Border");
        }

        Assert.DoesNotContain(document.Descendants(), element => Attribute(element, "Content") == "…");
        Assert.DoesNotContain(document.Descendants(),
            element => Name(element)?.StartsWith("Edit", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void SettingsPagesUseInsetViewportWithNonInteractiveDynamicFadesAndKeepOffsets()
    {
        var settings = XDocument.Load(RepositoryPath("Fovium", "Views", "SettingsWindow.axaml"));
        var pageView = XDocument.Load(RepositoryPath("Fovium", "Views", "SettingsPageView.axaml"));
        var source = File.ReadAllText(RepositoryPath("Fovium", "Views", "SettingsWindow.axaml.cs"));

        Assert.Equal(7, settings.Descendants().Count(element => element.Name.LocalName == "SettingsPageView"));
        var fades = pageView.Descendants()
            .Where(element => Name(element) is "TopFade" or "BottomFade")
            .ToArray();
        Assert.Equal(2, fades.Length);
        Assert.All(fades, fade =>
        {
            Assert.Equal("False", Attribute(fade, "IsHitTestVisible"));
            Assert.Equal("30", Attribute(fade, "Height"));
        });
        Assert.DoesNotContain("ScrollToHome", source, StringComparison.Ordinal);
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

        Assert.Equal("None", Attribute(window, "WindowDecorations"));
        Assert.Equal("False", Attribute(window, "Topmost"));
        Assert.Equal("False", Attribute(window, "ShowInTaskbar"));
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