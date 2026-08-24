# Platform integration

Role: Contract for external file activation and operating-system integration.
Read when: Working on command-line/shell activation, Open With, file associations, packaging, document icons, thumbnails, or platform-specific entry points.
Authoritative for: Activation semantics, multiple-file policy, association behavior, format-capability needs, thumbnail boundaries, and Windows/Linux/macOS integration direction.
Not authoritative for: Decoder implementation, navigation loading policy, Settings persistence, shipped branding assets, or current platform support claims.

## Boundary

Platform integration is an edge subsystem. It translates operating-system activation and shell capabilities into project-owned requests and consumes imaging capabilities; it does not belong inside the imaging pipeline and must not spread registry, MIME, bundle, or shell APIs through application code.

The future activation shape must accept an ordered collection of paths, conceptually `IReadOnlyList<string>`, even when one file is common. This is a requirement, not a mandated interface name.

Inputs may contain Unicode, spaces, long names, platform-specific syntax, and paths supplied by shell activation. Fovium must eventually support `fovium <path>` and equivalent native activation. Startup must not assume that a Fovium file picker produced the request.

## Single-file activation

Opening exactly one supported image follows the normal viewer model:

```text
open file
↓
create a viable sequence from its directory
↓
make the opened file current
↓
browse previous/next neighbors
```

This creates no import, catalog, or database.

## Multiple-file activation

Exactly two behaviors are valid:

### Mode A — containing folder

Use the first supplied file as the initial item and build navigation from its containing directory.

### Mode B — explicit selection

Use only the supplied files, preserving their supplied order as the navigation sequence. Files may come from multiple directories. Existing viability rules may skip unsupported, corrupt, or unsafe entries without inventing additional directory merging.

**Mode B — explicit selection is the default.**

The future preference is located at:

```text
Settings
→ Advanced
→ Multiple-file activation

When multiple files are opened

● Browse selected files only
○ Browse the folder containing the first file
```

Do not add a third implicit merge mode. Preference persistence belongs to [`SETTINGS.md`](SETTINGS.md).

## File associations and Open With

Fovium should eventually register the image types it can actually open. Registration advertises capability; the operating system and user retain ownership of the default-app choice. Fovium must never silently seize or rewrite default associations.

Settings may later offer **Open system file association settings** or a platform equivalent. CONTRACTS-R1 adds no registry, MIME, bundle, or default-app implementation.

Expected directions differ by platform:

| Platform | Direction |
| --- | --- |
| Windows | Application and supported-type registration, Open With, system-owned default-app selection, document icon, and a thumbnail provider where useful |
| Linux | `.desktop` and MIME registration, `mimeapps.list` ecosystem cooperation, and desktop/file-manager-specific thumbnail integration where feasible |
| macOS | Bundle document types, Launch Services Viewer role, document icon, and Quick Look thumbnail integration where useful |

Do not claim identical capabilities, APIs, packaging, or thumbnail behavior across these platforms.

## Format capability model

A future central Fovium-owned capability description must be able to answer, per format and where relevant per platform:

```text
CanDecode
CanNavigate
CanReadMetadata
CanGenerateThumbnail
CanRegisterAssociation
```

This prevents shell registration from promising formats the active build cannot safely handle. It is project-owned capability composition, not a plugin system and not a requirement that every format use one decoder. Skia, specialized native codecs, libheif, libavif, libvips, or other backends may contribute behind the imaging contracts owned by [`IMAGING-PIPELINE.md`](IMAGING-PIPELINE.md).

## Thumbnail policy

Prefer the operating system's provider where it already produces a good preview:

```text
good system thumbnail support
→ use the system provider

no useful system thumbnail and Fovium can safely generate one
→ Fovium thumbnail provider

thumbnail generation unavailable
→ registered Fovium image/document icon
```

Thumbnail support must not depend on Fovium being the default application.

The fallback icon is platform file-type/document-icon registration, not a fake thumbnail painted into every unsupported shell. Branding assets may later live under `resources/branding/` only after real assets and provenance exist; CONTRACTS-R1 creates none.

## Thumbnail architecture and safety

A thumbnail provider must not launch or compose the full Fovium application UI. It must not require Avalonia Settings, Stage, navigation, ordinary startup, or full viewer composition. Its conceptual inputs and work are bounded to:

```text
input stream/path
format probe
safe decode
orientation
requested target size
thumbnail conversion
output bitmap
```

Required policy:

- bounded memory and bounded parallelism;
- no network dependency;
- safe malformed-input handling;
- reduced decode where available;
- embedded preview where appropriate;
- no unnecessary full-resolution decode for 128, 256, or 512 px output.

For future RAW browsing, prefer a suitable embedded JPEG preview before full RAW processing. Fovium remains a viewer, not a RAW processor.

Thumbnail reuse may eventually justify a reusable imaging assembly, but only after production imaging evidence exists. Do not create `Fovium.Imaging` solely for this hypothetical integration.

## Packaging and current status

Association registration, bundle/MIME metadata, document icons, thumbnail providers, and packaging are future platform work. None is implemented in CONTRACTS-R1, and no platform parity claim follows from these contracts.
