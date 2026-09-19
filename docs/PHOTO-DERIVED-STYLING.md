# Photo-derived styling

Role: Contract for R10 offline photograph analysis and the visual styles derived from it.
Read when: Changing Average, Dominant, Color Wash, Color Gradient, Soft Glow, automatic Matte color, Hairline Auto,
Photo Color Profile, or the shared analysis artifact.
Authoritative for: Analysis domain and bounds, cache/identity behavior, fallback publication, tone normalization,
Blink/Peek policy, and separation behavior.
Not authoritative for: Decode-format support, viewport geometry, Color Management, general Stage composition, or
settings storage mechanics.

## Analysis foundation

Every successful canonical decode produces one deterministic analysis from the oriented reference-sRGB photograph inside
the existing off-UI decode work. The analyzer resamples once to at most `96 px` on the long edge and reads no more than
`9,216` visible samples. It records a linear-light alpha-weighted average, raw deterministic 4-bit/channel population
clusters, up to five unchanged population-ranked palette entries, a representative Dominant derived from those raw
clusters, zero to ten separately selected Notable colors, a `6×6` spatial color field, and an outer-boundary tone. Fully
transparent samples do not contribute.

Representative Dominant aggregates the raw clusters into a fixed bounded set of 12 OKLab hue families and eight
neutral-lightness families. Smooth membership begins above chroma `0.015` and reaches full chromatic membership at
`0.065`; hue affinity uses the fourth power of positive cosine, while neutral families use a smooth `0.25` lightness
radius. A candidate must have at least `8%` support and at least `25%` of the largest family support. Admitted
candidates are ranked by `support × chromaWeight × lightnessWeight`, where chroma weight is
`0.65 + 2.65 × smoothstep(clamp((C − 0.010) / 0.100))` and lightness weight is `0.55 + 0.45 × sin(πL)`. Rounded score,
support, then fixed family index provide stable tie order. Thus population remains primary, substantial color may beat
gray/black/white softly, tiny saturated accents cannot win by saturation alone, and genuinely neutral, dark, or high-key
photographs may remain neutral, dark, or light. The raw palette is never reordered or discarded by this presentation
selection.

Notable selection does not replace or reorder that raw palette. During the same bounded scan the analyzer records one
quantized-bin identity and alpha byte per sample. Occupied bins are consolidated deterministically in OKLab: close
colors merge directly, while chromatic shades may also merge across bounded lightness only when hue and chroma remain
close. Each resulting family is measured for aggregate support, largest and top-three 8-neighbor components, component
count, `6×6` cell occupancy, and OKLab contrast at immediate group boundaries on the same sample grid.

Admission is separate from ranking. A candidate may qualify through substantial coherent mass, a compact chromatic
accent, a strong lightness-contrast neutral, distributed repeated structure, a muted but distinct secondary mass, or a
coherent low-chroma structure. The last route requires bounded support, component coherence, global novelty, and both
global and immediate-boundary lightness contrast; it does not admit a neutral merely because it is frequent.
Every route combines a hard support/coherence guard with the perceptual/spatial evidence relevant to that shape; no
single scalar is simultaneously the noise gate and rank. After admission, ranking treats support as a soft factor and
combines route strength, perceptual novelty, local contrast, spatial evidence, and chroma/lightness strength. Final
selection incrementally discounts candidates already explained by Characteristic, weighted Frequent shades, or an
already selected perceptual near-duplicate. A coherent structural mass may retain a bounded information floor even when
a narrow related Frequent bin exists. Selection stops below an information threshold and may retain a second
achromatic candidate only when its OKLab lightness differs by at least `0.24`; it still caps the qualified
shortlist/result at 16/10 rather than filling slots. Isolated pixels, uniform inputs, and fully transparent inputs
therefore produce no invented Notable color. This is bounded classical color/spatial salience, not object recognition,
subject importance, semantic segmentation, or a material-color correction.

R11-C compared the unchanged production scan against otherwise identical `128`, `160`, and `192 px` diagnostic runs on
25 deterministically selected owner photographs. Higher resolution did not repeatedly recover the visually missing
clothing, flower, sign, or neutral classes; results sometimes changed non-monotonically and the median analysis cost
rose from `1.60 ms` at `96 px` to `4.42 ms` at `192 px`. Production therefore remains one `96 px` analysis shared with
R10. The diagnostic overload is developer-only and cannot change normal Stage styling semantics.

