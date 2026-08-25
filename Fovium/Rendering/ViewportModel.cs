namespace Fovium.Rendering;

internal enum ViewportMode
{
    Fit,
    Manual,
}

internal readonly record struct ViewTransfer(
    ViewportMode Mode,
    double PhysicalScale,
    NormalizedPoint PointOfInterest)
{
    public static ViewTransfer Fit { get; } = new(ViewportMode.Fit, 1, new NormalizedPoint(0.5, 0.5));
}

internal sealed class ViewportModel
{
    public const double MinimumManualPhysicalScale = 0.01;
    public const double MaximumManualPhysicalScale = 64;
    public const double WheelStepRatio = 1.14;

    private PixelSize _sourceSize = new(1, 1);
    private LogicalSize _viewportSize = new(1, 1);
    private double _renderScaling = 1;
    private double _physicalScale = 1;
    private PointD _originDip;

    public PixelSize SourceSize => _sourceSize;

    public LogicalSize ViewportSize => _viewportSize;

    public double RenderScaling => _renderScaling;

    public double PhysicalScale => _physicalScale;

    public double DipScale => _physicalScale / _renderScaling;

    public PointD OriginDip => _originDip;

    public ViewportMode Mode { get; private set; } = ViewportMode.Fit;

    public RectD DestinationDip => new(
        _originDip.X,
        _originDip.Y,
        _sourceSize.Width * DipScale,
        _sourceSize.Height * DipScale);

    public bool UsesExactPixelSampling => IsIntegralPhysicalScale(_physicalScale);

    public void SetImage(PixelSize orientedSourceSize, ViewTransfer transfer)
    {
        if (!orientedSourceSize.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(orientedSourceSize));
        }

        _sourceSize = orientedSourceSize;
        if (transfer.Mode == ViewportMode.Fit)
        {
            Fit();
            return;
        }

