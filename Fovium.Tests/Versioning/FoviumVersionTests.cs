using Fovium.Application;

namespace Fovium.Tests.Versioning;

public sealed class FoviumVersionTests
{
    [Fact]
    public void InformationalVersionPreservesFourDigitBuild()
    {
        Assert.Equal("0.0.0.0013", FoviumVersion.Display);
    }

    [Fact]
    public void AssemblyAndFileVersionsUseNumericBuild()
    {
        Assert.Equal("0.0.0.13", FoviumVersion.AssemblyNumeric);
        Assert.Equal("0.0.0.13", FoviumVersion.FileNumeric);
    }
}
