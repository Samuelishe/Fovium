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
- future discrete zoom-step or zoom-sensitivity control;
- mouse behavior;
- navigation behavior that is genuinely user-configurable.

A future zoom-step control may present a Fine-to-Coarse slider. It adjusts the step curve, not the physical meaning of 100%, and should not expose renderer internals.

### Stage

Stage background is a typed choice: Black (default), fixed Neutral `#505050`, configurable Custom solid color, or Ambient. Matte is an independent toggle over any background with its own opaque color, Solid/Rounded/Soft/Angular outer style, and physical-pixel width (`24` default, `4–192`). Style affects only the area outside the complete rectangular photograph; transparent pixels remain backed by opaque Matte color. Ambient exposes bounded brightness, saturation, and blur controls. Background/color/Matte/style/width/brightness/saturation apply live; blur reparations are coalesced and asynchronous. Settings and the context menu observe the same state owner and autosave without Save/Apply. Stage remains independent from application theme; see [`PROJECT-VISION.md`](PROJECT-VISION.md) and [`THEMES.md`](THEMES.md).

### Appearance

Owns application UI theme selection: Dark or Light. It does not alter Stage or photograph pixels.

### Controls

Controls implements configurable bindings for previous/next, zoom in/out, Fit, 100%, Toggle Matte, fullscreen, Open, Settings, Peek 100%, and Blink Compare. Peek/Blink are visibly named hold commands and default to `Z`/`C`; they use the same locale-independent IDs, gesture capture, conflict confirmation, unassigned state, and reset table as press commands. Capture rejects bare modifiers and reserved `Esc`. Replacement assigns the new command and leaves the former owner unassigned rather than swapping. Reset shortcuts restores this section only.

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

The readable JSON document now uses `schemaVersion = 2` in the platform-appropriate per-user application-data directory. Its explicit v1→v2 migration maps the former four-value Stage enum to background plus independent Matte while preserving image-change policy. R3-F2's additive Matte style/width fields and R4's additive hold-command IDs do not change the schema. Older v2 documents receive `Z`/`C` defaults only when those gestures are not already owned by an existing command; existing customization wins and the colliding new command becomes Unassigned. It writes through a same-directory temporary file and replacement, tolerates unknown properties/command IDs, and falls back to deterministic defaults on missing, malformed, inaccessible, or unsupported-schema input. Extend this direct migration path only when a real incompatible change exists; do not build a generic migration framework.

Settings normally autosave after a valid non-destructive preference change. Do not require Save or Apply buttons without evidence that deferred application is necessary. Destructive or system-owned actions remain explicit.

Reset behavior must support:

```text
Reset this section
Reset all settings
```

Reset all restores a known default state and removes obsolete stored values rather than retaining hidden legacy configuration. A reset operation should be recoverable where practical and must not remove user photographs or unrelated platform data.

The implemented Settings surface contains only meaningful `Viewing`, `Stage`, `Controls`, and `About` sections. It does not create empty future tabs, Save/Apply buttons, a language selector, theme selection, or Performance controls.
