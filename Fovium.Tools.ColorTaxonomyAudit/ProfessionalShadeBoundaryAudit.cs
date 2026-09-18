using Fovium.ColorPicking;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal static class ProfessionalShadeBoundaryAudit
{
    private const double LightnessOffset = 0.006;
    private const double ChromaOffset = 0.004;
    private const double HueOffset = 1.0;

    public static IReadOnlyList<OwnerCandidateSample> Analyze(ProductionColorAdapter adapter)
    {
        var probes = new List<OwnerCandidateSample>();
        foreach (var definition in ProfessionalShadeCatalog.Definitions)
        {
            foreach (var region in definition.Regions)
            {
                AddRegionProbes(region, adapter, probes);
            }
        }

        return probes
            .OrderBy(item => item.Region, StringComparer.Ordinal)
            .ThenBy(item => item.Sample.Rgb.Packed)
            .ToArray();
    }

    private static void AddRegionProbes(
        ProfessionalShadeRegionDefinition region,
        ProductionColorAdapter adapter,
        ICollection<OwnerCandidateSample> probes)
    {
        var lightness = (region.MinimumLightness + Math.Min(region.MaximumLightness, 1.0)) / 2;
        var chroma = (region.MinimumChroma + region.MaximumChroma) / 2;
        var hue = CenterHue(region.MinimumHue, region.MaximumHue);

        Add("center", lightness, chroma, hue);
        Add("lightness-low-inside", region.MinimumLightness + LightnessOffset, chroma, hue);
        Add("lightness-low-outside", region.MinimumLightness - LightnessOffset, chroma, hue);
        Add("lightness-high-inside", region.MaximumLightness - LightnessOffset, chroma, hue);
        Add("lightness-high-outside", region.MaximumLightness + LightnessOffset, chroma, hue);
        Add("chroma-low-inside", lightness, region.MinimumChroma + ChromaOffset, hue);
        Add("chroma-low-outside", lightness, region.MinimumChroma - ChromaOffset, hue);
        Add("chroma-high-inside", lightness, region.MaximumChroma - ChromaOffset, hue);
        Add("chroma-high-outside", lightness, region.MaximumChroma + ChromaOffset, hue);
        var hueProbeChroma = Math.Min(chroma, region.MinimumChroma + 0.01);
        Add("hue-low-inside", lightness, hueProbeChroma, WrapHue(region.MinimumHue + HueOffset));
        Add("hue-low-outside", lightness, hueProbeChroma, WrapHue(region.MinimumHue - HueOffset));
        Add("hue-high-inside", lightness, hueProbeChroma, WrapHue(region.MaximumHue - HueOffset));
        Add("hue-high-outside", lightness, hueProbeChroma, WrapHue(region.MaximumHue + HueOffset));

        return;

        void Add(string suffix, double l, double c, double h)
        {
            if (l is < 0 or > 1 || c < 0 || !AuditSampling.TryOklchToSrgb(l, c, h, out var rgb))
            {
                return;
            }

            probes.Add(new OwnerCandidateSample($"{region.StableId}:{suffix}", adapter.Classify(rgb), null)
            {
                ProfessionalExplanation = adapter.ExplainProfessional(rgb)
            });
        }
    }

    private static double CenterHue(double minimum, double maximum)
    {
        var span = minimum <= maximum ? maximum - minimum : 360 - minimum + maximum;
        return WrapHue(minimum + span / 2);
    }

    private static double WrapHue(double hue) => (hue % 360 + 360) % 360;
}