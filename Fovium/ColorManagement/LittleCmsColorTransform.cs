namespace Fovium.ColorManagement;

internal interface IColorTransform : IDisposable
{
    void Transform(ReadOnlySpan<byte> inputBgraUnpremultiplied, Span<byte> outputBgraUnpremultiplied);
}

internal interface IColorTransformEngine : IDisposable
{
    bool IsAvailable { get; }

    string RuntimeDetail { get; }

    IColorTransform CreateTransform(ReadOnlyMemory<byte> destinationProfile);
}

internal sealed class LittleCmsColorTransformEngine : IColorTransformEngine
{
    private LittleCmsRuntime? _runtime;

    public LittleCmsColorTransformEngine(LittleCmsRuntimeAvailability availability)
    {
        _runtime = availability.Runtime;
        RuntimeDetail = availability.Detail;
    }

    public bool IsAvailable => Volatile.Read(ref _runtime) is not null;

    public string RuntimeDetail { get; }

    public IColorTransform CreateTransform(ReadOnlyMemory<byte> destinationProfile)
    {
        var runtime = Volatile.Read(ref _runtime)
            ?? throw new InvalidOperationException("The app-local Little CMS runtime is unavailable.");
        return new LittleCmsColorTransform(runtime, destinationProfile);
    }

    public void Dispose() => Interlocked.Exchange(ref _runtime, null)?.Dispose();
}

internal sealed class LittleCmsColorTransform : IColorTransform
{
    internal const uint RelativeColorimetricIntent = 1;
    internal const uint CopyAlphaFlags = 0x04000000;
    internal const uint TypeBgra8 = 279705;
    internal const uint DisplayClassSignature = 0x6D6E7472;
    internal const uint RgbSignature = 0x52474220;
    internal const uint XyzSignature = 0x58595A20;
    internal const uint LabSignature = 0x4C616220;

    private readonly LittleCmsNativeApi _api;
    private nint _context;
    private nint _sourceProfile;
    private nint _destinationProfile;
    private nint _transform;

    public LittleCmsColorTransform(LittleCmsRuntime runtime, ReadOnlyMemory<byte> destinationProfile)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (destinationProfile.IsEmpty)
        {
            throw new InvalidDataException("The destination ICC profile is empty.");
        }

        _api = runtime.Api;
        try
        {
            _context = _api.CreateContext(0, 0);
            if (_context == 0)
            {
                throw new InvalidOperationException("Little CMS could not create an isolated context.");
            }

            _sourceProfile = _api.CreateSrgbProfile(_context);
            unsafe
            {
                fixed (byte* bytes = destinationProfile.Span)
                {
                    _destinationProfile = _api.OpenProfileFromMemory(
                        _context,
                        (nint)bytes,
                        checked((uint)destinationProfile.Length));
                }
            }

            if (_sourceProfile == 0 || _destinationProfile == 0)
            {
                throw new InvalidDataException("Little CMS rejected the destination ICC profile.");
            }

            var deviceClass = _api.GetDeviceClass(_destinationProfile);
            var colorSpace = _api.GetColorSpace(_destinationProfile);
            var pcs = _api.GetPcs(_destinationProfile);
            if (deviceClass != DisplayClassSignature || colorSpace != RgbSignature ||
                pcs is not (XyzSignature or LabSignature))
            {
                throw new InvalidDataException("Little CMS reported a non-RGB-display destination profile.");
            }

            _transform = _api.CreateTransform(
                _context,
                _sourceProfile,
                TypeBgra8,
                _destinationProfile,
                TypeBgra8,
                RelativeColorimetricIntent,
                CopyAlphaFlags);
            if (_transform == 0)
            {
                throw new InvalidDataException("Little CMS could not create the destination transform.");
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public unsafe void Transform(
        ReadOnlySpan<byte> inputBgraUnpremultiplied,
        Span<byte> outputBgraUnpremultiplied)
    {
        ObjectDisposedException.ThrowIf(_transform == 0, this);
        if (inputBgraUnpremultiplied.Length == 0 ||
            inputBgraUnpremultiplied.Length % 4 != 0 ||
            outputBgraUnpremultiplied.Length < inputBgraUnpremultiplied.Length)
        {
            throw new ArgumentException("Little CMS BGRA buffers must contain the same nonempty whole-pixel length.");
        }

        fixed (byte* input = inputBgraUnpremultiplied)
        fixed (byte* output = outputBgraUnpremultiplied)
        {
            _api.DoTransform(
                _transform,
                (nint)input,
                (nint)output,
                checked((uint)(inputBgraUnpremultiplied.Length / 4)));
        }
    }

    public void Dispose()
    {
        var transform = Interlocked.Exchange(ref _transform, 0);
        if (transform != 0)
        {
            _api.DeleteTransform(transform);
        }

        var destination = Interlocked.Exchange(ref _destinationProfile, 0);
        if (destination != 0)
        {
            _api.CloseProfile(destination);
        }

        var source = Interlocked.Exchange(ref _sourceProfile, 0);
        if (source != 0)
        {
            _api.CloseProfile(source);
        }

        var context = Interlocked.Exchange(ref _context, 0);
        if (context != 0)
        {
            _api.DeleteContext(context);
        }
    }
}
