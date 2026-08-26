using System.Collections.Concurrent;
using Fovium.Imaging;
using Fovium.Loading;

namespace Fovium.Tests.Loading;

internal sealed class FakeImage(string name, long retainedBytes = 16) : IRetainedResource
{
    private int _disposeCount;

    public string Name { get; } = name;

    public long RetainedBytes { get; } = retainedBytes;

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public void Dispose() => Interlocked.Increment(ref _disposeCount);
}

internal sealed class FakeImageLoader(
    Func<string, ImageLoadAllowance, CancellationToken, Task<ImageLoadResult<FakeImage>>> load)
    : IImageLoader<FakeImage>
{
    private readonly ConcurrentQueue<string> _calls = new();
    private readonly ConcurrentQueue<(string Name, ImageLoadAllowance Allowance)> _requests = new();

    public IReadOnlyList<string> Calls => _calls.ToArray();

    public IReadOnlyList<(string Name, ImageLoadAllowance Allowance)> Requests => _requests.ToArray();

    public Task<ImageLoadResult<FakeImage>> LoadAsync(
        string path,
        ImageLoadAllowance allowance,
        CancellationToken cancellationToken)
    {
        var name = Path.GetFileName(path);
        _calls.Enqueue(name);
        _requests.Enqueue((name, allowance));
        return load(path, allowance, cancellationToken);
    }

    public static FakeImageLoader Immediate(
        Func<string, ImageLoadResult<FakeImage>> load) =>
        new((path, _, _) => Task.FromResult(load(path)));
}

internal sealed class DisposableFakeImageLoader : IImageLoader<FakeImage>, IDisposable
{
    private int _disposeCount;

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public Task<ImageLoadResult<FakeImage>> LoadAsync(
        string path,
        ImageLoadAllowance allowance,
        CancellationToken cancellationToken) => Task.FromResult(FakeLoadResult.Success(path));

    public void Dispose() => Interlocked.Increment(ref _disposeCount);
}

internal static class FakeLoadResult
{
    public static ImageLoadResult<FakeImage> Success(string path, long bytes = 16) =>
        ImageLoadResult<FakeImage>.Success(new FakeImage(Path.GetFileName(path), bytes));

    public static ImageLoadResult<FakeImage> Failure(ImageLoadErrorKind kind) =>
        ImageLoadResult<FakeImage>.Failure(new ImageLoadError(kind, kind.ToString()));
}
