using System.Text;
using Fovium.ColorManagementProbe;

namespace Fovium.Tests.ColorManagementProbe;

public sealed class DisplayProfileIdentityTests
{
    [Fact]
    public void IdenticalBytesHaveCanonicalContentIdentity()
    {
        var first = DisplayProfileIdentity.FromBytes(Encoding.ASCII.GetBytes("abc"));
        var second = DisplayProfileIdentity.FromBytes("abc"u8);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(
            "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD",
            first.Sha256);
    }

    [Fact]
    public void OneByteDifferenceChangesIdentity()
    {
        var original = DisplayProfileIdentity.FromBytes("abc"u8);
        var changed = DisplayProfileIdentity.FromBytes("abd"u8);

        Assert.NotEqual(original, changed);
        Assert.NotEqual(original.Sha256, changed.Sha256);
    }
}
