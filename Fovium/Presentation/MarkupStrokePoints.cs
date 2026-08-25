using System.Collections;
using Fovium.Rendering;

namespace Fovium.Presentation;

// Completed chunks are immutable and shared by retained frame snapshots. Only
// the current bounded tail is copied while a freehand gesture is growing.
internal sealed class MarkupStrokePoints : IReadOnlyList<PointD>
{
    private readonly PointD[][] _completedChunks;
    private readonly PointD[] _tail;

    internal MarkupStrokePoints(PointD[][] completedChunks, PointD[] tail, int count)
    {
        _completedChunks = completedChunks;
        _tail = tail;
        Count = count;
    }

    public int Count { get; }

    public PointD this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
            var completedCount = _completedChunks.Length * StrokePointBuilder.ChunkSize;
            return index < completedCount
                ? _completedChunks[index / StrokePointBuilder.ChunkSize][index % StrokePointBuilder.ChunkSize]
                : _tail[index - completedCount];
        }
    }

    public static MarkupStrokePoints From(params PointD[] points) =>
        new([], points.ToArray(), points.Length);

    internal bool SharesCompletedStorageWith(MarkupStrokePoints other) =>
        _completedChunks.Length > 0 &&
        other._completedChunks.Length > 0 &&
        ReferenceEquals(_completedChunks[0], other._completedChunks[0]);

    public IEnumerator<PointD> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class StrokePointBuilder
{
    internal const int ChunkSize = 64;

    private readonly int _maximumPoints;
    private readonly List<PointD[]> _completedChunks = [];
    private PointD[][] _publishedChunks = [];
    private PointD[] _tail = new PointD[ChunkSize];
    private int _tailCount;

    public StrokePointBuilder(PointD first, int maximumPoints)
    {
        _maximumPoints = maximumPoints;
        Add(first);
    }

    public int Count { get; private set; }

    public PointD Last { get; private set; }

    public bool Add(PointD point)
    {
        if (Count >= _maximumPoints || (Count > 0 && Last == point))
        {
            return false;
        }

        if (_tailCount == ChunkSize)
        {
            _completedChunks.Add(_tail);
            _publishedChunks = _completedChunks.ToArray();
            _tail = new PointD[ChunkSize];
            _tailCount = 0;
        }

        _tail[_tailCount++] = point;
        Count++;
        Last = point;
        return true;
    }

    public MarkupStrokePoints Snapshot()
    {
        var tail = new PointD[_tailCount];
        Array.Copy(_tail, tail, _tailCount);
        return new MarkupStrokePoints(_publishedChunks, tail, Count);
    }
}
