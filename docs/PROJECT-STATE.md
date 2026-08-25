# Project state

Role: Compact handoff of the project's current truth.
Read when: At the start of every nontrivial task.
Authoritative for: Current checkpoint, immediate focus, implemented functionality, and active blockers.
Not authoritative for: Durable decisions, future plans, detailed contracts, or Git HEAD/status.

## Current checkpoint

R5-F1 is complete. The project version is `0.0.0.0011`; its semantics and accepted checkpoint history are owned by [`VERSIONING.md`](VERSIONING.md).

The retained R0 probe remains experimental evidence under `experiments/Fovium.RenderProbe`. Production code is the single `Fovium` assembly and does not depend on the experiment. Local Windows acceptance used one `RenderScaling = 1.00` display; no GitHub-hosted CI or Linux/macOS runtime result is claimed by this stage.

## Current focus

Owner review of true partial erasing and per-image markup history. The expected next bounded stage is R5-F2 for markup shapes, opacity, and Shift constraints only after acceptance; do not begin it or metadata/context-menu work automatically.

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
- Bounded asynchronous blur-only Ambient preparation from the oriented decoded photograph; render-time brightness/saturation; adjacent preparation; byte-accounted cache ownership; and matching-identity publication.
- Persistent command shortcuts with locale-independent identifiers, conflict confirmation, unassigned state, reset, and reserved `Esc` behavior. Defaults add `+`, `-`, `0`, `1`, and `M` viewer controls.
- Whole-viewport Peek 100% on configurable hold `Z`, anchored to the source point beneath the cursor with deterministic Stage fallback, temporary pan, and exact semantic Fit/manual restoration.
- Non-navigating Blink Compare on configurable hold `C`, acquiring the previous viable image through retained inspection leases while preserving canonical sequence/index/generation state.
- One application-level transient inspection mode, primary-key release ownership, repeat suppression, persistent-command/Esc/focus-loss cancellation, and stale-result rejection.
- Temporary Blink Stage presentation with unchanged solid backgrounds and Matte, matching prepared comparison Ambient when available, and Black fallback rather than mismatched Ambient.
- Configurable cursor highlight on `H`, rendered as a physical-radius translucent circle over photograph or Stage while hiding the system cursor only inside the viewport.
- Configurable presenter dock on `P` with Brush, true partial Eraser, Line, Rectangle, Arrow, color, shared stroke/eraser size, Undo, Redo, and undoable Clear; its mouse-only controls do not steal viewer shortcut focus.
- Bounded per-image ordered markup-operation history in oriented source coordinates, transformed by the current Fit/manual/100%/pan/fullscreen geometry and retained only for the current in-memory sequence session.
- An isolated destination-bounded transparent markup compositor whose Clear-blended erase operations affect markup only, never the photograph, Stage, or Matte.
- Configurable `Ctrl+Z`, `Ctrl+Y`, and `Ctrl+Delete` commands for per-image Undo, Redo, and undoable Clear, with conflict-safe additive defaults for existing schema-v2 settings.
- Peek renders the canonical image's overlay; Blink selects only the comparison image's own overlay and cannot leak current markup onto it.
- Traversal-excluded local imaging corpus policy plus hardened async session shutdown/cache release.

Markup save/export, text/ellipse/highlighter/opacity/Shift constraints/contextual tool shortcuts/edit handles/layers, selected-reference A/B comparison, language/theme selection, metadata UI, broad codecs, file associations/thumbnails, and full monitor-aware ICC are not implemented.

## Active blockers

- Avalonia's direct-Skia lease used by the accepted initial renderer is explicitly unstable and must remain isolated.
- Physical-pixel 100% is validated by pure tests at 1.00/1.25/1.50/2.00, but production runtime evidence exists only at `RenderScaling = 1.00`; per-monitor transitions still need real hardware coverage.
- The monitor-aware color pipeline and raw embedded-profile extraction boundary have not been selected.
- Broader codec support and a huge/tiled-image strategy remain unselected.
- GitHub Actions portability is configured but cannot be claimed as passing until the workflow runs remotely.
- R4 runtime inspection evidence is Windows-only at `RenderScaling = 1.00`; precise cold non-cached Blink latency and real fractional-DPI interaction still need representative owner hardware evidence.
- R5/R5-F1 pointer, eraser, and rendering evidence is Windows-only at `RenderScaling = 1.00`; fractional-DPI, Linux, and macOS runtime feel remain unvalidated.

Open technical risks are tracked in [`KNOWN-PROBLEMS.md`](KNOWN-PROBLEMS.md). Directional stages are in [`ROADMAP.md`](ROADMAP.md). Git remains the authority for branch, HEAD, and worktree status.
