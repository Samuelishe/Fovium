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

Status: Accepted in R3; scheduling priority superseded by D-041 in R5-P1.

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

## D-037 — Presenter markup is image-bound session state

Status: Superseded by D-039 in R5-F1.

Brush, Line, Rectangle, and Arrow primitives live only in bounded managed memory keyed by image identity and use oriented source coordinates. Rendering transforms them through the existing destination after the photograph; Peek keeps current identity and Blink selects comparison identity. Hiding tools preserves marks, Clear removes only the current image, and new sequence/session clears all. No source write, sidecar, save/export, document format, undo/editing/layers, or second compositor is introduced.

## D-038 — Presenter controls remain explicit temporary chrome

Status: Accepted in R5.

Cursor Highlight and Markup Tools are shared configurable press commands defaulting to `H` and `P` without stealing occupied gestures. Highlight is viewport-space, physical-radius, and hides the system cursor only over the viewer. The compact markup dock appears only on request and its mouse controls do not take viewer keyboard focus; it is not a persistent toolbar or general annotation UI.

## D-039 — Markup is an ordered, erasable, per-image operation history

Status: Accepted in R5-F1.

Each image-bound memory-only markup document has one bounded chronological operation history and one cursor; active rendering replays Draw, Erase, and Clear operations before that cursor. A freehand Eraser uses Clear blending only inside a destination-bounded isolated transparent markup layer, so it removes crossed markup pixels without mutating photograph, Stage, or Matte. One continuous draw/erase gesture is one history step, Clear is an undoable operation, Undo/Redo remain per image, and a new edit after Undo releases the redo tail. This foundation does not introduce source writes, sidecars, export, object editing, layers, or a general annotation document format.

## D-040 — Markup opacity and constraints are captured drawing semantics

Status: Accepted in R5-F2.

Draw operations capture immutable color, source-space width, and opacity; opacity applies to Brush, Line, Rectangle, Ellipse, and Arrow but never weakens Eraser. Ellipse is a two-control-point oriented-source shape. A project-owned pure geometry helper constrains Line/Arrow/Brush to 45-degree directions and Rectangle/Ellipse to square/circle geometry; Shift is translated only at the Avalonia boundary, and releasing it can restore the collected freehand Brush draft before commit. Contextual `[`/`]` and `Ctrl+[`/`Ctrl+]` commands adjust active dock thickness/opacity, Clear defaults to `C`, and Blink moves to `Shift+C`, leaving `Ctrl+C` available for a future conventional Copy command. No dedicated Highlighter, contextual tool-selection scheme, or toolbar redesign is introduced.

## D-041 — Matching current Ambient outranks speculative neighbor work

Status: Accepted through owner review of R5-P3.

The canonical photograph still publishes before decoration. Its matching current-image Ambient is user-visible presentation work and never waits for adjacent preload; later neighbor work remains speculative. R5-P2 refines publication so a cached matching Ambient and photograph enter the viewport atomically, while progressively ready neighbors may prepare without a full-neighbor barrier. A genuine miss uses Black rather than a mismatched previous-image derivative, and latest-wins source/blur authority rejects stale publication. No fade, cache-budget increase, lower resolution, or previous-image fallback is introduced. Owner review accepts normal human browsing after R5-P3; extreme rapid navigation may still outrun readiness and expose the identity-safe Black fallback.

## D-042 — Speculative preload admits against reclaimable LRU capacity

Status: Accepted in R5-P3.

Speculative retained admission is based on the existing cache budget after reserving the protected current entry, not only on currently unused bytes. Old non-protected LRU entries are reclaimable, while decode working allowance remains separately bounded and `ByteBudgetCache.Add` performs the final race-safe eviction/admission check. This keeps adjacent preload alive at steady-state saturation without increasing the global cache, weakening current-image protection, or increasing decode concurrency.

## D-043 — Presenter interaction uses contextual controls and client-relative floating UI

Status: Accepted in R5-F3.

