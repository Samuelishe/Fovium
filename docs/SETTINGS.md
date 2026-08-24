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

R3 implements Black (default), Neutral, Ambient, and Ambient + Matte as one typed, autosaved preference. Existing schema-v1 documents without the additive field resolve to Black without migration. Settings and the context menu observe the same state owner and apply changes immediately without Save/Apply. No blur, darkness, saturation, or matte-width sliders are exposed. Stage is photographic presentation and is independent from the application UI theme; see [`PROJECT-VISION.md`](PROJECT-VISION.md) and [`THEMES.md`](THEMES.md).

### Appearance

Owns application UI theme selection: Dark or Light. It does not alter Stage or photograph pixels.

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

R2 implements a readable JSON document with `schemaVersion = 1` in the platform-appropriate per-user application-data directory. It writes through a same-directory temporary file and replacement, tolerates unknown properties, and falls back to deterministic defaults on missing, malformed, inaccessible, or unsupported-schema input. Add migration only when an actual incompatible change exists; do not build a migration framework in advance.

Settings normally autosave after a valid non-destructive preference change. Do not require Save or Apply buttons without evidence that deferred application is necessary. Destructive or system-owned actions remain explicit.

Reset behavior must support:

```text
Reset this section
Reset all settings
```

Reset all restores a known default state and removes obsolete stored values rather than retaining hidden legacy configuration. A reset operation should be recoverable where practical and must not remove user photographs or unrelated platform data.

R3 materializes only meaningful `Viewing`, `Stage`, and `About` sections. It does not create empty future tabs, Save/Apply buttons, a language selector, theme selection, reset UI, customization sliders, or Performance controls.
