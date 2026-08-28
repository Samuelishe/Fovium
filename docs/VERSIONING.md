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
0.1.0.0008
0.1.0.0009
0.1.0.0010
```

CLR assembly and file-version fields are numeric and do not preserve meaningful leading zeros. Their early-line equivalents are `0.0.0.1` through `0.0.0.13`; the promoted milestone is `0.1.0.0`. The human-facing informational version preserves the four-digit BUILD.

## Component semantics

This policy applies prospectively from the accepted R9-A-F1 checkpoint. Earlier checkpoint numbers remain historical facts and are not reclassified or renumbered.

### MAJOR

MAJOR identifies an owner-controlled mature product generation. `1.0.0.0000` is an explicit product/release decision and is never assigned automatically.

### MINOR

MINOR identifies a major roadmap or release-line transition comprising a substantial new product chapter. It changes only when a roadmap milestone explicitly defines a new product line, for example `0.1.x` → `0.2.0.0000`.

When MINOR increments, PATCH resets to `0` and BUILD resets to `0000`.

### PATCH

PATCH increments for a new standalone user-visible capability, such as Slideshow, a meaningful new image-format capability, or another independently useful viewer feature.

When PATCH increments, BUILD resets to `0000`. For example, a new standalone feature after `0.1.0.0010` begins at `0.1.1.0000`.

### BUILD

BUILD increments for an accepted corrective or polish checkpoint within the current feature line. This includes bug fixes, UX corrections, performance corrections, regression repairs, and refinements of the capability introduced by the current PATCH line.

For example:

```text
0.1.1.0000  standalone feature
0.1.1.0001  corrective checkpoint
0.1.1.0002  polish checkpoint
```

BUILD remains four digits in the human-facing `InformationalVersion`. CLR `AssemblyVersion` and `FileVersion` use the numeric equivalent without meaningful leading zeros.

### No product version change

The product version does not change for work that does not alter accepted product behavior, including:

- architecture probes and experiments;
- test-only corrections;
- documentation-only corrections;
- CI/build-only corrections;
- native prerequisite or supply-chain stages that have not yet entered shipped product behavior.

If infrastructure work materially changes shipped product capability, classify the resulting product behavior instead of the implementation category.

## Deterministic classification

Classify every future stage in this order:

1. New roadmap or release line: increment MINOR, then reset PATCH to `0` and BUILD to `0000`.
2. New standalone user-visible capability: increment PATCH and reset BUILD to `0000`.
3. Fix, polish, or refinement of the current capability: increment BUILD.
4. No accepted product-behavior change: keep the version unchanged.

MAJOR remains an explicit OWNER decision only. Ordinary future stages that fit steps 1–4 do not require a separate version-selection question.

The displayed checkpoint is not a substitute for a Git commit, tag, or branch. Git remains authoritative for source history. Failed attempts, ordinary compiles, and intermediate edits do not consume versions; an accepted checkpoint must never reuse an earlier version. No automatic rollover after BUILD `9999` is defined.

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
| `0.1.0.0008` | R8-B-W1 | Windows ordinary-SDR photograph Monitor Color Management through active display ICC and app-local Little CMS 2.19 |
| `0.1.0.0009` | R9-A | Session-local Photo Presentation View with independent Matte-inclusive fitting and physical edge margin |
| `0.1.0.0010` | R9-A-F1 | Photo Presentation discoverability and Settings-window size persistence/centering UX corrective |
| `0.1.1.0000` | R9-B | Session-local Slideshow with publication-based timing, Stop/Loop, and one bounded prepared next frame |
| `0.1.1.0001` | R9-B-F1 | Slideshow view-mode independence and viewer focus-chrome correction |
| `0.1.1.0002` | R9-A-F2 | Photo Presentation scale made independent of Matte |

The `0.0.0.xxxx` line records the completed foundation and early product-construction checkpoints. The explicit owner-controlled `0.1.0.0000` transition marks the first substantial usable Fovium alpha; it does not claim feature completeness, a stable API, production release status, or `1.0` quality. Current implementation state remains owned by [`PROJECT-STATE.md`](PROJECT-STATE.md).

## Current checkpoint

R9-A-F2 is the locally complete corrective checkpoint at `0.1.1.0002`. It preserves R9-B's PATCH line while incrementing BUILD for the older Photo Presentation layout correction that makes photograph scale independent of Matte.

## Future code and packaging source

The root `Directory.Build.props` is the canonical source for the current components and formatted display identity. It supplies the production `Fovium` assembly with:

```text
InformationalVersion = 0.1.1.0002
AssemblyVersion      = 0.1.1.2
FileVersion          = 0.1.1.2
```

From that source:

- `InformationalVersion` preserves the four-digit BUILD display;
- `AssemblyVersion` and `FileVersion` receive the numeric equivalent;
- Settings → About reads runtime/project metadata instead of hardcoding a string in XAML;
- installer, package, bundle, and distribution metadata derive from the same owner;
- diagnostics report the resolved runtime version rather than duplicating constants.

The small runtime accessor reads assembly metadata and does not duplicate the display version. Future About, diagnostics, installer, and package metadata must continue to derive from this owner.
