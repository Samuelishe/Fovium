# Project state

Role: Compact handoff of the project's current truth.
Read when: At the start of every nontrivial task.
Authoritative for: Current checkpoint, immediate focus, implemented functionality, and active blockers.
Not authoritative for: Durable decisions, future plans, detailed contracts, or Git HEAD/status.

## Current checkpoint

R10-E is locally complete on pushed R10-D baseline `50f1a4922bb84bdfd89b40ad7abcb2546c50c302`. Product version is
`0.1.3.0001`: the 45 broad families and 1,800 creative anchors are unchanged, while five independently evidenced
Professional terms add five bounded lobes for a total of 99 terms / 104 regions. The accepted terms are Apple Green,
Pea Green, Burnt Orange, Straw, and Payne's Gray; every lobe is reachable and no lobe is shadowed.

The ignored research corpus now includes pinned UW/LabintheWild and Stanford CoCoLab human-survey numeric evidence plus
NBS/ISCC source dictionaries from Color-Library. Exact-name ingestion rejects compound homonyms, deduplicates rows, and
caps each survey source at 128 deterministic anchors per candidate. Raw numeric evidence grew from 34,330 to 46,571
anchors and normalized candidate evidence from 13,434 to 22,168 while source quality and independence groups remain
explicit. Report schema remains v2.

The retained R0 probe remains experimental evidence under `experiments/Fovium.RenderProbe`. Production code is the
single `Fovium` assembly and does not depend on the experiment. The accepted R7-B hosted matrix restored, built, and
tested successfully on Windows, Ubuntu, and macOS at commit `d5de440`; this is portability evidence for the .NET
solution and managed TIFF backend, not manual Linux/macOS viewer/render validation. Local visual acceptance remains
Windows at `RenderScaling = 1.00`.

## Current focus

R10-E's whole 250-entry frontier rerun leaves 99 Accepted, 21 Deferred, 4 Synonym, 2 Rejected, and 124 Unreviewed.
Burnt Umber was removed from the acceptance batch when its core consumed the existing Russet anchor; Cherry,
Raspberry, Primrose, Cinnabar, Dandelion, Putty, and other highlighted candidates remain Deferred for fragmentation,
weak component independence, or same-core risk. Cinnabar Green and rose-like Primrose compounds remain excluded.
Antique White/Linen remains the sole same-core warning rather than being forced apart without new evidence. A future
push must prove ordinary and native-workflow shared .NET tests on hosted Windows/Linux/macOS.

The current Codex/Rider ACP session negotiates no true in-app browser backend. Browser-plugin discovery returns an empty
backend list, and the Rider Playwright MCP entry has no command or URL and is absent from the session tool registry.
Isolated installed Chrome may render ignored evidence, but is explicitly only a fallback. A true backend requires a
capable host/session or a separately valid MCP registration plus Rider restart outside repository code.

## Implemented application functionality

- Runnable zero-UI Avalonia desktop viewer with Black, Neutral, Custom, Ambient, Average, Dominant, abstract Color Wash,
  Color Gradient, or Soft Glow Stage backgrounds and an independent optional Matte.
- Central JPEG/PNG/WebP/TIFF/HEIF/AVIF capability registry: candidate extensions, MIME hints, and picker patterns derive
  from one authority, while backend content probes determine actual format.
- One shared bounded decoder dispatcher with Skia JPEG/PNG/static-WebP, focused managed TIFF, and focused app-local
  libheif HEIF/AVIF backends; extension remains only a discovery hint.
- Static JPEG, PNG, lossy/lossless/alpha WebP, bounded classic 8-bit TIFF, and bounded static 8-bit SDR HEIF/AVIF decode
  into the shared BGRA8888/Premul representation. Multi-frame encoded images, TIFF pages/high precision, and HEIF/AVIF
  sequences, `>8` bit precision, or explicit PQ/HLG are rejected recoverably.
