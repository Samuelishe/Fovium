#!/usr/bin/env python3
"""Build and package Fovium's pinned decode-only libheif runtime."""

from __future__ import annotations

import argparse
import gzip
import hashlib
import json
import os
import platform
import re
import shutil
import subprocess
import sys
import tarfile
import urllib.request
import zipfile
from pathlib import Path
from typing import Any, Iterable


SCRIPT_ROOT = Path(__file__).resolve().parent
REPOSITORY_ROOT = SCRIPT_ROOT.parents[2]
VERSIONS_PATH = SCRIPT_ROOT / "versions.json"
ARTIFACT_ROOT = REPOSITORY_ROOT / "artifacts" / "native"

FORBIDDEN_DEPENDENCY_MARKERS = (
    "x265",
    "kvazaar",
    "aom",
    "rav1e",
    "svtav1",
    "svt-av1",
    "ffmpeg",
    "avcodec",
    "openjpeg",
    "openjph",
    "vvdec",
    "vvenc",
    "uvg266",
    "openh264",
    "libjpeg",
    "/opt/homebrew",
    "/usr/local",
)

LIBDE265_OPTIONS = (
    "BUILD_SHARED_LIBS=ON",
    "BUILD_FRAMEWORK=OFF",
    "ENABLE_DECODER=OFF",
    "ENABLE_ENCODER=OFF",
    "ENABLE_SDL=OFF",
    "ENABLE_SHERLOCK265=OFF",
    "ENABLE_INTERNAL_DEVELOPMENT_TOOLS=OFF",
    "WITH_FUZZERS=OFF",
)

DAV1D_OPTIONS = (
    "enable_tools=false",
    "enable_examples=false",
    "enable_tests=false",
    "enable_seek_stress=false",
    "enable_docs=false",
    "testdata_tests=false",
    "xxhash_muxer=disabled",
)

LIBHEIF_OPTIONS = (
    "BUILD_SHARED_LIBS=ON",
    "BUILD_TESTING=OFF",
    "BUILD_DOCUMENTATION=OFF",
    "BUILD_DEVELOPMENT_TOOLS=OFF",
    "ENABLE_EXPERIMENTAL_FEATURES=OFF",
    "ENABLE_PLUGIN_LOADING=OFF",
    "WITH_EXAMPLES=OFF",
    "WITH_EXAMPLE_HEIF_THUMB=OFF",
    "WITH_EXAMPLE_HEIF_VIEW=OFF",
    "WITH_GDK_PIXBUF=OFF",
    "WITH_FUZZERS=OFF",
    "WITH_LIBDE265=ON",
    "WITH_LIBDE265_PLUGIN=OFF",
    "WITH_DAV1D=ON",
    "WITH_DAV1D_PLUGIN=OFF",
    "WITH_X265=OFF",
    "WITH_KVAZAAR=OFF",
    "WITH_AOM_ENCODER=OFF",
    "WITH_AOM_DECODER=OFF",
    "WITH_RAV1E=OFF",
    "WITH_SvtEnc=OFF",
    "WITH_X264=OFF",
    "WITH_OpenH264_DECODER=OFF",
    "WITH_FFMPEG_DECODER=OFF",
    "WITH_JPEG_DECODER=OFF",
    "WITH_JPEG_ENCODER=OFF",
    "WITH_OpenJPEG_DECODER=OFF",
    "WITH_OpenJPEG_ENCODER=OFF",
    "WITH_OPENJPH_DECODER=OFF",
    "WITH_OPENJPH_ENCODER=OFF",
    "WITH_VVDEC=OFF",
    "WITH_VVENC=OFF",
    "WITH_UVG266=OFF",
    "WITH_UNCOMPRESSED_CODEC=OFF",
    "WITH_WEBCODECS=OFF",
    "WITH_HEADER_COMPRESSION=OFF",
    "WITH_LIBSHARPYUV=OFF",
)


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--rid", required=True)
    parser.add_argument(
        "--heif-fixture",
        type=Path,
        default=SCRIPT_ROOT / "fixtures" / "fovium-rgb8.heic",
    )
    parser.add_argument(
        "--avif-fixture",
        type=Path,
        default=SCRIPT_ROOT / "fixtures" / "fovium-rgb8.avif",
    )
    return parser.parse_args()


