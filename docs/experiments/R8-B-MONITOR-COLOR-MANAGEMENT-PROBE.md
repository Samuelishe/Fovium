# R8-B monitor color-management probe

Role: Canonical evidence for the R8-B-P1 monitor color-management architecture and rendering investigation.
Read when: Selecting or implementing the first bounded SDR monitor color-management product stage.
Authoritative for: R8-B-P1 measurements, platform findings, rejected assumptions, and the productization blocker.
Not authoritative for: Current product support, future owner approval, or HDR behavior.

## Question and boundary

R8-B-P1 asks how Fovium can derive a destination-aware photographic presentation from the retained source representation without mutating it, changing viewport/navigation state, redefining Color Picker values, or turning Histogram into a display histogram. It is an isolated experiment at product version `0.1.0.0007`; production rendering is unchanged.

The investigated first product scope is 8-bit SDR photography. Stage, Matte, Ambient, markup, overlays, and the general Avalonia UI remain ordinary UI/sRGB presentation in the initial architecture. HDR, PQ/HLG, scRGB, EDR, gain maps, high-depth output, and whole-UI color management remain out of scope.

Experiment source is under [`../../experiments/Fovium.ColorManagementProbe/`](../../experiments/Fovium.ColorManagementProbe/). It uses Avalonia `12.1.1` and SkiaSharp `3.119.4`, adds no production dependency, and never enters the normal viewer path.

## Current source-color inventory

`DecodedImage` owns exact encoded bytes, an `ImageDescriptor`, and a shared BGRA8888/Premul `SKBitmap`/`SKImage`. It does not retain a second raw ICC payload. The bitmap/image is tagged with the best truthful `SKColorSpace` available at decode time.

| Descriptor state | Pixel meaning | Retained `SKColorSpace` | Raw ICC separately retained | Truthful source-to-destination transform now |
| --- | --- | --- | --- | --- |
| `AssumedSrgb` | Untagged input decoded under the documented sRGB assumption | sRGB | No; exact encoded bytes remain | Yes, under the explicit assumption |
| `NormalizedSrgb` | Decoded values are normalized/identified as sRGB | sRGB | No; exact encoded bytes remain | Yes |
| `NormalizedSrgbFromNclx` | libheif produced sRGB output from accepted SDR NCLX and recorded the source tuple | sRGB | No standalone ICC; exact container remains | Yes from the normalized sRGB representation |
| `NormalizedNonSrgb` | Values belong to the retained normalized non-sRGB Skia space | non-sRGB `SKColorSpace` | No; exact encoded bytes remain | Yes for profile classes Skia can represent |
| `EmbeddedProfileUnpreserved` | Profile presence is known, but decoded fallback pixels cannot be advertised as an exact interpretation | fallback sRGB presentation tag | No; exact encoded bytes remain recoverable | No; only an explicitly approximate fallback |

Backend details:

- JPEG, PNG, and static WebP use `SKCodec.Info.ColorSpace`: null becomes assumed sRGB, recognized sRGB becomes normalized sRGB, and a recognized non-sRGB space becomes normalized non-sRGB.
- TIFF reads the bounded ICC tag. A valid Skia color space is attached; unusable embedded bytes become `EmbeddedProfileUnpreserved` rather than assumed sRGB.
- HEIF/AVIF bound ICC payloads to 16 MiB and inspect NCLX. Governing supported SDR NCLX is decoded to sRGB and recorded as `NormalizedSrgbFromNclx`; valid ICC without governing NCLX is attached; unusable known data is unpreserved.

A retained normalized `SKColorSpace` is enough to perform its supported source transform. Original ICC bytes are not required merely to repeat that transform. They remain valuable for diagnostics and for reprocessing profiles Skia cannot represent. `SKColorSpace.ToProfile()` returned no serializable bytes for the constructed sRGB, Display P3, and Adobe-like spaces, so normalized Skia state is not a raw-profile recovery mechanism.

## Corpus and provenance

The ignored `resources/test-images/color-management/` corpus contained project-authored numeric patches plus three public ICC Registry profiles: sRGB 2014 v2 matrix/TRC, Display P3 v4 matrix/TRC, and the sRGB v4 preference display-class LUT profile. Exact URLs and SHA-256 values are recorded in the experiment [README](../../experiments/Fovium.ColorManagementProbe/README.md). A copy of the workstation's assigned Windows sRGB profile and derived truncated, damaged-magic, and impossible-declared-size files remained ignored. No monitor profile or public corpus file is tracked.

## Skia ICC and pixel evidence

`SKColorSpace.CreateIcc` accepted the official sRGB 2014 and Display P3 matrix/TRC profiles and rejected malformed, truncated, and invalid-size inputs without native failure. The experiment's own parser rejects empty, over-16-MiB, short, invalid-signature, impossible-size, and malformed tag-table inputs into `InvalidDestinationProfile`.

