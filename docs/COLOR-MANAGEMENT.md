# Color management

Role: Desired color behavior and boundary for future implementation research.
Read when: Working on source profiles, pixel formats, display profiles, monitor transitions, transforms, SDR/HDR, or color-related dependencies.
Authoritative for: Source-profile preservation, destination-profile concept, missing/invalid profile policy direction, multi-monitor behavior, transform boundary, and validation goals.
Not authoritative for: A selected color library, final platform implementation, renderer choice, or current support claims.

## Desired final behavior

Fovium should interpret embedded ICC profiles and common source spaces such as sRGB, Display P3, Adobe RGB, and other wide-gamut sources; transform from the source space to the active monitor's destination profile; and update the destination when the window changes monitors.

The intended boundary is conceptually:

`source pixels + source color description` → `color transform` → `destination-aware display representation`

The transform belongs between source decode and final display, with explicit ownership and caching. Navigation must not know color-library details, and a renderer must not discard source meaning merely because the initial version cannot complete the final transform.

## Source policy

- Preserve valid embedded profile bytes or an equivalently complete project-owned representation through decoding.
- Do not silently collapse tagged wide-gamut data to untagged pixels.
- Treat missing profiles by an explicit fallback policy, likely sRGB for formats/content where that is defensible; validate exceptions by format.
- Treat invalid profiles as recoverable input errors: use a documented safe fallback and retain diagnostic visibility.
- Keep alpha, transfer function, precision, and premultiplication choices explicit at conversion boundaries.

EXIF orientation is imaging correctness rather than color metadata and is owned by [`IMAGING-PIPELINE.md`](IMAGING-PIPELINE.md).

## Destination and monitor behavior

The active destination is associated with the display presenting the photograph, not a single process-wide constant. The application should detect relevant monitor/profile changes and regenerate or select an appropriate display representation without losing viewport state. Multi-monitor behavior must cover monitors with different ICC profiles and scale factors.

If a platform cannot provide reliable destination-profile information, the fallback and diagnostic state must be explicit. Claims of correctness require measured output or platform evidence, not an assumption that Avalonia or Skia handles the entire monitor-aware pipeline automatically.

## R0/R1 findings and not-yet-validated direction

R0 introduced SkiaSharp for the experiment. `SKCodec.Info.ColorSpace` preserves a normalized Skia color-space object when recognized, but the tested boundary did not expose the original embedded ICC payload and did not yield usable equivalent profile bytes for the inspected sRGB JPEG. Avalonia Bitmap does not expose the needed source profile/orientation facts publicly. The initial boundary therefore retains original encoded bytes plus explicit normalized/fallback color state rather than keeping decoded pixels alone.

R1 retains the original encoded bytes and records whether SKCodec reported assumed sRGB, normalized sRGB, or normalized non-sRGB. Decode creates a premultiplied BGRA representation using the recognized Skia color space when available and assumes sRGB for untagged input. This is the inherited probe policy, not final color management; it does not claim monitor-aware conversion, wide-gamut correctness, raw ICC preservation, or invalid-profile correctness.

R3 derives Ambient from that same currently accepted decoded/display representation into an sRGB premultiplied BGRA presentation surface. It leaves encoded bytes, source profile state, and the original photographic `SKImage` untouched. Ambient colors, Neutral, and matte are presentation defaults only: they do not establish monitor ICC, wide-gamut, or calibrated-gray correctness, and the derivation boundary remains replaceable with the future destination-aware path.

R6-B Histogram counts the RGB channel values in the currently owned decoded representation before Stage, markup, and any future monitor transform. It must not be described as an sRGB or display-output histogram for every source. Histogram analysis is read-only and does not alter source-profile retention or rendering policy.

R7-A routes static WebP through the same `SKCodec.Info.ColorSpace` and BGRA8888/Premul source-state policy as JPEG/PNG. This preserves the existing normalized-sRGB/non-sRGB distinction when Skia exposes it, but it is not evidence of raw WebP ICC preservation or monitor-aware correctness.

