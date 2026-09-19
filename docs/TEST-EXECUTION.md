# Test execution

Role: Minimal command guide and verification contract for Fovium tests.
Read when: Running, filtering, adding, or interpreting automated tests.
Authoritative for: Normal local test commands, focused versus full-suite execution, Release verification, and CI test
expectations.
Not authoritative for: Test design details, coverage policy, UI/rendering acceptance, or current CI status.

## Commands

Run the full suite in the default configuration:

```powershell
dotnet test Fovium.sln
```

Run the focused HOME-UX-R1-F2 privacy, migration, Recent, thumbnail, carousel, settings, localization, command, and
structure contracts:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Fovium.Tests.Home|FullyQualifiedName~HomeSettingsTests|FullyQualifiedName~JsonSettingsStoreTests|FullyQualifiedName~HomeLocalizationTests|FullyQualifiedName~ViewerCommandExecutorTests|FullyQualifiedName~ShortcutDefaultsTests|FullyQualifiedName~FoviumVersionTests"
```

Ignored Windows runtime evidence under `artifacts/home-r1-f2/` covers a 20-location real MRU at beginning/middle/end,
physical mouse slow/strong drag, edge fades, activation after scrolling, resize at a nonzero offset, per-card removal,
context-menu grouping, and the complete privacy sequence. Turning memory off produced persisted count zero and cache
count/bytes zero; two photo activations plus restart while off still produced zero; enabling began empty; the next photo
produced exactly one item. The harness backs up and byte-verifies restoration of the per-user Settings document.

The final local HOME-UX-R1-F2 focused filter passes 160/160 and the full Release suite passes 2,324/2,324; the Release
solution build completes with zero warnings and errors. The deterministic native downloader suite passes 13/13. Pure
tests own threshold, velocity, clamp, decay, edge/input cancellation, migration, privacy, capacity, viewport scheduling,
cache generation, orientation, and stale-publication semantics; physical precision-touchpad and Linux/macOS feel remain
separate manual evidence.

`PhotoColorProfileTests` is also an explicit Rider diagnostic gate:

```powershell
dotnet build Fovium.sln -c Release
dotnet build Fovium.Tests/Fovium.Tests.csproj -c Debug --no-restore
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~PhotoColorProfileTests"
```

HOME-UX-R1-F2 records 11/11 passing tests and clean Debug/Release compiles. Rider's previous red diagnostics came from a
frontend/backend document synchronization failure (`backend document length 0`, expected 9680), followed by cascading
unresolved/ambiguous symbols. Reopening the file forced a full daemon analysis to `UP_TO_DATE` and cleared the errors;
no source, test-semantic, `.idea`, or cache deletion was needed.

Run the same verification used by CI after a Release build:

```powershell
dotnet restore Fovium.sln
dotnet build Fovium.sln -c Release --no-restore
dotnet test Fovium.sln -c Release --no-build
```

Run the focused SETTINGS-UX-R1-F1 Recent policy, canonical publication, color model/geometry, scroll edge, structure,
localization, persistence, and version contracts:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~RecentCapturePolicyTests|FullyQualifiedName~RecentNavigationCaptureTests|FullyQualifiedName~RecentCapturePolicySettingsTests|FullyQualifiedName~ColorSelectionModelTests|FullyQualifiedName~ColorWheelGeometryTests|FullyQualifiedName~SettingsScrollEdgeStateTests|FullyQualifiedName~SettingsEcosystemStructureTests|FullyQualifiedName~JsonSettingsStoreTests|FullyQualifiedName~LocalizationTests|FullyQualifiedName~FoviumVersionTests"
```

The final local filter passes 138/138 and the full Release suite passes 2,383/2,383; the preceding Release solution
build has zero warnings and errors. Four representative injected mutations were each killed by the narrow test named
for its contract: slideshow capture, Cancel returning the edited color, inverted Value-strip direction, and a top fade
visible at offset zero. The new 33 test methods contain 106 explicit assertions with no assertion-free method.

Ignored Windows evidence under `artifacts/settings-ux-r1-f1/` uses the production UI and a backed-up/restored real
settings document. It covers RU/EN General, Stage top/middle/bottom, Presentation mid-scroll, `760×560`, per-section
offset retention, non-overflow General, the HS wheel at red/cyan/warm/near-white/near-black, RGB/HSV/HEX input, marker
visibility, exact HEX edit, and picker ownership from both Settings and Viewer Markup. The attempted forced 1.25 scale
launch did not prove that Avalonia used a different RenderScaling. A real conflicting shortcut also opens the redesigned
owned Fovium dialog and Cancel leaves the existing owner intact. Real fractional-DPI remains unclaimed. No Fovium
process or changed AppData is left after capture.

Run the focused SETTINGS-UX-R1 language, startup, persistence, section, window-drag, and version contracts:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~SettingsWindowDragOriginTests|FullyQualifiedName~LanguageSelectionTests|FullyQualifiedName~LanguageSettingsTests|FullyQualifiedName~ApplicationStartupTests|FullyQualifiedName~SettingsSectionCatalogTests|FullyQualifiedName~FoviumVersionTests"
```

The local Windows runtime harness under ignored `artifacts/settings-ux-r1/` uses the production executable and a local
photograph, backs up/restores the real per-user settings file, and verifies RU/EN presentation, persisted language plus
restart application, all seven navigation sections, fullscreen Viewer ownership without global topmost, free-surface
drag, transparent-edge resize to `760×560`, and the project close button. SETTINGS-UX-R1 passes the 21-test focused
filter and 2,260/2,260 full Release tests; the preceding solution build completes with zero warnings and errors.

Run the focused BRAND-R1 asset, project wiring, window ownership, README local-target, and workflow-badge contracts:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --filter "FullyQualifiedName~Fovium.Tests.Branding"
pwsh eng/branding/verify-branding.ps1
```

