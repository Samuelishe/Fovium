# R0 rendering / DPI / decoder / color-foundation probe

Role: Retained technical evidence and recommendation from the bounded R0 experiment.
Read when: Implementing the first viewer slice, revisiting the initial renderer/decoder choice, or comparing later measurements with R0.
Authoritative for: R0 setup, observations, compared options, and the recommendation supported by this experiment.
Not authoritative for: Current project state, product UX, final ICC/codec/huge-image architecture, or permanent performance limits.

## Environment

| Item | R0 environment |
| --- | --- |
| OS | Windows 11 Pro, x64, build 26200 |
| .NET SDK | `10.0.204` |
| Avalonia | `12.1.1` |
| Direct SkiaSharp reference | `3.119.4`, matching the minimum resolved by Avalonia.Skia 12.1.1; independent stable `4.151.1` was researched but not mixed into this graph |
| Runtime display scale | One available `RenderScaling = 1.00` environment |
| Runtime graphics evidence | Avalonia desktop window and direct Skia lease both rendered; the concrete GPU API/backend name was not exposed by this probe |

The solution also compiled against the cross-platform Avalonia desktop packages. This Windows run is not Linux/macOS runtime or multi-monitor evidence.

The official package/release survey also covered ImageSharp 4.1.1, Magick.NET 14.16.0, NetVips 3.2.0/libvips 8.18.5, Little CMS 2.19.1, libheif 1.23.1, and libavif 1.4.2. Their license, platform, decode/color/resampling, and native-packaging tradeoffs are summarized in [`../THIRD-PARTY.md`](../THIRD-PARTY.md); none was installed merely for comparison count.

## Compared paths

### Rendering

1. **Avalonia DrawingContext** — `Avalonia.Media.Imaging.Bitmap` drawn by a custom `Control` with an explicit `RenderOptions.BitmapInterpolationMode` and the same orientation/viewport matrix as the other path.
2. **Direct Skia** — controlled `SKCodec`/`SKBitmap`/`SKImage` source drawn through `SKCanvas.DrawImage` and explicit `SKSamplingOptions`. The bridge is confined to one `ICustomDrawOperation` using Avalonia's public but `[Unstable]` `ISkiaSharpApiLeaseFeature`; no reflection or unsafe integration is used.

Both paths displayed the generated patterns and local JPEG/PNG input without visible orientation, alpha, or geometry corruption in the available runtime. The standard Avalonia path is useful as a baseline, but it does not expose the sampling and source-profile control required of the primary photographic path.

### Decode

1. **Avalonia Bitmap** decoded the same encoded JPEG/PNG bytes into an Avalonia-compatible bitmap.
2. **Skia `SKCodec`** probed dimensions, encoded format, frame count, encoded origin, source color-space state, and reduced-decode dimensions before explicitly decoding to premultiplied BGRA.

For one OS-provided 1920 × 1200 JPEG, a single observed run reported 1.49 ms header/probe, 12.81 ms SKCodec decode, 12.43 ms Avalonia Bitmap decode, and 1.40 ms SKImage preparation. These stopwatch values are comparative smoke evidence, not benchmark results.

The probe supports only JPEG/PNG input because R0 is not the broad codec stage. Malformed input becomes a recoverable probe error. A checked, experiment-local 512 MiB estimate guards the two simultaneous BGRA representations before full decode. This cap is not product policy.

## Pixel and DPI findings

The tested state model is:

`PhysicalScale = physical display pixels / oriented source pixels`

`DipScale = PhysicalScale / RenderScaling`

Therefore photographic 100% is `PhysicalScale = 1.0`, while its DIP scale changes with the active display. Pure tests cover `RenderScaling` 1.00, 1.25, 1.50, and 2.00 for 100%, Fit, logical/source conversions, cursor anchoring, resize recomputation, and physical alignment.

At runtime on the available 1.00-scale display, the one-pixel/checker pattern was crisp at physical 100% when the image origin was aligned to a physical pixel. The probe rounds integral-physical-scale placement in backing-pixel space and converts it back to DIP. That result supports alignment at exact integer physical scales; it is not proof that unconditional rounding is correct at every fractional zoom.

`TopLevel.ScalingChanged` updates the model and recalculates DIP mapping without changing physical zoom. No second DPI display was available, so per-monitor transition behavior was exercised by pure state tests only, not by moving the runtime window between differently scaled monitors.