Skia rejected the valid official 60,988-byte ICC v4 preference display-class profile containing A2B/B2A LUT tags. Little CMS 2.19 accepted and transformed that same profile: LUT-sRGB → Display P3 mapped `196,83,41` to `183,95,62`, while Display P3 → LUT-sRGB mapped it to `211,66,0`. This is the decisive limitation: SkiaSharp 3.119.4 alone is not a truthful general monitor-ICC engine.

For supported matrix/TRC profiles, Skia and a developer-only Little CMS 2.19 relative-colorimetric reference produced identical 8-bit output for all six measured patches:

| Transform | Input | Skia | Little CMS |
| --- | --- | --- | --- |
| Display P3 → sRGB | `196,83,41` | `212,73,19` | `212,73,19` |
| Display P3 → sRGB | `64,180,140` | `0,183,137` | `0,183,137` |
| Display P3 → sRGB | `32,128,240` | `0,130,248` | `0,130,248` |
| sRGB → Display P3 | `196,83,41` | `182,90,53` | `182,90,53` |
| sRGB → Display P3 | `64,180,140` | `98,178,143` | `98,178,143` |
| sRGB → Display P3 | `32,128,240` | `63,126,232` | `63,126,232` |

SkiaSharp exposes no rendering-intent choice through `SKColorSpace`; the measured matrix/TRC results match Little CMS relative colorimetric. Relative colorimetric is the recommended first photographic policy, with no user-facing intent setting. If Little CMS is selected, black-point compensation and other intent details need their own bounded validation.

Opaque alpha remained 255. A half-alpha Display P3 patch transformed to unpremultiplied `211,74,22,128`, within expected 8-bit premultiplication rounding, and transparent output was canonical `0,0,0,0`. Color conversion is therefore delegated to a trusted unpremul/transform/premul path, never applied as a byte matrix to premultiplied channels. Geometry, dimensions, orientation, and viewport mapping are unchanged.

## Avalonia direct-Skia target

The real Windows Avalonia `ISkiaSharpApiLeaseFeature` exposed an `SKCanvas`, `SKSurface`, and `GRContext`, but both the leased surface and snapshot reported a null/untagged destination `SKColorSpace`. The platform handle was a real `HWND`. Fovium can draw a source-tagged `SKImage`, but this target does not tell Skia the active display profile and the lease offers no API to assign one. Drawing to an untagged surface did not perform a destination transform.

Therefore the existing direct-Skia draw is not already monitor-aware, and render-time strategy C cannot be implemented by simply attaching a monitor ICC to the leased Avalonia surface. It would require either a separate destination-tagged intermediate presentation or a future platform surface/content-description integration.

## Windows destination evidence

The probe maps the real Avalonia `HWND` through `MonitorFromWindow`, obtains the monitor device name with `GetMonitorInfoW`, creates that display DC, and retrieves its active profile with `GetICMProfileW`. On the tested Windows workstation this returned a valid 3,144-byte ICC v2.1 RGB display profile described as `sRGB IEC61966-2.1`, SHA-256 prefix `2B3AA1645779A9E6`; no full local path is retained here. `QueryDisplayConfig` reported Advanced Color unsupported/disabled and 8 bits per channel.

For ordinary SDR desktop output with Advanced Color inactive, Windows does not automatically color-manage an untagged legacy application surface; an app-side transform to the selected display ICC is viable. When Advanced Color is active, Windows automatically treats 8-bit legacy application content as sRGB and maps it to the display. Preconverting such content to physical monitor RGB and then presenting it through the current untagged target would risk a second transform. The first product stage must therefore classify Advanced Color/HDR desktop mode as `UnsupportedDisplayMode` for app-managed monitor ICC and present the normal reference-sRGB fallback until an explicitly tagged Advanced Color surface path is proven.

VCGT/calibration curves are detected only for diagnostics. Display calibration loading belongs to Windows/calibration software or display hardware; an image CMM must not apply VCGT a second time.

Viable refresh triggers are window position/screen changes, Avalonia screen-topology change, application activation, and Windows display/settings notifications. Production should re-read bounded profile bytes and compare identity; a path-only change with identical bytes must not rebuild transforms.

## macOS destination findings

The isolated compile/API probe binds CoreGraphics/ColorSync only on macOS: `CGMainDisplayID` → `ColorSyncProfileCreateWithDisplayID` → `ColorSyncProfileCopyData`. The production mapping must instead obtain the active `NSScreen`/display ID for the viewer window. No physical macOS monitor was available in this Windows run, so neither Avalonia target tagging nor real ColorSync/WindowServer presentation is validated.

