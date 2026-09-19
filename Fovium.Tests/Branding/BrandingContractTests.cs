using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Fovium.Tests.Branding;

public sealed partial class BrandingContractTests
{
    private static readonly int[] ExpectedIconSizes = [16, 20, 24, 32, 48, 64, 128, 256];

    [Fact]
    public void WindowsIcon_ContainsRequiredPngFramesInAscendingOrder()
    {
        var iconPath = RepositoryPath("resources", "branding", "fovium.ico");
        using var stream = File.OpenRead(iconPath);
        using var reader = new BinaryReader(stream);

        Assert.Equal(0, reader.ReadUInt16());
        Assert.Equal(1, reader.ReadUInt16());
        var frameCount = reader.ReadUInt16();
        Assert.Equal(ExpectedIconSizes.Length, frameCount);

        var entries = Enumerable.Range(0, frameCount)
            .Select(_ => ReadIconEntry(reader))
            .ToArray();

        Assert.Equal(ExpectedIconSizes, entries.Select(entry => entry.Width));
        Assert.All(entries, entry =>
        {
            Assert.Equal(entry.Width, entry.Height);
            Assert.Equal(1, entry.Planes);
            Assert.Equal(32, entry.BitsPerPixel);
            Assert.InRange((long)entry.Offset + entry.Length, 1, stream.Length);

            stream.Position = entry.Offset;
            Assert.Equal(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
                reader.ReadBytes(8));
        });
    }

    [Fact]
    public void ApplicationProject_OwnsExecutableAndDefaultWindowIcon()
    {
        var projectPath = RepositoryPath("Fovium", "Fovium.csproj");
        var project = XDocument.Load(projectPath);

        var applicationIcon = Assert.Single(project.Descendants("ApplicationIcon"));
        Assert.EndsWith(
            "resources/branding/fovium.ico",
            applicationIcon.Value.Replace('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "true",
            Assert.Single(project.Descendants("AvaloniaIncludeApplicationIconAsWindowIcon")).Value);

        var viewRoot = RepositoryPath("Fovium", "Views");
        var windows = Directory.EnumerateFiles(viewRoot, "*.axaml")
            .Where(path => File.ReadAllText(path).TrimStart().StartsWith("<Window ", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(4, windows.Length);
        Assert.All(windows, path => Assert.DoesNotMatch(@"\sIcon\s*=", File.ReadAllText(path)));
    }

    [Fact]
    public void Readme_LocalTargetsAndWorkflowBadgesResolveToOwnedFiles()
    {
        var readmePath = RepositoryPath("README.md");
        var readme = File.ReadAllText(readmePath);

        var localTargets = MarkdownTargetRegex().Matches(readme)
            .Select(match => match.Groups[1].Value.Trim().Trim('<', '>'))
            .Concat(HtmlImageTargetRegex().Matches(readme).Select(match => match.Groups[1].Value))
            .Where(target => !target.StartsWith('#'))
            .Where(target => !Uri.IsWellFormedUriString(target, UriKind.Absolute))
            .Select(target => Uri.UnescapeDataString(target.Split('#')[0]))
            .Where(target => target.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(localTargets);
        Assert.All(localTargets, target =>
        {
            var path = RepositoryPath(target.Split('/'));
            Assert.True(File.Exists(path) || Directory.Exists(path), $"README target is missing: {target}");
        });

        var workflows = new[] { "ci.yml", "native-libheif.yml", "native-lcms2.yml" };
        Assert.All(workflows, workflow =>
        {
            Assert.True(File.Exists(RepositoryPath(".github", "workflows", workflow)));
            Assert.Contains($"actions/workflows/{workflow}/badge.svg?branch=master", readme, StringComparison.Ordinal);
            Assert.Contains($"actions/workflows/{workflow}\"", readme, StringComparison.Ordinal);
        });
    }

    private static IconEntry ReadIconEntry(BinaryReader reader)
    {
        var width = reader.ReadByte();
        var height = reader.ReadByte();
        _ = reader.ReadByte();
        _ = reader.ReadByte();
        var planes = reader.ReadUInt16();
        var bitsPerPixel = reader.ReadUInt16();
        var length = reader.ReadUInt32();
        var offset = reader.ReadUInt32();
        return new IconEntry(
            width == 0 ? 256 : width,
            height == 0 ? 256 : height,
            planes,
            bitsPerPixel,
            length,
            offset);
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

    private sealed record IconEntry(
        int Width,
        int Height,
        ushort Planes,
        ushort BitsPerPixel,
        uint Length,
        uint Offset);

    [GeneratedRegex(@"!?(?:\[[^\]]*\])\(([^)]+)\)")]
    private static partial Regex MarkdownTargetRegex();

    [GeneratedRegex("<img\\s+[^>]*src=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlImageTargetRegex();
}