The PowerShell verifier additionally builds and publishes the Windows apphost, extracts its Win32 icon through the
shell API, and requires the extracted pixels to match the project ICO. `capture-readme-hero.ps1` is a Windows visual
acceptance harness: it generates a deterministic synthetic image, opens it in the real Fovium runtime, and records
ignored zero-UI, Photo Info, and Settings-window evidence before writing the selected privacy-clean README hero.
The final local BRAND-R1 Windows Release run passes 2,241/2,241 tests; the preceding solution build completes with zero
warnings and errors. Hosted Windows/Linux/macOS proof follows only after an owner-authorized push.

Generated local evidence and build artifacts under ignored `artifacts/` are project-owned only where the cleanup
allowlist says so. Preview cleanup before applying it:

```powershell
pwsh eng/clean-artifacts.ps1 -Mode Safe -WhatIf
pwsh eng/clean-artifacts.ps1 -Mode AllGenerated -WhatIf
```

`Safe` removes known transient/test and stale stage-evidence paths, `Reports` adds known generated reports,
`NativeBuilds` targets reproducible native output trees, and explicit `AllGenerated` combines those scopes. Verified
download/package and pinned research caches require the additional `-IncludeCaches`; unknown top-level directories and
owner/source photographs are never targets. Apply an inspected plan with `-Confirm:$false`. Evidence harnesses that own
an output directory mark it and clean/recreate only that exact directory before regeneration.

Run only the current ProjectStats tests when iterating on repository tooling:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.ProjectStats"
```

Run only render-independent R0 logic tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.RenderProbe"
```

Run the isolated R8-B-P1 profile-validation, monitor-selection, transform, and source-domain invariance tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --filter "FullyQualifiedName~Fovium.Tests.ColorManagementProbe"
```

Run Stage settings, geometry, preparation, cache/lifetime, and offscreen composition tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Stage"
```

The Stage suite includes R5-P1/R5-P2 synchronization evidence that current Ambient preparation requires no
adjacent-progress signal, cached photo+Ambient installs as one viewport state, each ready neighbor can trigger
progressive preparation, mismatched Ambient rasterizes as Black rather than the wrong Stage, and obsolete current work
cannot publish over the latest selection. R5-P3 Loading tests model 30 distinct transitions through a five-resource
cache, prove speculative preload continues through repeated saturation, verify admission includes reclaimable LRU
capacity but excludes protected current, retain lease/disposal safety, and reject resources larger than the whole cache.
Render diagnostics separately distinguish viewport/custom-draw/Skia-lease boundaries. Owner visual acceptance
establishes the normal 3–4 photo/second envelope; deliberately faster 5–6+ photo/second behavior remains a documented
stress limitation rather than a unit-test timing threshold.

Run configurable-command, gesture-normalization, and command-execution tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Input"
```

Run R4 hold lifecycle, inspection acquisition, viewport transfer/restoration, and temporary Stage tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Input|FullyQualifiedName~Fovium.Tests.Loading.ViewerSessionTests|FullyQualifiedName~Fovium.Tests.Rendering.ViewportModelTests|FullyQualifiedName~Fovium.Tests.Viewer"
```

Run R5 through R5-F2 presenter history, oriented transform, partial-eraser/opacity raster, constraint geometry,
settings, input, and inspection tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Presentation|FullyQualifiedName~Fovium.Tests.Settings|FullyQualifiedName~Fovium.Tests.Input|FullyQualifiedName~Fovium.Tests.Viewer"
```

Run the production logic and boundary tests while iterating:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Application|FullyQualifiedName~Fovium.Tests.Navigation|FullyQualifiedName~Fovium.Tests.Loading|FullyQualifiedName~Fovium.Tests.Imaging|FullyQualifiedName~Fovium.Tests.Input|FullyQualifiedName~Fovium.Tests.Rendering|FullyQualifiedName~Fovium.Tests.Viewer|FullyQualifiedName~Fovium.Tests.Settings|FullyQualifiedName~Fovium.Tests.Localization|FullyQualifiedName~Fovium.Tests.Stage|FullyQualifiedName~Fovium.Tests.Versioning"
```

