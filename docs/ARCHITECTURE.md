# Architecture

Role: Boundary and dependency guide for the initial implementation.
Read when: Structuring production code, reviewing coupling, adding a subsystem, or considering an abstraction.
Authoritative for: Minimal-solution philosophy, logical responsibilities, dependency direction, platform/UI separation, decoder abstraction direction, and source-versus-display representations.
Not authoritative for: Exact future classes, final renderer/library selection, interaction policy, or current state.

## Shape

Begin with one production assembly when implementation starts. Add projects only when an evidenced build, deployment, test, or dependency boundary requires them. Do not pre-create enterprise Clean Architecture layers.

Repository tooling and its tests are separate from this future production shape. `Fovium.Tools.ProjectStats` depends only on the BCL, has no reference to future Fovium runtime code, and remains a diagnostic CLI rather than an application subsystem. `Fovium.Tests` may reference tooling now and production assemblies later; production assemblies must never depend on the test or repository-tool projects.

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
- The UI/coordinator requests work and publishes the newest valid result; it does not decode JPEGs or perform ICC transforms itself.
- Platform-specific implementations sit behind narrow boundaries at the edge. Platform details must not leak throughout application code.
- Settings describe user policy; subsystems interpret it within safe product caps.

Dependencies should point toward small project-owned contracts and pure models only where substitution, resource ownership, or test isolation creates a real need. Do not add interfaces speculatively.

## Imaging extensibility

Multiple decoder backends may coexist behind a project-owned probe/decode contract. Adding a codec backend must not require rewriting navigation, viewport math, cache policy, or rendering. This is backend extensibility, not a user plugin system. Exact contracts wait for R0 evidence; see [`IMAGING-PIPELINE.md`](IMAGING-PIPELINE.md).

## Source and display representations

A **source image representation** retains facts required to interpret the asset: oriented dimensions, pixel representation, source color/profile data, relevant metadata references, frame information, and resource ownership.

A **display representation** is prepared for a particular rendering/color path, scale need, or destination and may be cached under an explicit budget. It must not silently become the only surviving copy of source semantics.

Keeping these concepts distinct allows later monitor-aware transforms, alternative sampling paths, adjacent-image preload, and large-image strategies without pushing those concerns into navigation.

## Concurrency and lifetime

Long-running operations receive cancellation and/or an explicit selection generation. Native or unmanaged resources have clear owners and deterministic disposal. Latest-wins publication and resource budgets are specified in [`PERFORMANCE.md`](PERFORMANCE.md); coding rules are in [`CODING-GUIDELINES.md`](CODING-GUIDELINES.md).