- Fit, physical-pixel 100%, cursor-anchored wheel zoom, pan, and view-state preservation.
- Session-local Photo Presentation View on configurable `F6`, independently fitting every oriented photograph inside a
  persisted `0–15%` physical edge margin (`4%` default), then drawing optional Matte around the stable photo destination
  without changing its scale, with ordinary zoom/pan/Peek/Blink/Hand geometry disabled until exit to normal Fit.
- Session-local Slideshow on configurable `F5`, with synchronized checked context-menu/Settings surfaces,
  publication-based 1–60 second timing, bounded Stop-at-end/Loop traversal, manual-navigation reset, Esc-first
  cancellation, and one memory-admitted prepared managed next frame.
- Same-directory single-file activation and ordered explicit multi-file activation.
- Natural filename ordering, failure skipping, adjacent preload, latest-wins publication, and a byte-bounded cache.
- Fullscreen, cursor auto-hide, `Ctrl+O`, and a basic temporary context menu.
- English/Russian runtime localization foundation with English fallback.
- Persistent image-change view policy: Keep current scale by default, or Fit each image.
- Dark Settings window with implemented Viewing, Color, Stage, Presentation, Controls, and About sections; schema-v2
  JSON autosave; explicit v1 migration; and malformed-file fallback. Monitor Color Management is enabled by default and
  its single persisted checkbox can restore the exact legacy path.
- Windows ordinary-SDR photograph presentation through the assigned active-monitor RGB ICC and app-local Little CMS
  2.19, with one encoded-size managed source per active source/destination, source/destination latest-wins publication,
  the unchanged canonical spatial renderer for all viewport interaction, explicit fallback state, and no monitor
  transform of Stage, Matte, Ambient, markup, Color Picker, or Histogram.
- Persisted Stage background, custom/matte colors, independent Matte, and configurable Ambient
  brightness/saturation/blur with synchronized Settings and context-menu surfaces; Matte color may use Custom,
  presentation-safe Average, or presentation-safe Dominant source and may add Hairline Auto separation.
- One deterministic oriented reference-sRGB analysis per decoded photograph, bounded to a 96-pixel long edge and
  attached to the existing byte-accounted decoded cache entry; exact-source publication exposes mathematical average,
  representative dominant, unchanged five-entry population palette, `6×6` spatial field, and boundary tone without
  source re-decode or geometry recomputation. Color Gradient and Soft Glow add two `32×32` retained rasters prepared
  once from that same result; zoom, pan, resize, DPI, Matte, and slideshow cadence only stretch the existing artifact.
- Persisted Matte physical width and Solid, Rounded, Soft, and Angular outer presentation styles; all styles retain an
  opaque rectangular backing beneath alpha photographs without changing photo geometry.
- Bounded asynchronous blur-only Ambient preparation from the oriented decoded photograph; current-first scheduling
  after photograph publication; progressive direction-prioritized adjacent readiness; render-time brightness/saturation;
  byte-accounted cache ownership; and matching-identity publication.
- Atomic cached photo+Stage installation plus identity-aware coordinator and actual render-frame diagnostics distinguish
  generated/cache readiness and count matching versus Black-fallback Ambient frames.
- Speculative decode admission accounts for reclaimable non-protected LRU capacity instead of only currently unused
  bytes, so bounded adjacent preload continues after cache saturation without evicting the protected current photograph.
- Opt-in anonymized sustained-session diagnostics correlate selection/cache/preload/Ambient readiness with viewport,
  custom-draw, Skia-lease, and Stage-render counters without permanent viewport UI.
- Persistent command shortcuts with locale-independent identifiers, conflict confirmation, unassigned state, reset, and
  reserved `Esc` behavior. Defaults add `+`, `-`, `0`, `1`, and `M` viewer controls.
- Whole-viewport Peek 100% on configurable hold `Z`, anchored to the source point beneath the cursor with deterministic
  Stage fallback, temporary pan, and exact semantic Fit/manual restoration.
- Non-navigating Blink Compare on configurable hold `Shift+C`, acquiring the previous viable image through retained
  inspection leases while preserving canonical sequence/index/generation state.
