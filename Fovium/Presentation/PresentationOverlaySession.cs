using Fovium.Rendering;

namespace Fovium.Presentation;

internal readonly record struct MarkupHistoryLimits(
    int MaximumOperationsPerImage,
    int MaximumPointsPerStroke,
    int MaximumPointsPerImage,
    int MaximumPointsPerSession)
{
    public static MarkupHistoryLimits Default { get; } = new(
        PresentationOverlaySession.MaximumHistoryOperationsPerImage,
        PresentationOverlaySession.MaximumPointsPerStroke,
        PresentationOverlaySession.MaximumTotalPointsPerImage,
        PresentationOverlaySession.MaximumTotalCommittedPointsPerSession);
}

internal sealed class PresentationOverlaySession
{
    internal const int MaximumHistoryOperationsPerImage = 2048;
    internal const int MaximumPointsPerStroke = 8192;
    internal const int MaximumTotalPointsPerImage = 65_536;
    internal const int MaximumTotalCommittedPointsPerSession = 262_144;

    private readonly Dictionary<string, MarkupDocument> _documents;
    private readonly MarkupHistoryLimits _limits;
    private PresentationSettings _settings;
    private DrawingDraft? _draft;
    private int _totalCommittedPoints;

    public PresentationOverlaySession(
        PresentationSettings settings,
        IEqualityComparer<string>? identityComparer = null,
        MarkupHistoryLimits? limits = null)
    {
        _settings = settings.Normalize();
        _documents = new Dictionary<string, MarkupDocument>(
            identityComparer ?? StringComparer.Ordinal);
        _limits = limits ?? MarkupHistoryLimits.Default;
        ValidateLimits(_limits);
        ActiveColor = _settings.DefaultMarkupColor;
        ActiveStrokePhysicalPixels = _settings.DefaultMarkupStrokePhysicalPixels;
        ActiveOpacity = _settings.DefaultMarkupOpacity;
    }

    public event EventHandler? Changed;

    public PresentationSettings Settings => _settings;

    public string? CurrentImageIdentity { get; private set; }

    public bool HighlightEnabled { get; private set; }

    public bool MarkupToolsVisible { get; private set; }

    public MarkupTool ActiveTool { get; private set; } = MarkupTool.Brush;

    public PresentationColor ActiveColor { get; private set; }

    public double ActiveStrokePhysicalPixels { get; private set; }

    public double ActiveOpacity { get; private set; }

    public bool IsDrawing => _draft is not null;

    public bool CanUndo => _draft is not null || GetCurrentDocument()?.CanUndo == true;

    public bool CanRedo => _draft is null && GetCurrentDocument()?.CanRedo == true;

    public bool CanClear => GetCurrentDocument()?.HasPotentiallyVisibleMarkup == true;

    internal int TotalCommittedPoints => _totalCommittedPoints;

