namespace Fovium.Viewer;

internal sealed class PhotoPresentationViewSession
{
    public event EventHandler? Changed;

    public bool IsEnabled { get; private set; }

    public void Toggle() => SetEnabled(!IsEnabled);

    public void SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled)
        {
            return;
        }

        IsEnabled = enabled;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
