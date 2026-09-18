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
perceptual roles/undertones/lightness/chroma classes, 45 broad families, 94 accepted Professional term IDs, 99 bounded
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

`Fovium.Tools.ColorTaxonomyAudit report` builds one ordered `fovium-color-semantics-report/v1` model, then renders all
representations from it. The normal owner route is:

```powershell
pwsh eng/color-taxonomy.ps1
pwsh eng/color-taxonomy.ps1 -Open
```

The default ignored output is `artifacts/reports/color-semantics/`:

```text
index.html
taxonomy.json
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

The canonical JSON uses stable ordered arrays and separate `productionSignature` and `reportSignature` SHA-256 values.
The production signature excludes commit identity and research evidence, so core and deep modes prove identical
runtime truth. Research fields are explicitly marked available/unavailable. Schema v1 is designed for future report
diffing; a diff CLI is intentionally deferred until a real comparison workflow justifies it.

## Geometry and gamut truth

The 3D mapping is `X = C cos(H)`, `Y = C sin(H)`, `Z = L`. The Explorer is a self-contained offline HTML/Canvas
document with rotation, zoom, pan, lightness/density controls, layer toggles, EN/RU display, domain filtering, search,
hover/click details, Professional cores/coverage, creative anchors, and research dispositions when present. It uses no
CDN or frontend framework.

The visible gamut is a deterministic 16×16×16 reference-sRGB cube (4,096 samples) transformed to OKLCH. Professional
coverage in 3D is drawn only from reachable winning source samples; exact region bounds remain in JSON and domain
sheets. The vector overview uses six fixed lightness slices and visibly hatches unreachable mathematical OKLCH. It must
never depict the full cylinder as attainable source color.

Every Professional term and lobe has a deterministic reachable winning core. The report first reuses canonical
inside/center probes, then bounded interior search and the sampled source cube for numerically narrow edge cases. This
is explainability evidence, not a new classification rule.

## Evidence, performance, and limits

On the R10-C implementation machine, core generation was about `0.25 s` and the full 13-file report about `5.4 MiB`;
loading the retained F12 250-candidate audit took about `0.7 s` and produced about `5.8 MiB`. The interactive scene uses
4,096 gamut points, 94 term cores, 99 lobe identities, and optional 1,800 creative anchors rather than DOM nodes per
sample. Viewer runtime and classifier hot paths are unchanged.

The overview, relations sheet, Blue domain sheet, and initial 3D Explorer scene were rendered through isolated local
headless Chrome and inspected at full resolution. DOM/JavaScript execution and offline asset references were checked.
This is not a claim of manual mouse/rotation acceptance or cross-browser/macOS UI validation.

Generated reports, PNGs, browser profiles, ignored reference corpora, owner images, and absolute/private paths never
belong in Git or deterministic signatures. Ordinary CI tests report structure and determinism but do not make semantic
coverage a quality percentage or gate.

