# Fovium

<!--
Role: Public repository landing page.
Read when: First encountering the repository.
Authoritative for: A concise project introduction and links to canonical documentation.
Not authoritative for: Detailed product contracts, implementation decisions, or live project state.
-->

Fovium is a distraction-free, cross-platform photo viewer focused on rendering quality, seamless navigation, and photographer-first interaction.

## What Fovium is

Fovium is an authored desktop viewer for people who care about how photographs are displayed and inspected. Windows is the primary target, Linux is a full target, and macOS is intended after real runtime validation.

It is not a DAM, catalog, organizer, file manager, RAW editor, or general media suite. The complete product boundary lives in the [project vision](docs/PROJECT-VISION.md).

## Philosophy

> The main UI component of Fovium is the photograph itself.

Zero-UI is intentional: normal viewing removes persistent chrome rather than surrounding the image with controls. Rendering quality and precise interaction take priority over feature count.

## Core ideas

- seamless previous/next navigation through viable images in the current directory;
- cursor-anchored zoom, exact panning, and photographic 100% based on physical display pixels;
- photographer-oriented inspection with whole-viewport **Peek 100%** and non-navigating **Blink Compare**;
- session-local presenter tools with image-bound markup, Ellipse, per-stroke opacity, constrained drawing, true partial erasing, per-image Undo/Redo, contextual controls, accurate drawing cursors, and a configurable cursor highlight;
- a deliberate photographic **Stage** with Black, Neutral, Custom, Ambient, Average, Dominant, or abstract Color Wash backgrounds plus an independent custom/auto-colored Matte and optional Hairline Auto separation;
- a session-local **Photo Presentation View** that independently fits each photograph inside a configurable edge margin while Matte decorates the resolved photo without changing its scale;
- a session-local **Slideshow** on `F5`, with a persisted 1–60 second interval, stop-at-last or natural-order loop behavior, and one bounded prepared next frame;
- on-demand movable Photo Info and decoded-RGB Histogram overlays that follow the actually presented photograph, including Blink;
- an offline click-to-sample photographic Color Picker with reference-sRGB HEX/RGB(A), one local OKLab-nearest human name, and ten-click session history;
- JPEG, PNG, static WebP, bounded static 8-bit TIFF, bounded static 8-bit SDR HEIF/HEIC, and bounded static 8-bit SDR AVIF through one content-detected format-capability foundation;
- no database, import workflow, or plugin platform.

See the canonical [UX contract](docs/UX-CONTRACT.md), [rendering contract](docs/RENDERING.md), and [imaging direction](docs/IMAGING-PIPELINE.md) for details.

## Current status

The current locally complete checkpoint is **`0.1.2.0000`**. R10-A adds one bounded offline reference-sRGB analysis per decoded photograph and reuses it for Average, Dominant, and abstract Color Wash backgrounds, presentation-safe automatic Matte color, and one-physical-pixel Hairline Auto separation. Exact source identity and Black/neutral/no-line fallbacks prevent stale styling. R9-A-F2 keeps Photo Presentation scale independent from Matte, R9-B-F1 keeps Slideshow independent from viewer layout mode, and R8-B-W1 remains enabled-by-default photograph-only Monitor Color Management for ordinary Windows SDR through the active display ICC and app-local Little CMS 2.19; hosted and non-Windows visual status must be evaluated separately.

This is an alpha milestone, not a feature-complete or stable release claim. Gradient/textured Matte or backgrounds, inner shadows, material presets, slideshow transitions/shuffle/music/countdown UI, animated formats, RAW, Advanced Metadata, persistent palettes, full monitor-aware ICC, platform associations, markup export/object editing, and selected-reference A/B comparison are not implemented. The current matrix is [FORMAT-SUPPORT.md](docs/FORMAT-SUPPORT.md), derived styling is specified in [PHOTO-DERIVED-STYLING.md](docs/PHOTO-DERIVED-STYLING.md), Slideshow semantics are in [SLIDESHOW.md](docs/SLIDESHOW.md), and the canonical handoff is [PROJECT-STATE.md](docs/PROJECT-STATE.md).

English/Russian catalogs and the Dark secondary-UI baseline are implemented; language/theme selection remains future work. See [VERSIONING.md](docs/VERSIONING.md) and the [documentation index](docs/INDEX.md) for the canonical owners.

## Technology direction

- C# and .NET 10;
- Avalonia 12.1.1 as the accepted initial cross-platform UI host;
- an isolated direct-Skia photographic path with SkiaSharp 3.119.4;
- one shared bounded decoder dispatcher, with controlled SKCodec for JPEG/PNG/static WebP, focused managed LibTiff.NET for bounded TIFF, and a small direct interop backend over Fovium's app-local libheif runtime for bounded HEIF/AVIF;
- xUnit for repository, experiment, and production logic tests.

Full monitor-aware ICC, broad codec coverage, and huge/tiled image handling remain future work.

## Repository structure

| Path | Purpose |
| --- | --- |
| `docs/` | Canonical product, technical, planning, and repository contracts |
| `eng/` | Small repository verification wrappers |
| `resources/` | Tracked asset root governed by provenance policy |
| `Fovium/` | First production Core Viewer application |
| `Fovium.Tools.ProjectStats/` | BCL-only repository diagnostics CLI |
| `Fovium.Tests/` | Automated tests for repository tooling, retained R0 logic, and production viewer boundaries |
| `experiments/Fovium.RenderProbe/` | Disposable R0 rendering/imaging evidence executable; not the viewer |
| `.github/workflows/ci.yml` | Windows, Linux, and macOS restore/build/test workflow |

## Development / verification

With PowerShell 7 and the .NET 10 SDK:

```powershell
dotnet restore Fovium.sln
dotnet build Fovium.sln -c Release
dotnet test Fovium.sln -c Release --no-build
pwsh eng/repo-baseline.ps1
pwsh eng/project-stats.ps1
```

ProjectStats generates an ignored local `project-stats.md` report. Test details are in [TEST-EXECUTION.md](docs/TEST-EXECUTION.md).

Run the viewer with zero or more paths (zero opens the picker):

```powershell
dotnet run --project Fovium -- "C:\path\to\photo.jpg"
```

## Documentation

Start with the [documentation index](docs/INDEX.md). Repository agents use [AGENTS.md](AGENTS.md) for selective context routing. Durable decisions, plans, risks, and current state deliberately have separate owners.

## Third-party and provenance

Introduced test dependencies, CI actions, evaluated technologies, and future asset provenance are tracked in [THIRD-PARTY.md](docs/THIRD-PARTY.md). No external image, icon, font, logo, or test-image asset is currently shipped.

## License

The project license has not been selected yet. Third-party components retain their own licenses and terms.
