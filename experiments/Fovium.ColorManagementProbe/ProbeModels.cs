using System.Security.Cryptography;

namespace Fovium.ColorManagementProbe;

internal enum DisplayColorFallback
{
    Managed,
    DestinationUnavailable,
    InvalidDestinationProfile,
    UnsupportedSourceProfile,
    UnsupportedDisplayMode,
    PlatformUnsupported,
}

internal readonly record struct ProbeRect(int X, int Y, int Width, int Height)
{
    public long IntersectionArea(ProbeRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(checked(X + Width), checked(other.X + other.Width));
        var bottom = Math.Min(checked(Y + Height), checked(other.Y + other.Height));
        return right <= left || bottom <= top
            ? 0
            : checked((long)(right - left) * (bottom - top));
    }
}

internal readonly record struct ProbeMonitor(string StableId, ProbeRect Bounds);

internal static class ActiveMonitorSelector
{
    public static ProbeMonitor? Select(
        ProbeRect windowBounds,
        IReadOnlyList<ProbeMonitor> monitors,
        string? currentStableId = null)
    {
        if (monitors.Count == 0)
        {
            return null;
        }

        var maximumArea = monitors.Max(monitor => windowBounds.IntersectionArea(monitor.Bounds));
        var tied = monitors.Where(monitor => windowBounds.IntersectionArea(monitor.Bounds) == maximumArea).ToArray();
        if (currentStableId is not null)
        {
            var current = tied.FirstOrDefault(monitor =>
                string.Equals(monitor.StableId, currentStableId, StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(current.StableId))
            {
                return current;
            }
        }

        if (maximumArea == 0)
        {
            return null;
        }

        return tied
            .OrderBy(monitor => monitor.StableId, StringComparer.Ordinal)
            .First();
    }
}

internal static class ColorFallbackClassifier
{
    public static DisplayColorFallback Classify(
        bool platformSupported,
        bool displayModeSupported,
        IccProfileInspection? destination,
        bool sourceTransformSupported)
    {
        if (!platformSupported)
        {
            return DisplayColorFallback.PlatformUnsupported;
        }

        if (!displayModeSupported)
        {
            return DisplayColorFallback.UnsupportedDisplayMode;
        }

        if (destination is null || destination.Value.State == DisplayColorFallback.DestinationUnavailable)
        {
            return DisplayColorFallback.DestinationUnavailable;
        }

        if (!destination.Value.IsValid)
        {
            return DisplayColorFallback.InvalidDestinationProfile;
        }

        return sourceTransformSupported
            ? DisplayColorFallback.Managed
            : DisplayColorFallback.UnsupportedSourceProfile;
    }
}

internal readonly record struct DisplayProfileIdentity(string Sha256)
{
    public static DisplayProfileIdentity FromBytes(ReadOnlySpan<byte> profileBytes) =>
        new(Convert.ToHexString(SHA256.HashData(profileBytes)));
}

internal readonly record struct ColorTransformKey(
    string SourceColorIdentity,
    DisplayProfileIdentity DestinationIdentity,
    string PixelFormat,
    string AlphaSemantics,
    string RenderingIntent);

internal readonly record struct IccProfileSummary(
    int DeclaredSize,
    string Version,
    string DeviceClass,
    string ColorSpace,
    string Pcs,
    string? Description,
    bool HasAToB,
    bool HasBToA,
    bool HasVcgt,
    DisplayProfileIdentity Identity);

internal readonly record struct IccProfileInspection(
    DisplayColorFallback State,
    IccProfileSummary? Summary,
    string Detail)
{
    public bool IsValid => State == DisplayColorFallback.Managed && Summary is not null;
}
