using Fovium.Home;
using Fovium.Settings;

namespace Fovium.Tests.Home;

public sealed class RecentLocationAvailabilityTests
{
    [Theory]
    [InlineData((int)RecentLocationKind.File)]
    [InlineData((int)RecentLocationKind.Folder)]
    public void UnavailableItemCanRecoverWithoutChangingHistory(int kindValue)
    {
        var location = new RecentLocation
        {
            Kind = (RecentLocationKind)kindValue,
            Path = "remembered-location",
        };
        var available = false;

        var unavailable = RecentLocationAvailability.Resolve(
            location,
            _ => available,
            _ => available);
        available = true;
        var recovered = RecentLocationAvailability.Resolve(
            location,
            _ => available,
            _ => available);

        Assert.Equal(RecentAvailability.Unavailable, unavailable);
        Assert.Equal(RecentAvailability.Available, recovered);
        Assert.Equal("remembered-location", location.Path);
    }
}