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

The exact types and library boundaries are deliberately deferred to R0.

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

Orientation is part of baseline display correctness. Downstream dimensions, viewport math, and 100% semantics refer to the **oriented** source image. The pipeline must apply or carry the orientation exactly once and make that choice unambiguous.

EXIF, XMP, IPTC, and other descriptive metadata are useful but optional to ordinary display. Their extraction may be lazy and independently fallible. Metadata parsing must not block navigation unnecessarily. Source ICC/profile data is not optional UI metadata: it must survive to the color boundary even before the full color pipeline exists.

## Decoder registry direction

A project-owned registry may route probes and decodes among multiple libraries or native/specialized codecs. Backends should expose capabilities and failures in common terms, preserve resource ownership, and honor cancellation when the underlying API allows it. A backend addition must not require navigation, viewport, cache, or renderer redesign.

This registry is internal composition, not a plugin system or third-party extension API.

## Failure and large-image policy

During directory navigation, unsupported, corrupt, truncated, policy-rejected, or oversized candidates may be skipped while navigation continues to the next viable image. Failures must not crash the process or publish stale content.

For direct open, the future UI should retain a stable Stage and show a quiet, actionable message instead of an out-of-memory crash or a sequence of modal dialogs. Tiled or region decoding may extend the safe envelope later; it is not assumed for the initial viewer.

Limits come from current available resources, actual representations, concurrent work, and product caps. Permanent numeric caps are not selected in DOCS-R1. Scheduling and cache policy belong to [`PERFORMANCE.md`](PERFORMANCE.md).

## Format-support philosophy

Likely areas include JPEG, PNG, WebP, TIFF, HEIF/HEIC, AVIF, JPEG XL, JPEG 2000, PSD previews, OpenEXR, and embedded RAW previews. This is a research set, not an initial support promise.

Initial support should be intentionally bounded by validated backends and test assets. Broader codecs can arrive incrementally without changing core viewer semantics. Candidate technologies are tracked as evaluations in [`THIRD-PARTY.md`](THIRD-PARTY.md).
