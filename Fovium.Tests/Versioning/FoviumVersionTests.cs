using Fovium.Application;

namespace Fovium.Tests.Versioning;

public sealed class FoviumVersionTests
{
    [Fact]
    public void InformationalVersionPreservesFourDigitBuild()
    {
        Assert.Equal("0.1.2.0000", FoviumVersion.Display);
    }

    [Fact]
    public void AssemblyAndFileVersionsUseNumericBuild()
    {
        Assert.Equal("0.1.2.0", FoviumVersion.AssemblyNumeric);
        Assert.Equal("0.1.2.0", FoviumVersion.FileNumeric);
    }
}