The immutable managed result is attached to its exact `DecodedImage` and charged to the same session-local byte-bounded
decoded cache entry. There is no second file decode, independent styling cache, full-resolution analysis loop, or
viewport-sized derived surface. Adjacent decoded preload naturally includes the same small analysis. Cancellation during
analysis disposes the unpublished decoded candidate; normal sequence generation/latest-wins rules reject late
candidates.

Zoom, pan, Fit, physical 100%, resize, fullscreen, Photo Presentation layout, Matte geometry, and Peek reuse the
attached result and never schedule analysis. Color Picker continues to sample canonical source pixels into reference
sRGB, while Histogram continues to read its source-domain decoded pixels; neither consumes presentation styling or
monitor-managed output.

## Backgrounds and publication

Average is an opaque Stage fill using the exact mathematical analyzed reference-sRGB average. Dominant uses the exact
representative color selected above. Color Wash expands the analysis's `6×6` spatial field with deterministic smoothstep
interpolation in OKLab into a `64×64` soft abstract raster. Each cell receives a modest `1.18×` chroma gain capped at
`0.16`, with lightness constrained to `0.20–0.76`; this retains more source color character without neon saturation or
monitor-dependent sampling. Visual comparison found `4×4` materially more muted and `8×8` more likely to reveal broad
source shapes, so `6×6` is the lowest selected complexity. The native wash remains 16,384 bytes and contains no
photographic-resolution detail.

R10-B adds two calmer alternatives. Color Gradient averages the outer two bands of the `6×6` field in OKLab, compares
horizontal and vertical endpoint distance, and deterministically selects the stronger axis (horizontal on an exact tie).
Its boundary-blended endpoints and average midpoint are constrained to lightness `0.18–0.78` and chroma `≤0.14` with a
small `1.06×` gain. Soft Glow does not infer a subject position: it uses a fixed centered radial field whose center
mixes
average and representative Dominant, whose middle remains average-led, and whose edge is boundary-led with restrained
lightness separation. Both descriptions become opaque `32×32` OKLab-interpolated rasters, 4,096 bytes each.

All three rasters are prepared once with the analysis, byte-accounted under the same `DecodedImage`, and shared with
draw operations through retained leases. The maximum combined production styling state is 25,460 bytes: 884 managed
analysis with ten Notable values,
16,384 Color Wash, and two 4,096-byte gradient rasters. Geometry only stretches the selected artifact and never rebuilds
it. A missing raster never triggers UI-thread gradient synthesis; it uses the same truthful Black fallback.

Persisted per-mode Brightness/Saturation is a later Stage-presentation transform over the selected exact solid or tiny
raster. Identity values preserve the original output. The transform does not feed back into this analysis, its Average
or Dominant, palette/spatial field, Notable colors, Color Profile, automatic Matte policy, Picker, or Histogram, and
changing it never regenerates the retained analysis/rasters.

Derived styling is accepted only when its source identity equals the actually rendered photograph identity. If analysis
is unavailable or mismatched, derived backgrounds render Black, automatic Matte renders the fixed neutral fallback, and
Hairline Auto is omitted. A previous photograph's style is never displayed as the new photograph's style.

Blink follows the photograph actually being shown: a decoded comparison uses its own attached analysis, otherwise the
same truthful fallback applies. Blink does not borrow the canonical photograph's style or schedule work. Peek keeps the
canonical photograph and therefore reuses its analysis without recomputation.

## Semantic Color Profile

R11-A derives one immutable `PhotoColorProfile` from this already-computed analysis immediately after successful
attachment. R11-C retains R11-B-F1's zero to ten adaptive Notable colors while refining admission and presentation as
described above. It classifies
representative, Average,
raw palette, and Notable values through shared `Fovium.ColorSemantics`; it does not read pixels, decode again, invoke
CMM, or create a raster. A five-entry profile with ten Notable values retains an estimated 2,904 bytes and is charged
to the same exact `DecodedImage`. R11-C visually inspected a deterministic 60-photo, nine-root corpus split before
output inspection into 25 tuning, 15 validation, and 20 final holdout photographs. Labels are engineering visual review,
not objective color ground truth. Remaining misses include a coherent blue clothing region suppressed as redundant and
small or fragmented semantic subjects; neither justifies an object-recognition claim or a higher-resolution pass.
Across the final 60 profiles, selected counts ranged from zero through seven (mean `3.2`): four returned zero, and no
photograph needed more than seven. The adaptive `0..10` capacity therefore remains sufficient without force-filling or
adding another UI row.