macOS is compositor/ColorSync-managed in normal app presentation. App-side preconversion to physical monitor RGB must not be enabled until the Avalonia surface semantics are measured on a real Mac; otherwise double management is possible. The ColorSync provider remains the profile/identity diagnostic path, while a once-only source-description/compositor path is the preferred architecture.

## Linux, X11, colord, and Wayland

X11 can expose per-output assignment through desktop conventions and colord. `_ICC_PROFILE` alone is not a universal modern solution. A colord adapter would need output-to-device mapping and D-Bus lifecycle/error handling; R8-B-P1 adds no D-Bus package and does not select one yet.

Wayland `color-management-v1` is architecturally different: clients describe source/content color on a surface and the compositor transforms it for one or more outputs. A client-preconverted physical-monitor raster is wrong for a window spanning displays and can be transformed again. The protocol also provides preferred/output descriptions and bounds ICC payloads, but compositor support is optional. Avalonia 12.1.1 does not expose the required Fovium surface-description integration in the current direct-Skia lease. Do not hand-build a private Wayland stack inside the viewer; require an Avalonia/platform integration stage or use an explicit sRGB/unmanaged fallback.

## Double-management conclusion

| Platform state | Safe initial interpretation |
| --- | --- |
| Windows SDR, Advanced Color off | App transforms source to the selected output ICC; current target is untagged and Windows does not add the missing app transform |
| Windows Advanced Color/HDR on | Do not preconvert through the current legacy surface; Windows treats it as sRGB and manages it, so physical-monitor preconversion can double-transform |
| macOS | Treat the compositor as color-management authority until real Avalonia/ColorSync target evidence proves a different once-only boundary |
| Linux X11 | App-side per-output ICC may be viable after colord/output mapping and engine selection |
| Linux Wayland | Describe source/content to the compositor; do not preconvert to one physical output profile |

No single assumption such as “always preconvert to monitor RGB” is portable.

## Rendering strategies and measurements

Release CPU-raster measurements used a synthetic 6,000 × 4,000 (24 MP) Display P3 source and sRGB destination. They are architectural evidence from one workstation, not performance thresholds.

| Candidate | Destination size | Draw/convert | Snapshot | Destination raster | Observation |
| --- | ---: | ---: | ---: | ---: | --- |
| A. Full-source representation | 6000 × 4000 | ~483.8 ms | ~0.19 ms | 96,000,000 bytes | Exact reusable full frame, but high switch latency and one extra full raster |
| B. Viewport representation | 1920 × 1080 | ~55.4 ms | ~0.02 ms | 8,294,400 bytes | Bounded to visible presentation; must regenerate for material scale/pan changes |
| C. Render-time destination surface | 1920 × 1080 | ~55.2 ms | none | target/intermediate still required | Similar transform work, but the Avalonia lease cannot carry the destination ICC directly |

Private-memory observations in the same harness were approximately 192.2 MiB for the retained 24 MP source `SKBitmap` plus `SKImage`, 288.4 MiB with a full destination, and 200.7 MiB with a viewport destination. Thus Strategy A added about 96.2 MiB while Strategy B added about 8.3 MiB. The source observation also indicates that current estimator assumptions should be revisited separately; R8-B-P1 does not change cache accounting.

The recommended first implementation is Strategy B: derive only a viewport/display-sized destination presentation from the retained canonical source, and retain at most the current visible source/destination pair plus the active Blink comparison using the same destination. Do not transform every cached/preloaded image for every monitor. Fit/100%/zoom/pan choose source sampling geometry; color conversion remains independent. Resampling and color conversion should stay inside the selected trusted engine/render operation rather than applying speculative byte math.

To avoid a navigation or monitor-transition color flash, retain the last truthful frame or neutral Stage until the new source/destination presentation is ready, then publish it atomically. Never publish wrong-monitor color and silently replace it later. Peek changes geometry only and reuses the same destination identity. Blink combines the currently visible source identity with the current destination identity, never an older monitor destination.

## Destination selection, identity, cache, and fallback

Select the monitor containing the largest positive intersection with the viewer window, matching `MonitorFromWindow` behavior. Retain the current monitor on an exact-area tie to prevent boundary flapping; retain the last valid destination while minimized/off-screen, then refresh on the next valid placement. This is more stable than center-only switching and more explicit than an opaque screen-name mapping.

Destination equivalence is profile-byte SHA-256 plus relevant output state such as Advanced Color mode, not a profile path. A bounded transform key is:

`source color identity + destination profile identity/output state + pixel format/alpha semantics + rendering intent`

Transform construction can be reused under that key, but no unbounded process cache is recommended. Presentation ownership is current window/session state, not `DecodedImage` or the global decoded-image cache.

