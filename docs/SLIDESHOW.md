# Slideshow

Role: Canonical behavior and ownership contract for timed slideshow navigation.
Read when: Changing slideshow timing, navigation, preparation, memory, viewing-mode interaction, or persisted slideshow preferences.
Authoritative for: Slideshow session state, timer semantics, end behavior, prepared-next ownership, and viewing-mode independence.
Not authoritative for: General navigation order, decode formats, Photo Presentation geometry, or Color Management transform policy.

## Session and viewing-mode independence

Slideshow is one session-local navigation controller over the existing `ViewerSession` sequence. It does not scan directories, own a playlist, or own a viewer layout mode. `viewer.toggleSlideshow` (`F5` by default), its checked context-menu item, and the live Presentation Settings checkbox all observe the same `SlideshowSession`; running state, current index, deadlines, and generations are never serialized.

Starting and stopping keep the actually presented photograph and current viewing mode unchanged. Slideshow never owns the viewer layout mode: it advances the existing viewer using whichever Normal Viewer image-change policy or Photo Presentation state the user currently selected. In Normal Viewer, automatic navigation resolves `KeepCurrentScale` or `FitEachImage` through the same view-transfer owner as manual navigation. When Photo Presentation is enabled, its existing geometry remains authoritative. Slideshow has no separate Fit, zoom, margin, Matte, or viewport policy.

Photo Presentation may be toggled independently through F6, its context menu, or its live Settings control while slideshow keeps running. A mode or Normal Viewer policy change does not restart the slide countdown and does not invalidate a geometry-independent prepared managed source. At natural Stop-at-end completion, automatic navigation stops and the last viable photograph remains visible in whichever viewing mode is then active.

## Presented-time countdown

The configured whole-second duration is the minimum time for which the current fully published photograph remains visible. The default is `5 s`, normalized to `1–60 s`. A navigation or decode request does not start the next interval. Only the authoritative `PresentedImageChanged` publication starts a fresh countdown, so decode and destination Color Management preparation never shorten a slide's display time. Changing duration while running restarts a full countdown from the currently presented photograph. Changing end behavior rebuilds only the one next decision and preserves the current deadline.

The controller uses one cancellable `Task.Delay` countdown at a time; .NET delay and `Stopwatch` elapsed measurements are monotonic with respect to wall-clock changes. There is no tick queue. If preparation exceeds the interval, the current complete frame remains visible until the next viable image is ready, publication happens atomically, and only then is another countdown created.

Manual Left/Right navigation remains available while running. Beginning a manual request cancels the old countdown/preparation generation; only publication of the latest selected photograph restarts timing and retargets preparation. Late intermediate results cannot publish or arm a timer. F5 or Esc cancels slideshow-origin pending work and cannot navigate on its own; Esc precedence is active hold, running slideshow, fullscreen, then viewer close.

## End behavior and viability

`StopAtEnd` searches forward through the existing recoverable-failure skipping path and naturally stops when no viable item follows, retaining the final image and current viewing mode. `Loop` performs one bounded wrap and continues at the first viable item in natural sequence order without changing that mode. The current item is excluded from the candidate enumeration, so a one-image sequence—or a sequence with only one viable image—does not repeatedly decode, transform, or republish itself. Stop-at-end becomes stopped; Loop remains running but quiescent until the user changes configuration, navigates, or stops it.

Every bounded decision visits each other sequence index at most once. Unsupported, broken, missing, or resource-rejected entries retain the existing recoverable skip semantics; valid images are never skipped for cadence and no future navigation backlog is built.

## One prepared next presentation

While the current slide is visible, slideshow acquires the exact next viable image through the existing neighbor-inspection and decoded-cache authority, then may request the existing full-source managed-presentation coordinator to prepare it. There is no slideshow decode cache or second Color Management path. The viewport retains the current managed presentation while the coordinator owns at most one exact next source/destination result.

A prepared managed result is valid only for its exact decoded image identity, encoded geometry/orientation, destination monitor profile identity, and current Color Management state. Manual retargeting, stop, new sequence, or destination change cancels or clears obsolete ownership; geometry-only mode, policy, resize, or fullscreen changes do not. F4 atomic publication remains authoritative: future pixels, geometry for the active viewer mode, Matte/Ambient, overlays, and presented identity commit together; hidden preparation never changes Photo Info, Histogram, Color Picker, or markup authority.

Speculative admission uses estimated retained BGRA bytes. A next result is admitted only when it is at most `128 MiB` and current plus next is at most `256 MiB`. This admits measured 15 MP (`60,000,000` bytes) and 24 MP (`96,000,000` bytes) candidates, including respective current-plus-next totals of `120,000,000` and `192,000,000` bytes, while rejecting a 50 MP (`200,000,000` byte) next surface. Rejection changes latency only: normal navigation still prepares and publishes that valid image without skipping or exposing a blank/Matte-only frame.

## Persisted configuration

Schema-v2 Settings stores only:

- `SlideDurationSeconds`, default `5`, normalized to whole seconds `1–60`;
- `EndBehavior`, `StopAtEnd` by default or `Loop`.

Running state, current position, countdown state, prepared identity, and diagnostic counters are session-only and always begin stopped on a new application run.
