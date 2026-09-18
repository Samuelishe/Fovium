using System.Diagnostics;
using Fovium.ColorPicking;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal static class ProfessionalShadeBenchmark
{
    private const int SampleCount = 1024;

    public static double MeasureNanosecondsPerSample(int rounds = 256)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rounds);
        var samples = Enumerable.Range(0, SampleCount)
            .Select(index =>
            {
                var color = OklchColor.FromSrgb(
                    (byte)(index * 73),
                    (byte)(index * 151),
                    (byte)(index * 199));
                return (Color: color, Role: PerceptualColorClassifier.ClassifyRole(color),
                    Family: PerceptualColorClassifier.ClassifyHue(color));
            })
            .ToArray();

        Run(samples, 4);
        var stopwatch = Stopwatch.StartNew();
        Run(samples, rounds);
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalNanoseconds / (rounds * (double)samples.Length);
    }

    private static void Run(
        IReadOnlyList<(OklchColor Color, PerceptualColorRole Role, PerceptualHueFamily Family)> samples,
        int rounds)
    {
        for (var round = 0; round < rounds; round++)
        {
            foreach (var sample in samples)
            {
                _ = ProfessionalShadeClassifier.Classify(sample.Color, sample.Role, sample.Family);
            }
        }
    }
}