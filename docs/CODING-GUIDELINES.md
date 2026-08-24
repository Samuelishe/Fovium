# Coding guidelines

Role: Implementation conventions for future Fovium code.
Read when: Writing, reviewing, or testing C# and related application code.
Authoritative for: C# style principles, async/cancellation rules, resource disposal, error handling, test expectations, and pragmatic UI structure.
Not authoritative for: Product behavior, subsystem design, selected packages, or project status.

## General

- Use the current validated modern C# language level and enable nullable reference types.
- Prefer clear domain names, small cohesive types, and explicit state transitions over abbreviations or framework jargon.
- Make invalid states hard to represent with appropriate types; avoid loosely typed dictionaries and sentinel values for core state.
- Keep changes small and coherent. Do not mix opportunistic rewrites with a focused feature or fix.
- Avoid architecture ceremony, speculative abstractions, and patterns whose only justification is possible future use.
- Do not use Service Locator or mutable global application state.

## Async and concurrency

- Keep disk, probe, decode, color conversion, display preparation, and other expensive work off the UI thread.
- Propagate `CancellationToken` through operations that can use it, while preserving generation/ownership checks for correctness.
- Do not call `.Result` or `.Wait()` on the UI thread.
- Make thread-affinity and publication points visible in code.
- Prefer structured task ownership; do not create unobserved fire-and-forget work.

## Errors and resources

- Catch exceptions only where code can add context, translate to a meaningful domain result, recover, or guarantee cleanup.
- Do not use broad `catch` blocks to hide failures. Never treat out-of-memory risk as ordinary control flow after an unsafe decode begins.
- Dispose streams, bitmaps, native handles, color transforms, and other owned resources deterministically.
- Document ownership when a library type has non-obvious native lifetime or thread constraints.
- Log or diagnose enough identity and stage information to distinguish probe, decode, transform, preparation, and publication failures without leaking user data unnecessarily.

## UI structure

MVVM is not a goal by itself. Use ViewModels/coordinators for presentation state and orchestration where useful. Genuine pointer interaction, control lifecycle, and rendering integration may live in focused code-behind or a custom control. Do not accumulate unrelated behavior in `MainWindow` or a single God ViewModel.

Keep viewport math and other nontrivial pure logic independent of Avalonia objects when practical. Platform APIs remain behind narrow adapters at the edge.

## Tests and verification

Write focused tests for nontrivial pure logic, including viewport transforms, Fit/100% calculations, cursor anchoring, orientation, generation/latest-wins rules, resource estimates, and cache policy. Add integration or visual evidence where framework, native, DPI, color, or renderer behavior cannot be proved by a unit test.

The initial automated framework is xUnit. Tests must use isolated project-generated data, deterministic assertions, and temporary paths rather than developer-machine state. Do not add coverage tooling or abstraction seams without a concrete need. Repository diagnostics must preserve ordinal output ordering, repository-relative paths, and explicit skipped-input behavior.

Verification is proportional: run the smallest reliable checks that cover the changed behavior, then broaden when shared contracts or risky native/resource behavior are affected. Do not claim visual, timing, or platform correctness from unit tests alone.
