using System.Buffers.Binary;
using BitMiracle.LibTiff.Classic;
using TiffOrientation = BitMiracle.LibTiff.Classic.Orientation;

namespace Fovium.Tests.Imaging;

internal static class TiffTestData
{
    public static readonly byte[] RgbPattern =
    [
        255, 0, 0,
        0, 255, 0,
        0, 0, 255,
        255, 255, 0,
        0, 255, 255,
        255, 0, 255,
    ];

    public static byte[] CreateRgb(
        Compression compression = Compression.NONE,
        bool bigEndian = false,
        bool tiled = false,
        TiffOrientation orientation = TiffOrientation.TOPLEFT,
        bool withInvalidIccProfile = false)
    {
        using var stream = new MemoryStream();
        using (var tiff = OpenWriter(stream, bigEndian))
        {
            SetCommonFields(tiff, 3, 2, 3, 8, Photometric.RGB, compression, orientation, tiled);
            if (withInvalidIccProfile)
            {
                byte[] profileBytes = [1, 2, 3, 4, 5, 6, 7, 8];
                tiff.SetField(TiffTag.ICCPROFILE, profileBytes.Length, profileBytes);
            }
            if (tiled)
            {
                var tile = new byte[16 * 16 * 3];
                for (var row = 0; row < 2; row++)
                {
                    RgbPattern.AsSpan(row * 9, 9).CopyTo(tile.AsSpan(row * 16 * 3));
                }

                Assert.True(tiff.WriteTile(tile, 0, 0, 0, 0) > 0);
            }
            else
            {
                Assert.True(tiff.WriteScanline(RgbPattern[..9], 0));
                Assert.True(tiff.WriteScanline(RgbPattern[9..], 1));
            }

            tiff.WriteDirectory();
        }

        return stream.ToArray();
    }

    public static byte[] CreateGray(Photometric photometric = Photometric.MINISBLACK)
    {
        using var stream = new MemoryStream();
        using (var tiff = OpenWriter(stream, false))
        {
            SetCommonFields(tiff, 3, 1, 1, 8, photometric, Compression.NONE, TiffOrientation.TOPLEFT, false);
            Assert.True(tiff.WriteScanline(new byte[] { 0, 127, 255 }, 0));
            tiff.WriteDirectory();
        }

        return stream.ToArray();
    }

    public static byte[] CreateRgba(bool associated, ExtraSample? declaredSample = null)
    {
        using var stream = new MemoryStream();
        using (var tiff = OpenWriter(stream, false))
        {
            SetCommonFields(tiff, 3, 1, 4, 8, Photometric.RGB, Compression.NONE, TiffOrientation.TOPLEFT, false);
            tiff.SetField(
                TiffTag.EXTRASAMPLES,
                1,
                new[]
                {
                    declaredSample ?? (associated ? ExtraSample.ASSOCALPHA : ExtraSample.UNASSALPHA),
                });
            var row = associated
                ? new byte[] { 255, 0, 0, 255, 0, 128, 0, 128, 0, 0, 0, 0 }
                : new byte[] { 255, 0, 0, 255, 0, 255, 0, 128, 0, 0, 255, 0 };
            Assert.True(tiff.WriteScanline(row, 0));
            tiff.WriteDirectory();
        }

        return stream.ToArray();
    }

    public static byte[] CreateUnsupported(
        int bitsPerSample = 16,
        SampleFormat sampleFormat = SampleFormat.UINT,
        Photometric photometric = Photometric.RGB,
        bool multiplePages = false)
    {
        using var stream = new MemoryStream();
        using (var tiff = OpenWriter(stream, false))
        {
            var pages = multiplePages ? 2 : 1;
            for (var page = 0; page < pages; page++)
            {
                var samples = photometric == Photometric.RGB ? 3 : 1;
                SetCommonFields(
                    tiff,
                    1,
                    1,
                    samples,
                    bitsPerSample,
                    photometric,
                    Compression.NONE,
                    TiffOrientation.TOPLEFT,
                    false);
                tiff.SetField(TiffTag.SAMPLEFORMAT, sampleFormat);
                var byteCount = Math.Max(1, (bitsPerSample / 8) * samples);
                Assert.True(tiff.WriteScanline(new byte[byteCount], 0));
                tiff.WriteDirectory();
            }
        }

        return stream.ToArray();
    }

