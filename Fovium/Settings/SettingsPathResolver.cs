namespace Fovium.Settings;

internal static class SettingsPathResolver
{
    public static string ResolveCurrent()
    {
        var baseDirectory = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : OperatingSystem.IsLinux()
                ? ResolveLinuxBaseDirectory()
                : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new InvalidOperationException("A per-user application-data directory is unavailable.");
        }

        return Path.Combine(baseDirectory, "Fovium", "settings.json");
    }

    internal static string ResolveLinuxBaseDirectory(string? xdgConfigHome, string userProfile) =>
        string.IsNullOrWhiteSpace(xdgConfigHome)
            ? Path.Combine(userProfile, ".config")
            : Path.GetFullPath(xdgConfigHome);

    private static string ResolveLinuxBaseDirectory() => ResolveLinuxBaseDirectory(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
}
