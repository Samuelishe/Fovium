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
- photographer-oriented inspection, including future **Peek 100%** and **Blink Compare**;
- a deliberate photographic **Stage** with Black, Neutral, Ambient, and Ambient + Matte modes;
- color-management foundations and broader image-format support over time;
- no database, import workflow, or plugin platform.

See the canonical [UX contract](docs/UX-CONTRACT.md), [rendering contract](docs/RENDERING.md), and [imaging direction](docs/IMAGING-PIPELINE.md) for details.

## Current status

The current project checkpoint is **`0.0.0.0006`**. R3 adds persisted Black, Neutral, Ambient, and Ambient + Matte presentation modes to the runnable JPEG/PNG Core Viewer while retaining the R2 viewing and navigation contracts.

This remains an early vertical slice, not a release claim. Full monitor-aware ICC, broad codecs, broader Settings, metadata, platform associations, and later photographer features are not implemented. The expected next stage is **R4 Peek 100% / Blink Compare**. The canonical handoff is [PROJECT-STATE.md](docs/PROJECT-STATE.md); bounded R0 evidence remains in the [experiment report](docs/experiments/R0-RENDERING-PROBE.md).

English/Russian catalogs and the Dark secondary-UI baseline are implemented; language/theme selection remains future work. See [VERSIONING.md](docs/VERSIONING.md) and the [documentation index](docs/INDEX.md) for the canonical owners.

## Technology direction

- C# and .NET 10;
- Avalonia 12.1.1 as the accepted initial cross-platform UI host;
- an isolated direct-Skia photographic path with SkiaSharp 3.119.4;
- controlled SKCodec probing/decoding as the initial JPEG/PNG foundation;
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
