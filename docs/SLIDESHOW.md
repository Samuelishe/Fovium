# Slideshow

Role: Canonical behavior and ownership contract for timed slideshow navigation.
Read when: Changing slideshow timing, navigation, preparation, memory, presentation ownership, or persisted slideshow preferences.
Authoritative for: Slideshow session state, timer semantics, end behavior, prepared-next ownership, and integration with Photo Presentation View.
Not authoritative for: General navigation order, decode formats, Photo Presentation geometry, or Color Management transform policy.

## Session and presentation ownership

Slideshow is one session-local navigation controller over the existing `ViewerSession` sequence. It does not scan directories or own a playlist. `viewer.toggleSlideshow` (`F5` by default), its checked context-menu item, and the live Presentation Settings checkbox all observe the same `SlideshowSession`; running state, current index, deadlines, generations, and temporary layout ownership are never serialized.

Starting keeps the actually presented photograph visible and enables Photo Presentation View when necessary. Slideshow uses Photo Presentation View as the sole presentation-geometry authority; it has no separate Fit, zoom, margin, or Matte geometry. If presentation was already enabled, explicit stop leaves it enabled. If slideshow enabled it temporarily, explicit F5, Settings, context-menu, or Esc stop returns to ordinary Fit through the accepted Photo Presentation exit path. Turning Photo Presentation off while running first stops slideshow and then applies Off.

At a natural Stop-at-end completion, automatic navigation stops and the last viable photograph remains visible in its presentation layout. Temporary ownership is transferred to the visible Photo Presentation session state instead of causing an abrupt layout snap; the user may subsequently leave presentation explicitly with F6 or its other surfaces.

## Presented-time countdown

The configured whole-second duration is the minimum time for which the current fully published photograph remains visible. The default is `5 s`, normalized to `1–60 s`. A navigation or decode request does not start the next interval. Only the authoritative `PresentedImageChanged` publication starts a fresh countdown, so decode and destination Color Management preparation never shorten a slide's display time. Changing duration while running restarts a full countdown from the currently presented photograph. Changing end behavior rebuilds only the one next decision and preserves the current deadline.

The controller uses one cancellable `Task.Delay` countdown at a time; .NET delay and `Stopwatch` elapsed measurements are monotonic with respect to wall-clock changes. There is no tick queue. If preparation exceeds the interval, the current complete frame remains visible until the next viable image is ready, publication happens atomically, and only then is another countdown created.

Manual Left/Right navigation remains available while running. Beginning a manual request cancels the old countdown/preparation generation; only publication of the latest selected photograph restarts timing and retargets preparation. Late intermediate results cannot publish or arm a timer. F5 or Esc cancels slideshow-origin pending work and cannot navigate on its own; Esc precedence is active hold, running slideshow, fullscreen, then viewer close.

## End behavior and viability

`StopAtEnd` searches forward through the existing recoverable-failure skipping path and naturally stops when no viable item follows, retaining the final image and presentation layout. `Loop` performs one bounded wrap and continues at the first viable item in natural sequence order. The current item is excluded from the candidate enumeration, so a one-image sequence—or a sequence with only one viable image—does not repeatedly decode, transform, or republish itself. Stop-at-end becomes stopped; Loop remains running but quiescent until the user changes configuration, navigates, or stops it.

Every bounded decision visits each other sequence index at most once. Unsupported, broken, missing, or resource-rejected entries retain the existing recoverable skip semantics; valid images are never skipped for cadence and no future navigation backlog is built.

## One prepared next presentation

While the current slide is visible, slideshow acquires the exact next viable image through the existing neighbor-inspection and decoded-cache authority, then may request the existing full-source managed-presentation coordinator to prepare it. There is no slideshow decode cache or second Color Management path. The viewport retains the current managed presentation while the coordinator owns at most one exact next source/destination result.

A prepared managed result is valid only for its exact decoded image identity, encoded geometry/orientation, destination monitor profile identity, and current Color Management state. Manual retargeting, stop, new sequence, or destination change cancels or clears obsolete ownership. F4 atomic publication remains authoritative: future pixels, Photo Presentation geometry, Matte/Ambient, overlays, and presented identity commit together; hidden preparation never changes Photo Info, Histogram, Color Picker, or markup authority.

Speculative admission uses estimated retained BGRA bytes. A next result is admitted only when it is at most `128 MiB` and current plus next is at most `256 MiB`. This admits measured 15 MP (`60,000,000` bytes) and 24 MP (`96,000,000` bytes) candidates, including respective current-plus-next totals of `120,000,000` and `192,000,000` bytes, while rejecting a 50 MP (`200,000,000` byte) next surface. Rejection changes latency only: normal navigation still prepares and publishes that valid image without skipping or exposing a blank/Matte-only frame.

## Persisted configuration

Schema-v2 Settings stores only:

- `SlideDurationSeconds`, default `5`, normalized to whole seconds `1–60`;
- `EndBehavior`, `StopAtEnd` by default or `Loop`.

Running state, current position, countdown state, prepared identity, diagnostic counters, and Photo Presentation ownership are session-only and always begin stopped on a new application run.
