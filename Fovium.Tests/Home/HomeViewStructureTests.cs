using System.Xml.Linq;

namespace Fovium.Tests.Home;

public sealed class HomeViewStructureTests
{
    [Fact]
    public void RecentUsesOneHorizontalStripWithHiddenSystemScrollbars()
    {
        var document = XDocument.Load(RepositoryPath("Fovium", "Views", "ViewerWindow.axaml"));
        var recent = Assert.Single(document.Descendants(),
            element => Name(element) == "HomeRecentSection");
        var scroller = Assert.Single(recent.Descendants(),
            element => Name(element) == "HomeRecentScroller");
        var panel = Assert.Single(recent.Descendants(),
            element => Name(element) == "HomeRecentItemsPanel");

        Assert.Equal("Hidden", Attribute(scroller, "HorizontalScrollBarVisibility"));
        Assert.Equal("Disabled", Attribute(scroller, "VerticalScrollBarVisibility"));
        Assert.Equal("StackPanel", panel.Name.LocalName);
        Assert.Equal("Horizontal", Attribute(panel, "Orientation"));
        Assert.DoesNotContain(recent.Descendants(), element => element.Name.LocalName == "WrapPanel");
    }

    [Fact]
    public void HomeKeepsPrimaryActionsAndDoesNotDuplicateBrandIdentity()
    {
        var document = XDocument.Load(RepositoryPath("Fovium", "Views", "ViewerWindow.axaml"));
        var home = Assert.Single(document.Descendants(),
            element => Name(element) == "HomeSurface");

        Assert.Single(home.Descendants(), element => Name(element) == "HomeOpenFilesButton");
        Assert.Single(home.Descendants(), element => Name(element) == "HomeOpenFolderButton");
        Assert.DoesNotContain(home.Descendants(), element => Name(element) == "HomeBrandIcon");
        Assert.DoesNotContain(home.Descendants(), element => Name(element) == "HomeBrandTitle");
    }

    private static string? Name(XElement element) =>
        element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == "Name")?.Value;

    private static string? Attribute(XElement element, string name) =>
        element.Attribute(name)?.Value;

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