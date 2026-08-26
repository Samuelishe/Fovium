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

LittleCMS, native platform color APIs, ImageSharp ICC facilities, and other suitable libraries remain research candidates. No final ICC engine or destination-monitor integration is selected.

The initial runnable viewer is SDR-first and retains encoded source data so a future profile extractor/transform path is not blocked by the R1 decoded bitmap boundary. HDR output is future work; current boundaries must avoid presenting R1's 8-bit BGRA display representation as the only possible source representation.

## Validation direction

Future acceptance needs known-profile test images, untagged and invalid-profile cases, wide-gamut sources, more than one destination profile, monitor transitions, and comparison against trusted reference output. Diagnostics should distinguish source profile, assumed fallback, destination profile, transform path, and conversion timing without placing this data in the normal viewport.
