# Architecture

Role: Boundary and dependency guide for the initial implementation.
Read when: Structuring production code, reviewing coupling, adding a subsystem, or considering an abstraction.
Authoritative for: Minimal-solution philosophy, logical responsibilities, dependency direction, platform/UI separation, decoder abstraction direction, and source-versus-display representations.
Not authoritative for: Exact future classes, final renderer/library selection, interaction policy, or current state.

## Shape

R1 begins with one production assembly, `Fovium`. Add projects only when an evidenced build, deployment, test, or dependency boundary requires them. Do not pre-create enterprise Clean Architecture layers.

Repository tooling and experiments are separate from this future production shape. `Fovium.Tools.ProjectStats` depends only on the BCL and remains diagnostic CLI code. `experiments/Fovium.RenderProbe` is disposable evidence, not a production layer or class-name source. `Fovium.Tests` may reference tooling, experiments, and production assemblies; production assemblies must never depend on them.

Logical responsibilities remain distinct even if physically colocated:

- directory discovery and navigation;
- image probing, decode planning, and decoding;
- source image representation and metadata extraction;
- display preparation, color transformation, and rendering;
- viewport math;
- cache and loading coordination;
- settings;
- platform integration;
- UI and view-specific interaction.

Use ordinary composition and explicit ownership. Do not use a Service Locator, mutable global state, a giant `MainWindow` code-behind, or a God ViewModel. MVVM is a tool, not a rule: pointer-heavy view interaction may live in a custom control or focused code-behind while domain-independent math remains testable.

## Dependency direction

- Navigation identifies candidates and selection generations; it does not know Skia, pixels, or renderer details.
- Probing and decoding consume a selected source and produce project-owned imaging results; they do not drive navigation or UI.
- Rendering consumes prepared display data plus viewport state; it never chooses the next file.
- The UI/coordinator requests work and publishes the newest valid result; it does not decode formats or perform ICC transforms itself.
- Platform-specific implementations sit behind narrow boundaries at the edge. Platform details must not leak throughout application code.
- Settings storage owns typed persisted preferences; the viewer coordinator resolves image-change policy into a `ViewTransfer`. Renderer and viewport math never query settings.
- Stage policy and asynchronous Ambient preparation are coordinated above rendering. Selection first resolves any cached matching Stage presentation; the viewport then installs photograph, identity, view transfer, Stage settings, and that retained Ambient as one render-visible transition before current missing work starts. Current Ambient remains immediate presentation work; each progressively ready adjacent decode may trigger speculative matching preparation without a full-neighbor barrier. The renderer receives one identity-coherent snapshot and rejects mismatched Ambient; it materializes backend paths without reading settings, scheduling background work, or modifying viewport state.
- Viewer keys resolve through stable command, scope, group, context, and project-owned gesture models. Resolution orders active Markup, Highlight, then Global scope; duplicates are conflicts only within the same scope. Group metadata independently drives Settings organization. The Avalonia key adapter is a narrow boundary; command execution is not a generic application command bus and leaves `Esc` reserved.
- Hold input is owned by one focused controller that remembers the initiating primary key and suppresses repeat/re-entry. A small router dispatches inspection holds to the viewer inspection coordinator and Markup Space-Hand to presentation state without creating another tracker. Inspection snapshots render-independent view semantics and presents through a narrow viewport layer rather than mutating canonical navigation.
- Presenter state is a bounded, session-local model keyed by canonical image identity. Each document has one ordered operation array and history cursor; immutable active snapshots contain oriented-source draw, erase, and clear semantics with no Skia/Avalonia transforms. Draw elements capture immutable opacity as well as color and source-space width. A pure project-owned geometry helper owns 45-degree and square/circle constraints; the Avalonia viewport translates Shift into a project-owned modifier at its boundary. The viewport chooses canonical or Blink comparison identity, while the markup renderer alone replays history in a destination-bounded isolated layer after the photograph.
- Drawing-cursor and floating-overlay placement are pure presentation models. The viewport translates physical tool size through `RenderScaling`, suppresses the general Highlight while markup interaction is active, and reuses canonical pan for the non-history Hand tool. R5-F3-P1 separates update frequency at the Avalonia boundary: a compositor-cached photographic custom visual, a transparent markup composition visual, a small transform-positioned pointer-feedback control, and transform-driven floating UI. Draft changes publish only immutable markup snapshots; pointer motion changes only pointer translation; panel placement is persisted once after drag. Project-owned vector icon geometry remains centralized secondary-UI data rather than a third-party dependency.
- The viewport exposes a retained, read-only presented-image lease and identity event without navigation authority. Photo Info and Histogram consume that boundary so canonical and Blink presentations share one identity-safe path; Peek does not publish a new identity. Metadata parses retained encoded bytes behind its adapter. Histogram acquires a second lease on the existing decoded native payload, performs bounded cancellable BGRA iteration behind a project-owned reader, and publishes only the latest visible presentation. Neither overlay has navigation authority.
- `ViewerSession` exposes a cancellable read-only neighbor-inspection acquisition. It returns a retained project-owned image lease, prefers cache/preload, may yield speculative preload, and rejects stale sequence/selection results without changing canonical indices, generation, or protected current identity.
- Speculative photo admission asks the byte cache how large an unprotected replacement may be after ordinary LRU reclamation. That allowance excludes the protected current entry but includes reclaimable neighbors; decode working memory remains independently bounded, and `ByteBudgetCache.Add` remains the final race-safe ownership/eviction gate.

