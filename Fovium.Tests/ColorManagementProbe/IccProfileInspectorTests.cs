using System.Buffers.Binary;
using Fovium.ColorManagementProbe;
using SkiaSharp;

namespace Fovium.Tests.ColorManagementProbe;

public sealed class IccProfileInspectorTests
{
    [Fact]
    public void MinimalBoundedProfileReportsHeaderAndContentIdentity()
    {
        var bytes = IccTestData.CreateMinimalProfile();

        var inspection = IccProfileInspector.Inspect(bytes);

        var summary = Assert.IsType<IccProfileSummary>(inspection.Summary);
        Assert.True(inspection.IsValid);
        Assert.Equal(DisplayColorFallback.Managed, inspection.State);
        Assert.Equal(132, summary.DeclaredSize);
        Assert.Equal("4.3.0", summary.Version);
        Assert.Equal("mntr", summary.DeviceClass);
        Assert.Equal("RGB ", summary.ColorSpace);
        Assert.Equal("XYZ ", summary.Pcs);
        Assert.Equal(DisplayProfileIdentity.FromBytes(bytes), summary.Identity);
        Assert.False(summary.HasAToB);
        Assert.False(summary.HasBToA);
        Assert.False(summary.HasVcgt);
    }

    [Fact]
    public void ProjectAuthoredMatrixProfileIsAdmittedAndParsesInSkia()
    {
        var bytes = IccTestData.CreateMatrixRgbProfile();

        var inspection = IccProfileInspector.Inspect(bytes);
        using var parsed = SKColorSpace.CreateIcc(bytes);

        Assert.True(inspection.IsValid);
        Assert.NotNull(inspection.Summary);
        Assert.NotNull(parsed);
        Assert.Equal(DisplayProfileIdentity.FromBytes(bytes), inspection.Summary.Value.Identity);
        Assert.Equal("RGB ", inspection.Summary.Value.ColorSpace);
        Assert.Equal("XYZ ", inspection.Summary.Value.Pcs);
    }

    [Theory]
    [MemberData(nameof(MalformedProfiles))]
    public void EmptyAndMalformedProfilesAreInvalidWithoutAFalseSummary(byte[] bytes)
    {
        var inspection = IccProfileInspector.Inspect(bytes);

        Assert.False(inspection.IsValid);
        Assert.Equal(DisplayColorFallback.InvalidDestinationProfile, inspection.State);
        Assert.Null(inspection.Summary);
        Assert.NotEmpty(inspection.Detail);
    }

    [Fact]
    public void DeclaredSizeOutsideSignedRangeIsRejectedRecoverably()
    {
        var bytes = IccTestData.CreateMinimalProfile(declaredSize: uint.MaxValue);

        var inspection = IccProfileInspector.Inspect(bytes);

        Assert.False(inspection.IsValid);
        Assert.Equal(DisplayColorFallback.InvalidDestinationProfile, inspection.State);
        Assert.Null(inspection.Summary);
    }

    [Theory]
    [InlineData(12, "scnr")]
    [InlineData(16, "CMYK")]
    [InlineData(20, "RGB ")]
    public void NonDisplayNonRgbOrUnsupportedConnectionSpaceIsRejected(int offset, string signature)
    {
        var bytes = IccTestData.CreateMinimalProfile();
        System.Text.Encoding.ASCII.GetBytes(signature).CopyTo(bytes, offset);

        var inspection = IccProfileInspector.Inspect(bytes);

        Assert.False(inspection.IsValid);
        Assert.Equal(DisplayColorFallback.InvalidDestinationProfile, inspection.State);
        Assert.Null(inspection.Summary);
    }

    [Fact]
    public void ProfileIdentityCoversDeclaredPayloadRatherThanTrailingContainerBytes()
    {
        var first = IccTestData.CreateMinimalProfile(256, 132);
        var second = (byte[])first.Clone();
        first[^1] = 1;
        second[^1] = 2;

        var firstInspection = IccProfileInspector.Inspect(first);
        var secondInspection = IccProfileInspector.Inspect(second);

        Assert.True(firstInspection.IsValid);
        Assert.True(secondInspection.IsValid);
        Assert.Equal(firstInspection.Summary?.Identity, secondInspection.Summary?.Identity);
        Assert.NotEqual(DisplayProfileIdentity.FromBytes(first), DisplayProfileIdentity.FromBytes(second));
    }

    [Fact]
    public void ExactMaximumIsAdmittedButMaximumPlusOneIsRejected()
    {
        var exact = IccTestData.CreateMinimalProfile(IccProfileInspector.MaximumProfileBytes, 132);
        var oversized = new byte[IccProfileInspector.MaximumProfileBytes + 1];

        var admitted = IccProfileInspector.Inspect(exact);
        var rejected = IccProfileInspector.Inspect(oversized);

        Assert.True(admitted.IsValid);
        Assert.Equal(132, admitted.Summary?.DeclaredSize);
        Assert.False(rejected.IsValid);
        Assert.Equal(DisplayColorFallback.InvalidDestinationProfile, rejected.State);
        Assert.Null(rejected.Summary);
        Assert.Contains("exceeds", rejected.Detail, StringComparison.OrdinalIgnoreCase);
    }

    public static TheoryData<byte[]> MalformedProfiles()
    {
        var declaredTooLarge = IccTestData.CreateMinimalProfile(declaredSize: 133);
        var declaredTooSmall = IccTestData.CreateMinimalProfile(declaredSize: 127);
        var noSignature = IccTestData.CreateMinimalProfile(includeSignature: false);
        var truncated = IccTestData.CreateMinimalProfile(127, 127);
        var random = new byte[132];
        BinaryPrimitives.WriteUInt32BigEndian(random, 132);
        return
        [
            [],
            truncated,
            declaredTooLarge,
            declaredTooSmall,
            noSignature,
            random,
        ];
    }
}
