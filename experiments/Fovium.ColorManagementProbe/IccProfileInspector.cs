using System.Buffers.Binary;
using System.Text;

namespace Fovium.ColorManagementProbe;

internal static class IccProfileInspector
{
    public const int MaximumProfileBytes = 16 * 1024 * 1024;
    private const int HeaderSize = 128;

    public static IccProfileInspection Inspect(ReadOnlySpan<byte> bytes)
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
        if (declaredSizeValue > int.MaxValue)
        {
            return Invalid("The ICC declared size exceeds the supported range.");
        }

        var declaredSize = (int)declaredSizeValue;
        if (declaredSize < HeaderSize || declaredSize > bytes.Length)
        {
            return Invalid("The ICC declared size lies outside the supplied bytes.");
        }

        if (!bytes.Slice(36, 4).SequenceEqual("acsp"u8))
        {
            return Invalid("The ICC magic signature is missing.");
        }

        var deviceClass = ReadSignature(bytes, 12);
        if (!string.Equals(deviceClass, "mntr", StringComparison.Ordinal))
        {
            return Invalid("The assigned destination ICC is not a display-device profile.");
        }

        var colorSpace = ReadSignature(bytes, 16);
        if (!string.Equals(colorSpace, "RGB ", StringComparison.Ordinal))
        {
            return Invalid("The assigned destination ICC is not an RGB profile.");
        }

        var pcs = ReadSignature(bytes, 20);
        if (pcs is not ("XYZ " or "Lab "))
        {
            return Invalid("The assigned destination ICC has an unsupported connection space.");
        }

        var version = $"{bytes[8]}.{bytes[9] >> 4}.{bytes[9] & 0x0f}";
        var tags = ReadTagSignatures(bytes[..declaredSize]);
        var summary = new IccProfileSummary(
            declaredSize,
            version,
            deviceClass,
            colorSpace,
            pcs,
            TryReadDescription(bytes[..declaredSize]),
            tags.Any(tag => tag.StartsWith("A2B", StringComparison.Ordinal)),
            tags.Any(tag => tag.StartsWith("B2A", StringComparison.Ordinal)),
            tags.Contains("vcgt", StringComparer.Ordinal),
            DisplayProfileIdentity.FromBytes(bytes[..declaredSize]));
        return new IccProfileInspection(DisplayColorFallback.Managed, summary, "Valid bounded ICC profile.");
    }

    private static IccProfileInspection Invalid(string detail) =>
        new(DisplayColorFallback.InvalidDestinationProfile, null, detail);

    private static string ReadSignature(ReadOnlySpan<byte> bytes, int offset) =>
        Encoding.ASCII.GetString(bytes.Slice(offset, 4));

    private static string? TryReadDescription(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderSize + 4)
        {
            return null;
        }

        var count = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(HeaderSize, 4));
        if (count > 4096 || HeaderSize + 4L + (count * 12L) > bytes.Length)
        {
            return null;
        }

        for (var index = 0; index < count; index++)
        {
            var entryOffset = checked(HeaderSize + 4 + (index * 12));
            if (!bytes.Slice(entryOffset, 4).SequenceEqual("desc"u8))
            {
                continue;
            }

            var tagOffset = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(entryOffset + 4, 4));
            var tagSize = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(entryOffset + 8, 4));
            if (tagOffset > int.MaxValue || tagSize > int.MaxValue ||
                tagOffset + (ulong)tagSize > (ulong)bytes.Length || tagSize < 12)
            {
                return null;
            }

            return ReadDescriptionTag(bytes.Slice((int)tagOffset, (int)tagSize));
        }

        return null;
    }

    private static string[] ReadTagSignatures(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderSize + 4)
        {
            return [];
        }

        var count = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(HeaderSize, 4));
        if (count > 4096 || HeaderSize + 4L + (count * 12L) > bytes.Length)
        {
            return [];
        }

        var tagCount = checked((int)count);
        var tags = new string[tagCount];
        for (var index = 0; index < tagCount; index++)
        {
            tags[index] = ReadSignature(bytes, checked(HeaderSize + 4 + (index * 12)));
        }

        return tags;
    }

    private static string? ReadDescriptionTag(ReadOnlySpan<byte> tag)
    {
        if (tag[..4].SequenceEqual("desc"u8))
        {
            var asciiLength = BinaryPrimitives.ReadUInt32BigEndian(tag.Slice(8, 4));
            if (asciiLength is 0 or > int.MaxValue || 12UL + asciiLength > (ulong)tag.Length)
            {
                return null;
            }

            return Encoding.ASCII.GetString(tag.Slice(12, (int)asciiLength)).TrimEnd('\0').Trim();
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

        var unicodeLength = BinaryPrimitives.ReadUInt32BigEndian(tag.Slice(20, 4));
        var offset = BinaryPrimitives.ReadUInt32BigEndian(tag.Slice(24, 4));
        if ((unicodeLength & 1) != 0 || offset > int.MaxValue || unicodeLength > int.MaxValue ||
            offset + (ulong)unicodeLength > (ulong)tag.Length)
        {
            return null;
        }

        return Encoding.BigEndianUnicode.GetString(tag.Slice((int)offset, (int)unicodeLength)).TrimEnd('\0').Trim();
    }
}
