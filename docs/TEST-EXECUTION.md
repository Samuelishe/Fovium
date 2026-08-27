# Test execution

Role: Minimal command guide and verification contract for Fovium tests.
Read when: Running, filtering, adding, or interpreting automated tests.
Authoritative for: Normal local test commands, focused versus full-suite execution, Release verification, and CI test expectations.
Not authoritative for: Test design details, coverage policy, UI/rendering acceptance, or current CI status.

## Commands

Run the full suite in the default configuration:

```powershell
dotnet test Fovium.sln
```

Run the same verification used by CI after a Release build:

```powershell
dotnet restore Fovium.sln
dotnet build Fovium.sln -c Release --no-restore
dotnet test Fovium.sln -c Release --no-build
```

Run only the current ProjectStats tests when iterating on repository tooling:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.ProjectStats"
```

Run only render-independent R0 logic tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.RenderProbe"
```

Run the isolated R8-B-P1 profile-validation, monitor-selection, transform, and source-domain invariance tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --filter "FullyQualifiedName~Fovium.Tests.ColorManagementProbe"
```

Run Stage settings, geometry, preparation, cache/lifetime, and offscreen composition tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Stage"
```

The Stage suite includes R5-P1/R5-P2 synchronization evidence that current Ambient preparation requires no adjacent-progress signal, cached photo+Ambient installs as one viewport state, each ready neighbor can trigger progressive preparation, mismatched Ambient rasterizes as Black rather than the wrong Stage, and obsolete current work cannot publish over the latest selection. R5-P3 Loading tests model 30 distinct transitions through a five-resource cache, prove speculative preload continues through repeated saturation, verify admission includes reclaimable LRU capacity but excludes protected current, retain lease/disposal safety, and reject resources larger than the whole cache. Render diagnostics separately distinguish viewport/custom-draw/Skia-lease boundaries. Owner visual acceptance establishes the normal 3–4 photo/second envelope; deliberately faster 5–6+ photo/second behavior remains a documented stress limitation rather than a unit-test timing threshold.

Run configurable-command, gesture-normalization, and command-execution tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Input"
```

Run R4 hold lifecycle, inspection acquisition, viewport transfer/restoration, and temporary Stage tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Input|FullyQualifiedName~Fovium.Tests.Loading.ViewerSessionTests|FullyQualifiedName~Fovium.Tests.Rendering.ViewportModelTests|FullyQualifiedName~Fovium.Tests.Viewer"
```

Run R5 through R5-F2 presenter history, oriented transform, partial-eraser/opacity raster, constraint geometry, settings, input, and inspection tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Presentation|FullyQualifiedName~Fovium.Tests.Settings|FullyQualifiedName~Fovium.Tests.Input|FullyQualifiedName~Fovium.Tests.Viewer"
```

Run the production logic and boundary tests while iterating:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Application|FullyQualifiedName~Fovium.Tests.Navigation|FullyQualifiedName~Fovium.Tests.Loading|FullyQualifiedName~Fovium.Tests.Imaging|FullyQualifiedName~Fovium.Tests.Input|FullyQualifiedName~Fovium.Tests.Rendering|FullyQualifiedName~Fovium.Tests.Viewer|FullyQualifiedName~Fovium.Tests.Settings|FullyQualifiedName~Fovium.Tests.Localization|FullyQualifiedName~Fovium.Tests.Stage|FullyQualifiedName~Fovium.Tests.Versioning"
```

Run format capability, discovery, static WebP, bounded TIFF, HEIF/AVIF, Photo Info, Histogram, and Stage integration tests:

```powershell
dotnet test Fovium.Tests/Fovium.Tests.csproj --filter "FullyQualifiedName~Fovium.Tests.Imaging|FullyQualifiedName~Fovium.Tests.Navigation|FullyQualifiedName~Fovium.Tests.Metadata|FullyQualifiedName~Fovium.Tests.Histogram|FullyQualifiedName~Fovium.Tests.Stage"
```

## Scope

Focused tests are preferred during implementation; run the full solution before handoff when shared tooling or project configuration changes. The xUnit suite covers repository tooling and retained R0 logic plus production activation, navigation, decoding, viewport/view-policy transfer, settings persistence, loading ownership, cache, memory policy, localization, Stage geometry/preparation/publication/lifetime, DPI-aware Matte geometry and offscreen alpha composition, version metadata, and native render-lease lifetime.