    public static byte[] CreateIndependentUncompressedRgb(
        bool bigEndian,
        int width = 2,
        int height = 1)
    {
        const int entryCount = 9;
        const int ifdOffset = 8;
        const int bitsOffset = ifdOffset + 2 + (entryCount * 12) + 4;
        const int pixelOffset = bitsOffset + 6;
        var bytes = new byte[pixelOffset + 6];
        bytes[0] = bytes[1] = bigEndian ? (byte)'M' : (byte)'I';
        WriteUInt16(bytes.AsSpan(2), 42, bigEndian);
        WriteUInt32(bytes.AsSpan(4), ifdOffset, bigEndian);
        WriteUInt16(bytes.AsSpan(ifdOffset), entryCount, bigEndian);
        var entry = ifdOffset + 2;
        WriteEntry(bytes, ref entry, 256, 4, 1, checked((uint)width), bigEndian);
        WriteEntry(bytes, ref entry, 257, 4, 1, checked((uint)height), bigEndian);
        WriteEntry(bytes, ref entry, 258, 3, 3, bitsOffset, bigEndian);
        WriteEntry(bytes, ref entry, 259, 3, 1, 1, bigEndian);
        WriteEntry(bytes, ref entry, 262, 3, 1, 2, bigEndian);
        WriteEntry(bytes, ref entry, 273, 4, 1, pixelOffset, bigEndian);
        WriteEntry(bytes, ref entry, 277, 3, 1, 3, bigEndian);
        WriteEntry(bytes, ref entry, 278, 4, 1, checked((uint)height), bigEndian);
        WriteEntry(bytes, ref entry, 279, 4, 1, 6, bigEndian);
        WriteUInt16(bytes.AsSpan(bitsOffset), 8, bigEndian);
        WriteUInt16(bytes.AsSpan(bitsOffset + 2), 8, bigEndian);
        WriteUInt16(bytes.AsSpan(bitsOffset + 4), 8, bigEndian);
        new byte[] { 255, 0, 0, 0, 255, 0 }.CopyTo(bytes, pixelOffset);
        return bytes;
    }

    public static byte[] CreateBigTiffSignature(bool bigEndian) =>
        bigEndian
            ? [0x4D, 0x4D, 0x00, 0x2B, 0, 8, 0, 0]
            : [0x49, 0x49, 0x2B, 0x00, 8, 0, 0, 0];

    private static Tiff OpenWriter(Stream stream, bool bigEndian) =>
        Tiff.ClientOpen("test TIFF", bigEndian ? "wb" : "w", stream, new TiffStream())
        ?? throw new InvalidOperationException("Could not create TIFF fixture writer.");

    private static void SetCommonFields(
        Tiff tiff,
        int width,
        int height,
        int samples,
        int bits,
        Photometric photometric,
        Compression compression,
        TiffOrientation orientation,
        bool tiled)
    {
        tiff.SetField(TiffTag.IMAGEWIDTH, width);
        tiff.SetField(TiffTag.IMAGELENGTH, height);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, samples);
        tiff.SetField(TiffTag.BITSPERSAMPLE, bits);
        tiff.SetField(TiffTag.ORIENTATION, orientation);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.PHOTOMETRIC, photometric);
        tiff.SetField(TiffTag.COMPRESSION, compression);
        if (tiled)
        {
            tiff.SetField(TiffTag.TILEWIDTH, 16);
            tiff.SetField(TiffTag.TILELENGTH, 16);
        }
        else
        {
            tiff.SetField(TiffTag.ROWSPERSTRIP, height);
        }
    }

    private static void WriteEntry(
        byte[] bytes,
        ref int offset,
        ushort tag,
        ushort type,
        uint count,
        uint value,
        bool bigEndian)
    {
        WriteUInt16(bytes.AsSpan(offset), tag, bigEndian);
        WriteUInt16(bytes.AsSpan(offset + 2), type, bigEndian);
        WriteUInt32(bytes.AsSpan(offset + 4), checked((int)count), bigEndian);
        if (type == 3 && count == 1)
        {
            WriteUInt16(bytes.AsSpan(offset + 8), checked((ushort)value), bigEndian);
        }
        else
        {
            WriteUInt32(bytes.AsSpan(offset + 8), checked((int)value), bigEndian);
        }

        offset += 12;
    }

    private static void WriteUInt16(Span<byte> destination, int value, bool bigEndian)
    {
        if (bigEndian)
        {
            BinaryPrimitives.WriteUInt16BigEndian(destination, checked((ushort)value));
        }
        else
        {
            BinaryPrimitives.WriteUInt16LittleEndian(destination, checked((ushort)value));
        }
    }

    private static void WriteUInt32(Span<byte> destination, int value, bool bigEndian)
    {
        if (bigEndian)
        {
            BinaryPrimitives.WriteUInt32BigEndian(destination, checked((uint)value));
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, checked((uint)value));
        }
    }
}
