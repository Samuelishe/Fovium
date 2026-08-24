using Fovium.Loading;

namespace Fovium.Tests.Loading;

public sealed class AutomaticMemoryPolicyTests
{
    [Fact]
    public void SmallRuntimeMemoryUsesConservativeMinimumClamps()
    {
        var policy = AutomaticMemoryPolicy.FromAvailableMemory(512L * 1024 * 1024);

        Assert.Equal(AutomaticMemoryPolicy.MinimumCacheBudgetBytes, policy.CacheBudgetBytes);
        Assert.Equal(AutomaticMemoryPolicy.MinimumForegroundDecodeBudgetBytes, policy.ForegroundDecodeBudgetBytes);
        Assert.True(policy.SpeculativeDecodeBudgetBytes < policy.ForegroundDecodeBudgetBytes);
    }

    [Fact]
    public void LargeRuntimeMemoryUsesProductMaximumClamps()
    {
        var policy = AutomaticMemoryPolicy.FromAvailableMemory(128L * 1024 * 1024 * 1024);

        Assert.Equal(AutomaticMemoryPolicy.MaximumCacheBudgetBytes, policy.CacheBudgetBytes);
        Assert.Equal(AutomaticMemoryPolicy.MaximumForegroundDecodeBudgetBytes, policy.ForegroundDecodeBudgetBytes);
        Assert.InRange(policy.SpeculativeDecodeBudgetBytes, 1, policy.CacheBudgetBytes);
    }
}
