using System.Security.Cryptography;
using Fovium.Imaging;
using Fovium.Rendering;

namespace Fovium.ColorManagement;

internal enum MonitorColorState
{
    Managed,
    Disabled,
    EngineUnavailable,
    DestinationUnavailable,
    InvalidDestinationProfile,
    UnsupportedSourceProfile,
    UnsupportedDisplayMode,
    PlatformUnsupported,
}

internal readonly record struct DesktopRect(int X, int Y, int Width, int Height)
{
    public long IntersectionArea(DesktopRect other)
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

internal readonly record struct DisplayMonitor(nint Handle, string StableId, DesktopRect Bounds);

internal static class ActiveDisplayMonitorSelector
{
    public static DisplayMonitor? Select(
        DesktopRect windowBounds,
        IReadOnlyList<DisplayMonitor> monitors,
        nint currentHandle = 0)
    {
        if (monitors.Count == 0)
        {
            return null;
        }

        var maximumArea = monitors.Max(monitor => windowBounds.IntersectionArea(monitor.Bounds));
        if (maximumArea == 0)
        {
            return null;
        }

        var tied = monitors
            .Where(monitor => windowBounds.IntersectionArea(monitor.Bounds) == maximumArea)
            .ToArray();
        var current = tied.FirstOrDefault(monitor => monitor.Handle == currentHandle);
        if (current.Handle != 0)
        {
            return current;
        }

        return tied.OrderBy(monitor => monitor.StableId, StringComparer.Ordinal).First();
    }
}

internal readonly record struct DisplayProfileIdentity(string ProfileSha256, bool AdvancedColorEnabled)
{
    public static DisplayProfileIdentity FromBytes(ReadOnlySpan<byte> bytes, bool advancedColorEnabled) =>
        new(Convert.ToHexString(SHA256.HashData(bytes)), advancedColorEnabled);

    public string DiagnosticPrefix => ProfileSha256[..Math.Min(16, ProfileSha256.Length)];
}

internal sealed record DisplayProfile(
    byte[] Bytes,
    DisplayProfileIdentity Identity,
    string? Description,
    bool HasVcgt,
    string MonitorIdentity,
    nint MonitorHandle);

internal readonly record struct DisplayProfileResolution(
    MonitorColorState State,
    DisplayProfile? Profile,
    string Detail,
    bool? AdvancedColorEnabled = null,
    uint? BitsPerColorChannel = null)
{
    public bool IsManaged => State == MonitorColorState.Managed && Profile is not null;
}

internal readonly record struct ManagedPhotoKey(
    long ImageIdentity,
    DisplayProfileIdentity DestinationIdentity,
    PixelSize EncodedSize,
    ExifOrientation Orientation);

internal static class ManagedPhotoRequestPolicy
{
    public static bool ShouldRequest(ManagedPhotoKey? requested, ManagedPhotoKey current) =>
        requested != current;
}

internal static class MonitorColorPolicy
{
    public static bool IsEligibleSource(SourceColorState state) => state is
        SourceColorState.AssumedSrgb or
        SourceColorState.NormalizedSrgb or
        SourceColorState.NormalizedSrgbFromNclx or
        SourceColorState.NormalizedNonSrgb;

    public static MonitorColorState Classify(
        bool enabled,
        bool platformSupported,
        bool engineAvailable,
        DisplayProfileResolution destination,
        SourceColorState sourceState)
    {
        if (!enabled)
        {
            return MonitorColorState.Disabled;
        }

        if (!platformSupported)
        {
            return MonitorColorState.PlatformUnsupported;
        }

        if (!engineAvailable)
        {
            return MonitorColorState.EngineUnavailable;
        }

        if (destination.State != MonitorColorState.Managed)
        {
            return destination.State;
        }

        return IsEligibleSource(sourceState)
            ? MonitorColorState.Managed
            : MonitorColorState.UnsupportedSourceProfile;
    }
}