def run(arguments: Iterable[object], *, cwd: Path | None = None) -> str:
    command = [str(argument) for argument in arguments]
    print("+", " ".join(command), flush=True)
    completed = subprocess.run(
        command,
        cwd=cwd,
        check=False,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        errors="replace",
    )
    if completed.stdout:
        print(completed.stdout, end="" if completed.stdout.endswith("\n") else "\n")
    if completed.returncode != 0:
        raise RuntimeError(
            f"Command failed with exit code {completed.returncode}: {' '.join(command)}"
        )
    return completed.stdout


def read_versions() -> dict[str, Any]:
    return json.loads(VERSIONS_PATH.read_text(encoding="utf-8"))


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def require_under_artifacts(path: Path) -> None:
    resolved = path.resolve()
    artifact_root = ARTIFACT_ROOT.resolve()
    if resolved == artifact_root or artifact_root not in resolved.parents:
        raise RuntimeError(f"Refusing destructive operation outside {artifact_root}: {resolved}")


def recreate_directory(path: Path) -> None:
    require_under_artifacts(path)
    if path.exists():
        shutil.rmtree(path)
    path.mkdir(parents=True)


def download_and_extract(
    component: dict[str, Any], downloads: Path, sources: Path
) -> Path:
    archive = downloads / component["archiveFile"]
    if archive.exists() and sha256(archive) != component["archiveSha256"]:
        archive.unlink()

    if not archive.exists():
        print(f"Downloading {component['archiveUrl']}")
        request = urllib.request.Request(
            component["archiveUrl"], headers={"User-Agent": "Fovium-native-build/1"}
        )
        with urllib.request.urlopen(request) as response, archive.open("wb") as output:
            shutil.copyfileobj(response, output)

    actual_hash = sha256(archive)
    if actual_hash != component["archiveSha256"]:
        raise RuntimeError(
            f"Archive hash mismatch for {archive.name}: {actual_hash}"
        )

    with tarfile.open(archive, mode="r:*") as source_archive:
        source_archive.extractall(sources, filter="data")

    extracted = sources / component["sourceDirectory"]
    if not extracted.is_dir():
        raise RuntimeError(f"Expected source directory was not extracted: {extracted}")
    return extracted


def require_host(rid: str) -> None:
    machine = platform.machine().lower()
    system = platform.system()
    accepted = {
        "win-x64": system == "Windows" and machine in {"amd64", "x86_64"},
        "linux-x64": system == "Linux" and machine in {"amd64", "x86_64"},
        "osx-arm64": system == "Darwin" and machine in {"arm64", "aarch64"},
        "osx-x64": system == "Darwin" and machine in {"amd64", "x86_64"},
    }
    if rid not in accepted:
        raise RuntimeError(f"Unsupported RID: {rid}")
    if not accepted[rid]:
        raise RuntimeError(
            f"RID {rid} does not match host {system}/{platform.machine()}"
        )


def configure_reproducible_environment(
    rid: str, versions: dict[str, Any]
) -> None:
    os.environ["SOURCE_DATE_EPOCH"] = versions["tooling"]["sourceDateEpoch"]
    if rid != "win-x64":
        return

    existing_link = os.environ.get("LINK", "").strip()
    os.environ["LINK"] = f"{existing_link} /Brepro".strip()


def numeric_version(value: str) -> tuple[int, ...]:
    match = re.search(r"\d+(?:\.\d+)+", value)
    if match is None:
        raise RuntimeError(f"Could not parse tool version from: {value!r}")
    return tuple(int(part) for part in match.group(0).split("."))


def verify_toolchain(versions: dict[str, Any]) -> None:
    tooling = versions["tooling"]
    requirements = (
        ("Python", platform.python_version(), tooling["minimumPython"]),
        ("CMake", run(["cmake", "--version"]), tooling["minimumCMake"]),
        ("NASM", run(["nasm", "-v"]), tooling["minimumNasm"]),
    )
    for name, actual_text, minimum_text in requirements:
        if numeric_version(actual_text) < numeric_version(minimum_text):
            raise RuntimeError(
                f"{name} {actual_text.strip()} is older than required {minimum_text}"
            )


