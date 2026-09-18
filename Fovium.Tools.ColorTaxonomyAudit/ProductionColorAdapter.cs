using System.Globalization;
using Fovium.ColorSemantics;
using Fovium.Localization;

namespace Fovium.Tools.ColorTaxonomyAudit;

internal sealed class ProductionColorAdapter
{
    private readonly PerceptualColorNameResolver _resolver =
        new(Localizer.Create(CultureInfo.GetCultureInfo("en-US")));

    public AuditClassification Classify(AuditRgb rgb)
    {
        var description = PerceptualColorClassifier.Describe(rgb.Red, rgb.Green, rgb.Blue);
        var lab = OklabColor.FromSrgb(rgb.Red, rgb.Green, rgb.Blue);
        var oklch = description.Oklch!.Value;

        return new AuditClassification(
            rgb,
            lab.L,
            lab.A,
            lab.B,
            oklch.L,
            oklch.C,
            oklch.HueDegrees,
            description.Role!.Value.ToString(),
            description.Undertone!.Value.ToString(),
            description.HueFamily!.Value.ToString(),
            description.LightnessClass!.Value.ToString(),
            description.ChromaClass!.Value.ToString(),
            _resolver.ResolveShort(description),
            _resolver.ResolveDetailed(description))
        {
            ProfessionalTerm = description.ProfessionalTerm?.ToString(),
            Specificity = ResolveSpecificity(description)
        };
    }

    public static string ClassifyRole(double lightness, double chroma, double hueDegrees) =>
        PerceptualColorClassifier.ClassifyRole(new OklchColor(lightness, chroma, hueDegrees)).ToString();

    public static string ClassifyFamily(double lightness, double chroma, double hueDegrees) =>
        PerceptualColorClassifier.ClassifyHue(new OklchColor(lightness, chroma, hueDegrees)).ToString();

    public AuditProfessionalExplanation ExplainProfessional(AuditRgb rgb)
    {
        var color = OklchColor.FromSrgb(rgb.Red, rgb.Green, rgb.Blue);
        var role = PerceptualColorClassifier.ClassifyRole(color);
        var family = PerceptualColorClassifier.ClassifyHue(color);
        var explanation = ProfessionalShadeClassifier.Explain(color, role, family);
        return new AuditProfessionalExplanation(
            explanation.Winner?.Term.ToString(),
            explanation.Winner?.TermStableId,
            explanation.Winner?.RegionStableId,
            explanation.Candidates.Select(item => new AuditProfessionalRegionEvaluation(
                item.Term.ToString(),
                item.TermStableId,
                item.RegionStableId,
                item.Priority,
                item.Matched,
                item.FailureReason)).ToArray());
    }

    private static string ResolveSpecificity(PerceptualColorDescription description)
    {
        if (description.ProfessionalTerm is not null)
        {
            return "ProfessionalTerm";
        }

        if (description.Role is PerceptualColorRole.Neutral or
            PerceptualColorRole.NearBlack or
            PerceptualColorRole.NearWhite)
        {
            return "NeutralRole";
        }

        return description.HueFamily is
            PerceptualHueFamily.Greige or
            PerceptualHueFamily.Beige or
            PerceptualHueFamily.Sand or
            PerceptualHueFamily.Cream or
            PerceptualHueFamily.Peach or
            PerceptualHueFamily.Apricot or
            PerceptualHueFamily.Ochre or
            PerceptualHueFamily.Mustard or
            PerceptualHueFamily.Taupe or
            PerceptualHueFamily.Terracotta or
            PerceptualHueFamily.Mint or
            PerceptualHueFamily.RoseGray or
            PerceptualHueFamily.VioletGray or
            PerceptualHueFamily.LilacGray or
            PerceptualHueFamily.Burgundy or
            PerceptualHueFamily.Crimson or
            PerceptualHueFamily.Rose or
            PerceptualHueFamily.PinkLilac
            ? "ExistingSpecificFamily"
            : "GenericFamily";
    }
}