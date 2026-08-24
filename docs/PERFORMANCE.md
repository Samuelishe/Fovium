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

R1 implements one sequential speculative preload worker. After a publication it searches for one viable previous and one viable next neighbor, with current foreground selection/cancellation taking priority. R2 may refine directionality or concurrency only from measured need.

## Cache and memory budget

Use a bounded cache with explicit cost accounting and eviction. Costs should include all retained managed/native source and display representations, color-converted surfaces, and other significant prepared data rather than only encoded bytes.

Automatic policy is computed at runtime from actually available resources, current pressure, concurrent work, and conservative product caps. It does not run a one-time CPU/GPU benchmark or maintain a hardware-model database. Planned choices may include Automatic, fixed budgets such as 256 MB through 2 GB, and Custom, but values remain product/UI directions rather than permanent limits.

This document owns the policy; [`SETTINGS.md`](SETTINGS.md) owns where future user-facing Automatic/manual choices appear and how those preferences persist.

Large-image decode policy follows the same model: probe first, estimate peak working cost, include safety margin and concurrent allocations, then admit, defer, downsample, tile in the future, or reject. R0's checked 512 MiB guard estimates its two simultaneous BGRA copies and protects only the disposable probe; it is not a universal product cutoff.

R1's provisional Automatic formula uses `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` as the cross-platform runtime allowance (with a 1 GiB fallback only if unavailable):

- cache budget = one eighth, clamped to 256 MiB–1 GiB;
- foreground working allowance = one quarter, clamped to 256 MiB–2 GiB;
- one speculative decode allowance = the smaller of the cache budget and half the foreground allowance.

Admission checks both estimated peak working bytes and retained encoded-plus-BGRA bytes. The foreground image is admitted before speculative work, the displayed cache key is protected, and byte-accounted LRU eviction releases neighbors deterministically. These constants are R1 safety evidence, not permanent product settings.

## Diagnostics

Future opt-in diagnostics may show renderer/backend, display scaling, cache budget/usage, current decoded allocation, pending decodes, decode time, and color-transform time. Diagnostics are for investigation and must not become permanent viewport chrome.

## Acceptance measurement

Define representative local test assets and record environment, format, dimensions, source profile, cold/warm state, renderer/backend, display scaling, and memory conditions. Measure distributions and worst visible stalls for critical interactions, not only averages. Track managed allocations, native allocations where observable, cache accounting, UI-thread blocking, stale-result rejection, and cleanup after cancellation.

R1 Windows smoke at `RenderScaling = 1.00` used local runtime inputs: a 3840×2400 JPEG probed/decoded/prepared in 23.5/46.2/5.1 ms (81.9 ms decoder end-to-end), a 2400×3840 JPEG in 18.7/32.8/7.4 ms (65.5 ms end-to-end), and a 640×480 alpha PNG in 18.3/1.5/0.5 ms (26.6 ms end-to-end). A warm Release launch showed the window at 538 ms and the first photograph at 632 ms by screen polling. A prepared adjacent switch was observed at 29 ms; an immediate follow-on navigation/skip/load change at 105 ms. These are single-run engineering observations, not thresholds.

After an initial 360-key navigation burst, process working/private memory rose by about 13.5/14.2 MiB; a second 360-key burst rose by about 1.3/0.9 MiB and handle count decreased from 777 to 772. The process remained responsive. This is an observed bounded smoke, not a leak proof or a cross-platform benchmark. Numeric acceptance thresholds still require representative environments and assets.
