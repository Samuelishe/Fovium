# Fovium color-name catalog

Role: Embedded, offline color-name anchors used by the photographic Color Picker.

The committed `fovium-color-names.json` is a deterministic 1,800-entry curated
derivative of David Aerne's `meodai/color-names` dataset. Fovium ships only the
stable RGB ID, RGB/HEX anchor, and canonical English name required at runtime.
The catalog is loaded from the application assembly; normal build and runtime
do not download or regenerate it.

## Pinned source

- Project: `meodai/color-names`
- Upstream commit: `cc5fc08de437ea2522d32f751cecb4aa1e96f8e3`
- Source file: `src/colornames.csv`
- Source URL: <https://raw.githubusercontent.com/meodai/color-names/cc5fc08de437ea2522d32f751cecb4aa1e96f8e3/src/colornames.csv>
- Source SHA-256: `5dd5a199d58beb3b2121b79a02236ecf88397c3f6fbaff73eb5bc776d435bc60`
- Source rows: 31,915
- Upstream `good name` rows: 4,959
- License: MIT; see `LICENSE.meodai-color-names.txt`

The other required research candidate, `tajmone/name-that-color`, was not used.
Its 1,566-name list has a more complicated inherited data-provenance chain,
including the older Name That Color compilation and named external lists.

## Deterministic curation

`eng/color-catalog/generate.ps1` verifies the pinned source hash and row count,
then:

1. keeps upstream `good name` entries with names of at most 12 characters;
2. rejects numbered/generated-looking names and names outside a bounded
   letters-and-simple-punctuation character policy;
3. removes identity, nationality, protected-brand, and overt novelty terms
   listed by the generator;
4. preserves 17 exact basic anchors (`Black`, `White`, `Red`, and peers);
5. selects 1,800 anchors with deterministic farthest-point coverage in OKLab;
6. sorts output by RGB/HEX and derives stable IDs as `rgb-rrggbb`.

Current generated result:

- eligible rows before coverage selection: 3,031
- final rows: 1,800
- serialized size: 151,533 bytes
- catalog SHA-256: `1a2b8860eef101afdb8ace7a13507b29e163a7a01c63058984a5c1f1ea249aa0`

## Regeneration

Download the exact pinned CSV into the ignored imaging sandbox, then run:

```powershell
pwsh ./eng/color-catalog/generate.ps1 `
  -SourcePath ./resources/test-images/color-catalog/meodai-colornames-cc5fc08.csv
```

The source download remains ignored and must not be staged. Regeneration is a
developer action; it is not part of `dotnet build`, application startup, or
Color Picker activation. Dataset updates happen only through reviewed Fovium
releases.
