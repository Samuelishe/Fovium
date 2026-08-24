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

- future discrete zoom-step or zoom-sensitivity control;
- mouse behavior;
- navigation behavior that is genuinely user-configurable.

A future zoom-step control may present a Fine-to-Coarse slider. It adjusts the step curve, not the physical meaning of 100%, and should not expose renderer internals.

### Stage

Owns Black, Neutral, Ambient, and Ambient + Matte choices plus later justified Ambient/Matte controls. Stage is photographic presentation and is independent from the application UI theme; see [`PROJECT-VISION.md`](PROJECT-VISION.md) and [`THEMES.md`](THEMES.md).

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

The future About page shows Fovium, the canonical formatted version, project/site links, third-party license access, and an optional Copy diagnostics action. Version identity comes from [`VERSIONING.md`](VERSIONING.md), not a hardcoded view string.

Copied diagnostics may include only genuinely known values such as Fovium version, OS, .NET/Avalonia versions, renderer/backend, `RenderScaling`, and cache budget. About is not a benchmark, hardware inventory, or hardware re-detection tool.

## Persistence

Ordinary preferences use platform-appropriate per-user application-data storage. They do not require a database, catalog, or project-local file.

The future representation should be readable, explicitly schema-versioned, and safely persisted through atomic replacement or an equivalently robust mechanism. Add schema migration only when an actual incompatible change exists; do not build a migration framework in advance.

Settings normally autosave after a valid non-destructive preference change. Do not require Save or Apply buttons without evidence that deferred application is necessary. Destructive or system-owned actions remain explicit.

Reset behavior must support:

```text
Reset this section
Reset all settings
```

Reset all restores a known default state and removes obsolete stored values rather than retaining hidden legacy configuration. A reset operation should be recoverable where practical and must not remove user photographs or unrelated platform data.

No persistence format, Settings UI, or runtime preference model is implemented in CONTRACTS-R1.
