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

## Results

| Candidate | Decode evidence | Packaging evidence | Gate result |
| --- | --- | --- | --- |
| Existing SkiaSharp 3.119.4 | `SKCodec.Create` returned `Unimplemented` for both controlled files | Already shipped | Cannot decode either target format |
| LibHeifSharp 3.2.0 + LibHeif.Native.Runtime 1.20.2 | App-local libheif 1.20.2 decoded both controlled files on Windows; HEVC and AV1 decoders were present | Runtime package contains `win-x64` and `linux-x64`, but no macOS native asset | Fails mandatory macOS arm64 packaging |
| tryAGI.HeifSharp 1.0.2 native bundle | Bundled libheif 1.21.2 could read headers but reported neither HEVC nor AV1 decoder and could not decode either file | Contains Windows/Linux/macOS assets, plus encoder/x265 and compiler-runtime baggage | Fails decode and decode-only dependency requirements |
| NetVips 3.2.0 + NetVips.Native 8.18.5 | Decoded the AVIF file; HEIF pixel decode failed because HEVC decompression was not built in | Broad all-RID imaging runtime | Fails HEIF and is materially broader than the focused boundary |
| Pure-managed HEIC candidates | HEIC-only direction; no matching AVIF backend with equivalent maturity and contract | Avoids native loading but would split the stage across unrelated implementations | Does not meet the one focused HEIF/AVIF backend goal |

The focused LibHeifSharp API exposed the product-relevant primary image, source bit depth, alpha/depth presence, ICC/NCLX data, transforms, and interleaved RGBA decode. This makes the managed adapter technically plausible. It does not solve the missing packaged macOS runtime.

## Decision

R7-C is not productized. Fovium remains at `0.1.0.0005`; no production package, backend, format capability, discovery extension, or support claim is added. The controlled files prove Windows API feasibility only, not cross-platform product support. In particular, no hosted R7-C decode evidence, real 10-bit rejection fixture, HDR fixture, alpha fixture, or container-transform fixture exists yet.

## Best next option

Establish a reproducible, application-owned decode-only libheif runtime distribution before resuming R7-C:

- build from an official pinned libheif source;
- include HEVC decode (for example libde265) and AV1 decode (for example dav1d or the accepted decoder path);
- exclude x265 and other encoder-only components;
- publish explicit `win-x64`, `linux-x64`, `osx-arm64`, and preferably `osx-x64` assets;
- record complete binary/license inventory and reproducible build provenance;
- prove each packaged asset is the one loaded by real fixture decode on its hosted runner.

Only after that prerequisite should Fovium add the backend, deterministic 8-bit/10-bit/HDR/alpha/transform fixtures, and the full content-routing/integration suite.

Current product truth remains in [`../FORMAT-SUPPORT.md`](../FORMAT-SUPPORT.md); dependency provenance remains in [`../THIRD-PARTY.md`](../THIRD-PARTY.md).
