using Fovium.ColorManagement;

namespace Fovium.Tests.ColorManagement;

public sealed class ProductionDisplayProfileIdentityTests
{
    [Fact]
    public void PathIndependentIdentityUsesExactAdmittedBytesAndOutputMode()
    {
        byte[] bytes = [1, 2, 3, 4];

        var first = DisplayProfileIdentity.FromBytes(bytes, false);
        var sameBytes = DisplayProfileIdentity.FromBytes(bytes.ToArray(), false);
        var advancedColor = DisplayProfileIdentity.FromBytes(bytes, true);

        Assert.Equal(first, sameBytes);
        Assert.NotEqual(first, advancedColor);
        Assert.Equal(64, first.ProfileSha256.Length);
    }
}
