namespace Fovium.ColorPicking;

internal sealed class ColorPickerSession
{
    public const int HistoryCapacity = 10;
    private readonly List<ColorSample> _history = new(HistoryCapacity);

    public event EventHandler? Changed;

    public bool IsVisible { get; private set; }

    public ColorSample? CurrentSample { get; private set; }

    public IReadOnlyList<ColorSample> History => _history;

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

    public void Commit(ColorSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (_history.Count == HistoryCapacity)
        {
            _history.RemoveAt(0);
        }

        _history.Add(sample);
        CurrentSample = sample;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
