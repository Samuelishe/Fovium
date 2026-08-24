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

## Scope

Focused tests are preferred during implementation; run the full solution before handoff when shared tooling or project configuration changes. The current xUnit suite covers repository tooling plus R0 viewport, orientation, decode-cost, and malformed-input logic. No production Fovium application exists yet.

UI interaction, rendering quality, runtime DPI, pixel alignment, color, native lifetime, and platform behavior require bounded integration, visual, and manual smoke evidence; passing pure tests cannot prove those properties.

CI restores, builds, and tests on Windows, Linux, and macOS. The workflow's presence is not evidence of a hosted passing run, and repository-tool portability does not yet prove a future Avalonia viewer runs correctly on those systems.
