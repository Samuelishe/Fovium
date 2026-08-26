using System.Runtime.InteropServices;

namespace Fovium.Imaging;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct LibHeifError
{
    public readonly int Code;
    public readonly int Subcode;
    public readonly nint Message;

    public bool IsSuccess => Code == 0;

    public string Detail => Marshal.PtrToStringUTF8(Message) ?? $"libheif error {Code}/{Subcode}";
}

[StructLayout(LayoutKind.Sequential)]
internal struct LibHeifNclxProfile
{
    public byte Version;
    public int ColorPrimaries;
    public int TransferCharacteristics;
    public int MatrixCoefficients;
    public byte FullRangeFlag;
    public float RedX;
    public float RedY;
    public float GreenX;
    public float GreenY;
    public float BlueX;
    public float BlueY;
    public float WhiteX;
    public float WhiteY;
}

internal sealed class LibHeifRuntime : IDisposable
{
    public const string RequiredVersion = "1.23.1";
    private const int HevcCompressionFormat = 1;
    private const int Av1CompressionFormat = 4;

    private readonly List<nint> _libraryHandles;
    private bool _initialized;
    private bool _disposed;

    private LibHeifRuntime(
        string loadedLibraryPath,
        List<nint> libraryHandles,
        LibHeifNativeApi api,
        string version)
    {
        LoadedLibraryPath = loadedLibraryPath;
        _libraryHandles = libraryHandles;
        Api = api;
        Version = version;
        _initialized = true;
    }

    public string LoadedLibraryPath { get; }

    public string Version { get; }

    public LibHeifNativeApi Api { get; }

    public static LibHeifRuntime Load(LibHeifRuntimeLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        var handles = new List<nint>();
        try
        {
            foreach (var dependencyPath in location.DependencyPaths)
            {
                handles.Add(NativeLibrary.Load(dependencyPath));
            }

            var mainHandle = NativeLibrary.Load(location.MainLibraryPath);
            handles.Add(mainHandle);
            var api = new LibHeifNativeApi(mainHandle);
            var initError = api.Init(nint.Zero);
            if (!initError.IsSuccess)
            {
                throw new FileLoadException($"libheif initialization failed: {initError.Detail}");
            }

            var version = Marshal.PtrToStringUTF8(api.GetVersion()) ?? "unknown";
            if (!string.Equals(version, RequiredVersion, StringComparison.Ordinal))
            {
                api.Deinit();
                throw new FileLoadException(
                    $"Fovium requires libheif {RequiredVersion}, but the app-local library reports {version}.");
            }

            var hasHevcDecoder = api.HaveDecoderForFormat(HevcCompressionFormat) != 0;
            var hasAv1Decoder = api.HaveDecoderForFormat(Av1CompressionFormat) != 0;
            var hasHevcEncoder = api.HaveEncoderForFormat(HevcCompressionFormat) != 0;
            var hasAv1Encoder = api.HaveEncoderForFormat(Av1CompressionFormat) != 0;
            if (!hasHevcDecoder || !hasAv1Decoder || hasHevcEncoder || hasAv1Encoder)
            {
                api.Deinit();
                throw new FileLoadException(
                    "The Fovium-owned libheif runtime does not match the required decode-only contract " +
                    $"(HEVC decoder={hasHevcDecoder}, AV1 decoder={hasAv1Decoder}, " +
                    $"HEVC encoder={hasHevcEncoder}, AV1 encoder={hasAv1Encoder}).");
            }

            return new LibHeifRuntime(
                Path.GetFullPath(location.MainLibraryPath),
                handles,
                api,
                version);
        }
        catch
        {
            for (var index = handles.Count - 1; index >= 0; index--)
            {
                NativeLibrary.Free(handles[index]);
            }

            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_initialized)
        {
            Api.Deinit();
            _initialized = false;
        }

        for (var index = _libraryHandles.Count - 1; index >= 0; index--)
        {
            NativeLibrary.Free(_libraryHandles[index]);
        }

        _libraryHandles.Clear();
    }
}

