# Settings

Role: Product-shell contract for Settings organization and preference persistence.
Read when: Designing a preference, Settings UI, configuration storage, reset behavior, or About surface.
Authoritative for: Logical Settings sections, persistence philosophy, reset/autosave behavior, and which preferences belong in each section.
Not authoritative for: Viewer input bindings, Stage rendering, theme palettes, localization catalog format, platform activation semantics, or current implementation status.

## Place in the product

Settings may be capable without becoming visible during ordinary viewing. They open only on request and do not weaken zero-UI: the normal viewport remains the photograph, while the context menu remains the primary discoverable path to secondary functions.

The initial logical sections are:

```text
General
Viewing
Stage
Presentation
Appearance
Controls
Color
Performance
Advanced
About
```

Visible ordering may be refined, but a preference must have one coherent owner rather than appearing in several sections.

## Sections

### General

- language selection;
- startup and window behavior when a real product need is established;
- ordinary remembered application preferences.

Locale behavior is owned by [`LOCALIZATION.md`](LOCALIZATION.md).

### Viewing

- **Scale when changing images** is implemented with `Keep current scale` (default) and `Fit each image`. Fit remains semantic Fit; a non-Fit view preserves physical scale and normalized point of interest, including a deliberately reduced scale.
- **Photo Presentation** stores only an edge margin percentage, default `4%`, normalized to `0–15%` and exposed in `0.5%` steps. The inset applies to the complete photograph plus optional Matte and is calculated from the shorter physical viewport dimension. The mode's enabled state is session-local, starts false, and is controlled live by one shared authority exposed through the viewer command, context menu, and Viewing Settings checkbox; it is never persisted.
- future discrete zoom-step or zoom-sensitivity control;
- mouse behavior;
- navigation behavior that is genuinely user-configurable.

A future zoom-step control may present a Fine-to-Coarse slider. It adjusts the step curve, not the physical meaning of 100%, and should not expose renderer internals.

### Stage

Stage background is a typed choice: Black (default), fixed Neutral `#505050`, configurable Custom solid color, or Ambient. Matte is an independent toggle over any background with its own opaque color, Solid/Rounded/Soft/Angular outer style, and physical-pixel width (`24` default, `4–192`). Style affects only the area outside the complete rectangular photograph; transparent pixels remain backed by opaque Matte color. Ambient exposes bounded brightness, saturation, and blur controls. Background/color/Matte/style/width/brightness/saturation apply live; blur reparations are coalesced and asynchronous. Settings and the context menu observe the same state owner and autosave without Save/Apply. Stage remains independent from application theme; see [`PROJECT-VISION.md`](PROJECT-VISION.md) and [`THEMES.md`](THEMES.md).

### Appearance

Owns application UI theme selection: Dark or Light. It does not alter Stage or photograph pixels.

### Controls

Controls is generated from typed command metadata and grouped as Navigation, Viewing, Inspection, Presentation, Markup, and Application rather than a flat or ID-derived list. Commands additionally own Global, Highlight, or Markup scope. Active resolution is Markup → Highlight → Global; the same gesture is allowed across scopes, while conflict confirmation remains within a scope. Slideshow defaults to Global Viewing `F5`; Photo Presentation defaults to `F6`; Peek/Blink default to hold `Z`/`Shift+C`; highlight/markup toggles to `H`/`P`; history to `Ctrl+Z`/`Ctrl+Y`; Clear to Markup-scoped `C`; markup size/opacity to `[`/`]` and `Ctrl+[`/`Ctrl+]`; Highlight radius to Highlight-scoped `[`/`]`; tool selection to Markup-scoped `V/B/E/L/R/O/A`; and temporary Hand to Markup-scoped hold `Space`. All retain locale-independent IDs, gesture capture, unassigned state, and reset. Capture rejects bare modifiers and reserved `Esc`. Replacement assigns the new command and leaves a same-scope former owner unassigned rather than swapping; cross-scope owners remain unchanged.

R6-A adds Global Presentation command `viewer.togglePhotoInfo`, default `I`, through the same conflict-safe additive normalization: an existing customized Global `I` wins and leaves Photo Info unassigned. Schema v2 also stores normalized client-relative Photo Info placement, default bottom-left. Visibility is deliberately session-local and always starts false; no Settings tab or persisted visibility flag is introduced.

R6-B adds Global Presentation command `viewer.toggleHistogram`, default `G`, with the same additive conflict preservation. Schema v2 stores normalized Histogram placement, default bottom-right. Histogram visibility is session-local and starts false; no appearance settings or new Settings section are introduced.

R8-A adds Global Inspection command `viewer.toggleColorPicker`, default `K`, through the same additive conflict-preserving normalization. Schema v2 stores only normalized Color Picker placement, default top-right. Picker visibility, current sample, and its ten-click history are per-viewer-session state and never serialized. Existing Controls customization is preserved and no Color Picker Settings page or schema bump is introduced.

R9-A adds Global Viewing command `viewer.togglePhotoPresentation`, default `F6`, with the same additive conflict preservation. If an existing customized Global command already owns `F6`, it remains authoritative and the new command is Unassigned. Only `PhotoPresentationView.EdgeMarginPercent` is serialized; active mode is never written.

