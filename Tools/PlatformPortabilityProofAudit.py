#!/usr/bin/env python3
"""Static platform-portability proof audit for HECTON-8.

Evidence class: STATIC_SOURCE / PACKAGE_LOCK / FILESYSTEM. This tool reports
packages, serialized settings, payload/build artifact presence, and native
plugin surface. It does not build, import, launch, profile, or prove device
readiness.
"""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_REPORT_PATH = REPO_ROOT / "Docs" / "AgentLogs" / "PlatformPortabilityProofAudit_HFI_AUDIT.md"
DEFAULT_JSON_PATH = REPO_ROOT / "Docs" / "AgentLogs" / "PlatformPortabilityProofAudit_HFI_AUDIT.json"
SCHEMA = "hecton8.platform_portability_proof_audit.v1"

XR_PACKAGES = (
    "com.unity.xr.management",
    "com.unity.xr.openxr",
    "com.unity.xr.meta-openxr",
)

PICO_PACKAGE_TOKENS = (
    "pico",
    "picoxr",
)

NATIVE_PLUGIN_EXTENSIONS = {".dll", ".so", ".dylib", ".bundle", ".aar", ".jar"}


def normalize_path(path: Path, repo_root: Path = REPO_ROOT) -> str:
    try:
        return path.resolve().relative_to(repo_root.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def read_text(path: Path) -> str:
    if not path.exists():
        return ""
    return path.read_text(encoding="utf-8", errors="ignore")


def load_json(path: Path) -> dict[str, object]:
    if not path.exists():
        return {}
    try:
        payload = json.loads(path.read_text(encoding="utf-8", errors="ignore"))
    except json.JSONDecodeError:
        return {}
    return payload if isinstance(payload, dict) else {}


def package_dependencies(path: Path) -> dict[str, object]:
    payload = load_json(path)
    deps = payload.get("dependencies")
    return deps if isinstance(deps, dict) else {}


def package_version(value: object) -> str:
    if isinstance(value, str):
        return value
    if isinstance(value, dict):
        version = value.get("version")
        return str(version) if version is not None else ""
    return ""


def package_surface(root: Path) -> dict[str, object]:
    manifest_deps = package_dependencies(root / "Packages" / "manifest.json")
    lock_deps = package_dependencies(root / "Packages" / "packages-lock.json")
    all_names = sorted(set(manifest_deps) | set(lock_deps))
    xr = {
        name: {
            "manifest": name in manifest_deps,
            "lock": name in lock_deps,
            "manifestVersion": package_version(manifest_deps.get(name)),
            "lockVersion": package_version(lock_deps.get(name)),
        }
        for name in XR_PACKAGES
    }
    pico = [
        name
        for name in all_names
        if any(token in name.lower() for token in PICO_PACKAGE_TOKENS)
    ]
    return {
        "manifestDependencyCount": len(manifest_deps),
        "lockDependencyCount": len(lock_deps),
        "xrPackages": xr,
        "allRequiredXrPackagesInManifest": all(item["manifest"] for item in xr.values()),
        "allRequiredXrPackagesInLock": all(item["lock"] for item in xr.values()),
        "picoPackageCandidates": pico,
    }


def regex_int(text: str, pattern: str) -> int | None:
    match = re.search(pattern, text)
    if not match:
        return None
    try:
        return int(match.group(1))
    except ValueError:
        return None


def regex_string(text: str, pattern: str) -> str:
    match = re.search(pattern, text)
    return match.group(1).strip() if match else ""


def project_settings_surface(root: Path) -> dict[str, object]:
    text = read_text(root / "ProjectSettings" / "ProjectSettings.asset")
    xr_text = read_text(root / "ProjectSettings" / "XRSettings.asset")
    android_arch = regex_int(text, r"\bAndroidTargetArchitectures:\s*(-?\d+)")
    android_backend = regex_int(text, r"\bscriptingBackend:\s*(?:\r?\n\s+[A-Za-z]+:\s+\d+)*\r?\n\s+Android:\s*(-?\d+)")
    if android_backend is None:
        android_backend = regex_int(text, r"\bAndroid:\s*(-?\d+)\s*(?:\r?\n\s+il2cppCompilerConfiguration:)")
    android_target_sdk = regex_int(text, r"\bAndroidTargetSdkVersion:\s*(-?\d+)")
    android_min_sdk = regex_int(text, r"\bAndroidMinSdkVersion:\s*(-?\d+)")
    android_identifier = regex_string(text, r"\bapplicationIdentifier:\s*(?:\r?\n\s+[A-Za-z]+:\s+[^\r\n]*)*\r?\n\s+Android:\s*([^\r\n]+)")
    build_target_vr_empty = bool(re.search(r"\bm_BuildTargetVRSettings:\s*\[\]", text))
    xr_legacy_disabled_false = '"VR Device Disabled"' in xr_text and '"False"' in xr_text
    return {
        "projectSettingsPresent": bool(text),
        "xrSettingsPresent": bool(xr_text),
        "androidTargetArchitectures": android_arch,
        "androidArm64OnlySerialized": android_arch == 2,
        "androidScriptingBackend": android_backend,
        "androidIl2CppSerialized": android_backend == 1,
        "androidTargetSdkVersion": android_target_sdk,
        "androidMinSdkVersion": android_min_sdk,
        "androidApplicationIdentifier": android_identifier,
        "buildTargetVrSettingsEmpty": build_target_vr_empty,
        "xrLegacyDisabledFalse": xr_legacy_disabled_false,
        "xrProviderSerializedProof": not build_target_vr_empty,
    }


def count_files(root: Path, relative: str, ignore_meta: bool = False) -> dict[str, object]:
    path = root / relative
    files: list[Path] = []
    if path.exists():
        files = [
            item
            for item in path.rglob("*")
            if item.is_file() and (not ignore_meta or item.suffix != ".meta")
        ]
    return {
        "path": normalize_path(path),
        "exists": path.exists(),
        "fileCount": len(files),
        "sampleFiles": [normalize_path(item) for item in files[:20]],
    }


def artifact_surface(root: Path) -> dict[str, object]:
    monolith = root / "Assets" / "StreamingAssets" / "Hecton8" / "DataMonolith" / "static_data.h8bin"
    builds = count_files(root, "Builds")
    build_result_logs = sorted((root / "Docs" / "AgentLogs").glob("Build_Result_*.txt"))
    return {
        "addressables": count_files(root, "Assets/AddressableAssetsData", ignore_meta=True),
        "dataMonolith": {
            "path": normalize_path(monolith),
            "exists": monolith.exists(),
            "bytes": monolith.stat().st_size if monolith.exists() else 0,
        },
        "builds": builds,
        "buildResultLogCount": len(build_result_logs),
        "buildResultLogs": [normalize_path(item) for item in build_result_logs[:20]],
    }


def classify_native_plugin(path: Path) -> str:
    lower_parts = [part.lower() for part in path.parts]
    suffix = path.suffix.lower()
    if suffix in {".aar", ".jar"}:
        return "androidArchiveOrJar"
    if suffix == ".so":
        return "linuxOrAndroidSo"
    if suffix in {".dylib", ".bundle"}:
        return "macos"
    if suffix == ".dll":
        if any(part in {"editor", "roslyn"} for part in lower_parts):
            return "editorOrManagedDll"
        if any(part in {"windows", "win", "x86_64"} for part in lower_parts):
            return "windowsNativeOrManagedDll"
        return "managedOrUnknownDll"
    return "unknown"


def native_plugin_surface(root: Path) -> dict[str, object]:
    search_roots = [root / "Assets" / "Plugins", root / "Assets" / "_Project" / "Plugins"]
    files: list[Path] = []
    for search_root in search_roots:
        if search_root.exists():
            files.extend(
                item
                for item in search_root.rglob("*")
                if item.is_file() and item.suffix.lower() in NATIVE_PLUGIN_EXTENSIONS
            )
    files = sorted(files)
    by_ext: dict[str, int] = {}
    by_class: dict[str, int] = {}
    first_party: list[Path] = []
    for item in files:
        ext = item.suffix.lower()
        by_ext[ext] = by_ext.get(ext, 0) + 1
        klass = classify_native_plugin(item)
        by_class[klass] = by_class.get(klass, 0) + 1
        name = item.name.lower()
        if name.startswith("hecton") or name.startswith("liblz4"):
            first_party.append(item)
    return {
        "pluginFileCount": len(files),
        "byExtension": dict(sorted(by_ext.items())),
        "byClass": dict(sorted(by_class.items())),
        "firstPartyOrRuntimeCritical": [normalize_path(item) for item in first_party],
        "sampleFiles": [normalize_path(item) for item in files[:40]],
    }


def readiness_surface(packages: dict[str, object], settings: dict[str, object], artifacts: dict[str, object]) -> dict[str, object]:
    android_scaffold = bool(
        packages["allRequiredXrPackagesInManifest"]
        and packages["allRequiredXrPackagesInLock"]
        and settings["androidArm64OnlySerialized"]
        and settings["androidIl2CppSerialized"]
        and settings["androidTargetSdkVersion"]
    )
    addressables = artifacts["addressables"]
    monolith = artifacts["dataMonolith"]
    builds = artifacts["builds"]
    if not isinstance(addressables, dict) or not isinstance(monolith, dict) or not isinstance(builds, dict):
        raise TypeError("artifact payload malformed")
    return {
        "androidQuestScaffold": android_scaffold,
        "xrProviderSerializedProof": bool(settings["xrProviderSerializedProof"]),
        "addressablesContentPresent": int(addressables["fileCount"]) > 0,
        "dataMonolithPresent": bool(monolith["exists"]),
        "buildArtifactPresent": int(builds["fileCount"]) > 0 or int(artifacts["buildResultLogCount"]) > 0,
        "picoPackagePresent": bool(packages["picoPackageCandidates"]),
    }


def build_payload(root: Path) -> dict[str, object]:
    packages = package_surface(root)
    settings = project_settings_surface(root)
    artifacts = artifact_surface(root)
    native_plugins = native_plugin_surface(root)
    readiness = readiness_surface(packages, settings, artifacts)
    return {
        "schema": SCHEMA,
        "root": normalize_path(root),
        "packages": packages,
        "projectSettings": settings,
        "artifacts": artifacts,
        "nativePlugins": native_plugins,
        "readiness": readiness,
    }


def write_json(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def yes_no(value: object) -> str:
    return "yes" if bool(value) else "no"


def write_markdown(path: Path, payload: dict[str, object]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    packages = payload["packages"]
    settings = payload["projectSettings"]
    artifacts = payload["artifacts"]
    native_plugins = payload["nativePlugins"]
    readiness = payload["readiness"]
    if not all(isinstance(item, dict) for item in (packages, settings, artifacts, native_plugins, readiness)):
        raise TypeError("payload malformed")
    addressables = artifacts["addressables"]
    monolith = artifacts["dataMonolith"]
    builds = artifacts["builds"]
    lines = [
        "# Platform Portability Proof Audit",
        "",
        "Evidence class: STATIC_SOURCE / PACKAGE_LOCK / FILESYSTEM. No Unity import, player build, install, launch, profiler, GC, memory, shader, headset, Deck, macOS, Linux, or console proof was executed.",
        "",
        f"- Schema: `{payload['schema']}`",
        f"- Root: `{payload['root']}`",
        "",
        "## Package/XR Surface",
        "",
        f"- Required XR packages in manifest: `{yes_no(packages['allRequiredXrPackagesInManifest'])}`",
        f"- Required XR packages in lock: `{yes_no(packages['allRequiredXrPackagesInLock'])}`",
        f"- PICO package candidates: `{len(packages['picoPackageCandidates'])}`",
        "",
        "| Package | Manifest | Lock | Manifest Version | Lock Version |",
        "|---|---|---|---|---|",
    ]
    xr_packages = packages["xrPackages"]
    if isinstance(xr_packages, dict):
        for name, item in xr_packages.items():
            if isinstance(item, dict):
                lines.append(
                    f"| `{name}` | `{yes_no(item['manifest'])}` | `{yes_no(item['lock'])}` | "
                    f"`{item['manifestVersion']}` | `{item['lockVersion']}` |"
                )
    lines.extend(
        [
            "",
            "## Android/XR Settings",
            "",
            f"- Android application id: `{settings['androidApplicationIdentifier']}`",
            f"- Android target SDK: `{settings['androidTargetSdkVersion']}`",
            f"- Android min SDK: `{settings['androidMinSdkVersion']}`",
            f"- Android ARM64-only serialized value: `{settings['androidTargetArchitectures']}` / `{yes_no(settings['androidArm64OnlySerialized'])}`",
            f"- Android IL2CPP serialized value: `{settings['androidScriptingBackend']}` / `{yes_no(settings['androidIl2CppSerialized'])}`",
            f"- `m_BuildTargetVRSettings` empty: `{yes_no(settings['buildTargetVrSettingsEmpty'])}`",
            f"- XR provider serialized proof: `{yes_no(settings['xrProviderSerializedProof'])}`",
            "",
            "## Payload / Build Artifacts",
            "",
            f"- Addressables data path: `{addressables['path']}`, files: `{addressables['fileCount']}`",
            f"- Data Monolith path: `{monolith['path']}`, exists: `{yes_no(monolith['exists'])}`, bytes: `{monolith['bytes']}`",
            f"- Builds path: `{builds['path']}`, exists: `{yes_no(builds['exists'])}`, files: `{builds['fileCount']}`",
            f"- Build result logs: `{artifacts['buildResultLogCount']}`",
            "",
            "## Native Plugin Surface",
            "",
            f"- Plugin files: `{native_plugins['pluginFileCount']}`",
            f"- By extension: `{native_plugins['byExtension']}`",
            f"- By class: `{native_plugins['byClass']}`",
            "",
        ]
    )
    first_party = native_plugins["firstPartyOrRuntimeCritical"]
    if first_party:
        lines.append("First-party/runtime-critical candidates:")
        lines.append("")
        for item in first_party:
            lines.append(f"- `{item}`")
        lines.append("")

    lines.extend(
        [
            "## Readiness Flags",
            "",
            "| Flag | Value |",
            "|---|---|",
        ]
    )
    for key in sorted(readiness):
        lines.append(f"| `{key}` | `{yes_no(readiness[key])}` |")
    lines.extend(
        [
            "",
            "## Interpretation",
            "",
            "- Quest/Android scaffold exists only if XR packages, ARM64, IL2CPP, and target SDK settings are present. That is not headset readiness.",
            "- Empty `m_BuildTargetVRSettings`, missing Addressables data, missing Data Monolith, and missing build artifacts block any GREEN platform claim.",
            "- Native plugin parity is unresolved until Windows, Linux/Deck, macOS, Android/Quest, and PCVR player builds prove load behavior on target hardware.",
            "- This audit is a no-claim gate. It prevents package/settings text from being inflated into runtime proof.",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def hard_failures(payload: dict[str, object], args: argparse.Namespace) -> list[str]:
    readiness = payload["readiness"]
    if not isinstance(readiness, dict):
        raise TypeError("readiness payload malformed")
    failures: list[str] = []
    checks = (
        ("fail_on_missing_xr_provider", "xrProviderSerializedProof", "missing XR provider serialized proof"),
        ("fail_on_missing_addressables", "addressablesContentPresent", "missing Addressables content"),
        ("fail_on_missing_data_monolith", "dataMonolithPresent", "missing Data Monolith payload"),
        ("fail_on_missing_build_artifact", "buildArtifactPresent", "missing build artifact/log proof"),
    )
    for flag, key, message in checks:
        if getattr(args, flag) and not bool(readiness[key]):
            failures.append(message)
    return failures


def print_text(payload: dict[str, object], failures: list[str]) -> None:
    packages = payload["packages"]
    settings = payload["projectSettings"]
    artifacts = payload["artifacts"]
    native_plugins = payload["nativePlugins"]
    readiness = payload["readiness"]
    print("Platform portability proof audit")
    print(f"schema={payload['schema']}")
    print(f"root={payload['root']}")
    if isinstance(packages, dict):
        print(f"xrPackagesManifest={packages['allRequiredXrPackagesInManifest']}")
        print(f"xrPackagesLock={packages['allRequiredXrPackagesInLock']}")
        print(f"picoPackageCandidates={len(packages['picoPackageCandidates'])}")
    if isinstance(settings, dict):
        print(f"androidArm64Only={settings['androidArm64OnlySerialized']}")
        print(f"androidIl2Cpp={settings['androidIl2CppSerialized']}")
        print(f"androidTargetSdk={settings['androidTargetSdkVersion']}")
        print(f"buildTargetVrSettingsEmpty={settings['buildTargetVrSettingsEmpty']}")
        print(f"xrProviderSerializedProof={settings['xrProviderSerializedProof']}")
    if isinstance(artifacts, dict):
        addressables = artifacts["addressables"]
        monolith = artifacts["dataMonolith"]
        builds = artifacts["builds"]
        if isinstance(addressables, dict) and isinstance(monolith, dict) and isinstance(builds, dict):
            print(f"addressablesFiles={addressables['fileCount']}")
            print(f"dataMonolithExists={monolith['exists']}")
            print(f"buildFiles={builds['fileCount']}")
            print(f"buildResultLogs={artifacts['buildResultLogCount']}")
    if isinstance(native_plugins, dict):
        print(f"nativePluginFiles={native_plugins['pluginFileCount']}")
        print(f"nativePluginClasses={native_plugins['byClass']}")
    if isinstance(readiness, dict):
        for key in sorted(readiness):
            print(f"{key}={readiness[key]}")
    if failures:
        print("status=FAIL")
        for failure in failures:
            print(f"failure={failure}")
    else:
        print("status=PASS_WITH_WARNINGS")


def run(args: argparse.Namespace) -> int:
    payload = build_payload(Path(args.root))
    write_json(Path(args.json_path), payload)
    write_markdown(Path(args.report_path), payload)
    failures = hard_failures(payload, args)
    if args.json:
        print(json.dumps(payload | {"failures": failures}, indent=2, sort_keys=True))
    else:
        print_text(payload, failures)
    return 1 if failures else 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=str(REPO_ROOT))
    parser.add_argument("--report-path", default=str(DEFAULT_REPORT_PATH))
    parser.add_argument("--json-path", default=str(DEFAULT_JSON_PATH))
    parser.add_argument("--json", action="store_true", help="Print JSON payload to stdout.")
    parser.add_argument("--fail-on-missing-xr-provider", action="store_true")
    parser.add_argument("--fail-on-missing-addressables", action="store_true")
    parser.add_argument("--fail-on-missing-data-monolith", action="store_true")
    parser.add_argument("--fail-on-missing-build-artifact", action="store_true")
    return parser


def main() -> int:
    return run(build_parser().parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
