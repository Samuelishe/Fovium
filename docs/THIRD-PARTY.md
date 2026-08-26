# Third-party technology

Role: Ledger and evaluation boundary for external dependencies, repository actions/services, and borrowed assets.
Read when: Evaluating, adding, upgrading, replacing, or removing a library, tool, native component, action, service, or asset.
Authoritative for: What third-party material has been introduced and the minimum provenance fields for future additions.
Not authoritative for: Final technical choices still under evaluation, subsystem design, or package lock/version data once build manifests exist.

## Introduced

Exact direct versions are authoritative in the linked project manifests. REPO-R1 introduced the test packages; R0 introduced the Avalonia/Skia line for the disposable RenderProbe; R1 adopts the same coherent line in the production `Fovium` project. Restore verified the combined graph on .NET 10.

| Name | Author / organization | License | Purpose | Official source | Version | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Microsoft.NET.Test.Sdk | Microsoft | MIT | MSBuild targets and test host integration | [NuGet](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.9.0) | `18.9.0` | Direct test-project reference; VSTest platform |
| xunit | xUnit.net project | Apache-2.0 | Test framework, assertions, and analyzers | [NuGet](https://www.nuget.org/packages/xunit/2.9.3) | `2.9.3` | Direct test-project reference; xUnit v2 |
| xunit.runner.visualstudio | xUnit.net project | Apache-2.0 | VSTest discovery and execution adapter | [NuGet](https://www.nuget.org/packages/xunit.runner.visualstudio/4.0.0) | `4.0.0` | Direct private test asset; supports xUnit v2 |
| Avalonia | AvaloniaUI | MIT | Core UI/control APIs for RenderProbe and the production viewer | [NuGet](https://www.nuget.org/packages/Avalonia/12.1.1) | `12.1.1` | Direct RenderProbe reference in R0; direct production reference in R1 |
| Avalonia.Desktop | AvaloniaUI | MIT | Windows/Linux/macOS desktop platform bundle | [NuGet](https://www.nuget.org/packages/Avalonia.Desktop/12.1.1) | `12.1.1` | Direct experimental and production reference; introduces platform/native runtime assets transitively; production adoption R1 |
| Avalonia.Skia | AvaloniaUI | MIT | Avalonia Skia renderer and isolated direct-canvas lease | [NuGet](https://www.nuget.org/packages/Avalonia.Skia/12.1.1) | `12.1.1` | Direct experimental and production reference; requires SkiaSharp `>= 3.119.4`; production adoption R1 |
| Avalonia.Themes.Fluent | AvaloniaUI | MIT | Basic RenderProbe controls and R1 Dark secondary UI | [NuGet](https://www.nuget.org/packages/Avalonia.Themes.Fluent/12.1.1) | `12.1.1` | Direct experimental and production reference; not the final Fovium visual system; production adoption R1 |
| SkiaSharp | Microsoft / SkiaSharp contributors | MIT | Controlled `SKCodec` decode and explicit photographic sampling | [NuGet](https://www.nuget.org/packages/SkiaSharp/3.119.4) | `3.119.4` | Direct experimental and production reference pinned to Avalonia.Skia's coherent graph; production adoption R1 |
| BitMiracle.LibTiff.NET | Bit Miracle / LibTiff.NET contributors | BSD-3-Clause (project “New BSD” license) | Focused classic-TIFF probe, tag/layout validation, decompression, and strip/tile access behind Fovium's TIFF backend | [NuGet](https://www.nuget.org/packages/BitMiracle.LibTiff.NET/2.4.660), [project](https://github.com/BitMiracle/libtiff.net), [license](https://bitmiracle.github.io/libtiff.net/help/articles/license.html) | `2.4.660` | Direct fully managed .NET Standard 2.0 production reference introduced in R7-B; selected over much larger Magick.NET/libvips/ImageSharp stacks for the bounded TIFF-only scope; Fovium performs its own strict 8-bit/product validation and installs one Debug-only library-wide diagnostic handler instead of the default stderr handler |
| MetadataExtractor | Drew Noakes / contributors | Apache-2.0 | Focused read-only EXIF/IPTC/XMP metadata parsing behind a Fovium adapter | [NuGet](https://www.nuget.org/packages/MetadataExtractor/2.9.3), [project](https://github.com/drewnoakes/metadata-extractor-dotnet) | `2.9.3` | Direct managed production reference introduced in R6-A; selected over a hand-written EXIF parser and a complete second ImageSharp imaging pipeline |
| XmpCore | Drew Noakes / Adobe XMP SDK lineage | BSD (Adobe XMP SDK) | XMP support required transitively by MetadataExtractor | [NuGet](https://www.nuget.org/packages/XmpCore/6.1.10.1), [project](https://github.com/drewnoakes/xmp-core-dotnet) | `6.1.10.1` | Managed transitive dependency introduced with MetadataExtractor in R6-A; no native assets |
| libheif | struktur AG / libheif contributors | LGPL-3.0-or-later library | Production decode-only HEIF/AVIF native runtime | [Repository](https://github.com/strukturag/libheif), [release](https://github.com/strukturag/libheif/releases/tag/v1.23.1) | `1.23.1`, commit `2c4bbb54c2738d4a5efbbe3e5fa1d5d76bb88eb0` | App-local source-built runtime introduced by R7-C-N1 and productized in R7-C. A small project-owned direct binding is used; no managed wrapper or native-runtime NuGet package is added. Plugin loading, all encoders, examples/CLIs, and unrelated codecs are disabled; exact source/binary hashes and per-RID inventory are owned by `eng/native/libheif/versions.json` and artifact manifests |
| libde265 | struktur AG / libde265 contributors | LGPL-3.0-or-later | Production HEVC decoder behind libheif | [Repository](https://github.com/strukturag/libde265), [release](https://github.com/strukturag/libde265/releases/tag/v1.1.1) | `1.1.1`, commit `4dd701fffac01632ffd5cabc5ef10deb56accba1` | App-local shared decoder library; encoder and CLI targets are disabled; loaded only from the Fovium runtime bundle |
| dav1d | VideoLAN / dav1d contributors | BSD-2-Clause | Production AV1 decoder behind libheif | [Official source](https://code.videolan.org/videolan/dav1d), [release archive](https://download.videolan.org/pub/videolan/dav1d/1.5.4/) | `1.5.4`, commit `191bdda98ec3c68137754dc97da1db34043d7cd4` | App-local shared decoder library; tools/examples/tests are disabled; loaded only from the Fovium runtime bundle |
| color-names dataset | David Aerne / meodai | MIT | Embedded offline nearest-color name anchors | [Repository](https://github.com/meodai/color-names), [pinned source](https://raw.githubusercontent.com/meodai/color-names/cc5fc08de437ea2522d32f751cecb4aa1e96f8e3/src/colornames.csv) | commit `cc5fc08de437ea2522d32f751cecb4aa1e96f8e3` | R8-A ships a deterministic 1,800-entry derivative from 31,915 source rows (4,959 upstream `good name` rows). Fovium limits length/characters, removes numbered and bounded identity/brand/novelty terms, protects 17 basic anchors, and selects color-space coverage in OKLab. Source SHA-256, generated SHA-256, exact rules, and MIT text are owned by `resources/color-names/README.md`; no runtime package or network access is added |
| libavif command-line tools | Alliance for Open Media / libavif contributors | BSD-2-Clause | One-time generation of project-authored AVIF test fixtures | [Repository](https://github.com/AOMediaCodec/libavif), [release](https://github.com/AOMediaCodec/libavif/releases/tag/v1.4.2) | `1.4.2`, AOM `3.14.1` in official Windows artifact | Fixture-generation tool only under ignored research artifacts; exact artifact URL, SHA-256, synthetic pattern, options, and output hashes are recorded in `eng/native/libheif/fixtures/README.md`; no executable or library is shipped or required by tests |
| Meson | Meson contributors | Apache-2.0 | Reproducible dav1d native build tooling | [PyPI](https://pypi.org/project/meson/1.12.0/), [repository](https://github.com/mesonbuild/meson) | `1.12.0` | Build-only Python environment under ignored artifacts; not shipped in the native runtime or product |
| Ninja | Ninja contributors / PyPI package maintainers | Apache-2.0 | Reproducible native build executor | [PyPI](https://pypi.org/project/ninja/1.13.0/), [repository](https://github.com/ninja-build/ninja) | `1.13.0` | Build-only Python environment under ignored artifacts; not shipped in the native runtime or product |

No separate coverage collector is introduced. Other transitive package resolution remains NuGet/MSBuild data rather than a manually duplicated version ledger here.

## Planned / under evaluation

The following are evaluated candidates, not installed dependencies or promises. Stable versions were checked from official release/package metadata on 2026-08-24; they are research snapshots, not version pins.

| Candidate | Checked stable / license | Platforms and capabilities | Complexity and Fovium fit |
| --- | --- | --- | --- |
| C# / .NET | .NET 10 GA / MIT | Cross-platform managed runtime | Introduced as the repository runtime; packaging still needs platform validation |
| Avalonia bitmap pipeline | Avalonia `12.1.1` / MIT | Cross-platform basic JPEG/PNG bitmap decode and explicit coarse interpolation | Introduced for R0 comparison; insufficient source-profile/orientation visibility for the primary decoder |
| SixLabors.ImageSharp | `4.1.1` / Six Labors Split License 1.0 | Fully managed, cross-platform common formats, metadata/ICC access, many resamplers | Attractive metadata/profile candidate; license obligations and photographic decode behavior require review |
| Magick.NET-Q8-AnyCPU / ImageMagick | `14.16.0` / Apache-2.0 wrapper/package | Broad cross-platform formats, profiles, transforms, filters/resampling | Approximately 95 MB AnyCPU package and substantial native surface; plausible broad-format backend, not an initial default |
| NetVips / libvips | NetVips `3.2.0` MIT; libvips `8.18.5` LGPL-2.1 | Cross-platform demand-driven imaging, broad formats, ICC through native stack, shrink/tile-friendly processing | Strong future huge-image candidate; native deployment and project-owned lifetime/error mapping need proof |
| Little CMS | current stable `2.19.1` / MIT; R8-B-P1 reference tag `lcms2.19` | Native cross-platform ICC v2/v4 color transforms | Developer-only R8-B-P1 reference build matched Skia 3.119.4 exactly on six matrix/TRC patches and accepted/transformed a valid ICC v4 LUT profile that Skia rejected. No library, binary, package, or runtime supply chain is introduced; production adoption requires explicit owner approval and a reproducible native-runtime stage |
| Other specialized/native codecs | No version selected | Format-specific coverage such as JPEG XL, JPEG 2000, OpenEXR, PSD previews, or RAW previews | Add only behind project-owned imaging contracts when a concrete format demands it |

Consult the affected technical owner before choosing a candidate: rendering in [`RENDERING.md`](RENDERING.md), imaging in [`IMAGING-PIPELINE.md`](IMAGING-PIPELINE.md), and color in [`COLOR-MANAGEMENT.md`](COLOR-MANAGEMENT.md).

## Assets / borrowed material

No third-party photograph, icon, font, logo, or downloaded test corpus is shipped. R8-A's only new shipped data is the attributed MIT-licensed derived color-name catalog recorded above and beside the resource. R7-C's tracked HEIF/AVIF fixtures contain only project-authored synthetic patterns; their source method, one-time official encoder tooling, exact options, hashes, and no-photograph statement are recorded beside them. Downloaded public samples and generated performance/malformed/regression material remain ignored under `resources/test-images/`. Future shipped or test assets must follow [`resources/README.md`](../resources/README.md) and record name, author/source, license, purpose, official source, modifications, and introduced stage here before commit.

## Repository services and actions

| Name | Author / organization | License / terms | Purpose | Official source | Version / action line | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GitHub Actions | GitHub | Hosted service; GitHub Terms apply | Hosted restore/build/test and native-runtime/product workflows | [Documentation](https://docs.github.com/actions) | Service | R7-C-N1/N1-F1 and the extended R7-C production backend are owner-accepted green on `win-x64`, `linux-x64`, and `osx-arm64`; R8-A's normal Windows/Linux/macOS managed workflow is owner-accepted green |
| actions/checkout | GitHub | MIT | Checkout repository contents in CI | [Repository](https://github.com/actions/checkout) | `actions/checkout@v7` | Major action line used by `ci.yml` and `native-libheif.yml` |
| actions/setup-dotnet | GitHub | MIT | Install .NET 10 GA in CI | [Repository](https://github.com/actions/setup-dotnet) | `actions/setup-dotnet@v6` | Major action line used by `ci.yml` |
| actions/upload-artifact | GitHub | MIT | Upload per-RID native runtime bundles and evidence | [Repository](https://github.com/actions/upload-artifact) | `actions/upload-artifact@v6` | Used only by `native-libheif.yml`; artifacts are build evidence, not a product release |

## Future entry fields

Every future introduced dependency, action, service, or asset entry must record:

- name and exact package/component/action identity;
- purpose and owning subsystem;
- author or vendor;
- license, terms, and any redistribution obligations;
- official source URL;
- introduced stage and decision reference;
- managed/native/runtime/service/asset form and supported platforms;
- version authority (normally the relevant project, workflow, or central package manifest).

Record removal or replacement rather than deleting historical provenance. Do not approve a dependency solely because it is popular; capture evidence relevant to Fovium's quality, resource, color, and deployment constraints.
