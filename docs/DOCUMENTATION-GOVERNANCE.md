# Documentation governance

Role: Rules for maintaining a compact, non-conflicting repository knowledge base.
Read when: Adding or changing documentation, updating project handoff state, or resolving conflicting statements.
Authoritative for: Canonical ownership, update triggers, conflict resolution, selective reading, historical archival direction, and persistent artifact discipline.
Not authoritative for: The product and technical content owned by other documents.

## Canonical ownership

Each durable subject has one owner. Other documents may summarize and link but must not establish a competing contract.

| Subject | Canonical owner |
| --- | --- |
| Agent workflow, safety, and context routing | [`../AGENTS.md`](../AGENTS.md) |
| Current checkpoint, focus, functionality, and blockers | [`PROJECT-STATE.md`](PROJECT-STATE.md) |
| Product identity, long-term boundaries, Stage, Peek/Blink direction | [`PROJECT-VISION.md`](PROJECT-VISION.md) |
| User-visible interaction | [`UX-CONTRACT.md`](UX-CONTRACT.md) |
| Responsibility and dependency boundaries | [`ARCHITECTURE.md`](ARCHITECTURE.md) |
| Probe, decode, formats, orientation, source representation | [`IMAGING-PIPELINE.md`](IMAGING-PIPELINE.md) |
| Current supported formats and per-format capability matrix | [`FORMAT-SUPPORT.md`](FORMAT-SUPPORT.md) |
| Viewport, DPI, physical-pixel 100%, sampling research | [`RENDERING.md`](RENDERING.md) |
| Source/destination color and transform direction | [`COLOR-MANAGEMENT.md`](COLOR-MANAGEMENT.md) |
| Loading, cache, concurrency, limits, performance measurement | [`PERFORMANCE.md`](PERFORMANCE.md) |
| C# implementation practice | [`CODING-GUIDELINES.md`](CODING-GUIDELINES.md) |
| Test commands and execution evidence | [`TEST-EXECUTION.md`](TEST-EXECUTION.md) |
| ProjectStats behavior and metric semantics | [`PROJECT-STATS.md`](PROJECT-STATS.md) |
| R0 retained experimental evidence | [`experiments/R0-RENDERING-PROBE.md`](experiments/R0-RENDERING-PROBE.md) |
| Version format, checkpoint numbering, and future metadata source | [`VERSIONING.md`](VERSIONING.md) |
| Settings organization, preference persistence, reset, and About content | [`SETTINGS.md`](SETTINGS.md) |
| UI locales, locale resolution, fallback, and translation boundaries | [`LOCALIZATION.md`](LOCALIZATION.md) |
| Application UI themes and visual roles | [`THEMES.md`](THEMES.md) |
| File activation, associations, document icons, thumbnails, and packaging integration | [`PLATFORM-INTEGRATION.md`](PLATFORM-INTEGRATION.md) |
| Future stage direction | [`ROADMAP.md`](ROADMAP.md) |
| Accepted durable decisions | [`DECISIONS-LOG.md`](DECISIONS-LOG.md) |
| Open unresolved risks | [`KNOWN-PROBLEMS.md`](KNOWN-PROBLEMS.md) |
| Dependency, action/service, and asset provenance | [`THIRD-PARTY.md`](THIRD-PARTY.md) |
| Documentation navigation | [`INDEX.md`](INDEX.md) |

## Selective reading

Every nontrivial task starts with root `AGENTS.md` and `PROJECT-STATE.md`, then reads only routed owner documents and affected files. Do not load the entire documentation set by habit. `INDEX.md` describes where information lives; it must not reproduce the information.

## Update triggers

- Update `PROJECT-STATE.md` when the checkpoint, immediate focus, implemented capability, or active blocker changes. Never copy current status into another progress file.
- Add or supersede a `DECISIONS-LOG.md` entry when evidence establishes or reverses a durable decision.
- Update `ROADMAP.md` when future stage scope or order changes; do not use it to report progress.
- Update `KNOWN-PROBLEMS.md` when an unresolved risk appears, narrows, or is resolved; hypotheses remain labeled as risks.
- Update a domain owner in the same change as a contract change. Update summaries only when they would otherwise misroute or materially mislead.
- Update `THIRD-PARTY.md` whenever an external component is introduced, upgraded with changed obligations, replaced, or removed.
- Update `PROJECT-STATS.md` with scanner, exclusion, output, or metric-semantics changes; generated report totals remain outside durable documentation.
- Update `TEST-EXECUTION.md` when the test platform, framework, supported commands, or CI test contract changes.
- Update `VERSIONING.md` once for each accepted checkpoint or explicit owner-controlled component change; mirror only the current checkpoint version in `PROJECT-STATE.md`.
- Update `SETTINGS.md`, `LOCALIZATION.md`, `THEMES.md`, or `PLATFORM-INTEGRATION.md` when the corresponding product-shell contract changes, rather than redefining it in an implementation document.
- Add or update a bounded experiment report when technical evidence must remain reviewable; promote only supported conclusions into the domain owner and decision log.
- Update `AGENTS.md` only for repository-wide operational rules or routing—not to store domain detail.

Dynamic branch, HEAD, status, generated measurements such as `project-stats.md/json`, build logs, and transient investigation notes belong to Git or ignored task artifacts, not `PROJECT-STATE.md`.

## Conflict resolution

1. Identify the subject and its canonical owner in the table above.
2. Treat the owner's statement as current unless direct evidence shows it is stale.
3. Repair the non-owner to link or summarize without redefining the rule.
4. If the owner itself conflicts with an accepted decision, stop and make the decision explicit: either align the owner or supersede the decision with rationale.
5. If evidence is incomplete, record an open risk rather than inventing certainty.

## Persistent artifacts and history

Keep durable knowledge in the repository, concise and reviewable. Do not create parallel progress reports, session handoff files, context planners, generated agent maps, or personal scratch documents as project truth.

When documents accumulate substantial obsolete history, move it to a future clearly marked archive location while keeping active owners compact. Archived material is historical evidence and never overrides active owners. Do not create an archive until there is real content to move.

Avoid duplicating prose. Use repository-relative links to owners, keep decisions separate from plans and risks, and remove stale summaries when a link is sufficient.
