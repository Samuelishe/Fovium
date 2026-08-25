namespace Fovium.Presentation;

internal readonly record struct FloatingOverlayDragUpdate(
    FloatingOverlayPoint Position,
    FloatingOverlayPoint Translation);

internal static class FloatingOverlayDrag
{
    public static FloatingOverlayDragUpdate Update(
        FloatingOverlayPoint basePosition,
        FloatingOverlayPoint pointerStart,
        FloatingOverlayPoint pointerCurrent,
        FloatingOverlaySize client,
        FloatingOverlaySize panel)
    {
        var desired = new FloatingOverlayPoint(
            basePosition.X + pointerCurrent.X - pointerStart.X,
            basePosition.Y + pointerCurrent.Y - pointerStart.Y);
        var placement = FloatingOverlayPlacement.FromPosition(desired, client, panel);
        var position = placement.Resolve(client, panel);
        return new FloatingOverlayDragUpdate(
            position,
            new FloatingOverlayPoint(
                position.X - basePosition.X,
                position.Y - basePosition.Y));
    }
}