Run format capability, discovery, static WebP, bounded TIFF, HEIF/AVIF, Photo Info, Histogram, and Stage integration
tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Imaging|FullyQualifiedName~Fovium.Tests.Navigation|FullyQualifiedName~Fovium.Tests.Metadata|FullyQualifiedName~Fovium.Tests.Histogram|FullyQualifiedName~Fovium.Tests.Stage"
```

## Scope

Focused tests are preferred during implementation; run the full solution before handoff when shared tooling or project
configuration changes. The xUnit suite covers repository tooling and retained R0 logic plus production activation,
navigation, decoding, viewport/view-policy transfer, settings persistence, loading ownership, cache, memory policy,
localization, Stage geometry/preparation/publication/lifetime, DPI-aware Matte geometry and offscreen alpha composition,
version metadata, and native render-lease lifetime.

UI interaction, shortcut capture/conflict dialogs, rendering quality, runtime DPI, pixel alignment, color, native
lifetime, and platform behavior require bounded integration, visual, and manual smoke evidence; passing pure tests
cannot prove those properties. R4 adds deterministic hold/inspection coverage. R5 adds deterministic coverage for
highlight/settings toggles, all four initial markup tools, source-space transforms, image/session identity,
no-file-write semantics, panel lifecycle, Skia clipping, H/P migration, and Peek/Blink overlay selection. R5-F1 adds
history-cursor and per-image isolation tests; real raster assertions for partial Line/Brush/Rectangle/Arrow erasure,
chronology, draft cancellation, and photograph protection; explicit history/session limit tests; Arrow thick-stroke
regression coverage; and history-command migration/reset/execution coverage. R5-F2 adds pure multi-quadrant
45-degree/square/circle constraint tests, live constrained/freehand Brush draft transitions, immutable opacity capture,
Ellipse/history/clipping/partial-erase raster evidence, translucent source-over and full erasure checks, dock-visible
style-command gating, OEM-bracket adapter checks, and exact-pair shortcut evolution/customization tests. R5-F3 adds
scope precedence/cross-scope conflict tests, typed group coverage, effective-tooltip/menu-state models, shared hold
routing, Hand/history isolation, 128 px raster regression, cursor state and physical-DPI geometry matrices, and
normalized floating placement/settings round trips. R5-F3-P1 adds exact routing tests for
pointer/draft/dock/viewport/Stage dirtiness, focused session-notification evidence, transform-only pointer/dock
geometry, compositor photo-isolation configuration, and opt-in counter separation; actual smoothness remains a Release
manual observation. R5-P2 adds atomic viewport-state, progressive neighbor, direction-priority, actual Stage-draw
fallback counters, and mismatched-identity raster coverage. R5-P3 adds reclaim-aware saturation, protected-current/LRU
lifetime, and higher render-pipeline counter coverage; perceptual acceptance remains owner-corpus review rather than a
unit-test claim.

R6-A adds a self-authored runtime JPEG/EXIF APP1 fixture and adapter mapping tests for camera, lens, focal length,
aperture, shutter rational, ISO, and unspecified capture time; no-EXIF/malformed/partial recovery; pure sparse/localized
formatting; immediate oriented/file base data; lazy hidden-panel behavior; latest-wins asynchronous publication; bounded
LRU/reparse avoidance/new-sequence reset; additive `I` conflict preservation; normalized bottom-left placement; and
canonical/Blink/Peek presented-identity behavior. Private owner photographs remain manual-only evidence and are never
test fixtures.

R6-B adds deterministic BGRA channel/alpha fixtures, transparent exclusion and unpremultiplication assertions,
exact-versus-bounded deterministic sampling, retained-pixel lifetime, shared plot normalization, 128-entry LRU,
hidden/toggle/cancel/latest-wins/new-sequence coordinator behavior, Blink-like identity swap/cache restoration, Peek
stability, additive `G` conflict preservation, bottom-right placement, EN/RU chrome, and a non-threshold 24 MP
exact/sampled engineering timing smoke. Private photographs remain manual-only visual/performance evidence.

R7-A adds exact capability-table/extension/Skia-mapping invariants; mixed JPEG/PNG/WebP discovery and case-insensitive
extension checks; generated lossy/lossless/alpha/static/animated/oriented WebP containers; content-extension mismatch;
malformed/resource-limit/static-frame policy; retained Photo Info/metadata/Histogram/Ambient/Matte integration; and
non-threshold generated WebP probe/decode/preparation evidence. No private image is a test fixture.

R7-B adds an independent minimal uncompressed classic-TIFF byte fixture plus focused library-generated evidence. Tests
cover little/big endian, strips/tiles, None/LZW/Deflate/PackBits, grayscale polarity, associated/unassociated alpha, all
eight orientation tags, exact BGRA pixels, ICC-state truth, content-extension mismatch,
BigTIFF/multipage/high-bit/floating/specialist/unknown-extra rejection, corrupt/resource-bomb recovery, shared
cross-backend concurrency, mixed TIFF/Skia parallel stress, and generic Photo
Info/MetadataExtractor/Histogram/Ambient/Matte integration. Private/local TIFF photographs remain manual-only evidence.

R7-C adds tracked project-authored 8-bit HEIF/AVIF RGB, AVIF alpha, rotation, mirror, 10-bit, PQ, HLG, sequence, and
malformed fixtures. Tests exercise actual production interop and app-local loading; codec-derived HEIF/AVIF identity
despite misleading extensions; `.heic/.heif/.hif/.avif` discovery and case; representative pixels, alpha 0/partial/255
and single premultiplication; oriented pixels/dimensions with `Normal` descriptor orientation; encoded
10-bit/PQ/HLG/sequence rejection; pre-decode resource admission; corrupt containment; shared two-slot concurrency;
missing-runtime isolation; and generic Photo Info/Histogram/Ambient use of the same `DecodedImage`. Set
`FOVIUM_REQUIRE_HEIF_TEST_RUNTIME=1` to make absence of the materialized current-RID bundle a test failure rather than a
local skip.

CI restores, builds, and tests on Windows, Linux, and macOS. The accepted R7-B commit `d5de440` completed all three
hosted restore-build-test jobs successfully, including the managed TIFF suite. This proves the solution/test contract
and cross-platform managed decoder execution, not manual Avalonia/Skia viewer behavior on Linux/macOS.

R7-C-N1 and R7-C-N1-F1 are complete and owner-accepted at `c4dba80bd23534f372ae09f9285c0e1c5991d5e3`. The path-filtered
`native-libheif.yml` matrix builds pinned source, packages and audits the decode-only runtime, loads exact app-local
libheif, verifies HEVC/AV1 decoder presence and encoder absence, and decodes tracked project-authored HEIF/AVIF
fixtures. Its mandatory jobs are `win-x64`, `linux-x64`, and `osx-arm64`; no platform skip is accepted.

R7-C extends those jobs to materialize the built bundle, restore/build Fovium, and run the actual production
`HeifImageDecodeBackend` tests with runtime presence mandatory. Normal CI remains the fast managed matrix.
Cross-platform product acceptance requires both matrices green after owner push; local Windows success alone is not that
hosted claim.

R8-A adds deterministic in-memory tests for standard OKLab vectors; exact 1,800-entry embedded catalog integrity/basic
anchors; exact/near/tie name matching; ten-item duplicate-preserving FIFO; uppercase RGB (A)/HEX; alpha 255/128/1/0 and
one unpremultiplication; reference-sRGB direct/1×1 Skia/Approximate source states; containing-source-cell geometry with
exclusive edges and render scaling 1.00/1.25/1.50/2.00; all eight EXIF orientations; picker/temporary-Hand/markup
precedence; render-layer isolation; additive `K`; placement/settings non-persistence; menu state; and EN/RU chrome. The
observational performance smoke reports initialization and 1,000 clicks without defining an SLA. Windows Release
screenshot smoke covers empty, fixed sample, exactly ten rows after eleven duplicate clicks, hide/reopen retention,
JPEG/HEIF/WebP/AVIF/TIFF/HIF/PNG navigation, fullscreen, Peek/Blink input, temporary Hand, and picker precedence while
the Markup panel is visible. Real cross-platform/fractional-DPI pointer feel remains manual evidence.

R8-A-F2 adds pure per-click history identity, selection-without-mutation, duplicate selection, hide/reopen retention,
explicit Clear, and ten-entry capacity tests. Representative reference-sRGB vectors prove standard OKLCH conversion, hue
families, achromatic boundaries, monotonic lightness/chroma thresholds, transparent omission, the owner smoke colors,
EN/RU semantic composition, and English fallback without changing creative matching. UI-coupled coverage remains bounded
to XAML build validation and pure drag-origin exclusion for history/Clear/Close; horizontal balance, scrolling feel, and
real photograph terminology remain owner desktop review rather than pixel automation.

R8-A-F3 adds role/undertone vectors for near-black, near-white, near-neutral, tinted-neutral, and chromatic samples;
lightness-dependent neutral-curve assertions; exact compound-hue transition boundaries; and EN/RU visible-name parity.
A local ignored harness exercises the production decoder, retained-pixel sampler, OKLCH classifier, semantic resolver,
and unchanged creative matcher over bounded read-only owner photographs without shipping private image evidence. The
Photo Info latest-wins regression waits for an exact internal request-completion acknowledgement emitted after metrics
and cleanup instead of assuming `TaskCompletionSource.SetResult` runs the coordinator continuation inline.

R8-A-F4 adds exact boundary pairs for greige/beige/sand/ochre, peach/terracotta, cream-white, and
blue-gray/violet-gray/lilac-gray/rose-gray; accepted-region vectors protect coral, crimson, blue-violet, turquoise,
olive-green, near-white, blue-gray, and rose-gray behavior. EN/RU tests cover every new semantic term and the
role-dependent `Color tone`/`Undertone` detail. The ignored production-compatible harness adds 36 read-only sample
points across 9 owner photographs without shipping private paths, images, or output.

R8-A-F5 replaces ad-hoc color clicking as the primary engineering audit with the tracked
`Fovium.Tools.ColorTaxonomyAudit` route. Fast/deep profiles combine an in-gamut OKLCH grid, fixed-seed RGB Monte Carlo,
an RGB cube, and deterministic one-channel refinement around discovered family boundaries; reports and optional
downloaded reference caches remain ignored. Focused tests cover profile/seed determinism, OKLCH gamut conversion,
boundary refinement, reference normalization/parsing/provenance, deterministic report signatures, the corrected
yellow-brown region, exact neighboring thresholds, accepted olive/brown controls, and generated monotonicity properties.
Run and evidence semantics are documented in
[`../eng/color-taxonomy-audit/README.md`](../eng/color-taxonomy-audit/README.md); ordinary CI requires no network or
external dataset.

R8-A-F6 extends the same tool with a classifier-independent coordinate cohort across 10-degree hue slices, five
lightness bands, five chroma bands, neutrals, near-black, and near-white. Each reference dataset contributes bounded
distance-gated k-nearest OKLab evidence; per-family profiles, changed-region/owner/holdout contact sheets, and two
independent post-tuning seeds expose semantic coverage without making external names product truth. Focused regression
and exact-boundary pairs cover the evidenced Mint, coral/red-orange, terracotta/brown, burgundy/red-magenta, warm-rose,
and violet/magenta corrections plus unchanged accepted controls. Ordinary CI remains offline and does not download or
assert against external datasets.

R8-A-F7 adds audit schema v3 specificity counts for generic families, existing specific families, declarative
professional terms, and neutral roles. Distance-gated agreement on a more specific recurring reference term produces a
review candidate rather than a production verdict; dedicated vocabulary-gap and accepted-term contact sheets expose
the evidence. Focused tests require stable/unique/reachable definitions, unique priorities, EN/RU completeness,
independently sourced term anchors, accepted adjacent controls, and exact boundary behavior. Canonical and two unseen
holdout seeds must retain the topology/reference metrics while reducing generic-only coverage.

R8-A-F8 advances the audit to schema v4. Reference-name normalization now discovers recurring candidate terms without
depending on the shipped professional-term enum; the report groups their source coverage, medoid, dispersion, sampled
production families, and shipped status. It also records the winning region, losing competitors and failure dimensions
for accepted anchors, renders a vocabulary-candidate contact sheet, and benchmarks the indexed production lookup while
excluding timing from the deterministic signature. Focused tests additionally require every declared region to win at
an interior point, exercise both lobes of composite definitions, and pin both sides of meaningful hue/chroma edges.

R8-A-F9 advances the report to schema v5. Candidate profiles deterministically split recurring names into compact
fixed-radius OKLab components, report independent-dataset support and discarded noise, and render a separate component
contact sheet so multimodal names do not force one oversized production region. Every declared professional region also
gets deterministic center and inside/outside L/C/h probes in a machine-readable and rendered counterexample sheet.
Focused tests cover component determinism, remote-noise rejection, all-region probe coverage, sibling preservation,
new composite identities, exact supported anchors, EN/RU display strings, and boundary transitions. External caches
remain optional and ordinary CI remains offline.

R8-A-F10 advances the report to schema v6. Every deep sample records all matching professional terms, producing a
deterministic ranked winner/competitor overlap report plus per-region matched, winning, and shadowed counts. The report
also renders a campaign sheet for the newest accepted anchors. Focused tests require complete region accounting,
deterministic pair ranking, unique priorities, reachable composite lobes, independently justified anchors, EN/RU output,
and preserved sibling controls. Canonical and two post-tuning holdouts must retain topology metrics and report zero
shadowed regions; ordinary CI remains offline and does not consume generated reports or external caches.

R8-A-F11 advances the report to schema v7. Candidate vocabulary receives audit-only semantic-domain and alias
metadata, overlap pairs include sampled share plus semantic severity, and `vocabulary-frontier.html`/`.svg` render the
top unshipped candidates independently of current production coverage. Focused tests preserve unique/reachable regions,
EN/RU completeness, exact supported anchors, both sides of meaningful new boundaries, sibling fallbacks, alias
normalization, overlap severity, and report determinism. Canonical plus two holdout seeds must retain topology metrics;
external caches and generated reports remain optional, ignored, and absent from ordinary CI.

R8-A-F12 advances the report to schema v8. The ignored reference cache adds Ridgway 1912 and Werner 1821 public-domain
lexical evidence, CC0 Wikidata color anchors, and Wiktionary color vocabulary while recording independence group,
cache policy, retrieval, license note, and SHA-256. The 250-entry master lexicon ranks all known candidates and reports
aliases, provenance, independent-source count, anchors, compact components/noise, dispersion, family distribution,
nearest shipped term, RU candidate, and disposition. Overlap evidence adds directional containment, Dice similarity,
same-core warnings, and per-term robust-core confidence. Canonical and two untuned holdouts require 99 reachable
regions, zero shadowed regions, deterministic signatures, and unchanged broad-family topology. Reports separately
retain complete-audit runtime, master-lexicon clustering time, and indexed production-classifier nanoseconds per sample;
all three machine-local timings are excluded from the deterministic signature.

## R10-C Color Semantics and report/Explorer

The reusable semantic boundary and report pipeline have a focused namespace suite:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --filter "FullyQualifiedName~Fovium.Tests.ColorSemantics"
pwsh eng/color-taxonomy.ps1
pwsh eng/color-taxonomy.ps1 -ResearchReport artifacts/color-taxonomy-audit/f12-final-canonical/summary.json
```

