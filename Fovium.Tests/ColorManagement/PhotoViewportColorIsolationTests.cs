using Fovium.ColorManagement;
using Fovium.Viewer;

namespace Fovium.Tests.ColorManagement;

public sealed class PhotoViewportColorIsolationTests
{
    [Fact]
    public void DestinationChangesDoNotPublishSourceImageChanges()
    {
        var viewport = new PhotoViewportControl();
        var sourceChangeCount = 0;
        viewport.PresentedImageChanged += (_, _) => sourceChangeCount++;
        var firstBytes = DisplayIccProfileAdmissionTests.CreateProfileHeader();
        var secondBytes = firstBytes.ToArray();
        secondBytes[^1] = 1;

        viewport.SetDisplayProfile(CreateResolution(firstBytes));
        viewport.SetDisplayProfile(CreateResolution(secondBytes));

        Assert.Equal(0, sourceChangeCount);
    }

    private static DisplayProfileResolution CreateResolution(byte[] bytes) => new(
        MonitorColorState.Managed,
        new DisplayProfile(
            bytes,
            DisplayProfileIdentity.FromBytes(bytes, false),
            "Synthetic",
            false,
            "monitor",
            1),
        "managed",
        false,
        8);
}