def find_library(prefix: Path, candidates: Iterable[str]) -> Path:
    for candidate in candidates:
        matches = list(prefix.rglob(candidate))
        if matches:
            return matches[0].resolve()
    raise RuntimeError(f"Could not find any of {list(candidates)} below {prefix}")


def build_libde265(source: Path, build: Path, prefix: Path, rpath: str) -> None:
    arguments = [
        "cmake",
        "-S",
        source,
        "-B",
        build,
        "-G",
        "Ninja",
        "-DCMAKE_BUILD_TYPE=Release",
        f"-DCMAKE_INSTALL_PREFIX={prefix}",
        f"-DCMAKE_INSTALL_RPATH={rpath}",
    ]
    arguments.extend(f"-D{option}" for option in LIBDE265_OPTIONS)
    run(arguments)
    run(["cmake", "--build", build, "--config", "Release"])
    run(["cmake", "--install", build, "--config", "Release"])


def build_dav1d(source: Path, build: Path, prefix: Path) -> None:
    run(
        [
            "meson",
            "setup",
            build,
            source,
            f"--prefix={prefix}",
            "--libdir=lib",
            "--buildtype=release",
            "--default-library=shared",
            *(f"-D{option}" for option in DAV1D_OPTIONS),
        ]
    )
    run(["meson", "compile", "-C", build])
    run(["meson", "install", "-C", build])


def build_libheif(
    source: Path, build: Path, prefix: Path, rid: str, rpath: str
) -> None:
    if rid == "win-x64":
        dav1d_library = find_library(prefix / "lib", ("dav1d.lib", "libdav1d.lib"))
        de265_library = find_library(prefix / "lib", ("libde265.lib", "de265.lib"))
    elif rid.startswith("linux-"):
        dav1d_library = find_library(prefix / "lib", ("libdav1d.so",))
        de265_library = find_library(prefix / "lib", ("libde265.so",))
    else:
        dav1d_library = find_library(prefix / "lib", ("libdav1d.dylib",))
        de265_library = find_library(prefix / "lib", ("libde265.dylib",))

    arguments: list[object] = [
        "cmake",
        "-S",
        source,
        "-B",
        build,
        "-G",
        "Ninja",
        "-DCMAKE_BUILD_TYPE=Release",
        f"-DCMAKE_INSTALL_PREFIX={prefix}",
        f"-DCMAKE_PREFIX_PATH={prefix}",
        f"-DCMAKE_INSTALL_RPATH={rpath}",
        "-DCMAKE_BUILD_WITH_INSTALL_RPATH=ON",
        f"-DDAV1D_INCLUDE_DIR={prefix / 'include'}",
        f"-DDAV1D_LIBRARY={dav1d_library}",
        f"-DLIBDE265_INCLUDE_DIR={prefix / 'include'}",
        f"-DLIBDE265_LIBRARY={de265_library}",
    ]
    arguments.extend(f"-D{option}" for option in LIBHEIF_OPTIONS)
    run(arguments)
    run(["cmake", "--build", build, "--config", "Release"])
    run(["cmake", "--install", build, "--config", "Release"])


def classify_runtime_file(name: str) -> str | None:
    lowered = name.lower()
    if "heif" in lowered:
        return "libheif"
    if "de265" in lowered:
        return "libde265"
    if "dav1d" in lowered:
        return "dav1d"
    return None


def copy_runtime_files(prefix: Path, native: Path, rid: str) -> list[Path]:
    search_root = prefix / ("bin" if rid == "win-x64" else "lib")
    if not search_root.is_dir():
        raise RuntimeError(f"Runtime search root does not exist: {search_root}")

    runtime_files: list[Path] = []
    for source in sorted(search_root.iterdir()):
        if rid == "win-x64":
            is_runtime = source.is_file() and source.suffix.lower() == ".dll"
        elif rid.startswith("linux-"):
            is_runtime = source.name.startswith("lib") and ".so" in source.name
        else:
            is_runtime = source.name.startswith("lib") and ".dylib" in source.name
        if not is_runtime or classify_runtime_file(source.name) is None:
            continue

        destination = native / source.name
        if source.is_symlink():
            destination.symlink_to(os.readlink(source))
        else:
            shutil.copy2(source, destination)
        runtime_files.append(destination)

    components = {classify_runtime_file(path.name) for path in runtime_files}
    if components != {"libheif", "libde265", "dav1d"}:
        raise RuntimeError(f"Incomplete runtime component set: {sorted(components)}")
    return runtime_files