internal sealed class LibHeifNativeApi
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate LibHeifError InitDelegate(nint parameters);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void DeinitDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate nint GetVersionDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int HaveCodecForFormatDelegate(int compressionFormat);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate nint ContextAllocDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void ContextFreeDelegate(nint context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void ContextSetMaximumImageSizeDelegate(nint context, int maximumWidth);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void ContextSetMaximumThreadsDelegate(nint context, int maximumThreads);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate LibHeifError ContextReadMemoryDelegate(
        nint context,
        nint data,
        nuint size,
        nint options);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int ContextGetTopLevelCountDelegate(nint context);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate LibHeifError ContextGetPrimaryHandleDelegate(nint context, out nint handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void ImageHandleReleaseDelegate(nint handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate uint ImageHandleGetItemIdDelegate(nint handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate uint ItemGetItemTypeDelegate(nint context, uint itemId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate nuint ContextGetItemReferencesDelegate(
        nint context,
        uint itemId,
        int index,
        out uint referenceType,
        out nint references);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void ReleaseItemReferencesDelegate(nint context, ref nint references);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int ImageHandleGetIntDelegate(nint handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate uint ImageHandleGetColorProfileTypeDelegate(nint handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate nuint ImageHandleGetRawProfileSizeDelegate(nint handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate LibHeifError ImageHandleGetRawProfileDelegate(nint handle, nint output);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate LibHeifError ImageHandleGetNclxDelegate(nint handle, out nint profile);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void NclxProfileFreeDelegate(nint profile);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate LibHeifError DecodeImageDelegate(
        nint handle,
        out nint image,
        int colorspace,
        int chroma,
        nint options);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate nint ImageGetPlaneReadonlyDelegate(nint image, int channel, out nuint stride);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int ImageIsPremultipliedAlphaDelegate(nint image);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void ImageReleaseDelegate(nint image);

    public LibHeifNativeApi(nint libraryHandle)
    {
        Init = Get<InitDelegate>(libraryHandle, "heif_init");
        Deinit = Get<DeinitDelegate>(libraryHandle, "heif_deinit");
        GetVersion = Get<GetVersionDelegate>(libraryHandle, "heif_get_version");
        HaveDecoderForFormat = Get<HaveCodecForFormatDelegate>(libraryHandle, "heif_have_decoder_for_format");
        HaveEncoderForFormat = Get<HaveCodecForFormatDelegate>(libraryHandle, "heif_have_encoder_for_format");
        ContextAlloc = Get<ContextAllocDelegate>(libraryHandle, "heif_context_alloc");
        ContextFree = Get<ContextFreeDelegate>(libraryHandle, "heif_context_free");
        ContextSetMaximumImageSize = Get<ContextSetMaximumImageSizeDelegate>(
            libraryHandle,
            "heif_context_set_maximum_image_size_limit");
        ContextSetMaximumThreads = Get<ContextSetMaximumThreadsDelegate>(
            libraryHandle,
            "heif_context_set_max_decoding_threads");
        ContextReadMemory = Get<ContextReadMemoryDelegate>(
            libraryHandle,
            "heif_context_read_from_memory_without_copy");
        ContextGetTopLevelCount = Get<ContextGetTopLevelCountDelegate>(
            libraryHandle,
            "heif_context_get_number_of_top_level_images");
        ContextGetPrimaryHandle = Get<ContextGetPrimaryHandleDelegate>(
            libraryHandle,
            "heif_context_get_primary_image_handle");
        ImageHandleRelease = Get<ImageHandleReleaseDelegate>(libraryHandle, "heif_image_handle_release");
        ImageHandleGetItemId = Get<ImageHandleGetItemIdDelegate>(libraryHandle, "heif_image_handle_get_item_id");
        ItemGetItemType = Get<ItemGetItemTypeDelegate>(libraryHandle, "heif_item_get_item_type");
        ContextGetItemReferences = Get<ContextGetItemReferencesDelegate>(
            libraryHandle,
            "heif_context_get_item_references");
        ReleaseItemReferences = Get<ReleaseItemReferencesDelegate>(
            libraryHandle,
            "heif_release_item_references");
        ImageHandleGetWidth = Get<ImageHandleGetIntDelegate>(libraryHandle, "heif_image_handle_get_width");
        ImageHandleGetHeight = Get<ImageHandleGetIntDelegate>(libraryHandle, "heif_image_handle_get_height");
        ImageHandleHasAlpha = Get<ImageHandleGetIntDelegate>(libraryHandle, "heif_image_handle_has_alpha_channel");
        ImageHandleGetLumaBits = Get<ImageHandleGetIntDelegate>(
            libraryHandle,
            "heif_image_handle_get_luma_bits_per_pixel");
        ImageHandleGetChromaBits = Get<ImageHandleGetIntDelegate>(
            libraryHandle,
            "heif_image_handle_get_chroma_bits_per_pixel");
        ImageHandleGetColorProfileType = Get<ImageHandleGetColorProfileTypeDelegate>(
            libraryHandle,
            "heif_image_handle_get_color_profile_type");
        ImageHandleGetRawProfileSize = Get<ImageHandleGetRawProfileSizeDelegate>(
            libraryHandle,
            "heif_image_handle_get_raw_color_profile_size");
        ImageHandleGetRawProfile = Get<ImageHandleGetRawProfileDelegate>(
            libraryHandle,
            "heif_image_handle_get_raw_color_profile");
        ImageHandleGetNclx = Get<ImageHandleGetNclxDelegate>(
            libraryHandle,
            "heif_image_handle_get_nclx_color_profile");
        NclxProfileFree = Get<NclxProfileFreeDelegate>(libraryHandle, "heif_nclx_color_profile_free");
        DecodeImage = Get<DecodeImageDelegate>(libraryHandle, "heif_decode_image");
        ImageGetPlaneReadonly = Get<ImageGetPlaneReadonlyDelegate>(libraryHandle, "heif_image_get_plane_readonly2");
        ImageIsPremultipliedAlpha = Get<ImageIsPremultipliedAlphaDelegate>(
            libraryHandle,
            "heif_image_is_premultiplied_alpha");
        ImageRelease = Get<ImageReleaseDelegate>(libraryHandle, "heif_image_release");
    }

    public InitDelegate Init { get; }
    public DeinitDelegate Deinit { get; }
    public GetVersionDelegate GetVersion { get; }
    public HaveCodecForFormatDelegate HaveDecoderForFormat { get; }
    public HaveCodecForFormatDelegate HaveEncoderForFormat { get; }
    public ContextAllocDelegate ContextAlloc { get; }
    public ContextFreeDelegate ContextFree { get; }
    public ContextSetMaximumImageSizeDelegate ContextSetMaximumImageSize { get; }
    public ContextSetMaximumThreadsDelegate ContextSetMaximumThreads { get; }
    public ContextReadMemoryDelegate ContextReadMemory { get; }
    public ContextGetTopLevelCountDelegate ContextGetTopLevelCount { get; }
    public ContextGetPrimaryHandleDelegate ContextGetPrimaryHandle { get; }
    public ImageHandleReleaseDelegate ImageHandleRelease { get; }
    public ImageHandleGetItemIdDelegate ImageHandleGetItemId { get; }
    public ItemGetItemTypeDelegate ItemGetItemType { get; }
    public ContextGetItemReferencesDelegate ContextGetItemReferences { get; }
    public ReleaseItemReferencesDelegate ReleaseItemReferences { get; }
    public ImageHandleGetIntDelegate ImageHandleGetWidth { get; }
    public ImageHandleGetIntDelegate ImageHandleGetHeight { get; }
    public ImageHandleGetIntDelegate ImageHandleHasAlpha { get; }
    public ImageHandleGetIntDelegate ImageHandleGetLumaBits { get; }
    public ImageHandleGetIntDelegate ImageHandleGetChromaBits { get; }
    public ImageHandleGetColorProfileTypeDelegate ImageHandleGetColorProfileType { get; }
    public ImageHandleGetRawProfileSizeDelegate ImageHandleGetRawProfileSize { get; }
    public ImageHandleGetRawProfileDelegate ImageHandleGetRawProfile { get; }
    public ImageHandleGetNclxDelegate ImageHandleGetNclx { get; }
    public NclxProfileFreeDelegate NclxProfileFree { get; }
    public DecodeImageDelegate DecodeImage { get; }
    public ImageGetPlaneReadonlyDelegate ImageGetPlaneReadonly { get; }
    public ImageIsPremultipliedAlphaDelegate ImageIsPremultipliedAlpha { get; }
    public ImageReleaseDelegate ImageRelease { get; }

    private static T Get<T>(nint libraryHandle, string exportName) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(libraryHandle, exportName));
}
