# Fovium agent entry point

Role: Operational entry point for Codex and other repository agents.
Read when: At the start of every repository task.
Authoritative for: Baseline sequence, context routing, repository safety, and agent workflow.
Not authoritative for: Product details, current project state, technical design, or roadmap content.

## Start here

For every nontrivial task:

1. Read this file and [`docs/PROJECT-STATE.md`](docs/PROJECT-STATE.md).
2. Run `pwsh ./eng/repo-baseline.ps1` before changing files.
3. Use the routing table to read only the owner documents needed for the task.
4. Inspect the affected files and preserve user work, including unrelated staged, unstaged, and untracked changes.
5. Make the smallest coherent change, then verify in proportion to its risk.

Do not load every document by default. If documents conflict, follow the owner named in [`docs/DOCUMENTATION-GOVERNANCE.md`](docs/DOCUMENTATION-GOVERNANCE.md) and repair or report the stale reference.

## Git safety

- Treat existing changes as user-owned unless the task establishes otherwise.
- Do not use `git reset`, `git clean`, `git checkout --`, or `git restore`.
- Do not commit, push, pull, fetch, merge, or rebase without explicit user authorization.
- Do not rewrite or remove existing content merely to make the worktree cleaner.
- Report the final `git status --short` and distinguish pre-existing changes from task changes.

## Context routing

| Task | Required context |
| --- | --- |
| Any nontrivial task | [`docs/PROJECT-STATE.md`](docs/PROJECT-STATE.md) |
| Product, feature, or UX | [`docs/PROJECT-VISION.md`](docs/PROJECT-VISION.md) + [`docs/UX-CONTRACT.md`](docs/UX-CONTRACT.md) |
| Avalonia or view interaction | [`docs/UX-CONTRACT.md`](docs/UX-CONTRACT.md) + [`docs/RENDERING.md`](docs/RENDERING.md) |
| Zoom, DPI, viewport, or rendering | [`docs/RENDERING.md`](docs/RENDERING.md) |
| Decode, formats, or orientation | [`docs/IMAGING-PIPELINE.md`](docs/IMAGING-PIPELINE.md) |
| Cache, preload, limits, or concurrency | [`docs/PERFORMANCE.md`](docs/PERFORMANCE.md) + relevant imaging owner |
| ICC, color, or display profiles | [`docs/COLOR-MANAGEMENT.md`](docs/COLOR-MANAGEMENT.md) + [`docs/RENDERING.md`](docs/RENDERING.md) |
| Architecture or dependencies | [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) |
| C# implementation | [`docs/CODING-GUIDELINES.md`](docs/CODING-GUIDELINES.md) + affected area owner |
| Tests or test execution | [`docs/TEST-EXECUTION.md`](docs/TEST-EXECUTION.md) + [`docs/CODING-GUIDELINES.md`](docs/CODING-GUIDELINES.md) |
| ProjectStats diagnostics | [`docs/PROJECT-STATS.md`](docs/PROJECT-STATS.md) |
| R0 rendering/decoder evidence | [`docs/experiments/R0-RENDERING-PROBE.md`](docs/experiments/R0-RENDERING-PROBE.md) + affected technical owner |
| Planning | [`docs/ROADMAP.md`](docs/ROADMAP.md) |
| External dependency | [`docs/THIRD-PARTY.md`](docs/THIRD-PARTY.md) + affected technical owner |
| Documentation change | [`docs/DOCUMENTATION-GOVERNANCE.md`](docs/DOCUMENTATION-GOVERNANCE.md) |
| Unresolved technical risk | [`docs/KNOWN-PROBLEMS.md`](docs/KNOWN-PROBLEMS.md) |

## Non-negotiable constraints

- Fovium is a fast, photographer-centric image viewer, not a DAM, editor, catalog, organizer, file manager, or AI product.
- Zero-UI is intentional: the photograph is the primary UI. Do not add persistent chrome or discoverability features by default.
- Directory navigation is a core subsystem and must support seamless, latest-wins movement through viable neighboring images.
- Photographic 100% means one oriented source pixel approximately maps to one physical display pixel; logical scale `1.0` is not proof of 100%.
- Preserve orientation and source color-profile information through imaging boundaries; full monitor-aware color behavior is not yet validated.
- Guard large-image work by estimated decoded resource cost, not encoded file size alone.
- Allow multiple codec backends behind project-owned contracts, but do not design a plugin system.
- Keep navigation, probing/decoding, rendering, viewport math, color, cache, metadata, settings, platform integration, and UI interaction logically separated without enterprise ceremony.
- Repository tooling and experiments remain outside production runtime code. R0 established an initial Avalonia/direct-Skia/SKCodec direction; production work must use the canonical decisions and must not copy the probe UI or treat experiment types as final architecture.

## Verification and handoff

Verification must match the change: documentation work needs link, scope, formatting, and diff checks; implementation work later needs focused build/tests and any area-specific evidence. Do not run expensive unrelated checks.

Final responses should lead with the outcome, list changed files, summarize verification and unresolved risks, note preserved pre-existing changes, and suggest a next step when useful. Never claim a behavior, dependency, or decision that has not been demonstrated or recorded by its owner.
