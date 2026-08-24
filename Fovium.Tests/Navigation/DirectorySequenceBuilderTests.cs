using Fovium.Navigation;

namespace Fovium.Tests.Navigation;

public sealed class DirectorySequenceBuilderTests
{
    [Fact]
    public void DirectorySnapshotIncludesOnlyJpegPngCandidatesAndNaturalSortsThem()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.DirectorySequence.Tests.");
        try
        {
            foreach (var name in new[] { "IMG_10.jpg", "IMG_2.PNG", "IMG_1.jpeg", "notes.txt" })
            {
                File.WriteAllText(Path.Combine(directory.FullName, name), "fixture");
            }

            var selected = Path.Combine(directory.FullName, "IMG_2.PNG");
            var sequence = new DirectorySequenceBuilder().Build(selected, CancellationToken.None);

            Assert.Equal(["IMG_1.jpeg", "IMG_2.PNG", "IMG_10.jpg"], sequence.Paths.Select(Path.GetFileName));
            Assert.Equal(1, sequence.InitialIndex);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void DirectUnusualExtensionRemainsCurrentWithoutBeingAdvertisedAsNeighborCandidate()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.DirectorySequence.Tests.");
        try
        {
            var selected = Path.Combine(directory.FullName, "recognized.data");
            File.WriteAllText(selected, "fixture");
            File.WriteAllText(Path.Combine(directory.FullName, "neighbor.jpg"), "fixture");

            var sequence = new DirectorySequenceBuilder().Build(selected, CancellationToken.None);

            Assert.Equal(2, sequence.Paths.Count);
            Assert.Equal("recognized.data", Path.GetFileName(sequence.Paths[sequence.InitialIndex]));
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
