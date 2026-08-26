# Versioning

Role: Contract for Fovium version identity and checkpoint numbering.
Read when: Assigning a project version, exposing version information, or configuring future assembly, package, installer, or About metadata.
Authoritative for: Display format, component semantics, accepted checkpoint history, and the future canonical version source.
Not authoritative for: Current implementation status, roadmap ordering, Git history, release packaging details, or dependency versions.

## Canonical format

Fovium displays its version as:

`MAJOR.MINOR.PATCH.BUILD`

`BUILD` uses four digits as a Fovium display and project convention:

```text
0.0.0.0001
0.0.0.0002
0.0.0.0003
0.0.0.0004
0.0.0.0005
0.0.0.0006
0.0.0.0007
0.0.0.0008
0.0.0.0009
0.0.0.0010
0.0.0.0011
0.0.0.0012
0.0.0.0013
0.1.0.0000
0.1.0.0001
0.1.0.0002
0.1.0.0003
0.1.0.0004
0.1.0.0005
0.1.0.0006
0.1.0.0007
```

CLR assembly and file-version fields are numeric and do not preserve meaningful leading zeros. Their early-line equivalents are `0.0.0.1` through `0.0.0.13`; the promoted milestone is `0.1.0.0`. The human-facing informational version preserves the four-digit BUILD.

## Component semantics

### MAJOR

An owner-controlled major product milestone. A future `1.0.0.0000` transition is explicit, never automatic.

### MINOR

An owner-controlled major user-visible product milestone. Creating the first executable does not automatically make the project `0.1.0.0000`; that transition requires an explicit owner decision.

### PATCH

An owner-controlled meaningful functional milestone within the current MINOR. Small fixes and polish do not automatically consume PATCH values.

### BUILD

A sequential accepted repository or product checkpoint. Documentation and foundation stages may consume BUILD values because the version also identifies project state.

Rules:

- increment once for an accepted coherent checkpoint;
- do not increment for failed attempts, ordinary compiles, or intermediate edits;
- never reuse an earlier BUILD value;
- reset BUILD to `0000` when MAJOR, MINOR, or PATCH changes;
- do not define automatic rollover after `9999`; an explicit owner decision must select the next version line.

The displayed checkpoint is not a substitute for a Git commit, tag, or branch. Git remains authoritative for source history.

## Accepted checkpoints

| Version | Stage | Meaning |
| --- | --- | --- |
| `0.0.0.0001` | DOCS-R1 + REPO-R1 | Initial repository, documentation, tooling, test, and CI foundation |
| `0.0.0.0002` | R0 | Rendering, DPI, decoder, and color-foundation investigation accepted |
| `0.0.0.0003` | CONTRACTS-R1 | Versioning, Settings, localization, themes, and platform-integration contracts established |
| `0.0.0.0004` | R1 | First runnable production Core Viewer vertical slice |
| `0.0.0.0005` | R2 | Persistent view policy, minimal Settings foundation, and navigation/lifetime hardening |
| `0.0.0.0006` | R3 | Persisted Black, Neutral, Ambient, and Ambient + Matte photographic Stage modes |
| `0.0.0.0007` | R3-F1 | Stage customization, independent Matte, settings-schema migration, and configurable controls |
| `0.0.0.0008` | R3-F2 | Configurable physical Matte width and Solid, Rounded, Soft, and Angular outer styles |
| `0.0.0.0009` | R4 | Peek 100% and non-navigating Blink Compare temporary inspection interactions |
| `0.0.0.0010` | R5 | Session-local presenter markup overlay and configurable cursor highlight |
| `0.0.0.0011` | R5-F1 | True partial Eraser, bounded per-image markup history, Undo/Redo, and undoable Clear |
| `0.0.0.0012` | R5-F2 | Ellipse/Circle, immutable per-draw opacity, Shift constraints, and contextual markup style shortcuts |
| `0.0.0.0013` | R5-P1 / R5-P2 / R5-P3 corrective line | Owner-accepted current-first scheduling, atomic Ambient presentation, and sustained preload across cache saturation |
| `0.1.0.0000` | R5-F3 | First substantial usable Fovium alpha: contextual controls, drawing cursors, Hand, movable icon dock, and polished secondary UI |
| `0.1.0.0001` | R5-F3-P1 | Interaction render-path isolation for photo, markup, pointer feedback, and floating UI |
| `0.1.0.0002` | R6-A | Read-only metadata foundation and movable Photo Info floating overlay |
| `0.1.0.0003` | R6-B | Lazy decoded-RGB Histogram and movable floating overlay |
| `0.1.0.0004` | R7-A | Project-owned format capability foundation and static WebP support |
| `0.1.0.0005` | R7-B | Shared decoder-backend boundary and bounded static 8-bit TIFF support |
| `0.1.0.0006` | R7-C | Bounded static 8-bit SDR HEIF/HEIC and AVIF support through the app-owned decode-only runtime |
| `0.1.0.0007` | R8-A | Offline click-to-sample photographic Color Picker with reference-sRGB values, local OKLab names, and ten-click session history |

The `0.0.0.xxxx` line records the completed foundation and early product-construction checkpoints. The explicit owner-controlled `0.1.0.0000` transition marks the first substantial usable Fovium alpha; it does not claim feature completeness, a stable API, production release status, or `1.0` quality. Current implementation state remains owned by [`PROJECT-STATE.md`](PROJECT-STATE.md).

## Future code and packaging source

The root `Directory.Build.props` is the canonical source for the current components and formatted display identity. It supplies the production `Fovium` assembly with:

```text
InformationalVersion = 0.1.0.0007
AssemblyVersion      = 0.1.0.7
FileVersion          = 0.1.0.7
```

From that source:

- `InformationalVersion` preserves the four-digit BUILD display;
- `AssemblyVersion` and `FileVersion` receive the numeric equivalent;
- Settings → About reads runtime/project metadata instead of hardcoding a string in XAML;
- installer, package, bundle, and distribution metadata derive from the same owner;
- diagnostics report the resolved runtime version rather than duplicating constants.

The small runtime accessor reads assembly metadata and does not duplicate the display version. Future About, diagnostics, installer, and package metadata must continue to derive from this owner.
