using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using SkiaSharp;

namespace Fovium.ColorManagementProbe;

internal static class ProbeReporter
{
    private sealed record ProfileParseEvidence(
        string Filename,
        IccProfileInspection Inspection,
        string Skia,
        double ParseMicroseconds);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static int RunHeadless(string profileDirectory, bool benchmark)
    {
        Directory.CreateDirectory(profileDirectory);
        using var srgb = SKColorSpace.CreateSrgb();
        using var displayP3 = SKColorSpace.CreateRgb(
            SKColorSpaceTransferFn.Srgb,
            SKColorSpaceXyz.DisplayP3);
        using var adobeRgb = SKColorSpace.CreateRgb(
            SKColorSpaceTransferFn.Srgb,
            SKColorSpaceXyz.AdobeRgb);

        var generated = new[]
        {
            WriteSyntheticProfile(profileDirectory, "fovium-synthetic-srgb.icc", srgb),
            WriteSyntheticProfile(profileDirectory, "fovium-synthetic-display-p3.icc", displayP3),
            WriteSyntheticProfile(profileDirectory, "fovium-synthetic-adobe-rgb-srgb-trc.icc", adobeRgb),
        };

        var installedSrgb = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "spool",
            "drivers",
            "color",
            "sRGB Color Space Profile.icm");
        if (OperatingSystem.IsWindows() && File.Exists(installedSrgb))
        {
            File.Copy(installedSrgb, Path.Combine(profileDirectory, "windows-srgb.icm"), overwrite: true);
        }

        var profiles = Directory
            .EnumerateFiles(profileDirectory)
            .Where(path => Path.GetExtension(path) is ".icc" or ".icm")
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(ReadProfile)
            .ToArray();

        var input = new ProbePixel(196, 83, 41, 255);
        var partial = input with { Alpha = 128 };
        var transparent = input with { Alpha = 0 };
        var transforms = new
        {
            Input = input,
            DisplayP3ToSrgb = SkiaColorTransformProbe.TransformPixel(input, displayP3, srgb),
            SrgbToDisplayP3 = SkiaColorTransformProbe.TransformPixel(input, srgb, displayP3),
            SrgbToAdobeRgb = SkiaColorTransformProbe.TransformPixel(input, srgb, adobeRgb),
            PartialAlphaP3ToSrgb = SkiaColorTransformProbe.TransformPixel(partial, displayP3, srgb),
            TransparentP3ToSrgb = SkiaColorTransformProbe.TransformPixel(transparent, displayP3, srgb),
            SrgbIdentity = SkiaColorTransformProbe.TransformPixel(input, srgb, srgb),
            DisplayP3ToUntagged = SkiaColorTransformProbe.DrawToUntaggedSurface(input, displayP3),
        };
        var officialTransforms = ReadOfficialProfileTransforms(profileDirectory);

        TransformBenchmark[] benchmarks = [];
        TransformMemoryObservation[] memory = [];
        if (benchmark)
        {
            benchmarks =
            [
                SkiaColorTransformProbe.Benchmark(6000, 4000, 6000, 4000, displayP3, srgb, retainSnapshot: true),
                SkiaColorTransformProbe.Benchmark(6000, 4000, 1920, 1080, displayP3, srgb, retainSnapshot: true),
                SkiaColorTransformProbe.Benchmark(6000, 4000, 1920, 1080, displayP3, srgb, retainSnapshot: false),
            ];
            memory =
            [
                SkiaColorTransformProbe.ObserveRetainedMemory(6000, 4000, 6000, 4000, displayP3, srgb),
                SkiaColorTransformProbe.ObserveRetainedMemory(6000, 4000, 1920, 1080, displayP3, srgb),
            ];
        }