The tests pin the pre-extraction schema-v8 fast-audit signature over 61,721 deterministic samples, verify 45 broad
families, 94 Professional terms, 99 regions, all stable IDs/order/cores, unchanged creative anchors and EN/RU values,
core/deep production-signature equivalence, deterministic JSON, reachable reference-sRGB geometry, local-only HTML,
semantic SVG structure, output modes, and missing-research degradation. Generated SVG/HTML pixels still require actual
visual inspection; exact giant-string snapshots are deliberately absent.

The retained mutation campaign proves that dropping one region fails the exact production inclusion test, accepting an
out-of-gamut OKLCH coordinate fails the gamut guard test, and shifting the Coral boundary fails the pinned whole-audit
semantic fingerprint. Every mutation is reverted before the normal focused/full run. Core reports are ordinary offline
tooling; ignored deep corpora and generated reports do not become CI quality gates.

## R10-D canonical identity, evidence, and report diff

Report schema v2 uses separate definition, fixed-RGB classification-outcome, and normalized-report signatures. The
cross-platform local gate runs the same Release DLL on Windows and WSL, generates two reports, and compares artifacts:

```powershell
pwsh eng/color-taxonomy.ps1 -OutputDirectory artifacts/r10d/windows
$repositoryWsl = (wsl -d Ubuntu-24.04 -- wslpath -a ((Get-Location).Path -replace '\\', '/')).Trim()
wsl -d Ubuntu-24.04 -- bash -lc "cd '$repositoryWsl' && \
  dotnet Fovium.Tools.ColorTaxonomyAudit/bin/Release/net10.0/Fovium.Tools.ColorTaxonomyAudit.dll \
  report --output artifacts/r10d/wsl --commit <same-sha> --static-only"
pwsh eng/color-taxonomy.ps1 -CompareBefore artifacts/r10d/windows/taxonomy.json `
  -CompareAfter artifacts/r10d/wsl/taxonomy.json -OutputDirectory artifacts/r10d/diff
