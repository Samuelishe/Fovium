# Rendering

Role: Technical contract for photographic viewport rendering and its remaining research agenda.
Read when: Working on renderer choice, Fit/100%, zoom, pan, DPI, pixel alignment, resize, or display sampling.
Authoritative for: Physical/logical pixel semantics, viewport-state direction, cursor anchoring, and the accepted initial rendering foundation.
Not authoritative for: Image decode policy, ICC implementation, or product-level input bindings.

## Quality premise

Rendering quality is more important than feature count. R0 accepted Avalonia as the UI/window host and a small direct-Skia custom draw adapter as the initial photographic path. Avalonia `DrawingContext.DrawImage` remains a useful comparison path but is not the primary renderer: its public interpolation choices are coarser and its bitmap boundary does not expose enough source semantics. The direct bridge currently uses Avalonia's public but `[Unstable]` Skia lease and must remain isolated and replaceable.

## Pixel semantics

Avalonia layout is expressed in logical pixels/DIP. Photographic **100%** means:

> One oriented source image pixel maps approximately to one physical display pixel.

Therefore `ScaleTransform = 1.0` or one source pixel per DIP is not sufficient proof of 100%. The accepted model uses `PhysicalScale = physical pixels / oriented source pixels` and `DipScale = PhysicalScale / RenderScaling`; 100% sets `PhysicalScale = 1.0`. Pure tests cover 1.00, 1.25, 1.50, and 2.00. Runtime R0 evidence is limited to a Windows `RenderScaling = 1.00` display, so fractional-DPI and cross-platform runtime acceptance remain required.

Moving a window between monitors must trigger correct scale and, eventually, destination-color reevaluation without corrupting the user's source-space point of interest.

## Viewport model

Persistent viewport state should be expressed in oriented source-image coordinates plus a physical display scale and viewport geometry. Avalonia transform objects should not be the primary application state. This keeps the model testable and independent of a particular control or backend.

**Fit** computes the largest aspect-preserving scale that contains the entire oriented image inside the available viewport. It never crops.

**100%** uses the physical-pixel mapping above. A double-click toggle returns between Fit and 100%; exact restoration behavior beyond that baseline can be refined with evidence.

**Manual zoom** advances through reasonably fine steps and may place image bounds beyond the viewport. For pointer-anchored zoom, the oriented source point under the pointer before a scale change must remain under the same viewport position afterward, subject only to deliberate boundary policy.

**Pan** changes the source-space point mapped to the viewport. It is available when zoom creates off-viewport content. Boundary behavior should be consistent and should not accumulate logical/physical rounding drift.

Resize, fullscreen changes, and DPI transitions must define whether they preserve Fit, physical scale, or source-space point of interest based on the active view mode. These cases require explicit tests rather than incidental framework behavior.

R4 temporary inspection stores a semantic `ViewTransfer`, never an Avalonia transform. Peek sets `PhysicalScale = 1.0` around the source point under the current pointer. If the pointer is outside the current photograph, it uses the existing normalized point of interest at viewport center. Bounds clamp naturally; release reapplies the captured Fit or manual physical scale plus normalized point of interest under current geometry, so geometry/DPI changes do not replay stale raw coordinates. Temporary Peek pan is discarded.

Blink temporarily replaces only the photographic presentation. Fit transfers as Fit; every non-Fit state transfers the same physical scale and normalized point of interest to the comparison dimensions with ordinary clamping. The canonical image and its semantic view remain retained and are restored directly on release. Solid Stage backgrounds remain unchanged. Under Ambient, the comparison may use only its own already prepared derivative for the active blur; absence or mismatch renders the normal Black fallback. Existing Matte settings are recomputed around the temporary destination rectangle.

## Sampling and alignment

The production display path selects sampling from physical scale. R0 compared the candidates for quality, edge behavior, alpha, and pixel alignment; later changes still require evidence rather than library popularity.