Viewer commands own a semantic group and one code-owned scope: Global, Highlight, or Markup. Resolution is deterministic—Markup, then Highlight, then Global—and the same gesture may be reused across contextual scopes without conflict; conflicts remain enforced within a scope. Markup interaction cursors replace the system arrow over the photograph: Brush previews captured physical size/color/opacity, Eraser shows its effective diameter, shapes use a precision target, and Hand exposes the existing pan behavior without history. `Space` temporarily selects Hand through the shared safe hold lifecycle. Markup size is bounded to `1–128` physical pixels. The presenter dock uses project-owned vector icons and a persisted normalized client-relative placement that clamps through resize/fullscreen; Controls grouping is driven by typed command metadata rather than ID parsing. This foundation does not implement metadata, EXIF, Histogram, text, edit handles, export, or layers.

## D-044 — Interaction rendering is isolated by update frequency

Status: Accepted in R5-F3-P1.

Photographic Stage/photo presentation, image-bound markup, pointer feedback, and floating secondary UI are separate render-frequency layers. Avalonia retains the low-frequency direct-Skia photographic visual in one viewport-sized compositor bitmap so damage from transparent overlays does not re-enter photo drawing. Markup consumes immutable snapshots in its own composition visual; pointer movement changes only a small control transform after style geometry is resolved; floating panels use transform-only live drag and persist normalized placement once on release. High-frequency pointer/draft/dock paths must not refresh the photographic presentation or rebuild toolbar state. The cache is display-sized, not a source-resolution annotation surface, and does not alter decoded-image cache policy.

## D-045 — Photo Info follows retained presented-image identity

Status: Accepted in R6-A.

Photo Info is a requested, movable, zero-layout overlay defaulting to configurable `I`; visibility is session-local and starts hidden, while normalized client-relative placement persists. Metadata parsing is lazy, background, read-only, and independently fallible. A focused MetadataExtractor adapter maps exact retained encoded bytes into immutable Fovium camera/lens/exposure/date models; third-party directory/tag types never escape. A bounded session LRU and request generation prevent reparsing and stale publication. The overlay observes/acquires the photograph actually presented, so Blink switches identity while Peek does not. Metadata never reopens or writes the source, creates sidecars, changes decode validity, or alters ICC/rendering policy.

## D-046 — Histogram is lazy whole-image decoded-RGB analysis

Status: Accepted in R6-B.

Histogram is an on-demand movable zero-layout overlay defaulting to configurable `G`; visibility is session-local and starts hidden, while normalized bottom-right placement persists. It observes the retained actually presented canonical/Blink image and ignores Peek/zoom/pan because it describes whole-image decoded RGB values rather than viewport, Stage, markup, or display-output pixels. One cancellable worker acquires the existing decoded native payload without re-read, re-decode, or full-image copy. BGRA8888/Premul samples exclude alpha zero and unpremultiply partial alpha; large images use a deterministic whole-image grid bounded to two million locations. A 128-entry session LRU and latest-wins generation prevent repeat work and stale publication. The initial plot uses one linear shared R/G/B maximum; it is not an editing control or an sRGB/monitor-output correctness claim.

## D-047 — Format capability and content truth are project-owned

Status: Accepted in R7-A.

Fovium owns stable format identity and one immutable capability authority independent of decoder-backend enums. Directory discovery and file-picker patterns derive known extension hints from it, while actual encoded content detected by the decoder determines format truth. The accepted table is JPEG, PNG, and static lossy/lossless/alpha WebP through the existing Skia backend. The pipeline is static-only: supported content reporting more than one frame is rejected recoverably rather than displaying frame zero as complete support. Format knowledge does not leak into navigation, Stage, markup, Photo Info, or Histogram, and this is backend composition rather than a decoder plugin system.

## D-048 — Decoder backends share one bounded gate and TIFF is fidelity-bounded

Status: Accepted in R7-B.

