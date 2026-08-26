using Fovium.ColorPicking;

namespace Fovium.Tests.ColorPicking;

public sealed class ColorPickerSessionTests
{
    [Fact]
    public void StartsHiddenAndEmpty()
    {
        var session = new ColorPickerSession();

        Assert.False(session.IsVisible);
        Assert.Null(session.CurrentSample);
        Assert.Empty(session.History);
    }

    [Fact]
    public void HideAndReopenRetainsCurrentAndHistory()
    {
        var session = new ColorPickerSession();
        var sample = CreateSample(1);
        session.SetVisible(true);
        session.Commit(sample);

        session.SetVisible(false);
        session.SetVisible(true);

        Assert.Same(sample, session.CurrentSample);
        Assert.Equal([sample], session.History);
    }

    [Fact]
    public void EleventhClickEvictsOnlyFirstAndRetainsTwoThroughEleven()
    {
        var session = new ColorPickerSession();
        foreach (var value in Enumerable.Range(1, 11))
        {
            session.Commit(CreateSample(value));
        }

        Assert.Equal(10, session.History.Count);
        Assert.Equal(Enumerable.Range(2, 10).Select(value => (byte)value), session.History.Select(x => x.Red));
        Assert.Equal((byte)11, session.CurrentSample?.Red);
    }

    [Fact]
    public void HundredClicksRemainBoundedToNinetyOneThroughOneHundred()
    {
        var session = new ColorPickerSession();
        foreach (var value in Enumerable.Range(1, 100))
        {
            session.Commit(CreateSample(value));
        }

        Assert.Equal(ColorPickerSession.HistoryCapacity, session.History.Count);
        Assert.Equal(Enumerable.Range(91, 10).Select(value => (byte)value), session.History.Select(x => x.Red));
    }

    [Fact]
    public void DuplicateClicksRemainDistinctRows()
    {
        var session = new ColorPickerSession();
        var sample = CreateSample(42);

        session.Commit(sample);
        session.Commit(sample);
        session.Commit(sample);

        Assert.Equal(3, session.History.Count);
        Assert.All(session.History, actual => Assert.Same(sample, actual));
    }

    private static ColorSample CreateSample(int value) => new(
        (byte)value,
        0,
        0,
        255,
        $"id-{value}",
        $"Name {value}",
        ColorSampleAccuracy.Exact);
}
