# Project state

Role: Compact handoff of the project's current truth.
Read when: At the start of every nontrivial task.
Authoritative for: Current checkpoint, immediate focus, implemented functionality, and active blockers.
Not authoritative for: Durable decisions, future plans, detailed contracts, or Git HEAD/status.

## Current checkpoint

R8-A is owner-accepted at `0.1.0.0007` and accepted commit `26fe6b3a458e4f7cd1662cb41dc49c0d2a22652e`; hosted CI is green. Fovium has a hidden-by-default offline photographic Color Picker: configurable `K` and the checked Overlays menu share one session state; pointer movement never commits; a photograph click fixes one retained source-pixel sample as reference-sRGB HEX/RGB(A), correct unpremultiplied alpha, and one deterministic nearest human name from an embedded curated 1,800-entry OKLab catalog. Its duplicate-preserving history is exactly the latest ten clicks, oldest-to-newest, retained across navigation/Peek/Blink/hide-reopen in one viewer window and never persisted. Monitor-aware Color Management remains unimplemented. Version semantics are owned by [`VERSIONING.md`](VERSIONING.md); picker semantics are owned by [`COLOR-PICKER.md`](COLOR-PICKER.md).

The retained R0 probe remains experimental evidence under `experiments/Fovium.RenderProbe`. Production code is the single `Fovium` assembly and does not depend on the experiment. The accepted R7-B hosted matrix restored, built, and tested successfully on Windows, Ubuntu, and macOS at commit `d5de440`; this is portability evidence for the .NET solution and managed TIFF backend, not manual Linux/macOS viewer/render validation. Local visual acceptance remains Windows at `RenderScaling = 1.00`.

## Current focus

R7-C and its R7-C-N1 prerequisite are owner-accepted. The accepted prerequisite commit is `c4dba80bd23534f372ae09f9285c0e1c5991d5e3`, and normal hosted CI plus native/product integration are green across Windows/Linux/macOS and `win-x64`/`linux-x64`/`osx-arm64`. R8-A is also owner-accepted with green hosted CI. R8-B-P1 is owner-review-ready as a bounded monitor color-management architecture/rendering investigation; it does not enable production transforms or change version `0.1.0.0007`. Its productization gate is blocked on the engine/platform decisions recorded by the [probe](experiments/R8-B-MONITOR-COLOR-MANAGEMENT-PROBE.md).

## Implemented application functionality

