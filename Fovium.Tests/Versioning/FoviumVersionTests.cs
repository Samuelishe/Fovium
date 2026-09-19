using Fovium.Application;

namespace Fovium.Tests.Versioning;

public sealed class FoviumVersionTests
{
    [Fact]
    public void InformationalVersionPreservesFourDigitBuild()
    {
        Assert.Equal("0.1.4.0003", FoviumVersion.Display);
    }

    [Fact]
    public void AssemblyAndFileVersionsUseNumericBuild()
    {
        Assert.Equal("0.1.4.3", FoviumVersion.AssemblyNumeric);
        Assert.Equal("0.1.4.3", FoviumVersion.FileNumeric);
    }
}