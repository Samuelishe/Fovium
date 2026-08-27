# R8-B-W1 seamless Color Management rendering probe

Role: Focused evidence for replacing geometry-dependent Windows SDR managed rasters.
Read when: Changing the R8-B-W1 photograph render architecture, managed-source ownership, or GPU/LUT direction.
Authoritative for: W1/F1/F2 regression history, Candidate A/B measurements, and the F3 architecture selection.
Not authoritative for: Platform support beyond Windows ordinary SDR, HDR policy, or general decoded-cache limits.

## Problem and baseline

The probe started from clean committed baseline `8f8ea8f7f263ec29855530e0a37af593dabe728b` (`fix: prevent managed zoom-out clipping`). No reset, revert, parent checkout, or history rewrite occurred. W1 created viewport-sized destination rasters and exposed Black while geometry work was pending. F1 retained/reprojected old detail and removed full Black frames, but could present incomplete source coverage. F2 retained a full-source viewport-density base and removed clipping, but owner video showed a visibly soft base followed by an asynchronous detail sharpness snap and a short center stitch/squeeze.

Those outcomes share one cause: viewport geometry selected the dimensions and coverage of the monitor-device raster. A geometry event could therefore change both the spatial implementation and the pixel resolution presented to the user. Overscan, extra tiers, or a shorter debounce cannot remove that coupling.

## Candidate A — full-source managed image

Candidate A reads the canonical encoded-size Skia image into unpremultiplied reference-sRGB BGRA, applies the app-local Little CMS 2.19 transform in place, premultiplies once into an untagged encoded-size BGRA image, and retains that image for the active source/destination identity. Little CMS 2.19's own testbed exercises same-format in-place transforms. `NormalizedNonSrgb` is truthfully normalized by the Skia `ReadPixels` color-space conversion; already sRGB-normalized states take the same efficient full-source read path. `EmbeddedProfileUnpreserved` remains in the existing fallback state.

The managed image has the canonical encoded dimensions and orientation semantics. `SkiaPhotoDrawOperation` selects canonical or managed pixels, then calls one shared orientation/viewport/sampling function. The managed key contains image identity, destination identity, encoded size, and orientation; it contains no viewport geometry.

Local Windows measurements used the Fovium-owned `lcms2.dll` reporting 2.19 and the project-authored matrix destination fixture. Values are one warmed representative run, not an SLA:

| Source | Dimensions | Full-source preparation | Managed retained pixels | Temporary reference pixels | Total canonical + managed steady pixels | Peak pixel storage |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| ~2 MP | 1920 × 1080 | 38.4–38.9 ms | 8.29 MB | 8.29 MB | 16.59 MB | 24.88 MB |
| 15 MP | 5000 × 3000 | 297.5–327.3 ms | 60 MB | 60 MB | 120 MB | 180 MB |
| 24 MP | 6000 × 4000 | 453.9–481.2 ms | 96 MB | 96 MB | 192 MB | 288 MB |
| 50 MP calculated | representative 50,000,000 pixels | not timed | 200 MB | 200 MB | 400 MB | 600 MB |

Two real 15 MP Release application runs against the assigned `sRGB IEC61966-2.1` display profile measured 48.60–58.28 ms source read, 163.36–163.53 ms Little CMS, and 66.70–67.22 ms finalization (279.35–288.34 ms total). After publication, 50 wheel steps, five seconds of pan, four resizes, Peek, and Fit/100% produced 260–261 managed frames from one request and one completed CMM operation: `geometryRequests=0`, `coalesced=0`, `stale=0`, `failures=0`. New source or destination preparation remains asynchronous; geometry interaction does not pay that cost. The current implementation does not pre-transform adjacent decoded-cache entries.

## Candidate B — render-time GPU LUT

SkiaSharp 3.119.4 exposes `SKRuntimeEffect`, runtime shader builders, child shaders, and runtime color filters. The actual Windows Avalonia direct-Skia lease reported a non-null `GRContext`; a runtime shader compiled and drew successfully into the same untagged leased surface. Two bounded runs recorded/submitted 200 640 × 360 runtime-shader draws in 23.90–28.43 ms total (about 0.119–0.142 ms per recorded draw). This establishes backend feasibility, not production frame-time acceptance for a photo shader plus LUT texture.

