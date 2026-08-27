using System.Buffers.Binary;
using Fovium.ColorManagement;

namespace Fovium.Tests.ColorManagement;

public sealed class DisplayIccProfileAdmissionTests
{
    [Fact]
    public void MinimalBoundedRgbDisplayHeaderIsAdmitted()
    {
        var bytes = CreateProfileHeader();

        var admission = DisplayIccProfileAdmissionPolicy.Inspect(bytes);

        Assert.True(admission.IsValid, admission.Detail);
        var summary = Assert.IsType<DisplayIccProfileSummary>(admission.Summary);
        Assert.Equal(132, summary.Size);
        Assert.Equal("mntr", summary.DeviceClass);
        Assert.Equal("RGB ", summary.ColorSpace);
        Assert.Equal("XYZ ", summary.Pcs);
    }

    [Theory]
    [MemberData(nameof(InvalidProfiles))]
    public void InvalidProfileIsRejected(byte[] bytes, string expectedDetail)
    {
        var admission = DisplayIccProfileAdmissionPolicy.Inspect(bytes);

        Assert.False(admission.IsValid);
        Assert.Null(admission.Summary);
        Assert.Contains(expectedDetail, admission.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProfileOverSixteenMebibytesIsRejectedBeforeParsing()
    {
        var bytes = new byte[DisplayIccProfileAdmissionPolicy.MaximumProfileBytes + 1];

        var admission = DisplayIccProfileAdmissionPolicy.Inspect(bytes);

        Assert.False(admission.IsValid);
        Assert.Contains("exceeds", admission.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExactSixteenMebibyteBoundaryIsAdmitted()
    {
        var bytes = new byte[DisplayIccProfileAdmissionPolicy.MaximumProfileBytes];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)bytes.Length);
        bytes[8] = 4;
        bytes[9] = 0x30;
        "mntr"u8.CopyTo(bytes.AsSpan(12, 4));
        "RGB "u8.CopyTo(bytes.AsSpan(16, 4));
        "XYZ "u8.CopyTo(bytes.AsSpan(20, 4));
        "acsp"u8.CopyTo(bytes.AsSpan(36, 4));

        var admission = DisplayIccProfileAdmissionPolicy.Inspect(bytes);

        Assert.True(admission.IsValid, admission.Detail);
        Assert.Equal(DisplayIccProfileAdmissionPolicy.MaximumProfileBytes, admission.Summary?.Size);
    }

    [Fact]
    public void TrailingContainerBytesAreRejectedRatherThanExcludedFromIdentity()
    {
        var bytes = CreateProfileHeader();
        Array.Resize(ref bytes, bytes.Length + 1);

        var admission = DisplayIccProfileAdmissionPolicy.Inspect(bytes);

        Assert.False(admission.IsValid);
        Assert.Contains("exactly match", admission.Detail, StringComparison.OrdinalIgnoreCase);
    }

    public static TheoryData<byte[], string> InvalidProfiles()
    {
        var truncated = CreateProfileHeader()[..64];
        var badSignature = CreateProfileHeader();
        "nope"u8.CopyTo(badSignature.AsSpan(36, 4));
        var impossibleSize = CreateProfileHeader();
        BinaryPrimitives.WriteUInt32BigEndian(impossibleSize, uint.MaxValue);
        var inputProfile = CreateProfileHeader();
        "scnr"u8.CopyTo(inputProfile.AsSpan(12, 4));
        var nonRgb = CreateProfileHeader();
        "GRAY"u8.CopyTo(nonRgb.AsSpan(16, 4));

        return new TheoryData<byte[], string>
        {
            { [], "empty" },
            { truncated, "shorter" },
            { badSignature, "magic" },
            { impossibleSize, "declared size" },
            { inputProfile, "display-device" },
            { nonRgb, "RGB" },
        };
    }

    internal static byte[] CreateProfileHeader()
    {
        var bytes = new byte[132];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)bytes.Length);
        bytes[8] = 4;
        bytes[9] = 0x30;
        "mntr"u8.CopyTo(bytes.AsSpan(12, 4));
        "RGB "u8.CopyTo(bytes.AsSpan(16, 4));
        "XYZ "u8.CopyTo(bytes.AsSpan(20, 4));
        "acsp"u8.CopyTo(bytes.AsSpan(36, 4));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(128, 4), 0);
        return bytes;
    }
}
