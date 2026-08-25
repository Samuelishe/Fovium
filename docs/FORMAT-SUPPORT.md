# Format support

Role: Current image-format capability matrix.
Read when: Checking whether a format can be discovered, decoded, composited, inspected, or animated.
Authoritative for: Current per-format decode, alpha, animation, metadata, and known format-specific limitations.
Not authoritative for: Probe/decode architecture, cache policy, dependency provenance, platform associations, or future roadmap order.

## Capability model

Fovium owns stable format identities and one immutable production capability table. Known extensions are directory-discovery and file-picker hints; detected encoded content is decode truth. An explicitly supplied unusual extension is still probed, so valid WebP renamed `.jpg` and valid JPEG renamed `.webp` decode according to their bytes. Full filesystem identity remains platform-aware; only extension matching is case-insensitive.

The current pipeline is static-image only. A supported encoded format whose codec reports more than one frame is rejected recoverably as unsupported. Fovium does not display frame zero while implying animation support.

## Current matrix

| Format | Decode | Alpha | Animation | Metadata | Current notes |
| --- | --- | --- | --- | --- | --- |
| JPEG | Yes | N/A | N/A | Partial, read-only | Skia `SKCodec`; EXIF orientation through `EncodedOrigin` |
| PNG | Static | Yes | No | Best-effort, read-only | Multi-frame/APNG is rejected if reported by the codec |
| WebP | Static lossy/lossless | Yes | No | Best-effort, read-only | Skia `SKCodec`; Photo Info base facts always available; animated WebP is rejected |
| TIFF | Bounded static 8-bit | Declared associated/unassociated alpha | Pages: no | Best-effort, read-only | Focused managed backend; classic single-image contiguous grayscale/RGB; see scope below |

TIFF support is deliberately narrower than the container. Proven inputs include classic little- and big-endian TIFF, strip and tiled storage, unsigned 8-bit grayscale/RGB, explicitly declared associated or unassociated alpha, and None/LZW/Deflate/PackBits compression. The decoder preserves all eight TIFF orientation meanings in the common oriented-source descriptor. BigTIFF, multiple directories/pages, samples above 8 bits, floating-point samples, planar-separated data, unspecified extra samples, palette/CMYK/CIELAB/LogLuv and other specialist photometrics, JPEG-in-TIFF, and unproven compression variants are rejected recoverably. The current viewer still uses one complete BGRA8888/Premul raster; TIFF tiling is decoded fully and is not a region-rendering claim.

When an embedded TIFF ICC profile can be normalized by the current Skia color-space boundary, its normalized state is retained. A present but invalid/unusable profile is recorded explicitly rather than mislabeled as untagged sRGB. This remains source-state preservation, not monitor-aware ICC output correctness.

WebP camera/lens/exposure EXIF is available when the current MetadataExtractor backend recognizes it. Metadata absence or failure never invalidates decode. A controlled WebP EXIF fixture showed that SkiaSharp 3.119.4 did not expose its orientation through `SKCodec.EncodedOrigin`; Fovium therefore currently presents such WebP using encoded geometry rather than adding a second eager orientation parser. This limitation is tracked in [`KNOWN-PROBLEMS.md`](KNOWN-PROBLEMS.md).

JPEG, PNG, static WebP, and accepted TIFF share the same BGRA8888/Premul decoded representation, byte-budget cache, Ambient, Peek/Blink, Photo Info, Histogram, and markup boundaries. Transparent PNG/WebP/TIFF pixels reveal Stage or the opaque Matte backing according to the existing alpha contract.

## Not currently supported

Animated WebP/APNG playback, broader/high-bit-depth/multipage TIFF, HEIF/HEIC, AVIF, RAW, JPEG XL, JPEG 2000, PSD, OpenEXR, and other specialized formats are not current decode claims. File associations and thumbnail providers are also separate platform work.

Pipeline mechanics belong to [`IMAGING-PIPELINE.md`](IMAGING-PIPELINE.md); library provenance belongs to [`THIRD-PARTY.md`](THIRD-PARTY.md); future direction belongs to [`ROADMAP.md`](ROADMAP.md).
