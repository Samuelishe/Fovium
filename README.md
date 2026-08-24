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
- a deliberate presentation **Stage**, from a black baseline to future ambient modes;
- color-management foundations and broader image-format support over time;
- no database, import workflow, or plugin platform.

See the canonical [UX contract](docs/UX-CONTRACT.md), [rendering contract](docs/RENDERING.md), and [imaging direction](docs/IMAGING-PIPELINE.md) for details.

## Current status

Fovium is currently in the repository and technical-foundation stage. No runnable photo viewer exists yet. Repository diagnostics, automated tests, and cross-platform CI configuration are present; hosted CI has not yet been proven by a GitHub run.

The next technical stage is the bounded **R0 rendering/DPI/decoder/color-foundation probe**. The canonical current handoff is [PROJECT-STATE.md](docs/PROJECT-STATE.md).

## Technology direction

- C# and .NET 10;
- Avalonia as the planned cross-platform UI direction;
- a Skia-based rendering direction subject to R0 validation;
- xUnit for current repository-tool tests.

Avalonia, Skia, and imaging/color packages have not been introduced yet.

## Repository structure

| Path | Purpose |
| --- | --- |
| `docs/` | Canonical product, technical, planning, and repository contracts |
| `eng/` | Small repository verification wrappers |
| `resources/` | Tracked asset root governed by provenance policy |
| `Fovium.Tools.ProjectStats/` | BCL-only repository diagnostics CLI |
| `Fovium.Tests/` | Automated tests; currently focused on ProjectStats |
| `.github/workflows/ci.yml` | Windows, Linux, and macOS restore/build/test workflow |

There is intentionally no Fovium application project yet.

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

## Documentation

Start with the [documentation index](docs/INDEX.md). Repository agents use [AGENTS.md](AGENTS.md) for selective context routing. Durable decisions, plans, risks, and current state deliberately have separate owners.

## Third-party and provenance

Introduced test dependencies, CI actions, evaluated technologies, and future asset provenance are tracked in [THIRD-PARTY.md](docs/THIRD-PARTY.md). No external image, icon, font, logo, or test-image asset is currently shipped.

## License

The project license has not been selected yet. Third-party components retain their own licenses and terms.
