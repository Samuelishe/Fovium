using System.Buffers.Binary;
using System.Text;
using Fovium.Imaging;

namespace Fovium.Tests.Imaging;

public sealed class IsoBmffFileTypeProbeTests
{
    [Theory]
    [InlineData("heic", (int)IsoBmffProbeKind.StillImage, (int)IsoBmffImageFamily.Heif)]
    [InlineData("heix", (int)IsoBmffProbeKind.StillImage, (int)IsoBmffImageFamily.Heif)]
    [InlineData("avif", (int)IsoBmffProbeKind.StillImage, (int)IsoBmffImageFamily.Avif)]
    [InlineData("avis", (int)IsoBmffProbeKind.Sequence, (int)IsoBmffImageFamily.Avif)]
    [InlineData("hevc", (int)IsoBmffProbeKind.Sequence, (int)IsoBmffImageFamily.Heif)]
    [InlineData("mif1", (int)IsoBmffProbeKind.StillImage, (int)IsoBmffImageFamily.Unknown)]
    public void MajorBrandClassifiesStillSequenceAndContainerFamily(
        string majorBrand,
        int expectedKind,
        int expectedFamily)
    {
        var result = IsoBmffFileTypeProbe.Probe(CreateFileTypeBox(majorBrand));

        Assert.Equal((IsoBmffProbeKind)expectedKind, result.Kind);
        Assert.Equal((IsoBmffImageFamily)expectedFamily, result.Family);
        Assert.Equal(majorBrand, result.MajorBrand);
        Assert.Contains(majorBrand, result.Brands);
    }

    [Fact]
    public void CompatibleSequenceBrandRejectsAnOtherwiseStillMajorBrand()
    {
        var result = IsoBmffFileTypeProbe.Probe(CreateFileTypeBox("avif", "mif1", "avis"));

        Assert.Equal(IsoBmffProbeKind.Sequence, result.Kind);
        Assert.Equal(IsoBmffImageFamily.Avif, result.Family);
        Assert.Contains("avis", result.Brands);
    }

    [Fact]
    public void MixedHeifAndAvifBrandsRemainUnknownUntilNativePrimaryEvidence()
    {
        var result = IsoBmffFileTypeProbe.Probe(CreateFileTypeBox("mif1", "heic", "avif"));

        Assert.Equal(IsoBmffProbeKind.StillImage, result.Kind);
        Assert.Equal(IsoBmffImageFamily.Unknown, result.Family);
        Assert.Contains("heic", result.Brands);
        Assert.Contains("avif", result.Brands);
    }

    [Fact]
    public void FileTypeBoxMayFollowAValidBoundedLeadingBox()
    {
        var encoded = Concat(CreateBox("free", [1, 2, 3, 4]), CreateFileTypeBox("avif"));

        var result = IsoBmffFileTypeProbe.Probe(encoded);

        Assert.Equal(IsoBmffProbeKind.StillImage, result.Kind);
        Assert.Equal(IsoBmffImageFamily.Avif, result.Family);
    }

