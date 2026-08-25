# Test execution

Role: Minimal command guide and verification contract for Fovium tests.
Read when: Running, filtering, adding, or interpreting automated tests.
Authoritative for: Normal local test commands, focused versus full-suite execution, Release verification, and CI test expectations.
Not authoritative for: Test design details, coverage policy, UI/rendering acceptance, or current CI status.

## Commands

Run the full suite in the default configuration:

```powershell
dotnet test Fovium.sln
```

Run the same verification used by CI after a Release build:

```powershell
dotnet restore Fovium.sln
dotnet build Fovium.sln -c Release --no-restore
dotnet test Fovium.sln -c Release --no-build
```

Run only the current ProjectStats tests when iterating on repository tooling:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.ProjectStats"
```

Run only render-independent R0 logic tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.RenderProbe"
```

Run Stage settings, geometry, preparation, cache/lifetime, and offscreen composition tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Stage"
```

The Stage suite includes R5-P1/R5-P2 synchronization evidence that current Ambient preparation requires no adjacent-progress signal, cached photo+Ambient installs as one viewport state, each ready neighbor can trigger progressive preparation, mismatched Ambient rasterizes as Black rather than the wrong Stage, and obsolete current work cannot publish over the latest selection. R5-P3 Loading tests model 30 distinct transitions through a five-resource cache, prove speculative preload continues through repeated saturation, verify admission includes reclaimable LRU capacity but excludes protected current, retain lease/disposal safety, and reject resources larger than the whole cache. Render diagnostics separately distinguish viewport/custom-draw/Skia-lease boundaries. Owner visual acceptance establishes the normal 3–4 photo/second envelope; deliberately faster 5–6+ photo/second behavior remains a documented stress limitation rather than a unit-test timing threshold.

Run configurable-command, gesture-normalization, and command-execution tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Input"
```

Run R4 hold lifecycle, inspection acquisition, viewport transfer/restoration, and temporary Stage tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Input|FullyQualifiedName~Fovium.Tests.Loading.ViewerSessionTests|FullyQualifiedName~Fovium.Tests.Rendering.ViewportModelTests|FullyQualifiedName~Fovium.Tests.Viewer"
```

Run R5 through R5-F2 presenter history, oriented transform, partial-eraser/opacity raster, constraint geometry, settings, input, and inspection tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Presentation|FullyQualifiedName~Fovium.Tests.Settings|FullyQualifiedName~Fovium.Tests.Input|FullyQualifiedName~Fovium.Tests.Viewer"
```

Run the production logic and boundary tests while iterating:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Application|FullyQualifiedName~Fovium.Tests.Navigation|FullyQualifiedName~Fovium.Tests.Loading|FullyQualifiedName~Fovium.Tests.Imaging|FullyQualifiedName~Fovium.Tests.Input|FullyQualifiedName~Fovium.Tests.Rendering|FullyQualifiedName~Fovium.Tests.Viewer|FullyQualifiedName~Fovium.Tests.Settings|FullyQualifiedName~Fovium.Tests.Localization|FullyQualifiedName~Fovium.Tests.Stage|FullyQualifiedName~Fovium.Tests.Versioning"
```

## Scope

Focused tests are preferred during implementation; run the full solution before handoff when shared tooling or project configuration changes. The xUnit suite covers repository tooling and retained R0 logic plus production activation, navigation, decoding, viewport/view-policy transfer, settings persistence, loading ownership, cache, memory policy, localization, Stage geometry/preparation/publication/lifetime, DPI-aware Matte geometry and offscreen alpha composition, version metadata, and native render-lease lifetime.

UI interaction, shortcut capture/conflict dialogs, rendering quality, runtime DPI, pixel alignment, color, native lifetime, and platform behavior require bounded integration, visual, and manual smoke evidence; passing pure tests cannot prove those properties. R4 adds deterministic hold/inspection coverage. R5 adds deterministic coverage for highlight/settings toggles, all four initial markup tools, source-space transforms, image/session identity, no-file-write semantics, panel lifecycle, Skia clipping, H/P migration, and Peek/Blink overlay selection. R5-F1 adds history-cursor and per-image isolation tests; real raster assertions for partial Line/Brush/Rectangle/Arrow erasure, chronology, draft cancellation, and photograph protection; explicit history/session limit tests; Arrow thick-stroke regression coverage; and history-command migration/reset/execution coverage. R5-F2 adds pure multi-quadrant 45-degree/square/circle constraint tests, live constrained/freehand Brush draft transitions, immutable opacity capture, Ellipse/history/clipping/partial-erase raster evidence, translucent source-over and full erasure checks, dock-visible style-command gating, OEM-bracket adapter checks, and exact-pair shortcut evolution/customization tests. R5-F3 adds scope precedence/cross-scope conflict tests, typed group coverage, effective-tooltip/menu-state models, shared hold routing, Hand/history isolation, 128 px raster regression, cursor state and physical-DPI geometry matrices, and normalized floating placement/settings round trips. R5-F3-P1 adds exact routing tests for pointer/draft/dock/viewport/Stage dirtiness, focused session-notification evidence, transform-only pointer/dock geometry, compositor photo-isolation configuration, and opt-in counter separation; actual smoothness remains a Release manual observation. R5-P2 adds atomic viewport-state, progressive neighbor, direction-priority, actual Stage-draw fallback counters, and mismatched-identity raster coverage. R5-P3 adds reclaim-aware saturation, protected-current/LRU lifetime, and higher render-pipeline counter coverage; perceptual acceptance remains owner-corpus review rather than a unit-test claim.

R6-A adds a self-authored runtime JPEG/EXIF APP1 fixture and adapter mapping tests for camera, lens, focal length, aperture, shutter rational, ISO, and unspecified capture time; no-EXIF/malformed/partial recovery; pure sparse/localized formatting; immediate oriented/file base data; lazy hidden-panel behavior; latest-wins asynchronous publication; bounded LRU/reparse avoidance/new-sequence reset; additive `I` conflict preservation; normalized bottom-left placement; and canonical/Blink/Peek presented-identity behavior. Private owner photographs remain manual-only evidence and are never test fixtures.

R6-B adds deterministic BGRA channel/alpha fixtures, transparent exclusion and unpremultiplication assertions, exact-versus-bounded deterministic sampling, retained-pixel lifetime, shared plot normalization, 128-entry LRU, hidden/toggle/cancel/latest-wins/new-sequence coordinator behavior, Blink-like identity swap/cache restoration, Peek stability, additive `G` conflict preservation, bottom-right placement, EN/RU chrome, and a non-threshold 24 MP exact/sampled engineering timing smoke. Private photographs remain manual-only visual/performance evidence.

CI restores, builds, and tests on Windows, Linux, and macOS. The workflow's presence is not evidence of a hosted passing run, and build/test success does not prove the Avalonia viewer's runtime rendering on those systems.