        var sourceBefore = input;
        _ = SkiaColorTransformProbe.TransformPixel(sourceBefore, srgb, displayP3);
        _ = SkiaColorTransformProbe.TransformPixel(sourceBefore, srgb, adobeRgb);
        var report = new
        {
            Runtime = new
            {
                Framework = Environment.Version.ToString(),
                Os = Environment.OSVersion.ToString(),
                Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                SkiaSharp = typeof(SKColorSpace).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            },
            ProfileDirectory = Path.GetFullPath(profileDirectory),
            MacMainDisplay = OperatingSystem.IsMacOS()
                ? MacDisplayProfileProbe.ReadMainDisplay()
                : (MacDisplayProfileResult?)null,
            GeneratedProfiles = generated,
            Profiles = profiles,
            Transforms = transforms,
            OfficialIccTransforms = officialTransforms,
            SimulatedDestinationsDiffer = transforms.SrgbToDisplayP3 != transforms.SrgbToAdobeRgb,
            SourcePixelUnchanged = sourceBefore == input,
            ColorPickerReferenceSrgbInvariantByOwnership = true,
            HistogramSourceDomainInvariantByOwnership = true,
            Benchmarks = benchmarks,
            MemoryObservations = memory,
        };
        Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        return profiles.Any(profile => profile.Inspection.IsValid) ? 0 : 2;
    }

    public static void WriteAvaloniaEvidence(
        AvaloniaTargetEvidence? target,
        string? platformHandleDescriptor,
        WindowsDisplayProfileResult windows)
    {
        var safeWindows = OperatingSystem.IsWindows()
            ? new
            {
                windows.Available,
                windows.MonitorDevice,
                ProfileFileName = windows.ProfilePath is null ? null : Path.GetFileName(windows.ProfilePath),
                ProfileByteCount = windows.ProfileBytes?.Length,
                windows.Inspection,
                windows.AdvancedColorSupported,
                windows.AdvancedColorEnabled,
                windows.BitsPerColorChannel,
                windows.Detail,
            }
            : null;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Target = target,
            PlatformHandleDescriptor = platformHandleDescriptor,
            Windows = safeWindows,
        }, JsonOptions));
    }

    private static object WriteSyntheticProfile(string directory, string filename, SKColorSpace colorSpace)
    {
        var bytes = SkiaColorTransformProbe.TrySerialize(colorSpace);
        if (bytes is null)
        {
            return new
            {
                Filename = filename,
                Bytes = 0,
                Sha256 = (string?)null,
                Generator = "SkiaSharp 3.119.4 SKColorSpace.ToProfile returned no serializable bytes",
            };
        }

        var path = Path.Combine(directory, filename);
        File.WriteAllBytes(path, bytes);
        return new
        {
            Filename = filename,
            Bytes = bytes.Length,
            Sha256 = (string?)DisplayProfileIdentity.FromBytes(bytes).Sha256,
            Generator = "SkiaSharp 3.119.4 SKColorSpace.ToProfile",
        };
    }

    private static object? ReadOfficialProfileTransforms(string directory)
    {
        var srgbPath = Path.Combine(directory, "sRGB2014.icc");
        var displayP3Path = Path.Combine(directory, "Display-P3.icc");
        if (!File.Exists(srgbPath) || !File.Exists(displayP3Path))
        {
            return null;
        }

        using var srgb = SKColorSpace.CreateIcc(File.ReadAllBytes(srgbPath));
        using var displayP3 = SKColorSpace.CreateIcc(File.ReadAllBytes(displayP3Path));
        if (srgb is null || displayP3 is null)
        {
            return null;
        }

        var patches = new[]
        {
            new ProbePixel(196, 83, 41, 255),
            new ProbePixel(64, 180, 140, 255),
            new ProbePixel(32, 128, 240, 255),
        };
        return new
        {
            DisplayP3ToSrgb = patches.Select(pixel => new
            {
                Input = pixel,
                Output = SkiaColorTransformProbe.TransformPixel(pixel, displayP3, srgb),
            }).ToArray(),
            SrgbToDisplayP3 = patches.Select(pixel => new
            {
                Input = pixel,
                Output = SkiaColorTransformProbe.TransformPixel(pixel, srgb, displayP3),
            }).ToArray(),
        };
    }

    private static ProfileParseEvidence ReadProfile(string path)
    {
        var watch = Stopwatch.StartNew();
        var bytes = File.ReadAllBytes(path);
        var inspection = IccProfileInspector.Inspect(bytes);
        string skiaResult;
        try
        {
            using var colorSpace = SKColorSpace.CreateIcc(bytes);
            skiaResult = colorSpace is null
                ? "rejected"
                : colorSpace.IsSrgb ? "parsed:sRGB" : "parsed:non-sRGB";
        }
        catch (ArgumentException)
        {
            skiaResult = "rejected:ArgumentException";
        }

        watch.Stop();
        return new ProfileParseEvidence(
            Path.GetFileName(path),
            inspection,
            skiaResult,
            watch.Elapsed.TotalMicroseconds);
    }
}
