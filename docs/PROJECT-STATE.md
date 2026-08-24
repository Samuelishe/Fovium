# Project state

Role: Compact handoff of the project's current truth.
Read when: At the start of every nontrivial task.
Authoritative for: Current checkpoint, immediate focus, implemented functionality, and active blockers.
Not authoritative for: Durable decisions, future plans, detailed contracts, or Git HEAD/status.

## Current checkpoint

DOCS-R1 and REPO-R1 are complete locally. The repository has documentation/RAG-lite, hygiene policy, a tracked resources root, ProjectStats tooling, automated tests, and a cross-platform CI workflow. No GitHub-hosted CI run has been observed yet.

## Current focus

Begin a separate bounded R0 probe for rendering quality, DPI and physical-pixel semantics, decoder foundations, and color-management feasibility.

## Implemented application functionality

None. The solution contains repository tooling and its tests only; there is no Fovium application project or runnable viewer.

## Active blockers

- The rendering path and image decode stack have not been validated or selected.
- Physical-pixel 100% and DPI-transition behavior need runtime evidence.
- The monitor-aware color pipeline has not been selected.
- GitHub Actions portability is configured but cannot be claimed as passing until the workflow runs remotely.

Open technical risks are tracked in [`KNOWN-PROBLEMS.md`](KNOWN-PROBLEMS.md). Directional stages are in [`ROADMAP.md`](ROADMAP.md). Git remains the authority for branch, HEAD, and worktree status.
