# Roadmap

Role: Directional sequence of project stages.
Read when: Planning scope, choosing the next bounded outcome, or deciding whether work belongs now or later.
Authoritative for: Future stage direction and ordering.
Not authoritative for: Current progress, durable decisions, exact dates, or a permanent numbering guarantee.

Stage numbering is a planning aid and may evolve when evidence changes scope. Each stage should update [`PROJECT-STATE.md`](PROJECT-STATE.md), record durable decisions in [`DECISIONS-LOG.md`](DECISIONS-LOG.md), and leave unresolved risks in [`KNOWN-PROBLEMS.md`](KNOWN-PROBLEMS.md).

## DOCS-R1 — Repository documentation / Codex context foundation

Establish compact authoritative documents, selective context routing, repository safety instructions, and a read-only baseline helper. Do not create production application code.

## REPO-R1 — Repository foundation

Establish repository hygiene, a public README, asset provenance policy, BCL-only ProjectStats diagnostics, meaningful tests for repository tooling, and Windows/Linux/macOS restore-build-test CI. Keep generated reports untracked and create no viewer application.

## R0 — Rendering and imaging foundation probe

Bounded investigation of rendering quality, DPI and physical-pixel 100%, viewport behavior, decoder boundaries, source/profile preservation, and color-foundation feasibility. Produce evidence and decisions before establishing the production stack.

## CONTRACTS-R1 — Product-shell contracts

Establish canonical versioning, Settings, localization, themes, external activation, file-association, and thumbnail-integration contracts before production viewer implementation. This is documentation direction, not implemented application capability.

## R1 — First production Core Viewer

Create the smallest coherent viewer that opens supported images and implements the validated baseline viewport, input, and same-directory previous/next navigation path without pretending broader codec or color completion. The accepted vertical slice establishes JPEG/PNG, zero-UI Black Stage, navigation, preload/cache, viewport input, fullscreen, basic context menu, and EN/RU runtime localization.

## R2 — Navigation, view continuity, Settings foundation, and performance hardening

Accepted outcome: preserve physical scale/point of interest by default with a Fit-each alternative, persist the choice through the first minimal Settings surface, establish the ignored local imaging corpus, and harden async shutdown/cache ownership. Local evidence retained adjacent-only preload, two decode slots, and the provisional memory formula.

## R3 — Stage

Accepted outcome: persisted Black, Neutral, Ambient, and Ambient + Matte presentation modes with bounded oriented derivation, shared cache ownership, synchronized Settings/context-menu access, and unchanged photograph/viewport behavior.

## R3-F1 — Stage customization and configurable controls

Accepted outcome: split Stage background from independent Matte, add Custom/color and bounded Ambient controls with schema-v2 migration, and establish persistent conflict-safe viewer command bindings before hold-based inspection actions.

## R3-F2 — Configurable Matte geometry and styles

Accepted outcome: add persisted DPI-aware Matte width plus Solid, Rounded, Soft, and Angular outer presentation styles while retaining a rectangular, unmodified photograph and isolating all Matte changes from Ambient preparation.

## R4 — Peek 100% / Blink Compare

Accepted outcome: add configurable press-and-hold whole-viewport Peek 100% and previous-viable Blink Compare on the shared viewport/session models. Peek preserves cursor/source anchoring and restores semantic view state; Blink uses retained read-only acquisition without changing canonical navigation, and both cancel safely on lost hold authority.

## R5 — Presenter overlay and cursor highlight

Accepted outcome: add configurable cursor highlight plus an on-demand Brush/Line/Rectangle/Arrow dock. Markup is bounded, image-bound in oriented source space, session-local, memory-only, compatible with viewport changes and Peek/Blink, and never writes source or sidecar data.

## R5-F1 — True Eraser and markup history foundation

Accepted outcome: replace append-only primitives with bounded per-image ordered operation history; add a true partial visual Eraser through an isolated markup layer, per-image Undo/Redo, undoable Clear, and conflict-safe configurable history shortcuts without changing source files or adding persistent annotation data.