Little CMS sampled 3D RGB LUTs at 17³, 33³, and 65³. Trilinear CPU evaluation was compared with direct Little CMS for 16,384 deterministic random RGB samples. Generation timings below are observed two-run ranges:

| Destination | Grid | Bytes (BGRA nodes) | Generation | Mean channel error | Maximum channel error |
| --- | ---: | ---: | ---: | ---: | ---: |
| Matrix/TRC | 17³ | 19,652 | 0.42–0.61 ms | 0.2849 | 1 |
| Matrix/TRC | 33³ | 143,748 | 2.32–2.49 ms | 0.2413 | 1 |
| Matrix/TRC | 65³ | 1,098,500 | 4.22–6.02 ms | 0.2000 | 1 |
| Project CLUT | 17³ | 19,652 | 0.25–0.31 ms | 2.7802 | 124 |
| Project CLUT | 33³ | 143,748 | 1.45–1.49 ms | 1.1608 | 101 |
| Project CLUT | 65³ | 1,098,500 | 9.08–10.53 ms | 0.8179 | 97 |

The matrix path is accurate, but the mandatory project CLUT destination retains unacceptable sparse outliers even at 65³. A larger/exact 8-bit LUT, robust atlas interpolation, source-non-sRGB shader composition, texture lifetime, and real photo render cost would require a separate bounded investigation. Candidate B is therefore feasible at the actual GPU/API boundary but did not pass the universal ICC fidelity gate for this correction.

## Geometry oracle

An opaque asymmetric high-frequency source includes changing single-pixel features and a center-line discontinuity. Canonical and copy-transform managed sources are rendered through 50 Fit/zoom/pan/resize destinations, exact physical 100%, and Normal/90°/180°/270°/horizontal-mirror orientations. Frame bytes are identical. There is no second Color Management destination rectangle, coverage map, or resolution tier, so candidate-specific missing/duplicated center rows/columns and delayed soft-to-sharp replacements have no publication event through which to occur.

The real assigned sRGB-like destination was also tested over 65,536 deterministic RGB samples: mean channel difference from identity was 0.0013 and maximum difference was one 8-bit level; alpha was unchanged. This supplies the near-identity color oracle while the shared renderer supplies exact spatial equivalence.

## Decision

| Property | Committed F2 | Candidate A | Candidate B | Hybrid |
| --- | --- | --- | --- | --- |
| Geometry regeneration | Yes | No | No | No for established path |
| Zoom/pan CMM work | Deferred raster work | Zero | Zero | Zero if correctly partitioned |
| Temporary blur/snap | Observed | None from CM | None in concept | Path-transition risk |
| Seam risk | Separate coverage geometry | Same renderer as canonical | Shader-coordinate implementation | Highest |
| Color fidelity | Direct Little CMS | Direct Little CMS | CLUT outliers up to 97 | Mixed |
| Valid ICC LUT support | Yes | Yes | Not yet accurate enough | Yes only via fallback branch |
| 15 MP managed memory | Viewport bounded | +60 MB | LUT-dependent | Both policies |
| 24 MP managed memory | Viewport bounded | +96 MB | LUT-dependent | Both policies |
| First-frame/navigation cost | Viewport conversion | ~327/481 ms at 15/24 MP probe | LUT generation/upload | Branch-dependent |
| Implementation complexity | High base/detail/coverage state | One source + existing renderer | Shader/LUT atlas and source-space composition | Highest |

Candidate A is selected and implemented as an ordinary forward correction. It is the only measured candidate that combines direct Little CMS matrix/CLUT fidelity with the existing renderer's exact spatial behavior. Candidate B and the hybrid are rejected for this stage; they remain research options only if future full-source memory or source-transition latency becomes a demonstrated blocker.

Productization gate: locally PASS for the F3 implementation, pending owner visual review and hosted normal/native matrices. Color Picker remains canonical reference-sRGB, Histogram remains source-domain, and markup continues to use the shared viewport source-coordinate transform. macOS/Linux monitor output and Windows Advanced Color/HDR remain outside this stage.
