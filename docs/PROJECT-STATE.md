# Project state

Role: Compact handoff of the project's current truth.
Read when: At the start of every nontrivial task.
Authoritative for: Current checkpoint, immediate focus, implemented functionality, and active blockers.
Not authoritative for: Durable decisions, future plans, detailed contracts, or Git HEAD/status.

## Current checkpoint

DOCS-R1, REPO-R1, and the bounded R0 rendering/imaging probe are complete locally. R0 accepted Avalonia as the initial desktop host, a physical-scale viewport model, and an isolated direct-Skia/SKCodec foundation. Retained evidence is in [`experiments/R0-RENDERING-PROBE.md`](experiments/R0-RENDERING-PROBE.md). No GitHub-hosted CI run is claimed by this local stage.

## Current focus

Plan R1 as the first production Core Viewer vertical slice using the bounded R0 decisions without importing the probe UI as product architecture.

## Implemented application functionality

None. The solution contains repository tooling, tests, and the disposable `experiments/Fovium.RenderProbe` executable. There is no production Fovium application project or runnable viewer.

## Active blockers

- Avalonia's direct-Skia lease used by the accepted initial renderer is explicitly unstable and must remain isolated.
- Physical-pixel 100% is validated by pure tests at 1.00/1.25/1.50/2.00, but runtime evidence exists only at `RenderScaling = 1.00`; per-monitor transitions still need real hardware coverage.
- The monitor-aware color pipeline and raw embedded-profile extraction boundary have not been selected.
- Broader codec support and a huge/tiled-image strategy remain unselected.
- GitHub Actions portability is configured but cannot be claimed as passing until the workflow runs remotely.

Open technical risks are tracked in [`KNOWN-PROBLEMS.md`](KNOWN-PROBLEMS.md). Directional stages are in [`ROADMAP.md`](ROADMAP.md). Git remains the authority for branch, HEAD, and worktree status.
