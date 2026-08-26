using System.Buffers.Binary;

namespace Fovium.Imaging;

internal enum IsoBmffImageFamily
{
    Unknown,
    Heif,
    Avif,
}

internal enum IsoBmffProbeKind
{
    NotRecognized,
    StillImage,
    Sequence,
    Malformed,
}

internal readonly record struct IsoBmffFileType(
    IsoBmffProbeKind Kind,
    IsoBmffImageFamily Family,
    string MajorBrand,
    IReadOnlySet<string> Brands)
{
    public static IsoBmffFileType NotRecognized { get; } =
        new(IsoBmffProbeKind.NotRecognized, IsoBmffImageFamily.Unknown, string.Empty, new HashSet<string>());

    public static IsoBmffFileType Malformed { get; } =
        new(IsoBmffProbeKind.Malformed, IsoBmffImageFamily.Unknown, string.Empty, new HashSet<string>());
}

internal static class IsoBmffFileTypeProbe
{
    public const int MaximumProbeBytes = 4096;

    private static readonly HashSet<string> HeifStillBrands =
        new(StringComparer.Ordinal) { "heic", "heix", "heim", "heis", "hevc", "hevx" };

    private static readonly HashSet<string> HeifSequenceBrands =
        new(StringComparer.Ordinal) { "hevc", "hevx", "hevm", "hevs", "msf1" };

    private static readonly HashSet<string> AvifStillBrands =
        new(StringComparer.Ordinal) { "avif" };

    private static readonly HashSet<string> AvifSequenceBrands =
        new(StringComparer.Ordinal) { "avis" };

    private static readonly HashSet<string> GenericImageBrands =
        new(StringComparer.Ordinal) { "mif1", "mif2", "mif3", "miaf", "1pic" };

    public static IsoBmffFileType Probe(ReadOnlySpan<byte> encoded)
    {
        var data = encoded[..Math.Min(encoded.Length, MaximumProbeBytes)];
        var offset = 0;
        while (offset <= data.Length - 8)
        {
            var boxSize32 = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            var boxType = ReadFourCc(data.Slice(offset + 4, 4));
            var headerSize = 8;
            ulong boxSize = boxSize32;

            if (boxSize32 == 1)
            {
                if (offset > data.Length - 16)
                {
                    return boxType == "ftyp" ? IsoBmffFileType.Malformed : IsoBmffFileType.NotRecognized;
                }

                boxSize = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(offset + 8, 8));
                headerSize = 16;
            }
            else if (boxSize32 == 0)
            {
                boxSize = (ulong)(data.Length - offset);
            }

            if (boxSize < (ulong)headerSize || boxSize > int.MaxValue)
            {
                return boxType == "ftyp" ? IsoBmffFileType.Malformed : IsoBmffFileType.NotRecognized;
            }

            var boxEndLong = (long)offset + (long)boxSize;
            if (boxEndLong > data.Length)
            {
                return boxType == "ftyp" ? IsoBmffFileType.Malformed : IsoBmffFileType.NotRecognized;
            }

            var boxEnd = (int)boxEndLong;
            if (boxType == "ftyp")
            {
                return ParseFileType(data.Slice(offset + headerSize, boxEnd - offset - headerSize));
            }

            if (boxSize == 0)
            {
                break;
            }

            offset = boxEnd;
        }

        return IsoBmffFileType.NotRecognized;
    }

    private static IsoBmffFileType ParseFileType(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 8 || (payload.Length - 8) % 4 != 0)
        {
            return IsoBmffFileType.Malformed;
        }

        var majorBrand = ReadFourCc(payload[..4]);
        var brands = new HashSet<string>(StringComparer.Ordinal) { majorBrand };
        for (var offset = 8; offset <= payload.Length - 4; offset += 4)
        {
            brands.Add(ReadFourCc(payload.Slice(offset, 4)));
        }

        var hasHeif = brands.Overlaps(HeifStillBrands);
        var hasAvif = brands.Overlaps(AvifStillBrands) || brands.Overlaps(AvifSequenceBrands);
        var hasGeneric = brands.Overlaps(GenericImageBrands);
        if (!hasHeif && !hasAvif && !hasGeneric)
        {
            return IsoBmffFileType.NotRecognized;
        }

        var family = hasHeif == hasAvif
            ? IsoBmffImageFamily.Unknown
            : hasHeif
                ? IsoBmffImageFamily.Heif
                : IsoBmffImageFamily.Avif;
        var isSequence = brands.Overlaps(HeifSequenceBrands) || brands.Overlaps(AvifSequenceBrands);
        return new IsoBmffFileType(
            isSequence ? IsoBmffProbeKind.Sequence : IsoBmffProbeKind.StillImage,
            family,
            majorBrand,
            brands);
    }

    private static string ReadFourCc(ReadOnlySpan<byte> value) =>
        string.Create(4, value.ToArray(), static (destination, bytes) =>
        {
            for (var index = 0; index < destination.Length; index++)
            {
                destination[index] = (char)bytes[index];
            }
        });
}