Photo Info labels representative Dominant as Characteristic, labels the unchanged population palette Frequent shades,
and shows Notable colors as a separate optional grid of at most two five-swatch rows. Average remains in the reusable
data model but is omitted
from the compact UI. Raw entries are not merged merely because structural names repeat: they may encode visibly
distinct lightness/chroma masses within one human category. Notable swatches omit percentages because family support is
an admission/ranking measure, not an object-area claim. Professional terms win over broad fallback, while creative
names, HEX, and OKLCH are secondary tooltip detail. A fully transparent analysis publishes no profile rather than
naming an internal styling fallback.

The presented-image lease controls publication exactly as for other Photo Info facts. Blink uses the comparison image's
attached profile; release restores canonical data; Peek emits no identity change. Hide/show, drag, zoom, pan, resize,
fullscreen, Photo Presentation, and Slideshow only reuse the retained value. No profile value is an input to Stage,
Matte, Color Management, Picker, Histogram, or the photo analysis itself.

## Matte and separation

Matte color source is persisted independently from Matte enabled/style/width:

- Custom preserves the existing exact user color;
- Average uses the analyzed average;
- Dominant uses the same analyzed representative Dominant as the Stage background.

Automatic Matte tones are deterministically normalized in OKLCH to lightness `0.30–0.88` and chroma at most `0.10`. This
presentation-safe mapping limits extreme darkness, brightness, and saturation without machine learning or network
access. It changes only Matte presentation color and never photograph pixels, destination, scale, or source mapping.

Photo separation is either None or Hairline Auto. Hairline Auto is present only with enabled Matte and exact matching
analysis. It is one physical pixel wide with alpha `176`; Black, mid-gray, and White candidates are scored by the
minimum WCAG contrast against both the resolved Matte and analyzed photograph-boundary tone, with deterministic tie
order. The line is drawn immediately outside the rectangular photograph boundary before the photograph, so it remains
restrained and does not change geometry.

All settings apply in Normal Viewer, Photo Presentation, and Slideshow through the same Stage renderer. They introduce
no command, shortcut, Color Management operation, alternate Fit/zoom path, source edit, or network dependency.

## R10-B candidate review and evidence

The selected modes were compared against Color Wash on eleven ignored local photographs covering high-key, low-key,
near-black and ordinary neutrals, strong chroma, warm earth, cool blue/cyan, green, portrait-like skin, a small bright
accent, and portrait/landscape/square geometry. Generated PNGs and their HTML contact sheet remain ignored and were
actually inspected through an isolated headless-Chrome fallback because the ACP session exposed no true browser backend.

- Color Gradient was accepted as a quiet directional extension of low-frequency source structure that remains visibly
  distinct from the spatial Color Wash.
- Soft Glow was accepted after increasing its bounded lightness separation; it supplies gentle center/edge depth without
  estimating or reconstructing the photographic subject.
- mesh and multi-radial fields were rejected because they approached recognizable reconstruction or random wallpaper;
- procedural grain/material texture was rejected because device-scale noise competes with photographs and complicates
  cross-platform rendering;
- extra inner shadow/depth decoration was rejected as redundant with Matte and Hairline and too close to faux UI chrome.

Automated raster comparison covers deterministic output, exact identity, missing/stale fallback, transparency,
high/low/neutral tone bounds, 1.00/1.25/1.50/2.00 scaling, cache accounting, navigation races, Blink/Peek ownership,
source-domain Picker/Histogram independence, and one CMM transform across repeated style/scaling draws. The focused
photo-policy/cache/renderer/invariance layer also passes 236 tests under Ubuntu 24.04/.NET 10 in WSL2, and the WSLg
viewer
entered its event loop with an isolated Color Gradient setting. WSLg's RDP/GPU surface could not be captured for
truthful
inspection from the current host, so human evidence remains Windows reference-sRGB output at `RenderScaling = 1.00`; it
is not Linux/macOS or real fractional-DPI visual acceptance.
