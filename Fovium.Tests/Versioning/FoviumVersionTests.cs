using Fovium.Application;

namespace Fovium.Tests.Versioning;

public sealed class FoviumVersionTests
{
    [Fact]
    public void InformationalVersionPreservesFourDigitBuild()
    {
        Assert.Equal("0.1.2.0002", FoviumVersion.Display);
    }

    [Fact]
    public void AssemblyAndFileVersionsUseNumericBuild()
    {
        Assert.Equal("0.1.2.2", FoviumVersion.AssemblyNumeric);
        Assert.Equal("0.1.2.2", FoviumVersion.FileNumeric);
    }
}
