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
| Hold `Z` | Peek at whole-viewport photographic 100% around the cursor/source point; release restores the exact prior semantic view (default binding) |
| Hold `Shift+C` | Temporarily show the previous viable image without navigating; release restores the retained current presentation (default binding) |
| `H` | Toggle the configured translucent cursor highlight inside the viewer viewport (default binding) |
| `P` | Show/hide the compact markup tools dock without clearing existing marks (default binding) |
| `I` | Show/hide the movable Photo Info panel for the currently presented photograph (default binding) |
| `G` | Show/hide the movable RGB Histogram panel for the currently presented photograph (default binding) |
| `K` | Show/hide the movable photographic Color Picker panel (default binding) |
| `F6` | Toggle session-local Photo Presentation View (default binding) |
| `Ctrl+Z` | Undo the current image's last markup operation; cancel an unfinished draft first (default binding) |
| `Ctrl+Y` | Redo the current image's next markup operation (default binding) |
| `C` | Clear the current image's markup as one undoable operation while the markup dock is visible (default binding) |
| `[` / `]` | Decrease/increase active markup thickness by one physical pixel while the dock is visible (default bindings) |
| `Ctrl+[` / `Ctrl+]` | Decrease/increase active markup opacity by five percentage points while the dock is visible (default bindings) |
| `V` / `B` / `E` / `L` / `R` / `O` / `A` | Select Hand, Brush, Eraser, Line, Rectangle, Ellipse, or Arrow while the markup dock is visible (default bindings) |
| Hold `Space` | Temporarily use Hand while markup tools are visible; release restores the selected tool (default binding) |
| `F11` | Toggle fullscreen (default binding) |
| `Esc` | Cancel active Peek/Blink; otherwise leave fullscreen; otherwise close the viewer |

The effective bindings except `Esc` are user-configurable in Settings → Controls. Peek and Blink are hold commands: the resolved full gesture begins the action once, while release of its primary key ends it even if modifiers changed. The first active hold wins; repeat key-down and a second hold are ignored. Any persistent viewer command, focus loss, sequence replacement, Settings/context-menu transition, shutdown, or `Esc` first restores the temporary presentation. Fullscreen preserves ordinary zoom/pan behavior, and Peek/Blink work identically there after any active hold is canceled before a fullscreen transition.

## Photo Presentation View

Photo Presentation View is an explicit session-local viewing mode and starts disabled on every launch. Its checked context-menu item and configurable `viewer.togglePhotoPresentation` command share one viewport state authority. Each currently presented portrait, landscape, square, or panoramic image is independently fitted as one visual object: photograph plus optional Matte, centered inside an edge-inset presentation rectangle. The persisted margin is a percentage of the shorter physical viewport dimension (`4%` default, normalized to `0–15%`) and therefore has consistent visual proportion across DPI. Stage remains the background outside that object.

The mode owns geometry. Wheel, `+`, `-`, `0`, `1`, double-click, drag pan, permanent/temporary Hand, Peek, and Blink are unavailable and do not exit the mode; explicit navigation, fullscreen, Color Picker, Histogram, Photo Info, Cursor Highlight, and drawing tools remain available. Blink is initially disabled because comparison photographs with a different aspect ratio require independent presentation layout. Entering or resizing recomputes only pure geometry; leaving sets the current photograph to ordinary Fit and restores every normal input. The user's persisted normal image-change policy is never modified.

When highlight is active, a translucent configured circle follows the pointer over photograph and Stage and the system cursor is hidden only while it is inside the photo viewport. Highlight-scoped `[`/`]` change its persisted physical radius by four pixels. Markup scope takes precedence when its dock is visible, so the same bindings instead adjust markup thickness; this cross-scope reuse is intentional and conflict-free. The highlight does not alter navigation, viewport state, Peek, Blink, or drawing input.

The markup dock is visible only after `P`. Its compact project-owned icons expose Hand, Brush, Eraser, Line, Rectangle, Ellipse, Arrow, Undo, Redo, and Clear; color, `1–128` physical-pixel size, and opacity remain compact style controls. The panel is draggable only by its grip, persists normalized client-relative placement, and clamps into the viewer after resize/fullscreen. Icon tooltips use the effective rebound shortcut. Left drag draws/erases for drawing tools or pans through the existing viewport when Hand is permanent/temporarily held; wheel zoom remains available and Hand creates no history. Draw gestures capture their starting color, stroke size, and opacity. Shift snaps Line, Arrow, and the constrained Brush preview to 45-degree directions and makes Rectangle/Ellipse a square/circle; releasing Shift before mouse-up restores the collected freehand Brush preview. Eraser remains unconstrained and full strength. Over the photo, Brush shows its physical color/opacity/size footprint, Eraser shows its diameter, shapes use a precision crosshair, Hand uses a pan cursor, and the system arrow/general Highlight is suppressed until markup closes or the pointer returns from ordinary dock UI. Hiding the dock prevents drawing but leaves committed marks visible. Undo/Redo is image-bound, one continuous gesture is one step, a new operation after Undo drops the redo tail, and Clear is one undoable operation affecting only the current image.

