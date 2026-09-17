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
        Assert.Null(session.SelectedEntry);
        Assert.Empty(session.History);
    }

    [Fact]
    public void NewSampleBecomesSelectedAsItsOwnHistoryEntry()
    {
        var session = new ColorPickerSession();
        var sample = CreateSample(1);

        var entry = session.Commit(sample);

        Assert.Same(entry, session.SelectedEntry);
        Assert.Same(sample, entry.Sample);
        Assert.Same(sample, session.CurrentSample);
        Assert.Equal([entry], session.History);
        Assert.False(entry.Description.IsTransparent);
    }

    [Fact]
    public void HideAndReopenRetainsSelectionAndHistory()
    {
        var session = new ColorPickerSession();
        var sample = CreateSample(1);
        session.SetVisible(true);
        var entry = session.Commit(sample);

        session.SetVisible(false);
        session.SetVisible(true);

        Assert.Same(sample, session.CurrentSample);
        Assert.Same(entry, session.SelectedEntry);
        Assert.Equal([entry], session.History);
    }

    [Fact]
    public void SelectingOldEntryChangesOnlySelectionWithoutReorderingOrReclassification()
    {
        var session = new ColorPickerSession();
        var first = session.Commit(CreateSample(1));
        var second = session.Commit(CreateSample(2));
        var originalOrder = session.History.ToArray();
        var firstDescription = first.Description;

        var found = session.Select(first.EntryId);

        Assert.True(found);
        Assert.Same(first, session.SelectedEntry);
        Assert.Same(first.Sample, session.CurrentSample);
        Assert.Same(
            firstDescription,
            Assert.IsType<ColorHistoryEntry>(session.SelectedEntry).Description);
        Assert.Equal(originalOrder, session.History);
        Assert.Same(second, session.History[1]);
    }

    [Fact]
    public void UnknownEntryCannotChangeSelection()
    {
        var session = new ColorPickerSession();
        var selected = session.Commit(CreateSample(1));

        Assert.False(session.Select(long.MaxValue));
        Assert.Same(selected, session.SelectedEntry);
    }

    [Fact]
    public void CommitSelectionAndClearEachPublishOneStateChange()
    {
        var session = new ColorPickerSession();
        var changes = 0;
        session.Changed += (_, _) => changes++;
        var first = session.Commit(CreateSample(1));
        var second = session.Commit(CreateSample(2));

        Assert.True(session.Select(first.EntryId));
        Assert.True(session.Select(first.EntryId));
        Assert.False(session.Select(long.MaxValue));
        session.ClearHistory();

        Assert.Equal(4, changes);
        Assert.NotEqual(first.EntryId, second.EntryId);
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
        Assert.Equal(
            Enumerable.Range(2, 10).Select(value => (byte)value),
            session.History.Select(entry => entry.Sample.Red));
        Assert.Equal((byte)11, session.CurrentSample?.Red);
        Assert.Same(session.History[^1], session.SelectedEntry);
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
        Assert.Equal(
            Enumerable.Range(91, 10).Select(value => (byte)value),
            session.History.Select(entry => entry.Sample.Red));
    }

    [Fact]
    public void DuplicateClicksRemainDistinctRows()
    {
        var session = new ColorPickerSession();
        var sample = CreateSample(42);

        var first = session.Commit(sample);
        var second = session.Commit(sample);
        var third = session.Commit(sample);

        Assert.Equal(3, session.History.Count);
        Assert.Equal(3, session.History.Select(entry => entry.EntryId).Distinct().Count());
        Assert.All(session.History, entry => Assert.Same(sample, entry.Sample));
        Assert.True(session.Select(first.EntryId));
        Assert.Same(first, session.SelectedEntry);
        Assert.Equal([first, second, third], session.History);
    }

    [Fact]
    public void ClearEmptiesHistoryAndSelectionWithoutClosingPicker()
    {
        var session = new ColorPickerSession();
        session.SetVisible(true);
        session.Commit(CreateSample(1));
        session.Commit(CreateSample(2));

        session.ClearHistory();

        Assert.True(session.IsVisible);
        Assert.Empty(session.History);
        Assert.Null(session.SelectedEntry);
        Assert.Null(session.CurrentSample);
    }

    [Fact]
    public void NextSampleAfterClearStartsFreshHistory()
    {
        var session = new ColorPickerSession();
        var oldEntry = session.Commit(CreateSample(1));
        session.ClearHistory();

        var newEntry = session.Commit(CreateSample(2));

        Assert.Single(session.History, newEntry);
        Assert.Same(newEntry, session.SelectedEntry);
        Assert.NotEqual(oldEntry.EntryId, newEntry.EntryId);
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