using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.SceneGraph;
using System.Numerics;

namespace Fovium.Rendering;

internal sealed class CompositionCustomDrawHost
{
    private readonly DrawHandler _handler = new();
    private CompositionCustomVisual? _visual;
    private ICustomDrawOperation? _pendingOperation;

    public void Attach(Visual owner, Size size)
    {
        var compositor = ElementComposition.GetElementVisual(owner)?.Compositor;
        if (compositor is null || _visual?.Compositor == compositor)
        {
            return;
        }

        _visual = compositor.CreateCustomVisual(_handler);
        ElementComposition.SetElementChildVisual(owner, _visual);
        Resize(size);
        if (_pendingOperation is { } operation)
        {
            _pendingOperation = null;
            _visual.SendHandlerMessage(new DrawOperationMessage(operation));
        }
    }

    public void Detach(Visual owner)
    {
        Interlocked.Exchange(ref _pendingOperation, null)?.Dispose();
        if (_visual is null)
        {
            return;
        }

        _visual.SendHandlerMessage(new DrawOperationMessage(null));
        ElementComposition.SetElementChildVisual(owner, null);
        _visual = null;
    }

    public void Resize(Size size)
    {
        if (_visual is { } visual)
        {
            visual.Size = new Vector2((float)size.Width, (float)size.Height);
        }
    }

    public void SetOperation(ICustomDrawOperation? operation)
    {
        if (_visual is { } visual)
        {
            visual.SendHandlerMessage(new DrawOperationMessage(operation));
            return;
        }

        Interlocked.Exchange(ref _pendingOperation, operation)?.Dispose();
    }

    private sealed record DrawOperationMessage(ICustomDrawOperation? Operation);

    private sealed class DrawHandler : CompositionCustomVisualHandler
    {
        private ICustomDrawOperation? _operation;

        public override void OnMessage(object message)
        {
            if (message is not DrawOperationMessage update)
            {
                return;
            }

            Interlocked.Exchange(ref _operation, update.Operation)?.Dispose();
            Invalidate();
        }

        public override void OnRender(ImmediateDrawingContext context) =>
            _operation?.Render(context);
    }
}
