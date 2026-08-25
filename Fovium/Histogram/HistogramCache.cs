namespace Fovium.Histogram;

internal sealed class HistogramCache(int capacity = HistogramCache.DefaultCapacity)
{
    public const int DefaultCapacity = 128;

    private readonly int _capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    private readonly Dictionary<long, LinkedListNode<Entry>> _entries = [];
    private readonly LinkedList<Entry> _lru = [];

    public int Count => _entries.Count;

    public bool TryGet(long imageIdentity, out HistogramReadResult? result)
    {
        if (!_entries.TryGetValue(imageIdentity, out var node))
        {
            result = null;
            return false;
        }

        _lru.Remove(node);
        _lru.AddFirst(node);
        result = node.Value.Result;
        return true;
    }

    public void Add(long imageIdentity, HistogramReadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (_entries.Remove(imageIdentity, out var previous))
        {
            _lru.Remove(previous);
        }

        var node = _lru.AddFirst(new Entry(imageIdentity, result));
        _entries.Add(imageIdentity, node);
        while (_entries.Count > _capacity)
        {
            var oldest = _lru.Last!;
            _lru.RemoveLast();
            _entries.Remove(oldest.Value.ImageIdentity);
        }
    }

    public void Clear()
    {
        _entries.Clear();
        _lru.Clear();
    }

    private sealed record Entry(long ImageIdentity, HistogramReadResult Result);
}
