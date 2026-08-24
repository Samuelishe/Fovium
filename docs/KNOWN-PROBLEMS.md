# Known problems and open risks

Role: Register of unresolved technical risks and validation gaps.
Read when: Planning technical research, encountering an uncertain boundary, or checking whether a limitation is established.
Authoritative for: Open risks and questions that do not yet have an accepted resolution.
Not authoritative for: Confirmed implementation bugs, current progress, accepted decisions, or roadmap order.

These are investigation items, not claims about code that does not yet exist.

| Risk | Why it matters | Intended resolution path |
| --- | --- | --- |
| Avalonia direct-Skia bridge is unstable API | The accepted initial photographic path uses public `ISkiaSharpApiLeaseFeature`, but Avalonia marks it unstable | Keep one replaceable adapter and validate upgrades before changing the package line |
| Runtime DPI evidence is single-display Windows at 100% | Pure math covers 100/125/150/200%, but simulated scale is not per-monitor or cross-platform runtime proof | Exercise real fractional/per-monitor Windows, Linux scaling, and Retina during R1 validation |
| Monitor-aware color pipeline is not selected | Source ICC and destination profiles may need library and platform cooperation | Preserve source data, research Skia/LittleCMS/native paths, then record a decision |
| Raw embedded ICC extraction is unresolved | SKCodec exposes normalized color space but not the original profile payload required by some future transform paths | Evaluate explicit profile extraction before claiming monitor-aware color |
| Codec stack beyond initial SKCodec JPEG/PNG is not selected | Format breadth, correctness, native deployment, and resource costs vary substantially | Add project-owned decoder contracts and evaluate a backend only when a format requires it |
| Huge-image strategy beyond admission limits is unknown | Full decode may be unsafe while tiled/region support is backend-specific | Start with probe-based limits; investigate tiling later |
| Retained custom-draw lifetime needs production hardening | A draw operation may outlive image replacement if ownership is wrong | Define render-operation/image ownership and stress replacement/cancellation in R1 |
| Avalonia/Skia runtime portability is not proven on Linux/macOS | The solution builds locally on Windows and CI may compile elsewhere, but runtime graphics behavior is platform-specific | Perform real Linux and macOS RenderProbe/Core Viewer smoke when environments are available |
| File-association and thumbnail-provider mechanics are not validated | Registration, sandboxing, packaging, thumbnail APIs, and supported capabilities differ materially across Windows, Linux desktops, and macOS | Validate each platform independently when a concrete packaging or thumbnail milestone begins; do not assume parity |

When evidence resolves a risk, record any durable choice in [`DECISIONS-LOG.md`](DECISIONS-LOG.md), update the owning technical document, and remove or narrow the risk here. Confirmed defects in future code should use the project's eventual issue workflow rather than turning this file into a bug backlog.