```

An empty structured diff plus equal definition/outcome signatures is the semantic gate; OS-specific expected hashes are
forbidden. Focused tests cover explicit cohort denominators, every-lobe witnesses, separate representatives and local
stability, neutral/weak/strong hue meaningfulness, lexical-versus-numeric provenance, authoritative deep-overlap export,
compact-summary payload, v2 offline HTML, and field-level report diff. The 4,096-point visualization cloud is never used
as proof that every thin region was sampled.

## R10-E independent evidence and vocabulary wave

Source-adapter tests cover UW/LabintheWild RGB rows, Stanford human-speaker target HSL rows, NBS/ISCC dictionary rows,
malformed numerics, duplicate rows, exact aliases, compound contamination, provenance quality/independence, stable
ordering, and the 128-anchor per-source/term cap. The focused production tests require all five new terms to resolve
multiple independent anchors where available, retain EN/RU names, keep explicit L/C/H edges and semantic priorities,
preserve old sibling anchors, and leave all 104 lobes reachable with unique IDs and priorities.

```powershell
pwsh eng/color-taxonomy-audit/fetch-references.ps1 -CacheDirectory artifacts/r10e/references
pwsh eng/color-taxonomy.ps1 -Deep -ReferenceDirectory artifacts/r10e/references `
  -OutputDirectory artifacts/r10e/final -Png
pwsh eng/color-taxonomy.ps1 -CompareBefore artifacts/r10e/baseline/taxonomy.json `
  -CompareAfter artifacts/r10e/final/taxonomy.json -OutputDirectory artifacts/r10e/diff
