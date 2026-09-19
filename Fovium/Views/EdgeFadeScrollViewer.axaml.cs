using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Fovium.Views;

internal sealed partial class EdgeFadeScrollViewer : UserControl
{
    private readonly ContentControl _bodyHost;
    private readonly Border _topFade;
    private readonly Border _bottomFade;
    private readonly ScrollViewer _scroller;

    public EdgeFadeScrollViewer()
    {
        InitializeComponent();
        _scroller = FindRequired<ScrollViewer>("ScrollViewport");
        _bodyHost = FindRequired<ContentControl>("BodyHost");
        _topFade = FindRequired<Border>("TopFade");
        _bottomFade = FindRequired<Border>("BottomFade");
        _scroller.ScrollChanged += (_, _) => UpdateEdgeFades();
        _scroller.SizeChanged += (_, _) => ScheduleEdgeFadeUpdate();
        AttachedToVisualTree += (_, _) => ScheduleEdgeFadeUpdate();
    }

    public Control? Body
    {
        get => _bodyHost.Content as Control;
        set => _bodyHost.Content = value;
    }

    public Thickness BodyMargin
    {
        get => _bodyHost.Margin;
        set => _bodyHost.Margin = value;
    }

    public IBrush? TopFadeBrush
    {
        get => _topFade.Background;
        set => _topFade.Background = value;
    }

    public IBrush? BottomFadeBrush
    {
        get => _bottomFade.Background;
        set => _bottomFade.Background = value;
    }

    internal double VerticalOffset => _scroller.Offset.Y;

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void ScheduleEdgeFadeUpdate() =>
        Dispatcher.UIThread.Post(UpdateEdgeFades, DispatcherPriority.Loaded);

    private void UpdateEdgeFades()
    {
        var state = ScrollEdgeState.Resolve(
            _scroller.Offset.Y,
            _scroller.Extent.Height,
            _scroller.Viewport.Height);
        _topFade.IsVisible = state.ShowTopFade;
        _bottomFade.IsVisible = state.ShowBottomFade;
    }

    private T FindRequired<T>(string name)
        where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"Edge-fade scroll control is missing: {name}.");
}