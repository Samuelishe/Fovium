# Fovium Little CMS native runtime

Role: Reproducible source-build owner for the future Fovium Monitor Color Management native prerequisite.

This directory builds the core Little CMS shared library from the pinned official `lcms2.19` release. It does not integrate Little CMS into production Fovium, enable Monitor Color Management, or change normal rendering. Generated sources, build trees, runtime bundles, and archives remain under ignored `artifacts/native/`.

## Pin and license

- Component: Little CMS 2.19
- Upstream tag: `lcms2.19`
- Tag commit: `b76633e60c8387a77268fb3359277ca25b5fd75c`
- Source archive: <https://github.com/mm2/Little-CMS/releases/download/lcms2.19/lcms2-2.19.tar.gz>
- Archive SHA-256: `49e7e134e4299733dd0eda434fa468997a28ab3d33fa397c642b03644f552216`
- License: MIT, `Copyright (c) 2023 Marti Maria Saguer`
- License text SHA-256: `6dbd60437f8ef91d8de1f08ad75882547fd4931bfcc3566a0735f28db1484d31`

`versions.json` is the machine-owned pin. The build fails if the source hash or expected upstream license text differs.

## Build

Windows requires an x64 Visual Studio C++ Build Tools installation plus CMake and Python:

```powershell
pwsh eng/native/lcms2/build.ps1 -Rid win-x64
```

Linux and macOS require a C compiler, CMake, and Python 3:

```bash
bash eng/native/lcms2/build.sh linux-x64
bash eng/native/lcms2/build.sh osx-arm64
```

`osx-x64` is also supported by the scripts but is not a required product RID and must not be described as proven without a real build/run.

The upstream CMake build is configured for the Release shared core only. Static libraries, command-line tools, upstream tests, JPEG, TIFF, zlib, the GPL fast-float plug-in, and the GPL threaded plug-in are disabled. Core platform threading support remains enabled. macOS is built for exactly the requested architecture with deployment target 14.0.

Builds set a pinned `SOURCE_DATE_EPOCH`; Windows also passes the existing repository convention `/Brepro` through the linker environment. Archive timestamps, ownership, ordering, and permissions are normalized. The build still compares clean outputs rather than assuming those controls guarantee identical bytes on every toolchain.

## Output and locality gate

Each build creates an independent bundle:

```text
artifacts/native/fovium-lcms2-<rid>/
  manifest.json
  evidence/
    dependency-audit.txt
    smoke-report.txt
  licenses/
    LICENSE.lcms2.txt
  runtimes/<rid>/native/
    <upstream Little CMS shared runtime and required relative symlinks>
```

Stable archives and adjacent SHA-256 files are written to `artifacts/native/packages/`.

The smoke executable is linked with same-directory loader semantics (`$ORIGIN` on Linux, `@loader_path` on macOS, and Windows DLL search beside the executable). Before smoke execution, the complete CMake install prefix is renamed out of availability. The smoke resolves the actual module containing `cmsGetEncodedCMMversion`, canonicalizes its path, and fails unless it is inside the bundle's `runtimes/<rid>/native/` directory. The smoke executable, object file, headers, import/static libraries, and debug artifacts are removed before the final runtime manifest is generated.

## Smoke contract

The native smoke uses only the shipped Little CMS API and checks:

- API-reported encoded runtime version `2190` (`2.19`);
- an sRGB matrix/TRC to linear-RGB matrix/TRC relative-colorimetric transform with bounded, recorded RGB8 patches;
- a programmatically authored ICC v4 RGB device-link profile containing an actual 3D 16-bit CLUT, serialized and reopened from memory, with exact recorded RGB8 patches;
- empty, invalid-signature, truncated, and impossible-size ICC buffers returning controlled failure;
- the project-side 16 MiB ICC admission recommendation rejecting larger input before an lcms call;
- independent contexts, profiles, and transform handles executing concurrently against the same immutable profile bytes;
- relative-colorimetric intent with flags `0`, so black-point compensation is not enabled.

The build runs the complete smoke 100 times. This is repetition evidence for native lifetime mistakes, not a complete leak or fuzzing proof.

## Audit and manifest

The build records:

- `dumpbin /HEADERS` and `/DEPENDENTS` on Windows;
- `readelf -h`, `readelf -d`, and `ldd` on Linux;
- `file`, `lipo -archs`, `otool -D`, `otool -L`, and `otool -l` on macOS.

The audit rejects the wrong architecture, a non-local lcms resolution, custom `/usr/local` or Homebrew paths, and accidental JPEG/TIFF/PNG/zlib dependencies. Linux/macOS runtime symlinks must remain relative, non-dangling, and contained in the artifact. On macOS the physical dylib receives an `@rpath` install name before final hashes are calculated, and `LC_BUILD_VERSION` must report `minos 14.0`.

`manifest.json` is generated from the final shipped bytes after relocation and smoke. It records source provenance, build configuration, toolchain, deployment target where relevant, runtime file types/symlinks/sizes/SHA-256 values, license identity, support-file hashes, smoke summary, and `buildPrefixAvailable: false`.

## Threading evidence and future interop inventory

The upstream engine API describes `cmsDoTransform` as reentrant. Upstream contexts isolate global state and plug-ins, and Little CMS has platform locking support when threads are enabled. For future Fovium integration, keep profile/transform lifetime and mutation externally owned; the conservative architecture is independent contexts/transforms per worker. This smoke deliberately proves that pattern and does not depend on the separately licensed multithreaded plug-in.

The minimum likely direct-interop surface for R8-B-W1 is:

- `cmsGetEncodedCMMversion`;
- `cmsCreateContext`, `cmsDeleteContext`, and `cmsSetLogErrorHandlerTHR`;
- `cmsOpenProfileFromMemTHR`, `cmsCloseProfile`, `cmsGetColorSpace`, `cmsGetDeviceClass`, `cmsGetPCS`, `cmsIsMatrixShaper`, and `cmsIsCLUT`;
- `cmsCreateTransformTHR`, `cmsDoTransform` or `cmsDoTransformLineStride`, and `cmsDeleteTransform`.

This is an inventory, not a C# binding. R8-B-N1 adds no managed wrapper or production P/Invoke code.
