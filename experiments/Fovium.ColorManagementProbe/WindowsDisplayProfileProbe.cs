using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Fovium.ColorManagementProbe;

internal readonly record struct WindowsDisplayProfileResult(
    bool Available,
    string? MonitorDevice,
    string? ProfilePath,
    byte[]? ProfileBytes,
    IccProfileInspection Inspection,
    bool? AdvancedColorSupported,
    bool? AdvancedColorEnabled,
    uint? BitsPerColorChannel,
    string Detail);

internal static class WindowsDisplayProfileProbe
{
    private const uint MonitorDefaultToNearest = 2;
    private const int IcmOn = 2;
    private const uint QdcOnlyActivePaths = 2;
    private const int DisplayConfigDeviceInfoGetSourceName = 1;
    private const int DisplayConfigDeviceInfoGetAdvancedColorInfo = 9;

    public static WindowsDisplayProfileResult ReadForWindow(nint hwnd)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Unavailable("Windows display-profile APIs are not available on this platform.");
        }

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == 0)
        {
            return Unavailable("MonitorFromWindow returned no monitor.");
        }

        var monitorInfo = MonitorInfoEx.Create();
        if (!GetMonitorInfoW(monitor, ref monitorInfo))
        {
            return Unavailable($"GetMonitorInfoW failed with {Marshal.GetLastWin32Error()}.");
        }

        var deviceName = monitorInfo.DeviceName.TrimEnd('\0');
        var hdc = CreateDCW("DISPLAY", deviceName, null, 0);
        if (hdc == 0)
        {
            return Unavailable($"CreateDCW failed with {Marshal.GetLastWin32Error()}.", deviceName);
        }

        try
        {
            _ = SetICMMode(hdc, IcmOn);
            var profilePath = TryGetIcmProfile(hdc, out var profileError);
            var advanced = TryReadAdvancedColor(deviceName);
            if (profilePath is null)
            {
                return new WindowsDisplayProfileResult(
                    false,
                    deviceName,
                    null,
                    null,
                    new IccProfileInspection(
                        DisplayColorFallback.DestinationUnavailable,
                        null,
                        profileError),
                    advanced.Supported,
                    advanced.Enabled,
                    advanced.BitsPerChannel,
                    profileError);
            }

            byte[] bytes;
            try
            {
                var fileInfo = new FileInfo(profilePath);
                if (fileInfo.Length is <= 0 or > IccProfileInspector.MaximumProfileBytes)
                {
                    return new WindowsDisplayProfileResult(
                        false,
                        deviceName,
                        profilePath,
                        null,
                        new IccProfileInspection(
                            DisplayColorFallback.InvalidDestinationProfile,
                            null,
                            $"Assigned profile size {fileInfo.Length} is outside the admitted range."),
                        advanced.Supported,
                        advanced.Enabled,
                        advanced.BitsPerChannel,
                        "The assigned profile failed bounded file admission.");
                }

                bytes = File.ReadAllBytes(profilePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return Unavailable(
                    $"The assigned profile could not be read: {exception.GetType().Name}.",
                    deviceName,
                    profilePath,
                    advanced);
            }

            var inspection = IccProfileInspector.Inspect(bytes);
            return new WindowsDisplayProfileResult(
                inspection.IsValid,
                deviceName,
                profilePath,
                bytes,
                inspection,
                advanced.Supported,
                advanced.Enabled,
                advanced.BitsPerChannel,
                inspection.Detail);
        }
        finally
        {
            _ = DeleteDC(hdc);
        }
    }

    private static string? TryGetIcmProfile(nint hdc, out string error)
    {
        uint characters = 0;
        _ = GetICMProfileW(hdc, ref characters, null);
        if (characters is 0 or > 32768)
        {
            error = $"GetICMProfileW size query failed with {Marshal.GetLastWin32Error()}.";
            return null;
        }

        var buffer = new StringBuilder(checked((int)characters));
        if (!GetICMProfileW(hdc, ref characters, buffer))
        {
            error = $"GetICMProfileW failed with {Marshal.GetLastWin32Error()}.";
            return null;
        }

        error = string.Empty;
        return buffer.ToString();
    }

    private static (bool? Supported, bool? Enabled, uint? BitsPerChannel) TryReadAdvancedColor(string deviceName)
    {
        if (GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out var pathCount, out var modeCount) != 0 ||
            pathCount == 0 || pathCount > 128 || modeCount > 512)
        {
            return default;
        }

        var paths = new DisplayConfigPathInfo[pathCount];
        var modes = new DisplayConfigModeInfo[modeCount];
        if (QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, 0) != 0)
        {
            return default;
        }

        foreach (var path in paths.AsSpan(0, checked((int)pathCount)))
        {
            var sourceName = DisplayConfigSourceDeviceName.Create(path.SourceInfo.AdapterId, path.SourceInfo.Id);
            if (DisplayConfigGetSourceDeviceName(ref sourceName) != 0 ||
                !string.Equals(sourceName.ViewGdiDeviceName.TrimEnd('\0'), deviceName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var advanced = DisplayConfigGetAdvancedColorInfo.Create(path.TargetInfo.AdapterId, path.TargetInfo.Id);
            if (QueryAdvancedColorInfo(ref advanced) != 0)
            {
                return default;
            }

            return (
                (advanced.Value & 0x1) != 0,
                (advanced.Value & 0x2) != 0,
                advanced.BitsPerColorChannel);
        }

        return default;
    }

    private static WindowsDisplayProfileResult Unavailable(
        string detail,
        string? monitorDevice = null,
        string? profilePath = null,
        (bool? Supported, bool? Enabled, uint? BitsPerChannel) advanced = default) =>
        new(
            false,
            monitorDevice,
            profilePath,
            null,
            new IccProfileInspection(DisplayColorFallback.DestinationUnavailable, null, detail),
            advanced.Supported,
            advanced.Enabled,
            advanced.BitsPerChannel,
            detail);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        public static MonitorInfoEx Create() => new()
        {
            Size = checked((uint)Marshal.SizeOf<MonitorInfoEx>()),
            DeviceName = string.Empty,
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathSourceInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigRational
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathTargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public DisplayConfigRational RefreshRate;
        public uint ScanLineOrdering;
        [MarshalAs(UnmanagedType.Bool)]
        public bool TargetAvailable;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathInfo
    {
        public DisplayConfigPathSourceInfo SourceInfo;
        public DisplayConfigPathTargetInfo TargetInfo;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct DisplayConfigModeInfo
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigDeviceInfoHeader
    {
        public int Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayConfigSourceDeviceName
    {
        public DisplayConfigDeviceInfoHeader Header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ViewGdiDeviceName;

        public static DisplayConfigSourceDeviceName Create(Luid adapterId, uint id) => new()
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = DisplayConfigDeviceInfoGetSourceName,
                Size = checked((uint)Marshal.SizeOf<DisplayConfigSourceDeviceName>()),
                AdapterId = adapterId,
                Id = id,
            },
            ViewGdiDeviceName = string.Empty,
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigGetAdvancedColorInfo
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint Value;
        public uint ColorEncoding;
        public uint BitsPerColorChannel;

        public static DisplayConfigGetAdvancedColorInfo Create(Luid adapterId, uint id) => new()
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = DisplayConfigDeviceInfoGetAdvancedColorInfo,
                Size = checked((uint)Marshal.SizeOf<DisplayConfigGetAdvancedColorInfo>()),
                AdapterId = adapterId,
                Id = id,
            },
        };
    }

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfoW(nint monitor, ref MonitorInfoEx monitorInfo);

    [DllImport("gdi32.dll", EntryPoint = "CreateDCW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateDCW(string driver, string device, string? output, nint initData);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int SetICMMode(nint hdc, int mode);

    [DllImport("gdi32.dll", EntryPoint = "GetICMProfileW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetICMProfileW(nint hdc, ref uint size, StringBuilder? filename);

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint pathCount,
        [In, Out] DisplayConfigPathInfo[] paths,
        ref uint modeCount,
        [In, Out] DisplayConfigModeInfo[] modes,
        nint topologyId);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    private static extern int DisplayConfigGetSourceDeviceName(ref DisplayConfigSourceDeviceName requestPacket);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    private static extern int QueryAdvancedColorInfo(ref DisplayConfigGetAdvancedColorInfo requestPacket);
}
