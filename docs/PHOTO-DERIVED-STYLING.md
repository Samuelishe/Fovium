# Photo-derived styling

Role: Contract for R10-A offline photograph analysis and the visual styles derived from it.
Read when: Changing Average, Dominant, Color Wash, automatic Matte color, Hairline Auto, or the shared analysis artifact.
Authoritative for: Analysis domain and bounds, cache/identity behavior, fallback publication, tone normalization, Blink/Peek policy, and separation behavior.
Not authoritative for: Decode-format support, viewport geometry, Color Management, general Stage composition, or settings storage mechanics.

## Analysis foundation

Every successful canonical decode produces one deterministic analysis from the oriented reference-sRGB photograph inside the existing off-UI decode work. The analyzer resamples once to at most `96 px` on the long edge and reads no more than `9,216` visible samples. It records a linear-light alpha-weighted average, a deterministic 4-bit/channel dominant cluster, up to five weighted palette entries, a `4×4` spatial color field, and an outer-boundary tone. Fully transparent samples do not contribute.

The immutable managed result is attached to its exact `DecodedImage` and charged to the same session-local byte-bounded decoded cache entry. There is no second file decode, independent styling cache, full-resolution analysis loop, or viewport-sized derived surface. Adjacent decoded preload naturally includes the same small analysis. Cancellation during analysis disposes the unpublished decoded candidate; normal sequence generation/latest-wins rules reject late candidates.

Zoom, pan, Fit, physical 100%, resize, fullscreen, Photo Presentation layout, Matte geometry, and Peek reuse the attached result and never schedule analysis. Color Picker continues to sample canonical source pixels into reference sRGB, while Histogram continues to read its source-domain decoded pixels; neither consumes presentation styling or monitor-managed output.

## Backgrounds and publication

Average and Dominant are opaque solid Stage fills using the exact corresponding analyzed reference-sRGB colors. Color Wash expands the analysis's `4×4` spatial field with deterministic smoothstep interpolation in OKLab into a `64×64` soft abstract raster; its cells are constrained in OKLCH to lightness `0.18–0.72` and chroma at most `0.12`, so recognizable photographic detail is absent. The 16,384-byte native wash image is prepared once with the analysis, byte-accounted under the same `DecodedImage`, and shared with draw operations through retained leases. Geometry only stretches that artifact and never rebuilds it.

Derived styling is accepted only when its source identity equals the actually rendered photograph identity. If analysis is unavailable or mismatched, derived backgrounds render Black, automatic Matte renders the fixed neutral fallback, and Hairline Auto is omitted. A previous photograph's style is never displayed as the new photograph's style.

Blink follows the photograph actually being shown: a decoded comparison uses its own attached analysis, otherwise the same truthful fallback applies. Blink does not borrow the canonical photograph's style or schedule work. Peek keeps the canonical photograph and therefore reuses its analysis without recomputation.

## Matte and separation

Matte color source is persisted independently from Matte enabled/style/width:

- Custom preserves the existing exact user color;
- Average uses the analyzed average;
- Dominant uses the analyzed dominant cluster.

Automatic Matte tones are deterministically normalized in OKLCH to lightness `0.30–0.88` and chroma at most `0.10`. This presentation-safe mapping limits extreme darkness, brightness, and saturation without machine learning or network access. It changes only Matte presentation color and never photograph pixels, destination, scale, or source mapping.

Photo separation is either None or Hairline Auto. Hairline Auto is present only with enabled Matte and exact matching analysis. It is one physical pixel wide with alpha `176`; Black, mid-gray, and White candidates are scored by the minimum WCAG contrast against both the resolved Matte and analyzed photograph-boundary tone, with deterministic tie order. The line is drawn immediately outside the rectangular photograph boundary before the photograph, so it remains restrained and does not change geometry.

All settings apply in Normal Viewer, Photo Presentation, and Slideshow through the same Stage renderer. They introduce no command, shortcut, Color Management operation, alternate Fit/zoom path, source edit, or network dependency.
