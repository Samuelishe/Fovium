using Fovium.Navigation;

namespace Fovium.Application;

internal sealed class ActivationService(DirectorySequenceBuilder directorySequenceBuilder)
{
    public async Task<ImageSequence?> ResolveAsync(
        ActivationPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.Mode switch
        {
            ActivationMode.Home => null,
            ActivationMode.Directory => await directorySequenceBuilder.BuildAsync(
                plan.Paths[0],
                cancellationToken),
            ActivationMode.Folder => await directorySequenceBuilder.BuildFolderAsync(
                plan.Paths[0],
                cancellationToken),
            ActivationMode.ExplicitSelection => new ImageSequence(plan.Paths, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(plan)),
        };
    }
}