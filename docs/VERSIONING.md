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
```

CLR assembly and file-version fields are numeric and do not preserve meaningful leading zeros. Their equivalent values may therefore be `0.0.0.1` through `0.0.0.7`. The human-facing informational version preserves the four-digit BUILD.

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

The current accepted project checkpoint is `0.0.0.0008`. Current implementation state remains owned by [`PROJECT-STATE.md`](PROJECT-STATE.md).

## Future code and packaging source

The root `Directory.Build.props` is the canonical source for the current components and formatted display identity. It supplies the production `Fovium` assembly with:

```text
InformationalVersion = 0.0.0.0008
AssemblyVersion      = 0.0.0.8
FileVersion          = 0.0.0.8
```

From that source:

- `InformationalVersion` preserves the four-digit BUILD display;
- `AssemblyVersion` and `FileVersion` receive the numeric equivalent;
- Settings → About reads runtime/project metadata instead of hardcoding a string in XAML;
- installer, package, bundle, and distribution metadata derive from the same owner;
- diagnostics report the resolved runtime version rather than duplicating constants.

The small runtime accessor reads assembly metadata and does not duplicate the display version. Future About, diagnostics, installer, and package metadata must continue to derive from this owner.
