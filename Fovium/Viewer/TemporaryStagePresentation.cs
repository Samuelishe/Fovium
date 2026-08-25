using Fovium.Imaging;
using Fovium.Stage;

namespace Fovium.Viewer;

internal sealed class TemporaryStagePresentation : IDisposable
{
    private DecodedImage.AmbientLease? _ambient;

    private TemporaryStagePresentation(StageSettings stage, DecodedImage.AmbientLease? ambient)
    {
        Stage = stage;
        _ambient = ambient;
    }

    public StageSettings Stage { get; }

    public DecodedImage.AmbientLease? Ambient => Volatile.Read(ref _ambient);

    public static TemporaryStagePresentation Create(StageSettings stage, DecodedImage comparisonImage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(comparisonImage);
        DecodedImage.AmbientLease? ambient = null;
        if (stage.BackgroundMode.RequiresAmbient())
        {
            ambient = comparisonImage.TryAcquireAmbient();
            if (ambient is not null && !ambient.Blur.Equals(stage.AmbientBlur))
            {
                ambient.Dispose();
                ambient = null;
            }
        }

        return new TemporaryStagePresentation(stage, ambient);
    }

    public DecodedImage.AmbientLease? TakeAmbient() => Interlocked.Exchange(ref _ambient, null);

    public void Dispose() => Interlocked.Exchange(ref _ambient, null)?.Dispose();
}
