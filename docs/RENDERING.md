# Rendering

Role: Technical contract and research agenda for photographic viewport rendering.
Read when: Working on renderer choice, Fit/100%, zoom, pan, DPI, pixel alignment, resize, or display sampling.
Authoritative for: Physical/logical pixel semantics, viewport-state direction, cursor anchoring, and bounded R0 rendering probe requirements.
Not authoritative for: Final renderer selection, image decode policy, ICC implementation, or product-level input bindings.

## Quality premise

Rendering quality is more important than feature count. Avalonia's standard `<Image>` path is not automatically accepted as the production photographic renderer. A custom `Control`, Avalonia `DrawingContext`, Skia/SkiaSharp access, a dedicated viewport renderer, and prepared display representations are candidates to measure, not conclusions.

## Pixel semantics

Avalonia layout is expressed in logical pixels/DIP. Photographic **100%** means:

> One oriented source image pixel maps approximately to one physical display pixel.

Therefore `ScaleTransform = 1.0` or one source pixel per DIP is not sufficient proof of 100%. The renderer must account for effective `RenderScaling` and physical output behavior. R0 must validate Windows scaling at 100%, 125%, 150%, and 200%; HiDPI and per-monitor transitions; representative Linux scaling; Retina behavior when macOS runtime access exists; and physical pixel alignment.

Moving a window between monitors must trigger correct scale and, eventually, destination-color reevaluation without corrupting the user's source-space point of interest.

## Viewport model

Persistent viewport state should be expressed in oriented source-image coordinates plus a physical display scale and viewport geometry. Avalonia transform objects should not be the primary application state. This keeps the model testable and independent of a particular control or backend.

**Fit** computes the largest aspect-preserving scale that contains the entire oriented image inside the available viewport. It never crops.

**100%** uses the physical-pixel mapping above. A double-click toggle returns between Fit and 100%; exact restoration behavior beyond that baseline can be refined with evidence.

**Manual zoom** advances through reasonably fine steps and may place image bounds beyond the viewport. For pointer-anchored zoom, the oriented source point under the pointer before a scale change must remain under the same viewport position afterward, subject only to deliberate boundary policy.

**Pan** changes the source-space point mapped to the viewport. It is available when zoom creates off-viewport content. Boundary behavior should be consistent and should not accumulate logical/physical rounding drift.

Resize, fullscreen changes, and DPI transitions must define whether they preserve Fit, physical scale, or source-space point of interest based on the active view mode. These cases require explicit tests rather than incidental framework behavior.

## Sampling and alignment

The display path may need different sampling for downscale, modest upscale, exact 100%, interactive zoom, and prepared representations. R0 should compare quality, latency, sharpness, edge behavior, alpha handling, and pixel alignment. Popularity of a library is not acceptance evidence.

At exact 100%, avoid unintended blur from fractional physical-pixel placement. At other scales, choose sampling that favors photographic quality while keeping interactive zoom and pan responsive. No final resampling algorithm is selected yet.

## R0 bounded probe

R0 should produce evidence and a decision record, not a prototype that silently becomes architecture. At minimum it must:

- render representative photographs through plausible Avalonia/Skia paths;
- demonstrate Fit, physical-pixel 100%, pointer-anchored zoom, pan, resize, and fullscreen behavior;
- record effective logical-to-physical conversion and inspect pixel alignment across the available DPI matrix;
- exercise window movement between differently scaled monitors where hardware permits;
- preserve oriented dimensions and source color-profile data across the decode/render boundary;
- compare at least one high-quality downscale path and the exact-100% path;
- measure first display, interaction latency, allocations, and native-resource lifetime sufficiently to reject unsafe choices;
- identify what could and could not be validated on the available operating systems and displays.

Color goals and unknowns are owned by [`COLOR-MANAGEMENT.md`](COLOR-MANAGEMENT.md). R0 scope is directional in [`ROADMAP.md`](ROADMAP.md).
