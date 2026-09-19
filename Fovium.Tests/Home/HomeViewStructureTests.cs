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

    [Fact]
    public void ContextMenuGroupsSessionAndApplicationActionsAndHidesCloseOnHome()
    {
        var source = File.ReadAllText(RepositoryPath("Fovium", "Views", "ViewerWindow.axaml.cs"))
            .ReplaceLineEndings("\n");
        var start = source.IndexOf("private ContextMenu CreateContextMenu()", StringComparison.Ordinal);
        var end = source.IndexOf("private IReadOnlyDictionary<StageBackgroundMode", start, StringComparison.Ordinal);
        var menu = source[start..end];
        var open = menu.IndexOf("UiStrings.MenuOpen", StringComparison.Ordinal);
        var close = menu.IndexOf("closePhoto,", StringComparison.Ordinal);
        var previous = menu.IndexOf("_previousMenuItem", StringComparison.Ordinal);
        var settings = menu.IndexOf("UiStrings.MenuSettings", StringComparison.Ordinal);
        var exit = menu.IndexOf("UiStrings.MenuExitFovium", StringComparison.Ordinal);

        Assert.True(open < close && close < previous);
        Assert.True(previous < settings && settings < exit);
        Assert.Contains("closePhoto.IsVisible = _contentState.Mode == ViewerContentMode.Viewer", menu);
        Assert.Contains("UiStrings.MenuSettings,\n                    ViewerCommand.Settings", menu);
        Assert.Contains("new Separator(),\n                CreateMenuItem(UiStrings.MenuExitFovium", menu);
    }

    [Fact]
    public void DisablingRecentStopsWorkAndClearsHomeOwnedThumbnailState()
    {
        var source = File.ReadAllText(RepositoryPath("Fovium", "Views", "ViewerWindow.axaml.cs"))
            .ReplaceLineEndings("\n");
        var start = source.IndexOf("if (!settings.Home.RememberRecentPhotos)", StringComparison.Ordinal);
        var end = source.IndexOf("if (_contentState.Mode == ViewerContentMode.Home)", start, StringComparison.Ordinal);
        var disabledRecent = source[start..end];

        Assert.Contains("StopHomeRefresh();", disabledRecent);
        Assert.Contains("DisposeHomeThumbnailBitmaps();", disabledRecent);
        Assert.Contains("_recentThumbnailProvider.ClearMemoryCache();", disabledRecent);
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