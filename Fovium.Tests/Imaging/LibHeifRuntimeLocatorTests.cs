using Fovium.Imaging;

namespace Fovium.Tests.Imaging;

public sealed class LibHeifRuntimeLocatorTests
{
    [Theory]
    [MemberData(nameof(AcceptedRuntimeLayouts))]
    public void AcceptedRidsLocateOnlyTheirExactAppLocalRuntime(
        string rid,
        string mainLibrary,
        string dav1dLibrary,
        string de265Library)
    {
        var root = Directory.CreateTempSubdirectory("Fovium.LibHeifLocator.Tests.");
        try
        {
            var nativeDirectory = Directory.CreateDirectory(Path.Combine(root.FullName, "runtimes", rid, "native"));
            foreach (var name in new[] { mainLibrary, dav1dLibrary, de265Library })
            {
                File.WriteAllBytes(Path.Combine(nativeDirectory.FullName, name), [0x01]);
            }

            var located = new LibHeifRuntimeLocator(root.FullName, rid).TryLocate(
                out var location,
                out var detail);

            Assert.True(located, detail);
            var actual = Assert.IsType<LibHeifRuntimeLocation>(location);
            Assert.Equal(rid, actual.Rid);
            Assert.Equal(Path.GetFullPath(nativeDirectory.FullName), actual.NativeDirectory);
            Assert.Equal(Path.GetFullPath(Path.Combine(nativeDirectory.FullName, mainLibrary)), actual.MainLibraryPath);
            Assert.Equal(
                [
                    Path.GetFullPath(Path.Combine(nativeDirectory.FullName, dav1dLibrary)),
                    Path.GetFullPath(Path.Combine(nativeDirectory.FullName, de265Library)),
                ],
                actual.DependencyPaths);
            Assert.Contains(actual.MainLibraryPath, detail, StringComparison.Ordinal);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Theory]
    [InlineData("osx-x64")]
    [InlineData("linux-arm64")]
    [InlineData("win-arm64")]
    [InlineData("unknown")]
    public void UnprovenRidIsUnavailableWithoutSearchingTheFilesystem(string rid)
    {
        var root = Directory.CreateTempSubdirectory("Fovium.LibHeifLocator.Tests.");
        try
        {
            var located = new LibHeifRuntimeLocator(root.FullName, rid).TryLocate(
                out var location,
                out var detail);

            Assert.False(located);
            Assert.Null(location);
            Assert.Contains(rid, detail, StringComparison.Ordinal);
            Assert.DoesNotContain("/usr/lib", detail, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("homebrew", detail, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void MissingBundleReportsTheExactAppOwnedPathWithoutSystemFallback()
    {
        var root = Directory.CreateTempSubdirectory("Fovium.LibHeifLocator.Tests.");
        try
        {
            var expected = Path.Combine(root.FullName, "runtimes", "win-x64", "native");

            var located = new LibHeifRuntimeLocator(root.FullName, "win-x64").TryLocate(
                out var location,
                out var detail);

            Assert.False(located);
            Assert.Null(location);
            Assert.Contains(Path.GetFullPath(expected), detail, StringComparison.Ordinal);
            Assert.DoesNotContain("PATH", detail, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/usr", detail, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Theory]
    [InlineData("dav1d.dll", "de265")]
    [InlineData("libde265.dll", "dav1d")]
    public void IncompleteBundleDoesNotResolveAMissingComponentOutsideItsNativeDirectory(
        string presentDependency,
        string missingComponent)
    {
        var root = Directory.CreateTempSubdirectory("Fovium.LibHeifLocator.Tests.");
        try
        {
            var nativeDirectory = Directory.CreateDirectory(
                Path.Combine(root.FullName, "runtimes", "win-x64", "native"));
            File.WriteAllBytes(Path.Combine(nativeDirectory.FullName, "heif.dll"), [0x01]);
            File.WriteAllBytes(Path.Combine(nativeDirectory.FullName, presentDependency), [0x01]);

            var located = new LibHeifRuntimeLocator(root.FullName, "win-x64").TryLocate(
                out var location,
                out var detail);

            Assert.False(located);
            Assert.Null(location);
            Assert.Contains(missingComponent, detail, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(nativeDirectory.FullName, detail, StringComparison.Ordinal);
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void BrokenAppLocalBundleReturnsUnavailableWithoutEscapingNativeLoadFailure()
    {
        var rid = LibHeifRuntimeLocator.GetSupportedCurrentRid();
        if (rid is not ("win-x64" or "linux-x64" or "osx-arm64"))
        {
            return;
        }

        var layout = GetRuntimeLayout(rid);
        var root = Directory.CreateTempSubdirectory("Fovium.LibHeifLocator.Tests.");
        try
        {
            var nativeDirectory = Directory.CreateDirectory(Path.Combine(root.FullName, "runtimes", rid, "native"));
            foreach (var name in new[] { layout.MainLibrary, layout.Dav1dLibrary, layout.De265Library })
            {
                File.WriteAllBytes(Path.Combine(nativeDirectory.FullName, name), [0x01]);
            }

            var availability = new LibHeifRuntimeLocator(root.FullName, rid).TryLoad();

            Assert.False(availability.IsAvailable);
            Assert.Null(availability.Runtime);
            Assert.Contains("unavailable", availability.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            root.Delete(true);
        }
    }

    public static TheoryData<string, string, string, string> AcceptedRuntimeLayouts() =>
        new()
        {
            { "win-x64", "heif.dll", "dav1d.dll", "libde265.dll" },
            { "linux-x64", "libheif.so.1", "libdav1d.so.7", "libde265.so.0" },
            { "osx-arm64", "libheif.1.dylib", "libdav1d.7.dylib", "libde265.0.dylib" },
        };

    private static (string MainLibrary, string Dav1dLibrary, string De265Library) GetRuntimeLayout(string rid) =>
        rid switch
        {
            "win-x64" => ("heif.dll", "dav1d.dll", "libde265.dll"),
            "linux-x64" => ("libheif.so.1", "libdav1d.so.7", "libde265.so.0"),
            "osx-arm64" => ("libheif.1.dylib", "libdav1d.7.dylib", "libde265.0.dylib"),
            _ => throw new ArgumentOutOfRangeException(nameof(rid)),
        };
}
