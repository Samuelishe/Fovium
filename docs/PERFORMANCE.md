# Performance

Role: Responsiveness, scheduling, cache, and resource-policy contract.
Read when: Working on startup, loading, navigation, preload, cache, concurrency, large images, performance settings, or diagnostics.
Authoritative for: Latest-wins semantics, cancellation/generation ownership, decode concurrency, memory budgeting, runtime automatic policy, and acceptance measurement principles.
Not authoritative for: Exact image viability fields, viewport math, permanent numeric limits, or selected performance libraries.

## Experience targets

Fovium should feel immediate at startup, first image open, previous/next navigation, zoom, pan, fullscreen, resize, DPI transitions, and later Peek 100%, Blink Compare, and Ambient generation. These paths must be measured separately because an average throughput number can hide visible stalls.

The UI thread must not perform disk I/O, expensive header parsing, full decode, ICC conversion, expensive display preparation, or Ambient generation. UI work is limited to input, lightweight state coordination, and publication/render integration that the chosen framework requires.

## Loading and latest-wins ownership

Each navigation selection receives a monotonic generation or equivalent owned request identity. Results may publish only if they still belong to the current selection. Cancellation reduces wasted work, but correctness never depends on a backend observing cancellation promptly.

Rapid `Right Arrow` input must not allow an older decode to replace the newest selected image later. Avoid unrelated boolean flags; model request ownership, lifecycle, and publication conditions directly.

Decode concurrency is bounded. Foreground work for the selected image outranks speculative neighbors. Concurrency limits should reflect memory pressure as well as CPU availability, since several simultaneous decodes may allocate large native and managed buffers.

## Navigation preload

Navigation is a core subsystem. After opening an image, discover viable neighbors in the same directory and prepare at least the most useful adjacent candidates under a bounded policy. Preload is cancellable/speculative, must yield to current-image work, and must not make opening the current image slower.

Unsupported, corrupt, or resource-policy-rejected candidates are skipped so navigation can continue. Probe/decode eligibility is owned by [`IMAGING-PIPELINE.md`](IMAGING-PIPELINE.md).

## Cache and memory budget

Use a bounded cache with explicit cost accounting and eviction. Costs should include all retained managed/native source and display representations, color-converted surfaces, and other significant prepared data rather than only encoded bytes.

Automatic policy is computed at runtime from actually available resources, current pressure, concurrent work, and conservative product caps. It does not run a one-time CPU/GPU benchmark or maintain a hardware-model database. Planned choices may include Automatic, fixed budgets such as 256 MB through 2 GB, and Custom, but values remain product/UI directions rather than permanent limits.

Large-image decode policy follows the same model: probe first, estimate peak working cost, include safety margin and concurrent allocations, then admit, defer, downsample, tile in the future, or reject. R0's checked 512 MiB guard estimates its two simultaneous BGRA copies and protects only the disposable probe; it is not a universal product cutoff.

## Diagnostics

Future opt-in diagnostics may show renderer/backend, display scaling, cache budget/usage, current decoded allocation, pending decodes, decode time, and color-transform time. Diagnostics are for investigation and must not become permanent viewport chrome.

## Acceptance measurement

Define representative local test assets and record environment, format, dimensions, source profile, cold/warm state, renderer/backend, display scaling, and memory conditions. Measure distributions and worst visible stalls for critical interactions, not only averages. Track managed allocations, native allocations where observable, cache accounting, UI-thread blocking, stale-result rejection, and cleanup after cancellation.

Numeric acceptance thresholds should be set only after R0/R1 measurements on representative hardware. Once adopted, record them here rather than scattering them through code or status documents.
