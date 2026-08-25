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

## D-017 — Pre-release versions identify accepted checkpoints

Status: Accepted in CONTRACTS-R1.

Fovium's pre-release checkpoint line uses display versions `0.0.0.xxxx`. BUILD advances once per accepted coherent checkpoint—not per compile or failed attempt—and resets to `0000` after an explicit MAJOR, MINOR, or PATCH change. CONTRACTS-R1 establishes `0.0.0.0003`; CLR/file metadata may represent it numerically as `0.0.0.3`.

## D-018 — Application theme and photographic Stage are independent

Status: Accepted in CONTRACTS-R1.

Dark/Light controls application UI surfaces. Stage background and its independent Matte modifier control photographic presentation. Changing either system must not silently change the other or alter source pixels. D-030 owns the current Stage model.

## D-019 — Initial localization is English and Russian with English fallback

Status: Accepted in CONTRACTS-R1.

Fovium begins with `en` and `ru`, uses a matching supported OS locale until the user selects one, and falls back through English to a visible key plus diagnostic warning. Key-based external catalogs must allow additional locales without application redesign.

## D-020 — Multiple-file activation defaults to the explicit selection

Status: Accepted in CONTRACTS-R1.

When the operating system supplies multiple files, Fovium will by default browse only that ordered explicit sequence, including selections spanning directories. An Advanced setting may instead browse the directory containing the first supplied file; no implicit merge mode is defined.

## D-021 — File associations remain under operating-system/user control

Status: Accepted in CONTRACTS-R1.

Fovium may register supported types and participate in Open With, but it must never silently seize default associations. Any Settings shortcut hands control to the platform's association UI rather than bypassing its ownership model.

## D-022 — Thumbnail integration is isolated from the viewer UI

Status: Accepted in CONTRACTS-R1.

A future platform thumbnail provider may reuse bounded imaging capabilities but must not launch or depend on the full viewer UI. Existing good system providers remain preferred; when no thumbnail can be generated, the platform-registered Fovium image/document icon is the fallback rather than a fabricated thumbnail.

## D-023 — R1 production starts as one application assembly

Status: Accepted in R1.

The first production viewer is the single `Fovium` assembly. Logical application, navigation, loading, imaging, rendering, localization, and view responsibilities remain separated inside it. Production has no dependency on RenderProbe, and no extra production project is justified by R1 evidence.

## D-024 — Foreground publication is generation-owned and cache-bounded

Status: Accepted in R1.

Every sequence selection has explicit session/generation identity; cancellation is an efficiency mechanism, while generation equality controls publication. The previous display lease remains visible until an accepted replacement is ready. Adjacent speculative loads use a byte-accounted bounded LRU and may not displace the protected current image.

## D-025 — Retained drawing uses explicit native-image leases

Status: Accepted in R1.

Cache, displayed image, and retained Avalonia custom draw operations hold explicit reference-counted leases. Replacement, eviction, stale completion, and shutdown release their own ownership; the native Skia image is disposed only after the last render operation releases it. This isolates both lifetime and Avalonia's unstable Skia API at the rendering edge.

## D-026 — Navigation preserves non-Fit physical scale by default

Status: Accepted in R2.

Fovium defaults to **Keep current scale**: semantic Fit remains Fit, while 100% or manual views preserve physical scale and normalized point of interest across image changes. A deliberately reduced scale is not raised to Fit merely because the next image could fill more space. This behavior was discovered as useful during R1 manual use, supports sequence inspection and future Blink Compare, and never changes source pixels. Users may choose **Fit each image** instead; every new sequence still begins in Fit.

## D-027 — Black remains the default photographic Stage

Status: Accepted in R3.

Stage is a persisted presentation preference independent from application theme and viewport state. Black remains the immediate zero-cost default; Neutral uses the explicit non-calibrated presentation value `#505050`. Neither changes photograph pixels or sampling.

## D-028 — Ambient is a bounded optional derivative

