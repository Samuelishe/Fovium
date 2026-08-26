using System.Runtime.InteropServices;

namespace Fovium.Imaging;

internal sealed record LibHeifRuntimeLocation(
    string Rid,
    string NativeDirectory,
    string MainLibraryPath,
    IReadOnlyList<string> DependencyPaths);

internal sealed record LibHeifRuntimeAvailability(
    LibHeifRuntime? Runtime,
    string TechnicalDetail)
{
    public bool IsAvailable => Runtime is not null;
}

internal sealed class LibHeifRuntimeLocator
{
    private readonly string _applicationBaseDirectory;
    private readonly string? _ridOverride;

    public LibHeifRuntimeLocator(string? applicationBaseDirectory = null, string? ridOverride = null)
    {
        _applicationBaseDirectory = Path.GetFullPath(applicationBaseDirectory ?? AppContext.BaseDirectory);
        _ridOverride = ridOverride;
    }

    public LibHeifRuntimeAvailability TryLoad()
    {
        try
        {
            if (!TryLocate(out var location, out var detail) || location is null)
            {
                return new LibHeifRuntimeAvailability(null, detail);
            }

            var runtime = LibHeifRuntime.Load(location);
            return new LibHeifRuntimeAvailability(
                runtime,
                $"Loaded Fovium-owned libheif {runtime.Version} from {runtime.LoadedLibraryPath}.");
        }
        catch (Exception exception) when (exception is
            DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or IOException or
            UnauthorizedAccessException)
        {
            return new LibHeifRuntimeAvailability(
                null,
                $"The Fovium-owned libheif runtime is unavailable: {exception.Message}");
        }
    }

    public bool TryLocate(out LibHeifRuntimeLocation? location, out string technicalDetail)
    {
        var rid = _ridOverride ?? GetSupportedCurrentRid();
        if (rid is not ("win-x64" or "linux-x64" or "osx-arm64"))
        {
            location = null;
            technicalDetail = $"HEIF/AVIF decoding is unavailable for runtime identifier '{rid ?? "unknown"}'.";
            return false;
        }

        var nativeDirectory = Path.GetFullPath(
            Path.Combine(_applicationBaseDirectory, "runtimes", rid, "native"));
        if (!Directory.Exists(nativeDirectory))
        {
            location = null;
            technicalDetail = $"The Fovium-owned native runtime directory is missing: {nativeDirectory}";
            return false;
        }

        var mainLibrary = FindLibrary(nativeDirectory, rid switch
        {
            "win-x64" => ["heif.dll", "libheif.dll"],
            "linux-x64" => ["libheif.so.1", "libheif.so"],
            "osx-arm64" => ["libheif.1.dylib", "libheif.dylib"],
            _ => [],
        }, "libheif");
        if (mainLibrary is null)
        {
            location = null;
            technicalDetail = $"The Fovium-owned libheif library is missing from {nativeDirectory}.";
            return false;
        }

        var dependencies = new List<string>();
        foreach (var component in new[] { "dav1d", "de265" })
        {
            var dependency = FindLibrary(nativeDirectory, [], component);
            if (dependency is null)
            {
                location = null;
                technicalDetail = $"The Fovium-owned {component} library is missing from {nativeDirectory}.";
                return false;
            }

            dependencies.Add(dependency);
        }

        location = new LibHeifRuntimeLocation(rid, nativeDirectory, mainLibrary, dependencies);
        technicalDetail = $"Fovium-owned libheif runtime found at {mainLibrary}.";
        return true;
    }

    internal static string? GetSupportedCurrentRid()
    {
        if (OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            return "win-x64";
        }

        if (OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            return "linux-x64";
        }

        if (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            return "osx-arm64";
        }

        return RuntimeInformation.RuntimeIdentifier;
    }

    private static string? FindLibrary(
        string nativeDirectory,
        IReadOnlyList<string> preferredNames,
        string componentMarker)
    {
        foreach (var preferredName in preferredNames)
        {
            var candidate = Path.GetFullPath(Path.Combine(nativeDirectory, preferredName));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Directory
            .EnumerateFiles(nativeDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).Contains(componentMarker, StringComparison.OrdinalIgnoreCase))
            .Where(path => Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                           Path.GetFileName(path).Contains(".so", StringComparison.Ordinal) ||
                           Path.GetExtension(path).Equals(".dylib", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(Path.GetFullPath)
            .FirstOrDefault();
    }
}
