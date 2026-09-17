# Color Picker

Role: Canonical owner for Fovium's photographic Color Picker semantics.
Read when: Changing sampling geometry, reference-color interpretation, names,
history, picker input precedence, or its floating overlay.

## Product boundary

The Color Inspector is a compact, offline inspection tool. It is hidden by default
and `viewer.toggleColorPicker` toggles it (`K` by default). The context-menu
entry, shortcut, panel, and checked state share this command/session authority.
The movable panel overlays the photograph without resizing the viewport; only
its normalized position is persisted. Its horizontal presentation keeps a
bounded selectable recent list at left and stable color detail at right; it is
an inspector, not a color editor or a permanent palette.

Pointer motion never commits or replaces a sample. An explicit primary click
inside the presented photograph commits exactly one sample. Stage, Matte,
markup, pointer feedback, and overlay UI are not sampled. While the picker is
active, its ordinary photo click takes precedence over markup drawing. Holding
Space gives the existing temporary Hand precedence for pan; wheel zoom remains
available.

## Pixel and presentation identity

A click retains the currently presented `DecodedImage` before reading pixels.
Ordinary and Peek presentation therefore sample the canonical photograph;
active Blink samples its visible comparison lease. The synchronous retained
request cannot mix geometry from one presentation with pixels from another.

The viewport point is mapped through the exact rendered destination rectangle
to continuous oriented-source coordinates. Fovium floors each coordinate to
the containing source pixel and treats the destination's right and bottom
edges as exclusive. It then applies the descriptor's EXIF orientation inverse
exactly once to locate the encoded BGRA pixel. HEIF/AVIF output already
normalized to `Orientation.Normal` is not transformed again. This source-pixel
rule is stable across Fit, photographic 100%, manual zoom, pan, Peek, Blink,
fullscreen, and the tested pure render-scaling geometry.

## Reference sRGB and alpha

Displayed HEX, RGB (A), and nearest names describe a reference-sRGB
interpretation of the photograph sample. They are not monitor-framebuffer,
OS-compositor, or emitted-display values. Moving the window to another monitor
must not redefine an ordinary picker value when future monitor-aware Color
Management is added.

Decoded pixels are BGRA8888/Premul. Opaque and partial-alpha samples are
unpremultiplied once before naming and presentation. Opaque values use
`#RRGGBB` and `RGB R, G, B`; partial alpha uses `#RRGGBBAA` and
`RGBA R, G, B, A`. Alpha does not pull the naming point toward black. For zero
alpha, lost hidden RGB cannot be recovered: the sample is `#00000000`, named
the localized `Transparent`, and bypasses nearest-name matching.

`AssumedSrgb`, `NormalizedSrgb`, and `NormalizedSrgbFromNclx` use their decoded
RGB directly. A valid normalized non-sRGB Skia representation converts only
the selected pixel to reference sRGB. If source profile meaning is known but
unpreserved, Fovium uses the available decoded value with an `Approximate`
sample state and subtle `≈` UI marker. This is truthful source-to-reference
interpretation, not monitor Color Management.

## Perceptual and creative names

Every nontransparent committed reference-sRGB sample is converted through the
accepted project-owned OKLab math to OKLCH, then classified once into semantic
hue, lightness, and chroma identities. The primary short and detailed names are
built from a bounded localized taxonomy. This classification does not consult
the creative anchor catalog, locale, monitor pixels, or image identity.

Thresholds are monotonic and shared for all colors: lightness boundaries are
`0.25`, `0.45`, `0.72`, and `0.88`; chroma boundaries are `0.025`, `0.07`,
`0.14`, and `0.24`. Exact neutrals use a `0.008` chroma ceiling; restrained
warm/cool-gray casts extend to `0.035`, with cool blue-gray extending to
`0.055`. Bounded hue sectors provide the general families, while burgundy,
brown, olive, coral, and pink use only OKLCH hue/lightness/chroma conditions.
There are no per-RGB overrides. These initial thresholds are deterministic
presentation policy backed by representative and boundary tests, not a claim
of a physical color standard.

Fully transparent samples retain the localized `Transparent` semantic and do
not invent OKLCH, hue, lightness, or chroma values. Fovium never reports
Pantone, RAL, NCS, or another proprietary/physical identifier from a decoded
reference-sRGB sample.

The embedded 1,800-name catalog remains the secondary creative name:

The embedded catalog contains exactly 1,800 curated RGB/name anchors derived
deterministically from the MIT-licensed `meodai/color-names` dataset. Runtime
stores precomputed standard OKLab coordinates and performs a deterministic
linear nearest search using squared Euclidean distance. Exact anchors win;
equal-distance results retain stable catalog order. Canonical English names,
stable IDs, RGB anchors, matching, and tie order remain locale-independent.
Optional embedded display catalogs map stable ID to a reviewed localized name;
Russian has complete 1,800-ID coverage. Lookup is indexed once per locale and
falls back to canonical English for any missing entry or unusable catalog.
Current and historical samples resolve both bounded perceptual terms and the
creative display name at the UI boundary. Changing locale therefore requires
neither resampling nor repeating nearest-name matching.

## Selection and history

History is an in-memory FIFO of exactly the latest ten clicks, displayed oldest
to newest. Each click owns a distinct session entry identity even when sampled
RGBA and creative stable ID are equal. A new click appends and selects that
entry. Clicking an older row changes selection only: it does not sample,
rematch, reorder, or append. Click eleven removes click one and appends eleven
at the bottom.

Selection and history survive navigation, Blink, Peek, and hiding/reopening the
panel within the viewer window. Explicit Clear removes both without closing the
panel or changing the photograph; the next click starts a fresh history. They
are never written to settings or disk and reset with a new viewer session.

Catalog provenance and regeneration are recorded in
[`../resources/color-names/README.md`](../resources/color-names/README.md) and
[`THIRD-PARTY.md`](THIRD-PARTY.md). No runtime network, database, service, or
color-library dependency is involved.
