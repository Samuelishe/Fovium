using Fovium.ColorSemantics;

namespace Fovium.ColorPicking;

internal sealed class ColorPickerSession
{
    public const int HistoryCapacity = 10;
    private readonly List<ColorHistoryEntry> _history = new(HistoryCapacity);
    private long _nextEntryId;

    public event EventHandler? Changed;

    public bool IsVisible { get; private set; }

    public ColorHistoryEntry? SelectedEntry { get; private set; }

    public ColorSample? CurrentSample => SelectedEntry?.Sample;

    public IReadOnlyList<ColorHistoryEntry> History => _history;

    public bool Toggle()
    {
        SetVisible(!IsVisible);
        return IsVisible;
    }

    public void SetVisible(bool visible)
    {
        if (IsVisible == visible)
        {
            return;
        }

        IsVisible = visible;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public ColorHistoryEntry Commit(ColorSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (_history.Count == HistoryCapacity)
        {
            _history.RemoveAt(0);
        }

        var entry = new ColorHistoryEntry(
            checked(++_nextEntryId),
            sample,
            ColorSampleSemantics.Describe(sample));
        _history.Add(entry);
        SelectedEntry = entry;
        Changed?.Invoke(this, EventArgs.Empty);
        return entry;
    }

    public bool Select(long entryId)
    {
        var entry = _history.Find(candidate => candidate.EntryId == entryId);
        if (entry is null || ReferenceEquals(entry, SelectedEntry))
        {
            return entry is not null;
        }

        SelectedEntry = entry;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void ClearHistory()
    {
        if (_history.Count == 0 && SelectedEntry is null)
        {
            return;
        }

        _history.Clear();
        SelectedEntry = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed record ColorHistoryEntry(
    long EntryId,
    ColorSample Sample,
    PerceptualColorDescription Description);