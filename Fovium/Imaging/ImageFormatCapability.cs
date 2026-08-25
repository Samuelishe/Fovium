using System.Collections.ObjectModel;
using SkiaSharp;

namespace Fovium.Imaging;

internal enum ImageFormatId
{
    Jpeg,
    Png,
    Webp,
}

internal enum ImageAlphaCapability
{
    NotApplicable,
    Supported,
}

internal enum ImageAnimationPolicy
{
    StaticOnly,
}

internal sealed class ImageFormatCapability
{
    public ImageFormatCapability(
        ImageFormatId id,
        string stableId,
        string displayName,
        IEnumerable<string> extensions,
        IEnumerable<string> mimeTypes,
        bool staticDecodeSupported,
        ImageAlphaCapability alphaCapability,
        ImageAnimationPolicy animationPolicy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Id = id;
        StableId = stableId;
        DisplayName = displayName;
        Extensions = Array.AsReadOnly(extensions.ToArray());
        MimeTypes = Array.AsReadOnly(mimeTypes.ToArray());
        StaticDecodeSupported = staticDecodeSupported;
        AlphaCapability = alphaCapability;
        AnimationPolicy = animationPolicy;
    }

    public ImageFormatId Id { get; }

    public string StableId { get; }

    public string DisplayName { get; }

    public ReadOnlyCollection<string> Extensions { get; }

    public ReadOnlyCollection<string> MimeTypes { get; }

    public bool StaticDecodeSupported { get; }

    public ImageAlphaCapability AlphaCapability { get; }

    public ImageAnimationPolicy AnimationPolicy { get; }
}

internal static class ImageFormatCapabilities
{
    private static readonly ReadOnlyCollection<ImageFormatCapability> Registered = Array.AsReadOnly(
        new[]
        {
            new ImageFormatCapability(
                ImageFormatId.Jpeg,
                "jpeg",
                "JPEG",
                [".jpg", ".jpeg"],
                ["image/jpeg"],
                staticDecodeSupported: true,
                ImageAlphaCapability.NotApplicable,
                ImageAnimationPolicy.StaticOnly),
            new ImageFormatCapability(
                ImageFormatId.Png,
                "png",
                "PNG",
                [".png"],
                ["image/png"],
                staticDecodeSupported: true,
                ImageAlphaCapability.Supported,
                ImageAnimationPolicy.StaticOnly),
            new ImageFormatCapability(
                ImageFormatId.Webp,
                "webp",
                "WEBP",
                [".webp"],
                ["image/webp"],
                staticDecodeSupported: true,
                ImageAlphaCapability.Supported,
                ImageAnimationPolicy.StaticOnly),
        });

    private static readonly IReadOnlyDictionary<ImageFormatId, ImageFormatCapability> ById =
        Registered.ToDictionary(capability => capability.Id);

    private static readonly IReadOnlyDictionary<string, ImageFormatCapability> ByExtension =
        Registered
            .SelectMany(capability => capability.Extensions.Select(extension => (extension, capability)))
            .ToDictionary(pair => pair.extension, pair => pair.capability, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<SKEncodedImageFormat, ImageFormatId> SkiaFormats =
        new Dictionary<SKEncodedImageFormat, ImageFormatId>
        {
            [SKEncodedImageFormat.Jpeg] = ImageFormatId.Jpeg,
            [SKEncodedImageFormat.Png] = ImageFormatId.Png,
            [SKEncodedImageFormat.Webp] = ImageFormatId.Webp,
        };

    public static IReadOnlyList<ImageFormatCapability> All => Registered;

    public static IReadOnlyList<string> CandidateExtensions { get; } = Array.AsReadOnly(
        Registered.SelectMany(capability => capability.Extensions).ToArray());

    public static IReadOnlyList<string> FilePickerPatterns { get; } = Array.AsReadOnly(
        Registered
            .SelectMany(capability => capability.Extensions)
            .Select(extension => $"*{extension}")
            .ToArray());

    public static IReadOnlyList<string> FilePickerMimeTypes { get; } = Array.AsReadOnly(
        Registered.SelectMany(capability => capability.MimeTypes).Distinct(StringComparer.Ordinal).ToArray());

    public static ImageFormatCapability Get(ImageFormatId id) => ById[id];

    public static bool IsCandidateExtension(string? extension) =>
        extension is not null && ByExtension.ContainsKey(extension);

    public static bool TryGetDetected(
        SKEncodedImageFormat detectedFormat,
        out ImageFormatCapability? capability)
    {
        if (SkiaFormats.TryGetValue(detectedFormat, out var id))
        {
            capability = Get(id);
            return true;
        }

        capability = null;
        return false;
    }

    public static bool SupportsFrameCount(ImageFormatCapability capability, int frameCount)
    {
        ArgumentNullException.ThrowIfNull(capability);
        return capability.StaticDecodeSupported &&
            capability.AnimationPolicy == ImageAnimationPolicy.StaticOnly &&
            frameCount == 1;
    }
}
