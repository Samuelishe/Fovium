# R7-C HEIF/AVIF backend probe

Role: Retained feasibility evidence for the gated R7-C native decoder investigation.
Read when: Re-evaluating HEIF/HEIC or AVIF decoding and native runtime packaging.
Authoritative for: The R7-C candidate observations and productization blocker recorded here.
Not authoritative for: Current supported-format claims, dependency provenance, or an accepted future implementation.

## Question

Can Fovium add one focused, decode-only HEIF/AVIF backend whose native runtime is application-owned and proven on Windows x64, Linux x64, and macOS arm64, without silently reducing high-bit-depth/HDR sources or adding unnecessary encoder components?

## Controlled inputs

The local ignored corpus supplied one static AVIF and one static HEIF. Only anonymized technical facts are retained here:

| Input | Container brand | Dimensions | Source depth | Alpha | Top-level images |
| --- | --- | ---: | ---: | --- | ---: |
| AVIF | `avif` | 1920 x 1080 | 8 bit | No | 1 |
| HEIF | `heic` | 8736 x 5856 | 8 bit | No | 1 |

The source files remain ignored and are not product fixtures.

R7-C-N1 additionally introduces two tiny project-authored tracked smoke fixtures: a `16 x 12` asymmetric RGB pattern encoded once as 8-bit HEIF and AVIF. Their generator provenance and immutable hashes are recorded beside the fixtures under [`../../eng/native/libheif/fixtures/`](../../eng/native/libheif/fixtures/). They contain no personal photograph or metadata.

## Results

| Candidate | Decode evidence | Packaging evidence | Gate result |
| --- | --- | --- | --- |
| Existing SkiaSharp 3.119.4 | `SKCodec.Create` returned `Unimplemented` for both controlled files | Already shipped | Cannot decode either target format |
| LibHeifSharp 3.2.0 + LibHeif.Native.Runtime 1.20.2 | App-local libheif 1.20.2 decoded both controlled files on Windows; HEVC and AV1 decoders were present | Runtime package contains `win-x64` and `linux-x64`, but no macOS native asset | Fails mandatory macOS arm64 packaging |
| tryAGI.HeifSharp 1.0.2 native bundle | Bundled libheif 1.21.2 could read headers but reported neither HEVC nor AV1 decoder and could not decode either file | Contains Windows/Linux/macOS assets, plus encoder/x265 and compiler-runtime baggage | Fails decode and decode-only dependency requirements |
| NetVips 3.2.0 + NetVips.Native 8.18.5 | Decoded the AVIF file; HEIF pixel decode failed because HEVC decompression was not built in | Broad all-RID imaging runtime | Fails HEIF and is materially broader than the focused boundary |
| Pure-managed HEIC candidates | HEIC-only direction; no matching AVIF backend with equivalent maturity and contract | Avoids native loading but would split the stage across unrelated implementations | Does not meet the one focused HEIF/AVIF backend goal |

The focused LibHeifSharp API exposed the product-relevant primary image, source bit depth, alpha/depth presence, ICC/NCLX data, transforms, and interleaved RGBA decode. This makes the managed adapter technically plausible. It does not by itself solve native runtime packaging.

## R7-C-N1 native runtime evidence

The repository now owns a direct-source, decode-only build under [`../../eng/native/libheif/`](../../eng/native/libheif/). It does not use a system libheif, install a production NuGet reference, register a product backend, or change format discovery.

Pinned official sources:

| Component | Version | Tag commit | Release archive SHA-256 | Role / license |
| --- | --- | --- | --- | --- |
| libheif | `1.23.1` | `2c4bbb54c2738d4a5efbbe3e5fa1d5d76bb88eb0` | `0de0327f60fcd47de90d5654c6fe152232738d60d84fe084ec3e0f35e03b166a` | Container/decode integration; LGPL-3.0-or-later |
| libde265 | `1.1.1` | `4dd701fffac01632ffd5cabc5ef10deb56accba1` | `fd48a927e94ed74fc7ce8829d222b9d8599fcbfe8b6448ba66705babc56ab219` | HEVC decoder; LGPL-3.0-or-later |
| dav1d | `1.5.4` | `191bdda98ec3c68137754dc97da1db34043d7cd4` | `686616b7c69eb88d44459391ab25cac13b6647a3b288835c5784e71c1514a5c5` | AV1 decoder; BSD-2-Clause |

