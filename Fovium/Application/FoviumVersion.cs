using System.Reflection;

namespace Fovium.Application;

internal static class FoviumVersion
{
    private static readonly Assembly Assembly = typeof(FoviumVersion).Assembly;

    public static string Display =>
        Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? throw new InvalidOperationException("Fovium informational version metadata is missing.");

    public static string AssemblyNumeric =>
        Assembly.GetName().Version?.ToString()
        ?? throw new InvalidOperationException("Fovium assembly version metadata is missing.");

    public static string FileNumeric =>
        Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
        ?? throw new InvalidOperationException("Fovium file version metadata is missing.");
}
