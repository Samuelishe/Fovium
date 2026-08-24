# Roadmap

Role: Directional sequence of project stages.
Read when: Planning scope, choosing the next bounded outcome, or deciding whether work belongs now or later.
Authoritative for: Future stage direction and ordering.
Not authoritative for: Current progress, durable decisions, exact dates, or a permanent numbering guarantee.

Stage numbering is a planning aid and may evolve when evidence changes scope. Each stage should update [`PROJECT-STATE.md`](PROJECT-STATE.md), record durable decisions in [`DECISIONS-LOG.md`](DECISIONS-LOG.md), and leave unresolved risks in [`KNOWN-PROBLEMS.md`](KNOWN-PROBLEMS.md).

## DOCS-R1 — Repository documentation / Codex context foundation

Establish compact authoritative documents, selective context routing, repository safety instructions, and a read-only baseline helper. Do not create production application code.

## REPO-R1 — Repository foundation

Establish repository hygiene, a public README, asset provenance policy, BCL-only ProjectStats diagnostics, meaningful tests for repository tooling, and Windows/Linux/macOS restore-build-test CI. Keep generated reports untracked and create no viewer application.

## R0 — Rendering and imaging foundation probe

Bounded investigation of rendering quality, DPI and physical-pixel 100%, viewport behavior, decoder boundaries, source/profile preservation, and color-foundation feasibility. Produce evidence and decisions before establishing the production stack.

## CONTRACTS-R1 — Product-shell contracts

Establish canonical versioning, Settings, localization, themes, external activation, file-association, and thumbnail-integration contracts before production viewer implementation. This is documentation direction, not implemented application capability.

## R1 — First production Core Viewer

Create the smallest coherent viewer that opens supported images and implements the validated baseline viewport, input, and same-directory previous/next navigation path without pretending broader codec or color completion. The accepted vertical slice establishes JPEG/PNG, zero-UI Black Stage, navigation, preload/cache, viewport input, fullscreen, basic context menu, and EN/RU runtime localization.

## R2 — Navigation, cache, and performance polish as needed

Measure and refine adjacent-image preparation, latest-wins loading, bounded caching, failure skipping, startup/decode latency, and navigation responsiveness as evidence requires. Validate the provisional memory formula and runtime portability. Navigation already exists as a core path; R2 hardens it rather than introducing it as an optional feature.

## R3 — Stage

Implement and validate Black, Neutral, Ambient, and Ambient + Matte presentation modes without changing the original photograph or compromising responsiveness.

## R4 — Peek 100% / Blink Compare

Build photographer inspection actions on the shared viewport and navigation models, preserving view state and point of interest where intended.

## R5 — Settings / metadata / context-menu polish

Add unobtrusive metadata access, coherent context menus, settings, performance policy controls, and requested temporary diagnostics without persistent viewport chrome.

## Later / separate platform milestones

- file association and Open With integration;
- platform packaging;
- platform thumbnail providers where they add value;
- broader codec coverage and specialized/native backends;
- full monitor ICC and multi-monitor color validation;
- huge-image tiled or region strategies;
- macOS runtime validation;
- HDR research when the SDR pipeline is trustworthy.
