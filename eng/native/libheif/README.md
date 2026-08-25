# Fovium decode-only libheif runtime

Role: Reproducible native build owner for the gated R7-C-N1 prerequisite.
Read when: Building, auditing, or updating Fovium's experimental HEIF/AVIF native runtime.
Authoritative for: Native source pins, build commands, artifact layout, and decode-only supply-chain checks.
Not authoritative for: Product format support or production decoder registration.

This tooling does not add HEIF/AVIF to Fovium. It builds a separate application-local runtime artifact for later R7-C integration.

## Pinned stack

Pins, upstream commits, release archive URLs, and SHA-256 values are machine-owned by [`versions.json`](versions.json):

- libheif 1.23.1;
- libde265 1.1.1 for built-in HEVC decode;
- dav1d 1.5.4 for built-in AV1 decode.

libheif plugin loading and every encoder are disabled. The only external codec implementations built into the runtime contract are libde265 and dav1d. Examples, command-line tools, tests, documentation, fuzzers, development tools, GDK Pixbuf, experimental features, and unrelated image codecs are disabled.

## Clean build commands

Prerequisites are a native C/C++ toolchain, CMake 3.28+, Python 3.12+, NASM 2.16+, and network access to the pinned official archives. The wrappers create an ignored virtual environment containing pinned Meson and Ninja versions. macOS builds explicitly target macOS 14.0, the lowest macOS release in the current supported .NET 10 matrix and an Avalonia 12 supported desktop tier; `versions.json` is the single owner of that value.

Windows x64 from PowerShell with Visual Studio C++ tools installed:

```powershell
pwsh eng/native/libheif/build.ps1 -Rid win-x64
```

Linux x64:

```bash
bash eng/native/libheif/build.sh linux-x64
```

macOS arm64:

```bash
bash eng/native/libheif/build.sh osx-arm64
```

The same script supports `osx-x64` on a real x64 macOS host. It does not pretend that an arm64 build is x64-compatible.

Each invocation re-extracts source and rebuilds into ignored `artifacts/native/`. Downloaded archives may be reused only after their pinned SHA-256 is revalidated.

## Artifact contract

The result is named `fovium-libheif-<rid>` and contains:

- `runtimes/<rid>/native/` with only libheif, libde265, and dav1d runtime libraries;
- `manifest.json` with source pins, build configuration, tool versions, binary filenames/sizes/SHA-256 values, and licenses;
- `license-inventory.json` plus the three upstream license texts;
- `dependency-audit.txt` from `dumpbin`, `readelf`/`ldd`, or `otool`;
- `smoke-report.txt` proving the exact loaded libheif path/version, HEVC and AV1 decoder presence, encoder absence, and successful HEIF/AVIF decode.

Linux uses `$ORIGIN`. macOS packaging deterministically rewrites the three shipped dylib identities to `@rpath`, rewrites their mutual dependencies to the same app-local identities, and gives every real dylib an `@loader_path` runtime search path. Windows resolves the dependency DLLs beside libheif. The dependency audit runs after relocation and verifies the exact macOS deployment target and RID architecture. The build prefix is then renamed out of reach while the smoke runs, on both Unix platforms and Windows, so a passing decode cannot borrow the original install tree. The smoke also fails if libheif loads outside the artifact runtime directory or if an HEVC/AV1 encoder is available.

Final binary hashes are generated only after relocation, dependency audit, and self-contained smoke. Formal macOS signing/notarization is not part of this prerequisite artifact.

The artifact is additionally packed as a deterministic ZIP on Windows or deterministic tar.gz on Unix. No runtime is published or referenced by the production application in R7-C-N1.
