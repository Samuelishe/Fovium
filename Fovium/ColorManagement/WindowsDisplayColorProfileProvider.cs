using System.Runtime.InteropServices;
using System.Text;

namespace Fovium.ColorManagement;

internal interface IDisplayColorProfileProvider
{
    DisplayProfileResolution ResolveForWindow(
        nint windowHandle,
        nint currentMonitorHandle,
        bool forceProfileRefresh = false);
}

internal sealed class UnsupportedDisplayColorProfileProvider : IDisplayColorProfileProvider
{
    public DisplayProfileResolution ResolveForWindow(
        nint windowHandle,
        nint currentMonitorHandle,
        bool forceProfileRefresh = false) =>
        new(
            MonitorColorState.PlatformUnsupported,
            null,
            "Physical-monitor color management is not implemented on this platform.");
}

internal sealed class WindowsDisplayColorProfileProvider : IDisplayColorProfileProvider
{
    private const int IcmOn = 2;
    private const uint QdcOnlyActivePaths = 2;
    private const int DisplayConfigDeviceInfoGetSourceName = 1;
    private const int DisplayConfigDeviceInfoGetAdvancedColorInfo = 9;
    private const uint MaximumMonitorCount = 128;
    private const uint MaximumDisplayModeCount = 512;
    private DisplayProfileResolution? _cachedResolution;
    private nint _cachedMonitorHandle;

