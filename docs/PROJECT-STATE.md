# Project state

Role: Compact handoff of the project's current truth.
Read when: At the start of every nontrivial task.
Authoritative for: Current checkpoint, immediate focus, implemented functionality, and active blockers.
Not authoritative for: Durable decisions, future plans, detailed contracts, or Git HEAD/status.

## Current checkpoint

R5-P3 is implemented for owner review on corrective build `0.0.0.0013`. R5-P1 current-first scheduling and R5-P2 atomic cached presentation remain; R5-P3 corrects the speculative-admission collapse that appeared when the photo cache became full. Black-flash elimination is still not owner-accepted. The last owner-accepted product checkpoint remains R5-F2 / `0.0.0.0012`; version semantics are owned by [`VERSIONING.md`](VERSIONING.md).

The retained R0 probe remains experimental evidence under `experiments/Fovium.RenderProbe`. Production code is the single `Fovium` assembly and does not depend on the experiment. Local Windows acceptance used one `RenderScaling = 1.00` display; no GitHub-hosted CI or Linux/macOS runtime result is claimed by this stage.

## Current focus

Owner visual review of sustained long-sequence Ambient browsing after cache saturation. R5-F3 remains next only after explicit acceptance; do not begin it or metadata/context-menu work automatically.

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
- Peek renders the canonical image's overlay; Blink selects only the comparison image's own overlay and cannot leak current markup onto it.
- Traversal-excluded local imaging corpus policy plus hardened async session shutdown/cache release.

Markup save/export, text, dedicated Highlighter, contextual tool-selection shortcuts, dock/Controls grouping polish, edit handles/layers, selected-reference A/B comparison, language/theme selection, metadata UI, broad codecs, file associations/thumbnails, and full monitor-aware ICC are not implemented.

## Active blockers

- Avalonia's direct-Skia lease used by the accepted initial renderer is explicitly unstable and must remain isolated.
- Physical-pixel 100% is validated by pure tests at 1.00/1.25/1.50/2.00, but production runtime evidence exists only at `RenderScaling = 1.00`; per-monitor transitions still need real hardware coverage.
- The monitor-aware color pipeline and raw embedded-profile extraction boundary have not been selected.
- Broader codec support and a huge/tiled-image strategy remain unselected.
- GitHub Actions portability is configured but cannot be claimed as passing until the workflow runs remotely.
- R4 runtime inspection evidence is Windows-only at `RenderScaling = 1.00`; precise cold non-cached Blink latency and real fractional-DPI interaction still need representative owner hardware evidence.
- R5 through R5-F2 pointer, eraser, constrained-drawing, opacity, and rendering evidence is Windows-only at `RenderScaling = 1.00`; fractional-DPI, Linux, and macOS runtime feel remain unvalidated.
- R5-P1 reduced coordinator-side current Ambient latency from roughly `134 ms` to `15 ms`, but owner review still observed a compositor-visible Black transition. R5-P3 then reproduced the delayed failure on a long owner-supplied local 24 MP corpus: the first speculative `ResourceLimit` occurred at ordinal 10 and the first foreground miss/missing Ambient/fallback frame at ordinal 11. After reclaim-aware admission, 100 distinct forward plus 20 backward transitions were cache hits with matching Ambient and zero measured fallback frames, while the cache plateaued near its existing 1 GiB cap. The product defect nevertheless remains open until owner visual acceptance; fractional-DPI and cross-platform presentation remain unmeasured.

Open technical risks are tracked in [`KNOWN-PROBLEMS.md`](KNOWN-PROBLEMS.md). Directional stages are in [`ROADMAP.md`](ROADMAP.md). Git remains the authority for branch, HEAD, and worktree status.
