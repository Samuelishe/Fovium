# UX contract

Role: Product-level contract for observable viewer interaction.
Read when: Designing or changing input, window/fullscreen behavior, menus, temporary overlays, Stage access, or settings UX.
Authoritative for: Zero-UI behavior, mouse and keyboard bindings, Fit/100/manual zoom semantics at product level, pan, context-menu philosophy, metadata access, cursor behavior, and future settings interaction principles.
Not authoritative for: Viewport equations, DPI conversion, renderer selection, or internal input architecture.

## Normal viewing state

The photograph dominates the viewport and persistent chrome is absent. Do not add onboarding, first-run hints, tutorial overlays, over-photo navigation arrows, always-visible toolbars, filename overlays, or hover edge zones. Discoverability alone is not sufficient justification for adding viewport UI.

Temporary overlays are acceptable only in response to a direct action and should disappear promptly. The cursor should hide after inactivity when it would otherwise distract from the photograph, then return on pointer movement or relevant interaction; exact timing awaits runtime validation.

## Baseline input

| Input | Behavior |
| --- | --- |
| Mouse wheel | Step zoom in/out, anchored at the pointer |
| Left drag | Pan when the image is zoomed beyond the viewport |
| Right click | Open the context menu |
| Double click | Toggle Fit and 100% |
| `Left Arrow` | Navigate to the previous viable image (default binding) |
| `Right Arrow` | Navigate to the next viable image (default binding) |
| `+` / `-` | Zoom one normal step around the viewport point of interest (default bindings) |
| `0` | Fit (default binding) |
| `1` | Photographic 100% (default binding) |
| `M` | Toggle Matte without changing the Stage background (default binding) |
| `F11` | Toggle fullscreen (default binding) |
| `Esc` | Leave fullscreen; otherwise close the viewer |

The effective bindings except `Esc` are user-configurable in Settings → Controls. `Esc` is reserved and cancels shortcut capture before applying its fullscreen/close behavior. Fullscreen preserves the same zoom and pan behavior as a normal window. The command model is ready to distinguish future press/hold behavior, but Peek/Blink are not implemented.

## View behavior

**Fit** shows the entire oriented photograph, preserves aspect ratio, maximizes use of the available viewport, and never crops.

**100%** has photographic physical-pixel meaning: approximately one oriented source pixel per physical display pixel. The technical contract is owned by [`RENDERING.md`](RENDERING.md).

**Manual zoom** uses reasonably fine discrete steps. Each wheel step keeps the source point beneath the cursor at the same viewport position. A future setting may adjust step size. Manual zoom may move image bounds outside the viewport; left-drag panning then moves the view over the image.

## Navigation experience

Left/right navigation operates over supported, safe, decodable neighboring files in the opened image's directory. It should feel seamless rather than inserting a routine black frame or spinner between photographs. Unsupported, corrupt, or unsafe images may be skipped without ending navigation. Internal loading policy belongs to [`PERFORMANCE.md`](PERFORMANCE.md) and image viability belongs to [`IMAGING-PIPELINE.md`](IMAGING-PIPELINE.md).

By default, navigation preserves physical scale and normalized point of interest for non-Fit views; an intentionally reduced common scale stays reduced. Fit remains semantic Fit. The user may instead select **Fit each image**, which centers every navigated image in Fit. A newly opened sequence always begins in Fit. The preference is owned by [`SETTINGS.md`](SETTINGS.md).

## Context menu, metadata, and Stage

Rare features live behind right click. Planned metadata access follows:

`Photo` → `Information / Metadata`

A future split between basic photo information and advanced metadata is acceptable. A persistent EXIF sidebar is not.

A complete metadata viewer is acceptable when explicitly opened; it must not reserve permanent viewport space.

Stage background selection is available from both the context menu and Settings. Black remains default; Neutral, Custom, and Ambient apply immediately. Matte is an independent modifier over every background; Settings owns its color, physical width, and outer style while the context menu and `M` retain the uncluttered enable/disable path. Neither choice changes Fit, physical scale, pan, point of interest, photo rectangle, or photo sampling. Ambient remains fixed to the full photograph rather than following viewport zoom/pan. Stage definitions belong to [`PROJECT-VISION.md`](PROJECT-VISION.md).

## Settings principles

Settings expose durable preferences, not ordinary navigation. They open as secondary UI from the context menu or `Ctrl+,` and never occupy the normal viewport. They should use plain choices with sensible automatic defaults and avoid expert jargon where a product concept exists. Section ownership, persistence, and reset behavior belong to [`SETTINGS.md`](SETTINGS.md); performance policy belongs to [`PERFORMANCE.md`](PERFORMANCE.md).

Dark/Light application theme affects controls and secondary UI, never the photograph or Stage. The separation is owned by [`THEMES.md`](THEMES.md).

External single- and multiple-file activation enters the same viewing experience, but its sequence construction and platform behavior belong to [`PLATFORM-INTEGRATION.md`](PLATFORM-INTEGRATION.md). In particular, an ordered explicit multi-file selection is a defined input rather than an implicit directory merge.