libde265 and dav1d are linked as built-in libheif decoder backends. Plugin loading, x265, AOM, rav1e, SVT-AV1, Kvazaar, x264, FFmpeg, OpenH264, JPEG/JPEG 2000, OpenJPH, VVC codecs, all encoders, examples, CLIs, tests, documentation, fuzzers, GDK Pixbuf, and experimental features are disabled. The packaged payload contains only libheif, libde265, dav1d, their loader-name links where the platform uses them, machine-readable manifests, dependency audits, smoke evidence, and the three license texts.

Current proof:

| RID | Build / locality | Codec inventory | Controlled decode | Status |
| --- | --- | --- | --- | --- |
| `win-x64` | Clean local and hosted MSVC build; `heif.dll`, `libde265.dll`, and `dav1d.dll` load beside one another | HEVC decoder yes; AV1 decoder yes; HEVC/AV1 encoders no | 8-bit HEIF pass; 8-bit AVIF pass | Hosted PASS for `5f938e11`; two local clean builds produced the same packaged SHA-256 |
| `linux-x64` | Clean local Ubuntu 24.04 x64 container and hosted build; `libheif.so` uses `$ORIGIN` and `ldd` resolves both codec libraries from the artifact directory | HEVC decoder yes; AV1 decoder yes; HEVC/AV1 encoders no | 8-bit HEIF pass; 8-bit AVIF pass | Hosted PASS for `5f938e11` |
| `osx-arm64` | First hosted build loaded libheif 1.23.1 and decoded both fixtures, but its dav1d install name and libheif dependency retained the absolute build prefix | HEVC decoder yes; AV1 decoder yes; HEVC/AV1 encoders no | 8-bit HEIF pass; 8-bit AVIF pass | Hosted FAIL for `5f938e11`: the strict relocation audit correctly rejected the otherwise successful decode; corrected artifact requires a new hosted run |
| `osx-x64` | The build owner accepts a real x64 macOS host | No runner/artifact evidence yet | No evidence | Preferred additional RID remains unproven |

Each artifact carries `manifest.json` with source pins, build options, final post-relocation binary hashes, toolchain versions, fixture hashes, and license inventory; `dependency-audit.txt`; and `smoke-report.txt` with the absolute loaded libheif path/version and decoder/encoder/decode results. Windows `dumpbin` and Linux `readelf`/`ldd` found no x265, unrelated codec, developer-prefix, or custom system-library dependency. Windows depends on the normal MSVC runtime; Linux depends only on normal platform C/C++ runtime libraries beyond its app-local codec set. The corrected macOS build targets macOS 14.0 consistently, rewrites the packaged dylibs to `@rpath` identities plus `@loader_path`, audits `minos` and arm64, and makes the original prefix unavailable during smoke; these corrections remain local code evidence until a new hosted run executes them.

## Decision

R7-C is not productized. Fovium remains at `0.1.0.0005`; no production package, backend, format capability, discovery extension, or support claim is added. The first hosted R7-C-N1 matrix proves the pinned decode stack and real HEIF/AVIF decode on all three mandatory RIDs, but the macOS artifact was not relocatable and therefore remains unacceptable. R7-C-N1 stays blocked until the corrected macOS relocation/deployment audit and prefix-independent smoke pass in a new hosted matrix. There is still no product integration evidence, real 10-bit rejection fixture, HDR fixture, alpha fixture, or container-transform fixture.

## Best next option

Push the relocation correction and require green build/package/audit/prefix-independent-decode jobs for `win-x64`, `linux-x64`, and `osx-arm64`. If all mandatory artifacts pass, R7-C-N1 can be accepted and R7-C may resume with the managed backend, real 8/10-bit/HDR/alpha/transform fixtures, and the full content-routing/integration suite. If macOS still fails, retain `0.1.0.0005` and resolve the precise native artifact defect without weakening the platform matrix.

Current product truth remains in [`../FORMAT-SUPPORT.md`](../FORMAT-SUPPORT.md); dependency provenance remains in [`../THIRD-PARTY.md`](../THIRD-PARTY.md).
