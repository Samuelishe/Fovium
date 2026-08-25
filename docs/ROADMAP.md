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

## R2 — Navigation, view continuity, Settings foundation, and performance hardening

Accepted outcome: preserve physical scale/point of interest by default with a Fit-each alternative, persist the choice through the first minimal Settings surface, establish the ignored local imaging corpus, and harden async shutdown/cache ownership. Local evidence retained adjacent-only preload, two decode slots, and the provisional memory formula.

## R3 — Stage

Accepted outcome: persisted Black, Neutral, Ambient, and Ambient + Matte presentation modes with bounded oriented derivation, shared cache ownership, synchronized Settings/context-menu access, and unchanged photograph/viewport behavior.

## R3-F1 — Stage customization and configurable controls

Accepted outcome: split Stage background from independent Matte, add Custom/color and bounded Ambient controls with schema-v2 migration, and establish persistent conflict-safe viewer command bindings before hold-based inspection actions.

## R3-F2 — Configurable Matte geometry and styles

Accepted outcome: add persisted DPI-aware Matte width plus Solid, Rounded, Soft, and Angular outer presentation styles while retaining a rectangular, unmodified photograph and isolating all Matte changes from Ambient preparation.

## R4 — Peek 100% / Blink Compare

Accepted outcome: add configurable press-and-hold whole-viewport Peek 100% and previous-viable Blink Compare on the shared viewport/session models. Peek preserves cursor/source anchoring and restores semantic view state; Blink uses retained read-only acquisition without changing canonical navigation, and both cancel safely on lost hold authority.

## R5 — Presenter overlay and cursor highlight

Accepted outcome: add configurable cursor highlight plus an on-demand Brush/Line/Rectangle/Arrow dock. Markup is bounded, image-bound in oriented source space, session-local, memory-only, compatible with viewport changes and Peek/Blink, and never writes source or sidecar data.

## R5-F1 — True Eraser and markup history foundation

Accepted outcome: replace append-only primitives with bounded per-image ordered operation history; add a true partial visual Eraser through an isolated markup layer, per-image Undo/Redo, undoable Clear, and conflict-safe configurable history shortcuts without changing source files or adding persistent annotation data.

## R5-F2 — Markup shapes, opacity, and Shift constraints

Accepted outcome: add Ellipse/Shift-circle, immutable per-draw opacity, Shift constraints for Line/Arrow/Rectangle/Ellipse/Brush, contextual bracket-key thickness/opacity adjustment, and conflict-safe Clear/Blink default evolution without adding a dedicated Highlighter or persistent markup data.

## R5-P1 — Ambient transition latency hardening

Accepted outcome: retain photograph-first navigation while promoting matching current-image Ambient ahead of speculative neighbor work, add identity-aware photo-to-Ambient diagnostics, and eliminate the normal sequential 24 MP Black edge flash without mismatched fallback, a larger cache, or transition animation.

## R5-F3 — Contextual drawing shortcuts and presentation UI polish

Expected next bounded stage after explicit R5-P1 owner acceptance. It may address contextual tool shortcuts plus compact dock/Controls grouping and iconography; do not start automatically or fold in text, selection/editing, export, sidecars, or layers.

## R6 — Metadata / context-menu polish

Potential later bounded stage: unobtrusive metadata access and coherent context-menu polish. It requires explicit owner acceptance after the remaining bounded presenter work; do not expand presenter markup into editing/export work.

## Later / separate platform milestones

- file association and Open With integration;
- platform packaging;
- platform thumbnail providers where they add value;
- broader codec coverage and specialized/native backends;
- full monitor ICC and multi-monitor color validation;
- huge-image tiled or region strategies;
- macOS runtime validation;
- HDR research when the SDR pipeline is trustworthy.
