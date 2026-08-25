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

R0 accepted controlled Skia `SKCodec` probing/decoding as the initial JPEG/PNG foundation. R1 implements that path behind a project-owned asynchronous loader and result boundary. The production source representation owns encoded bytes, detected format, encoded/oriented dimensions, orientation, frame count, normalized color state, pixel format, reduced-decode capability, cost estimates, timings, and deterministic native-image ownership. It does not depend on RenderProbe types.

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

EXIF, XMP, IPTC, and other descriptive metadata are useful but optional to ordinary display. Their extraction may be lazy and independently fallible. Metadata parsing must not block navigation unnecessarily. Source ICC/profile data is not optional UI metadata: it must survive to the color boundary even before the full color pipeline exists.

R6-A implements the optional descriptive path through a focused managed `MetadataExtractor` adapter. It reads the exact encoded byte array already retained by `DecodedImage` through a non-copying memory stream, maps useful EXIF values into immutable Fovium types, and contains malformed/no-metadata results without changing decode success. External directory, tag, and rational types do not cross the adapter. Parsing is lazy while Photo Info is visible, background-run, cancellable at publication authority, and count-cached for the current sequence. ICC discoveries remain informational and do not alter rendering.

## Decoder registry direction

A project-owned registry may route probes and decodes among multiple libraries or native/specialized codecs. Backends should expose capabilities and failures in common terms, preserve resource ownership, and honor cancellation when the underlying API allows it. A backend addition must not require navigation, viewport, cache, or renderer redesign.

This registry is internal composition, not a plugin system or third-party extension API.

## Failure and large-image policy

During directory navigation, unsupported, corrupt, truncated, policy-rejected, or oversized candidates may be skipped while navigation continues to the next viable image. Failures must not crash the process or publish stale content.

For direct open, R1 retains the Black Stage and shows a short localized in-viewport error instead of an out-of-memory crash or modal sequence. During navigation, missing, corrupt, unsupported, and policy-rejected candidates are skipped while the previous decoded photograph remains visible. Tiled or region decoding may extend the safe envelope later; it is not part of R1.

Limits come from current available resources, actual representations, concurrent work, and product caps. R0 used a 512 MiB two-BGRA-copy safety guard only to protect the experiment; it is not a permanent limit. Scheduling and cache policy belong to [`PERFORMANCE.md`](PERFORMANCE.md).

## Format-support philosophy

Likely areas include JPEG, PNG, WebP, TIFF, HEIF/HEIC, AVIF, JPEG XL, JPEG 2000, PSD previews, OpenEXR, and embedded RAW previews. This is a research set, not an initial support promise.

R1 advertises JPEG/JPG/PNG candidates and validates actual content through SKCodec. A directly supplied unusual extension is attempted rather than trusted or rejected solely by name. Broader codecs can arrive incrementally without changing core viewer semantics. Candidate technologies are tracked as evaluations in [`THIRD-PARTY.md`](THIRD-PARTY.md).
