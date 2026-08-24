# Decisions log

Role: Compact record of durable decisions already established.
Read when: A design question may already be settled, or a durable decision is being made or revised.
Authoritative for: Decision existence, rationale, status, and supersession history.
Not authoritative for: Current progress, implementation details not decided here, open risks, or future plans.

New entries should state the decision and why it constrains future work. Do not record hypotheses as accepted decisions; do not silently rewrite history—supersede an entry with a linked newer decision.

## D-001 — Viewer scope, not DAM/editor

Status: Accepted in DOCS-R1.

Fovium is a photographer-centric viewer. It will not become a DAM, catalog/import system, database-backed library, organizer, file manager, RAW editor, general media suite, or AI photo product. This keeps effort centered on display, inspection, and navigation quality.

## D-002 — Zero-UI is intentional

Status: Accepted in DOCS-R1.

The photograph is the primary UI. Persistent toolbars, navigation affordances, filename overlays, onboarding, and discoverability chrome are not default solutions. Rare actions use shortcuts, context menus, Settings, or requested temporary overlays.

## D-003 — Avalonia direction requires renderer validation

Status: Accepted in DOCS-R1; fulfilled by D-012 and D-013 in R0.

Avalonia is the current cross-platform framework direction, but its standard image path is not presumed sufficient. R0 must validate candidate Avalonia/Skia rendering paths for quality, DPI semantics, performance, and resource behavior before a final renderer decision.

## D-004 — Photographic 100% uses physical pixels

Status: Accepted in DOCS-R1.

At 100%, one oriented source image pixel maps approximately to one physical display pixel. A logical/DIP scale of `1.0` is not inherently photographic 100%.

## D-005 — No production code before the R0 decision

Status: Accepted in DOCS-R1; clarified in REPO-R1.

DOCS-R1 contains documentation only. Repository hygiene, diagnostics, tests for that tooling, and CI may precede R0, but the first application/runtime technical work is a bounded R0 probe. Production implementation direction follows its evidence and recorded decisions; no application project exists before R0.

## D-006 — Multiple codecs behind project-owned contracts

Status: Accepted in DOCS-R1.

Different decoder backends may serve different formats behind project-owned imaging contracts. Adding a backend must not force navigation, viewport, cache, or renderer redesign. This does not create a plugin system.

## D-007 — Automatic resource policy is runtime policy

Status: Accepted in DOCS-R1.

Automatic memory/cache and large-image policy uses current available resources, pressure, actual representation costs, and conservative product caps. It does not benchmark hardware once or identify every CPU/GPU model.

## D-008 — ProjectStats is disposable diagnostics

Status: Accepted in REPO-R1.

ProjectStats provides deterministic repository-level diagnostics, not semantic analysis or a quality gate. Its `project-stats.md` and `project-stats.json` outputs are local, generated, and untracked; changing totals do not belong in current-state documentation or CI acceptance thresholds.

## D-009 — Cross-platform CI starts at repository foundation

Status: Accepted in REPO-R1.

GitHub Actions restores, builds, and tests the solution on Windows, Linux, and macOS from the repository foundation onward. This checks tooling/test portability but does not prove future viewer runtime, rendering, DPI, or color correctness on those platforms.

## D-010 — Shipped assets require provenance

Status: Accepted in REPO-R1.

Source-controlled shipped and test assets live under `resources/`. Externally sourced or adapted assets require recorded source, author, license, and purpose before introduction; unverified downloads, generation dumps, user photographs, and runtime data are excluded.

## D-011 — Repository tooling does not benchmark hardware

Status: Accepted in REPO-R1.

ProjectStats describes repository structure only. It does not benchmark, inventory, or score developer hardware, and no hardware benchmark is part of repository-foundation tooling.

## D-012 — Avalonia is the initial desktop host

Status: Accepted in R0.

Avalonia 12.1.1 provided the required Windows windowing, input, custom-control, file-picker, runtime scaling notification, and Skia integration foundation in the bounded probe. It is accepted for the initial production vertical slice; Linux/macOS runtime correctness remains a validation obligation rather than an R0 claim.

## D-013 — Initial photographic renderer uses an isolated direct-Skia adapter

Status: Accepted in R0.

The initial photographic path uses explicit SkiaSharp sampling through a narrow Avalonia custom-draw adapter. Avalonia DrawingContext remains a diagnostic baseline, not the primary renderer. Avalonia marks the required Skia lease unstable, so this integration must remain isolated and replaceable.

## D-014 — Viewport state uses oriented coordinates and physical scale

Status: Accepted in R0.

Render-independent viewport state is expressed in oriented source coordinates, logical viewport geometry, active `RenderScaling`, and physical scale. `DipScale = PhysicalScale / RenderScaling`; photographic 100% sets physical scale to 1.0. Avalonia transform objects are not application state.

## D-015 — Initial JPEG/PNG foundation uses controlled SKCodec probing and decode

Status: Accepted in R0.

SKCodec supplies the initial bounded JPEG/PNG header/orientation/decode path. Production code will place it behind project-owned results, retain encoded source data until profile extraction is resolved, and must not treat the experiment's classes as final contracts. Broader formats may use other backends later.

## D-016 — Initial sampling uses one rendering path

Status: Accepted in R0.

Use nearest sampling for exact integer-pixel inspection and linear plus linear mipmaps for general Fit/downscale and interaction. R0 did not establish a visible/performance need for separate interactive and settled representations; cubic policies remain open to later evidence.
