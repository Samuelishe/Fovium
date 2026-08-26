# Documentation index

Role: Navigation map for project documentation.
Read when: Selecting context for a task or locating an authoritative owner.
Authoritative for: Document discovery and reading routes.
Not authoritative for: The subjects summarized by linked documents.

| Document | Contains | Read when |
| --- | --- | --- |
| [`PROJECT-STATE.md`](PROJECT-STATE.md) | Current checkpoint, focus, implemented capability, and active blockers | Every nontrivial task |
| [`PROJECT-VISION.md`](PROJECT-VISION.md) | Product identity, audience, philosophy, Stage, photographer features, and non-goals | Evaluating product direction or feature fit |
| [`UX-CONTRACT.md`](UX-CONTRACT.md) | Observable interaction and zero-UI behavior | Changing input, menus, window behavior, or settings UX |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Responsibility boundaries, dependency direction, and minimal architecture rules | Designing components or dependencies |
| [`IMAGING-PIPELINE.md`](IMAGING-PIPELINE.md) | Probe, orientation, decoding, metadata boundary, formats, and large-image safety | Working on image ingestion or codecs |
| [`FORMAT-SUPPORT.md`](FORMAT-SUPPORT.md) | Current supported formats and per-format decode/alpha/animation/metadata status | Checking or changing format-level capability |
| [`RENDERING.md`](RENDERING.md) | Viewport semantics, DPI, physical-pixel 100%, zoom, pan, and R0 rendering evidence | Working on display or viewport behavior |
| [`COLOR-MANAGEMENT.md`](COLOR-MANAGEMENT.md) | Source/destination profiles, transforms, multi-monitor behavior, and research boundary | Working on color or display profiles |
| [`COLOR-PICKER.md`](COLOR-PICKER.md) | Offline click sampling, reference sRGB, local names, and ten-click history | Changing Color Picker behavior, math, catalog, input, or overlay |
| [`PERFORMANCE.md`](PERFORMANCE.md) | Responsiveness, preload/cache policy, concurrency, limits, and measurement | Working on loading, navigation latency, memory, or diagnostics |
| [`CODING-GUIDELINES.md`](CODING-GUIDELINES.md) | C# implementation and testing conventions | Writing or reviewing production code |
| [`TEST-EXECUTION.md`](TEST-EXECUTION.md) | Local/CI test commands and evidence boundaries | Running, filtering, or interpreting tests |
| [`PROJECT-STATS.md`](PROJECT-STATS.md) | Repository diagnostics usage, exclusions, metrics, and limits | Running or changing ProjectStats |
| [`experiments/R0-RENDERING-PROBE.md`](experiments/R0-RENDERING-PROBE.md) | R0 setup, observations, comparisons, limitations, and recommendation | Implementing from or re-evaluating R0 evidence |
| [`experiments/R7-C-HEIF-AVIF-BACKEND-PROBE.md`](experiments/R7-C-HEIF-AVIF-BACKEND-PROBE.md) | HEIF/AVIF backend and native-runtime packaging feasibility evidence | Re-evaluating the gated R7-C codec direction |
| [`experiments/R8-B-MONITOR-COLOR-MANAGEMENT-PROBE.md`](experiments/R8-B-MONITOR-COLOR-MANAGEMENT-PROBE.md) | Monitor-profile discovery, Skia/Little CMS transforms, direct-target semantics, platform differences, and rendering-strategy evidence | Selecting or implementing the gated monitor color-management direction |
| [`VERSIONING.md`](VERSIONING.md) | Display/numeric version semantics, checkpoint increments, and version metadata direction | Changing a project checkpoint or exposing a version |
| [`SETTINGS.md`](SETTINGS.md) | Settings sections, preference ownership, persistence, migration, and reset policy | Designing settings or stored preferences |
| [`LOCALIZATION.md`](LOCALIZATION.md) | Supported locales, locale resolution, fallback, and translation boundaries | Adding or changing localized UI text |
| [`THEMES.md`](THEMES.md) | Dark/light application themes, semantic roles, and separation from Stage | Styling application UI or theme behavior |
| [`PLATFORM-INTEGRATION.md`](PLATFORM-INTEGRATION.md) | External activation, file associations, document icons, thumbnails, and packaging boundaries | Integrating with operating-system file workflows |
| [`ROADMAP.md`](ROADMAP.md) | Directional stages and future work | Planning or scoping a stage |
| [`DECISIONS-LOG.md`](DECISIONS-LOG.md) | Durable decisions already established | Checking or recording why a direction is binding |
| [`KNOWN-PROBLEMS.md`](KNOWN-PROBLEMS.md) | Open risks and unresolved technical questions | Investigating uncertainty or planning validation |
| [`THIRD-PARTY.md`](THIRD-PARTY.md) | Dependencies, evaluated technology, CI actions/services, and asset provenance | Considering or adding third-party material |
| [`DOCUMENTATION-GOVERNANCE.md`](DOCUMENTATION-GOVERNANCE.md) | Canonical ownership, update triggers, conflict resolution, and archival rules | Editing documentation or resolving overlap |

Operational repository rules live in the root [`AGENTS.md`](../AGENTS.md). The public introduction is [`README.md`](../README.md).
