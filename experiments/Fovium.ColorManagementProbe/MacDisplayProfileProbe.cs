using System.Runtime.InteropServices;

namespace Fovium.ColorManagementProbe;

internal readonly record struct MacDisplayProfileResult(
    bool Available,
    uint DisplayId,
    byte[]? ProfileBytes,
    IccProfileInspection Inspection,
    string Detail);

internal static class MacDisplayProfileProbe
{
    private const string ColorSyncLibrary =
        "/System/Library/Frameworks/ApplicationServices.framework/Frameworks/ColorSync.framework/ColorSync";
    private const string CoreFoundationLibrary =
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string CoreGraphicsLibrary =
        "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    public static MacDisplayProfileResult ReadMainDisplay()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return Unavailable(0, "ColorSync display APIs are not available on this platform.");
        }

        return ReadDisplay(CGMainDisplayID());
    }

    public static MacDisplayProfileResult ReadDisplay(uint displayId)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return Unavailable(displayId, "ColorSync display APIs are not available on this platform.");
        }

        var profile = ColorSyncProfileCreateWithDisplayID(displayId);
        if (profile == 0)
        {
            return Unavailable(displayId, "ColorSync returned no profile for the display ID.");
        }

        try
        {
            var data = ColorSyncProfileCopyData(profile, out var error);
            try
            {
                if (data == 0)
                {
                    return Unavailable(displayId, "ColorSyncProfileCopyData returned no ICC bytes.");
                }

                var length = CFDataGetLength(data);
                if (length is <= 0 or > IccProfileInspector.MaximumProfileBytes || length > int.MaxValue)
                {
                    return Unavailable(displayId, $"ColorSync returned an inadmissible {length}-byte profile.");
                }

                var pointer = CFDataGetBytePtr(data);
                if (pointer == 0)
                {
                    return Unavailable(displayId, "ColorSync returned a profile with no byte pointer.");
                }

                var bytes = new byte[(int)length];
                Marshal.Copy(pointer, bytes, 0, bytes.Length);
                var inspection = IccProfileInspector.Inspect(bytes);
                return new MacDisplayProfileResult(
                    inspection.IsValid,
                    displayId,
                    bytes,
                    inspection,
                    inspection.Detail);
            }
            finally
            {
                if (data != 0)
                {
                    CFRelease(data);
                }

                if (error != 0)
                {
                    CFRelease(error);
                }
            }
        }
        finally
        {
            CFRelease(profile);
        }
    }

    private static MacDisplayProfileResult Unavailable(uint displayId, string detail) => new(
        false,
        displayId,
        null,
        new IccProfileInspection(DisplayColorFallback.DestinationUnavailable, null, detail),
        detail);

    [DllImport(ColorSyncLibrary)]
    private static extern nint ColorSyncProfileCreateWithDisplayID(uint displayId);

    [DllImport(ColorSyncLibrary)]
    private static extern nint ColorSyncProfileCopyData(nint profile, out nint error);

    [DllImport(CoreFoundationLibrary)]
    private static extern nint CFDataGetLength(nint data);

    [DllImport(CoreFoundationLibrary)]
    private static extern nint CFDataGetBytePtr(nint data);

    [DllImport(CoreFoundationLibrary)]
    private static extern void CFRelease(nint value);

    [DllImport(CoreGraphicsLibrary)]
    private static extern uint CGMainDisplayID();
}
