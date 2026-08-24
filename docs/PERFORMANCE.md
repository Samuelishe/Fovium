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

R2 retains one sequential speculative preload worker. After a publication it searches for one viable previous and one viable next neighbor, with current foreground selection/cancellation taking priority. Local stress did not establish enough benefit to justify direction prediction, next+1 preload, or more than the existing two decode slots.

R3 retains those photo/decode priorities. When Ambient is selected, photo publication occurs first with Black fallback; the Ambient coordinator waits for ordinary adjacent photo preload, prepares the current bounded derivative, then useful cached adjacent derivatives. Black and Neutral schedule no Ambient work. Source identity plus a Stage generation prevents late preparation from publishing for another photograph or inactive mode; cancellation only reduces waste.

## Cache and memory budget

Use a bounded cache with explicit cost accounting and eviction. Costs should include all retained managed/native source and display representations, color-converted surfaces, and other significant prepared data rather than only encoded bytes.

Automatic policy is computed at runtime from actually available resources, current pressure, concurrent work, and conservative product caps. It does not run a one-time CPU/GPU benchmark or maintain a hardware-model database. Planned choices may include Automatic, fixed budgets such as 256 MB through 2 GB, and Custom, but values remain product/UI directions rather than permanent limits.

This document owns the policy; [`SETTINGS.md`](SETTINGS.md) owns where future user-facing Automatic/manual choices appear and how those preferences persist.

Large-image decode policy follows the same model: probe first, estimate peak working cost, include safety margin and concurrent allocations, then admit, defer, downsample, tile in the future, or reject. R0's checked 512 MiB guard estimates its two simultaneous BGRA copies and protects only the disposable probe; it is not a universal product cutoff.

R1's provisional Automatic formula uses `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` as the cross-platform runtime allowance (with a 1 GiB fallback only if unavailable):

- cache budget = one eighth, clamped to 256 MiB–1 GiB;
- foreground working allowance = one quarter, clamped to 256 MiB–2 GiB;
- one speculative decode allowance = the smaller of the cache budget and half the foreground allowance.

Admission checks both estimated peak working bytes and retained encoded-plus-BGRA bytes. The foreground image is admitted before speculative work, the displayed cache key is protected, and byte-accounted LRU eviction releases neighbors deterministically. R2 retained this formula after local stress; these constants remain provisional evidence, not permanent product settings.

R3 does not increase those caps. A prepared Ambient is at most `384` pixels on its long edge in premultiplied BGRA (about `576 KiB` at a square worst case and typically less), is owned by its decoded image, and increases the same cache entry's retained-byte cost. Optional/speculative entries are evicted before a protected current image; a derivative that still cannot fit is discarded and the visible Stage remains Black.

## Diagnostics

Future opt-in diagnostics may show renderer/backend, display scaling, cache budget/usage, current decoded allocation, pending decodes, decode time, and color-transform time. Diagnostics are for investigation and must not become permanent viewport chrome.

## Acceptance measurement

Define representative local test assets and record environment, format, dimensions, source profile, cold/warm state, renderer/backend, display scaling, and memory conditions. Measure distributions and worst visible stalls for critical interactions, not only averages. Track managed allocations, native allocations where observable, cache accounting, UI-thread blocking, stale-result rejection, and cleanup after cancellation.

R1 Windows smoke at `RenderScaling = 1.00` used local runtime inputs: a 3840×2400 JPEG probed/decoded/prepared in 23.5/46.2/5.1 ms (81.9 ms decoder end-to-end), a 2400×3840 JPEG in 18.7/32.8/7.4 ms (65.5 ms end-to-end), and a 640×480 alpha PNG in 18.3/1.5/0.5 ms (26.6 ms end-to-end). A warm Release launch showed the window at 538 ms and the first photograph at 632 ms by screen polling. A prepared adjacent switch was observed at 29 ms; an immediate follow-on navigation/skip/load change at 105 ms. These are single-run engineering observations, not thresholds.

After an initial 360-key navigation burst, process working/private memory rose by about 13.5/14.2 MiB; a second 360-key burst rose by about 1.3/0.9 MiB and handle count decreased from 777 to 772. The process remained responsive. This is an observed bounded smoke, not a leak proof or a cross-platform benchmark. Numeric acceptance thresholds still require representative environments and assets.

R2 Windows local-corpus stress used repeated mixed-direction bursts, failed-candidate skipping, explicit sequence replacement, and high-cost decoded images. Across six sampled cycles the process remained responsive; working set ranged about 1083–1199 MiB and private bytes about 1188–1309 MiB after warm-up rather than increasing by traversal count. A two-file explicit reopen reduced the working/private set to about 681/754 MiB. Native/shell handle observations were affected by the Windows file picker and are not a leak verdict. Settings autosave completed without a visible navigation stall, and graceful shutdown after in-flight-aware lifetime hardening completed in about 77 ms in one run.

Three warm R2 launches with an existing tiny settings document showed the window at 505–546 ms and photograph at 513–560 ms by the same screen-polling style used for the R1 538/632 ms observation. This shows no material first-open regression in that local environment; it is not a general performance claim. All R2 numbers remain local observations, not thresholds or cross-platform proof.

R3 Windows local-corpus measurement used four already-decoded representative JPEG/PNG sources. The bounded Ambient step produced `384×384` or `256×384` BGRA resources of `589,824` or `393,216` bytes in `8.29–15.22 ms` in a Debug local measurement. Black and Neutral followed the no-preparation path. Rapid mixed-direction navigation under Ambient stayed responsive with stable handle observations (`797` to `791`); after traversing new material, a later repeated cycle settled near `331 MiB` working set / `328 MiB` private bytes rather than growing by traversal count. This is a bounded single-machine smoke, not a leak proof or Release benchmark.