```

The generated corpus and reports remain ignored. Hosted Windows/Linux/macOS proof follows only after an owner-approved
push; local tests do not claim macOS visual acceptance.

The R8-A-F12 Histogram regression does not assume that completing a test reader runs the coordinator continuation
inline. Tests subscribe to an internal post-classification/post-cleanup completion signal keyed by image identity and
outcome, then assert stale metrics and unchanged latest publication without sleeps or retries. The production
generation, image-identity, presentation-identity, cancellation, and cleanup lifecycle remains the authority.

R8-B-P1 adds deterministic in-memory tests for bounded ICC display-profile validation, content identity, active-monitor
largest-intersection/tie behavior, typed fallback precedence, complete transform-key equality, Skia destination/alpha
behavior, and source-versus-destination ownership. The final domain-independence test proves that two destination
transforms differ while R8-A reference-sRGB Picker output and source-domain Histogram bins remain unchanged. Platform
APIs and real display-profile availability remain probe/manual evidence rather than skipped ordinary unit tests.
HOME-UX-R1-F2 hardens native source acquisition with deterministic Python unit tests that inject network/cache behavior
without
contacting upstream:

```powershell
python -m unittest discover -s eng/native/libheif/tests -p 'test_*.py' -v
```

The 13 cases cover primary success, bounded transient recovery, permanent 404, primary exhaustion to fallback,
connection
reset before/after partial output, terminal downloaded hash mismatch, verified cache reuse, invalid-cache replacement,
stale-part removal, and both sources failing. Pinned URLs/versions/SHA-256 remain owned by `versions.json`; a fresh hash
mismatch is terminal, and only a validated `.part` is atomically promoted. A clean live smoke acquired the exact dav1d
1.5.4 archive from VideoLAN's official read-only GitHub mirror and matched its pinned SHA-256. Hosted matrix success is
not claimed before a future owner push.

Run a clean native build locally with:

```powershell
pwsh eng/native/libheif/build.ps1 -Rid win-x64
```

or on a matching Unix host:

```bash
bash eng/native/libheif/build.sh linux-x64
bash eng/native/libheif/build.sh osx-arm64
```

The scripts fail on host/RID mismatch, archive hash mismatch, non-local libheif loading, missing HEVC/AV1 decoders,
present HEVC/AV1 encoders, forbidden codec/developer-path dependencies, or fixture decode failure. macOS additionally
requires exact app-local install names, `@loader_path`, the configured deployment target, and the RID architecture.
Smoke runs with the original build prefix renamed out of reach. Per-RID `manifest.json`, `dependency-audit.txt`, and
`smoke-report.txt` are the detailed evidence owners.

## R8-B-W1 Monitor Color Management

Run production color tests with the accepted current-RID artifact materialized:

```powershell
$env:FOVIUM_REQUIRE_LCMS_TEST_RUNTIME = '1'
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --filter "FullyQualifiedName~Fovium.Tests.ColorManagement"
```

The project-authored fixtures under `eng/native/lcms2/fixtures/` are generated with pinned Little CMS 2.19 and prove
both a matrix/TRC RGB display destination and a real `BToA0` CLUT RGB display destination. Tests verify app-local
path/version, exact BGRA patches, relative colorimetric/BPC-off policy, alpha 255/128/1/0, untagged final device pixels,
Display-P3→full-source reference-sRGB normalization, canonical-source immutability, 16 MiB admission, monitor
geometry/ties, output-state identity, source fallback, same-size managed-source ownership, geometry-free
source/destination keys, one CMM operation across 50 Fit/zoom/pan/resize/100% frames, shared EXIF orientation and center
geometry, source/destination latest-wins races, stable exact 100%, shutdown during native work, and absence of
presented-source events on destination change. F4 additionally blocks the managed renderer to prove first-load
Stage-only behavior, portrait/landscape photo+Matte/Ambient atomicity, A→B→C and reverse latest-wins behavior,
recoverable fallback publication, and unchanged Picker/Histogram/Photo Info/markup authority while a target is pending.
The require variable turns missing runtime into failure; ordinary CI may exercise fake/pure paths without building
native code.

The separate `native-lcms2.yml` matrix builds/audits/smokes `win-x64`, `linux-x64`, and `osx-arm64`, materializes each
bundle into Fovium output, verifies final hashes, and runs the actual production interop tests. This is cross-platform
engine evidence, not physical-monitor support outside Windows. Local Windows smoke additionally records anonymized real
HWND/profile state with `FOVIUM_COLOR_DIAGNOSTICS=1`; full profile paths are forbidden.

## R9-A Photo Presentation View

Run the focused layout, command, persistence, and F4 publication regressions with:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --filter "FullyQualifiedName~PhotoPresentation|FullyQualifiedName~AtomicManagedPhotoTransitionTests|FullyQualifiedName~ViewerCommandExecutorTests|FullyQualifiedName~SettingsServiceTests"
```

