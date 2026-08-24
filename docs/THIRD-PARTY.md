# Third-party technology

Role: Ledger and evaluation boundary for external dependencies, repository actions/services, and borrowed assets.
Read when: Evaluating, adding, upgrading, replacing, or removing a library, tool, native component, action, service, or asset.
Authoritative for: What third-party material has been introduced and the minimum provenance fields for future additions.
Not authoritative for: Final technical choices still under evaluation, subsystem design, or package lock/version data once build manifests exist.

## Introduced

REPO-R1 introduces only test infrastructure dependencies. Exact direct package versions are authoritative in [`Fovium.Tests.csproj`](../Fovium.Tests/Fovium.Tests.csproj); restore has verified this combination on .NET 10. No imaging, rendering, UI, or color dependency is introduced.

| Name | Author / organization | License | Purpose | Official source | Version | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Microsoft.NET.Test.Sdk | Microsoft | MIT | MSBuild targets and test host integration | [NuGet](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.9.0) | `18.9.0` | Direct test-project reference; VSTest platform |
| xunit | xUnit.net project | Apache-2.0 | Test framework, assertions, and analyzers | [NuGet](https://www.nuget.org/packages/xunit/2.9.3) | `2.9.3` | Direct test-project reference; xUnit v2 |
| xunit.runner.visualstudio | xUnit.net project | Apache-2.0 | VSTest discovery and execution adapter | [NuGet](https://www.nuget.org/packages/xunit.runner.visualstudio/4.0.0) | `4.0.0` | Direct private test asset; supports xUnit v2 |

No separate coverage collector is introduced. Transitive package resolution remains NuGet/MSBuild data rather than a manually duplicated version ledger here.

## Planned / under evaluation

The following are candidates, not installed dependencies or promises:

| Candidate | Possible purpose | Validation needed |
| --- | --- | --- |
| C# / .NET 10 | Application language/runtime direction | SDK/runtime/platform availability and packaging |
| Avalonia | Cross-platform desktop UI direction | Windowing, input, DPI, multi-monitor, and renderer integration |
| Avalonia bitmap pipeline | Baseline image display candidate | Photographic quality, scaling, color-data preservation, performance |
| Skia / SkiaSharp | Rendering and color-space candidate | Pixel semantics, sampling, native lifetime, packaging |
| ImageSharp | Managed image/metadata/ICC candidate | Format behavior, precision, performance, licensing |
| Magick.NET / ImageMagick | Broad format candidate | Decode quality, resource policy, native footprint, licensing |
| NetVips / libvips | Efficient imaging/large-image candidate | Platform deployment, access patterns, color behavior |
| LittleCMS | ICC transform candidate | Monitor integration, formats, lifetime, throughput |
| libheif / libavif | Specialized codec candidates | Platform binaries, licensing, metadata/color fidelity |
| Other specialized/native codecs | Format-specific fallback | Need, maintenance, packaging, and common-contract fit |

Consult the affected technical owner before choosing a candidate: rendering in [`RENDERING.md`](RENDERING.md), imaging in [`IMAGING-PIPELINE.md`](IMAGING-PIPELINE.md), and color in [`COLOR-MANAGEMENT.md`](COLOR-MANAGEMENT.md).

## Assets / borrowed material

No external images, icons, fonts, logos, generated image dumps, or test-image corpora are introduced in REPO-R1. Future shipped or test assets must follow [`resources/README.md`](../resources/README.md) and record name, author/source, license, purpose, official source, modifications, and introduced stage here before commit.

## Repository services and actions

| Name | Author / organization | License / terms | Purpose | Official source | Version / action line | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GitHub Actions | GitHub | Hosted service; GitHub Terms apply | Hosted restore/build/test workflow | [Documentation](https://docs.github.com/actions) | Service | Workflow configured locally; no hosted run is claimed |
| actions/checkout | GitHub | MIT | Checkout repository contents in CI | [Repository](https://github.com/actions/checkout) | `actions/checkout@v7` | Major action line used by `ci.yml` |
| actions/setup-dotnet | GitHub | MIT | Install .NET 10 GA in CI | [Repository](https://github.com/actions/setup-dotnet) | `actions/setup-dotnet@v6` | Major action line used by `ci.yml` |

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
