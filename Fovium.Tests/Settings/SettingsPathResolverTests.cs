using Fovium.Settings;

namespace Fovium.Tests.Settings;

public sealed class SettingsPathResolverTests
{
    [Fact]
    public void LinuxUsesXdgConfigHomeWhenPresent()
    {
        var path = SettingsPathResolver.ResolveLinuxBaseDirectory("relative-xdg", "/unused");

        Assert.Equal(Path.GetFullPath("relative-xdg"), path);
    }

    [Fact]
    public void LinuxFallsBackToProfileConfigDirectory()
    {
        var path = SettingsPathResolver.ResolveLinuxBaseDirectory(null, "/home/tester");

        Assert.Equal(Path.Combine("/home/tester", ".config"), path);
    }
}
