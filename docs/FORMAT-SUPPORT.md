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

WebP camera/lens/exposure EXIF is available when the current MetadataExtractor backend recognizes it. Metadata absence or failure never invalidates decode. A controlled WebP EXIF fixture showed that SkiaSharp 3.119.4 did not expose its orientation through `SKCodec.EncodedOrigin`; Fovium therefore currently presents such WebP using encoded geometry rather than adding a second eager orientation parser. This limitation is tracked in [`KNOWN-PROBLEMS.md`](KNOWN-PROBLEMS.md).

JPEG, PNG, and static WebP share the same BGRA8888/Premul decoded representation, byte-budget cache, Ambient, Peek/Blink, Photo Info, Histogram, and markup boundaries. Transparent PNG/WebP pixels reveal Stage or the opaque Matte backing according to the existing alpha contract.

## Not currently supported

Animated WebP/APNG playback, TIFF, HEIF/HEIC, AVIF, RAW, JPEG XL, JPEG 2000, PSD, OpenEXR, and other specialized formats are not current decode claims. File associations and thumbnail providers are also separate platform work.

Pipeline mechanics belong to [`IMAGING-PIPELINE.md`](IMAGING-PIPELINE.md); library provenance belongs to [`THIRD-PARTY.md`](THIRD-PARTY.md); future direction belongs to [`ROADMAP.md`](ROADMAP.md).
