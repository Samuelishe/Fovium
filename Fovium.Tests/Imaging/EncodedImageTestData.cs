using System.Buffers.Binary;
using System.Text;
using SkiaSharp;

namespace Fovium.Tests.Imaging;

internal static class EncodedImageTestData
{
    public static byte[] Create(
        SKEncodedImageFormat format,
        int width = 12,
        int height = 8,
        bool withAlpha = false)
    {
        using var bitmap = CreateBitmap(width, height, withAlpha);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    public static byte[] CreateWebp(
        SKWebpEncoderCompression compression,
        bool withAlpha = false,
        int width = 12,
        int height = 8)
    {
        using var bitmap = CreateBitmap(width, height, withAlpha);
        using var pixmap = bitmap.PeekPixels();
        using var data = pixmap.Encode(new SKWebpEncoderOptions(compression, 90))
            ?? throw new InvalidOperationException("Skia could not encode the WebP test fixture.");
        return data.ToArray();
    }

    public static byte[] CreateAnimatedWebp(int width = 4, int height = 3)
    {
        var first = ExtractWebpImageChunks(CreateWebp(
            SKWebpEncoderCompression.Lossless,
            width: width,
            height: height));
        var second = ExtractWebpImageChunks(CreateWebp(
            SKWebpEncoderCompression.Lossless,
            width: width,
            height: height));
        using var body = new MemoryStream();
        body.Write("WEBP"u8);
        var extended = new byte[10];
        extended[0] = 0x02;
        WriteUInt24(extended.AsSpan(4, 3), width - 1);
        WriteUInt24(extended.AsSpan(7, 3), height - 1);
        WriteChunk(body, "VP8X", extended);
        WriteChunk(body, "ANIM", new byte[6]);
        WriteChunk(body, "ANMF", CreateAnimationFrame(first, width, height));
        WriteChunk(body, "ANMF", CreateAnimationFrame(second, width, height));

        using var result = new MemoryStream();
        result.Write("RIFF"u8);
        var bodyBytes = body.ToArray();
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(size, checked((uint)bodyBytes.Length));
        result.Write(size);
        result.Write(bodyBytes);
        return result.ToArray();
    }

    public static byte[] CreateOrientedWebp(int width = 12, int height = 8)
        => CreateWebpWithExif(CreateExifOrientationPayload(6), width, height);

    public static byte[] CreateWebpWithExif(byte[] exifPayload, int width = 12, int height = 8)
    {
        ArgumentNullException.ThrowIfNull(exifPayload);
        var imageChunks = ExtractWebpImageChunks(CreateWebp(
            SKWebpEncoderCompression.Lossless,
            width: width,
            height: height));
        using var body = new MemoryStream();
        body.Write("WEBP"u8);
        var extended = new byte[10];
        extended[0] = 0x08;
        WriteUInt24(extended.AsSpan(4, 3), width - 1);
        WriteUInt24(extended.AsSpan(7, 3), height - 1);
        WriteChunk(body, "VP8X", extended);
        body.Write(imageChunks);
        WriteChunk(body, "EXIF", exifPayload);

        using var result = new MemoryStream();
        result.Write("RIFF"u8);
        var bodyBytes = body.ToArray();
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(size, checked((uint)bodyBytes.Length));
        result.Write(size);
        result.Write(bodyBytes);
        return result.ToArray();
    }

    private static SKBitmap CreateBitmap(int width, int height, bool withAlpha)
    {
        var bitmap = new SKBitmap(new SKImageInfo(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        var pixels = bitmap.GetPixelSpan();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var alpha = withAlpha && x < width / 2 ? (byte)0 : byte.MaxValue;
                var offset = (y * bitmap.RowBytes) + (x * 4);
                pixels[offset] = alpha == 0 ? (byte)0 : (byte)(70 + (((x + y) * 11) % 150));
                pixels[offset + 1] = alpha == 0 ? (byte)0 : (byte)(40 + ((y * 17) % 180));
                pixels[offset + 2] = alpha == 0 ? (byte)0 : (byte)(25 + ((x * 13) % 200));
                pixels[offset + 3] = alpha;
            }
        }

        return bitmap;
    }

    private static byte[] ExtractWebpImageChunks(byte[] webp) => webp[12..];

    private static byte[] CreateAnimationFrame(byte[] imageChunks, int width, int height)
    {
        var frame = new byte[16 + imageChunks.Length];
        WriteUInt24(frame.AsSpan(6, 3), width - 1);
        WriteUInt24(frame.AsSpan(9, 3), height - 1);
        WriteUInt24(frame.AsSpan(12, 3), 100);
        imageChunks.CopyTo(frame, 16);
        return frame;
    }

    private static byte[] CreateExifOrientationPayload(ushort orientation)
    {
        var payload = new byte[32];
        "Exif\0\0"u8.CopyTo(payload);
        var tiff = payload.AsSpan(6);
        tiff[0] = (byte)'I';
        tiff[1] = (byte)'I';
        BinaryPrimitives.WriteUInt16LittleEndian(tiff[2..], 42);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff[4..], 8);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff[8..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff[10..], 0x0112);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff[12..], 3);
        BinaryPrimitives.WriteUInt32LittleEndian(tiff[14..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(tiff[18..], orientation);
        return payload;
    }

    private static void WriteChunk(Stream destination, string fourCharacterCode, byte[] payload)
    {
        destination.Write(Encoding.ASCII.GetBytes(fourCharacterCode));
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(size, checked((uint)payload.Length));
        destination.Write(size);
        destination.Write(payload);
        if ((payload.Length & 1) != 0)
        {
            destination.WriteByte(0);
        }
    }

    private static void WriteUInt24(Span<byte> destination, int value)
    {
        destination[0] = (byte)value;
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)(value >> 16);
    }
}