def compile_smoke(prefix: Path, native: Path, rid: str) -> Path:
    source = SCRIPT_ROOT / "smoke" / "smoke.c"
    if rid == "win-x64":
        import_library = find_library(prefix / "lib", ("heif.lib", "libheif.lib"))
        executable = native / "fovium-libheif-smoke.exe"
        run(
            [
                "cl",
                "/nologo",
                "/O2",
                f"/Fo{native / 'fovium-libheif-smoke.obj'}",
                f"/I{prefix / 'include'}",
                source,
                "/link",
                f"/LIBPATH:{prefix / 'lib'}",
                import_library.name,
                f"/OUT:{executable}",
            ]
        )
    else:
        executable = native / "fovium-libheif-smoke"
        rpath = "@loader_path" if rid.startswith("osx-") else "$ORIGIN"
        linker_arguments = [f"-Wl,-rpath,{rpath}"]
        if rid.startswith("linux-"):
            linker_arguments.append("-ldl")
        run(
            [
                "cc",
                "-O2",
                f"-I{prefix / 'include'}",
                source,
                f"-L{native}",
                "-lheif",
                *linker_arguments,
                "-o",
                executable,
            ]
        )
    return executable


def parse_smoke_report(output: str) -> dict[str, str]:
    report: dict[str, str] = {}
    for line in output.splitlines():
        if "=" in line:
            key, value = line.split("=", 1)
            report[key.strip()] = value.strip()
    return report


def run_smoke(
    executable: Path,
    native: Path,
    heif_fixture: Path,
    avif_fixture: Path,
    expected_version: str,
) -> str:
    if not heif_fixture.is_file() or not avif_fixture.is_file():
        raise RuntimeError("Both tracked HEIF and AVIF smoke fixtures are required")

    environment_path = os.environ.get("PATH", "")
    os.environ["PATH"] = str(native) + os.pathsep + environment_path
    try:
        output = run([executable, heif_fixture.resolve(), avif_fixture.resolve()])
    finally:
        os.environ["PATH"] = environment_path

    report = parse_smoke_report(output)
    required = {
        "runtime.version": expected_version,
        "decoder.hevc": "1",
        "decoder.av1": "1",
        "encoder.hevc": "0",
        "encoder.av1": "0",
        "heif.decode": "PASS",
        "avif.decode": "PASS",
        "result": "PASS",
    }
    for key, expected in required.items():
        if report.get(key) != expected:
            raise RuntimeError(
                f"Smoke report {key!r} was {report.get(key)!r}, expected {expected!r}"
            )

    loaded_path = Path(report["runtime.path"]).resolve()
    if native.resolve() not in loaded_path.parents:
        raise RuntimeError(
            f"Smoke loaded non-artifact libheif: {loaded_path}; expected below {native.resolve()}"
        )

    for label in ("heif", "avif"):
        luma_bits = int(report[f"{label}.lumaBits"])
        chroma_bits = int(report[f"{label}.chromaBits"])
        if max(luma_bits, chroma_bits) > 8:
            raise RuntimeError(f"Tracked {label} smoke fixture is not 8-bit")
    return output


