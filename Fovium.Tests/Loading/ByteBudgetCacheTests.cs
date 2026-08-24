using Fovium.Loading;

namespace Fovium.Tests.Loading;

public sealed class ByteBudgetCacheTests
{
    [Fact]
    public void ByteBudgetRejectsNeighborThatCannotFitBesideProtectedCurrent()
    {
        using var cache = new ByteBudgetCache<string, FakeImage>(100, StringComparer.Ordinal);
        var current = new FakeImage("current", 60);
        var neighbor = new FakeImage("neighbor", 60);

        Assert.True(cache.Add("current", current, protect: true));
        Assert.False(cache.Add("neighbor", neighbor, protect: false));

        Assert.Equal(60, cache.RetainedBytes);
        Assert.Equal(1, cache.Count);
        Assert.Equal(1, neighbor.DisposeCount);
        Assert.True(cache.TryAcquire("current", out var currentLease));
        currentLease!.Dispose();
    }

    [Fact]
    public void LeastRecentlyUsedNeighborIsEvictedWhileCurrentStaysProtected()
    {
        using var cache = new ByteBudgetCache<string, FakeImage>(100, StringComparer.Ordinal);
        var current = new FakeImage("current", 40);
        var firstNeighbor = new FakeImage("first", 30);
        var secondNeighbor = new FakeImage("second", 40);
        cache.Add("current", current, protect: true);
        cache.Add("first", firstNeighbor, protect: false);

        cache.Add("second", secondNeighbor, protect: false);

        Assert.False(cache.TryAcquire("first", out _));
        Assert.True(cache.TryAcquire("current", out var currentLease));
        Assert.True(cache.TryAcquire("second", out var secondLease));
        Assert.Equal(1, firstNeighbor.DisposeCount);
        currentLease!.Dispose();
        secondLease!.Dispose();
    }

    [Fact]
    public void OutstandingLeaseKeepsResourceAliveAfterCacheEviction()
    {
        using var cache = new ByteBudgetCache<string, FakeImage>(100, StringComparer.Ordinal);
        var image = new FakeImage("leased", 50);
        cache.Add("leased", image, protect: true);
        Assert.True(cache.TryAcquire("leased", out var lease));

        cache.Clear();

        Assert.Equal(0, image.DisposeCount);
        Assert.Equal("leased", lease!.Value.Name);
        lease.Dispose();
        Assert.Equal(1, image.DisposeCount);
    }

    [Fact]
    public void ShutdownDisposesEveryUnleasedEntry()
    {
        var first = new FakeImage("first", 40);
        var second = new FakeImage("second", 40);
        var cache = new ByteBudgetCache<string, FakeImage>(100, StringComparer.Ordinal);
        cache.Add("first", first, protect: true);
        cache.Add("second", second, protect: false);

        cache.Dispose();

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
    }

    [Fact]
    public void RepeatedNeighborsCannotGrowItemCountWithoutBound()
    {
        using var cache = new ByteBudgetCache<string, FakeImage>(100, StringComparer.Ordinal);
        cache.Add("current", new FakeImage("current", 20), protect: true);

        for (var index = 0; index < 100; index++)
        {
            cache.Add($"neighbor-{index}", new FakeImage($"neighbor-{index}", 20), protect: false);
        }

        Assert.InRange(cache.Count, 1, 5);
        Assert.InRange(cache.RetainedBytes, 1, 100);
    }
}