Dependencies should point toward small project-owned contracts and pure models only where substitution, resource ownership, or test isolation creates a real need. Do not add interfaces speculatively.

## Imaging extensibility

Multiple decoder backends coexist behind one small project-owned synchronous backend-result contract and one asynchronous high-level loader. Adding a codec backend must not require rewriting navigation, viewport math, cache policy, or rendering. This is backend extensibility, not a user plugin system. R7-A adds one immutable project-owned capability authority for stable format identity, candidate extensions, picker hints, detected Skia mapping, alpha, and static/animation policy. Navigation consumes only candidate capability; the decoder consumes content-detected capability; `ImageDescriptor` carries Fovium format identity rather than a backend enum or arbitrary string.

R7-B makes the high-level `ImageDecoder` own the existing two expensive-decode slots across every backend. A TIFF signature-first backend returns `NotMyFormat` without preventing the later Skia probe; recognized unsupported/corrupt/resource-limited variants stop with common domain failures. Skia remains isolated to JPEG, PNG, and static WebP. The focused TIFF backend validates classic single-image 8-bit layout before allocation, decodes strips or tiles directly into the final Skia BGRA bitmap, and releases all row/tile working buffers before the common `DecodedImage` enters cache/viewer ownership. Viewer/session code receives no TIFF or LibTiff types.

The production direct-Skia adapter is confined to `SkiaPhotoDrawOperation`, hosted by a focused composition custom-draw boundary whose compositor-managed bitmap cache prevents unrelated overlay damage from re-entering photographic Skia drawing. Stage composition uses a focused Skia stage renderer called by that adapter; Skia types do not leak into navigation or render-independent viewport math, and Avalonia's unstable lease remains at the platform/render edge.

## Source and display representations

A **source image representation** retains facts required to interpret the asset: oriented dimensions, pixel representation, source color/profile data, relevant metadata references, frame information, and resource ownership.

A **display representation** is prepared for a particular rendering/color path, scale need, or destination and may be cached under an explicit budget. It must not silently become the only surviving copy of source semantics.

Keeping these concepts distinct allows later monitor-aware transforms, alternative sampling paths, adjacent-image preload, and large-image strategies without pushing those concerns into navigation.

## Concurrency and lifetime

Long-running operations receive cancellation and an explicit session/generation identity. Reference-counted cache/display/render leases let eviction or replacement release ownership while a retained Avalonia draw operation keeps native photo and optional Ambient `SKImage` instances alive. The optional blur-prepared Ambient is keyed by source identity plus blur, attached to the owning decoded image, and charged to the same byte-budget LRU. Brightness/saturation and Matte changes do not create new native images. During Blink, the canonical photo/Ambient presentation stays retained while a separately retained comparison photo and, only when already matching, comparison Ambient are temporarily rendered. Release never navigates, decodes, or performs a fallible cache lookup. Eviction, blur replacement, inspection cancellation, new-sequence clear, stale completion, and shutdown therefore share deterministic ownership. Markup owns only capped managed primitive arrays; it neither owns photo/Ambient leases, participates in the photo cache, writes source/sidecar files, nor introduces another compositor. Latest-wins publication and resource budgets are specified in [`PERFORMANCE.md`](PERFORMANCE.md); coding rules are in [`CODING-GUIDELINES.md`](CODING-GUIDELINES.md).
