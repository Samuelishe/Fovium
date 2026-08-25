using System.Text;
using Fovium.Imaging;
using Fovium.Rendering;
using SkiaSharp;

namespace Fovium.Tests.Metadata;

internal static class MetadataTestImages
{
    public static byte[] CreateJpegWithExif(bool includeExposure = true)
    {
        var jpeg = CreateTinyJpeg();
        var payload = CreateExifPayload(includeExposure);
        var segmentLength = payload.Length + 2;
        var result = new byte[jpeg.Length + payload.Length + 4];
        result[0] = 0xFF;
        result[1] = 0xD8;
        result[2] = 0xFF;
        result[3] = 0xE1;
        result[4] = (byte)(segmentLength >> 8);
        result[5] = (byte)segmentLength;
        payload.CopyTo(result, 6);
        jpeg.AsSpan(2).CopyTo(result.AsSpan(6 + payload.Length));
        return result;
    }

    public static byte[] CreateJpegWithoutExif() => CreateTinyJpeg();

    public static byte[] CreateMalformedExifJpeg()
    {
        var jpeg = CreateTinyJpeg();
        byte[] payload = [.. Encoding.ASCII.GetBytes("Exif\0\0"), 0x49, 0x49, 0x2A];
        var result = new byte[jpeg.Length + payload.Length + 4];
        result[0] = 0xFF;
        result[1] = 0xD8;
        result[2] = 0xFF;
        result[3] = 0xE1;
        var segmentLength = payload.Length + 2;
        result[4] = (byte)(segmentLength >> 8);
        result[5] = (byte)segmentLength;
        payload.CopyTo(result, 6);
        jpeg.AsSpan(2).CopyTo(result.AsSpan(6 + payload.Length));
        return result;
    }

    public static DecodedImage CreateDecoded(
        byte[] encodedSource,
        string path,
        PixelSize? encodedSize = null,
        ExifOrientation orientation = ExifOrientation.Normal)
    {
        var size = encodedSize ?? new PixelSize(12, 8);
        var bitmap = new SKBitmap(new SKImageInfo(size.Width, size.Height));
        bitmap.Erase(SKColors.CornflowerBlue);
        var image = SKImage.FromBitmap(bitmap);
        var descriptor = new ImageDescriptor(
            path,
            "Jpeg",
            size,
            OrientationTransform.GetOrientedSize(size, orientation),
            orientation,
            1,
            SourceColorState.AssumedSrgb,
            false,
            "Bgra8888/Premul",
            encodedSource.LongLength + bitmap.ByteCount,
            encodedSource.LongLength + bitmap.ByteCount,
            TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.Zero);
        return new DecodedImage(encodedSource, descriptor, bitmap, image);
    }

    private static byte[] CreateTinyJpeg()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(2, 2));
        bitmap.Erase(SKColors.DarkOrange);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    private static byte[] CreateExifPayload(bool includeExposure)
    {
        var make = Ascii("TESTMAKE");
        var model = Ascii("TESTMODEL");
        var date = Ascii("2026:08:25 18:42:00");
        var lens = Ascii("TEST 85mm");
        var ifd0Count = 3;
        var ifd0End = 8 + 2 + (ifd0Count * 12) + 4;
        var makeOffset = ifd0End;
        var modelOffset = makeOffset + make.Length;
        var subIfdOffset = AlignEven(modelOffset + model.Length);
        var subIfdCount = includeExposure ? 6 : 2;
        var subIfdData = subIfdOffset + 2 + (subIfdCount * 12) + 4;
        var exposureOffset = subIfdData;
        var apertureOffset = exposureOffset + 8;
        var focalOffset = apertureOffset + 8;
        var dateOffset = focalOffset + 8;
        var lensOffset = dateOffset + date.Length;
        var length = lensOffset + lens.Length;
        var tiff = new byte[length];
        using var stream = new MemoryStream(tiff, writable: true);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write((byte)'I');
        writer.Write((byte)'I');
        writer.Write((ushort)42);
        writer.Write((uint)8);
        writer.Write((ushort)ifd0Count);
        WriteOffsetEntry(writer, 0x010F, 2, (uint)make.Length, (uint)makeOffset);
        WriteOffsetEntry(writer, 0x0110, 2, (uint)model.Length, (uint)modelOffset);
        WriteInlineUIntEntry(writer, 0x8769, (uint)subIfdOffset);
        writer.Write(0u);
        WriteAt(stream, makeOffset, make);
        WriteAt(stream, modelOffset, model);

        stream.Position = subIfdOffset;
        writer.Write((ushort)subIfdCount);
        if (includeExposure)
        {
            WriteOffsetEntry(writer, 0x829A, 5, 1, (uint)exposureOffset);
            WriteOffsetEntry(writer, 0x829D, 5, 1, (uint)apertureOffset);
            WriteInlineShortEntry(writer, 0x8827, 400);
            WriteOffsetEntry(writer, 0x9003, 2, (uint)date.Length, (uint)dateOffset);
            WriteOffsetEntry(writer, 0x920A, 5, 1, (uint)focalOffset);
            WriteOffsetEntry(writer, 0xA434, 2, (uint)lens.Length, (uint)lensOffset);
        }
        else
        {
            WriteOffsetEntry(writer, 0x9003, 2, (uint)date.Length, (uint)dateOffset);
            WriteOffsetEntry(writer, 0xA434, 2, (uint)lens.Length, (uint)lensOffset);
        }

        writer.Write(0u);
        if (includeExposure)
        {
            WriteRational(stream, exposureOffset, 1, 320);
            WriteRational(stream, apertureOffset, 2, 1);
            WriteRational(stream, focalOffset, 85, 1);
        }

        WriteAt(stream, dateOffset, date);
        WriteAt(stream, lensOffset, lens);
        return [.. Encoding.ASCII.GetBytes("Exif\0\0"), .. tiff];
    }

    private static byte[] Ascii(string value) => [.. Encoding.ASCII.GetBytes(value), 0];

    private static int AlignEven(int value) => (value + 1) & ~1;

    private static void WriteOffsetEntry(
        BinaryWriter writer,
        ushort tag,
        ushort type,
        uint count,
        uint offset)
    {
        writer.Write(tag);
        writer.Write(type);
        writer.Write(count);
        writer.Write(offset);
    }

    private static void WriteInlineUIntEntry(BinaryWriter writer, ushort tag, uint value)
    {
        writer.Write(tag);
        writer.Write((ushort)4);
        writer.Write(1u);
        writer.Write(value);
    }

    private static void WriteInlineShortEntry(BinaryWriter writer, ushort tag, ushort value)
    {
        writer.Write(tag);
        writer.Write((ushort)3);
        writer.Write(1u);
        writer.Write(value);
        writer.Write((ushort)0);
    }

    private static void WriteRational(Stream stream, int offset, uint numerator, uint denominator)
    {
        stream.Position = offset;
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(numerator);
        writer.Write(denominator);
    }

    private static void WriteAt(Stream stream, int offset, byte[] value)
    {
        stream.Position = offset;
        stream.Write(value);
    }
}
