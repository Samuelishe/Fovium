#!/usr/bin/env python3
"""Build, audit, smoke, and package Fovium's pinned Little CMS runtime."""

from __future__ import annotations

import argparse
import datetime as dt
import gzip
import hashlib
import json
import os
import pathlib
import platform
import re
import shutil
import stat
import subprocess
import sys
import tarfile
import tempfile
import urllib.request
import zipfile


SCRIPT_ROOT = pathlib.Path(__file__).resolve().parent
REPOSITORY_ROOT = SCRIPT_ROOT.parents[2]
VERSIONS_PATH = SCRIPT_ROOT / "versions.json"
ARTIFACTS_ROOT = REPOSITORY_ROOT / "artifacts" / "native"
DOWNLOADS_ROOT = ARTIFACTS_ROOT / "downloads"
WORK_ROOT = ARTIFACTS_ROOT / "work" / "lcms2"
PACKAGES_ROOT = ARTIFACTS_ROOT / "packages"
SMOKE_SOURCE = SCRIPT_ROOT / "smoke" / "smoke.c"


class BuildFailure(RuntimeError):
    pass


def log(message: str) -> None:
    print(f"[lcms2] {message}", flush=True)


def run(
    command: list[str],
    *,
    cwd: pathlib.Path | None = None,
    env: dict[str, str] | None = None,
    capture: bool = False,
) -> str:
    rendered = subprocess.list2cmdline(command) if os.name == "nt" else " ".join(command)
    log(f"run: {rendered}")
    completed = subprocess.run(
        command,
        cwd=cwd,
        env=env,
        check=False,
        text=True,
        stdout=subprocess.PIPE if capture else None,
        stderr=subprocess.STDOUT if capture else None,
    )
    output = completed.stdout or ""
    if capture and output:
        print(output, end="" if output.endswith("\n") else "\n", flush=True)
    if completed.returncode != 0:
        raise BuildFailure(f"Command failed with exit code {completed.returncode}: {rendered}")
    return output


def command_output(command: list[str]) -> str:
    return run(command, capture=True).strip()


def load_versions() -> dict:
    data = json.loads(VERSIONS_PATH.read_text(encoding="utf-8"))
    if data.get("schemaVersion") != 1:
        raise BuildFailure("Unsupported versions.json schema.")
    return data


def version_tuple(value: str) -> tuple[int, ...]:
    match = re.search(r"(\d+(?:\.\d+)+)", value)
    if not match:
        raise BuildFailure(f"Could not parse version from: {value}")
    return tuple(int(part) for part in match.group(1).split("."))


def require_tools(versions: dict, rid: str) -> None:
    required = ["cmake", "python"]
    if rid == "win-x64":
        required.extend(["cl", "dumpbin"])
    else:
        required.extend(["cc", "file"])
        if rid == "linux-x64":
            required.extend(["readelf", "ldd"])
        else:
            required.extend(["otool", "lipo", "install_name_tool"])
    missing = [tool for tool in required if shutil.which(tool) is None]
    if missing:
        raise BuildFailure(f"Required tools are missing: {', '.join(missing)}")

    python_minimum = version_tuple(versions["tooling"]["minimumPython"])
    if sys.version_info[:3] < python_minimum:
        raise BuildFailure(f"Python {versions['tooling']['minimumPython']} or newer is required.")
    cmake_version = version_tuple(command_output(["cmake", "--version"]).splitlines()[0])
    if cmake_version < version_tuple(versions["tooling"]["minimumCMake"]):
        raise BuildFailure(f"CMake {versions['tooling']['minimumCMake']} or newer is required.")


def validate_host(rid: str) -> None:
    system = platform.system()
    machine = platform.machine().lower()
    expected = {
        "win-x64": ("Windows", {"amd64", "x86_64"}),
        "linux-x64": ("Linux", {"x86_64", "amd64"}),
        "osx-arm64": ("Darwin", {"arm64", "aarch64"}),
        "osx-x64": ("Darwin", {"x86_64", "amd64"}),
    }
    if rid not in expected:
        raise BuildFailure(f"Unsupported RID: {rid}")
    expected_system, expected_machines = expected[rid]
    if system != expected_system or machine not in expected_machines:
        raise BuildFailure(f"RID {rid} does not match host {system}/{machine}.")
    if platform.architecture()[0] != "64bit":
        raise BuildFailure(f"RID {rid} requires a 64-bit process.")


