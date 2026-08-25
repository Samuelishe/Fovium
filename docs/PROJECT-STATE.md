# Project state

Role: Compact handoff of the project's current truth.
Read when: At the start of every nontrivial task.
Authoritative for: Current checkpoint, immediate focus, implemented functionality, and active blockers.
Not authoritative for: Durable decisions, future plans, detailed contracts, or Git HEAD/status.

## Current checkpoint

R5-F3-P1 is complete at `0.1.0.0001`. The `0.1.0.0000` first substantial usable Fovium alpha milestone remains historical. High-frequency pointer feedback, markup replay, and floating-dock motion are isolated from the low-frequency photographic presentation. R5-P1 current-first scheduling, R5-P2 atomic cached presentation, and R5-P3 sustained preload across cache saturation remain owner-accepted for normal human browsing. Version semantics are owned by [`VERSIONING.md`](VERSIONING.md).

The retained R0 probe remains experimental evidence under `experiments/Fovium.RenderProbe`. Production code is the single `Fovium` assembly and does not depend on the experiment. Local Windows acceptance used one `RenderScaling = 1.00` display; no GitHub-hosted CI or Linux/macOS runtime result is claimed by this stage.

## Current focus

The next intended stage is R6-A, metadata foundation and a Photo Info floating overlay, followed by R6-B Histogram. Do not begin either automatically.

## Implemented application functionality

- Runnable zero-UI Avalonia desktop viewer with Black, Neutral, Custom, or Ambient Stage backgrounds and an independent optional Matte.
- JPEG/PNG content probing and decode with EXIF orientation.
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
- Peek renders the canonical image's overlay; Blink selects only the comparison image's own overlay and cannot leak current markup onto it.
- Traversal-excluded local imaging corpus policy plus hardened async session shutdown/cache release.

Markup save/export, text, dedicated Highlighter, edit handles/layers, selected-reference A/B comparison, language/theme selection, metadata/EXIF/Histogram UI, broad codecs, file associations/thumbnails, and full monitor-aware ICC are not implemented.

## Active blockers

- Avalonia's direct-Skia lease used by the accepted initial renderer is explicitly unstable and must remain isolated.
- Physical-pixel 100% is validated by pure tests at 1.00/1.25/1.50/2.00, but production runtime evidence exists only at `RenderScaling = 1.00`; per-monitor transitions still need real hardware coverage.
- The monitor-aware color pipeline and raw embedded-profile extraction boundary have not been selected.
- Broader codec support and a huge/tiled-image strategy remain unselected.
- GitHub Actions portability is configured but cannot be claimed as passing until the workflow runs remotely.
- R4 runtime inspection evidence is Windows-only at `RenderScaling = 1.00`; precise cold non-cached Blink latency and real fractional-DPI interaction still need representative owner hardware evidence.
- R5 through R5-F2 pointer, eraser, constrained-drawing, opacity, and rendering evidence is Windows-only at `RenderScaling = 1.00`; fractional-DPI, Linux, and macOS runtime feel remain unvalidated.
- R5-P1 reduced coordinator-side current Ambient latency from roughly `134 ms` to `15 ms`; R5-P2 removed cached-handoff intermediate state; R5-P3 explained the delayed long-sequence failure at cache saturation and restored continuous bounded LRU replacement. Owner review accepts normal human browsing around 3–4 distinct 24 MP photographs per second. Deliberately browsing roughly 5–6+ per second can outrun speculative photo/Ambient readiness and briefly expose matching Black fallback; stale or mismatched Ambient remains forbidden. Fractional-DPI and cross-platform presentation remain unmeasured.
- R5-F3-P1 pointer/cursor, compositor-cache, drawing, and floating-panel runtime evidence is Windows-only at `RenderScaling = 1.00`; pure geometry covers 1.00/1.25/1.50/2.00, but real fractional-DPI, Linux, and macOS interaction feel and cached exact-pixel presentation remain unvalidated.

Open technical risks are tracked in [`KNOWN-PROBLEMS.md`](KNOWN-PROBLEMS.md). Directional stages are in [`ROADMAP.md`](ROADMAP.md). Git remains the authority for branch, HEAD, and worktree status.
