using Fovium.RenderProbe;

namespace Fovium.Tests.RenderProbe;

public sealed class ProbeImageFactoryTests
{
    [Fact]
    public async Task MalformedInputFailsAsRecoverableInvalidData()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.RenderProbe.Tests.");
        var path = Path.Combine(directory.FullName, "malformed.jpg");
        try
        {
            await File.WriteAllBytesAsync(path, [0xFF, 0xD8, 0x00, 0x01, 0x02]);

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => ProbeImageFactory.LoadFileAsync(path, CancellationToken.None));

            Assert.Contains("probe", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