- Runnable zero-UI Avalonia desktop viewer with Black, Neutral, Custom, or Ambient Stage backgrounds and an independent optional Matte.
- Central JPEG/PNG/WebP/TIFF/HEIF/AVIF capability registry: candidate extensions, MIME hints, and picker patterns derive from one authority, while backend content probes determine actual format.
- One shared bounded decoder dispatcher with Skia JPEG/PNG/static-WebP, focused managed TIFF, and focused app-local libheif HEIF/AVIF backends; extension remains only a discovery hint.
- Static JPEG, PNG, lossy/lossless/alpha WebP, bounded classic 8-bit TIFF, and bounded static 8-bit SDR HEIF/AVIF decode into the shared BGRA8888/Premul representation. Multi-frame encoded images, TIFF pages/high precision, and HEIF/AVIF sequences, `>8` bit precision, or explicit PQ/HLG are rejected recoverably.
- Fit, physical-pixel 100%, cursor-anchored wheel zoom, pan, and view-state preservation.
- Same-directory single-file activation and ordered explicit multi-file activation.
- Natural filename ordering, failure skipping, adjacent preload, latest-wins publication, and a byte-bounded cache.
- Fullscreen, cursor auto-hide, `Ctrl+O`, and a basic temporary context menu.
- English/Russian runtime localization foundation with English fallback.
- Persistent image-change view policy: Keep current scale by default, or Fit each image.
- Dark Settings window with implemented Viewing, Stage, Presentation, Controls, and About sections; schema-v2 JSON autosave; explicit v1 migration; and malformed-file fallback.
- Persisted Stage background, custom/matte colors, independent Matte, and configurable Ambient brightness/saturation/blur with synchronized Settings and context-menu surfaces.
- Persisted Matte physical width and Solid, Rounded, Soft, and Angular outer presentation styles; all styles retain an opaque rectangular backing beneath alpha photographs without changing photo geometry.
- Bounded asynchronous blur-only Ambient preparation from the oriented decoded photograph; current-first scheduling after photograph publication; progressive direction-prioritized adjacent readiness; render-time brightness/saturation; byte-accounted cache ownership; and matching-identity publication.
- Atomic cached photo+Stage installation plus identity-aware coordinator and actual render-frame diagnostics distinguish generated/cache readiness and count matching versus Black-fallback Ambient frames.
- Speculative decode admission accounts for reclaimable non-protected LRU capacity instead of only currently unused bytes, so bounded adjacent preload continues after cache saturation without evicting the protected current photograph.
- Opt-in anonymized sustained-session diagnostics correlate selection/cache/preload/Ambient readiness with viewport, custom-draw, Skia-lease, and Stage-render counters without permanent viewport UI.
- Persistent command shortcuts with locale-independent identifiers, conflict confirmation, unassigned state, reset, and reserved `Esc` behavior. Defaults add `+`, `-`, `0`, `1`, and `M` viewer controls.
- Whole-viewport Peek 100% on configurable hold `Z`, anchored to the source point beneath the cursor with deterministic Stage fallback, temporary pan, and exact semantic Fit/manual restoration.
- Non-navigating Blink Compare on configurable hold `Shift+C`, acquiring the previous viable image through retained inspection leases while preserving canonical sequence/index/generation state.
- One application-level transient inspection mode, primary-key release ownership, repeat suppression, persistent-command/Esc/focus-loss cancellation, and stale-result rejection.
- Temporary Blink Stage presentation with unchanged solid backgrounds and Matte, matching prepared comparison Ambient when available, and Black fallback rather than mismatched Ambient.
- Configurable cursor highlight on `H`, rendered as a physical-radius translucent circle over photograph or Stage while hiding the system cursor only inside the viewport.
- Configurable presenter dock on `P` with Brush, true partial Eraser, Line, Rectangle, Ellipse, Arrow, color, immutable per-draw opacity, shared stroke/eraser size, Undo, Redo, and undoable Clear; its mouse-only controls do not steal viewer shortcut focus.
- Bounded per-image ordered markup-operation history in oriented source coordinates, transformed by the current Fit/manual/100%/pan/fullscreen geometry and retained only for the current in-memory sequence session.
- An isolated destination-bounded transparent markup compositor whose Clear-blended erase operations affect markup only, never the photograph, Stage, or Matte.
- Project-owned Shift constraints: Line/Arrow/Brush snap to the nearest 45-degree direction, Rectangle becomes a square, and Ellipse becomes a circle without storing viewport coordinates.
- Contextual `[`/`]` thickness and `Ctrl+[`/`Ctrl+]` opacity adjustments while the presenter dock is visible; Clear defaults to `C`, and its previous untouched `Ctrl+Delete`/Blink `C` pair evolves idempotently to Clear `C`/Blink `Shift+C`.
- Configurable `Ctrl+Z` and `Ctrl+Y` commands for per-image Undo/Redo plus conflict-safe schema-v2 additive defaults and customization preservation for all new/evolved shortcuts.
- Code-owned Global, Highlight, and Markup shortcut scopes with deterministic Markup → Highlight → Global precedence, cross-scope gesture reuse, and typed Navigation/Viewing/Inspection/Presentation/Markup/Application groups.
- Contextual `V/B/E/L/R/O/A` Hand/Brush/Eraser/Line/Rectangle/Ellipse/Arrow commands; permanent Hand pans without history, while hold `Space` temporarily activates Hand through the shared repeat/focus/cancellation-safe hold lifecycle.
- Lightweight physical-pixel drawing feedback: opacity/color/size-aware Brush circle, true Eraser-diameter outline, precision shape crosshair, and Hand cursor; markup interaction suppresses but does not disable persisted Cursor Highlight.
- Update-frequency render isolation: direct-Skia photograph/Stage presentation is compositor-cached as one low-frequency layer, markup replays in its own transparent composition visual, pointer feedback moves through a small transform-only surface, and live dock drag uses a transform before one placement commit on release.
- Shared markup physical size range extended to `1–128 px` without changing existing/default values.
- Compact project-owned vector-icon presenter dock with normalized client-relative persisted placement, bounds clamping across resize/fullscreen, localized effective-shortcut tooltips, and no external icon dependency.
- Grouped Controls generated from command metadata plus standard icon-and-text context menus with checked, shared-state Cursor Highlight and Markup Tools toggles.
- Read-only `MetadataExtractor` adapter behind project-owned typed photographic metadata, parsing retained encoded bytes lazily off the UI thread with bounded session cache and latest-wins presented-image authority.
- Session-local Photo Info toggle on configurable `I`, checked Overlays menu entry, immediate oriented dimensions/MP/filename/format/encoded-size data, sparse camera/lens/exposure/capture details, and normalized persisted bottom-left floating placement.
- Session-local Histogram toggle on configurable `G`, checked Overlays menu entry, normalized persisted bottom-right placement, and a lightweight 256-bin RGB plot using one shared channel scale.
- Session-local Color Picker toggle on configurable `K`, checked Overlays menu entry, compact normalized persisted top-right placement, fixed click-to-sample semantics, and lightweight precision pointer feedback that does not redraw the photograph.
- Exact presented-image source sampling across Fit/100%/zoom/pan/Peek/Blink and all EXIF orientations, with BGRA premultiplied-alpha recovery, reference-sRGB single-pixel normalization where trustworthy, and an Approximate state where source color meaning is known but unpreserved.
- Embedded offline 1,800-entry curated color-name catalog with standard OKLab nearest matching, stable tie order, localized Transparent/Approximate semantics, and a duplicate-preserving fixed ten-click per-window FIFO that is never serialized.
- Lazy retained-pixel histogram acquisition with exact BGRA8888/Premul counting below the work limit, deterministic whole-image sampling up to two million locations above it, transparent-pixel exclusion, partial-alpha unpremultiplication, cooperative cancellation, latest-wins publication, and a 128-entry session LRU.
- Peek renders the canonical image's overlay; Blink selects only the comparison image's own overlay and cannot leak current markup onto it.
- Traversal-excluded local imaging corpus policy plus hardened async session shutdown/cache release.

