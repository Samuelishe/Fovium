namespace Fovium.RenderProbe;

internal enum ViewportMode
{
    Fit,
    Manual,
}

internal sealed class ViewportModel
{
    private ImageSize _sourceSize = new(1, 1);
    private LogicalSize _viewportSize = new(1, 1);
    private double _renderScaling = 1;
    private double _physicalScale = 1;
    private PointD _originDip;

    public ImageSize SourceSize => _sourceSize;

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

    public void SetImage(ImageSize orientedSourceSize)
    {
        if (!orientedSourceSize.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(orientedSourceSize));
        }

        _sourceSize = orientedSourceSize;
        Fit();
    }

    public void SetViewport(LogicalSize logicalSize, double renderScaling)
    {
        if (!logicalSize.IsValid)
        {
            return;
        }

        ValidateRenderScaling(renderScaling);
        var sourceAtCenter = SourcePointAt(new PointD(
            _viewportSize.Width / 2,
            _viewportSize.Height / 2));

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

    public void SetPhotographic100()
    {
        _physicalScale = 1;
        Mode = ViewportMode.Manual;
        CenterOrigin();
        ClampOrigin();
    }

    public void ZoomAt(PointD cursorDip, double newPhysicalScale)
    {
        if (!double.IsFinite(newPhysicalScale) || newPhysicalScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newPhysicalScale));
        }

        var sourcePoint = SourcePointAt(cursorDip);
        _physicalScale = newPhysicalScale;
        Mode = ViewportMode.Manual;
        _originDip = new PointD(
            cursorDip.X - sourcePoint.X * DipScale,
            cursorDip.Y - sourcePoint.Y * DipScale);
        ClampOrigin();
    }

    public void SetPhysicalScaleCentered(double newPhysicalScale) =>
        ZoomAt(
            new PointD(_viewportSize.Width / 2, _viewportSize.Height / 2),
            newPhysicalScale);

    public void PanBy(PointD deltaDip)
    {
        Mode = ViewportMode.Manual;
        _originDip = new PointD(_originDip.X + deltaDip.X, _originDip.Y + deltaDip.Y);
        ClampOrigin();
    }

    public PointD SourcePointAt(PointD viewportPointDip) => new(
        (viewportPointDip.X - _originDip.X) / DipScale,
        (viewportPointDip.Y - _originDip.Y) / DipScale);

    public PointD ViewportPointFor(PointD sourcePoint) => new(
        _originDip.X + sourcePoint.X * DipScale,
        _originDip.Y + sourcePoint.Y * DipScale);

    public PointD PhysicalAlignedOrigin() => new(
        Math.Round(_originDip.X * _renderScaling, MidpointRounding.AwayFromZero) / _renderScaling,
        Math.Round(_originDip.Y * _renderScaling, MidpointRounding.AwayFromZero) / _renderScaling);

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

    private static void ValidateRenderScaling(double renderScaling)
    {
        if (!double.IsFinite(renderScaling) || renderScaling <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(renderScaling));
        }
    }
}