At exact integer physical scales, align the image origin in backing-pixel space; R0 showed a crisp one-pixel pattern at 100% on the available display. Do not apply rounding indiscriminately at every fractional zoom.

R1 uses nearest plus physical backing-pixel origin alignment at exact integer physical scales. General Fit, fractional zoom, downscale, and interaction use linear filtering with linear mipmaps; fractional origins are not unconditionally rounded. There is one renderer for interactive and settled states because R0 did not justify a second representation.

The Stage path keeps photograph rendering unchanged and composes explicit layers in this order: selected background, optional independent Matte, original photograph, image-bound markup, then viewport-space cursor highlight and ordinary temporary/error UI. Stage appearance is not an input to photograph sampling. Matte geometry inflates behind the already-resolved photograph bounds and clips to the viewport; it never changes the photograph destination. Its width is persisted in physical pixels (`24` by default, normalized to `4–192`) and converted through current `RenderScaling`.

Presenter operations are stored in oriented image space and transformed through the resolved photo destination on every draw, so Fit, physical 100%, manual zoom, pan, resize, fullscreen, and Peek need no overlay-state rewrite. When active markup exists, the renderer clips to the photograph and visible viewport intersection, opens one transparent Skia layer bounded to that intersection, and chronologically replays source-over draw operations plus Clear-blended Erase/Clear operations. The isolated layer protects photograph, Stage, Matte, and alpha semantics; an empty snapshot pays no layer cost and no permanent source-resolution bitmap exists. Blink selects the temporary image identity before taking its history snapshot; missing comparison markup renders nothing rather than reusing current markup. Cursor highlight and the Eraser diameter outline remain later Avalonia viewport-space presentation, with physical size converted by `RenderScaling`.

Matte's initial outer styles are Solid, Rounded, Soft, and Angular. Rounded and Angular derive their radius/chamfer from `1.5 ×` Matte width with geometric clamping. Soft uses a bounded draw-time Skia mask blur over Matte geometry with sigma `width / 3`; it creates no full-viewport buffer or asynchronous preparation. Every style also draws one opaque rectangle exactly beneath the photograph, preserving alpha compositing and the complete rectangular source image. The render-independent geometry records bounds and scalar/point data only; Skia paths remain inside the Stage renderer.

Ambient cover geometry is calculated from a reusable oriented `384 px` long-edge representation and the current viewport. Preparation owns spatial reduction and bounded blur; render-time color treatment owns brightness and saturation so their live changes do not regenerate an `SKImage`. Defaults are blur `18`, brightness `0.65`, and saturation `0.85`; accepted ranges are `8–32`, `0.30–1.00`, and `0–1.25`. The viewport cover crop is recomputed cheaply on resize without re-decoding or regeneration. Blur changes are coalesced for `150 ms`, latest-wins, and replace one source+blur asset atomically. Neutral is the non-calibrated sRGB presentation value `#505050`; Custom defaults to `#202020`. Matte defaults to `#202020`, is `24` physical pixels converted through `RenderScaling`, and has no shadow. These are centralized presentation defaults, not colorimetric standards.

The production viewport remains independent of Avalonia and Skia. Fit is capped at physical 100%, wheel zoom uses discrete `1.14` steps around the cursor, and internal physical-scale bounds are `0.01` through `64` as transform-safety limits rather than user-facing policy. Double-click from Fit enters physical 100% around the clicked source point; any non-Fit state returns to Fit. Navigation transfers physical zoom and normalized point of interest, while Fit remains Fit.

## Retained R0 evidence

The disposable probe, exact comparisons, measured observations, and limitations are recorded in [`experiments/R0-RENDERING-PROBE.md`](experiments/R0-RENDERING-PROBE.md). R1 implemented the retained direction independently and manually exercised it on Windows at `RenderScaling = 1.00`; fractional/per-monitor and Linux/macOS runtime proof remain open. Color goals and unknowns remain owned by [`COLOR-MANAGEMENT.md`](COLOR-MANAGEMENT.md).
