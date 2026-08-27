using System.Buffers.Binary;
using System.Text;

namespace Fovium.ColorManagement;

internal readonly record struct DisplayIccProfileSummary(
    int Size,
    string Version,
    string DeviceClass,
    string ColorSpace,
    string Pcs,
    string? Description,
    bool HasVcgt);

internal readonly record struct DisplayIccProfileAdmission(
    bool IsValid,
    DisplayIccProfileSummary? Summary,
    string Detail);

internal static class DisplayIccProfileAdmissionPolicy
{
    public const int MaximumProfileBytes = 16 * 1024 * 1024;
    private const int HeaderSize = 128;

    public static DisplayIccProfileAdmission Inspect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return Invalid("The destination profile is empty.");
        }

        if (bytes.Length > MaximumProfileBytes)
        {
            return Invalid($"The destination profile exceeds the {MaximumProfileBytes}-byte limit.");
        }

        if (bytes.Length < HeaderSize)
        {
            return Invalid("The destination profile is shorter than the ICC header.");
        }

        var declaredSizeValue = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        if (declaredSizeValue > int.MaxValue || declaredSizeValue != bytes.Length)
        {
            return Invalid("The ICC declared size does not exactly match the supplied bytes.");
        }

        if (!bytes.Slice(36, 4).SequenceEqual("acsp"u8))
        {
            return Invalid("The ICC magic signature is missing.");
        }

        var deviceClass = ReadSignature(bytes, 12);
        if (deviceClass != "mntr")
        {
            return Invalid("The assigned destination ICC is not a display-device profile.");
        }

        var colorSpace = ReadSignature(bytes, 16);
        if (colorSpace != "RGB ")
        {
            return Invalid("The assigned destination ICC is not an RGB profile.");
        }

        var pcs = ReadSignature(bytes, 20);
        if (pcs is not ("XYZ " or "Lab "))
        {
            return Invalid("The assigned destination ICC has an unsupported connection space.");
        }

        if (!TryReadTagTable(bytes, out var tags, out var tagDetail))
        {
            return Invalid(tagDetail);
        }

        var version = $"{bytes[8]}.{bytes[9] >> 4}.{bytes[9] & 0x0f}";
        return new DisplayIccProfileAdmission(
            true,
            new DisplayIccProfileSummary(
                bytes.Length,
                version,
                deviceClass,
                colorSpace,
                pcs,
                TryReadDescription(bytes, tags),
                tags.Any(tag => tag.Signature == "vcgt")),
            "Valid bounded RGB display ICC profile.");
    }

    private static bool TryReadTagTable(
        ReadOnlySpan<byte> bytes,
        out IccTagEntry[] tags,
        out string detail)
    {
        var count = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(HeaderSize, 4));
        if (count > 4096 || HeaderSize + 4L + count * 12L > bytes.Length)
        {
            tags = [];
            detail = "The ICC tag table is outside the supplied bytes.";
            return false;
        }

        tags = new IccTagEntry[checked((int)count)];
        for (var index = 0; index < tags.Length; index++)
        {
            var entryOffset = checked(HeaderSize + 4 + index * 12);
            var offset = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(entryOffset + 4, 4));
            var size = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(entryOffset + 8, 4));
            if (offset > int.MaxValue || size > int.MaxValue || offset + (ulong)size > (ulong)bytes.Length)
            {
                detail = "An ICC tag lies outside the supplied bytes.";
                tags = [];
                return false;
            }

            tags[index] = new IccTagEntry(ReadSignature(bytes, entryOffset), (int)offset, (int)size);
        }

        detail = string.Empty;
        return true;
    }

    private static string? TryReadDescription(ReadOnlySpan<byte> bytes, IReadOnlyList<IccTagEntry> tags)
    {
        var entry = tags.FirstOrDefault(tag => tag.Signature == "desc");
        if (entry.Size < 12)
        {
            return null;
        }

        var tag = bytes.Slice(entry.Offset, entry.Size);
        if (tag[..4].SequenceEqual("desc"u8))
        {
            var asciiLength = BinaryPrimitives.ReadUInt32BigEndian(tag.Slice(8, 4));
            return asciiLength is > 0 and <= int.MaxValue && 12UL + asciiLength <= (ulong)tag.Length
                ? Encoding.ASCII.GetString(tag.Slice(12, (int)asciiLength)).TrimEnd('\0').Trim()
                : null;
        }

        if (!tag[..4].SequenceEqual("mluc"u8) || tag.Length < 28)
        {
            return null;
        }

        var count = BinaryPrimitives.ReadUInt32BigEndian(tag.Slice(8, 4));
        var recordSize = BinaryPrimitives.ReadUInt32BigEndian(tag.Slice(12, 4));
        if (count == 0 || recordSize < 12 || 16UL + recordSize > (ulong)tag.Length)
        {
            return null;
        }

        var length = BinaryPrimitives.ReadUInt32BigEndian(tag.Slice(20, 4));
        var offset = BinaryPrimitives.ReadUInt32BigEndian(tag.Slice(24, 4));
        return (length & 1) == 0 && offset <= int.MaxValue && length <= int.MaxValue &&
               offset + (ulong)length <= (ulong)tag.Length
            ? Encoding.BigEndianUnicode.GetString(tag.Slice((int)offset, (int)length)).TrimEnd('\0').Trim()
            : null;
    }

    private static string ReadSignature(ReadOnlySpan<byte> bytes, int offset) =>
        Encoding.ASCII.GetString(bytes.Slice(offset, 4));

    private static DisplayIccProfileAdmission Invalid(string detail) => new(false, null, detail);

    private readonly record struct IccTagEntry(string Signature, int Offset, int Size);
}