## R5-F2 — Markup shapes, opacity, and Shift constraints

Accepted outcome: add Ellipse/Shift-circle, immutable per-draw opacity, Shift constraints for Line/Arrow/Rectangle/Ellipse/Brush, contextual bracket-key thickness/opacity adjustment, and conflict-safe Clear/Blink default evolution without adding a dedicated Highlighter or persistent markup data.

## R5-P1 / R5-P2 / R5-P3 — Ambient transition and sustained-readiness hardening

Accepted corrective outcome on `0.0.0.0013`: retain photograph-first/current-first scheduling, atomically install cached matching Ambient, prepare adjacent Ambient progressively, and keep speculative preload alive through byte-cache saturation by admitting against reclaimable LRU capacity. Owner review accepts normal human browsing around 3–4 24 MP photographs per second; deliberately browsing roughly 5–6+ per second may outrun speculative readiness and expose a brief matching Black fallback, never stale or mismatched Ambient.

## R5-F3 — Contextual drawing shortcuts and presentation UI polish

Accepted outcome and `0.1.0.0000` alpha milestone: code-owned contextual shortcut scopes, drawing/Hand cursors, a 128 px markup-size range, permanent and temporary Hand modes, a normalized movable presenter dock, project-owned icons, overlay context-menu toggles, and grouped Controls. No text, selection/editing, export, sidecars, or layers were added.

## R5-F3-P1 — Interaction render-path isolation

Accepted corrective outcome at `0.1.0.0001`: split low-frequency compositor-cached photographic presentation, independent markup replay, transform-positioned pointer feedback, and transform-only live floating-panel drag. Draft movement no longer refreshes the presenter toolbar, pointer activity no longer restarts its inactivity timer, and opt-in counters verify that high-frequency interaction does not redraw the photograph.

## R6-A — Metadata foundation and Photo Info floating overlay

Accepted outcome at `0.1.0.0002`: focused read-only metadata adapter and project-owned typed summary, lazy identity-safe background parsing from retained encoded bytes, bounded session cache, and a configurable `I` movable Photo Info panel that follows canonical/Blink presentation without changing Peek or navigation. No Advanced Metadata browser, writing, sidecars, Histogram, or ICC behavior was added.

## R6-B — Histogram floating overlay

Accepted outcome at `0.1.0.0003`: lazy identity-safe whole-image decoded-RGB analysis from retained native pixels, deterministic two-million-location large-image sampling, transparent-pixel-safe 256-bin channels, bounded session cache, and a configurable `G` movable Histogram panel that follows canonical/Blink presentation without changing Peek or navigation. Luminance modes, clipping warnings, editing, waveform/vectorscope, and ICC behavior were not added.

## R7-A — Format capability foundation and static WebP

Accepted outcome at `0.1.0.0004`: one project-owned JPEG/PNG/WebP capability authority supplies directory/picker hints and maps detected Skia content into stable Fovium identity. Static lossy/lossless/alpha WebP joins the existing decode/cache/Ambient/inspection/Photo Info/Histogram/markup path; multi-frame content is rejected recoverably under one static-image policy. No dedicated libwebp backend, animation, TIFF, HEIF/HEIC, AVIF, RAW, association, thumbnail, or ICC implementation was added.

## R7-B — Decoder backend boundary and bounded static TIFF

Accepted outcome at `0.1.0.0005`: one shared two-slot decoder dispatcher separates not-my-format from unsupported/corrupt/resource failures, retains Skia for JPEG/PNG/static WebP, and adds a focused managed TIFF backend. Product TIFF scope is classic single-image unsigned 8-bit contiguous grayscale/RGB/declared-alpha for the proven endian/storage/compression subset. High-bit-depth, floating-point, multipage, BigTIFF, specialist photometrics, and huge-image region rendering remain explicit non-goals.

## R7-C — HEIF/AVIF backend and native packaging gate

