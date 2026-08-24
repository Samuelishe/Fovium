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

## Sampling and alignment

The production display path selects sampling from physical scale. R0 compared the candidates for quality, edge behavior, alpha, and pixel alignment; later changes still require evidence rather than library popularity.

At exact integer physical scales, align the image origin in backing-pixel space; R0 showed a crisp one-pixel pattern at 100% on the available display. Do not apply rounding indiscriminately at every fractional zoom.

R1 uses nearest plus physical backing-pixel origin alignment at exact integer physical scales. General Fit, fractional zoom, downscale, and interaction use linear filtering with linear mipmaps; fractional origins are not unconditionally rounded. There is one renderer for interactive and settled states because R0 did not justify a second representation.

R3 keeps that photograph path unchanged and composes explicit layers in this order: Stage background, optional matte, original photograph, then ordinary temporary/error UI outside the photographic draw operation. Stage mode is not an input to photograph sampling. Matte geometry inflates behind the already-resolved photograph bounds and clips to the viewport; it never changes the photograph destination.

Ambient cover geometry is calculated from a reusable oriented `384 px` long-edge representation and the current viewport. The representation is strongly blurred (`18 px` sigma at preparation resolution), desaturated to `0.55`, and darkened to `0.45`; its viewport cover crop is recomputed cheaply on resize without re-decoding or regeneration. Neutral is the non-calibrated sRGB presentation value `#505050`. Matte is `24` physical pixels converted through `RenderScaling`, uses `#202020`, and has no shadow in R3. These are centralized R3 defaults subject to later visual evidence, not colorimetric standards.

The production viewport remains independent of Avalonia and Skia. Fit is capped at physical 100%, wheel zoom uses discrete `1.14` steps around the cursor, and internal physical-scale bounds are `0.01` through `64` as transform-safety limits rather than user-facing policy. Double-click from Fit enters physical 100% around the clicked source point; any non-Fit state returns to Fit. Navigation transfers physical zoom and normalized point of interest, while Fit remains Fit.

## Retained R0 evidence

The disposable probe, exact comparisons, measured observations, and limitations are recorded in [`experiments/R0-RENDERING-PROBE.md`](experiments/R0-RENDERING-PROBE.md). R1 implemented the retained direction independently and manually exercised it on Windows at `RenderScaling = 1.00`; fractional/per-monitor and Linux/macOS runtime proof remain open. Color goals and unknowns remain owned by [`COLOR-MANAGEMENT.md`](COLOR-MANAGEMENT.md).
