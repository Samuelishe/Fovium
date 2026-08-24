namespace Fovium.Loading;

internal interface IRetainedResource : IDisposable
{
    long RetainedBytes { get; }
}

internal sealed class SharedResource<T> where T : class, IDisposable
{
    private readonly object _sync = new();
    private T? _value;
    private int _references = 1;

    public SharedResource(T value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public SharedResourceLease<T> Acquire()
    {
        lock (_sync)
        {
            if (_value is null)
            {
                throw new ObjectDisposedException(typeof(T).Name);
            }

            checked
            {
                _references++;
            }

            return new SharedResourceLease<T>(this, _value);
        }
    }

    public void ReleaseOwner() => Release();

    internal void Release()
    {
        T? dispose = null;
        lock (_sync)
        {
            if (_references <= 0)
            {
                return;
            }

            _references--;
            if (_references == 0)
            {
                dispose = _value;
                _value = null;
            }
        }

        dispose?.Dispose();
    }
}

internal sealed class SharedResourceLease<T> : IDisposable where T : class, IDisposable
{
    private SharedResource<T>? _owner;

    internal SharedResourceLease(SharedResource<T> owner, T value)
    {
        _owner = owner;
        Value = value;
    }

    public T Value { get; }

    public SharedResourceLease<T> Acquire()
    {
        var owner = Volatile.Read(ref _owner) ?? throw new ObjectDisposedException(nameof(SharedResourceLease<T>));
        return owner.Acquire();
    }

    public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release();
}
