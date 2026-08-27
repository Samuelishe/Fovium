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

internal readonly record struct ManagedPhotoGeometry(
    RectD ViewportBounds,
    RectD PhotoDestination,
    double RenderScaling,
    bool ExactPixelSampling)
{
    public RectD VisiblePhotoBounds => Intersect(ViewportBounds, PhotoDestination);

    public bool IsValid =>
        ViewportBounds.Width > 0 &&
        ViewportBounds.Height > 0 &&
        PhotoDestination.Width > 0 &&
        PhotoDestination.Height > 0 &&
        RenderScaling > 0 &&
        VisiblePhotoBounds.Width > 0 &&
        VisiblePhotoBounds.Height > 0;

    private static RectD Intersect(RectD left, RectD right)
    {
        var x = Math.Max(left.X, right.X);
        var y = Math.Max(left.Y, right.Y);
        var edgeX = Math.Min(left.X + left.Width, right.X + right.Width);
        var edgeY = Math.Min(left.Y + left.Height, right.Y + right.Height);
        return edgeX <= x || edgeY <= y
            ? new RectD(0, 0, 0, 0)
            : new RectD(x, y, edgeX - x, edgeY - y);
    }
}

internal readonly record struct ManagedPhotoCoverage(
    RectD OrientedSourceRect,
    RectD RasterDestination,
    PixelSize RasterPixelSize,
    double OverscanFactor,
    bool OverscanCapped)
{
    public long RetainedBytes => checked((long)RasterPixelSize.Width * RasterPixelSize.Height * 4);
}

internal static class ManagedPhotoCoveragePlanner
{
    public const double PreferredOverscanFactor = 1.4;
    public const long MaximumOverscanRasterBytes = 48L * 1024 * 1024;

    public static ManagedPhotoCoverage Create(ManagedPhotoGeometry geometry, PixelSize orientedSize)
    {
        if (!geometry.IsValid || orientedSize.Width <= 0 || orientedSize.Height <= 0)
        {
            throw new ArgumentException("Managed photo coverage requires valid geometry and source size.");
        }

        var visible = geometry.VisiblePhotoBounds;
        var visiblePixelWidth = Math.Max(1, checked((int)Math.Ceiling(visible.Width * geometry.RenderScaling)));
        var visiblePixelHeight = Math.Max(1, checked((int)Math.Ceiling(visible.Height * geometry.RenderScaling)));
        var visibleBytes = checked((long)visiblePixelWidth * visiblePixelHeight * 4);
        var boundedFactor = visibleBytes >= MaximumOverscanRasterBytes
            ? 1
            : Math.Min(
                PreferredOverscanFactor,
                Math.Sqrt(MaximumOverscanRasterBytes / (double)visibleBytes));
        var rasterDestination = Intersect(
            ExpandAroundCenter(visible, boundedFactor),
            geometry.PhotoDestination);
        var pixelWidth = PixelLength(rasterDestination.Width, geometry.RenderScaling);
        var pixelHeight = PixelLength(rasterDestination.Height, geometry.RenderScaling);
        var retainedBytes = checked((long)pixelWidth * pixelHeight * 4);
        while (boundedFactor > 1 && retainedBytes > MaximumOverscanRasterBytes)
        {
            boundedFactor = Math.Max(
                1,
                boundedFactor * Math.Sqrt(MaximumOverscanRasterBytes / (double)retainedBytes) * 0.999);
            rasterDestination = Intersect(
                ExpandAroundCenter(visible, boundedFactor),
                geometry.PhotoDestination);
            pixelWidth = PixelLength(rasterDestination.Width, geometry.RenderScaling);
            pixelHeight = PixelLength(rasterDestination.Height, geometry.RenderScaling);
            retainedBytes = checked((long)pixelWidth * pixelHeight * 4);
        }
        var sourceRect = DestinationToSource(
            rasterDestination,
            geometry.PhotoDestination,
            orientedSize);
        var effectiveFactor = Math.Min(
            rasterDestination.Width / visible.Width,
            rasterDestination.Height / visible.Height);
        return new ManagedPhotoCoverage(
            sourceRect,
            rasterDestination,
            new PixelSize(pixelWidth, pixelHeight),
            effectiveFactor,
            boundedFactor < PreferredOverscanFactor);
    }

    public static RectD MapSourceToDestination(
        RectD sourceRect,
        RectD photoDestination,
        PixelSize orientedSize) => new(
            photoDestination.X + sourceRect.X / orientedSize.Width * photoDestination.Width,
            photoDestination.Y + sourceRect.Y / orientedSize.Height * photoDestination.Height,
            sourceRect.Width / orientedSize.Width * photoDestination.Width,
            sourceRect.Height / orientedSize.Height * photoDestination.Height);

    public static RectD VisibleSourceRect(ManagedPhotoGeometry geometry, PixelSize orientedSize) =>
        DestinationToSource(geometry.VisiblePhotoBounds, geometry.PhotoDestination, orientedSize);