def audit_dependencies(native: Path, rid: str, work: Path) -> str:
    lines: list[str] = []
    binaries = [
        path
        for path in sorted(native.iterdir())
        if path.is_file() and classify_runtime_file(path.name) is not None
    ]
    for binary in binaries:
        lines.append(f"## {binary.name}")
        if rid == "win-x64":
            lines.append(run(["dumpbin", "/DEPENDENTS", binary]))
        elif rid.startswith("linux-"):
            lines.append(run(["readelf", "-d", binary]))
            lines.append(run(["ldd", binary]))
        else:
            lines.append(run(["otool", "-L", binary]))
            lines.append(run(["otool", "-l", binary]))

    report = "\n".join(lines)
    if rid.startswith("linux-"):
        # ldd prints randomized load addresses; they are not dependency identity.
        report = re.sub(r"\s+\(0x[0-9a-fA-F]+\)", "", report)
    lowered = report.lower()
    for marker in FORBIDDEN_DEPENDENCY_MARKERS:
        if marker in lowered:
            raise RuntimeError(f"Forbidden dependency marker in audit: {marker}")

    work_marker = str(work.resolve()).lower()
    if work_marker in lowered:
        raise RuntimeError("Build-work absolute path leaked into packaged dependency metadata")

    if rid.startswith("linux-"):
        for dependency in ("libde265", "libdav1d"):
            matching = [line for line in report.splitlines() if dependency in line.lower()]
            if not matching or not any(str(native.resolve()) in line for line in matching):
                raise RuntimeError(
                    f"Linux audit did not resolve {dependency} from the artifact directory"
                )
    return report


def copy_licenses(
    versions: dict[str, Any], source_paths: dict[str, Path], licenses: Path
) -> list[dict[str, str]]:
    inventory: list[dict[str, str]] = []
    for name, component in versions["sources"].items():
        source = source_paths[name] / component["licenseFile"]
        destination_name = f"{name}-{component['license']}.txt"
        destination = licenses / destination_name
        shutil.copy2(source, destination)
        inventory.append(
            {
                "component": name,
                "version": component["version"],
                "license": component["license"],
                "file": f"licenses/{destination_name}",
                "sourceUrl": component["sourceUrl"],
            }
        )
    return inventory


def tool_version(command: Iterable[object]) -> str:
    return run(command).splitlines()[0].strip()


