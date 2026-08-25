using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using Avalonia.VisualTree;
using Fovium.Diagnostics;
using Fovium.Presentation;
using Fovium.Rendering;
using System.Numerics;

namespace Fovium.Viewer;

internal readonly record struct MarkupOverlayFrame(
    RectD PhotoDestination,
    Rendering.PixelSize OrientedSourceSize,
    MarkupRenderSnapshot Snapshot);

internal sealed class MarkupOverlayControl : Control
{
    private InteractionRenderDiagnostics _diagnostics = new();
    private MarkupOverlayFrame? _frame;
    private CompositionCustomVisual? _customVisual;

    public MarkupOverlayControl()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
    }

    internal MarkupOverlayFrame? CurrentFrame => _frame;

    internal void ConfigureDiagnostics(InteractionRenderDiagnostics diagnostics) =>
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));

    internal void SetPresentation(MarkupOverlayFrame? frame)
    {
        if (_frame == frame)
        {
            return;
        }

        _frame = frame;
        _customVisual?.SendHandlerMessage(new MarkupFrameMessage(frame));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var compositor = ElementComposition.GetElementVisual(this)?.Compositor;
        if (compositor is null)
        {
            return;
        }

        _customVisual = compositor.CreateCustomVisual(new MarkupVisualHandler(_diagnostics));
        ElementComposition.SetElementChildVisual(this, _customVisual);
        UpdateCompositionSize(Bounds.Size);
        _customVisual.SendHandlerMessage(new MarkupFrameMessage(_frame));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_customVisual is { } visual)
        {
            visual.SendHandlerMessage(new MarkupFrameMessage(null));
            ElementComposition.SetElementChildVisual(this, null);
            _customVisual = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        UpdateCompositionSize(arranged);
        return arranged;
    }

    private void UpdateCompositionSize(Size size)
    {
        if (_customVisual is { } visual)
        {
            visual.Size = new Vector2((float)size.Width, (float)size.Height);
        }
    }

    private sealed record MarkupFrameMessage(MarkupOverlayFrame? Frame);

    private sealed class MarkupVisualHandler(InteractionRenderDiagnostics diagnostics)
        : CompositionCustomVisualHandler
    {
        private MarkupOverlayFrame? _frame;

        public override void OnMessage(object message)
        {
            if (message is MarkupFrameMessage update)
            {
                _frame = update.Frame;
                Invalidate();
            }
        }

        public override void OnRender(ImmediateDrawingContext context)
        {
            if (_frame is not { Snapshot.IsEmpty: false } frame)
            {
                return;
            }

            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null)
            {
                return;
            }

            using var canvasLease = feature.Lease();
            diagnostics.RecordMarkupOverlayDraw();
            SkiaMarkupOverlayRenderer.Draw(
                canvasLease.SkCanvas,
                frame.PhotoDestination,
                frame.OrientedSourceSize,
                frame.Snapshot,
                new RectD(0, 0, EffectiveSize.X, EffectiveSize.Y));
        }
    }
}