    public static bool Contains(RectD container, RectD contained)
    {
        const double epsilon = 0.0001;
        return contained.X + epsilon >= container.X &&
            contained.Y + epsilon >= container.Y &&
            contained.X + contained.Width <= container.X + container.Width + epsilon &&
            contained.Y + contained.Height <= container.Y + container.Height + epsilon;
    }

    public static bool Intersects(RectD left, RectD right) =>
        left.X < right.X + right.Width &&
        left.X + left.Width > right.X &&
        left.Y < right.Y + right.Height &&
        left.Y + left.Height > right.Y;

    private static RectD DestinationToSource(
        RectD destinationRect,
        RectD photoDestination,
        PixelSize orientedSize) => new(
            (destinationRect.X - photoDestination.X) / photoDestination.Width * orientedSize.Width,
            (destinationRect.Y - photoDestination.Y) / photoDestination.Height * orientedSize.Height,
            destinationRect.Width / photoDestination.Width * orientedSize.Width,
            destinationRect.Height / photoDestination.Height * orientedSize.Height);

    private static RectD ExpandAroundCenter(RectD value, double factor)
    {
        var width = value.Width * factor;
        var height = value.Height * factor;
        return new RectD(
            value.X - (width - value.Width) / 2,
            value.Y - (height - value.Height) / 2,
            width,
            height);
    }

    private static int PixelLength(double logicalLength, double renderScaling) =>
        Math.Max(1, checked((int)Math.Ceiling(logicalLength * renderScaling)));

    private static RectD Intersect(RectD left, RectD right)
    {
        var x = Math.Max(left.X, right.X);
        var y = Math.Max(left.Y, right.Y);
        var edgeX = Math.Min(left.X + left.Width, right.X + right.Width);
        var edgeY = Math.Min(left.Y + left.Height, right.Y + right.Height);
        return edgeX <= x || edgeY <= y
            ? new RectD(0, 0, 0, 0)
            : new RectD(x, y, edgeX - x, edgeY - y);
    }
}

internal static class ManagedPhotoBaseCoveragePlanner
{
    public const long MaximumBaseRasterBytes = 32L * 1024 * 1024;

    public static ManagedPhotoCoverage Create(ManagedPhotoGeometry geometry, PixelSize orientedSize)
    {
        if (!geometry.IsValid || orientedSize.Width <= 0 || orientedSize.Height <= 0)
        {
            throw new ArgumentException("Managed photo base coverage requires valid geometry and source size.");
        }

        var maximumWidth = Math.Max(
            1,
            checked((int)Math.Floor(geometry.ViewportBounds.Width * geometry.RenderScaling)));
        var maximumHeight = Math.Max(
            1,
            checked((int)Math.Floor(geometry.ViewportBounds.Height * geometry.RenderScaling)));
        var sourceAspect = orientedSize.Width / (double)orientedSize.Height;
        var width = maximumWidth;
        var height = Math.Max(1, checked((int)Math.Floor(width / sourceAspect)));
        if (height > maximumHeight)
        {
            height = maximumHeight;
            width = Math.Max(1, checked((int)Math.Floor(height * sourceAspect)));
        }

        var capped = false;
        var retainedBytes = checked((long)width * height * 4);
        while (retainedBytes > MaximumBaseRasterBytes)
        {
            capped = true;
            var factor = Math.Sqrt(MaximumBaseRasterBytes / (double)retainedBytes) * 0.999;
            width = Math.Max(1, checked((int)Math.Floor(width * factor)));
            height = Math.Max(1, checked((int)Math.Floor(height * factor)));
            retainedBytes = checked((long)width * height * 4);
        }

        return new ManagedPhotoCoverage(
            new RectD(0, 0, orientedSize.Width, orientedSize.Height),
            geometry.PhotoDestination,
            new PixelSize(width, height),
            1,
            capped);
    }
}

internal readonly record struct ManagedPhotoKey(
    long ImageIdentity,
    DisplayProfileIdentity DestinationIdentity,
    PixelSize EncodedSize,
    ExifOrientation Orientation,
    ManagedPhotoGeometry Geometry);

internal readonly record struct ManagedPhotoPublicationDecision(
    bool SuppressLegacyPhoto,
    bool PhotoPresentationVisible,
    bool GeometryOnlyBlackFallback);

internal static class ManagedPhotoPublicationPolicy
{
    public static ManagedPhotoPublicationDecision Resolve(
        bool presentationAcquired,
        ManagedPhotoPendingReason pendingReason) => presentationAcquired
        ? new ManagedPhotoPublicationDecision(
            SuppressLegacyPhoto: false,
            PhotoPresentationVisible: true,
            GeometryOnlyBlackFallback: false)
        : new ManagedPhotoPublicationDecision(
            SuppressLegacyPhoto: true,
            PhotoPresentationVisible: false,
            GeometryOnlyBlackFallback:
                pendingReason is ManagedPhotoPendingReason.GeometryRefinementPending or
                    ManagedPhotoPendingReason.CoverageRefinementPending or
                    ManagedPhotoPendingReason.QualityRefinementPending);
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