Fallback states are explicit: `Managed`, `DestinationUnavailable`, `InvalidDestinationProfile`, `UnsupportedSourceProfile`, `UnsupportedDisplayMode`, and `PlatformUnsupported`. Missing, unreadable, zero-byte, malformed, oversized, or unsupported destination profiles never crash or block normal viewing. The viewer continues with its truthful existing source/reference-sRGB presentation and diagnostics record why the result is not monitor managed. No modal UI is required.

## Source-owned feature invariants

Two simulated destinations produced different presentation RGB while leaving the input source pixel unchanged. R8-A Color Picker still acquires the canonical presented `DecodedImage` and converts its one sample to reference sRGB; it never reads the destination presentation. Histogram still reads the same decoded source-owned pixels and therefore requires no recomputation when the destination changes. Ambient remains the current UI/sRGB aesthetic background for the first product stage. These ownership boundaries preserve Picker, Histogram, Ambient, viewport, navigation, overlays, history, and markup across monitor transitions.

## Recommended production path

- **Windows SDR / Advanced Color off:** real `HWND` → largest-intersection `HMONITOR` → official display-profile API; bounded ICC validation; destination-aware viewport-sized photographic presentation.
- **Windows Advanced Color/HDR:** explicit unsupported app-managed state and normal reference-sRGB legacy presentation until a tagged Advanced Color/DXGI path is separately proven.
- **macOS:** active-window display ID → ColorSync profile identity for diagnostics; validate and then use a once-only compositor/source-description path, never blind physical-RGB preconversion.
- **Linux X11:** separately validate per-output colord mapping and app-side transform.
- **Linux Wayland:** compositor `color-management-v1` source/content description; do not preconvert to one monitor; wait for or build a bounded Avalonia platform integration only after owner selection.
- **Transform engine:** Little CMS is the recommended general ICC engine because valid LUT display profiles exceed SkiaSharp 3.119.4 capability. A dedicated owner-approved native-runtime feasibility/supply-chain stage is required before it can enter production. Skia remains valid evidence/reference for supported matrix/TRC transforms, not the universal engine.
- **Rendering/cache:** Strategy B, a viewport-sized derived presentation; source `DecodedImage` stays canonical; cache only current destination presentation and active Blink comparison.
- **Selection/refresh:** largest positive window intersection with current-monitor tie retention; refresh on screen/window/topology/activation/profile changes and compare profile hash plus output mode.
- **Fallback:** typed non-modal fallback to the existing source/reference-sRGB presentation; never claim managed output when destination, source, platform, or display mode is unsupported.

## Productization gate

**BLOCKED.** The probe establishes a viable Windows SDR architecture and enough evidence to scope the next bounded prerequisite, but a universal Skia-only implementation is disproved by a valid LUT display profile. The owner must explicitly decide whether to authorize a reproducible Little CMS native-runtime stage. Real macOS target/compositor evidence, an X11 profile-source decision, and Wayland surface-description integration also remain platform gates; real multi-monitor hardware transition evidence remains pending.

## R8-B-N1 follow-up

The owner subsequently accepted R8-B-P1 and R8-B-N1. Accepted N1 commit `5155b7806703a657d89ab2923fd2936814a37a16` owns pinned Little CMS 2.19 under [`../../eng/native/lcms2/`](../../eng/native/lcms2/) and has green hosted `win-x64`, `linux-x64`, and `osx-arm64` native evidence plus normal CI. R8-B-W1 then implements the bounded Windows ordinary-SDR product path at `0.1.0.0008`: active-monitor profile discovery, app-local production interop, viewport-sized destination pixels, latest-wins publication, and explicit fallback. This appendix does not rewrite the historical probe result. macOS/Linux monitor output and Windows Advanced Color/HDR remain unimplemented.

## Primary references

- [Skia color management](https://docs.skia.org/docs/user/color/)
- [Avalonia direct-Skia lease source](https://github.com/AvaloniaUI/Avalonia/blob/main/src/Skia/Avalonia.Skia/ISkiaSharpApiLeaseFeature.cs)
- [Windows `MonitorFromWindow`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-monitorfromwindow)
- [Windows `GetICMProfileW`](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-geticmprofilew)
- [Windows Advanced Color and ICC profiles](https://learn.microsoft.com/en-us/windows/win32/wcs/advanced-color-icc-profiles)
- [Apple ColorSync display profile API](https://developer.apple.com/documentation/colorsync/colorsyncprofilecreatewithdisplayid%28_%3A%29)
- [Wayland color-management protocol](https://wayland.app/protocols/color-management-v1)
- [Wayland color-management model](https://wayland.freedesktop.org/docs/book/Color.html)
- [colord profile API](https://www.freedesktop.org/software/colord/gtk-doc/colord-cd-profile.html)
- [Little CMS releases](https://github.com/mm2/Little-CMS/releases)
