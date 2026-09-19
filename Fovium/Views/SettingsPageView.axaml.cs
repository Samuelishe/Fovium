using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Fovium.Views;

internal sealed partial class SettingsPageView : UserControl
{
    private readonly ContentControl _headerHost;
    private readonly ContentControl _bodyHost;
    private readonly Border _topFade;
    private readonly Border _bottomFade;
    private readonly ScrollViewer _scroller;

    public SettingsPageView()
    {
        InitializeComponent();
        _scroller = FindRequired<ScrollViewer>("ScrollViewport");
        _headerHost = FindRequired<ContentControl>("HeaderHost");
        _bodyHost = FindRequired<ContentControl>("BodyHost");
        _topFade = FindRequired<Border>("TopFade");
        _bottomFade = FindRequired<Border>("BottomFade");
        _scroller.ScrollChanged += (_, _) => UpdateEdgeFades();
        _scroller.SizeChanged += (_, _) => ScheduleEdgeFadeUpdate();
        AttachedToVisualTree += (_, _) => ScheduleEdgeFadeUpdate();
    }

    public Control? Header
    {
        get => _headerHost.Content as Control;
        set => _headerHost.Content = value;
    }

    public Control? Body
    {
        get => _bodyHost.Content as Control;
        set => _bodyHost.Content = value;
    }

    internal double VerticalOffset => _scroller.Offset.Y;

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void ScheduleEdgeFadeUpdate() =>
        Dispatcher.UIThread.Post(UpdateEdgeFades, DispatcherPriority.Loaded);

    private void UpdateEdgeFades()
    {
        var state = SettingsScrollEdgeState.Resolve(
            _scroller.Offset.Y,
            _scroller.Extent.Height,
            _scroller.Viewport.Height);
        _topFade.IsVisible = state.ShowTopFade;
        _bottomFade.IsVisible = state.ShowBottomFade;
    }

    private T FindRequired<T>(string name)
        where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Settings page control is missing: {name}.");
}