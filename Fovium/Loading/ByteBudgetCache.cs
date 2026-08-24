namespace Fovium.Loading;

internal sealed class ByteBudgetCache<TKey, TValue> : IDisposable
    where TKey : notnull
    where TValue : class, IRetainedResource
{
    private sealed record Entry(SharedResource<TValue> Resource, long Cost, LinkedListNode<TKey> Node);

    private readonly object _sync = new();
    private readonly Dictionary<TKey, Entry> _entries;
    private readonly LinkedList<TKey> _leastRecentlyUsed = new();
    private bool _disposed;
    private long _retainedBytes;
    private TKey? _protectedKey;
    private bool _hasProtectedKey;

    public ByteBudgetCache(long budgetBytes, IEqualityComparer<TKey>? comparer = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(budgetBytes);
        BudgetBytes = budgetBytes;
        _entries = new Dictionary<TKey, Entry>(comparer);
    }

    public long BudgetBytes { get; }

    public long RetainedBytes
    {
        get
        {
            lock (_sync)
            {
                return _retainedBytes;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _entries.Count;
            }
        }
    }

    public long RemainingBytes
    {
        get
        {
            lock (_sync)
            {
                return Math.Max(0, BudgetBytes - _retainedBytes);
            }
        }
    }

    public bool TryAcquire(TKey key, out SharedResourceLease<TValue>? lease)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!_entries.TryGetValue(key, out var entry))
            {
                lease = null;
                return false;
            }

            Touch(entry);
            lease = entry.Resource.Acquire();
            return true;
        }
    }

    public bool Add(TKey key, TValue value, bool protect)
    {
        ArgumentNullException.ThrowIfNull(value);
        List<SharedResource<TValue>> releases = [];
        var retained = true;
        lock (_sync)
        {
            ThrowIfDisposed();
            if (value.RetainedBytes > BudgetBytes)
            {
                value.Dispose();
                return false;
            }

            if (_entries.Remove(key, out var replaced))
            {
                _leastRecentlyUsed.Remove(replaced.Node);
                _retainedBytes -= replaced.Cost;
                releases.Add(replaced.Resource);
            }

            if (protect)
            {
                _protectedKey = key;
                _hasProtectedKey = true;
            }

            var node = _leastRecentlyUsed.AddFirst(key);
            _entries.Add(key, new Entry(new SharedResource<TValue>(value), value.RetainedBytes, node));
            _retainedBytes += value.RetainedBytes;
            EvictToBudget(key, releases);
            if (_retainedBytes > BudgetBytes)
            {
                var rejected = _entries[key];
                _entries.Remove(key);
                _leastRecentlyUsed.Remove(rejected.Node);
                _retainedBytes -= rejected.Cost;
                releases.Add(rejected.Resource);
                retained = false;
            }
        }

        foreach (var release in releases)
        {
            release.ReleaseOwner();
        }

        return retained;
    }

    public void Protect(TKey key)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            _protectedKey = key;
            _hasProtectedKey = true;
            if (_entries.TryGetValue(key, out var entry))
            {
                Touch(entry);
            }
        }
    }

    public void Clear()
    {
        List<SharedResource<TValue>> releases;
        lock (_sync)
        {
            ThrowIfDisposed();
            releases = _entries.Values.Select(entry => entry.Resource).ToList();
            _entries.Clear();
            _leastRecentlyUsed.Clear();
            _retainedBytes = 0;
            _protectedKey = default;
            _hasProtectedKey = false;
        }

        foreach (var release in releases)
        {
            release.ReleaseOwner();
        }
    }

    public Task ClearAsync()
    {
        List<SharedResource<TValue>> releases;
        lock (_sync)
        {
            ThrowIfDisposed();
            releases = _entries.Values.Select(entry => entry.Resource).ToList();
            _entries.Clear();
            _leastRecentlyUsed.Clear();
            _retainedBytes = 0;
            _protectedKey = default;
            _hasProtectedKey = false;
        }

        return Task.Run(() =>
        {
            foreach (var release in releases)
            {
                release.ReleaseOwner();
            }
        });
    }

    public void Dispose()
    {
        List<SharedResource<TValue>> releases;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            releases = _entries.Values.Select(entry => entry.Resource).ToList();
            _entries.Clear();
            _leastRecentlyUsed.Clear();
            _retainedBytes = 0;
        }

        foreach (var release in releases)
        {
            release.ReleaseOwner();
        }
    }

    private void EvictToBudget(TKey newlyAddedKey, List<SharedResource<TValue>> releases)
    {
        var node = _leastRecentlyUsed.Last;
        while (_retainedBytes > BudgetBytes && node is not null)
        {
            var previous = node.Previous;
            var key = node.Value;
            var isProtected = _hasProtectedKey && _entries.Comparer.Equals(key, _protectedKey!);
            if (!isProtected && !_entries.Comparer.Equals(key, newlyAddedKey))
            {
                var entry = _entries[key];
                _entries.Remove(key);
                _leastRecentlyUsed.Remove(node);
                _retainedBytes -= entry.Cost;
                releases.Add(entry.Resource);
            }

            node = previous;
        }
    }

    private void Touch(Entry entry)
    {
        _leastRecentlyUsed.Remove(entry.Node);
        _leastRecentlyUsed.AddFirst(entry.Node);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
