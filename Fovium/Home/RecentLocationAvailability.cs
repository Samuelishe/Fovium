using Fovium.Settings;

namespace Fovium.Home;

internal enum RecentAvailability
{
    Checking,
    Available,
    Unavailable,
}

internal static class RecentLocationAvailability
{
    public static RecentAvailability Resolve(
        RecentLocation location,
        Func<string, bool> fileExists,
        Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(directoryExists);
        var available = location.Kind switch
        {
            RecentLocationKind.File => fileExists(location.Path),
            RecentLocationKind.Folder => directoryExists(location.Path),
            _ => false,
        };
        return available ? RecentAvailability.Available : RecentAvailability.Unavailable;
    }
}