Accepted outcome at `0.1.0.0006`: one focused direct-interop backend resolves only Fovium's reproducible app-local libheif 1.23.1/libde265 1.1.1/dav1d 1.5.4 runtime. It supports one static 8-bit SDR HEVC or AV1 primary with proven alpha and container transforms, rejects higher precision, PQ/HLG, sequences, and ambiguous collections, and converges into the existing shared decode/cache/presentation path. Normal hosted CI and native/product integration are accepted green on the required Windows/Linux/macOS and `win-x64`/`linux-x64`/`osx-arm64` matrices.

## R8-A — Offline Color Picker / Eyedropper

Owner-accepted outcome at `0.1.0.0007`: hidden-by-default click-to-sample photographic inspection with a compact movable `K` overlay, source-pixel geometry, reference-sRGB HEX/RGB(A), correct premultiplied-alpha handling, and one nearest canonical name from a deterministic embedded 1,800-entry catalog using OKLab distance. Pointer movement never commits. The same per-window session retains exactly the latest ten clicks oldest-to-newest across navigation, Peek/Blink, and hide/reopen; no selected value/history is persisted. Picker input precedes markup, temporary Hand and wheel retain their behavior, and no permanent palette, editor, cloud/API, or runtime network path is added.

Core Fovium functionality remains offline: decode, navigation, Stage/presentation, Photo Info, Histogram, Color Picker/name matching, settings, and markup require no runtime network service. Development-time source/test-vector downloads do not change that product principle.

## Monitor Color Management

R8-B-P1 is owner-accepted as a bounded architecture/rendering probe at unchanged version `0.1.0.0007`. It establishes current source-state truth, Windows display-profile discovery, the untagged Avalonia direct-Skia target, matrix/TRC transform accuracy, a valid LUT-profile Skia limitation, platform-specific double-management boundaries, and the memory case for a viewport-sized derived presentation.

R8-B-N1 is owner-accepted at `5155b7806703a657d89ab2923fd2936814a37a16`. Its independent pinned Little CMS 2.19 source build, app-local RID bundles, license/provenance manifests, binary audits, build-prefix-independent loading, and matrix/TRC/CLUT/malformed/concurrency smoke are green on hosted `win-x64`, `linux-x64`, and `osx-arm64`; normal CI is also green.

R8-B-W1 and its F1–F4 corrective chain are owner-accepted at `0.1.0.0008`. The final architecture retains enabled-by-default, photograph-only Windows ordinary-SDR management through the active monitor ICC and app-local Little CMS, replaces the rejected viewport-raster family with one geometry-independent encoded-size managed source, and publishes new-source photo, Matte/Ambient, geometry, overlays, and presented identity atomically. Zoom, pan, resize, Peek, Fit, and 100% reuse the accepted photo renderer and cause no CMM work or managed-raster replacement. The evidence and Candidate A/B comparison are in the [seamless rendering probe](experiments/R8-B-W1-SEAMLESS-RENDERING-PROBE.md). Later macOS, Linux X11, Wayland, Windows Advanced Color/HDR, high-depth output, and broader real multi-monitor validation remain separate stages.

## R9 — Presentation viewing

### R9-A — Photo Presentation View

Locally ready at `0.1.0.0009`: add a session-local `F6` viewing mode that independently fits every portrait, landscape, square, or panorama together with its optional Matte inside a persisted physical-percentage presentation margin. Normal viewer zoom/pan and saved image-change policy remain unchanged; presentation geometry is synchronous and causes zero Color Management work. Blink is deliberately unavailable in this initial mode rather than using the wrong comparison layout.

### R9-B — Slideshow

Selected next stage after R9-A owner/hosted acceptance. It may drive the existing Photo Presentation layout through explicit start, persisted interval, and a deliberate stop-at-last versus loop policy. Timers, automatic navigation, end behavior, and slideshow state are not implemented in R9-A.

## Later / separate platform milestones

- file association and Open With integration;
- platform packaging;
- platform thumbnail providers where they add value;
- broader codec coverage and specialized/native backends;
- full monitor ICC and multi-monitor color validation;
- huge-image tiled or region strategies;
- macOS runtime validation;
- HDR research when the SDR pipeline is trustworthy.