Pointer feedback and floating-panel movement must track native pointer motion without rebuilding the photographic presentation. Passive Highlight/tool-cursor movement changes only the pointer layer; a drawing draft changes only markup plus pointer layers; dock drag changes only its live transform. Photo redraw remains reserved for image, Stage, viewport geometry, or inspection changes.

The Color Picker is hidden by default. Pointer movement shows only lightweight precision feedback and never replaces the fixed sample. A primary click inside the photograph commits one source-pixel sample; Stage and floating-panel clicks commit nothing. The compact movable overlay shows reference-sRGB HEX/RGB(A), one local nearest color name, and the latest ten clicks oldest-to-newest. Its history survives navigation, Peek/Blink, and hide/reopen in the same viewer session, but is never persisted. Picker clicks override markup drawing, hold-Space Hand still pans, wheel still zooms, and active Blink samples the visible comparison image. Detailed semantics are owned by [`COLOR-PICKER.md`](COLOR-PICKER.md).

Configurable shortcuts resolve by code-owned context: shortcut capture, active hold ownership, Markup scope, Highlight scope, then Global scope. Identical gestures may coexist across contextual scopes and a contextual binding shadows Global only while active; within-scope conflicts retain the existing confirmation behavior.

## View behavior

**Fit** shows the entire oriented photograph, preserves aspect ratio, maximizes use of the available viewport, and never crops.

**100%** has photographic physical-pixel meaning: approximately one oriented source pixel per physical display pixel. The technical contract is owned by [`RENDERING.md`](RENDERING.md).

**Manual zoom** uses reasonably fine discrete steps. Each wheel step keeps the source point beneath the cursor at the same viewport position. A future setting may adjust step size. Manual zoom may move image bounds outside the viewport; left-drag panning then moves the view over the image.

Peek temporarily sets physical scale to exactly `1.0`. A pointer over the photograph preserves its source point under the same viewport position within natural bounds; a pointer over Stage uses the current point of interest at viewport center. Left-drag may pan the temporary Peek view, while wheel and double-click are ignored; release restores Fit or the exact prior manual physical scale and normalized point of interest. Blink keeps mouse pan, wheel, and double-click inert and maps Fit to Fit or transfers the current manual physical scale and normalized point of interest to the comparison image.

## Navigation experience

Left/right navigation operates over supported, safe, decodable neighboring files in the opened image's directory. It should feel seamless rather than inserting a routine black frame or spinner between photographs. Unsupported, corrupt, or unsafe images may be skipped without ending navigation. Internal loading policy belongs to [`PERFORMANCE.md`](PERFORMANCE.md) and image viability belongs to [`IMAGING-PIPELINE.md`](IMAGING-PIPELINE.md).

By default, navigation preserves physical scale and normalized point of interest for non-Fit views; an intentionally reduced common scale stays reduced. Fit remains semantic Fit. The user may instead select **Fit each image**, which centers every navigated image in Fit. A newly opened sequence always begins in Fit. The preference is owned by [`SETTINGS.md`](SETTINGS.md).

While Photo Presentation View is active, that ordinary policy is temporarily overridden: every newly presented source is fitted independently together with its Matte. F4 atomic publication remains authoritative, so a pending portrait/landscape target cannot expose future geometry or Matte before its matching photograph.

## Context menu, metadata, and Stage

Rare features live behind right click. `I` or Overlays → Photo Info toggles a compact movable panel for the currently presented image. It starts hidden each application launch, preserves only normalized client-relative placement, follows Blink comparison identity, and leaves Peek unchanged. Missing fields collapse rather than producing placeholder rows. A future Advanced Metadata view remains separate; a persistent EXIF sidebar is not acceptable.

`G` or Overlays → Histogram toggles a separate compact movable panel. It describes whole-image decoded RGB values, not the visible zoom crop, Stage, Matte, Ambient, markup, pointer UI, or future monitor output. It follows Blink comparison identity, remains unchanged for Peek/zoom/pan, starts hidden, and may coexist with Photo Info and markup tools.

Stage background selection is available from both the context menu and Settings. Black remains default; Neutral, Custom, and Ambient apply immediately. Matte is an independent modifier over every background; Settings owns its color, physical width, and outer style while the context menu and `M` retain the uncluttered enable/disable path. Neither choice changes Fit, physical scale, pan, point of interest, photo rectangle, or photo sampling. Ambient remains fixed to the full photograph rather than following viewport zoom/pan. Stage definitions belong to [`PROJECT-VISION.md`](PROJECT-VISION.md).

## Settings principles

Settings expose durable preferences, not ordinary navigation. They open as secondary UI from the context menu or `Ctrl+,` and never occupy the normal viewport. They should use plain choices with sensible automatic defaults and avoid expert jargon where a product concept exists. Section ownership, persistence, and reset behavior belong to [`SETTINGS.md`](SETTINGS.md); performance policy belongs to [`PERFORMANCE.md`](PERFORMANCE.md).

Dark/Light application theme affects controls and secondary UI, never the photograph or Stage. The separation is owned by [`THEMES.md`](THEMES.md).

External single- and multiple-file activation enters the same viewing experience, but its sequence construction and platform behavior belong to [`PLATFORM-INTEGRATION.md`](PLATFORM-INTEGRATION.md). In particular, an ordered explicit multi-file selection is a defined input rather than an implicit directory merge.