- One application-level transient inspection mode, primary-key release ownership, repeat suppression,
  persistent-command/Esc/focus-loss cancellation, and stale-result rejection.
- Temporary Blink Stage presentation with unchanged solid backgrounds and Matte, matching prepared comparison Ambient
  when available, and Black fallback rather than mismatched Ambient.
- Configurable cursor highlight on `H`, rendered as a physical-radius translucent circle over photograph or Stage while
  hiding the system cursor only inside the viewport.
- Configurable presenter dock on `P` with Brush, true partial Eraser, Line, Rectangle, Ellipse, Arrow, color, immutable
  per-draw opacity, shared stroke/eraser size, Undo, Redo, and undoable Clear; its mouse-only controls do not steal
  viewer shortcut focus.
- Bounded per-image ordered markup-operation history in oriented source coordinates, transformed by the current
  Fit/manual/100%/pan/fullscreen geometry and retained only for the current in-memory sequence session.
- An isolated destination-bounded transparent markup compositor whose Clear-blended erase operations affect markup only,
  never the photograph, Stage, or Matte.
- Project-owned Shift constraints: Line/Arrow/Brush snap to the nearest 45-degree direction, Rectangle becomes a square,
  and Ellipse becomes a circle without storing viewport coordinates.
- Contextual `[`/`]` thickness and `Ctrl+[`/`Ctrl+]` opacity adjustments while the presenter dock is visible; Clear
  defaults to `C`, and its previous untouched `Ctrl+Delete`/Blink `C` pair evolves idempotently to Clear `C`/Blink
  `Shift+C`.
- Configurable `Ctrl+Z` and `Ctrl+Y` commands for per-image Undo/Redo plus conflict-safe schema-v2 additive defaults and
  customization preservation for all new/evolved shortcuts.
- Code-owned Global, Highlight, and Markup shortcut scopes with deterministic Markup → Highlight → Global precedence,
  cross-scope gesture reuse, and typed Navigation/Viewing/Inspection/Presentation/Markup/Application groups.
- Contextual `V/B/E/L/R/O/A` Hand/Brush/Eraser/Line/Rectangle/Ellipse/Arrow commands; permanent Hand pans without
  history, while hold `Space` temporarily activates Hand through the shared repeat/focus/cancellation-safe hold
  lifecycle.
- Lightweight physical-pixel drawing feedback: opacity/color/size-aware Brush circle, true Eraser-diameter outline,
  precision shape crosshair, and Hand cursor; markup interaction suppresses but does not disable persisted Cursor
  Highlight.
- Update-frequency render isolation: direct-Skia photograph/Stage presentation is compositor-cached as one low-frequency
  layer, markup replays in its own transparent composition visual, pointer feedback moves through a small transform-only
  surface, and every floating overlay uses a live transform before one placement commit on release.
- Shared markup physical size range extended to `1–128 px` without changing existing/default values.
- Compact project-owned vector-icon presenter dock with normalized client-relative persisted placement, bounds clamping
  across resize/fullscreen, localized effective-shortcut tooltips and Close control, and no external icon dependency.
- Grouped Controls generated from command metadata plus standard icon-and-text context menus with checked, shared-state
  Cursor Highlight and Markup Tools toggles.
- Read-only `MetadataExtractor` adapter behind project-owned typed photographic metadata, including
  camera/lens/exposure/capture, exposure compensation/mode, metering, white balance, and flash state, parsing retained
  encoded bytes lazily off the UI thread with bounded session cache and latest-wins presented-image authority.
- Session-local Photo Info toggle on configurable `I`, checked Overlays menu entry, compact sparse localized label/value
  rows with explanatory tooltips, immediate oriented dimensions/MP/filename/format/encoded-size data, photographic
  metadata details, and normalized persisted bottom-left floating placement.
- Session-local Histogram toggle on configurable `G`, checked Overlays menu entry, normalized persisted bottom-right
  placement, and a lightweight 256-bin RGB plot using one shared channel scale.