## Sampling findings

The direct path exposes nearest, linear, linear with linear mipmaps, Mitchell cubic, and Catmull–Rom cubic. Avalonia 12.1.1 maps its public interpolation modes more coarsely: None to nearest, Low/Unspecified to linear, Medium to linear plus mipmaps, and High to Mitchell for upscale or linear plus mipmaps for downscale. It cannot independently request both cubic choices.

Generated one-pixel, diagonal, multi-frequency checker, zone-plate-like, fine-geometry, and alpha-edge patterns were inspected at Fit and exact physical zoom presets of 25%, approximately 33%, 50%, 75%, 100%, 125%, 150%, and 200% through both render paths and all exposed sampling modes. Nearest with physical-pixel alignment is the clearest exact-pixel inspection mode. Linear plus mipmaps is the best initial general Fit/downscale choice among the bounded paths: it avoided the strongest high-frequency instability while remaining responsive. Cubic modes changed edge character, but the available single-display/photo inspection did not demonstrate enough benefit to make a settled second pass mandatory.

Initial policy: use one direct path; nearest at exact integer-pixel inspection and linear+mipmap for general downscale/interaction. Keep explicit switching in diagnostics and collect broader photographic evidence before promoting a cubic settled policy. Do not build a two-stage interactive/settled renderer yet.

## Decoder and orientation findings

`SKCodec` supplies encoded dimensions and all eight encoded-origin values before pixel decode. The experiment represents encoded and oriented dimensions separately, maps all eight EXIF orientation semantics in pure tests, and makes viewport state refer only to oriented coordinates. `GetScaledDimensions` exposes whether the codec advertises a reduced decode size.

`Avalonia.Bitmap` is simple and suitable for the comparison path, but its public representation does not preserve/expose encoded origin or source ICC/profile state. `SKCodec` is therefore the better initial JPEG/PNG probe/decode foundation and can later sit behind a project-owned decoder contract. Broader formats and specialized backends remain open.

## Color findings

`SKCodec.Info.ColorSpace` preserves a normalized Skia color-space object when one is recognized. The tested sRGB JPEG reported a normalized sRGB space; `ToProfile()` produced no usable profile bytes in that run. The SKCodec boundary does not expose the original raw embedded ICC payload as a first-class result. Avalonia Bitmap exposes still less source color information.

R0 policy is explicit: retain the original encoded bytes alongside decoded pixels and normalized color-space state; use sRGB when the source is untagged for probe display only. This avoids making the current decoder representation the sole copy of source color meaning. It does not provide monitor ICC conversion, validate wide gamut, define invalid-profile fallback, or prove that a normalized Skia space is equivalent to retaining the original profile for a future LittleCMS path.

## Resource and lifetime findings

Streams, codecs, color-space/profile wrappers, `SKBitmap`, `SKImage`, `SKPaint`, `SKCanvas`, encoded data, and Avalonia bitmaps have explicit owners and deterministic disposal. Decode runs off the UI thread; only publication and drawing occur on the UI path.

Eight alternating JPEG/PNG reloads left the probe responsive and did not reveal a crash or obvious runaway native allocation. In that one observational run, process working set moved from about 240.3 MiB to 243.4 MiB. This is not a leak test or stable-memory guarantee. Production work must harden ownership across Avalonia's retained/custom-draw lifetime and measure longer replacement/cancellation sequences.

## Recommendation

**ACCEPT Avalonia 12.1.1 as the initial windowing/input/platform host.**

**ACCEPT a direct SkiaSharp 3.119.4 photographic drawing adapter plus controlled SKCodec JPEG/PNG probe/decode as the initial production foundation**, with these constraints:

- keep the `[Unstable]` Avalonia-Skia lease behind one narrow replaceable adapter;
- keep viewport math independent of Avalonia and Skia;
- retain encoded source data and explicit source color state until a real profile-extraction/transform boundary exists;
- keep the Avalonia DrawingContext path as a diagnostic baseline, not the accepted primary photographic renderer;
- do not infer Linux/macOS runtime, multi-monitor DPI, monitor ICC, broad codec, or huge-image correctness from this Windows R0 run.

No blocker requires another pre-R1 experiment. R1 may begin as a small production vertical slice while the unresolved color, cross-platform runtime, custom-draw lifetime, and broad-codec risks remain explicit.
