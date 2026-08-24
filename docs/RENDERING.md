# Rendering

Role: Technical contract and research agenda for photographic viewport rendering.
Read when: Working on renderer choice, Fit/100%, zoom, pan, DPI, pixel alignment, resize, or display sampling.
Authoritative for: Physical/logical pixel semantics, viewport-state direction, cursor anchoring, and the accepted initial rendering foundation.
Not authoritative for: Final lifetime renderer choice, image decode policy, ICC implementation, or product-level input bindings.

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

The display path may need different sampling for downscale, modest upscale, exact 100%, interactive zoom, and prepared representations. R0 compared explicit candidates for quality, edge behavior, alpha, and pixel alignment; later changes still require evidence rather than library popularity.

At exact integer physical scales, align the image origin in backing-pixel space; R0 showed a crisp one-pixel pattern at 100% on the available display. Do not apply rounding indiscriminately at every fractional zoom.

The initial direct-Skia policy is nearest for exact-pixel inspection and linear plus linear mipmaps for general Fit/downscale and interaction. Mitchell and Catmull–Rom remain available research choices; R0 did not justify a separate settled representation or two-stage renderer.

## Retained R0 evidence

The disposable probe, exact comparisons, measured observations, and limitations are recorded in [`experiments/R0-RENDERING-PROBE.md`](experiments/R0-RENDERING-PROBE.md). That report is evidence, not a production architecture owner. Color goals and unknowns remain owned by [`COLOR-MANAGEMENT.md`](COLOR-MANAGEMENT.md).
