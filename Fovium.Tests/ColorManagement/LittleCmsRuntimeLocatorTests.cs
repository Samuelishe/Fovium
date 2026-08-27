using Fovium.ColorManagement;

namespace Fovium.Tests.ColorManagement;

public sealed class LittleCmsRuntimeLocatorTests
{
    [Fact]
    public void MissingAppLocalRuntimeDoesNotSearchTheSystem()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.LcmsMissing.Tests.");
        try
        {
            var locator = new LittleCmsRuntimeLocator(directory.FullName, "win-x64");

            var availability = locator.TryLoad();

            Assert.False(availability.IsAvailable);
            Assert.Null(availability.Runtime);
            Assert.Contains("directory is missing", availability.Detail, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void LocatorSelectsOnlyTheRidOwnedNativeDirectory()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.LcmsLocate.Tests.");
        try
        {
            var native = Directory.CreateDirectory(Path.Combine(directory.FullName, "runtimes", "win-x64", "native"));
            var library = Path.Combine(native.FullName, "lcms2.dll");
            File.WriteAllBytes(library, [1, 2, 3]);
            var locator = new LittleCmsRuntimeLocator(directory.FullName, "win-x64");

            var found = locator.TryLocate(out var location, out var detail);

            Assert.True(found, detail);
            Assert.NotNull(location);
            Assert.Equal(Path.GetFullPath(library), location.LibraryPath);
            Assert.Equal(Path.GetFullPath(native.FullName), location.NativeDirectory);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