The suite covers seven required aspect ratios across RenderScaling `1.00`/`1.25`/`1.50`/`2.00`, photograph-scale
invariance under Matte off/on, width, Solid/Rounded/Soft/Angular style, and color, physical percentage margins, no
upscale beyond photographic 100%, tiny-window safety, independent orientation layout, separate margin/resize/fullscreen
recalculation, session-only activation, additive `F6` conflict handling, geometry-input suppression/restoration, and
explicit Blink unavailability. R9-A-F2's OWNER-video regression repeats portrait Matte widths `32→64→100→147→32` with
exact photo destination/scale equality while separate Stage geometry changes. Controlled F4 publication retains the old
portrait/landscape layout until matching pixels are ready and atomically switches photo, Matte, Picker/Histogram/Photo
Info/markup authority with zero geometry CMM requests. Release manual smoke remains required for visual balance,
context-menu state, fullscreen, and return to ordinary Fit/zoom.

## R9-B Slideshow

The deterministic controller/sequence/prepared-next/input/settings suite uses controlled schedulers and completion
sources; it must not sleep for configured slide durations:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --filter "FullyQualifiedName~Slideshow|FullyQualifiedName~ViewerEscapePolicy|FullyQualifiedName~ImageSequenceTests|FullyQualifiedName~ViewerSessionTests|FullyQualifiedName~ViewerCommandExecutorTests|FullyQualifiedName~SettingsServiceTests|FullyQualifiedName~LocalizationTests"
```

Real local decode/CMM cadence and retained-byte evidence is opt-in and uses ignored local imaging assets. Separate paths
with the platform path separator:

```powershell
$env:FOVIUM_SLIDESHOW_PERF_IMAGES = 'C:\path\15mp.png;C:\path\24mp.png'
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --filter "FullyQualifiedName~SlideshowPerformanceSmokeTests" --logger "console;verbosity=detailed"
```

`FOVIUM_SLIDESHOW_DIAGNOSTICS=1` prints bounded starts/stops/natural stops/loops, expirations, manual resets, presented
count, prepared hits/misses/rejections/stale results, last visible duration/transition wait, and current/prepared
managed bytes at viewer shutdown. These counters and local screenshots/logs are diagnostic evidence, not committed
resources. Desktop acceptance remains a real Windows mixed-orientation run covering F5, checked menu, live Settings,
Stop-at-end, Loop, Right Arrow, F11, Esc, Matte continuity, and zero geometry-only CMM work.

R9-B-F1 additionally proves that Slideshow has no `PhotoPresentationViewSession` dependency, all four Slideshow/Photo
Presentation state combinations remain independent, external F6/Settings/context-menu view changes do not stop or rearm
the countdown, and Normal Viewer continues to own Keep-current-scale/Fit-each-image transfer. The main viewport control
test requires both logical focusability and a locally assigned null `FocusAdorner`; Windows Release stress repeats F5
plus Left/Right with F6, F11, context-menu, Settings, and focus loss/reacquisition to check that no inner viewport focus
rectangle appears.

## R10 photo-derived styling

Run the focused analysis, policy, identity, Stage, settings, and viewer regressions with:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --filter "FullyQualifiedName~PhotoStyling|FullyQualifiedName~PhotoDerived|FullyQualifiedName~StageViewportInvarianceTests|FullyQualifiedName~StageSettingsTests|FullyQualifiedName~JsonSettingsStoreTests|FullyQualifiedName~LocalizationTests"
```

The suite proves bounded 96-pixel oriented analysis, linear-light alpha-aware Average, unchanged raw palette population,
substantial representative Dominant selection, tiny-accent admission, neutral/dark/high-key outcomes, stable family
ordering, a deterministic `6×6` spatial summary, decoded-cache byte accounting, cancellation/stale suppression, shared
Dominant Matte authority, bounded Color Wash normalization, 64×64 smooth wash ownership, one-physical-pixel Hairline
policy, exact source identity and fallback, A→B→C/Blink/Peek authority, geometry-only reuse, persistence, localization,
and unchanged Picker/Histogram domains. Real-image timing and ignored PNG visual artifacts are opt-in:

```powershell
$env:FOVIUM_PHOTO_STYLE_PERF_IMAGES = 'C:\path\landscape.jpg;C:\path\portrait.jpg;C:\path\24mp.png'
$env:FOVIUM_PHOTO_STYLE_SMOKE_OUTPUT = 'C:\path\ignored-output'
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --filter "FullyQualifiedName~PhotoDerivedStylingPerformanceSmokeTests" --logger "console;verbosity=detailed"
```

