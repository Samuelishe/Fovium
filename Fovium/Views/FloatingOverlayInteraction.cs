using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Fovium.Diagnostics;
using Fovium.Presentation;

namespace Fovium.Views;

internal sealed class FloatingOverlayInteraction
{
    private readonly Control _client;
    private readonly Control _panel;
    private readonly IReadOnlySet<Visual> _interactiveChildren;
    private readonly InteractionRenderDiagnostics _diagnostics;
    private readonly TranslateTransform _translation = new();
    private IPointer? _dragPointer;
    private Point _dragStartPointer;
    private FloatingOverlayPoint _dragStartPosition;
    private FloatingOverlayPoint _dragCurrentPosition;
    private FloatingOverlayPlacement _placement;

    public FloatingOverlayInteraction(
        Control client,
        Control panel,
        FloatingOverlayPlacement placement,
        InteractionRenderDiagnostics diagnostics,
        IReadOnlyCollection<Control> interactiveChildren)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _panel = panel ?? throw new ArgumentNullException(nameof(panel));
        ArgumentNullException.ThrowIfNull(interactiveChildren);
        _interactiveChildren = interactiveChildren.Cast<Visual>().ToHashSet();
        _placement = placement.Normalize();
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        panel.RenderTransform = _translation;
        panel.AddHandler(
            InputElement.PointerPressedEvent,
            OnPointerPressed,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        panel.AddHandler(
            InputElement.PointerMovedEvent,
            OnPointerMoved,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        panel.AddHandler(
            InputElement.PointerReleasedEvent,
            OnPointerReleased,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        panel.PointerCaptureLost += OnPointerCaptureLost;
        panel.SizeChanged += (_, _) => ApplyPlacement();
    }

    public event Action<FloatingOverlayPlacement>? PlacementCommitted;

    public bool IsDragging => _dragPointer is not null;

    public void SetPlacement(FloatingOverlayPlacement placement)
    {
        _placement = placement.Normalize();
        ApplyPlacement();
    }

    public void ApplyPlacement()
    {
        if (IsDragging)
        {
            return;
        }

        SetPosition(_placement.Resolve(GetSize(_client.Bounds.Size), GetSize(_panel.Bounds.Size)));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsDragging ||
            !e.GetCurrentPoint(_panel).Properties.IsLeftButtonPressed ||
            !FloatingOverlayDragOrigin.MayInitiate(
                e.Source as Visual,
                _panel,
                _interactiveChildren))
        {
            return;
        }

        _dragPointer = e.Pointer;
        _dragStartPointer = e.GetPosition(_client);
        _dragStartPosition = new FloatingOverlayPoint(_panel.Margin.Left, _panel.Margin.Top);
        _dragCurrentPosition = _dragStartPosition;
        _translation.X = 0;
        _translation.Y = 0;
        e.Pointer.Capture(_panel);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragPointer != e.Pointer)
        {
            return;
        }

        if (!e.GetCurrentPoint(_panel).Properties.IsLeftButtonPressed)
        {
            CancelDrag(e.Pointer);
            return;
        }

        _diagnostics.RecordFloatingDockDragUpdate();
        var pointer = e.GetPosition(_client);
        var update = FloatingOverlayDrag.Update(
            _dragStartPosition,
            new FloatingOverlayPoint(_dragStartPointer.X, _dragStartPointer.Y),
            new FloatingOverlayPoint(pointer.X, pointer.Y),
            GetSize(_client.Bounds.Size),
            GetSize(_panel.Bounds.Size));
        _dragCurrentPosition = update.Position;
        _translation.X = update.Translation.X;
        _translation.Y = update.Translation.Y;
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragPointer != e.Pointer)
        {
            return;
        }

        _dragPointer = null;
        e.Pointer.Capture(null);
        _translation.X = 0;
        _translation.Y = 0;
        SetPosition(_dragCurrentPosition);
        _placement = FloatingOverlayPlacement.FromPosition(
            _dragCurrentPosition,
            GetSize(_client.Bounds.Size),
            GetSize(_panel.Bounds.Size));
        PlacementCommitted?.Invoke(_placement);
        e.Handled = true;
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_dragPointer == e.Pointer)
        {
            CancelDrag(pointer: null);
        }
    }

    private void CancelDrag(IPointer? pointer)
    {
        _dragPointer = null;
        pointer?.Capture(null);
        _translation.X = 0;
        _translation.Y = 0;
        ApplyPlacement();
    }

    private void SetPosition(FloatingOverlayPoint position) =>
        _panel.Margin = new Thickness(position.X, position.Y, 0, 0);

    private static FloatingOverlaySize GetSize(Size size) => new(size.Width, size.Height);
}

internal static class FloatingOverlayDragOrigin
{
    public static bool MayInitiate(
        Visual? origin,
        Visual panel,
        IReadOnlySet<Visual> interactiveChildren)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(interactiveChildren);

        for (var current = origin; current is not null; current = current.GetVisualParent())
        {
            if (interactiveChildren.Contains(current))
            {
                return false;
            }

            if (ReferenceEquals(current, panel))
            {
                return true;
            }
        }

        return false;
    }
}