- Session-local Color Inspector toggle on configurable `K`, checked Overlays menu entry, normalized persisted top-right
  placement, horizontal selectable recent list plus stable detail pane, explicit non-closing Clear, fixed
  click-to-sample semantics, and lightweight precision pointer feedback that does not redraw the photograph.
- Exact presented-image source sampling across Fit/100%/zoom/pan/Peek/Blink and all EXIF orientations, with BGRA
  premultiplied-alpha recovery, reference-sRGB single-pixel normalization where trustworthy, and an Approximate state
  where source color meaning is known but unpreserved.
- Deterministic reference-sRGB-to-OKLCH perceptual taxonomy with localized short/detailed EN/RU names, separate
  perceptual roles and undertones, lightness-dependent neutral boundaries, bounded compound hue transitions, monotonic
  lightness/chroma classes, bounded warm-neutral/earth-tone, mint, and violet/lilac-gray families, a lightness-aware
  yellow-brown/ochre boundary that leaves greener olive controls intact, truthful Transparent
  handling, and no proprietary physical-standard claims. A declarative professional-shade layer adds 94 bounded terms
  across purple/pink, blue, green/cyan, yellow/earth, brown, red/orange, and neutral/off-white domains. R8-A-F12 adds
  Heliotrope, Slate Blue, Spring Green, Pine Green, Brick Red, Raw Sienna, Raw Umber, Canary Yellow,
  Gamboge, Ecru, Buff, Goldenrod, Russet, and Heather. Composite regions may share
  one term identity without replacing broad-family identity or generic fallback; the embedded
  offline 1,800-entry creative catalog retains standard OKLab nearest matching, stable tie order, complete Russian
  stable-ID display names, and canonical-English fallback as secondary presentation data.
- Shared `Fovium.ColorSemantics` ownership for the structural classifier and separate creative matcher; deterministic
  core/deep `fovium-color-semantics-report/v2` JSON, compact analysis JSON/Markdown, report diff, SVG
  overview/domain/relations sheets, and offline Canvas 3D Explorer generated from one normalized model. Semantic
  interpretation remains downstream of source truth and never feeds rendering or Color Management.
- Duplicate-preserving ten-entry per-window Color Inspector FIFO with distinct click identities, stable mouse selection
  across navigation/hide/reopen, and explicit history/selection Clear; no sample, entry, selection, or history is
  serialized.
- Lazy retained-pixel histogram acquisition with exact BGRA8888/Premul counting below the work limit, deterministic
  whole-image sampling up to two million locations above it, transparent-pixel exclusion, partial-alpha
  unpremultiplication, cooperative cancellation, latest-wins publication, and a 128-entry session LRU.
- Peek renders the canonical image's overlay; Blink selects only the comparison image's own overlay and cannot leak
  current markup onto it.
- Traversal-excluded local imaging corpus policy plus hardened async session shutdown/cache release.

Markup save/export, text, dedicated Highlighter, edit handles/layers, persistent palettes/color libraries,
selected-reference A/B comparison, language/theme selection, Advanced Metadata, luminance/clipping histogram modes,
metadata writing, animated WebP/APNG, HEIF/AVIF sequences or HDR/high precision, high-bit-depth/floating/multipage TIFF,
RAW, file associations/thumbnails, and monitor-aware output outside ordinary Windows SDR are not implemented.

## Active blockers

- Avalonia's direct-Skia lease used by the accepted initial renderer is explicitly unstable and must remain isolated.
- Physical-pixel 100% is validated by pure tests at 1.00/1.25/1.50/2.00, but production runtime evidence exists only at
  `RenderScaling = 1.00`; per-monitor transitions still need real hardware coverage.
- Windows SDR Color Management has local single-monitor evidence only. Real multi-monitor tie/transition behavior,
  fractional-DPI hardware, Windows Advanced Color/HDR, macOS compositor ownership, and Linux X11/Wayland platform paths
  remain unvalidated or explicitly unsupported.
