using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Fovium.Imaging;
using Fovium.Loading;
using Fovium.Rendering;
using Fovium.Stage;

namespace Fovium.Viewer;

internal sealed class PhotoViewportControl : Control
{
    private readonly ViewportModel _viewport = new();
    private SharedResourceLease<DecodedImage>? _image;
    private DecodedImage.AmbientLease? _ambient;
    private SharedResourceLease<DecodedImage>? _inspectionImage;
    private DecodedImage.AmbientLease? _inspectionAmbient;
    private StageSettings? _inspectionStage;
    private ViewTransfer? _inspectionRestore;
    private InspectionMode _inspectionMode;
    private Point? _lastDragPoint;
    private PointD? _lastPointerPosition;
    private TopLevel? _topLevel;
    private double _wheelAccumulator;
    private StageSettings _stage = StageSettings.Default;

    public PhotoViewportControl()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public event EventHandler? PointerActivity;

    public event EventHandler? ViewStateChanged;

    public bool HasImage => _image is not null;

    public InspectionMode InspectionMode => _inspectionMode;

    public ViewTransfer CaptureViewTransfer() =>
        _image is null ? ViewTransfer.Fit : _viewport.CaptureTransfer();

    public void SetImage(SharedResourceLease<DecodedImage> image, ViewTransfer transfer)
    {
        ArgumentNullException.ThrowIfNull(image);
        DiscardInspection();
        var previous = _image;
        var previousAmbient = _ambient;
        _ambient = null;
        _image = image;
        _viewport.SetImage(image.Value.Descriptor.OrientedSize, transfer);
        InvalidateVisual();
        previous?.Dispose();
        previousAmbient?.Dispose();
        RaiseViewStateChanged();
    }

    public void SetStage(StageSettings stage, DecodedImage.AmbientLease? ambient)
    {
        ArgumentNullException.ThrowIfNull(stage);
        if (!stage.BackgroundMode.RequiresAmbient())
        {
            ambient?.Dispose();
            ambient = null;
        }

        var previous = _ambient;
        _stage = stage;
        _ambient = ambient;
        InvalidateVisual();
        previous?.Dispose();
    }

    public void ClearImage()
    {
        DiscardInspection();
        var previous = _image;
        var previousAmbient = _ambient;
        _image = null;
        _ambient = null;
        InvalidateVisual();
        previous?.Dispose();
        previousAmbient?.Dispose();
    }

    public void Fit()
    {
        if (_image is null)
        {
            return;
        }

        _viewport.Fit();
        InvalidateAndReport();
    }

    public void SetPhotographic100AtCenter()
    {
        if (_image is null)
        {
            return;
        }

        var center = new PointD(Bounds.Width / 2, Bounds.Height / 2);
        _viewport.ZoomAt(center, 1);
        InvalidateAndReport();
    }

    public void ZoomByStepsAtCenter(int steps)
    {
        if (_image is null || steps == 0)
        {
            return;
        }

        var center = new PointD(Bounds.Width / 2, Bounds.Height / 2);
        _viewport.ZoomBySteps(center, steps);
        InvalidateAndReport();
    }

    public bool BeginPeek100()
    {
        if (_image is null || _inspectionMode != InspectionMode.None)
        {
            return false;
        }

        _inspectionRestore = _viewport.CaptureTransfer();
        _inspectionMode = InspectionMode.Peek100;
        _lastDragPoint = null;
        _viewport.SetPhotographic100ForInspection(_lastPointerPosition);
        InvalidateVisual();
        return true;
    }

    public bool BeginBlinkCompare()
    {
        if (_image is null || _inspectionMode != InspectionMode.None)
        {
            return false;
        }

        _inspectionRestore = _viewport.CaptureTransfer();
        _inspectionMode = InspectionMode.BlinkCompare;
        _lastDragPoint = null;
        return true;
    }

    public bool ShowBlinkComparison(
        SharedResourceLease<DecodedImage> image,
        StageSettings stage,
        DecodedImage.AmbientLease? ambient)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(stage);
        if (_inspectionMode != InspectionMode.BlinkCompare || _inspectionRestore is null)
        {
            ambient?.Dispose();
            return false;
        }

