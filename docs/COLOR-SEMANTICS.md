# Color Semantics

Role: Canonical owner for Fovium's reusable semantic color model and its developer report/explorer.
Read when: Changing OKLab/OKLCH math, broad families, Professional terms/regions, creative names, taxonomy research,
or generated color-semantic reports.
Authoritative for: Production semantic ownership, source-truth direction, structural-versus-creative naming,
report schema/gamut policy, core/deep evidence boundaries, and report commands.
Not authoritative for: Pixel acquisition, Color Inspector interaction/history, monitor transforms, or source decoding.

## Boundary and direction

`Fovium.ColorSemantics` is a logical subsystem inside the existing single production assembly. A separate assembly is
not justified: there is no independent deployment or package boundary. It owns locale-independent OKLab/OKLCH values,
perceptual roles/undertones/lightness/chroma classes, 45 broad families, 99 accepted Professional term IDs, 104 bounded
regions/lobes, deterministic specificity/precedence, EN/RU-facing stable identities, and the separate 1,800-anchor
creative nearest-name catalog.

The dependency direction is one-way:

```text
reference-sRGB source truth
    -> color-space math
    -> semantic interpretation
    -> localized human-readable information
```

Semantic names never feed decoding, RGB values, Color Management, photograph rendering, Stage styling, Histogram, or
pixel sampling. `Fovium.ColorPicking` remains the client that owns exact presented-pixel acquisition, alpha/accuracy,
click identity, session/history, input, and overlay presentation. Its small `ColorSampleSemantics` adapter maps an
opaque sampled RGB value into the common model and handles the Picker-only Transparent state. Research aliases,
candidate dispositions, provenance, source independence, clustering, overlap campaigns, and frontier metrics remain
developer-only in `Fovium.Tools.ColorTaxonomyAudit`.

## Two naming systems

The structural taxonomy and creative matcher share color-space math but not algorithms or authority:

- the structural classifier chooses explainable broad fallback families and bounded conventional Professional regions;
- the creative matcher chooses the nearest of 1,800 fixed OKLab anchors and supplies secondary descriptive names.

Neither changes the other. A Professional term may contain several regions under one stable identity; broad fallback
identity remains available. Stable term and region IDs, priorities, role gates, parent families, and exact bounds are
production truth. External datasets do not generate runtime definitions.

## Deterministic report and Explorer

`Fovium.Tools.ColorTaxonomyAudit report` builds one ordered `fovium-color-semantics-report/v2` model, then renders all
representations from it. The normal owner route is:

```powershell
pwsh eng/color-taxonomy.ps1
pwsh eng/color-taxonomy.ps1 -Open
```

The default ignored output is `artifacts/reports/color-semantics/`:

```text
index.html
taxonomy.json
analysis-summary.json
analysis-summary.md
summary.txt
summary.md
static/overview.svg
static/relations.svg
static/domains/*.svg
```

`-Png` uses an installed local Chrome only as an optional SVG-to-PNG renderer; Chrome is not a project dependency or a
Codex browser backend. `-StaticOnly` and `-HtmlOnly` select bounded output subsets. `-ResearchReport <summary.json>`
enriches the report from an existing audit, while `-Deep` first produces a fresh deep audit and uses the ignored
reference cache when present. A missing research report never prevents the production/core report.

The canonical JSON exposes three identities. `productionDefinition` hashes stable IDs, parents, roles, bounds,
priorities, precedence, and creative-catalog source RGB definitions using explicit decimal canonicalization.
`classificationOutcomes` hashes only discrete results for fixed source-RGB cohorts. `canonicalReport` hashes the full
normalized report. Commit identity and research evidence never enter the first two. Visualization `X/Y/Z`, timings,
and raw libm tail bits never decide whether production semantics changed. Semantic bounds use `1e-6`, derived geometry
uses `1e-9`, and serialized hue uses `1e-5°`; all are substantially finer than authored classifier resolution.

Compare two v2 reports without Git archaeology:

```powershell
pwsh eng/color-taxonomy.ps1 -CompareBefore artifacts/before/taxonomy.json `
  -CompareAfter artifacts/after/taxonomy.json -OutputDirectory artifacts/report-diff
```

The machine-readable and Markdown diff name added/removed terms and lobes, changed bounds/priority/parents/
representatives, fixed-cohort classification changes, warning changes, and research disposition changes.

## Geometry and gamut truth

The 3D mapping is `X = C cos(H)`, `Y = C sin(H)`, `Z = L`. The Explorer is a self-contained offline HTML/Canvas
document with rotation, zoom, pan, lightness/density controls, layer toggles, EN/RU display, domain filtering, search,
hover/click details, Professional cores/coverage, creative anchors, and research dispositions when present. It uses no
CDN or frontend framework.

The visible gamut is a deterministic 16×16×16 reference-sRGB cube (4,096 samples) transformed to OKLCH. It is a
visualization cohort, not universal semantic coverage evidence. Report v2 separately names the whole-spectrum RGB
outcome cohort, every-lobe reachability witnesses, boundary/counterexample probes, a dense near-neutral RGB cohort,
and optional creative/research anchors. Every hit rate states its cohort and denominator; none is called accuracy.
The vector overview uses six fixed lightness slices and visibly hatches unreachable mathematical OKLCH.

Every Professional lobe has a deterministic reachable winning witness. A separate representative maximizes a bounded
interior margin among winning probes and is intended for human display. Local stability reports fixed ±1…8 RGB-axis
retention, first transition step, and normalized region margin; it is a deterministic diagnostic, not a probability.
Raw OKLCH hue remains available, while `HueMeaningfulness` distinguishes undefined neutral hue, weak tint hue, and
strong chromatic hue without changing RGB or OKLCH math.

## Evidence, performance, and limits

On the R10-D implementation machine, core generation is about `0.5 s`; the v2 deep model/report is about `1.1 s` after
the deep audit and its canonical JSON is about `5.8 MiB`. `analysis-summary.json` is about `0.2 MiB` and intentionally
omits gamut geometry and creative anchors. The interactive scene uses
4,096 gamut points, 99 term cores, 104 lobe identities, and optional 1,800 creative anchors rather than DOM nodes per
sample. Viewer runtime and classifier hot paths are unchanged.

The overview, relations sheet, Blue domain sheet, and initial 3D Explorer scene were rendered through isolated local
headless Chrome and inspected at full resolution. DOM/JavaScript execution and offline asset references were checked.
This is not a claim of manual mouse/rotation acceptance or cross-browser/macOS UI validation.

Generated reports, PNGs, browser profiles, ignored reference corpora, owner images, and absolute/private paths never
belong in Git or deterministic signatures. Ordinary CI tests report structure and determinism but do not make semantic
coverage a quality percentage or gate. Deep evidence exports authoritative overlap/Dice/directional containment,
shadowing/reachability, core, boundary competitors, component provenance, and separate lexical, numeric, and
independent-numeric support. Vocabulary density is only the count of accepted terms in a domain.

R10-E adds three ignored-cache numeric source adapters. Human-survey rows are exact-alias only, malformed and duplicate
rows are rejected, and each source/candidate pair is deterministically capped at 128 anchors before clustering. This
keeps source size from becoming evidence weight. NBS/ISCC dictionary values share the existing `iscc-nbs` independence
group; mirrors never multiply support. Source-quality class, license/cache policy, and independence group are exported
separately rather than collapsed into a score. The accepted R10-E cores are authored production regions, not generated
centroids; rejected or deferred research components remain tooling evidence.
