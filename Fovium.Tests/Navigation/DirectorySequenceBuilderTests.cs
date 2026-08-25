using Fovium.Navigation;

namespace Fovium.Tests.Navigation;

public sealed class DirectorySequenceBuilderTests
{
    [Fact]
    public void DirectorySnapshotIncludesMixedSupportedCandidatesAndNaturalSortsThem()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.DirectorySequence.Tests.");
        try
        {
            foreach (var name in new[]
                     {
                         "photo10.WEBP",
                         "photo3.png",
                         "photo4.TIF",
                         "photo5.tiff",
                         "photo2.webp",
                         "photo1.jpg",
                         "notes.txt",
                     })
            {
                File.WriteAllText(Path.Combine(directory.FullName, name), "fixture");
            }

            var selected = Path.Combine(directory.FullName, "photo2.webp");
            var sequence = new DirectorySequenceBuilder().Build(selected, CancellationToken.None);

            Assert.Equal(
                ["photo1.jpg", "photo2.webp", "photo3.png", "photo4.TIF", "photo5.tiff", "photo10.WEBP"],
                sequence.Paths.Select(Path.GetFileName));
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