        var previousImage = _inspectionImage;
        var previousAmbient = _inspectionAmbient;
        _inspectionImage = image;
        _inspectionAmbient = ambient;
        _inspectionStage = stage;
        _viewport.SetImage(image.Value.Descriptor.OrientedSize, _inspectionRestore.Value);
        InvalidateVisual();
        previousImage?.Dispose();
        previousAmbient?.Dispose();
        return true;
    }

    public bool EndInspection()
    {
        if (_inspectionMode == InspectionMode.None)
        {
            return false;
        }

        var restore = _inspectionRestore;
        var comparison = _inspectionImage;
        var comparisonAmbient = _inspectionAmbient;
        _inspectionMode = InspectionMode.None;
        _inspectionRestore = null;
        _inspectionImage = null;
        _inspectionAmbient = null;
        _inspectionStage = null;
        _lastDragPoint = null;
        if (_image is not null && restore is not null)
        {
            _viewport.SetImage(_image.Value.Descriptor.OrientedSize, restore.Value);
        }

        InvalidateVisual();
        comparison?.Dispose();
        comparisonAmbient?.Dispose();
        return true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var presentationStage = _inspectionImage is not null ? _inspectionStage! : _stage;
        var fallbackColor = presentationStage.BackgroundMode switch
        {
            StageBackgroundMode.Neutral => StageDefaults.NeutralColor,
            StageBackgroundMode.Custom => presentationStage.CustomBackgroundColor,
            _ => StageDefaults.BlackColor,
        };
        IBrush fallback = new SolidColorBrush(Color.FromRgb(
            fallbackColor.Red,
            fallbackColor.Green,
            fallbackColor.Blue));
        context.FillRectangle(fallback, new Rect(Bounds.Size));
        var cachedLease = _inspectionImage ?? _image;
        if (cachedLease is null)
        {
            return;
        }

        DecodedImage.RenderLease? renderLease = null;
        DecodedImage.AmbientLease? ambientLease = null;
        try
        {
            renderLease = cachedLease.Value.AcquireRenderLease();
            var ambient = _inspectionImage is not null ? _inspectionAmbient : _ambient;
            ambientLease = ambient?.Acquire();
            var descriptor = cachedLease.Value.Descriptor;
            context.Custom(new SkiaPhotoDrawOperation(
                new Rect(Bounds.Size),
                renderLease,
                descriptor.EncodedSize,
                descriptor.Orientation,
                GetDestination(),
                _viewport.UsesExactPixelSampling,
                presentationStage,
                _viewport.RenderScaling,
                ambientLease));
            renderLease = null;
            ambientLease = null;
        }
        finally
        {
            renderLease?.Dispose();
            ambientLease?.Dispose();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
        {
            _topLevel.ScalingChanged += OnScalingChanged;
        }

        UpdateViewportMetrics();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is not null)
        {
            _topLevel.ScalingChanged -= OnScalingChanged;
            _topLevel = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateViewportMetrics();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        NotifyPointerActivity();
        UpdatePointerPosition(e);
        if (_inspectionMode != InspectionMode.None)
        {
            e.Handled = true;
            return;
        }
        if (_image is null)
        {
            return;
        }

        _wheelAccumulator += e.Delta.Y;
        var steps = (int)Math.Truncate(_wheelAccumulator);
        if (steps == 0)
        {
            return;
        }

        _wheelAccumulator -= steps;
        var pointer = e.GetPosition(this);
        _viewport.ZoomBySteps(new PointD(pointer.X, pointer.Y), steps);
        InvalidateAndReport();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        NotifyPointerActivity();
        UpdatePointerPosition(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || _image is null)
        {
            return;
        }

        if (_inspectionMode != InspectionMode.None)
        {
            if (_inspectionMode == InspectionMode.Peek100 && e.ClickCount != 2)
            {
                _lastDragPoint = e.GetPosition(this);
                e.Pointer.Capture(this);
            }

            e.Handled = true;
            return;
        }

        if (e.ClickCount == 2)
        {
            var pointer = e.GetPosition(this);
            _viewport.ToggleFitAnd100(new PointD(pointer.X, pointer.Y));
            _lastDragPoint = null;
            InvalidateAndReport();
            e.Handled = true;
            return;
        }

        _lastDragPoint = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        NotifyPointerActivity();
        UpdatePointerPosition(e);
        var previous = _lastDragPoint;
        if (previous is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        _viewport.PanBy(new PointD(current.X - previous.Value.X, current.Y - previous.Value.Y));
        _lastDragPoint = current;
        if (_inspectionMode == InspectionMode.Peek100)
        {
            InvalidateVisual();
        }
        else
        {
            InvalidateAndReport();
        }
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        NotifyPointerActivity();
        UpdatePointerPosition(e);
        if (_lastDragPoint is null)
        {
            return;
        }

        _lastDragPoint = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private RectD GetDestination()
    {
        var destination = _viewport.DestinationDip;
        if (!_viewport.UsesExactPixelSampling)
        {
            return destination;
        }

        var aligned = _viewport.PhysicalAlignedOrigin();
        return destination with { X = aligned.X, Y = aligned.Y };
    }

    private void OnScalingChanged(object? sender, EventArgs e) => UpdateViewportMetrics();

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _lastPointerPosition = null;
    }

    private void UpdateViewportMetrics()
    {
        var renderScaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        _viewport.SetViewport(
            new LogicalSize(Math.Max(Bounds.Width, 1), Math.Max(Bounds.Height, 1)),
            renderScaling);
        InvalidateAndReport();
    }

    private void NotifyPointerActivity() => PointerActivity?.Invoke(this, EventArgs.Empty);

    private void InvalidateAndReport()
    {
        InvalidateVisual();
        RaiseViewStateChanged();
    }

    private void RaiseViewStateChanged() => ViewStateChanged?.Invoke(this, EventArgs.Empty);

    private void UpdatePointerPosition(PointerEventArgs e)
    {
        var pointer = e.GetPosition(this);
        _lastPointerPosition = new PointD(pointer.X, pointer.Y);
    }

    private void DiscardInspection()
    {
        var comparison = _inspectionImage;
        var comparisonAmbient = _inspectionAmbient;
        _inspectionMode = InspectionMode.None;
        _inspectionRestore = null;
        _inspectionImage = null;
        _inspectionAmbient = null;
        _inspectionStage = null;
        _lastDragPoint = null;
        comparison?.Dispose();
        comparisonAmbient?.Dispose();
    }
}
