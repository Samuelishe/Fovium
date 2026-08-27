namespace Fovium.Settings;

internal sealed record PhotoPresentationViewSettings
{
    public const double MinimumEdgeMarginPercent = 0;
    public const double MaximumEdgeMarginPercent = 15;
    public const double DefaultEdgeMarginPercent = 4;

    public double EdgeMarginPercent { get; init; } = DefaultEdgeMarginPercent;

    public static PhotoPresentationViewSettings Default { get; } = new();

    public PhotoPresentationViewSettings Normalize() => this with
    {
        EdgeMarginPercent = double.IsFinite(EdgeMarginPercent)
            ? Math.Clamp(
                EdgeMarginPercent,
                MinimumEdgeMarginPercent,
                MaximumEdgeMarginPercent)
            : DefaultEdgeMarginPercent,
    };
}