    [Fact]
    public void ExtendedSizeFileTypeBoxIsParsedWithinProbeBound()
    {
        var payload = CreateFileTypePayload("heic", "mif1");
        var encoded = new byte[16 + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(encoded, 1);
        Encoding.ASCII.GetBytes("ftyp").CopyTo(encoded, 4);
        BinaryPrimitives.WriteUInt64BigEndian(encoded.AsSpan(8), checked((ulong)encoded.Length));
        payload.CopyTo(encoded, 16);

        var result = IsoBmffFileTypeProbe.Probe(encoded);

        Assert.Equal(IsoBmffProbeKind.StillImage, result.Kind);
        Assert.Equal(IsoBmffImageFamily.Heif, result.Family);
    }

    [Theory]
    [MemberData(nameof(MalformedFileTypeBoxes))]
    public void HostileFileTypeLengthsAreMalformedWithoutReadingPastInput(byte[] encoded)
    {
        var result = IsoBmffFileTypeProbe.Probe(encoded);

        Assert.Equal(IsoBmffProbeKind.Malformed, result.Kind);
        Assert.Equal(IsoBmffImageFamily.Unknown, result.Family);
        Assert.Empty(result.Brands);
    }

    [Fact]
    public void UnrelatedOrBeyondProbeWindowContentIsNotRecognized()
    {
        var unrelated = CreateFileTypeBox("isom", "iso8");
        var beyondWindow = Concat(
            CreateBox("free", new byte[IsoBmffFileTypeProbe.MaximumProbeBytes - 8]),
            CreateFileTypeBox("avif"));

        var unrelatedResult = IsoBmffFileTypeProbe.Probe(unrelated);
        var beyondWindowResult = IsoBmffFileTypeProbe.Probe(beyondWindow);

        Assert.Equal(IsoBmffProbeKind.NotRecognized, unrelatedResult.Kind);
        Assert.Equal(IsoBmffProbeKind.NotRecognized, beyondWindowResult.Kind);
        Assert.Empty(unrelatedResult.Brands);
        Assert.Empty(beyondWindowResult.Brands);
    }

    public static TheoryData<byte[]> MalformedFileTypeBoxes()
    {
        var tooSmall = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(tooSmall, 4);
        Encoding.ASCII.GetBytes("ftyp").CopyTo(tooSmall, 4);

        var truncated = CreateFileTypeBox("avif");
        BinaryPrimitives.WriteUInt32BigEndian(truncated, checked((uint)truncated.Length + 16));

        var shortExtended = new byte[12];
        BinaryPrimitives.WriteUInt32BigEndian(shortExtended, 1);
        Encoding.ASCII.GetBytes("ftyp").CopyTo(shortExtended, 4);

        var oversizedExtended = new byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(oversizedExtended, 1);
        Encoding.ASCII.GetBytes("ftyp").CopyTo(oversizedExtended, 4);
        BinaryPrimitives.WriteUInt64BigEndian(oversizedExtended.AsSpan(8), (ulong)int.MaxValue + 1);

        var invalidPayload = CreateBox("ftyp", new byte[9]);
        return new TheoryData<byte[]> { tooSmall, truncated, shortExtended, oversizedExtended, invalidPayload };
    }

    internal static byte[] CreateFileTypeBox(string majorBrand, params string[] compatibleBrands) =>
        CreateBox("ftyp", CreateFileTypePayload(majorBrand, compatibleBrands));

    private static byte[] CreateFileTypePayload(string majorBrand, params string[] compatibleBrands)
    {
        if (majorBrand.Length != 4)
        {
            throw new ArgumentException("The major brand must contain exactly four characters.", nameof(majorBrand));
        }
        if (compatibleBrands.Any(brand => brand.Length != 4))
        {
            throw new ArgumentException("Every compatible brand must contain exactly four characters.", nameof(compatibleBrands));
        }
        var payload = new byte[8 + (compatibleBrands.Length * 4)];
        Encoding.ASCII.GetBytes(majorBrand).CopyTo(payload, 0);
        foreach (var (brand, index) in compatibleBrands.Select((brand, index) => (brand, index)))
        {
            Encoding.ASCII.GetBytes(brand).CopyTo(payload, 8 + (index * 4));
        }

        return payload;
    }

    private static byte[] CreateBox(string type, byte[] payload)
    {
        var result = new byte[8 + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(result, checked((uint)result.Length));
        Encoding.ASCII.GetBytes(type).CopyTo(result, 4);
        payload.CopyTo(result, 8);
        return result;
    }

    private static byte[] Concat(params byte[][] values)
    {
        var length = values.Sum(value => value.Length);
        var result = new byte[length];
        var offset = 0;
        foreach (var value in values)
        {
            value.CopyTo(result, offset);
            offset += value.Length;
        }

        return result;
    }
}