R10-B extends the same suite with strongest-axis and fixed radial policies, bounded 32×32 raster generation, exact
identity and missing-raster fallback, deterministic pixels, 1.00/1.25/1.50/2.00 scale invariance, transparent/high-key/
low-key/neutral cases, additive schema-v2 persistence, EN/RU parity, decoded-cache accounting, navigation/Blink/Peek
ownership, source-domain Picker/Histogram independence, and one CMM operation across repeated style/scaling draws. The
opt-in smoke reports median raster preparation, median 1280×800 Stage-only render cost against Neutral/Color Wash,
and writes only ignored visual artifacts when the output variable is set. A no-restore WSL2 Ubuntu 24.04 run of the
focused policy/cache/renderer/invariance filter passes 236 tests from the same Release output. The WSLg viewer enters
its
event loop, but the current host cannot capture the RDP/GPU surface for visual inspection; this is automated portability
and launch evidence, not Linux human visual acceptance.

## R11-A semantic Photo Color Profile

Run the focused semantic projection, lifecycle, Photo Info, inspection, cache, localization, and source-domain checks:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --filter "FullyQualifiedName~PhotoColorProfile|FullyQualifiedName~PhotoInfoCoordinatorTests|FullyQualifiedName~ImageDecoderPhotoStyleTests|FullyQualifiedName~PhotoStyleCacheTests|FullyQualifiedName~ViewerInspectionCoordinatorTests|FullyQualifiedName~ColorDomainIndependenceTests|FullyQualifiedName~LocalizationTests"
```

The tests cover Professional-first/broad-fallback naming, creative-name separation, exact RGB and raw palette order,
weights including sub-percent display, transparent and low-information outcomes, one analysis/projection, decoded-cache
accounting, hide/show and geometry reuse, A→B→C latest-wins, actual Blink comparison and canonical restoration, Peek,
projection failure fallback, EN/RU parity, and unchanged Picker/Histogram source truth.

Real-photo timing and the ignored profile contact sheet are opt-in:

```powershell
$env:FOVIUM_PHOTO_COLOR_PROFILE_IMAGES = 'C:\path\high-key.jpg;C:\path\low-key.jpg;C:\path\portrait.jpg'
$env:FOVIUM_PHOTO_COLOR_PROFILE_OUTPUT = 'artifacts/reports/photo-color-profile-r11a'
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --filter "FullyQualifiedName~PhotoColorProfilePerformanceSmokeTests" --logger "console;verbosity=detailed"
```

The final local R11-A run passes 2,200/2,200 Release tests on Windows; the focused 84-test semantic/profile filter also
passes under WSL2 Ubuntu 24.04. This is automated portability evidence, not Linux or macOS visual acceptance.

## R11-B notable Photo Colors

Run the bounded selector, semantic projection, exact-image lifecycle, cache accounting, localization, and source-domain
checks through the VSTest route:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --filter "FullyQualifiedName~PhotoStyleAnalyzerTests|FullyQualifiedName~PhotoColorProfile|FullyQualifiedName~PhotoInfoCoordinatorTests|FullyQualifiedName~ImageDecoderPhotoStyleTests|FullyQualifiedName~PhotoStyleCacheTests|FullyQualifiedName~ViewerInspectionCoordinatorTests|FullyQualifiedName~ColorDomainIndependenceTests|FullyQualifiedName~LocalizationTests"
```

The selector tests keep raw top-five frequency order independent from Notable output, consolidate neighboring warm
shades, admit coherent minority and repeated flower-like regions, reject a single-pixel contaminant, and produce no
Notable output for uniform or fully transparent input. Integration tests carry distinct Notable values through the
single off-UI analysis/profile attachment, retained-byte accounting, hide/show and geometry reuse, A→B→C publication,
cached Blink hold/release, and Peek's unchanged canonical identity. Semantic tests keep structural descriptions primary
and creative nearest names secondary; EN/RU keys and labels have exact parity.

The real-photo evidence route is the existing opt-in smoke with a local path-separated corpus. R11-B's 14-photo review
set includes cat/greenery, orange transport, pallet/industrial neutrals, several flower scales, brick/rust, blue-sky
industry, wet reflections, a red tram control, and a saturated sign. The ignored review bundle contains the pre-R11-B
and final contact sheets; it must never be committed or copied into tracked resources.

The final local Windows verification passes the 105-test focused filter above and 2,210/2,210 tests for
`dotnet test Fovium.sln -c Release --no-build`; the preceding Release build completes with zero warnings and errors.
Hosted CI and non-Windows runtime/visual acceptance remain pending.

## R11-B-F1 robust and adaptive Notable Colors

R11-B-F1 keeps the R11-B command above and adds generalized route, diagnostics, adaptive-capacity, and two-row layout
contracts. Synthetic cases cover a muted warm mass on green, compact saturated red, a light neutral on dark surround,
distributed brick stripes, mustard against burgundy, chromatic information in a neutral crowd, sparse noise, uniform
input, close-shade consolidation, and two perceptually separate colors in one broad family. The developer-only evidence
result exposes candidate features, admission/rejection, rank, presentation disposition, and phase timings; production
`DecodedImage` retains only final colors.

The opt-in real-photo harness writes a marked, self-cleaning ignored directory containing
`photo-color-profile-contact-sheet.png`, `notable-diagnostics.png`, and `notable-diagnostics.json`. The accepted local
protocol freezes coefficients after the known-class tuning set, then selects and inspects a fresh holdout without
changing coefficients for isolated misses. R11-B-F1 used 16 tuning photographs and a separately selected 20-photo
three-date holdout. Evidence remains local and ignored; photographs and absolute owner paths must not enter tracked
files.

The final local Windows verification passes the expanded 199-test targeted filter and 2,229/2,229 tests for
`dotnet test Fovium.sln -c Release --no-build`; the preceding Release solution build completes with zero warnings and
errors. Eleven representative injected mutations are killed and exactly reverted. Hosted CI and non-Windows
runtime/visual acceptance remain pending.
