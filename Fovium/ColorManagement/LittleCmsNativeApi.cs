using System.Runtime.InteropServices;

namespace Fovium.ColorManagement;

internal sealed class LittleCmsRuntime : IDisposable
{
    public const uint RequiredEncodedVersion = 2190;
    public const string RequiredVersion = "2.19";

    private nint _libraryHandle;

    private LittleCmsRuntime(string loadedLibraryPath, nint libraryHandle, LittleCmsNativeApi api)
    {
        LoadedLibraryPath = loadedLibraryPath;
        _libraryHandle = libraryHandle;
        Api = api;
    }

    public string LoadedLibraryPath { get; }

    public string Version => RequiredVersion;

    public LittleCmsNativeApi Api { get; }

    public static LittleCmsRuntime Load(LittleCmsRuntimeLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        var handle = NativeLibrary.Load(location.LibraryPath);
        try
        {
            var api = new LittleCmsNativeApi(handle);
            var encodedVersion = api.GetEncodedCmmVersion();
            if (encodedVersion != RequiredEncodedVersion)
            {
                throw new FileLoadException(
                    $"Fovium requires Little CMS {RequiredVersion} ({RequiredEncodedVersion}), " +
                    $"but the app-local library reports {encodedVersion}.");
            }

            return new LittleCmsRuntime(Path.GetFullPath(location.LibraryPath), handle, api);
        }
        catch
        {
            NativeLibrary.Free(handle);
            throw;
        }
    }

    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _libraryHandle, 0);
        if (handle != 0)
        {
            NativeLibrary.Free(handle);
        }
    }
}

internal sealed class LittleCmsNativeApi
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate uint GetEncodedCmmVersionDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate nint CreateContextDelegate(nint plugin, nint userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void DeleteContextDelegate(nint context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate nint CreateSrgbProfileDelegate(nint context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate nint OpenProfileFromMemoryDelegate(nint context, nint data, uint size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int CloseProfileDelegate(nint profile);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate uint GetProfileSignatureDelegate(nint profile);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate nint CreateTransformDelegate(
        nint context,
        nint inputProfile,
        uint inputFormat,
        nint outputProfile,
        uint outputFormat,
        uint intent,
        uint flags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void DoTransformDelegate(nint transform, nint input, nint output, uint size);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void DeleteTransformDelegate(nint transform);

    public LittleCmsNativeApi(nint libraryHandle)
    {
        GetEncodedCmmVersion = Get<GetEncodedCmmVersionDelegate>(libraryHandle, "cmsGetEncodedCMMversion");
        CreateContext = Get<CreateContextDelegate>(libraryHandle, "cmsCreateContext");
        DeleteContext = Get<DeleteContextDelegate>(libraryHandle, "cmsDeleteContext");
        CreateSrgbProfile = Get<CreateSrgbProfileDelegate>(libraryHandle, "cmsCreate_sRGBProfileTHR");
        OpenProfileFromMemory = Get<OpenProfileFromMemoryDelegate>(libraryHandle, "cmsOpenProfileFromMemTHR");
        CloseProfile = Get<CloseProfileDelegate>(libraryHandle, "cmsCloseProfile");
        GetDeviceClass = Get<GetProfileSignatureDelegate>(libraryHandle, "cmsGetDeviceClass");
        GetColorSpace = Get<GetProfileSignatureDelegate>(libraryHandle, "cmsGetColorSpace");
        GetPcs = Get<GetProfileSignatureDelegate>(libraryHandle, "cmsGetPCS");
        CreateTransform = Get<CreateTransformDelegate>(libraryHandle, "cmsCreateTransformTHR");
        DoTransform = Get<DoTransformDelegate>(libraryHandle, "cmsDoTransform");
        DeleteTransform = Get<DeleteTransformDelegate>(libraryHandle, "cmsDeleteTransform");
    }

    public GetEncodedCmmVersionDelegate GetEncodedCmmVersion { get; }
    public CreateContextDelegate CreateContext { get; }
    public DeleteContextDelegate DeleteContext { get; }
    public CreateSrgbProfileDelegate CreateSrgbProfile { get; }
    public OpenProfileFromMemoryDelegate OpenProfileFromMemory { get; }
    public CloseProfileDelegate CloseProfile { get; }
    public GetProfileSignatureDelegate GetDeviceClass { get; }
    public GetProfileSignatureDelegate GetColorSpace { get; }
    public GetProfileSignatureDelegate GetPcs { get; }
    public CreateTransformDelegate CreateTransform { get; }
    public DoTransformDelegate DoTransform { get; }
    public DeleteTransformDelegate DeleteTransform { get; }

    private static T Get<T>(nint libraryHandle, string exportName) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(libraryHandle, exportName));
}