- R8-A Color Inspector visual/input smoke is Windows-only at `RenderScaling = 1.00`. R8-A-F3/F4 add read-only visual and
  production-sampler evidence over bounded owner photographs, including 36 F4 points across 9 photographs, but final
  overlay balance,
  scroll/selection feel, semantic borderlines, real fractional-DPI, and Linux/macOS cursor/panel/input feel remain owner
  visual-review territory. R8-A-F5 through F12 add systematic developer-side topology, balanced semantic-reference,
  independent component-aware vocabulary-candidate, explainability, rendered contact-sheet, boundary-counterexample,
  overlap-severity, source-independent master lexicon, core-confidence, whole-catalog frontier, and holdout evidence,
  not cross-platform visual acceptance or an objective
  color-name ground truth.
- Codec support beyond JPEG/PNG/static WebP/bounded 8-bit TIFF/bounded HEIF/AVIF and a huge/region-rendered-image
  strategy remain unselected.
- WebP EXIF orientation is not currently surfaced by SkiaSharp 3.119.4 `SKCodec.EncodedOrigin` in the controlled
  fixture; Fovium retains encoded geometry rather than adding a second eager orientation parser.
- Hosted restore-build-test is confirmed on Windows/Linux/macOS for the accepted R7-B commit, including the managed TIFF
  suite. Manual Linux/macOS Avalonia/Skia runtime behavior remains unvalidated.
- R4 runtime inspection evidence is Windows-only at `RenderScaling = 1.00`; precise cold non-cached Blink latency and
  real fractional-DPI interaction still need representative owner hardware evidence.
- R5 through R5-F2 pointer, eraser, constrained-drawing, opacity, and rendering evidence is Windows-only at
  `RenderScaling = 1.00`; fractional-DPI, Linux, and macOS runtime feel remain unvalidated.
- R5-P1 reduced coordinator-side current Ambient latency from roughly `134 ms` to `15 ms`; R5-P2 removed cached-handoff
  intermediate state; R5-P3 explained the delayed long-sequence failure at cache saturation and restored continuous
  bounded LRU replacement. Owner review accepts normal human browsing around 3–4 distinct 24 MP photographs per second.
  Deliberately browsing roughly 5–6+ per second can outrun speculative photo/Ambient readiness and briefly expose
  matching Black fallback; stale or mismatched Ambient remains forbidden. Fractional-DPI and cross-platform presentation
  remain unmeasured.
- R5-F3-P1 pointer/cursor, compositor-cache, drawing, and floating-panel runtime evidence is Windows-only at
  `RenderScaling = 1.00`; pure geometry covers 1.00/1.25/1.50/2.00, but real fractional-DPI, Linux, and macOS
  interaction feel and cached exact-pixel presentation remain unvalidated.
- R6-B Histogram runtime evidence is Windows-only. The linear shared-maximum RGB plot and two-million-location
  deterministic sample are accepted initial presentation/performance choices, not proof that every photographic
  distribution or future decoded color representation is optimally summarized.
- R7-B TIFF scope is intentionally 8-bit and fully decoded. Valid embedded ICC can enter the existing normalized
  source-color boundary, but broad TIFF ICC fidelity and monitor output remain unvalidated; huge TIFF region/tile
  rendering is not implemented.
- R9-B desktop timing and visual evidence is Windows-only at `RenderScaling = 1.00`; Linux/macOS runtime cadence,
  fractional-DPI multi-monitor movement during a running slideshow, and very-large-image fallback latency remain
  unmeasured. Transitions, shuffle, music, countdown UI, and a multi-frame slideshow cache are intentionally absent.-
  R10-B visual acceptance covers eleven ignored local photographs across high/low key, neutral/chromatic, warm/cool,
  green, portrait/skin, accent, and mixed geometry on Windows reference-sRGB renders. Automated pixels and geometry
  cover
  1.00/1.25/1.50/2.00, but real fractional-DPI and Linux/macOS visual/runtime acceptance remain unmeasured.

Open technical risks are tracked in [`KNOWN-PROBLEMS.md`](KNOWN-PROBLEMS.md). Directional stages are in [
`ROADMAP.md`](ROADMAP.md). Git remains the authority for branch, HEAD, and worktree status.
