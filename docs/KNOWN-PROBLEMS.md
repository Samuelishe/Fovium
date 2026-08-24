# Known problems and open risks

Role: Register of unresolved technical risks and validation gaps.
Read when: Planning technical research, encountering an uncertain boundary, or checking whether a limitation is established.
Authoritative for: Open risks and questions that do not yet have an accepted resolution.
Not authoritative for: Confirmed implementation bugs, current progress, accepted decisions, or roadmap order.

These are investigation items, not claims about code that does not yet exist.

| Risk | Why it matters | Intended resolution path |
| --- | --- | --- |
| Final renderer path is unknown | Quality, DPI behavior, latency, and resource ownership depend on it | Compare bounded Avalonia/Skia paths in R0 |
| Avalonia standard image rendering is not accepted as production quality | Framework defaults may not satisfy photographic sampling or exact-pixel needs | Measure it as a candidate rather than assume acceptance |
| DPI and physical-pixel 100% semantics are unproven | DIP behavior and per-monitor scaling can blur or mis-size photographs | Validate scale conversion, alignment, and monitor transitions in R0 |
| Monitor-aware color pipeline is not selected | Source ICC and destination profiles may need library and platform cooperation | Preserve source data, research Skia/LittleCMS/native paths, then record a decision |
| Codec stack is not selected | Format breadth, correctness, native deployment, and resource costs vary substantially | Probe representative backends and define the minimum validated initial set |
| Huge-image strategy beyond admission limits is unknown | Full decode may be unsafe while tiled/region support is backend-specific | Start with probe-based limits; investigate tiling later |
| Native resource lifecycle is unproven | Bitmaps, codecs, color transforms, and GPU resources can leak or outlive requests | Exercise cancellation, replacement, disposal, and pressure in probes |
| Package/version/platform compatibility is unknown | .NET 10, Avalonia, native codecs, and OS packaging constraints may interact | Validate versions and runtime deployment before introducing dependencies |

When evidence resolves a risk, record any durable choice in [`DECISIONS-LOG.md`](DECISIONS-LOG.md), update the owning technical document, and remove or narrow the risk here. Confirmed defects in future code should use the project's eventual issue workflow rather than turning this file into a bug backlog.
