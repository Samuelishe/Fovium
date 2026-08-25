namespace Fovium.Presentation;

internal abstract record MarkupOperation
{
    public abstract int PointCount { get; }
}

internal sealed record DrawMarkupOperation(MarkupElement Element) : MarkupOperation
{
    public override int PointCount => Element.PointCount;
}

internal sealed record EraseMarkupOperation(
    double StrokeWidthSource,
    MarkupStrokePoints Points) : MarkupOperation
{
    public override int PointCount => Points.Count;
}

internal sealed record ClearMarkupOperation : MarkupOperation
{
    public static ClearMarkupOperation Instance { get; } = new();

    public override int PointCount => 0;
}

internal readonly record struct MarkupRenderSnapshot(
    IReadOnlyList<MarkupOperation> Operations,
    MarkupOperation? Draft)
{
    public static MarkupRenderSnapshot Empty { get; } = new(Array.Empty<MarkupOperation>(), null);

    public bool IsEmpty => Operations.Count == 0 && Draft is null;
}
