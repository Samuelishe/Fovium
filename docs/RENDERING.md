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

The Stage path composes explicit layers in this order: selected background, optional independent Matte, original photograph, image-bound markup, viewport-space interaction cursor/highlight, then ordinary floating/error UI. R5-F3-P1 gives those layers separate update frequencies. Stage/Matte/photo remain one low-frequency direct-Skia presentation behind an Avalonia compositor bitmap cache; immutable markup snapshots render through a separate transparent composition visual; the small pointer control redraws only when its style changes and follows passive motion through `TranslateTransform`; floating UI also uses live transform movement. Stage appearance is not an input to photograph sampling. Matte geometry inflates behind the already-resolved photograph bounds and clips to the viewport; it never changes the photograph destination. Its width is persisted in physical pixels (`24` by default, normalized to `4–192`) and converted through current `RenderScaling`.

Photo Info and Histogram are ordinary floating UI above those layers. Histogram result publication invalidates only its small Avalonia plot control; panel drag uses the shared transform path and never enters the direct-Skia photograph or markup render operations. The histogram describes decoded whole-image channels and is not part of a screenshot/composited-pixel pipeline.

Presenter operations are stored in oriented image space and transformed through the resolved photo destination on every draw, so Fit, physical 100%, manual zoom, pan, resize, fullscreen, and Peek need no overlay-state rewrite. Brush, Line, Rectangle, Ellipse, and Arrow replay source-over with the immutable opacity captured by each draw operation; Erase/Clear remain full-strength Clear blending regardless of prior alpha. Physical stroke/eraser size is bounded to `1–128 px` and converted to source width only when a gesture begins. When active markup exists, the markup composition visual clips to the photograph and visible viewport intersection and opens one transparent Skia layer bounded to that intersection. The isolated layer protects photograph, Stage, Matte, and alpha semantics; an empty snapshot pays no markup-layer cost and no permanent source-resolution bitmap exists. Blink selects the temporary image identity before taking its history snapshot; missing comparison markup renders nothing rather than reusing current markup. Brush, Eraser, precision-shape, Hand, and cursor-Highlight feedback remain lightweight Avalonia viewport-space presentation; requested physical dimensions convert to DIP through current `RenderScaling` and never create history.

Matte's initial outer styles are Solid, Rounded, Soft, and Angular. Rounded and Angular derive their radius/chamfer from `1.5 ×` Matte width with geometric clamping. Soft uses a bounded draw-time Skia mask blur over Matte geometry with sigma `width / 3`; it creates no full-viewport buffer or asynchronous preparation. Every style also draws one opaque rectangle exactly beneath the photograph, preserving alpha compositing and the complete rectangular source image. The render-independent geometry records bounds and scalar/point data only; Skia paths remain inside the Stage renderer.

Ambient cover geometry is calculated from a reusable oriented `384 px` long-edge representation and the current viewport. Preparation owns spatial reduction and bounded blur; render-time color treatment owns brightness and saturation so their live changes do not regenerate an `SKImage`. Photograph publication remains first. Before publication, selection synchronously acquires any already-cached derivative with the same image identity and blur; the viewport installs photo plus resolved Stage in one state transition, so it never exposes a new-photo/null-Ambient state when that derivative existed. A genuine miss still uses Black while immediate matching work completes, never the previous image's Ambient. Actual Stage draws count identity-matching and fallback frames because stopwatch latency alone cannot establish visual seamlessness: a `14 ms` gap may still cross one or more display frames depending on phase and refresh rate. Opt-in soak diagnostics also count viewport render, custom-operation scheduling/entry, and Skia-lease availability, so a missing custom draw cannot be mistaken for a successful Stage frame. Defaults are blur `18`, brightness `0.65`, and saturation `0.85`; accepted ranges are `8–32`, `0.30–1.00`, and `0–1.25`. The viewport cover crop is recomputed cheaply on resize without re-decoding or regeneration. Blur changes are coalesced for `150 ms`, latest-wins, and replace one source+blur asset atomically. Neutral is the non-calibrated sRGB presentation value `#505050`; Custom defaults to `#202020`. Matte defaults to `#202020`, is `24` physical pixels converted through `RenderScaling`, and has no shadow. These are centralized presentation defaults, not colorimetric standards.

The production viewport remains independent of Avalonia and Skia. Fit is capped at physical 100%, wheel zoom uses discrete `1.14` steps around the cursor, and internal physical-scale bounds are `0.01` through `64` as transform-safety limits rather than user-facing policy. Double-click from Fit enters physical 100% around the clicked source point; any non-Fit state returns to Fit. Navigation transfers physical zoom and normalized point of interest, while Fit remains Fit.

## Retained R0 evidence

The disposable probe, exact comparisons, measured observations, and limitations are recorded in [`experiments/R0-RENDERING-PROBE.md`](experiments/R0-RENDERING-PROBE.md). R1 implemented the retained direction independently and manually exercised it on Windows at `RenderScaling = 1.00`; fractional/per-monitor and Linux/macOS runtime proof remain open. Color goals and unknowns remain owned by [`COLOR-MANAGEMENT.md`](COLOR-MANAGEMENT.md).

## Windows SDR managed photograph presentation

R8-B-W1 leaves viewport math authoritative. The same oriented destination rectangle, exact physical-100% alignment, nearest exact-pixel policy, and linear+mipmap fractional/downscale policy render into an unpremultiplied reference-sRGB bitmap sized to the visible photograph intersection plus bounded overscan. Little CMS converts those bytes once to monitor-device RGB; alpha is retained and output is premultiplied once into an untagged final image. The direct-Skia operation draws Stage/Matte/Ambient in their existing UI domain, then the matching managed raster, followed by the independent markup/pointer layers. During same-source/same-destination geometry refinement it temporarily rescales/crops the prior device-RGB raster through stored oriented-source coverage and current viewport geometry; this proxy is short-lived, spatially coherent with source-space markup and sampling, and replaced by exact source/reference-to-destination output after interaction settles. It never labels device-RGB bytes as sRGB.

Source and destination changes remain atomic by strict identity; geometry revisions retain the same source/destination authority while proxy and exact quality states change. The canonical source lease and current source-space mapping remain the authority for Color Picker, Histogram, Photo Info, markup, Peek, and Blink. Moving monitors therefore changes only photograph presentation pixels, not zoom, pan, physical 100%, source sampling, or overlay state.