def create_manifest(
    bundle: Path,
    rid: str,
    versions: dict[str, Any],
    license_inventory: list[dict[str, str]],
    heif_fixture: Path,
    avif_fixture: Path,
) -> dict[str, Any]:
    native = bundle / "runtimes" / rid / "native"
    binary_files = []
    for path in sorted(native.iterdir()):
        if path.is_symlink():
            binary_files.append(
                {
                    "path": path.relative_to(bundle).as_posix(),
                    "symlink": os.readlink(path),
                }
            )
        elif path.is_file():
            binary_files.append(
                {
                    "path": path.relative_to(bundle).as_posix(),
                    "size": path.stat().st_size,
                    "sha256": sha256(path),
                    "component": classify_runtime_file(path.name),
                }
            )

    manifest = {
        "schemaVersion": 1,
        "artifact": bundle.name,
        "rid": rid,
        "sources": versions["sources"],
        "binaryFiles": binary_files,
        "buildConfiguration": {
            "purpose": "decode-only HEIF/AVIF native runtime prerequisite",
            "enabledCodecs": ["libde265 HEVC decoder", "dav1d AV1 decoder"],
            "pluginLoading": False,
            "encoders": [],
            "libde265CMakeOptions": list(LIBDE265_OPTIONS),
            "dav1dMesonOptions": list(DAV1D_OPTIONS),
            "libheifCMakeOptions": list(LIBHEIF_OPTIONS),
        },
        "smokeFixtures": [
            {
                "format": "HEIF",
                "file": heif_fixture.name,
                "sha256": sha256(heif_fixture),
            },
            {
                "format": "AVIF",
                "file": avif_fixture.name,
                "sha256": sha256(avif_fixture),
            },
        ],
        "toolchain": {
            "host": f"{platform.system()} {platform.machine()}",
            "python": platform.python_version(),
            "cmake": tool_version(["cmake", "--version"]),
            "meson": tool_version(["meson", "--version"]),
            "ninja": tool_version(["ninja", "--version"]),
            "sourceDateEpoch": versions["tooling"]["sourceDateEpoch"],
        },
        "licenses": license_inventory,
    }
    (bundle / "manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    return manifest


def create_deterministic_archive(bundle: Path, packages: Path, rid: str) -> Path:
    packages.mkdir(parents=True, exist_ok=True)
    if rid == "win-x64":
        archive = packages / f"{bundle.name}.zip"
        if archive.exists():
            archive.unlink()
        with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as output:
            for path in sorted(bundle.rglob("*")):
                if not path.is_file():
                    continue
                relative = (Path(bundle.name) / path.relative_to(bundle)).as_posix()
                info = zipfile.ZipInfo(relative, date_time=(1980, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o100644 << 16
                output.writestr(info, path.read_bytes())
    else:
        archive = packages / f"{bundle.name}.tar.gz"
        if archive.exists():
            archive.unlink()
        with archive.open("wb") as raw_output:
            with gzip.GzipFile(fileobj=raw_output, mode="wb", mtime=0) as compressed:
                with tarfile.open(fileobj=compressed, mode="w") as output:
                    for path in [bundle, *sorted(bundle.rglob("*"))]:
                        relative = Path(bundle.name) / path.relative_to(bundle)
                        info = output.gettarinfo(str(path), arcname=relative.as_posix())
                        info.uid = 0
                        info.gid = 0
                        info.uname = ""
                        info.gname = ""
                        info.mtime = 0
                        if info.isfile():
                            with path.open("rb") as source:
                                output.addfile(info, source)
                        else:
                            output.addfile(info)
    return archive


def main() -> int:
    arguments = parse_arguments()
    rid = arguments.rid
    require_host(rid)

    versions = read_versions()
    verify_toolchain(versions)
    configure_reproducible_environment(rid, versions)
    work = ARTIFACT_ROOT / "work" / rid
    bundle = ARTIFACT_ROOT / f"fovium-libheif-{rid}"
    downloads = ARTIFACT_ROOT / "downloads"
    packages = ARTIFACT_ROOT / "packages"
    recreate_directory(work)
    recreate_directory(bundle)
    downloads.mkdir(parents=True, exist_ok=True)

    sources_root = work / "sources"
    builds_root = work / "build"
    prefix = work / "prefix"
    native = bundle / "runtimes" / rid / "native"
    licenses = bundle / "licenses"
    sources_root.mkdir(parents=True)
    builds_root.mkdir(parents=True)
    prefix.mkdir(parents=True)
    native.mkdir(parents=True)
    licenses.mkdir(parents=True)

    source_paths = {
        name: download_and_extract(component, downloads, sources_root)
        for name, component in versions["sources"].items()
    }

    if rid.startswith("linux-"):
        rpath = "$ORIGIN"
    elif rid.startswith("osx-"):
        rpath = "@loader_path"
    else:
        rpath = ""

    build_libde265(
        source_paths["libde265"], builds_root / "libde265", prefix, rpath
    )
    build_dav1d(source_paths["dav1d"], builds_root / "dav1d", prefix)
    build_libheif(
        source_paths["libheif"], builds_root / "libheif", prefix, rid, rpath
    )

    copy_runtime_files(prefix, native, rid)
    smoke_executable = compile_smoke(prefix, native, rid)
    smoke_report = run_smoke(
        smoke_executable,
        native,
        arguments.heif_fixture,
        arguments.avif_fixture,
        versions["sources"]["libheif"]["version"],
    )
    smoke_executable.unlink()
    smoke_object = native / "fovium-libheif-smoke.obj"
    if smoke_object.exists():
        smoke_object.unlink()

    dependency_audit = audit_dependencies(native, rid, work)
    (bundle / "smoke-report.txt").write_text(smoke_report, encoding="utf-8")
    (bundle / "dependency-audit.txt").write_text(
        dependency_audit, encoding="utf-8"
    )
    license_inventory = copy_licenses(versions, source_paths, licenses)
    (bundle / "license-inventory.json").write_text(
        json.dumps(license_inventory, indent=2) + "\n", encoding="utf-8"
    )
    manifest = create_manifest(
        bundle,
        rid,
        versions,
        license_inventory,
        arguments.heif_fixture,
        arguments.avif_fixture,
    )
    archive = create_deterministic_archive(bundle, packages, rid)

    for name in ("manifest.json", "smoke-report.txt", "dependency-audit.txt"):
        shutil.copy2(bundle / name, packages / f"{bundle.name}.{name}")

    print(f"artifact.bundle={bundle}")
    print(f"artifact.archive={archive}")
    print(f"artifact.archiveSha256={sha256(archive)}")
    print(f"artifact.nativeFileCount={len(manifest['binaryFiles'])}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"native build failed: {error}", file=sys.stderr)
        raise
