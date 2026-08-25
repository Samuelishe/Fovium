using Fovium.Rendering;

namespace Fovium.Presentation;

internal sealed class PresentationOverlaySession
{
    internal const int MaximumElementsPerImage = 2048;
    internal const int MaximumBrushPoints = 8192;

    private readonly Dictionary<string, MarkupDocument> _documents;
    private PresentationSettings _settings;
    private DrawingDraft? _draft;

    public PresentationOverlaySession(
        PresentationSettings settings,
        IEqualityComparer<string>? identityComparer = null)
    {
        _settings = settings.Normalize();
        _documents = new Dictionary<string, MarkupDocument>(
            identityComparer ?? StringComparer.Ordinal);
        ActiveColor = _settings.DefaultMarkupColor;
        ActiveStrokePhysicalPixels = _settings.DefaultMarkupStrokePhysicalPixels;
    }

    public event EventHandler? Changed;

    public PresentationSettings Settings => _settings;

    public string? CurrentImageIdentity { get; private set; }

    public bool HighlightEnabled { get; private set; }

    public bool MarkupToolsVisible { get; private set; }

    public MarkupTool ActiveTool { get; private set; } = MarkupTool.Brush;

    public PresentationColor ActiveColor { get; private set; }

    public double ActiveStrokePhysicalPixels { get; private set; }

    public bool IsDrawing => _draft is not null;

    public void ApplySettings(PresentationSettings settings)
    {
        var normalized = settings.Normalize();
        if (normalized == _settings)
        {
            return;
        }

        _settings = normalized;
        ActiveColor = normalized.DefaultMarkupColor;
        ActiveStrokePhysicalPixels = normalized.DefaultMarkupStrokePhysicalPixels;
        if (!normalized.MarkupToolsEnabled)
        {
            MarkupToolsVisible = false;
            _draft = null;
        }

        RaiseChanged();
    }

    public bool ToggleHighlight()
    {
        HighlightEnabled = !HighlightEnabled;
        RaiseChanged();
        return HighlightEnabled;
    }

    public bool ToggleMarkupTools()
    {
        if (!_settings.MarkupToolsEnabled)
        {
            return false;
        }

        MarkupToolsVisible = !MarkupToolsVisible;
        if (!MarkupToolsVisible)
        {
            _draft = null;
        }

        RaiseChanged();
        return MarkupToolsVisible;
    }

    public void SelectImage(string? identity)
    {
        if (CurrentImageIdentity is null
            ? identity is null
            : identity is not null && _documents.Comparer.Equals(CurrentImageIdentity, identity))
        {
            return;
        }

        _draft = null;
        CurrentImageIdentity = identity;
        RaiseChanged();
    }

    public void StartNewSequence()
    {
        _draft = null;
        CurrentImageIdentity = null;
        _documents.Clear();
        RaiseChanged();
    }

    public void SetActiveTool(MarkupTool tool)
    {
        if (!Enum.IsDefined(tool) || ActiveTool == tool)
        {
            return;
        }

        _draft = null;
        ActiveTool = tool;
        RaiseChanged();
    }

    public void SetActiveColor(PresentationColor color)
    {
        if (ActiveColor == color)
        {
            return;
        }

        ActiveColor = color;
        RaiseChanged();
    }

    public void SetActiveStrokePhysicalPixels(double stroke)
    {
        var normalized = double.IsFinite(stroke)
            ? Math.Clamp(
                stroke,
                PresentationSettings.MinimumMarkupStrokePhysicalPixels,
                PresentationSettings.MaximumMarkupStrokePhysicalPixels)
            : _settings.DefaultMarkupStrokePhysicalPixels;
        if (ActiveStrokePhysicalPixels.Equals(normalized))
        {
            return;
        }

        ActiveStrokePhysicalPixels = normalized;
        RaiseChanged();
    }

    public bool BeginDrawing(PointD sourcePoint, double physicalScale)
    {
        if (!MarkupToolsVisible ||
            CurrentImageIdentity is null ||
            _draft is not null ||
            !double.IsFinite(physicalScale) ||
            physicalScale <= 0)
        {
            return false;
        }

        _draft = new DrawingDraft(
            CurrentImageIdentity,
            ActiveTool,
            ActiveColor,
            ActiveStrokePhysicalPixels / physicalScale,
            sourcePoint);
        RaiseChanged();
        return true;
    }

