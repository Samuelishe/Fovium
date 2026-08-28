using Fovium.Navigation;

namespace Fovium.Tests.Navigation;

public sealed class ImageSequenceTests
{
    [Fact]
    public void WrappedForwardEnumerationVisitsEveryOtherIndexExactlyOnce()
    {
        var sequence = new ImageSequence(["A.jpg", "B.jpg", "C.jpg"], 1);

        var indices = sequence.EnumerateAfter(1, NavigationDirection.Next, wrap: true).ToArray();

        Assert.Equal([2, 0], indices);
    }

    [Fact]
    public void SingleItemWrappedEnumerationHasNoSelfCandidate()
    {
        var sequence = new ImageSequence(["Only.jpg"], 0);

        Assert.Empty(sequence.EnumerateAfter(0, NavigationDirection.Next, wrap: true));
    }

    [Fact]
    public void ExplicitSequencePreservesInputOrder()
    {
        var sequence = new ImageSequence(["A.jpg", "D.jpg", "F.png"], 0);

        Assert.Equal(["A.jpg", "D.jpg", "F.png"], sequence.Paths.Select(Path.GetFileName));
    }

    [Fact]
    public void SequenceDoesNotWrapAtEitherBoundary()
    {
        var sequence = new ImageSequence(["A.jpg", "B.jpg"], 0);

        Assert.False(sequence.CanMoveFrom(0, NavigationDirection.Previous));
        Assert.True(sequence.CanMoveFrom(0, NavigationDirection.Next));
        Assert.True(sequence.CanMoveFrom(1, NavigationDirection.Previous));
        Assert.False(sequence.CanMoveFrom(1, NavigationDirection.Next));
    }

    [Fact]
    public void CandidateEnumerationTerminatesAtSequenceBoundary()
    {
        var sequence = new ImageSequence(["A.jpg", "B.jpg", "C.jpg"], 0);

        Assert.Equal([1, 2], sequence.EnumerateFrom(1, NavigationDirection.Next));
        Assert.Equal([1, 0], sequence.EnumerateFrom(1, NavigationDirection.Previous));
    }
}
