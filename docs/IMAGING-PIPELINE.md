# Imaging pipeline

Role: Contract for turning an image source into safe, semantically complete image data.
Read when: Working on format detection, probing, orientation, decoding, metadata extraction, large-image safety, or codec extensibility.
Authoritative for: Header probe, decode planning, decoder registry direction, source representation, metadata boundary, resource safety, and format-support philosophy.
Not authoritative for: Final codec libraries, viewport behavior, display sampling, cache scheduling, or metadata UI.

## Pipeline direction

The intended flow is:

1. Discover a candidate file without trusting its extension as format proof.
2. Probe headers cheaply before expensive decode.
3. Reject or defer work that violates a runtime safety policy.
4. Select an appropriate decoder backend through a project-owned contract.
5. Decode into a source representation that preserves orientation and color meaning.
6. Extract optional metadata without coupling it to correct display.
7. Prepare a display representation through the rendering/color path.

R0 accepted controlled Skia `SKCodec` probing/decoding as the initial JPEG/PNG foundation. R7-A extends that backend to static WebP behind a project-owned capability. R7-B adds a high-level dispatcher with one shared decode gate and a second focused managed backend for bounded TIFF. The production source representation owns encoded bytes, Fovium-detected format identity, encoded/oriented dimensions, orientation, frame/page count semantics, normalized color state, pixel format, reduced-decode capability, cost estimates, timings, and deterministic native-image ownership. It does not expose backend types or depend on RenderProbe types.

## Header probe and decode plan

When available, the probe reports:

- detected format and confidence;
- encoded and oriented width/height;
- bit depth or pixel representation;
- EXIF orientation or equivalent transform;
- frame/page count when relevant;
- embedded color-profile presence and accessible profile data;
- an approximate decoded memory cost;
- enough backend capability information to form a decode plan.

Probe failures and incomplete metadata must be represented explicitly. File size alone is never a sufficient safety signal.

Approximate cost should include dimensions, bytes per pixel or channel layout, frames needed, working surfaces, color-conversion buffers, mip/resample preparations, and backend overhead where known. For example, `30000 × 20000 × 4` bytes is about 2.24 GiB before additional working memory and may be refused even if its encoded file is small.

## Orientation and metadata boundary

Orientation is part of baseline display correctness. Downstream dimensions, viewport math, and 100% semantics refer to the **oriented** source image. The pipeline must apply or carry the orientation exactly once and make that choice unambiguous. R1 maps all eight SKCodec encoded origins to an explicit orientation transform, retains encoded dimensions separately, and renders through oriented coordinates; pure tests cover every orientation.

JPEG orientation continues through that path. A controlled WebP EXIF-orientation fixture showed SkiaSharp 3.119.4 returning the normal origin, so WebP orientation metadata is not currently applied to presentation. The lazy metadata adapter can read useful WebP EXIF fields, but it is deliberately not pulled into foreground decode as a second orientation parser. This is a documented correctness limitation, not a claim that WebP orientation is supported.

EXIF, XMP, IPTC, and other descriptive metadata are useful but optional to ordinary display. Their extraction may be lazy and independently fallible. Metadata parsing must not block navigation unnecessarily. Source ICC/profile data is not optional UI metadata: it must survive to the color boundary even before the full color pipeline exists.

R6-A implements the optional descriptive path through a focused managed `MetadataExtractor` adapter. It reads the exact encoded byte array already retained by `DecodedImage` through a non-copying memory stream, maps useful EXIF values into immutable Fovium types, and contains malformed/no-metadata results without changing decode success. External directory, tag, and rational types do not cross the adapter. Parsing is lazy while Photo Info is visible, background-run, cancellable at publication authority, and count-cached for the current sequence. ICC discoveries remain informational and do not alter rendering.

R6-B adds a separate read-only pixel-analysis path. A retained pixel lease shares `DecodedImage` native ownership for the duration of sequential BGRA8888/Premul access; no source reopen, re-decode, or full-image copy occurs. Transparent pixels are excluded and partial-alpha channels are unpremultiplied before binning. EXIF orientation does not require another oriented copy because rotation does not change channel counts.

## Decoder registry direction

A project-owned registry routes probes and decodes among focused libraries or native/specialized codecs. R7-A establishes the first small capability table for JPEG, PNG, and WebP plus a narrow Skia detected-format mapping. R7-B adds TIFF identity while keeping backend implementation out of the capability record. Extensions remain discovery/picker hints. Actual content yields Fovium identity before descriptor publication: the cheap TIFF signature distinguishes classic TIFF, BigTIFF, and `NotMyFormat`; otherwise Skia performs its own content detection. The backend contract distinguishes success, not-my-format, unsupported variant, corrupt data, resource limit, and decode failure. One high-level two-slot gate bounds all expensive backends.

The initial TIFF backend uses BitMiracle.LibTiff.NET only behind imaging. It accepts one classic directory/page, unsigned 8-bit contiguous grayscale/RGB and explicitly declared alpha, and the compression/storage subset proven in [`FORMAT-SUPPORT.md`](FORMAT-SUPPORT.md). It reads decompressed scanlines or tiles directly into the final BGRA8888/Premul bitmap, so no backend-specific full raster survives construction. Orientation remains a descriptor transform and is applied exactly once downstream. BigTIFF, multipage, high-bit-depth, floating-point, planar-separated, unspecified-extra-sample, and specialist-photometric input is rejected recoverably. The library's process-global default stderr error handler is replaced once with a thread-safe Fovium Debug-only diagnostic handler; it is never swapped per decode.

This registry is internal composition, not a plugin system or third-party extension API.

## Failure and large-image policy

During directory navigation, unsupported, corrupt, truncated, policy-rejected, or oversized candidates may be skipped while navigation continues to the next viable image. Failures must not crash the process or publish stale content.

For direct open, R1 retains the Black Stage and shows a short localized in-viewport error instead of an out-of-memory crash or modal sequence. During navigation, missing, corrupt, unsupported, and policy-rejected candidates are skipped while the previous decoded photograph remains visible. Tiled or region decoding may extend the safe envelope later; it is not part of R1.

Limits come from current available resources, actual representations, concurrent work, and product caps. R0 used a 512 MiB two-BGRA-copy safety guard only to protect the experiment; it is not a permanent limit. Scheduling and cache policy belong to [`PERFORMANCE.md`](PERFORMANCE.md).

## Format-support philosophy

Current per-format truth is owned by [`FORMAT-SUPPORT.md`](FORMAT-SUPPORT.md). Future areas include TIFF, HEIF/HEIC, AVIF, JPEG XL, JPEG 2000, PSD previews, OpenEXR, and embedded RAW previews. This is a research set, not a support promise.

R7-B advertises JPEG/JPG/PNG/WebP/TIF/TIFF candidates and validates actual content through the backend dispatcher. A directly supplied unusual extension is attempted rather than trusted or rejected solely by name. The current pipeline is one-static-image-only: any supported Skia payload reporting multiple frames and any TIFF reporting multiple directories/pages is rejected recoverably. Broader codecs can arrive incrementally without changing core viewer semantics. Candidate technologies are tracked as evaluations in [`THIRD-PARTY.md`](THIRD-PARTY.md).