Markup save/export, text, dedicated Highlighter, edit handles/layers, persistent palettes/color libraries, selected-reference A/B comparison, language/theme selection, Advanced Metadata, luminance/clipping histogram modes, metadata writing, animated WebP/APNG, HEIF/AVIF sequences or HDR/high precision, high-bit-depth/floating/multipage TIFF, RAW, file associations/thumbnails, and full monitor-aware ICC are not implemented.

## Active blockers

- Avalonia's direct-Skia lease used by the accepted initial renderer is explicitly unstable and must remain isolated.
- Physical-pixel 100% is validated by pure tests at 1.00/1.25/1.50/2.00, but production runtime evidence exists only at `RenderScaling = 1.00`; per-monitor transitions still need real hardware coverage.
- R8-B-P1 proves Skia 3.119.4 matrix/TRC conversion but also proves that it rejects a valid ICC v4 LUT display profile accepted by Little CMS 2.19. A general production engine is blocked on an owner decision about a reproducible Little CMS runtime stage; macOS compositor evidence and Linux X11/Wayland platform paths also remain unvalidated.
- R8-A Color Picker visual/input smoke is Windows-only at `RenderScaling = 1.00`; pure geometry covers 1.00/1.25/1.50/2.00, but real fractional-DPI and Linux/macOS cursor/panel/input feel remain unvalidated.
- Codec support beyond JPEG/PNG/static WebP/bounded 8-bit TIFF/bounded HEIF/AVIF and a huge/region-rendered-image strategy remain unselected.
- WebP EXIF orientation is not currently surfaced by SkiaSharp 3.119.4 `SKCodec.EncodedOrigin` in the controlled fixture; Fovium retains encoded geometry rather than adding a second eager orientation parser.
- Hosted restore-build-test is confirmed on Windows/Linux/macOS for the accepted R7-B commit, including the managed TIFF suite. Manual Linux/macOS Avalonia/Skia runtime behavior remains unvalidated.
- R4 runtime inspection evidence is Windows-only at `RenderScaling = 1.00`; precise cold non-cached Blink latency and real fractional-DPI interaction still need representative owner hardware evidence.
- R5 through R5-F2 pointer, eraser, constrained-drawing, opacity, and rendering evidence is Windows-only at `RenderScaling = 1.00`; fractional-DPI, Linux, and macOS runtime feel remain unvalidated.
- R5-P1 reduced coordinator-side current Ambient latency from roughly `134 ms` to `15 ms`; R5-P2 removed cached-handoff intermediate state; R5-P3 explained the delayed long-sequence failure at cache saturation and restored continuous bounded LRU replacement. Owner review accepts normal human browsing around 3–4 distinct 24 MP photographs per second. Deliberately browsing roughly 5–6+ per second can outrun speculative photo/Ambient readiness and briefly expose matching Black fallback; stale or mismatched Ambient remains forbidden. Fractional-DPI and cross-platform presentation remain unmeasured.
- R5-F3-P1 pointer/cursor, compositor-cache, drawing, and floating-panel runtime evidence is Windows-only at `RenderScaling = 1.00`; pure geometry covers 1.00/1.25/1.50/2.00, but real fractional-DPI, Linux, and macOS interaction feel and cached exact-pixel presentation remain unvalidated.
- R6-B Histogram runtime evidence is Windows-only. The linear shared-maximum RGB plot and two-million-location deterministic sample are accepted initial presentation/performance choices, not proof that every photographic distribution or future decoded color representation is optimally summarized.
- R7-B TIFF scope is intentionally 8-bit and fully decoded. Valid embedded ICC can enter the existing normalized source-color boundary, but broad TIFF ICC fidelity and monitor output remain unvalidated; huge TIFF region/tile rendering is not implemented.

Open technical risks are tracked in [`KNOWN-PROBLEMS.md`](KNOWN-PROBLEMS.md). Directional stages are in [`ROADMAP.md`](ROADMAP.md). Git remains the authority for branch, HEAD, and worktree status.
