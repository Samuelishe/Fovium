using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace Fovium.Views;

internal sealed partial class SettingsPageView : UserControl
{
    private readonly ContentControl _headerHost;
    private readonly EdgeFadeScrollViewer _bodyScroller;

    public SettingsPageView()
    {
        InitializeComponent();
        _headerHost = FindRequired<ContentControl>("HeaderHost");
        _bodyScroller = FindRequired<EdgeFadeScrollViewer>("BodyScroller");
    }

    public Control? Header
    {
        get => _headerHost.Content as Control;
        set => _headerHost.Content = value;
    }

    public Control? Body
    {
        get => _bodyScroller.Body;
        set => _bodyScroller.Body = value;
    }

    internal double VerticalOffset => _bodyScroller.VerticalOffset;

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private T FindRequired<T>(string name)
        where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Settings page control is missing: {name}.");
}