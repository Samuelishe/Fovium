# Color taxonomy audit

Role: Reproducible developer audit and report route for the production Color Semantics taxonomy.
Read when: Changing perceptual roles, hue families, undertones, naming-region thresholds, research evidence, or the
report/Explorer.

The `Fovium.Tools.ColorTaxonomyAudit` executable calls the production OKLab/OKLCH classifier and English resolver
through an internal friend-assembly boundary. The viewer has no dependency on the tool, reference cache, reports, or
network. Generated data stays under ignored `artifacts/color-taxonomy-audit/`.

## Run

Generate the normal production-truth report and self-contained Explorer with one owner command:

```powershell
pwsh eng/color-taxonomy.ps1
pwsh eng/color-taxonomy.ps1 -Open
```

Output is ignored under `artifacts/reports/color-semantics/`. Core mode needs no downloaded corpus. Use `-Deep` to run
the deep audit first, or `-ResearchReport <summary.json>` to enrich from an existing audit without recomputation.
`-Png` optionally renders the vector sheets through an installed local Chrome; it does not add a package/runtime
dependency. Canonical model, gamut, signature, and output contracts are owned by
[`../../docs/COLOR-SEMANTICS.md`](../../docs/COLOR-SEMANTICS.md).

The legacy audit command remains the topology/research campaign route:

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
views, a whole-catalog unshipped vocabulary-frontier view, current-campaign and F12 accepted/deferred sheets, a global
professional-overlap report, per-term core-confidence report, and a deterministic SHA-256 signature.
Runtime, master-lexicon research-clustering timing, and the isolated indexed-classifier benchmark are retained in the
reports but excluded from the signature.
Schema v8 reports generic/existing-specific/professional/neutral coverage, per-term sampled coverage, candidate source
support/dispersion/components/noise, the winning/competing region explanation for accepted anchors, and deterministic
center plus inside/outside L/C/h probes for every professional region. It additionally counts every deep sample matching
multiple professional terms, ranks winner/competitor pairs with sampled share and semantic-domain severity, and reports
matched/winning/shadowed volume per region. It also emits the 250-entry master candidate lexicon, semantic-domain
density, source-independence groups, dispositions, directional containment, Dice similarity, same-core warnings, and
per-term representative core/confidence. Audit-only aliases and research metadata organize discovery and conflict
triage; they do not affect production classification.
Reports rank abrupt semantic
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

The overlap report is a conflict diagnostic rather than a prohibition on related regions. Intentional sibling overlap is
valid when priority is unique, the winner is explainable, both regions remain reachable, and just-inside/outside
controls
remain coherent. Any shadowed region or equal-priority collision is a release-blocking catalog defect.

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
- Ridgway's 1912 color standards and Werner/Syme's 1821 nomenclature, public-domain/CC0 OCR retained as lexical-only
  historical evidence;
- Wikidata color items with sRGB color property P465 under CC0, retained as uncertain-independence anchors;
- Wiktionary Appendix:Colors under CC BY-SA/GFDL, retained as lexical-only evidence.

Every provenance entry records `independence`, `independenceGroup`, and `cachePolicy`; correlated mirrors sharing a
group count once. Names are normalized into broad audit-only semantic groups before k-nearest OKLab voting. Unknown
names do not vote.
Dataset agreement is independent evidence, never ground truth: source vocabularies are uneven, CSS is intentionally
small, and nearest named anchors do not define an objective boundary. No downloaded dataset or generated report belongs
in Git or ordinary CI.
