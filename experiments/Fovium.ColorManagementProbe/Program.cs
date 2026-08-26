using Avalonia;

namespace Fovium.ColorManagementProbe;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--avalonia-target", StringComparer.Ordinal))
        {
            return AppBuilder
                .Configure<TargetProbeApp>()
                .UsePlatformDetect()
                .StartWithClassicDesktopLifetime(args);
        }

        var profileDirectory = ReadOption(args, "--profiles") ?? Path.Combine(
            FindRepositoryRoot(),
            "resources",
            "test-images",
            "color-management",
            "profiles");
        return ProbeReporter.RunHeadless(
            profileDirectory,
            args.Contains("--benchmark", StringComparer.Ordinal));
    }

    private static string? ReadOption(string[] args, string option)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], option, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Fovium.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
