# Project vision

Role: Product identity and long-term product boundary.
Read when: Proposing a feature, evaluating product fit, or making experience-level tradeoffs.
Authoritative for: Intended user, photographer-centric philosophy, zero-UI intent, Stage concept, Peek/Blink product direction, non-goals, and feature-creep test.
Not authoritative for: Exact interaction bindings, implementation architecture, rendering math, or current progress.

## Identity and audience

Fovium is an authored, cross-platform desktop photo viewer. Windows is the primary target, Linux is a full target, and macOS should become a full target only after real runtime testing supports that claim.

It is made first for photographers and other users who value faithful presentation, speed, precise control, seamless navigation, color management, and strong format coverage. It is not optimized for the broadest possible audience.

> Fovium is a viewer for photographs, not UI around photographs.

> The photograph is Fovium's primary UI component.

Rendering quality and interaction feel outrank feature count.

## Zero-UI is intentional

The normal viewing state is the photograph with nearly all application chrome absent. This is a product contract, not a discoverability defect to solve with onboarding, hints, navigation arrows, persistent toolbars, filename overlays, or hover edge zones.

Rare actions belong in a context menu, keyboard shortcuts, Settings, or temporary overlays requested by the user. Observable interaction details belong to [`UX-CONTRACT.md`](UX-CONTRACT.md).

Settings may be sophisticated because they are explicitly opened and remain absent during viewing; their structure is owned by [`SETTINGS.md`](SETTINGS.md). Application UI theme affects controls and secondary surfaces, not the photograph or its Stage; [`THEMES.md`](THEMES.md) owns that separation.

## Stage

The presentation space around the photograph is the **Stage**, not leftover window background. Its background is one of:

- **Black**: the baseline.
- **Neutral**: a controlled neutral alternative.
- **Custom**: an explicit user-selected opaque solid color.
- **Ambient**: derived from the photograph, cover-filled, strongly blurred, darkened, and moderately desaturated so it never competes with the original.

An optional persisted **Matte** is an independent modifier over any background. It may use a Solid, Rounded, Soft, or Angular outer presentation with a configurable physical width and color, while the photograph itself always remains a complete rectangle. Stage processing does not alter the photograph or viewport state. Ambient is a bounded, strongly simplified derivative of the full oriented photograph and remains stable during zoom/pan; Matte adds separation behind the resolved photograph bounds without shrinking them. The shared state is available through the context menu and Settings.

## Photographer-specific direction

**Peek 100%** is a whole-viewport press-and-hold action: hold `Z` to inspect exact photographic 100% around the cursor/source point, then release to restore the prior semantic view. Its purpose is immediate sharpness checking without a loupe, overlay, or persistent viewport mutation.

**Blink Compare** is a press-and-hold action: hold `Shift+C` to see the previous viable image with Fit or the current physical scale and point of interest, then release to return immediately to the retained current presentation. Comparison never navigates or changes canonical selection.

Both use the shared shortcut system, allow one transient mode at a time, and add no persistent viewport UI. Selected-reference A/B and combined compare-at-100 remain future possibilities requiring evidence from real use.

**Presenter overlay** is an explicitly requested, temporary presentation aid: `H` toggles a translucent cursor highlight and `P` toggles a compact movable drawing dock. Brush, Line, Rectangle, Ellipse, Arrow, and true partial Eraser operations are bound to oriented photograph coordinates for the current in-memory session. Draw operations capture color, physical stroke size, and opacity; Shift constrains familiar shapes and can temporarily preview Brush as a straight 45-degree-snapped stroke. Hand and temporary Space-Hand reuse viewer pan without creating history. Physical drawing cursors expose the active Brush/Eraser footprint or a restrained shape target. Per-image Undo/Redo and undoable Clear replay that bounded operation history. They are not source edits, annotation documents, sidecars, layers, object-editing tools, or exportable content; hiding the dock preserves marks.

**Photo Info** is an on-demand zero-layout aid: configurable `I` shows a restrained movable panel for the photograph actually being presented, then hides it without changing the photograph. It summarizes useful photographic facts rather than dumping raw tags. Metadata is read-only, partial, session-local, and independently fallible; it is not a catalog, editor, sidecar, rating, or tagging system.

## Non-goals

Fovium must not drift into a:

- Lightroom replacement or RAW editor;
- DAM, database-backed library, catalog, or importer;
- file manager or photo organizer;
- general media suite;
- plugin platform;
- hardware benchmarking utility;
- generic AI photo product.
- paint, annotation-authoring, or collaboration system.

## Feature-creep test

A proposed feature belongs only if it materially improves viewing, inspection, navigation, presentation, or the correctness and reliability behind those actions. If it primarily manages a collection, edits source content, advertises itself in the viewport, or serves hypothetical platform breadth, reject or defer it.

Long-term success means photographs appear quickly, correctly, and without distraction, while controls feel exact enough that the viewer disappears from attention.