UI interaction, shortcut capture/conflict dialogs, rendering quality, runtime DPI, pixel alignment, color, native lifetime, and platform behavior require bounded integration, visual, and manual smoke evidence; passing pure tests cannot prove those properties. R4 adds deterministic hold/inspection coverage. R5 adds deterministic coverage for highlight/settings toggles, all four initial markup tools, source-space transforms, image/session identity, no-file-write semantics, panel lifecycle, Skia clipping, H/P migration, and Peek/Blink overlay selection. R5-F1 adds history-cursor and per-image isolation tests; real raster assertions for partial Line/Brush/Rectangle/Arrow erasure, chronology, draft cancellation, and photograph protection; explicit history/session limit tests; Arrow thick-stroke regression coverage; and history-command migration/reset/execution coverage. R5-F2 adds pure multi-quadrant 45-degree/square/circle constraint tests, live constrained/freehand Brush draft transitions, immutable opacity capture, Ellipse/history/clipping/partial-erase raster evidence, translucent source-over and full erasure checks, dock-visible style-command gating, OEM-bracket adapter checks, and exact-pair shortcut evolution/customization tests. R5-F3 adds scope precedence/cross-scope conflict tests, typed group coverage, effective-tooltip/menu-state models, shared hold routing, Hand/history isolation, 128 px raster regression, cursor state and physical-DPI geometry matrices, and normalized floating placement/settings round trips. R5-F3-P1 adds exact routing tests for pointer/draft/dock/viewport/Stage dirtiness, focused session-notification evidence, transform-only pointer/dock geometry, compositor photo-isolation configuration, and opt-in counter separation; actual smoothness remains a Release manual observation. R5-P2 adds atomic viewport-state, progressive neighbor, direction-priority, actual Stage-draw fallback counters, and mismatched-identity raster coverage. R5-P3 adds reclaim-aware saturation, protected-current/LRU lifetime, and higher render-pipeline counter coverage; perceptual acceptance remains owner-corpus review rather than a unit-test claim.

R6-A adds a self-authored runtime JPEG/EXIF APP1 fixture and adapter mapping tests for camera, lens, focal length, aperture, shutter rational, ISO, and unspecified capture time; no-EXIF/malformed/partial recovery; pure sparse/localized formatting; immediate oriented/file base data; lazy hidden-panel behavior; latest-wins asynchronous publication; bounded LRU/reparse avoidance/new-sequence reset; additive `I` conflict preservation; normalized bottom-left placement; and canonical/Blink/Peek presented-identity behavior. Private owner photographs remain manual-only evidence and are never test fixtures.

R6-B adds deterministic BGRA channel/alpha fixtures, transparent exclusion and unpremultiplication assertions, exact-versus-bounded deterministic sampling, retained-pixel lifetime, shared plot normalization, 128-entry LRU, hidden/toggle/cancel/latest-wins/new-sequence coordinator behavior, Blink-like identity swap/cache restoration, Peek stability, additive `G` conflict preservation, bottom-right placement, EN/RU chrome, and a non-threshold 24 MP exact/sampled engineering timing smoke. Private photographs remain manual-only visual/performance evidence.

R7-A adds exact capability-table/extension/Skia-mapping invariants; mixed JPEG/PNG/WebP discovery and case-insensitive extension checks; generated lossy/lossless/alpha/static/animated/oriented WebP containers; content-extension mismatch; malformed/resource-limit/static-frame policy; retained Photo Info/metadata/Histogram/Ambient/Matte integration; and non-threshold generated WebP probe/decode/preparation evidence. No private image is a test fixture.

R7-B adds an independent minimal uncompressed classic-TIFF byte fixture plus focused library-generated evidence. Tests cover little/big endian, strips/tiles, None/LZW/Deflate/PackBits, grayscale polarity, associated/unassociated alpha, all eight orientation tags, exact BGRA pixels, ICC-state truth, content-extension mismatch, BigTIFF/multipage/high-bit/floating/specialist/unknown-extra rejection, corrupt/resource-bomb recovery, shared cross-backend concurrency, mixed TIFF/Skia parallel stress, and generic Photo Info/MetadataExtractor/Histogram/Ambient/Matte integration. Private/local TIFF photographs remain manual-only evidence.

R7-C adds tracked project-authored 8-bit HEIF/AVIF RGB, AVIF alpha, rotation, mirror, 10-bit, PQ, HLG, sequence, and malformed fixtures. Tests exercise actual production interop and app-local loading; codec-derived HEIF/AVIF identity despite misleading extensions; `.heic/.heif/.hif/.avif` discovery and case; representative pixels, alpha 0/partial/255 and single premultiplication; oriented pixels/dimensions with `Normal` descriptor orientation; encoded 10-bit/PQ/HLG/sequence rejection; pre-decode resource admission; corrupt containment; shared two-slot concurrency; missing-runtime isolation; and generic Photo Info/Histogram/Ambient use of the same `DecodedImage`. Set `FOVIUM_REQUIRE_HEIF_TEST_RUNTIME=1` to make absence of the materialized current-RID bundle a test failure rather than a local skip.

CI restores, builds, and tests on Windows, Linux, and macOS. The accepted R7-B commit `d5de440` completed all three hosted restore-build-test jobs successfully, including the managed TIFF suite. This proves the solution/test contract and cross-platform managed decoder execution, not manual Avalonia/Skia viewer behavior on Linux/macOS.

