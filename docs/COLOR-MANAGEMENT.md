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

## Not yet validated implementation direction

Skia color spaces, LittleCMS, native platform color APIs, ImageSharp ICC facilities, and other suitable libraries are research candidates. None is selected or introduced in DOCS-R1. R0 should learn which pixel formats, profile data, transform ownership, and rendering hooks remain viable without committing prematurely to a complete ICC engine.

The initial runnable viewer may be SDR-first and may ship before full monitor ICC behavior, but its architecture must retain enough information to add that behavior. HDR output is future work; current boundaries should avoid assuming all content and destinations are 8-bit sRGB.

## Validation direction

Future acceptance needs known-profile test images, untagged and invalid-profile cases, wide-gamut sources, more than one destination profile, monitor transitions, and comparison against trusted reference output. Diagnostics should distinguish source profile, assumed fallback, destination profile, transform path, and conversion timing without placing this data in the normal viewport.