    public void ApplySettings(PresentationSettings settings)
    {
        var normalized = settings.Normalize();
        if (normalized == _settings)
        {
            return;
        }

        var previous = _settings;
        _settings = normalized;
        if (normalized.DefaultMarkupColor != previous.DefaultMarkupColor)
        {
            ActiveColor = normalized.DefaultMarkupColor;
        }

        if (!normalized.DefaultMarkupStrokePhysicalPixels.Equals(
                previous.DefaultMarkupStrokePhysicalPixels))
        {
            ActiveStrokePhysicalPixels = normalized.DefaultMarkupStrokePhysicalPixels;
        }

        if (!normalized.DefaultMarkupOpacity.Equals(previous.DefaultMarkupOpacity))
        {
            ActiveOpacity = normalized.DefaultMarkupOpacity;
        }

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
        _totalCommittedPoints = 0;
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

    public void SetActiveOpacity(double opacity)
    {
        var normalized = double.IsFinite(opacity)
            ? Math.Clamp(
                opacity,
                PresentationSettings.MinimumMarkupOpacity,
                PresentationSettings.MaximumMarkupOpacity)
            : _settings.DefaultMarkupOpacity;
        if (ActiveOpacity.Equals(normalized))
        {
            return;
        }

        ActiveOpacity = normalized;
        RaiseChanged();
    }

    public bool AdjustActiveStrokePhysicalPixels(double delta)
    {
        if (!MarkupToolsVisible || !double.IsFinite(delta) || delta == 0)
        {
            return false;
        }

        var previous = ActiveStrokePhysicalPixels;
        SetActiveStrokePhysicalPixels(previous + delta);
        return !ActiveStrokePhysicalPixels.Equals(previous);
    }

    public bool AdjustActiveOpacity(double delta)
    {
        if (!MarkupToolsVisible || !double.IsFinite(delta) || delta == 0)
        {
            return false;
        }

        var previous = ActiveOpacity;
        SetActiveOpacity(previous + delta);
        return !ActiveOpacity.Equals(previous);
    }

    public bool ClearCurrentFromCommand() => MarkupToolsVisible && ClearCurrent();

    public bool BeginDrawing(PointD sourcePoint, double physicalScale)
        => BeginDrawing(sourcePoint, physicalScale, null);

    public bool BeginDrawing(
        PointD sourcePoint,
        double physicalScale,
        PixelSize? sourceSize)
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
            ActiveOpacity,
            ActiveStrokePhysicalPixels / physicalScale,
            sourcePoint,
            sourceSize,
            _limits.MaximumPointsPerStroke);
        RaiseChanged();
        return true;
    }

    public bool ContinueDrawing(
        PointD sourcePoint,
        MarkupDrawingModifiers modifiers = MarkupDrawingModifiers.None)
    {
        if (_draft is null)
        {
            return false;
        }

        _draft.Move(sourcePoint, modifiers);
        RaiseChanged();
        return true;
    }

    public bool EndDrawing(
        PointD sourcePoint,
        MarkupDrawingModifiers modifiers = MarkupDrawingModifiers.None)
    {
        if (_draft is not { } draft)
        {
            return false;
        }

        draft.Move(sourcePoint, modifiers);
        _draft = null;
        var operation = draft.CreateOperation();
        var committed = operation is not null && TryCommit(draft.Identity, operation);
        RaiseChanged();
        return committed;
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

    public bool UndoCurrent()
    {
        if (_draft is not null)
        {
            _draft = null;
            RaiseChanged();
            return true;
        }

        var document = GetCurrentDocument();
        if (document is null || !document.Undo())
        {
            return false;
        }

        RaiseChanged();
        return true;
    }

    public bool RedoCurrent()
    {
        if (_draft is not null)
        {
            _draft = null;
            RaiseChanged();
            return true;
        }

        var document = GetCurrentDocument();
        if (document is null || !document.Redo())
        {
            return false;
        }

        RaiseChanged();
        return true;
    }

    public bool ClearCurrent()
    {
        _draft = null;
        var document = GetCurrentDocument();
        if (CurrentImageIdentity is null || document is null || !document.HasPotentiallyVisibleMarkup)
        {
            RaiseChanged();
            return false;
        }

        var committed = TryCommit(CurrentImageIdentity, ClearMarkupOperation.Instance);
        RaiseChanged();
        return committed;
    }

    public int GetActiveOperationCount(string identity) =>
        _documents.TryGetValue(identity, out var document) ? document.ActiveOperations.Count : 0;

    public int GetRetainedOperationCount(string identity) =>
        _documents.TryGetValue(identity, out var document) ? document.RetainedOperationCount : 0;

    public int GetRetainedPointCount(string identity) =>
        _documents.TryGetValue(identity, out var document) ? document.RetainedPointCount : 0;

    public MarkupRenderSnapshot GetRenderSnapshot(string? presentationIdentity)
    {
        if (presentationIdentity is null)
        {
            return MarkupRenderSnapshot.Empty;
        }

        var operations = _documents.TryGetValue(presentationIdentity, out var document)
            ? document.ActiveOperations
            : Array.Empty<MarkupOperation>();
        var draft = _draft is { Identity: var identity } &&
            _documents.Comparer.Equals(identity, presentationIdentity)
                ? _draft.CreateOperation()
                : null;
        return new MarkupRenderSnapshot(operations, draft);
    }

    private bool TryCommit(string identity, MarkupOperation operation)
    {
        var document = GetOrCreateDocument(identity);
        var removedRedoPoints = document.RedoPointCount;
        var retainedPointCount = document.RetainedPointCount - removedRedoPoints + operation.PointCount;
        var sessionPointCount = _totalCommittedPoints - removedRedoPoints + operation.PointCount;
        if (document.ActiveOperations.Count + 1 > _limits.MaximumOperationsPerImage ||
            operation.PointCount > _limits.MaximumPointsPerStroke ||
            retainedPointCount > _limits.MaximumPointsPerImage ||
            sessionPointCount > _limits.MaximumPointsPerSession)
        {
            return false;
        }

        document.Append(operation);
        _totalCommittedPoints = sessionPointCount;
        return true;
    }

    private MarkupDocument? GetCurrentDocument() =>
        CurrentImageIdentity is not null && _documents.TryGetValue(CurrentImageIdentity, out var document)
            ? document
            : null;

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

    private static void ValidateLimits(MarkupHistoryLimits limits)
    {
        if (limits.MaximumOperationsPerImage <= 0 ||
            limits.MaximumPointsPerStroke <= 0 ||
            limits.MaximumPointsPerImage <= 0 ||
            limits.MaximumPointsPerSession <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limits));
        }
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private sealed class MarkupDocument
    {
        private MarkupOperation[] _operations = [];
        private MarkupOperation[] _activeOperations = [];
        private int _cursor;

        public IReadOnlyList<MarkupOperation> ActiveOperations => _activeOperations;

        public int RetainedOperationCount => _operations.Length;

        public int RetainedPointCount { get; private set; }

        public int RedoPointCount => _operations
            .Skip(_cursor)
            .Sum(operation => operation.PointCount);

        public bool CanUndo => _cursor > 0;

        public bool CanRedo => _cursor < _operations.Length;

        public bool HasPotentiallyVisibleMarkup
        {
            get
            {
                var visible = false;
                foreach (var operation in _activeOperations)
                {
                    switch (operation)
                    {
                        case DrawMarkupOperation:
                            visible = true;
                            break;
                        case ClearMarkupOperation:
                            visible = false;
                            break;
                    }
                }

                return visible;
            }
        }

        public void Append(MarkupOperation operation)
        {
            var updated = new MarkupOperation[_cursor + 1];
            Array.Copy(_operations, updated, _cursor);
            updated[^1] = operation;
            _operations = updated;
            _cursor = updated.Length;
            _activeOperations = updated;
            RetainedPointCount = updated.Sum(item => item.PointCount);
        }

        public bool Undo()
        {
            if (!CanUndo)
            {
                return false;
            }

            _cursor--;
            RefreshActiveOperations();
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo)
            {
                return false;
            }

            _cursor++;
            RefreshActiveOperations();
            return true;
        }

        private void RefreshActiveOperations()
        {
            if (_cursor == _operations.Length)
            {
                _activeOperations = _operations;
                return;
            }

            var active = new MarkupOperation[_cursor];
            Array.Copy(_operations, active, _cursor);
            _activeOperations = active;
        }
    }

    private sealed class DrawingDraft
    {
        private readonly MarkupTool _tool;
        private readonly PresentationColor _color;
        private readonly double _opacity;
        private readonly double _strokeWidthSource;
        private readonly PointD _start;
        private readonly PixelSize? _sourceSize;
        private readonly StrokePointBuilder? _strokePoints;
        private PointD _current;
        private MarkupDrawingModifiers _modifiers;

        public DrawingDraft(
            string identity,
            MarkupTool tool,
            PresentationColor color,
            double opacity,
            double strokeWidthSource,
            PointD start,
            PixelSize? sourceSize,
            int maximumPoints)
        {
            Identity = identity;
            _tool = tool;
            _color = color;
            _opacity = opacity;
            _strokeWidthSource = strokeWidthSource;
            _start = start;
            _sourceSize = sourceSize;
            _current = start;
            if (tool is MarkupTool.Brush or MarkupTool.Eraser)
            {
                _strokePoints = new StrokePointBuilder(start, maximumPoints);
            }
        }

        public string Identity { get; }

        public void Move(PointD point, MarkupDrawingModifiers modifiers)
        {
            _current = point;
            _modifiers = modifiers;
            _strokePoints?.Add(point);
        }

        public MarkupOperation? CreateOperation()
        {
            var constrained = _modifiers.HasFlag(MarkupDrawingModifiers.Constrain) &&
                _tool != MarkupTool.Eraser;
            var endpoint = constrained
                ? _tool is MarkupTool.Rectangle or MarkupTool.Ellipse
                    ? MarkupConstraintGeometry.SquareEndpoint(_start, _current)
                    : MarkupConstraintGeometry.SnapEndpointTo45Degrees(_start, _current)
                : _current;
            if (constrained && _sourceSize is { } sourceSize)
            {
                endpoint = MarkupConstraintGeometry.ClipEndpointAlongRay(_start, endpoint, sourceSize);
            }

            return _tool switch
            {
                MarkupTool.Brush when constrained => new DrawMarkupOperation(
                    new BrushMarkup(
                        _color,
                        _strokeWidthSource,
                        MarkupStrokePoints.From(_start, endpoint),
                        _opacity)),
                MarkupTool.Brush => new DrawMarkupOperation(
                    new BrushMarkup(_color, _strokeWidthSource, _strokePoints!.Snapshot(), _opacity)),
                MarkupTool.Eraser => new EraseMarkupOperation(
                    _strokeWidthSource,
                    _strokePoints!.Snapshot()),
                MarkupTool.Line when _start != endpoint => new DrawMarkupOperation(
                    new LineMarkup(_color, _strokeWidthSource, _start, endpoint, _opacity)),
                MarkupTool.Rectangle when _start != endpoint => new DrawMarkupOperation(
                    new RectangleMarkup(_color, _strokeWidthSource, _start, endpoint, _opacity)),
                MarkupTool.Ellipse when _start != endpoint => new DrawMarkupOperation(
                    new EllipseMarkup(_color, _strokeWidthSource, _start, endpoint, _opacity)),
                MarkupTool.Arrow when _start != endpoint => new DrawMarkupOperation(
                    new ArrowMarkup(_color, _strokeWidthSource, _start, endpoint, _opacity)),
                _ => null,
            };
        }
    }
}