R7-B inspects the standard TIFF ICC profile tag at its backend boundary. When Skia can normalize the embedded bytes, the final bitmap carries that normalized color space and the descriptor records sRGB/non-sRGB state; unusable embedded bytes are recorded as known-but-unpreserved rather than falsely reported as untagged assumed sRGB. Exact source bytes remain retained. This is a truthful source-state bridge, not broad TIFF profile validation or monitor-aware output correctness.

R7-C inspects HEIF/AVIF ICC and NCLX before assigning source state. When valid ICC exists without governing NCLX, it is normalized through the current Skia boundary; unusable known profile data is recorded rather than mislabeled untagged. For common SDR NCLX, libheif 1.23.1 converts its requested RGB output to sRGB, so the descriptor records `NormalizedSrgbFromNclx` plus the original primaries/transfer/matrix values instead of claiming assumed sRGB. Wide-gamut SDR is not confused with HDR, although its current presentation is normalized at this boundary. Explicit PQ and HLG primaries are rejected without tone mapping. Depth is ignored and HDR gain-map enhancement is not reproduced. These are source-truth and SDR policies, not monitor-aware Color Management.

R8-A Color Picker values are explicitly reference sRGB. Existing sRGB-normalized decoded states are sampled directly; a valid normalized non-sRGB Skia representation transforms only the selected unpremultiplied pixel to sRGB. A known but unpreserved source profile produces an `Approximate` sample instead of a false exact claim. These stable interchange HEX/RGB values are independent of the active monitor. Future monitor-aware source-to-destination-display transforms must not redefine them as framebuffer or emitted-display RGB. High-bit-depth/HDR picker semantics remain future work.

R8-B-P1 preserves this production boundary and adds isolated evidence in [`experiments/R8-B-MONITOR-COLOR-MANAGEMENT-PROBE.md`](experiments/R8-B-MONITOR-COLOR-MANAGEMENT-PROBE.md). SkiaSharp 3.119.4 source-to-destination drawing matched Little CMS 2.19 relative-colorimetric output exactly for six official sRGB/Display-P3 matrix/TRC patches, preserved alpha, and left source pixels unchanged. It nevertheless rejected a valid official ICC v4 LUT display profile that Little CMS accepted, so Skia-only is not a general monitor-profile solution. No production dependency or render change is selected.

R8-B-N1 adds only the reproducible native prerequisite under [`../eng/native/lcms2/`](../eng/native/lcms2/): pinned Little CMS 2.19 source, app-local RID bundles, provenance/license manifests, dependency audits, and native matrix/TRC plus real CLUT smoke. Windows is locally proven and the required Linux/macOS hosted matrix remains pending. The library is not referenced by production Fovium, Monitor Color Management stays disabled, and Color Picker/Histogram semantics remain unchanged.

The real Windows Avalonia direct-Skia target was untagged: neither its surface nor snapshot exposed a destination `SKColorSpace`. The real probe `HWND` successfully resolved its assigned bounded RGB display ICC through official monitor/DC APIs. For ordinary Windows SDR with Advanced Color off, app-side conversion is viable; Advanced Color treats legacy 8-bit app content as sRGB and therefore needs an explicit unsupported/app-managed fallback until a tagged path is proven. macOS should use a once-only ColorSync/compositor path, X11 requires real per-output mapping such as colord, and Wayland expects source/content description through `color-management-v1` rather than preconversion to one physical monitor. These platform paths remain productization gates.

The recommended first rendering ownership is a viewport-sized destination presentation derived from the canonical `DecodedImage`, retained only for the current destination and active Blink comparison. A 24 MP probe measured about 96.2 MiB additional private memory for a full destination frame versus about 8.3 MiB for a 1920 × 1080 destination. Color Picker remains reference sRGB, Histogram remains source-domain, and source pixels/cache ownership never change on a destination switch.

The initial runnable viewer is SDR-first and retains encoded source data so a future profile extractor/transform path is not blocked by the R1 decoded bitmap boundary. HDR output is future work; current boundaries must avoid presenting R1's 8-bit BGRA display representation as the only possible source representation.

## Validation direction

Future acceptance needs known-profile test images, untagged and invalid-profile cases, wide-gamut sources, more than one destination profile, monitor transitions, and comparison against trusted reference output. Diagnostics should distinguish source profile, assumed fallback, destination profile, transform path, and conversion timing without placing this data in the normal viewport.