Status: Accepted in R3.

Ambient is prepared asynchronously from the full oriented decoded photograph at a bounded `384 px` long edge, not from viewport zoom/pan. Photograph publication and adjacent decode outrank it; failure or stale work leaves a Black fallback. The optional native resource is attached to its decoded image and charged to the existing byte-budget cache rather than retained in an unlimited second cache.

## D-029 — Ambient + Matte preserves photograph geometry

Status: Accepted in R3; product-model portion superseded by D-030 in R3-F1.

Ambient and Ambient + Matte share one prepared Ambient representation. Matte is drawn behind the already-resolved photograph bounds at `24` physical pixels with a stable `#202020` neutral and no default shadow; it may clip at viewport edges and never shrinks or moves the photograph. Exact aesthetic constants remain refinable from later visual evidence.

## D-030 — Stage background and Matte are independent

Status: Accepted in R3-F1.

The Stage background is Black, Neutral, Custom, or Ambient. Matte is an independent modifier over every background; the former `AmbientMatte` value exists only in deterministic schema-v1 migration and maps to Ambient plus enabled Matte. Custom and Matte colors are opaque persisted values. Neither setting changes photograph geometry, sampling, or application theme.

## D-031 — Ambient separates spatial preparation from live color treatment

Status: Accepted in R3-F1.

Prepared Ambient owns oriented reduction and bounded blur and is identified by source plus blur. Brightness and saturation are bounded render-time color treatment and do not generate new retained images; Matte/color changes likewise do not prepare Ambient. Blur changes are coalesced and latest-wins. Owner review establishes brighter/more saturated defaults of `0.65` brightness and `0.85` saturation while keeping blur `18`.

## D-032 — Configurable controls use stable command and gesture identities

Status: Accepted in R3-F1.

Configurable commands persist locale-independent IDs and project-owned key/modifier gestures. Capture rejects unsupported gestures, bare modifiers, and reserved `Esc`. Conflicts require explicit confirmation; replacement clears the previous command instead of swapping, and an unassigned command remains valid. Controls is a first-class Settings section, and the trigger model leaves a bounded path for future hold/release actions without implementing them early.

## D-033 — Matte styles alter only outer presentation

Status: Accepted in R3-F2.

Matte is an independent presentation layer with an opaque color, a physical-pixel width, and an initial Solid, Rounded, Soft, or Angular outer style. The complete photograph remains rectangular and unmodified, with an opaque Matte backing beneath its entire destination for alpha compositing. Style and width are synchronous renderer geometry and never invalidate Ambient; this initial style catalog and its aesthetic ratios may be refined by later visual evidence.

## D-034 — Peek is temporary whole-viewport physical 100%

Status: Accepted in R4.

Peek is a cursor/source-anchored whole-viewport inspection at exact photographic 100%, not a magnifier, lens, split view, or persistent zoom. It snapshots Fit or manual physical scale plus normalized point of interest, allows only temporary pan, and restores the semantic snapshot under current geometry without accumulating transform drift.

## D-035 — Blink is a non-navigating retained comparison

Status: Accepted in R4.

Blink compares the current canonical image with the nearest previous viable image through a read-only retained acquisition; it does not call normal Previous/Next navigation or mutate current/requested index, generation, sequence, cache protection, or view policy. Fit remains Fit; manual physical scale and point of interest transfer. The retained current presentation returns immediately. A comparison uses its own already prepared matching Ambient or Black fallback, never the current image's Ambient.

## D-036 — Temporary inspection uses shared configurable hold input

Status: Accepted in R4.

Peek and Blink are configurable hold commands in the shared stable command/gesture system, defaulting to `Z` and `C` without stealing existing customized bindings. One transient inspection mode may own a primary key at a time. Repeat and competing holds are ignored; matching primary-key release ends the hold despite modifier changes; `Esc`, focus loss, persistent commands, sequence replacement, Settings/context-menu transitions, and shutdown restore it safely.
