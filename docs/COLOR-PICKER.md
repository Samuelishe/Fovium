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
accepted project-owned OKLab math to OKLCH, then classified once into a semantic
role, optional undertone, hue family, lightness, and chroma identity. Roles
separate chromatic, neutral, near-neutral, tinted-neutral, near-black, and
near-white presentation. The primary short and detailed names are built from a
bounded localized taxonomy. This classification does not consult the creative
anchor catalog, locale, monitor pixels, or image identity.

Thresholds are centralized and shared for all colors: lightness boundaries are
`0.25`, `0.45`, `0.72`, and `0.88`; chroma boundaries are `0.025`, `0.07`,
`0.14`, and `0.24`. Exact neutrals use a `0.008` chroma ceiling. Subtle and
visible-cast neutral limits follow bounded triangular lightness curves, with a
small blue/violet allowance, so the same absolute chroma is not treated as
equally neutral at mid-gray and near the lightness extremes. Near-black and
near-white roles have their own bounded lightness/chroma gates; an undertone is
reported only above a small visibility floor so almost-zero chroma does not
acquire false hue precision.

Bounded hue sectors include short transition families such as red-orange,
olive-green, turquoise-cyan, cyan-blue, blue-violet, pink-lilac, and
red-magenta. Burgundy, brown, olive, coral, rose, crimson, pink, and the pastel
pink-lilac role use only OKLCH hue/lightness/chroma conditions. A bounded warm
earth layer distinguishes greige/beige/sand, cream, peach/apricot,
ochre/mustard, taupe, and terracotta from neighboring olive, orange, brown, and
near-white regions. Low-chroma violet and lilac casts likewise use dedicated
gray families instead of falling through to blue-gray. These regions combine
hue, lightness, chroma, and perceptual role through centralized thresholds;
there are no per-RGB overrides.

The yellow-brown/olive boundary uses one bounded lightness-aware ochre hue floor:
dark colors require a more yellow hue before leaving Brown, while the accepted
ochre sector widens smoothly through mid/light values and ends before the greener
Olive controls. This replaces the former ordering gap that classified moderate
yellow-browns around 85–91° as Olive. It remains a region rule over OKLCH rather
than a table of owner RGB samples.

History names use at most one useful lightness or chroma modifier, while
detailed names may use both. The detail row says `Color tone` for chromatic
samples and `Undertone` for neutral, tinted, near-black, and near-white roles,
so a cream-white sample is not presented as though olive were its primary
color. These thresholds are deterministic presentation policy backed by
boundary tests and a bounded real-photograph corpus, not a claim of an
objective or physical color standard.

Very light, visibly green-to-turquoise chromatic samples may use the bounded
`Mint` family. It is a professional descriptive region, not another creative
anchor: its shared lightness, chroma, and hue bounds deliberately preserve pale
green, ordinary turquoise/aquamarine, and near-white cyan. Coral/red-orange,
terracotta/brown, dark burgundy/red-magenta, warm rose/red, and
violet/magenta transitions likewise use shared OKLCH regions rather than RGB
exceptions.

R8-A-F7 keeps those broad families as the stable geometry and adds a separate declarative professional-shade layer.
Each definition has a stable identity, parent family, eligible role, bounded OKLCH region, explicit priority, and one
EN/RU localization key. The first evidence-backed set is Lavender, Periwinkle, Navy, Azure, Sky Blue, Sage, Emerald,
Forest Green, Aquamarine, Teal, Salmon, Wine, Rust, Scarlet, Tangerine, Ivory, Charcoal, and Slate. When a definition
matches, its concise human term becomes the primary inspector/history name; the broad family remains the detail tone
and the fallback outside well-supported regions. Definitions are pre-indexed by parent family, so selecting history or
rendering the UI does not scan external data, resample the image, or rerun creative matching.

Developer changes to this taxonomy use the reproducible audit route documented in
[`../eng/color-taxonomy-audit/README.md`](../eng/color-taxonomy-audit/README.md).
Its structured in-gamut grid, fixed-seed RGB sampling, boundary refinement,
topology checks, coordinate-driven balanced hue/lightness/chroma cohort,
family profiles, specificity/vocabulary-gap reporting, accepted-term coverage, and optional named-color neighbors
exercise the production classifier. Independent holdout seeds validate corrections after tuning.
External names rank suspicious regions only; they do not override
the project-owned taxonomy or become runtime data.

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
to newest in a desktop-oriented column sized for the informative short names;
ellipsis remains bounded and a row tooltip exposes the complete name. Each
click owns a distinct session entry identity even when sampled
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