    public bool ContinueDrawing(PointD sourcePoint)
    {
        if (_draft is null)
        {
            return false;
        }

        _draft.Move(sourcePoint);
        RaiseChanged();
        return true;
    }

    public bool EndDrawing(PointD sourcePoint)
    {
        if (_draft is not { } draft)
        {
            return false;
        }

        draft.Move(sourcePoint);
        _draft = null;
        var element = draft.CreateElement();
        if (element is not null)
        {
            var document = GetOrCreateDocument(draft.Identity);
            document.TryAdd(element);
        }

        RaiseChanged();
        return element is not null;
    }

    public void CancelDrawing()
    {
        if (_draft is null)
        {
            return;
        }

        _draft = null;
        RaiseChanged();
    }

    public bool ClearCurrent()
    {
        _draft = null;
        if (CurrentImageIdentity is null || !_documents.Remove(CurrentImageIdentity))
        {
            RaiseChanged();
            return false;
        }

        RaiseChanged();
        return true;
    }

    public int GetElementCount(string identity) =>
        _documents.TryGetValue(identity, out var document) ? document.Elements.Count : 0;

    public MarkupRenderSnapshot GetRenderSnapshot(string? presentationIdentity)
    {
        if (presentationIdentity is null)
        {
            return MarkupRenderSnapshot.Empty;
        }

        var elements = _documents.TryGetValue(presentationIdentity, out var document)
            ? document.Elements
            : Array.Empty<MarkupElement>();
        var draft = _draft is { Identity: var identity } &&
            _documents.Comparer.Equals(identity, presentationIdentity)
                ? _draft.CreateElement()
                : null;
        return new MarkupRenderSnapshot(elements, draft);
    }

    private MarkupDocument GetOrCreateDocument(string identity)
    {
        if (_documents.TryGetValue(identity, out var document))
        {
            return document;
        }

        document = new MarkupDocument();
        _documents.Add(identity, document);
        return document;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private sealed class MarkupDocument
    {
        private MarkupElement[] _elements = [];

        public IReadOnlyList<MarkupElement> Elements => _elements;

        public bool TryAdd(MarkupElement element)
        {
            if (_elements.Length >= MaximumElementsPerImage)
            {
                return false;
            }

            var updated = new MarkupElement[_elements.Length + 1];
            Array.Copy(_elements, updated, _elements.Length);
            updated[^1] = element;
            _elements = updated;
            return true;
        }
    }

    private sealed class DrawingDraft
    {
        private readonly MarkupTool _tool;
        private readonly PresentationColor _color;
        private readonly double _strokeWidthSource;
        private readonly PointD _start;
        private readonly List<PointD> _points;
        private PointD _current;

        public DrawingDraft(
            string identity,
            MarkupTool tool,
            PresentationColor color,
            double strokeWidthSource,
            PointD start)
        {
            Identity = identity;
            _tool = tool;
            _color = color;
            _strokeWidthSource = strokeWidthSource;
            _start = start;
            _points = [start];
            _current = start;
        }

        public string Identity { get; }

        public void Move(PointD point)
        {
            _current = point;
            if (_tool == MarkupTool.Brush &&
                _points.Count < MaximumBrushPoints &&
                _points[^1] != point)
            {
                _points.Add(point);
            }
        }

        public MarkupElement? CreateElement() => _tool switch
        {
            MarkupTool.Brush => new BrushMarkup(_color, _strokeWidthSource, _points.ToArray()),
            MarkupTool.Line when _start != _current =>
                new LineMarkup(_color, _strokeWidthSource, _start, _current),
            MarkupTool.Rectangle when _start != _current =>
                new RectangleMarkup(_color, _strokeWidthSource, _start, _current),
            MarkupTool.Arrow when _start != _current =>
                new ArrowMarkup(_color, _strokeWidthSource, _start, _current),
            _ => null,
        };
    }
}
