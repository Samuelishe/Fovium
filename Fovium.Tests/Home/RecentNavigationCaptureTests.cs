using Fovium.Home;

namespace Fovium.Tests.Home;

public sealed class RecentNavigationCaptureTests
{
    [Fact]
    public void MatchingCanonicalPresentationConsumesArmedPath()
    {
        var capture = new RecentNavigationCapture(StringComparison.Ordinal);
        capture.Arm("next.jpg");

        var captured = capture.TryConsume("next.jpg", inspectionActive: false, out var path);

        Assert.True(captured);
        Assert.Equal("next.jpg", path);
        Assert.False(capture.TryConsume("next.jpg", inspectionActive: false, out _));
    }

    [Fact]
    public void StalePresentationDoesNotRecordAndClearsPendingPath()
    {
        var capture = new RecentNavigationCapture(StringComparison.Ordinal);
        capture.Arm("new.jpg");

        Assert.False(capture.TryConsume("old.jpg", inspectionActive: false, out _));
        Assert.False(capture.TryConsume("new.jpg", inspectionActive: false, out _));
    }

    [Fact]
    public void InspectionPresentationWaitsForCanonicalPresentation()
    {
        var capture = new RecentNavigationCapture(StringComparison.Ordinal);
        capture.Arm("next.jpg");

        Assert.False(capture.TryConsume("peek.jpg", inspectionActive: true, out _));
        Assert.True(capture.TryConsume("next.jpg", inspectionActive: false, out var path));
        Assert.Equal("next.jpg", path);
    }

    [Fact]
    public void CancelDiscardsPendingNavigation()
    {
        var capture = new RecentNavigationCapture(StringComparison.OrdinalIgnoreCase);
        capture.Arm("Photo.JPG");
        capture.Cancel();

        Assert.False(capture.TryConsume("photo.jpg", inspectionActive: false, out _));
    }
}