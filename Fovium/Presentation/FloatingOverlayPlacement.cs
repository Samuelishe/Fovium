namespace Fovium.Presentation;

internal readonly record struct FloatingOverlaySize(double Width, double Height);

internal readonly record struct FloatingOverlayPoint(double X, double Y);

internal readonly record struct FloatingOverlayPlacement(double NormalizedX, double NormalizedY)
{
    public const double DefaultInset = 12;

    public static FloatingOverlayPlacement Default { get; } = new(0.5, 0);

    public static FloatingOverlayPlacement BottomLeft { get; } = new(0, 1);

    public static FloatingOverlayPlacement BottomRight { get; } = new(1, 1);

    public static FloatingOverlayPlacement TopRight { get; } = new(1, 0);

    public FloatingOverlayPlacement Normalize() => Normalize(Default);

    public FloatingOverlayPlacement Normalize(FloatingOverlayPlacement fallback) => new(
        NormalizeCoordinate(NormalizedX, fallback.NormalizedX),
        NormalizeCoordinate(NormalizedY, fallback.NormalizedY));

    public FloatingOverlayPoint Resolve(
        FloatingOverlaySize client,
        FloatingOverlaySize panel,
        double inset = DefaultInset)
    {
        var normalized = Normalize();
        var bounds = GetTravel(client, panel, inset);
        return new FloatingOverlayPoint(
            bounds.Left + (bounds.Width * normalized.NormalizedX),
            bounds.Top + (bounds.Height * normalized.NormalizedY));
    }

    public static FloatingOverlayPlacement FromPosition(
        FloatingOverlayPoint position,
        FloatingOverlaySize client,
        FloatingOverlaySize panel,
        double inset = DefaultInset)
    {
        var bounds = GetTravel(client, panel, inset);
        return new FloatingOverlayPlacement(
            NormalizePosition(position.X, bounds.Left, bounds.Width, Default.NormalizedX),
            NormalizePosition(position.Y, bounds.Top, bounds.Height, Default.NormalizedY));
    }

    private static TravelBounds GetTravel(
        FloatingOverlaySize client,
        FloatingOverlaySize panel,
        double inset)
    {
        var safeInset = double.IsFinite(inset) ? Math.Max(inset, 0) : DefaultInset;
        var clientWidth = NormalizeLength(client.Width);
        var clientHeight = NormalizeLength(client.Height);
        var panelWidth = NormalizeLength(panel.Width);
        var panelHeight = NormalizeLength(panel.Height);
        var left = Math.Min(safeInset, Math.Max((clientWidth - panelWidth) / 2, 0));
        var top = Math.Min(safeInset, Math.Max((clientHeight - panelHeight) / 2, 0));
        return new TravelBounds(
            left,
            top,
            Math.Max(clientWidth - panelWidth - (2 * left), 0),
            Math.Max(clientHeight - panelHeight - (2 * top), 0));
    }

    private static double NormalizePosition(
        double value,
        double origin,
        double travel,
        double fallback)
    {
        if (!double.IsFinite(value) || travel <= 0)
        {
            return fallback;
        }

        return Math.Clamp((value - origin) / travel, 0, 1);
    }

    private static double NormalizeCoordinate(double value, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, 0, 1) : fallback;

    private static double NormalizeLength(double value) =>
        double.IsFinite(value) ? Math.Max(value, 0) : 0;

    private readonly record struct TravelBounds(double Left, double Top, double Width, double Height);
}
