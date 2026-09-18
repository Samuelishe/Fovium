# Color taxonomy audit

Role: Reproducible developer audit route for the production Color Inspector taxonomy.
Read when: Changing perceptual roles, hue families, undertones, or naming-region thresholds.

The `Fovium.Tools.ColorTaxonomyAudit` executable calls the production OKLab/OKLCH classifier and English resolver
through an internal friend-assembly boundary. The viewer has no dependency on the tool, reference cache, reports, or
network. Generated data stays under ignored `artifacts/color-taxonomy-audit/`.

## Run

The default fixed seed is `0x5F0A2026`. Fast mode uses a 5°/0.05/0.02 in-gamut OKLCH grid, 25,000 deterministic RGB
samples, an RGB cube at step 32, and one-channel neighborhoods around discovered family boundaries:

```powershell
dotnet run --project Fovium.Tools.ColorTaxonomyAudit -c Release -- --mode fast `
  --output artifacts/color-taxonomy-audit/fast
```

Deep mode uses a 2°/0.025/0.01 grid, 150,000 deterministic samples, and RGB step 16:

```powershell
dotnet run --project Fovium.Tools.ColorTaxonomyAudit -c Release -- --mode deep `
  --output artifacts/color-taxonomy-audit/deep
```

Both modes are fully offline. Add an already-populated cache for reference-neighbor evidence, and compare a correction
against a retained machine report with the same mode and seed:

```powershell
dotnet run --project Fovium.Tools.ColorTaxonomyAudit -c Release -- --mode deep `
  --references artifacts/color-taxonomy-audit/references `
  --baseline artifacts/color-taxonomy-audit/before/summary.json `
  --output artifacts/color-taxonomy-audit/after
```

The default balanced semantic cohort is independent of the classifier's family result: it samples every 10° hue slice
across five representative lightness and five chroma bands, retains only in-gamut reference-sRGB points, and adds
neutral/near-black/near-white controls. Use additional fixed seeds only as post-tuning holdouts, for example:

```powershell
dotnet run --project Fovium.Tools.ColorTaxonomyAudit -c Release -- --mode deep `
  --seed 1779033703 --references artifacts/color-taxonomy-audit/references `
  --output artifacts/color-taxonomy-audit/holdout-b
```

Each run writes `summary.json`, `summary.md`, `summary.html`, `anomalies.csv`, `contact-sheet.svg`, balanced-spectrum,
family-profile, owner-candidate, holdout, changed-region, reference-disagreement, vocabulary-gap, accepted
professional-term, and professional-boundary views, plus reference-driven vocabulary-candidate and compact-component
views and a deterministic SHA-256 signature.
Runtime and the isolated indexed-classifier benchmark are retained in the reports but excluded from the signature.
Schema v5 reports generic/existing-specific/professional/neutral coverage, per-term sampled coverage, candidate source
support/dispersion/components/noise, the winning/competing region explanation for accepted anchors, and deterministic
center plus inside/outside L/C/h probes for every professional region. Reports rank abrupt semantic
neighbors, role/modifier reversals, lightness A→B→A paths, small connected components, thin slivers, broad family
coverage, distance-gated k-nearest reference disagreements, and per-family semantic profiles. These signals locate
regions for engineering review; a high score or a distant named anchor is not an automatic product verdict. Inspect the
rendered contact sheets, preserve the tuning seed, and require one or more unseen holdout seeds before accepting a
classifier correction. A vocabulary-gap candidate requires two distance-qualified datasets to repeat the same specific
term for a coordinate-driven generic sample; it is a research queue, not an automatic new product rule.

Candidate discovery uses an audit-only vocabulary normalizer that is intentionally broader than the shipped term enum.
This prevents current product vocabulary from defining its own research questions. Candidate profiles group recurring
names across datasets, report medoid/dispersion and production-family coverage, and deterministically extract compact
fixed-radius OKLab components before marking remote anchors as noise. They remain evidence for review rather than
generated runtime definitions. Region explanations identify the winning stable term/region and why competitors failed
(`parent-family`, `role`, `lightness`, `chroma`, or `hue`).

## Optional reference cache

Populate the ignored cache explicitly when network access is appropriate:

```powershell
pwsh ./eng/color-taxonomy-audit/fetch-references.ps1
```

The script records source, retrieval UTC time, usage/license note, pinned version where available, and SHA-256 in
`provenance.json`. Current inputs are:

- the official XKCD color-survey RGB export, used as crowd-language evidence and kept cache-only because the export has
  no explicit dataset license statement;
- the dated W3C CSS Color 4 named-color table, used as a small standardized sanity reference;
- `meodai/color-names` at commit `cc5fc08de437ea2522d32f751cecb4aa1e96f8e3` under MIT, treated as correlated
  secondary evidence because Fovium's creative catalog has the same upstream lineage;
- the 267 ISCC-NBS centroid names/RGB values from the pinned SLIB mirror at commit
  `05160e4ce21c65f99fea78dc4b29463e2c14bb22`; the file header grants redistribution, but the data remains cache-only;
- NBS Circular 553 from NIST, retained as methodology/reference vocabulary rather than parsed as sRGB anchors.

Names are normalized into broad audit-only semantic groups before k-nearest OKLab voting. Unknown names do not vote.
Dataset agreement is independent evidence, never ground truth: source vocabularies are uneven, CSS is intentionally
small, and nearest named anchors do not define an objective boundary. No downloaded dataset or generated report belongs
in Git or ordinary CI.