def sha256_file(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def remove_tree(path: pathlib.Path) -> None:
    if not path.exists() and not path.is_symlink():
        return
    resolved_parent = path.parent.resolve()
    allowed = (ARTIFACTS_ROOT / "work" / "lcms2").resolve()
    if resolved_parent != allowed:
        raise BuildFailure(f"Refusing to remove unexpected path: {path}")
    shutil.rmtree(path)


def acquire_source(source: dict, source_parent: pathlib.Path) -> pathlib.Path:
    DOWNLOADS_ROOT.mkdir(parents=True, exist_ok=True)
    archive = DOWNLOADS_ROOT / source["archiveFile"]
    expected_hash = source["archiveSha256"].lower()
    if archive.exists() and sha256_file(archive) != expected_hash:
        log("cached source archive hash mismatch; discarding it")
        archive.unlink()
    if not archive.exists():
        log(f"download: {source['archiveUrl']}")
        with urllib.request.urlopen(source["archiveUrl"]) as response, archive.open("wb") as output:
            shutil.copyfileobj(response, output)
    actual_hash = sha256_file(archive)
    if actual_hash != expected_hash:
        raise BuildFailure(f"Source archive SHA-256 mismatch: expected {expected_hash}, got {actual_hash}.")
    log(f"source.sha256={actual_hash}")

    source_parent.mkdir(parents=True, exist_ok=True)
    with tarfile.open(archive, "r:gz") as tar:
        tar.extractall(source_parent, filter="data")
    source_root = source_parent / source["sourceDirectory"]
    if not (source_root / "CMakeLists.txt").is_file():
        raise BuildFailure(f"Expected source directory was not extracted: {source_root}")
    license_path = source_root / source["licenseFile"]
    if not license_path.is_file() or sha256_file(license_path) != source["licenseSha256"]:
        raise BuildFailure("Upstream license SHA-256 did not match the pinned MIT text.")
    cmake_text = (source_root / "CMakeLists.txt").read_text(encoding="utf-8")
    header_text = (source_root / "include" / "lcms2.h").read_text(encoding="utf-8")
    if "VERSION 2.19" not in cmake_text or "#define LCMS_VERSION        2190" not in header_text:
        raise BuildFailure("Extracted source does not declare the pinned Little CMS 2.19 API version.")
    return source_root


def cmake_environment(versions: dict) -> dict[str, str]:
    env = os.environ.copy()
    env["SOURCE_DATE_EPOCH"] = versions["tooling"]["sourceDateEpoch"]
    env["TZ"] = "UTC"
    env["LC_ALL"] = "C"
    if os.name == "nt":
        existing_link = env.get("LINK", "").strip()
        reproducibility_flag = versions["tooling"]["windowsLinkerReproducibilityFlag"]
        env["LINK"] = f"{existing_link} {reproducibility_flag}".strip()
    return env


def configure_and_install(
    versions: dict,
    rid: str,
    source_root: pathlib.Path,
    build_root: pathlib.Path,
    prefix: pathlib.Path,
) -> None:
    options = versions["build"]["cmakeOptions"]
    configuration = versions["build"]["configuration"]
    command = [
        "cmake",
        "-S", str(source_root),
        "-B", str(build_root),
        f"-DCMAKE_INSTALL_PREFIX={prefix}",
    ]
    command.extend(f"-D{name}={value}" for name, value in sorted(options.items()))
    if rid == "win-x64":
        command.extend(["-A", "x64"])
    else:
        command.append(f"-DCMAKE_BUILD_TYPE={configuration}")
        if rid.startswith("osx-"):
            architecture = "arm64" if rid == "osx-arm64" else "x86_64"
            target = versions["tooling"]["macosDeploymentTarget"]
            command.extend([
                f"-DCMAKE_OSX_ARCHITECTURES={architecture}",
                f"-DCMAKE_OSX_DEPLOYMENT_TARGET={target}",
                "-DCMAKE_INSTALL_NAME_DIR=@rpath",
                "-DCMAKE_INSTALL_RPATH=@loader_path",
            ])
    env = cmake_environment(versions)
    run(command, env=env)
    run(["cmake", "--build", str(build_root), "--config", configuration, "--parallel"], env=env)
    run(["cmake", "--install", str(build_root), "--config", configuration], env=env)


def installed_runtime_files(prefix: pathlib.Path, rid: str) -> list[pathlib.Path]:
    if rid == "win-x64":
        candidates = list(prefix.rglob("lcms2.dll"))
    elif rid == "linux-x64":
        candidates = list(prefix.rglob("liblcms2.so*"))
    else:
        candidates = list(prefix.rglob("liblcms2*.dylib"))
    candidates = sorted({candidate for candidate in candidates if candidate.is_file() or candidate.is_symlink()})
    if not candidates:
        raise BuildFailure("The installed Little CMS shared runtime was not found.")
    if rid == "win-x64" and len(candidates) != 1:
        raise BuildFailure(f"Expected one Windows runtime DLL, found: {candidates}")
    return candidates


def copy_runtime_files(files: list[pathlib.Path], runtime_root: pathlib.Path) -> None:
    runtime_root.mkdir(parents=True, exist_ok=True)
    names = {path.name for path in files}
    for source in files:
        destination = runtime_root / source.name
        if source.is_symlink():
            target = os.readlink(source)
            if os.path.isabs(target) or pathlib.Path(target).name not in names:
                raise BuildFailure(f"Unsafe installed runtime symlink: {source} -> {target}")
            destination.symlink_to(target)
        else:
            shutil.copy2(source, destination)
    validate_runtime_symlinks(runtime_root)


def validate_runtime_symlinks(runtime_root: pathlib.Path) -> None:
    resolved_root = runtime_root.resolve()
    for path in runtime_root.iterdir():
        if path.is_symlink():
            target = path.resolve(strict=True)
            if target.parent != resolved_root:
                raise BuildFailure(f"Runtime symlink escapes artifact: {path} -> {os.readlink(path)}")


def find_import_library(prefix: pathlib.Path) -> pathlib.Path:
    candidates = sorted(prefix.rglob("lcms2.lib"))
    if len(candidates) != 1:
        raise BuildFailure(f"Expected one lcms2 import library, found: {candidates}")
    return candidates[0]


def compile_smoke(
    versions: dict,
    rid: str,
    prefix: pathlib.Path,
    runtime_root: pathlib.Path,
) -> pathlib.Path:
    include_root = prefix / "include"
    if not (include_root / "lcms2.h").is_file():
        raise BuildFailure("Installed Little CMS headers were not found for smoke compilation.")
    if rid == "win-x64":
        executable = runtime_root / "fovium-lcms2-smoke.exe"
        object_file = runtime_root / "fovium-lcms2-smoke.obj"
        import_library = find_import_library(prefix)
        run([
            "cl", "/nologo", "/W4", "/WX", "/O2", "/MD", "/DCMS_DLL",
            f"/I{include_root}", f"/Fo{object_file}", str(SMOKE_SOURCE),
            "/link", f"/LIBPATH:{import_library.parent}", "lcms2.lib", f"/OUT:{executable}",
        ])
    else:
        executable = runtime_root / "fovium-lcms2-smoke"
        rpath = "@loader_path" if rid.startswith("osx-") else "$ORIGIN"
        command = [
            "cc", "-std=c11", "-Wall", "-Wextra", "-Werror", "-O2",
            f"-I{include_root}", str(SMOKE_SOURCE), f"-L{runtime_root}", "-llcms2",
            f"-Wl,-rpath,{rpath}", "-pthread", "-o", str(executable),
        ]
        if rid == "linux-x64":
            command.insert(-2, "-ldl")
        else:
            architecture = "arm64" if rid == "osx-arm64" else "x86_64"
            target = versions["tooling"]["macosDeploymentTarget"]
            command[1:1] = ["-arch", architecture, f"-mmacosx-version-min={target}"]
        run(command)
    return executable


def relocate_macos(runtime_root: pathlib.Path) -> None:
    dylibs = [path for path in runtime_root.iterdir() if path.is_file() and not path.is_symlink() and path.suffix == ".dylib"]
    if len(dylibs) != 1:
        raise BuildFailure(f"Expected one physical macOS dylib, found: {dylibs}")
    dylib = dylibs[0]
    run(["install_name_tool", "-id", f"@rpath/{dylib.name}", str(dylib)])


def compiler_summary(rid: str) -> dict[str, str]:
    if rid == "win-x64":
        compiler = f"MSVC tools {os.environ.get('VCToolsVersion', 'unknown').strip()} (x64)"
    else:
        output = command_output(["cc", "--version"])
        compiler = output.splitlines()[0]
    return {
        "compiler": compiler,
        "cmake": command_output(["cmake", "--version"]).splitlines()[0],
        "host": f"{platform.system()} {platform.release()} {platform.machine()}",
    }


def audit_binaries(rid: str, runtime_root: pathlib.Path, smoke: pathlib.Path) -> str:
    sections: list[str] = []
    if rid == "win-x64":
        physical_libraries = [path for path in runtime_root.glob("lcms2.dll") if path.is_file()]
    elif rid == "linux-x64":
        physical_libraries = [
            path for path in runtime_root.glob("liblcms2.so*") if path.is_file() and not path.is_symlink()
        ]
    else:
        physical_libraries = [
            path for path in runtime_root.glob("liblcms2*.dylib") if path.is_file() and not path.is_symlink()
        ]
    if len(physical_libraries) != 1:
        raise BuildFailure(f"Expected one physical runtime library, found: {physical_libraries}")
    library = physical_libraries[0]

    def capture(label: str, command: list[str]) -> str:
        output = command_output(command)
        sections.append(f"## {label}\n{output}\n")
        return output

    if rid == "win-x64":
        headers = capture("dumpbin /HEADERS runtime", ["dumpbin", "/HEADERS", str(library)])
        dependencies = capture("dumpbin /DEPENDENTS runtime", ["dumpbin", "/DEPENDENTS", str(library)])
        capture("dumpbin /DEPENDENTS smoke", ["dumpbin", "/DEPENDENTS", str(smoke)])
        if "8664 machine (x64)" not in headers:
            raise BuildFailure("Windows runtime is not PE x64.")
        forbidden = ["jpeg", "tiff", "png", "zlib", "liblcms"]
        lowered = dependencies.lower()
        if any(marker in lowered for marker in forbidden):
            raise BuildFailure("Windows runtime has an unexpected non-system dependency.")
    elif rid == "linux-x64":
        header = capture("readelf -h runtime", ["readelf", "-h", str(library)])
        dynamic = capture("readelf -d runtime", ["readelf", "-d", str(library)])
        ldd_runtime = capture("ldd runtime", ["ldd", str(library)])
        smoke_dynamic = capture("readelf -d smoke", ["readelf", "-d", str(smoke)])
        ldd_smoke = capture("ldd smoke", ["ldd", str(smoke)])
        if "Advanced Micro Devices X86-64" not in header:
            raise BuildFailure("Linux runtime is not ELF x86-64.")
        if "$ORIGIN" not in smoke_dynamic:
            raise BuildFailure("Linux smoke does not contain an $ORIGIN runpath.")
        if str(runtime_root.resolve()) not in ldd_smoke:
            raise BuildFailure("Linux smoke did not resolve lcms2 from the artifact runtime directory.")
        combined = (dynamic + ldd_runtime).lower()
        if any(marker in combined for marker in ["libjpeg", "libtiff", "libpng", "libz", "/usr/local"]):
            raise BuildFailure("Linux runtime has an unexpected dependency.")
    else:
        file_output = capture("file runtime", ["file", str(library)])
        archs = capture("lipo -archs runtime", ["lipo", "-archs", str(library)])
        identity = capture("otool -D runtime", ["otool", "-D", str(library)])
        dependencies = capture("otool -L runtime", ["otool", "-L", str(library)])
        load_commands = capture("otool -l runtime", ["otool", "-l", str(library)])
        smoke_dependencies = capture("otool -L smoke", ["otool", "-L", str(smoke)])
        expected_arch = "arm64" if rid == "osx-arm64" else "x86_64"
        if archs.strip() != expected_arch or expected_arch not in file_output:
            raise BuildFailure(f"macOS runtime architecture is not exactly {expected_arch}.")
        if f"@rpath/{library.name}" not in identity:
            raise BuildFailure("macOS runtime install name is not app-relative.")
        if f"@rpath/{library.name}" not in smoke_dependencies:
            raise BuildFailure("macOS smoke does not reference the app-relative runtime install name.")
        if "minos 14.0" not in load_commands:
            raise BuildFailure("macOS runtime deployment target is not 14.0.")
        combined = (identity + dependencies + smoke_dependencies).lower()
        if any(marker in combined for marker in ["/opt/homebrew", "/usr/local", "libjpeg", "libtiff", "libpng", "libz"]):
            raise BuildFailure("macOS runtime has an unexpected dependency or build path.")
    return "\n".join(sections)


def run_smoke(smoke: pathlib.Path, runtime_root: pathlib.Path, iterations: int) -> tuple[str, dict[str, str]]:
    first_output = ""
    summary: dict[str, str] = {}
    for iteration in range(iterations):
        completed = subprocess.run(
            [str(smoke), str(runtime_root)],
            check=False,
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
        )
        output = completed.stdout or ""
        if completed.returncode != 0 or "result=PASS" not in output:
            print(output, end="" if output.endswith("\n") else "\n", flush=True)
            raise BuildFailure(f"Smoke iteration {iteration + 1} did not report PASS.")
        if iteration == 0:
            first_output = output
            for line in output.splitlines():
                if "=" in line:
                    key, value = line.split("=", 1)
                    summary[key] = value
        if iteration == 0 or (iteration + 1) % 25 == 0 or iteration + 1 == iterations:
            log(f"smoke.progress={iteration + 1}/{iterations}")
    summary["iterations"] = str(iterations)
    summary["failures"] = "0"
    return first_output, summary


def runtime_inventory(runtime_root: pathlib.Path) -> list[dict]:
    inventory: list[dict] = []
    for path in sorted(runtime_root.iterdir(), key=lambda item: item.name):
        if path.is_symlink():
            inventory.append({
                "path": path.name,
                "type": "symlink",
                "target": os.readlink(path),
            })
        elif path.is_file():
            inventory.append({
                "path": path.name,
                "type": "file",
                "size": path.stat().st_size,
                "sha256": sha256_file(path),
            })
        else:
            raise BuildFailure(f"Unexpected runtime artifact entry: {path}")
    return inventory


def write_artifact_metadata(
    versions: dict,
    rid: str,
    bundle_root: pathlib.Path,
    runtime_root: pathlib.Path,
    source_root: pathlib.Path,
    audit: str,
    smoke_output: str,
    smoke_summary: dict[str, str],
    toolchain: dict[str, str],
) -> pathlib.Path:
    license_root = bundle_root / "licenses"
    evidence_root = bundle_root / "evidence"
    license_root.mkdir(parents=True, exist_ok=True)
    evidence_root.mkdir(parents=True, exist_ok=True)
    license_path = license_root / "LICENSE.lcms2.txt"
    shutil.copy2(source_root / versions["source"]["licenseFile"], license_path)
    audit_path = evidence_root / "dependency-audit.txt"
    audit_path.write_text(audit, encoding="utf-8", newline="\n")
    smoke_path = evidence_root / "smoke-report.txt"
    smoke_path.write_text(
        smoke_output + f"\niterations={smoke_summary['iterations']}\nfailures=0\nbuildPrefix.availableDuringSmoke=0\n",
        encoding="utf-8",
        newline="\n",
    )

    support_files = []
    for path in [license_path, audit_path, smoke_path]:
        support_files.append({
            "path": path.relative_to(bundle_root).as_posix(),
            "size": path.stat().st_size,
            "sha256": sha256_file(path),
        })
    source = versions["source"]
    manifest = {
        "schemaVersion": 1,
        "rid": rid,
        "component": source["component"],
        "version": source["version"],
        "tag": source["tag"],
        "commit": source["commit"],
        "sourceUrl": source["sourceUrl"],
        "sourceArchive": {
            "url": source["archiveUrl"],
            "sha256": source["archiveSha256"],
        },
        "license": {
            "identifier": source["license"],
            "copyright": source["copyright"],
            "path": license_path.relative_to(bundle_root).as_posix(),
            "sha256": source["licenseSha256"],
        },
        "build": {
            "configuration": versions["build"]["configuration"],
            "cmakeOptions": versions["build"]["cmakeOptions"],
            "toolchain": toolchain,
            "sourceDateEpoch": versions["tooling"]["sourceDateEpoch"],
            "windowsLinkerReproducibilityFlag": versions["tooling"]["windowsLinkerReproducibilityFlag"] if rid == "win-x64" else None,
            "macosDeploymentTarget": versions["tooling"]["macosDeploymentTarget"] if rid.startswith("osx-") else None,
        },
        "runtimeDirectory": f"runtimes/{rid}/native",
        "runtimeFiles": runtime_inventory(runtime_root),
        "supportFiles": support_files,
        "smoke": {
            "result": smoke_summary.get("result"),
            "runtimePath": smoke_summary.get("runtime.path"),
            "runtimeVersion": smoke_summary.get("runtime.version"),
            "matrixTransform": smoke_summary.get("matrixTransform"),
            "lutTransform": smoke_summary.get("lutTransform"),
            "malformedProfile": "PASS" if all(smoke_summary.get(key) == "PASS" for key in [
                "malformed.empty", "malformed.badSignature", "malformed.truncated", "malformed.impossibleSize"
            ]) else "FAIL",
            "concurrency": smoke_summary.get("concurrency.independentTransforms"),
            "iterations": int(smoke_summary["iterations"]),
            "failures": 0,
            "intent": smoke_summary.get("intent"),
            "blackPointCompensation": int(smoke_summary.get("blackPointCompensation", "-1")),
            "buildPrefixAvailable": False,
        },
    }
    manifest_path = bundle_root / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
    validate_manifest(bundle_root, manifest_path)
    return manifest_path


def validate_manifest(bundle_root: pathlib.Path, manifest_path: pathlib.Path) -> None:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest["version"] != "2.19" or manifest["sourceArchive"]["sha256"] != "49e7e134e4299733dd0eda434fa468997a28ab3d33fa397c642b03644f552216":
        raise BuildFailure("Manifest source pin is invalid.")
    if manifest["smoke"]["result"] != "PASS" or manifest["smoke"]["buildPrefixAvailable"]:
        raise BuildFailure("Manifest smoke evidence is invalid.")
    for entry in manifest["runtimeFiles"]:
        path = bundle_root / manifest["runtimeDirectory"] / entry["path"]
        if entry["type"] == "symlink":
            if not path.is_symlink() or os.readlink(path) != entry["target"]:
                raise BuildFailure(f"Manifest symlink mismatch: {path}")
        elif sha256_file(path) != entry["sha256"]:
            raise BuildFailure(f"Manifest runtime hash mismatch: {path}")
    for entry in manifest["supportFiles"]:
        path = bundle_root / entry["path"]
        if sha256_file(path) != entry["sha256"]:
            raise BuildFailure(f"Manifest support-file hash mismatch: {path}")


def archive_bundle(bundle_root: pathlib.Path, rid: str, epoch: int) -> pathlib.Path:
    PACKAGES_ROOT.mkdir(parents=True, exist_ok=True)
    if rid == "win-x64":
        archive = PACKAGES_ROOT / f"fovium-lcms2-{rid}.zip"
        if archive.exists():
            archive.unlink()
        timestamp = dt.datetime.fromtimestamp(epoch, tz=dt.timezone.utc).timetuple()[:6]
        with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as output:
            for path in sorted(bundle_root.rglob("*")):
                if not path.is_file():
                    continue
                relative = pathlib.PurePosixPath(bundle_root.name) / path.relative_to(bundle_root).as_posix()
                info = zipfile.ZipInfo(str(relative), timestamp)
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = (stat.S_IFREG | 0o644) << 16
                output.writestr(info, path.read_bytes(), compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)
    else:
        archive = PACKAGES_ROOT / f"fovium-lcms2-{rid}.tar.gz"
        if archive.exists():
            archive.unlink()
        with archive.open("wb") as raw:
            with gzip.GzipFile(filename="", mode="wb", fileobj=raw, mtime=epoch, compresslevel=9) as compressed:
                with tarfile.open(fileobj=compressed, mode="w", format=tarfile.PAX_FORMAT) as output:
                    for path in [bundle_root, *sorted(bundle_root.rglob("*"))]:
                        relative = pathlib.PurePosixPath(bundle_root.name)
                        if path != bundle_root:
                            relative /= path.relative_to(bundle_root).as_posix()
                        info = output.gettarinfo(str(path), arcname=str(relative))
                        info.uid = 0
                        info.gid = 0
                        info.uname = "root"
                        info.gname = "root"
                        info.mtime = epoch
                        if info.isfile():
                            with path.open("rb") as stream:
                                output.addfile(info, stream)
                        else:
                            output.addfile(info)
    hash_path = archive.with_name(archive.name + ".sha256")
    hash_path.write_text(f"{sha256_file(archive)}  {archive.name}\n", encoding="ascii", newline="\n")
    return archive


def build(rid: str) -> None:
    versions = load_versions()
    validate_host(rid)
    require_tools(versions, rid)
    source = versions["source"]
    work = WORK_ROOT / rid
    remove_tree(work)
    source_parent = work / "source"
    build_root = work / "build"
    prefix = work / "install"
    unavailable_prefix = work / "install.unavailable-during-smoke"
    bundle_root = ARTIFACTS_ROOT / f"fovium-lcms2-{rid}"
    if bundle_root.exists():
        shutil.rmtree(bundle_root)
    source_root = acquire_source(source, source_parent)
    configure_and_install(versions, rid, source_root, build_root, prefix)
    runtime_root = bundle_root / "runtimes" / rid / "native"
    copy_runtime_files(installed_runtime_files(prefix, rid), runtime_root)
    if rid.startswith("osx-"):
        relocate_macos(runtime_root)
    smoke = compile_smoke(versions, rid, prefix, runtime_root)
    toolchain = compiler_summary(rid)

    prefix.rename(unavailable_prefix)
    if prefix.exists():
        raise BuildFailure("Build/install prefix remained available during smoke.")
    audit = audit_binaries(rid, runtime_root, smoke)
    iterations = int(versions["build"]["smokeIterations"])
    smoke_output, smoke_summary = run_smoke(smoke, runtime_root, iterations)
    smoke.unlink()
    for build_leak in runtime_root.glob("fovium-lcms2-smoke.*"):
        build_leak.unlink()
    validate_runtime_symlinks(runtime_root)
    manifest = write_artifact_metadata(
        versions,
        rid,
        bundle_root,
        runtime_root,
        source_root,
        audit,
        smoke_output,
        smoke_summary,
        toolchain,
    )
    archive = archive_bundle(bundle_root, rid, int(versions["tooling"]["sourceDateEpoch"]))
    log(f"artifact={bundle_root}")
    log(f"manifest={manifest}")
    log(f"archive={archive}")
    log(f"archive.sha256={sha256_file(archive)}")
    log(f"runtime.bytes={sum(entry.get('size', 0) for entry in runtime_inventory(runtime_root))}")
    log(f"smoke.passes={iterations}/{iterations}")


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rid", required=True, choices=["win-x64", "linux-x64", "osx-arm64", "osx-x64"])
    return parser.parse_args()


def main() -> int:
    try:
        arguments = parse_arguments()
        build(arguments.rid)
        return 0
    except (BuildFailure, OSError, subprocess.SubprocessError, json.JSONDecodeError) as error:
        print(f"[lcms2] ERROR: {error}", file=sys.stderr, flush=True)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
