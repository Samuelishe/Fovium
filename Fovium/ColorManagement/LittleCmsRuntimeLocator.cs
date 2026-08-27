using System.Runtime.InteropServices;

namespace Fovium.ColorManagement;

internal sealed record LittleCmsRuntimeLocation(
    string Rid,
    string NativeDirectory,
    string LibraryPath);

internal sealed record LittleCmsRuntimeAvailability(
    LittleCmsRuntime? Runtime,
    string Detail)
{
    public bool IsAvailable => Runtime is not null;
}

internal sealed class LittleCmsRuntimeLocator
{
    private readonly string _applicationBaseDirectory;
    private readonly string? _ridOverride;

    public LittleCmsRuntimeLocator(string? applicationBaseDirectory = null, string? ridOverride = null)
    {
        _applicationBaseDirectory = Path.GetFullPath(applicationBaseDirectory ?? AppContext.BaseDirectory);
        _ridOverride = ridOverride;
    }

    public LittleCmsRuntimeAvailability TryLoad()
    {
        try
        {
            if (!TryLocate(out var location, out var detail) || location is null)
            {
                return new LittleCmsRuntimeAvailability(null, detail);
            }

            var runtime = LittleCmsRuntime.Load(location);
            return new LittleCmsRuntimeAvailability(
                runtime,
                $"Loaded Fovium-owned Little CMS {runtime.Version} from {runtime.LoadedLibraryPath}.");
        }
        catch (Exception exception) when (exception is
            DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or
            FileLoadException or IOException or UnauthorizedAccessException)
        {
            return new LittleCmsRuntimeAvailability(
                null,
                $"The Fovium-owned Little CMS runtime is unavailable: {exception.Message}");
        }
    }

    public bool TryLocate(out LittleCmsRuntimeLocation? location, out string detail)
    {
        var rid = _ridOverride ?? GetSupportedCurrentRid();
        if (rid is not ("win-x64" or "linux-x64" or "osx-arm64"))
        {
            location = null;
            detail = $"Little CMS is unavailable for runtime identifier '{rid ?? "unknown"}'.";
            return false;
        }

        var nativeDirectory = Path.GetFullPath(
            Path.Combine(_applicationBaseDirectory, "runtimes", rid, "native"));
        if (!Directory.Exists(nativeDirectory))
        {
            location = null;
            detail = "The Fovium-owned Little CMS runtime directory is missing.";
            return false;
        }

        var preferredNames = rid switch
        {
            "win-x64" => new[] { "lcms2.dll" },
            "linux-x64" => new[] { "liblcms2.so.2", "liblcms2.so" },
            "osx-arm64" => new[] { "liblcms2.2.dylib", "liblcms2.dylib" },
            _ => [],
        };
        var libraryPath = preferredNames
            .Select(name => Path.GetFullPath(Path.Combine(nativeDirectory, name)))
            .FirstOrDefault(File.Exists);
        libraryPath ??= Directory
            .EnumerateFiles(nativeDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).Contains("lcms2", StringComparison.OrdinalIgnoreCase))
            .Where(path => Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                           Path.GetFileName(path).Contains(".so", StringComparison.Ordinal) ||
                           Path.GetExtension(path).Equals(".dylib", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(Path.GetFullPath)
            .FirstOrDefault();
        if (libraryPath is null)
        {
            location = null;
            detail = "The Fovium-owned Little CMS shared library is missing.";
            return false;
        }

        if (!IsContainedBy(libraryPath, nativeDirectory))
        {
            location = null;
            detail = "The resolved Little CMS library is outside the Fovium-owned runtime directory.";
            return false;
        }

        var resolvedTarget = new FileInfo(libraryPath).ResolveLinkTarget(returnFinalTarget: true);
        if (resolvedTarget is not null &&
            !IsContainedBy(Path.GetFullPath(resolvedTarget.FullName), nativeDirectory))
        {
            location = null;
            detail = "The resolved Little CMS symlink target is outside the Fovium-owned runtime directory.";
            return false;
        }

        location = new LittleCmsRuntimeLocation(rid, nativeDirectory, libraryPath);
        detail = "Fovium-owned Little CMS runtime found.";
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

    private static bool IsContainedBy(string path, string directory)
    {
        var relative = Path.GetRelativePath(directory, path);
        return relative != ".." &&
               !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
               !Path.IsPathRooted(relative);
    }
}