        _physicalScale = ClampManualScale(transfer.PhysicalScale);
        Mode = ViewportMode.Manual;
        PlacePointAtViewportCenter(transfer.PointOfInterest.Clamp());
        ClampOrigin();
    }

    public void SetImage(PixelSize orientedSourceSize) => SetImage(orientedSourceSize, ViewTransfer.Fit);

    public void SetViewport(LogicalSize logicalSize, double renderScaling)
    {
        if (!logicalSize.IsValid)
        {
            return;
        }

        ValidateRenderScaling(renderScaling);
        var sourceAtCenter = SourcePointAt(ViewportCenter());

        _viewportSize = logicalSize;
        _renderScaling = renderScaling;

        if (Mode == ViewportMode.Fit)
        {
            Fit();
            return;
        }

        _originDip = new PointD(
            logicalSize.Width / 2 - sourceAtCenter.X * DipScale,
            logicalSize.Height / 2 - sourceAtCenter.Y * DipScale);
        ClampOrigin();
    }

    public void Fit()
    {
        var physicalViewportWidth = _viewportSize.Width * _renderScaling;
        var physicalViewportHeight = _viewportSize.Height * _renderScaling;
        _physicalScale = Math.Min(
            1,
            Math.Min(
                physicalViewportWidth / _sourceSize.Width,
                physicalViewportHeight / _sourceSize.Height));
        Mode = ViewportMode.Fit;
        CenterOrigin();
    }

    public void ToggleFitAnd100(PointD pointerDip)
    {
        if (Mode != ViewportMode.Fit)
        {
            Fit();
            return;
        }

        ZoomAt(pointerDip, 1);
    }

    public void ZoomBySteps(PointD pointerDip, int steps)
    {
        if (steps == 0)
        {
            return;
        }

        var boundedSteps = Math.Clamp(steps, -8, 8);
        ZoomAt(pointerDip, _physicalScale * Math.Pow(WheelStepRatio, boundedSteps));
    }

    public void ZoomAt(PointD pointerDip, double newPhysicalScale)
    {
        if (!double.IsFinite(newPhysicalScale) || newPhysicalScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newPhysicalScale));
        }

        PlaceSourcePointAt(SourcePointAt(pointerDip), pointerDip, newPhysicalScale);
    }

    public void SetPhotographic100ForInspection(PointD? pointerDip)
    {
        var viewportAnchor = ViewportCenter();
        var sourceAnchor = SourcePointAt(viewportAnchor);
        if (pointerDip is { } pointer && Contains(DestinationDip, pointer))
        {
            viewportAnchor = pointer;
            sourceAnchor = SourcePointAt(pointer);
        }

        PlaceSourcePointAt(sourceAnchor, viewportAnchor, 1);
    }

    public void PanBy(PointD deltaDip)
    {
        if (Mode == ViewportMode.Fit)
        {
            return;
        }

        _originDip = new PointD(_originDip.X + deltaDip.X, _originDip.Y + deltaDip.Y);
        ClampOrigin();
    }

    public PointD SourcePointAt(PointD viewportPointDip) => new(
        (viewportPointDip.X - _originDip.X) / DipScale,
        (viewportPointDip.Y - _originDip.Y) / DipScale);

    public PointD ViewportPointFor(PointD sourcePoint) => new(
        _originDip.X + sourcePoint.X * DipScale,
        _originDip.Y + sourcePoint.Y * DipScale);

    public ViewTransfer CaptureTransfer()
    {
        if (Mode == ViewportMode.Fit)
        {
            return ViewTransfer.Fit;
        }

        var center = SourcePointAt(ViewportCenter());
        return new ViewTransfer(
            ViewportMode.Manual,
            _physicalScale,
            new NormalizedPoint(center.X / _sourceSize.Width, center.Y / _sourceSize.Height).Clamp());
    }

    public PointD PhysicalAlignedOrigin() => new(
        Math.Round(_originDip.X * _renderScaling, MidpointRounding.AwayFromZero) / _renderScaling,
        Math.Round(_originDip.Y * _renderScaling, MidpointRounding.AwayFromZero) / _renderScaling);

    private PointD ViewportCenter() => new(_viewportSize.Width / 2, _viewportSize.Height / 2);

    private void PlacePointAtViewportCenter(NormalizedPoint point)
    {
        var sourcePoint = new PointD(point.X * _sourceSize.Width, point.Y * _sourceSize.Height);
        var center = ViewportCenter();
        _originDip = new PointD(
            center.X - sourcePoint.X * DipScale,
            center.Y - sourcePoint.Y * DipScale);
    }

    private void PlaceSourcePointAt(
        PointD sourcePoint,
        PointD viewportPoint,
        double physicalScale)
    {
        _physicalScale = ClampManualScale(physicalScale);
        Mode = ViewportMode.Manual;
        _originDip = new PointD(
            viewportPoint.X - sourcePoint.X * DipScale,
            viewportPoint.Y - sourcePoint.Y * DipScale);
        ClampOrigin();
    }

    private void CenterOrigin()
    {
        var destinationWidth = _sourceSize.Width * DipScale;
        var destinationHeight = _sourceSize.Height * DipScale;
        _originDip = new PointD(
            (_viewportSize.Width - destinationWidth) / 2,
            (_viewportSize.Height - destinationHeight) / 2);
    }

    private void ClampOrigin()
    {
        _originDip = new PointD(
            ClampAxis(_originDip.X, _sourceSize.Width * DipScale, _viewportSize.Width),
            ClampAxis(_originDip.Y, _sourceSize.Height * DipScale, _viewportSize.Height));
    }

    private static double ClampAxis(double origin, double imageLength, double viewportLength) =>
        imageLength <= viewportLength
            ? (viewportLength - imageLength) / 2
            : Math.Clamp(origin, viewportLength - imageLength, 0);

    private static double ClampManualScale(double physicalScale) =>
        Math.Clamp(physicalScale, MinimumManualPhysicalScale, MaximumManualPhysicalScale);

    private static bool IsIntegralPhysicalScale(double physicalScale) =>
        Math.Abs(physicalScale - Math.Round(physicalScale)) <= 1e-9;

    private static bool Contains(RectD rect, PointD point) =>
        point.X >= rect.X &&
        point.X <= rect.X + rect.Width &&
        point.Y >= rect.Y &&
        point.Y <= rect.Y + rect.Height;

    private static void ValidateRenderScaling(double renderScaling)
    {
        if (!double.IsFinite(renderScaling) || renderScaling <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(renderScaling));
        }
    }
}