One project-owned dispatcher owns the existing two expensive-decode slots and common result semantics across backends. Skia remains the JPEG/PNG/static-WebP backend; focused managed LibTiff.NET handles content-signature-detected classic TIFF without leaking backend types beyond imaging. TIFF support is single-image unsigned 8-bit contiguous grayscale/RGB/explicit-alpha only for the proven storage/compression subset. BigTIFF, multiple pages, high-bit-depth, floating-point, unknown alpha, and specialist photometrics are rejected rather than silently degraded. Scanline/tile working memory is temporary; the viewer retains only exact encoded bytes plus the common `DecodedImage` raster and derivatives.

## D-049 — HEIF/AVIF uses one Fovium-owned decode-only runtime

Status: Accepted in R7-C.

Fovium builds and materializes its own app-local libheif 1.23.1 runtime; libde265 1.1.1 supplies HEVC decode and dav1d 1.5.4 supplies AV1 decode. A small project-owned direct binding resolves only `runtimes/<rid>/native` for proven `win-x64`, `linux-x64`, and `osx-arm64`, validates exact version plus HEVC/AV1 decoder presence and encoder absence, and never searches system codec locations. No network, system codec installation, broad native stack, encoder, or second native supply chain is required at runtime. A missing or broken bundle disables HEIF/AVIF recoverably without affecting JPEG, PNG, WebP, or TIFF.

## D-050 — HEIF/AVIF support is static, 8-bit, and SDR-fidelity-bounded

Status: Accepted in R7-C.

One focused backend accepts one unambiguous HEVC or AV1 primary still, supports alpha explicitly exposed by libheif, ignores depth auxiliaries, and may present an SDR primary without reproducing an HDR gain map. Source precision above 8 bits is rejected rather than quantized; explicit PQ/HLG is rejected without tone mapping; sequences and independent-image collections are rejected rather than showing frame zero. Container transforms are applied exactly once by native decode. Output and exact encoded bytes converge into the normal `DecodedImage` BGRA8888/Premul boundary, with native context/image owners released after copy. HEIF/AVIF share the global two-slot decode gate, cache, Stage, Photo Info, Histogram, Peek/Blink, and markup paths.

## D-051 — Core viewer behavior is offline

Status: Accepted direction in R7-C.

Core decode, navigation, Stage/presentation, Photo Info, Histogram, settings, markup, and the planned Color Picker/name matching work without a runtime network dependency. Development may download pinned source archives or public test vectors, but application startup and ordinary viewing never download codecs, query cloud services, or prompt for system codec installation.

## D-052 — Color Picker commits retained photographic source pixels

Status: Owner-review-ready in R8-A.

Pointer motion never changes the selected sample or history. One primary click inside the exact rendered photograph retains the currently presented image, maps to the containing oriented source cell by floor with exclusive right/bottom edges, and reads that decoded BGRA8888/Premul pixel. Descriptor orientation is inverted exactly once; Blink therefore samples the visible comparison while Peek remains canonical. Partial alpha is unpremultiplied once and zero alpha is `Transparent`, never inferred Black. Values are reference sRGB rather than monitor framebuffer values; valid normalized non-sRGB uses one 1×1 Skia conversion and unpreserved profile meaning remains visibly Approximate. Picker click precedes markup, while hold-Space Hand and wheel retain pan/zoom ownership.

## D-053 — Color names and recent samples are bounded, local, and reproducible

Status: Owner-review-ready in R8-A.

Fovium embeds a deterministic 1,800-entry curated derivative of the MIT-licensed `meodai/color-names` dataset and precomputes standard OKLab anchors once. A click performs a stable-order linear nearest search; no package, database, service, telemetry, or network lookup is used. Canonical names remain reviewed English with EN/RU surrounding UI. Per-viewer history is a duplicate-preserving ten-item FIFO displayed oldest-to-newest and survives navigation/hide/reopen only for that session; visibility/current/history are never persisted, while normalized overlay placement may be.