R7-C-N1 and R7-C-N1-F1 are complete and owner-accepted at `c4dba80bd23534f372ae09f9285c0e1c5991d5e3`. The path-filtered `native-libheif.yml` matrix builds pinned source, packages and audits the decode-only runtime, loads exact app-local libheif, verifies HEVC/AV1 decoder presence and encoder absence, and decodes tracked project-authored HEIF/AVIF fixtures. Its mandatory jobs are `win-x64`, `linux-x64`, and `osx-arm64`; no platform skip is accepted.

R7-C extends those jobs to materialize the built bundle, restore/build Fovium, and run the actual production `HeifImageDecodeBackend` tests with runtime presence mandatory. Normal CI remains the fast managed matrix. Cross-platform product acceptance requires both matrices green after owner push; local Windows success alone is not that hosted claim.

R8-A adds deterministic in-memory tests for standard OKLab vectors; exact 1,800-entry embedded catalog integrity/basic anchors; exact/near/tie name matching; ten-item duplicate-preserving FIFO; uppercase RGB(A)/HEX; alpha 255/128/1/0 and one unpremultiplication; reference-sRGB direct/1×1 Skia/Approximate source states; containing-source-cell geometry with exclusive edges and render scaling 1.00/1.25/1.50/2.00; all eight EXIF orientations; picker/temporary-Hand/markup precedence; render-layer isolation; additive `K`; placement/settings non-persistence; menu state; and EN/RU chrome. The observational performance smoke reports initialization and 1,000 clicks without defining an SLA. Windows Release screenshot smoke covers empty, fixed sample, exactly ten rows after eleven duplicate clicks, hide/reopen retention, JPEG/HEIF/WebP/AVIF/TIFF/HIF/PNG navigation, fullscreen, Peek/Blink input, temporary Hand, and picker precedence while the Markup panel is visible. Real cross-platform/fractional-DPI pointer feel remains manual evidence.

R8-B-P1 adds deterministic in-memory tests for bounded ICC display-profile validation, content identity, active-monitor largest-intersection/tie behavior, typed fallback precedence, complete transform-key equality, Skia destination/alpha behavior, and source-versus-destination ownership. The final domain-independence test proves that two destination transforms differ while R8-A reference-sRGB Picker output and source-domain Histogram bins remain unchanged. Platform APIs and real display-profile availability remain probe/manual evidence rather than skipped ordinary unit tests.

Run a clean native build locally with:

```powershell
pwsh eng/native/libheif/build.ps1 -Rid win-x64
```

or on a matching Unix host:

```bash
bash eng/native/libheif/build.sh linux-x64
bash eng/native/libheif/build.sh osx-arm64
```

The scripts fail on host/RID mismatch, archive hash mismatch, non-local libheif loading, missing HEVC/AV1 decoders, present HEVC/AV1 encoders, forbidden codec/developer-path dependencies, or fixture decode failure. macOS additionally requires exact app-local install names, `@loader_path`, the configured deployment target, and the RID architecture. Smoke runs with the original build prefix renamed out of reach. Per-RID `manifest.json`, `dependency-audit.txt`, and `smoke-report.txt` are the detailed evidence owners.

## R8-B-W1 Monitor Color Management

Run production color tests with the accepted current-RID artifact materialized:

```powershell
$env:FOVIUM_REQUIRE_LCMS_TEST_RUNTIME = '1'
dotnet test Fovium.Tests/Fovium.Tests.csproj -c Release --filter "FullyQualifiedName~Fovium.Tests.ColorManagement"
```

The project-authored fixtures under `eng/native/lcms2/fixtures/` are generated with pinned Little CMS 2.19 and prove both a matrix/TRC RGB display destination and a real `BToA0` CLUT RGB display destination. Tests verify app-local path/version, exact BGRA patches, relative colorimetric/BPC-off policy, alpha 255/128/1/0, untagged final device pixels, Display-P3→full-source reference-sRGB normalization, canonical-source immutability, 16 MiB admission, monitor geometry/ties, output-state identity, source fallback, same-size managed-source ownership, geometry-free source/destination keys, one CMM operation across 50 Fit/zoom/pan/resize/100% frames, shared EXIF orientation and center geometry, source/destination latest-wins races, stable exact 100%, shutdown during native work, and absence of presented-source events on destination change. The require variable turns missing runtime into failure; ordinary CI may exercise fake/pure paths without building native code.

The separate `native-lcms2.yml` matrix builds/audits/smokes `win-x64`, `linux-x64`, and `osx-arm64`, materializes each bundle into Fovium output, verifies final hashes, and runs the actual production interop tests. This is cross-platform engine evidence, not physical-monitor support outside Windows. Local Windows smoke additionally records anonymized real HWND/profile state with `FOVIUM_COLOR_DIAGNOSTICS=1`; full profile paths are forbidden.