    public DisplayProfileResolution ResolveForWindow(
        nint windowHandle,
        nint currentMonitorHandle,
        bool forceProfileRefresh = false)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new DisplayProfileResolution(
                MonitorColorState.PlatformUnsupported,
                null,
                "Windows display-profile APIs are unavailable on this platform.");
        }

        if (windowHandle == 0 || !GetWindowRect(windowHandle, out var windowRect))
        {
            return Unavailable("The viewer window bounds could not be resolved.");
        }

        var nativeMonitors = EnumerateMonitors();
        var selected = ActiveDisplayMonitorSelector.Select(
            windowRect.ToDesktopRect(),
            nativeMonitors.Select(monitor => monitor.Monitor).ToArray(),
            currentMonitorHandle);
        if (selected is null)
        {
            selected = nativeMonitors
                .Select(candidate => candidate.Monitor)
                .FirstOrDefault(candidate => candidate.Handle == currentMonitorHandle);
            if (selected.Value.Handle == 0)
            {
                return Unavailable("The viewer does not currently intersect a display monitor.");
            }
        }

        var monitor = nativeMonitors.Single(candidate => candidate.Monitor.Handle == selected.Value.Handle);
        var advancedColor = ReadAdvancedColorState(monitor.DeviceName);
        if (!advancedColor.IsKnown)
        {
            return new DisplayProfileResolution(
                MonitorColorState.UnsupportedDisplayMode,
                null,
                "Windows could not confirm an ordinary SDR output mode.");
        }

        if (advancedColor.Enabled)
        {
            return new DisplayProfileResolution(
                MonitorColorState.UnsupportedDisplayMode,
                null,
                "Advanced Color is enabled; legacy SDR output remains reference sRGB.",
                true,
                advancedColor.BitsPerColorChannel);
        }

        if (!forceProfileRefresh &&
            monitor.Monitor.Handle == _cachedMonitorHandle &&
            _cachedResolution is { } cached)
        {
            return cached;
        }

        var hdc = CreateDCW("DISPLAY", monitor.DeviceName, null, 0);
        if (hdc == 0)
        {
            return Unavailable("A display device context could not be created.", false, advancedColor.BitsPerColorChannel);
        }

        try
        {
            _ = SetICMMode(hdc, IcmOn);
            var profilePath = GetIcmProfilePath(hdc);
            if (profilePath is null)
            {
                return Unavailable(
                    "Windows did not provide an assigned display profile.",
                    false,
                    advancedColor.BitsPerColorChannel);
            }

            byte[] profileBytes;
            try
            {
                var file = new FileInfo(profilePath);
                if (!file.Exists)
                {
                    return Unavailable("The assigned display profile is unavailable.", false, advancedColor.BitsPerColorChannel);
                }

                if (file.Length is <= 0 or > DisplayIccProfileAdmissionPolicy.MaximumProfileBytes)
                {
                    return Invalid("The assigned display profile failed bounded file admission.", advancedColor.BitsPerColorChannel);
                }

                profileBytes = File.ReadAllBytes(profilePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return Unavailable(
                    $"The assigned display profile could not be read ({exception.GetType().Name}).",
                    false,
                    advancedColor.BitsPerColorChannel);
            }

            var admission = DisplayIccProfileAdmissionPolicy.Inspect(profileBytes);
            if (!admission.IsValid || admission.Summary is null)
            {
                return Invalid(admission.Detail, advancedColor.BitsPerColorChannel);
            }

            var identity = DisplayProfileIdentity.FromBytes(profileBytes, false);
            var profile = new DisplayProfile(
                profileBytes,
                identity,
                admission.Summary.Value.Description,
                admission.Summary.Value.HasVcgt,
                monitor.Monitor.StableId,
                monitor.Monitor.Handle);
            var resolution = new DisplayProfileResolution(
                MonitorColorState.Managed,
                profile,
                "Valid Windows SDR display profile resolved.",
                false,
                advancedColor.BitsPerColorChannel);
            _cachedMonitorHandle = monitor.Monitor.Handle;
            _cachedResolution = resolution;
            return resolution;
        }
        finally
        {
            _ = DeleteDC(hdc);
        }
    }

    private static NativeMonitor[] EnumerateMonitors()
    {
        var result = new List<NativeMonitor>();
        var callback = new MonitorEnumProc((monitorHandle, _, _, _) =>
        {
            var info = MonitorInfoEx.Create();
            if (GetMonitorInfoW(monitorHandle, ref info))
            {
                var deviceName = info.DeviceName.TrimEnd('\0');
                result.Add(new NativeMonitor(
                    new DisplayMonitor(monitorHandle, deviceName, info.Monitor.ToDesktopRect()),
                    deviceName));
            }

            return true;
        });
        _ = EnumDisplayMonitors(0, 0, callback, 0);
        GC.KeepAlive(callback);
        return result.ToArray();
    }

    private static string? GetIcmProfilePath(nint hdc)
    {
        uint characterCount = 0;
        _ = GetICMProfileW(hdc, ref characterCount, null);
        if (characterCount is 0 or > 32768)
        {
            return null;
        }

        var buffer = new StringBuilder(checked((int)characterCount));
        return GetICMProfileW(hdc, ref characterCount, buffer)
            ? buffer.ToString()
            : null;
    }

    private static AdvancedColorState ReadAdvancedColorState(string deviceName)
    {
        if (GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out var pathCount, out var modeCount) != 0 ||
            pathCount is 0 or > MaximumMonitorCount || modeCount > MaximumDisplayModeCount)
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
                !string.Equals(
                    sourceName.ViewGdiDeviceName.TrimEnd('\0'),
                    deviceName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var info = DisplayConfigGetAdvancedColorInfo.Create(path.TargetInfo.AdapterId, path.TargetInfo.Id);
            if (QueryAdvancedColorInfo(ref info) != 0)
            {
                return default;
            }

            return new AdvancedColorState(
                true,
                (info.Value & 0x2) != 0,
                info.BitsPerColorChannel);
        }

        return default;
    }

    private static DisplayProfileResolution Unavailable(
        string detail,
        bool? advancedColorEnabled = null,
        uint? bitsPerColorChannel = null) =>
        new(
            MonitorColorState.DestinationUnavailable,
            null,
            detail,
            advancedColorEnabled,
            bitsPerColorChannel);

    private static DisplayProfileResolution Invalid(string detail, uint? bitsPerColorChannel) =>
        new(
            MonitorColorState.InvalidDestinationProfile,
            null,
            detail,
            false,
            bitsPerColorChannel);

    private readonly record struct NativeMonitor(DisplayMonitor Monitor, string DeviceName);
    private readonly record struct AdvancedColorState(bool IsKnown, bool Enabled, uint? BitsPerColorChannel);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool MonitorEnumProc(nint monitor, nint hdc, nint rect, nint data);

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

        public DesktopRect ToDesktopRect() => new(Left, Top, Right - Left, Bottom - Top);
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
    private struct DisplayConfigModeInfo;

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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(nint hdc, nint clipRect, MonitorEnumProc callback, nint data);

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