R9-B adds Global Viewing command `viewer.toggleSlideshow`, default `F5`, under the same conflict rules. A customized Global owner of F5 remains authoritative and leaves Slideshow Unassigned. The live Presentation checkbox and checked context-menu item bind to the same session state and never write it.

### Presentation

The implemented section begins with a compact Slideshow subsection. Its live checkbox starts/stops the shared viewer session but is not a preference; duration persists as whole seconds (`5` default, normalized to `1–60`) and end behavior persists as Stop on last image (default) or Start again from first image. Duration changes restart a full interval from the current published frame; end behavior changes apply to the next bounded end decision without restarting the slideshow session. The section also owns the markup-tools permission plus cursor-highlight color/opacity/physical radius and default markup color/stroke size/opacity. Default markup opacity is `1.00`, accepts `0.05–1.00`, and initializes the active dock style; physical markup size accepts `1–128 px` without changing existing values. Highlight-radius commands persist through the same debounced settings owner. Normalized client-relative placements default to top-center for the markup dock, bottom-left for Photo Info, bottom-right for Histogram, and top-right for Color Picker; they survive restart and clamp against current client bounds, and no desktop coordinates are stored. Informational-overlay visibility, picker samples, and Slideshow Running/index/deadline/prepared ownership are not persisted. Presentation settings configure viewer aids, not annotations or metadata writes: image-bound markup remains memory-only and source metadata remains read-only.

### Color

Reserved for explicit user-facing color policy after the color pipeline exists. Do not expose speculative ICC controls or settings that the runtime cannot honor. The technical color contract remains [`COLOR-MANAGEMENT.md`](COLOR-MANAGEMENT.md).

### Performance

May later expose memory cache, large-image limits, preload policy, and manual expert overrides. Automatic runtime policies remain the preferred defaults and must not benchmark the machine. Technical policy belongs to [`PERFORMANCE.md`](PERFORMANCE.md).

### Advanced

Advanced is an intentional first-class section, not a dumping ground for ordinary preferences. Suitable subjects include:

- multiple-file activation behavior;
- expert rendering/codec diagnostics;
- experimental toggles with a real bounded purpose;
- expert performance overrides that would burden ordinary sections.

The default multiple-file behavior and its two valid modes are owned by [`PLATFORM-INTEGRATION.md`](PLATFORM-INTEGRATION.md). The future control belongs here as **Multiple-file activation**.

### About

R2's initial About surface shows Fovium and the canonical runtime version. Future additions may include project/site links, third-party license access, and an optional Copy diagnostics action. Version identity comes from [`VERSIONING.md`](VERSIONING.md), not a hardcoded view string.

Copied diagnostics may include only genuinely known values such as Fovium version, OS, .NET/Avalonia versions, renderer/backend, `RenderScaling`, and cache budget. About is not a benchmark, hardware inventory, or hardware re-detection tool.

## Persistence

Ordinary preferences use platform-appropriate per-user application-data storage. They do not require a database, catalog, or project-local file.

The readable JSON document uses `schemaVersion = 2` in the platform-appropriate per-user application-data directory. Its explicit v1→v2 migration maps the former four-value Stage enum to background plus independent Matte while preserving image-change policy. R3-F2 Matte geometry through R9-B additive Presentation/command/placement/layout/window-size/slideshow fields remain backward-compatible and do not change the schema. Missing Photo Presentation configuration receives a `4%` edge margin; active mode is not serialized. Missing Slideshow configuration receives `5 s` plus Stop-at-end; only duration/end behavior serialize. Settings window client width/height are persisted in logical DIPs, validated against bounded values, and clamped for the current screen work area only when an instance opens. Desktop position, monitor identity, and minimized/maximized state are not stored: every Settings instance opens centered over its owner. Missing opacity normalizes to `1.00`; missing dock placement uses top-center. New commands receive defaults only when their gesture is free within the same code-owned scope, so deliberate Highlight/Markup bracket duplication remains valid. One narrow idempotent evolution recognizes exactly the untouched previous pair `Blink=C` plus `Clear=Ctrl+Delete` and changes it to `Blink=Shift+C` plus `Clear=C`; if either member differs, both effective customizations are preserved. Existing customization always wins and a colliding same-scope new command becomes Unassigned. It writes through a same-directory temporary file and replacement, tolerates unknown properties/command IDs, and falls back to deterministic defaults on missing, malformed, inaccessible, or unsupported-schema input.

Settings normally autosave after a valid non-destructive preference change. Do not require Save or Apply buttons without evidence that deferred application is necessary. Destructive or system-owned actions remain explicit.

Reset behavior must support:

```text
Reset this section
Reset all settings
```

Reset all restores a known default state and removes obsolete stored values rather than retaining hidden legacy configuration. A reset operation should be recoverable where practical and must not remove user photographs or unrelated platform data.

The implemented Settings surface contains only meaningful `Viewing`, `Color`, `Stage`, `Presentation`, `Controls`, and `About` sections. Color contains one enabled-by-default persisted `MonitorColorManagementEnabled` choice exposed as “Use the active monitor color profile” plus concise Windows SDR scope text. Missing older schema-v2 JSON naturally receives the enabled default; no schema bump is required. Monitor/profile identity, bytes, path, fallback status, intent, and transform policy are runtime facts and are never persisted. It does not create Save/Apply buttons, a language selector, theme selection, Performance controls, an ICC picker, rendering-intent selection, soft proofing, gamut warning, or HDR setting.
