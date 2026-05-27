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
SCHEMA = "hecton8.platform_portability_proof_audit.v14"

XR_PACKAGES = (
    "com.unity.xr.management",
    "com.unity.xr.openxr",
    "com.unity.xr.meta-openxr",
)

ADDRESSABLES_PACKAGE = "com.unity.addressables"

PICO_PACKAGE_TOKENS = (
    "pico",
    "picoxr",
)

NATIVE_PLUGIN_EXTENSIONS = {".dll", ".so", ".dylib", ".bundle", ".aar", ".jar"}
SHADER_SOURCE_EXTENSIONS = {".shader", ".compute", ".hlsl"}
COMPUTE_REFERENCE_EXTENSIONS = {
    ".asset",
    ".controller",
    ".cs",
    ".mat",
    ".overridecontroller",
    ".playable",
    ".prefab",
    ".unity",
}
COMPUTE_SERIALIZED_REFERENCE_EXTENSIONS = {
    ".asset",
    ".controller",
    ".mat",
    ".overridecontroller",
    ".playable",
    ".prefab",
    ".unity",
}
MAX_COMPUTE_REFERENCE_FILE_BYTES = 2_000_000
COMPUTE_REFERENCE_SKIP_DIRECTORIES = {"_recovery"}
RISKY_COMPUTE_THREAD_GROUP_THRESHOLD = 64
QUEST_URP_ASSET = "Assets/_Project/Data/URP_Quest_VR.asset"
COMPUTE_DISPATCH_COMMANDBUFFER_PATTERN = re.compile(r"\.\s*DispatchCompute\s*\(")
COMPUTE_DISPATCH_SHADER_PATTERN = re.compile(r"\.\s*Dispatch\s*\(")
COMPUTE_THREAD_GROUP_QUERY_PATTERN = re.compile(r"\.\s*GetKernelThreadGroupSizes\s*\(")
VENDOR_ASSET_PREFIXES = (
    "Assets/Bakery/",
    "Assets/Crest/",
    "Assets/Editor/x64/Bakery/",
    "Assets/GPUInstancer/",
)
FIRST_PARTY_ASSET_PREFIX = "Assets/_Project/"
DISPATCH_PAYLOAD_GROUP_PATTERN = re.compile(
    r"\b[A-Za-z_][A-Za-z0-9_\.]*\.[A-Za-z0-9_]*DispatchGroups[A-Za-z0-9_]*\b"
)


def normalize_path(path: Path, repo_root: Path = REPO_ROOT) -> str:
    try:
        return path.resolve().relative_to(repo_root.resolve()).as_posix()
    except ValueError:
        return path.as_posix()


def read_text(path: Path) -> str:
    if not path.exists():
        return ""
    return path.read_text(encoding="utf-8", errors="ignore")


def path_owner(path: Path, root: Path) -> str:
    relative = normalize_path(path, root)
    if relative.startswith(FIRST_PARTY_ASSET_PREFIX):
        return "FirstParty"
    if relative.startswith(VENDOR_ASSET_PREFIXES):
        return "Vendor"
    if relative.startswith("Assets/"):
        return "ExternalAsset"
    return "ProjectSettings"


def is_first_party_path(path: Path, root: Path) -> bool:
    return path_owner(path, root) == "FirstParty"


def dispatch_expression_for_match(text: str, match: re.Match[str]) -> str:
    line_start = text.rfind("\n", 0, match.start()) + 1
    line_end = text.find("\n", match.start())
    if line_end < 0:
        line_end = len(text)
    expression = text[line_start:line_end].strip()
    balance = expression.count("(") - expression.count(")")
    cursor = line_end + 1
    line_count = 1
    while balance > 0 and cursor < len(text) and line_count < 12:
        next_end = text.find("\n", cursor)
        if next_end < 0:
            next_end = len(text)
        part = text[cursor:next_end].strip()
        expression += " " + part
        balance += part.count("(") - part.count(")")
        cursor = next_end + 1
        line_count += 1
    return expression


def is_payload_sized_dispatch_call(text: str, match: re.Match[str]) -> bool:
    return bool(DISPATCH_PAYLOAD_GROUP_PATTERN.search(dispatch_expression_for_match(text, match)))


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
    addressables = {
        "manifest": ADDRESSABLES_PACKAGE in manifest_deps,
        "lock": ADDRESSABLES_PACKAGE in lock_deps,
        "manifestVersion": package_version(manifest_deps.get(ADDRESSABLES_PACKAGE)),
        "lockVersion": package_version(lock_deps.get(ADDRESSABLES_PACKAGE)),
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
        "addressablesPackage": addressables,
        "addressablesPackageInManifest": bool(addressables["manifest"]),
        "addressablesPackageInLock": bool(addressables["lock"]),
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


def asset_guid(path: Path) -> str:
    meta = path.with_suffix(path.suffix + ".meta")
    return regex_string(read_text(meta), r"\bguid:\s*([0-9a-fA-F]+)")


def guid_is_referenced(text: str, guid: str) -> bool:
    return bool(guid) and guid.lower() in text.lower()


def serialized_guid_reference_samples(root: Path, guid: str, skip_paths: set[Path]) -> list[str]:
    if not guid:
        return []
    samples: list[str] = []
    search_roots = (root / "ProjectSettings", root / "Assets" / "XR")
    for search_root in search_roots:
        if not search_root.exists():
            continue
        for path in sorted(item for item in search_root.rglob("*") if item.is_file() and item.suffix.lower() == ".asset"):
            resolved = path.resolve()
            if resolved in skip_paths:
                continue
            if guid_is_referenced(read_text(path), guid):
                samples.append(normalize_path(path, root))
    return samples


def yaml_object_block(text: str, name: str) -> str:
    marker = f"m_Name: {name}"
    index = text.find(marker)
    if index < 0:
        return ""
    next_object = text.find("\n--- !u!", index + len(marker))
    return text[index:] if next_object < 0 else text[index:next_object]


def xr_management_surface(root: Path) -> dict[str, object]:
    editor_build_text = read_text(root / "ProjectSettings" / "EditorBuildSettings.asset")
    openxr_loader = root / "Assets" / "XR" / "Loaders" / "OpenXRLoader.asset"
    openxr_loader_text = read_text(openxr_loader)
    openxr_loader_guid = asset_guid(openxr_loader)
    openxr_settings = root / "Assets" / "XR" / "Settings" / "OpenXR Package Settings.asset"
    openxr_settings_text = read_text(openxr_settings)
    openxr_settings_guid = asset_guid(openxr_settings)
    loader_reference_samples = serialized_guid_reference_samples(root, openxr_loader_guid, {openxr_loader.resolve()})
    meta_quest_block = yaml_object_block(openxr_settings_text, "MetaQuestFeature Android")
    oculus_quest_block = yaml_object_block(openxr_settings_text, "OculusQuestFeature Android")
    quest_feature_blocks = [block for block in (meta_quest_block, oculus_quest_block) if block]
    return {
        "xrManagementOpenXrSettingsPath": normalize_path(openxr_settings, root),
        "xrManagementOpenXrSettingsGuid": openxr_settings_guid,
        "xrManagementOpenXrSettingsAssetPresent": "UnityEngine.XR.OpenXR.OpenXRSettings" in openxr_settings_text,
        "xrManagementOpenXrSettingsRegistered": guid_is_referenced(editor_build_text, openxr_settings_guid),
        "xrManagementOpenXrLoaderPath": normalize_path(openxr_loader, root),
        "xrManagementOpenXrLoaderGuid": openxr_loader_guid,
        "xrManagementOpenXrLoaderAssetPresent": "UnityEngine.XR.OpenXR.OpenXRLoader" in openxr_loader_text,
        "xrManagementOpenXrLoaderGuidReferenceCount": len(loader_reference_samples),
        "xrManagementOpenXrLoaderGuidReferenceSamples": loader_reference_samples[:20],
        "xrManagementSinglePassInstancedSerialized": "m_renderMode: 1" in openxr_settings_text,
        "xrManagementQuestFeaturePresent": bool(quest_feature_blocks),
        "xrManagementQuestFeatureEnabled": any("m_enabled: 1" in block for block in quest_feature_blocks),
    }


def android_graphics_api_surface(text: str) -> dict[str, object]:
    match = re.search(
        r"-\s*m_BuildTarget:\s*AndroidPlayer\s*\r?\n\s*m_APIs:\s*([0-9a-fA-F]+)\s*\r?\n\s*m_Automatic:\s*(-?\d+)",
        text,
    )
    raw = match.group(1) if match else ""
    automatic = int(match.group(2)) if match else None
    return {
        "androidGraphicsApisRaw": raw,
        "androidGraphicsAutomatic": automatic,
        "androidVulkanOnlySerialized": raw == "15000000" and automatic == 0,
    }


def project_settings_surface(root: Path) -> dict[str, object]:
    text = read_text(root / "ProjectSettings" / "ProjectSettings.asset")
    xr_text = read_text(root / "ProjectSettings" / "XRSettings.asset")
    xr_management = xr_management_surface(root)
    xr_validator_text = read_text(
        root / "Assets" / "_Project" / "Scripts" / "Editor" / "Build" / "XrPlatformReadinessValidator.cs"
    )
    route_repairer_text = read_text(
        root / "Assets" / "_Project" / "Scripts" / "Editor" / "Build" / "PlatformPortabilityRouteRepairer.cs"
    )
    android_arch = regex_int(text, r"\bAndroidTargetArchitectures:\s*(-?\d+)")
    android_backend = regex_int(text, r"\bscriptingBackend:\s*(?:\r?\n\s+[A-Za-z]+:\s+\d+)*\r?\n\s+Android:\s*(-?\d+)")
    if android_backend is None:
        android_backend = regex_int(text, r"\bAndroid:\s*(-?\d+)\s*(?:\r?\n\s+il2cppCompilerConfiguration:)")
    android_target_sdk = regex_int(text, r"\bAndroidTargetSdkVersion:\s*(-?\d+)")
    android_min_sdk = regex_int(text, r"\bAndroidMinSdkVersion:\s*(-?\d+)")
    android_sustained = regex_int(text, r"\bAndroidEnableSustainedPerformanceMode:\s*(-?\d+)")
    android_identifier = regex_string(text, r"\bapplicationIdentifier:\s*(?:\r?\n\s+[A-Za-z]+:\s+[^\r\n]*)*\r?\n\s+Android:\s*([^\r\n]+)")
    build_target_vr_empty = bool(re.search(r"\bm_BuildTargetVRSettings:\s*\[\]", text))
    xr_legacy_provider_proof = bool(text and "m_BuildTargetVRSettings:" in text and not build_target_vr_empty)
    xr_management_provider_proof = bool(
        xr_management["xrManagementOpenXrSettingsAssetPresent"]
        and xr_management["xrManagementOpenXrSettingsRegistered"]
        and xr_management["xrManagementOpenXrLoaderAssetPresent"]
        and int(xr_management["xrManagementOpenXrLoaderGuidReferenceCount"]) > 0
    )
    xr_legacy_disabled_false = '"VR Device Disabled"' in xr_text and '"False"' in xr_text
    xr_provider_route_fixer = (
        "WireAndroidOpenXrProviderRouteForCi" in xr_validator_text
        and "XRPackageMetadataStore.AssignLoader" in xr_validator_text
        and "XRGeneralSettingsPerBuildTarget" in xr_validator_text
        and "CreateDefaultManagerSettingsForBuildTarget" in xr_validator_text
        and "OpenXRSettings.GetSettingsForBuildTargetGroup" in xr_validator_text
        and "UnityEngine.XR.OpenXR.OpenXRLoader" in xr_validator_text
        and "SinglePassInstanced" in xr_validator_text
    )
    xr_provider_route_validator = (
        "ValidateOpenXrProviderRoute" in xr_validator_text
        and "HasOpenXrProviderRoute" in xr_validator_text
        and "HasOpenXrLoader" in xr_validator_text
        and "activeLoaders" in xr_validator_text
        and "m_BuildTargetVRSettings: []" in xr_validator_text
    )
    android_quest_xr_route_repairer = (
        "WireAndroidQuestXrRoutesForCi" in route_repairer_text
        and "ConfigureQuestAssetsForCi" in route_repairer_text
        and "WireQuestAndroidQualityRouteForCi" in route_repairer_text
        and "WireAndroidOpenXrProviderRouteForCi" in route_repairer_text
        and "ValidateAndroidXrReadinessForCi" in route_repairer_text
        and "AssetDatabase.SaveAssets" in route_repairer_text
    )
    surface = {
        "projectSettingsPresent": bool(text),
        "xrSettingsPresent": bool(xr_text),
        "xrReadinessValidatorPresent": bool(xr_validator_text),
        "androidQuestXrRouteRepairerPresent": android_quest_xr_route_repairer,
        "androidTargetArchitectures": android_arch,
        "androidArm64OnlySerialized": android_arch == 2,
        "androidScriptingBackend": android_backend,
        "androidIl2CppSerialized": android_backend == 1,
        "androidTargetSdkVersion": android_target_sdk,
        "androidMinSdkVersion": android_min_sdk,
        "androidSustainedPerformanceMode": android_sustained,
        "androidSustainedPerformanceEnabled": android_sustained == 1,
        "androidApplicationIdentifier": android_identifier,
        "buildTargetVrSettingsEmpty": build_target_vr_empty,
        "xrLegacyDisabledFalse": xr_legacy_disabled_false,
        "xrLegacyProviderSerializedProof": xr_legacy_provider_proof,
        "xrManagementProviderSerializedProof": xr_management_provider_proof,
        "xrProviderSerializedProof": xr_legacy_provider_proof or xr_management_provider_proof,
        "xrProviderRouteFixerPresent": xr_provider_route_fixer,
        "xrProviderRouteValidatorPresent": xr_provider_route_validator,
    }
    surface.update(xr_management)
    surface.update(android_graphics_api_surface(text))
    return surface


def quality_pipeline_surface(root: Path) -> dict[str, object]:
    quality_text = read_text(root / "ProjectSettings" / "QualitySettings.asset")
    graphics_text = read_text(root / "ProjectSettings" / "GraphicsSettings.asset")
    project_text = read_text(root / "ProjectSettings" / "ProjectSettings.asset")
    configurator_text = read_text(root / "Assets" / "_Project" / "Scripts" / "Editor" / "Build" / "QuestVulkanRenderPipelineConfigurator.cs")
    quest_asset = root / QUEST_URP_ASSET
    quest_guid = asset_guid(quest_asset)
    quality_rows: list[dict[str, object]] = []
    quality_region = quality_text.split("m_TextureMipmapLimitGroupNames:", 1)[0]
    for match in re.finditer(r"\n\s+-\s+serializedVersion:\s*\d+(.*?)(?=\n\s+-\s+serializedVersion:|\Z)", quality_region, re.S):
        block = match.group(1)
        name = regex_string(block, r"\bname:\s*([^\r\n]+)")
        guid = regex_string(block, r"\bcustomRenderPipeline:\s*\{[^}]*guid:\s*([0-9a-fA-F]+)")
        quality_rows.append({"index": len(quality_rows), "name": name, "renderPipelineGuid": guid})
    android_default = regex_int(quality_text, r"\bm_PerPlatformDefaultQuality:\s*(?:\r?\n\s+[^\r\n]+)*\r?\n\s+Android:\s*(-?\d+)")
    android_guid = ""
    if android_default is not None and 0 <= android_default < len(quality_rows):
        android_guid = str(quality_rows[android_default]["renderPipelineGuid"])
    return {
        "questUrpAssetPath": QUEST_URP_ASSET,
        "questUrpAssetPresent": quest_asset.exists(),
        "questUrpGuid": quest_guid,
        "qualitySettingCount": len(quality_rows),
        "qualityRows": quality_rows,
        "androidDefaultQualityIndex": android_default,
        "androidDefaultQualityRenderPipelineGuid": android_guid,
        "questUrpReferencedInQualitySettings": bool(quest_guid and quest_guid in quality_text),
        "questUrpReferencedInGraphicsSettings": bool(quest_guid and quest_guid in graphics_text),
        "questUrpReferencedInProjectSettings": bool(quest_guid and quest_guid in project_text),
        "androidDefaultQualityUsesQuestUrp": bool(quest_guid and android_guid == quest_guid),
        "questConfiguratorPresent": bool(configurator_text),
        "questConfiguratorQualityRouteAuditPresent": "AppendQualityRouteAudit" in configurator_text
        and "m_PerPlatformDefaultQuality" in configurator_text
        and "customRenderPipeline" in configurator_text
        and QUEST_URP_ASSET in configurator_text,
        "questConfiguratorQualityRouteFixerPresent": "WireQuestAndroidQualityRouteForCi" in configurator_text
        and "QualitySettings.GetQualitySettings" in configurator_text
        and "TryIncludePlatformAt" in configurator_text
        and "TryExcludePlatformAt" in configurator_text
        and "m_PerPlatformDefaultQuality" in configurator_text
        and "Quest (VR)" in configurator_text,
    }


def count_preloaded_shader_entries(graphics_text: str) -> int:
    match = re.search(r"\bm_PreloadedShaders:\s*(.*?)(?:\r?\n\s*m_[A-Za-z]|\Z)", graphics_text, re.S)
    if not match:
        return 0
    return len(re.findall(r"(?:^|\n)\s*-\s*\{", match.group(1)))


def shader_warmup_surface(root: Path) -> dict[str, object]:
    graphics_text = read_text(root / "ProjectSettings" / "GraphicsSettings.asset")
    bootstrap_text = read_text(root / "Assets" / "_Project" / "Scripts" / "Bootstrap" / "GameBootstrapper.cs")
    shader_sources: list[Path] = []
    shader_variant_collections: list[Path] = []
    shader_feature_count = 0
    target_45_or_higher = 0
    target_50_or_higher = 0
    assets = root / "Assets"
    if assets.exists():
        for path in assets.rglob("*"):
            if not path.is_file():
                continue
            if any(part.lower() in COMPUTE_REFERENCE_SKIP_DIRECTORIES for part in path.parts):
                continue
            suffix = path.suffix.lower()
            if suffix in SHADER_SOURCE_EXTENSIONS:
                shader_sources.append(path)
                text = read_text(path)
                shader_feature_count += len(re.findall(r"^\s*#pragma\s+(?:multi_compile|shader_feature)\b", text, re.M))
                for target in re.findall(r"^\s*#pragma\s+target\s+([0-9.]+)", text, re.M):
                    try:
                        value = float(target)
                    except ValueError:
                        continue
                    if value >= 4.5:
                        target_45_or_higher += 1
                    if value >= 5.0:
                        target_50_or_higher += 1
            elif suffix == ".shadervariants":
                shader_variant_collections.append(path)
            elif suffix == ".asset":
                if path.stat().st_size > MAX_COMPUTE_REFERENCE_FILE_BYTES:
                    continue
                text = read_text(path)
                if "ShaderVariantCollection" in text:
                    shader_variant_collections.append(path)
    shader_variant_collections = sorted(set(shader_variant_collections))
    return {
        "preloadedShaderEntries": count_preloaded_shader_entries(graphics_text),
        "shaderVariantCollectionFiles": len(shader_variant_collections),
        "shaderVariantCollectionSamples": [normalize_path(item, root) for item in shader_variant_collections[:20]],
        "bootstrapShaderCollectionFieldPresent": "ShaderVariantCollection[]" in bootstrap_text,
        "bootstrapExplicitWarmUpCallCount": len(re.findall(r"\.WarmUp\s*\(", bootstrap_text)),
        "bootstrapShaderWarmupFromCollectionCallCount": len(re.findall(r"\bShaderWarmup\.WarmupShaderFromCollection\s*\(", bootstrap_text)),
        "bootstrapGraphicsStateWarmUpProgressivelyCallCount": len(re.findall(r"\.WarmUpProgressively\s*\(", bootstrap_text)),
        "bootstrapIsWarmedUpReadCount": len(re.findall(r"\.isWarmedUp\b", bootstrap_text)),
        "warmupAllShadersCallSites": len(re.findall(r"\bShader\.WarmupAllShaders\s*\(", bootstrap_text)),
        "shaderSourceFiles": len(shader_sources),
        "shaderFeaturePragmaCount": shader_feature_count,
        "shaderTarget45OrHigherPragmas": target_45_or_higher,
        "shaderTarget50OrHigherPragmas": target_50_or_higher,
    }


def parse_numthreads_args(value: str) -> tuple[int | None, str]:
    product = 1
    normalized: list[str] = []
    for raw in value.split(","):
        token = raw.strip()
        normalized.append(token)
        if not re.fullmatch(r"\d+", token):
            return None, ", ".join(normalized)
        product *= int(token)
    return product, ", ".join(normalized)


def execution_surface_for_path(path: Path, root: Path) -> str:
    normalized = normalize_path(path, root).replace("\\", "/")
    parts = [part.lower() for part in normalized.split("/")]
    if "editor" in parts:
        return "Editor"
    if "tests" in parts or "test" in parts:
        return "Test"
    if not normalized.startswith("Assets/"):
        return "External"
    return "Runtime"


def line_number_for_index(text: str, index: int) -> int:
    return text.count("\n", 0, max(index, 0)) + 1


def iter_compute_reference_files(root: Path) -> list[Path]:
    assets = root / "Assets"
    if not assets.exists():
        return []
    return sorted(
        path
        for path in assets.rglob("*")
        if path.is_file() and path.suffix.lower() in COMPUTE_REFERENCE_EXTENSIONS
        and not any(part.lower() in COMPUTE_REFERENCE_SKIP_DIRECTORIES for part in path.parts)
    )


def compute_reference_texts(root: Path) -> tuple[list[tuple[Path, str]], int, int, list[str]]:
    texts: list[tuple[Path, str]] = []
    skipped_count = 0
    skipped_bytes = 0
    skipped_samples: list[str] = []
    for path in iter_compute_reference_files(root):
        size = path.stat().st_size
        if size > MAX_COMPUTE_REFERENCE_FILE_BYTES:
            skipped_count += 1
            skipped_bytes += size
            if len(skipped_samples) < 12:
                skipped_samples.append(normalize_path(path, root))
            continue
        texts.append((path, read_text(path)))
    return texts, skipped_count, skipped_bytes, skipped_samples


def compute_reference_surface(
    root: Path,
    compute_path: Path,
    reference_texts: list[tuple[Path, str]],
) -> dict[str, object]:
    relative = normalize_path(compute_path, root)
    filename = compute_path.name
    guid = asset_guid(compute_path)
    counts_by_surface: dict[str, int] = {}
    counts_by_kind: dict[str, int] = {}
    counts_by_surface_kind: dict[tuple[str, str], int] = {}
    samples: list[dict[str, object]] = []

    def record(path: Path, text: str, kind: str, index: int) -> None:
        surface = execution_surface_for_path(path, root)
        counts_by_surface[surface] = counts_by_surface.get(surface, 0) + 1
        counts_by_kind[kind] = counts_by_kind.get(kind, 0) + 1
        surface_kind = (surface, kind)
        counts_by_surface_kind[surface_kind] = counts_by_surface_kind.get(surface_kind, 0) + 1
        if len(samples) < 8:
            samples.append(
                {
                    "path": normalize_path(path, root),
                    "line": line_number_for_index(text, index),
                    "kind": kind,
                    "executionSurface": surface,
                }
            )

    for reference_path, text in reference_texts:
        if reference_path == compute_path:
            continue
        if not text:
            continue
        suffix = reference_path.suffix.lower()
        if guid and suffix in COMPUTE_SERIALIZED_REFERENCE_EXTENSIONS:
            index = text.find(guid)
            if index >= 0:
                record(reference_path, text, "serializedGuid", index)
                continue
        if suffix == ".cs":
            path_index = text.find(relative)
            name_index = text.find(filename)
            index = path_index if path_index >= 0 else name_index
            if index >= 0:
                record(reference_path, text, "sourcePath", index)

    runtime_source = counts_by_surface_kind.get(("Runtime", "sourcePath"), 0)
    runtime_serialized = counts_by_surface_kind.get(("Runtime", "serializedGuid"), 0)
    runtime_references = counts_by_surface.get("Runtime", 0)
    if runtime_serialized > 0:
        reachability = "RuntimeSerialized"
    elif runtime_source > 0:
        reachability = "RuntimeSource"
    elif counts_by_surface:
        reachability = "EditorOrTestOnly"
    else:
        reachability = "UnreferencedAsset"

    return {
        "guid": guid,
        "runtimeReferenceCount": runtime_references,
        "runtimeSourceReferenceCount": runtime_source,
        "runtimeSerializedReferenceCount": runtime_serialized,
        "referenceCountByExecutionSurface": dict(sorted(counts_by_surface.items())),
        "referenceCountByKind": dict(sorted(counts_by_kind.items())),
        "runtimeReachability": reachability,
        "referenceSamples": samples,
    }


def compute_thread_surface(root: Path) -> dict[str, object]:
    compute_files = sorted((root / "Assets").rglob("*.compute")) if (root / "Assets").exists() else []
    risky_groups: list[dict[str, object]] = []
    runtime_asset_risky_groups: list[dict[str, object]] = []
    editor_test_only_runtime_asset_risky_groups: list[dict[str, object]] = []
    runtime_risky_groups: list[dict[str, object]] = []
    risky_by_surface: dict[str, int] = {}
    risky_by_reachability: dict[str, int] = {}
    risky_by_owner: dict[str, int] = {}
    unknown_groups = 0
    target_50_files: list[str] = []
    total_numthreads = 0
    references: list[tuple[Path, str]] | None = None
    skipped_reference_count = 0
    skipped_reference_bytes = 0
    skipped_reference_samples: list[str] = []
    reference_cache: dict[Path, dict[str, object]] = {}

    def reference_for(path: Path) -> dict[str, object]:
        nonlocal references
        nonlocal skipped_reference_count
        nonlocal skipped_reference_bytes
        nonlocal skipped_reference_samples
        if path in reference_cache:
            return reference_cache[path]
        if references is None:
            references, skipped_reference_count, skipped_reference_bytes, skipped_reference_samples = compute_reference_texts(root)
        reference_cache[path] = compute_reference_surface(root, path, references)
        return reference_cache[path]

    for path in compute_files:
        text = read_text(path)
        if re.search(r"^\s*#pragma\s+target\s+5(?:\.0)?\b", text, re.M):
            target_50_files.append(normalize_path(path, root))
        for match in re.finditer(r"\[numthreads\(([^)]*)\)\]", text):
            total_numthreads += 1
            product, args = parse_numthreads_args(match.group(1))
            line = text.count("\n", 0, match.start()) + 1
            if product is None:
                unknown_groups += 1
                continue
            if product > RISKY_COMPUTE_THREAD_GROUP_THRESHOLD:
                surface = execution_surface_for_path(path, root)
                reference = reference_for(path)
                item = {
                    "path": normalize_path(path, root),
                    "line": line,
                    "args": args,
                    "threadGroupSize": product,
                    "executionSurface": surface,
                    "owner": path_owner(path, root),
                    "runtimeReachability": reference["runtimeReachability"],
                    "runtimeReferenceCount": reference["runtimeReferenceCount"],
                    "runtimeSourceReferenceCount": reference["runtimeSourceReferenceCount"],
                    "runtimeSerializedReferenceCount": reference["runtimeSerializedReferenceCount"],
                    "referenceSamples": reference["referenceSamples"],
                }
                risky_groups.append(item)
                risky_by_surface[surface] = risky_by_surface.get(surface, 0) + 1
                reachability = str(reference["runtimeReachability"])
                risky_by_reachability[reachability] = risky_by_reachability.get(reachability, 0) + 1
                owner = str(item["owner"])
                risky_by_owner[owner] = risky_by_owner.get(owner, 0) + 1
                if surface == "Runtime":
                    if reachability == "EditorOrTestOnly":
                        editor_test_only_runtime_asset_risky_groups.append(item)
                    else:
                        runtime_asset_risky_groups.append(item)
                if surface == "Runtime" and int(reference["runtimeReferenceCount"]) > 0:
                    runtime_risky_groups.append(item)
    return {
        "computeFileCount": len(compute_files),
        "numthreadsDeclarations": total_numthreads,
        "unknownThreadGroupDeclarations": unknown_groups,
        "referenceFilesScanned": len(references) if references is not None else 0,
        "referenceFileMaxBytes": MAX_COMPUTE_REFERENCE_FILE_BYTES,
        "skippedReferenceFileCount": skipped_reference_count,
        "skippedReferenceBytes": skipped_reference_bytes,
        "skippedReferenceSamples": skipped_reference_samples,
        "riskyThreadGroupThreshold": RISKY_COMPUTE_THREAD_GROUP_THRESHOLD,
        "riskyThreadGroups": risky_groups[:40],
        "riskyThreadGroupCount": len(risky_groups),
        "riskyThreadGroupCountByExecutionSurface": dict(sorted(risky_by_surface.items())),
        "riskyThreadGroupCountByRuntimeReachability": dict(sorted(risky_by_reachability.items())),
        "riskyThreadGroupCountByOwner": dict(sorted(risky_by_owner.items())),
        "runtimeAssetRiskyThreadGroups": runtime_asset_risky_groups[:40],
        "runtimeAssetRiskyThreadGroupCount": len(runtime_asset_risky_groups),
        "editorOrTestOnlyRuntimeAssetRiskyThreadGroups": editor_test_only_runtime_asset_risky_groups[:40],
        "editorOrTestOnlyRuntimeAssetRiskyThreadGroupCount": len(editor_test_only_runtime_asset_risky_groups),
        "runtimeRiskyThreadGroups": runtime_risky_groups[:40],
        "runtimeRiskyThreadGroupCount": len(runtime_risky_groups),
        "target50ComputeFiles": target_50_files[:40],
        "target50ComputeFileCount": len(target_50_files),
    }


def compute_dispatch_caller_surface(root: Path) -> dict[str, object]:
    assets = root / "Assets"
    cs_files = sorted(assets.rglob("*.cs")) if assets.exists() else []
    call_count = 0
    runtime_call_count = 0
    files_with_dispatch = 0
    runtime_files_with_dispatch = 0
    unchecked_call_count = 0
    runtime_unchecked_call_count = 0
    first_party_runtime_unchecked_call_count = 0
    payload_sized_first_party_runtime_unchecked_call_count = 0
    vendor_runtime_unchecked_call_count = 0
    unchecked_file_count = 0
    runtime_unchecked_file_count = 0
    first_party_runtime_unchecked_file_count = 0
    payload_sized_first_party_runtime_unchecked_file_count = 0
    vendor_runtime_unchecked_file_count = 0
    unchecked_by_surface: dict[str, int] = {}
    unchecked_by_owner: dict[str, int] = {}
    samples: list[dict[str, object]] = []
    first_party_runtime_samples: list[dict[str, object]] = []
    payload_sized_first_party_runtime_samples: list[dict[str, object]] = []
    vendor_runtime_samples: list[dict[str, object]] = []

    for path in cs_files:
        if any(part.lower() in COMPUTE_REFERENCE_SKIP_DIRECTORIES for part in path.parts):
            continue
        text = read_text(path)
        matches = list(COMPUTE_DISPATCH_COMMANDBUFFER_PATTERN.finditer(text))
        shader_dispatch_matches = list(COMPUTE_DISPATCH_SHADER_PATTERN.finditer(text))
        if shader_dispatch_matches and ("ComputeShader" in text or "FindKernel" in text):
            matches.extend(shader_dispatch_matches)
        elif shader_dispatch_matches:
            for match in shader_dispatch_matches:
                line_start = text.rfind("\n", 0, match.start()) + 1
                line_end = text.find("\n", match.start())
                if line_end < 0:
                    line_end = len(text)
                line = text[line_start:line_end].lower()
                if "compute" in line:
                    matches.append(match)
        if not matches:
            continue
        matches.sort(key=lambda item: item.start())
        surface = execution_surface_for_path(path, root)
        owner = path_owner(path, root)
        has_thread_group_query = bool(COMPUTE_THREAD_GROUP_QUERY_PATTERN.search(text))
        all_dispatches_payload_sized = all(is_payload_sized_dispatch_call(text, match) for match in matches)
        file_call_count = len(matches)
        call_count += file_call_count
        files_with_dispatch += 1
        if surface == "Runtime":
            runtime_call_count += file_call_count
            runtime_files_with_dispatch += 1
        if has_thread_group_query:
            continue
        unchecked_call_count += file_call_count
        unchecked_file_count += 1
        unchecked_by_surface[surface] = unchecked_by_surface.get(surface, 0) + file_call_count
        unchecked_by_owner[owner] = unchecked_by_owner.get(owner, 0) + file_call_count
        if surface == "Runtime":
            runtime_unchecked_call_count += file_call_count
            runtime_unchecked_file_count += 1
            if owner == "FirstParty":
                if all_dispatches_payload_sized:
                    payload_sized_first_party_runtime_unchecked_call_count += file_call_count
                    payload_sized_first_party_runtime_unchecked_file_count += 1
                else:
                    first_party_runtime_unchecked_call_count += file_call_count
                    first_party_runtime_unchecked_file_count += 1
            else:
                vendor_runtime_unchecked_call_count += file_call_count
                vendor_runtime_unchecked_file_count += 1
        for match in matches:
            expression = dispatch_expression_for_match(text, match)
            sample = {
                "path": normalize_path(path, root),
                "line": line_number_for_index(text, match.start()),
                "executionSurface": surface,
                "owner": owner,
                "call": expression[:160],
                "fileHasThreadGroupQuery": has_thread_group_query,
                "payloadSizedDispatch": is_payload_sized_dispatch_call(text, match),
            }
            if len(samples) < 40:
                samples.append(sample)
            if surface == "Runtime" and owner == "FirstParty" and not all_dispatches_payload_sized:
                if len(first_party_runtime_samples) < 40:
                    first_party_runtime_samples.append(sample)
            elif surface == "Runtime" and owner == "FirstParty" and all_dispatches_payload_sized:
                if len(payload_sized_first_party_runtime_samples) < 40:
                    payload_sized_first_party_runtime_samples.append(sample)
            elif surface == "Runtime" and owner != "FirstParty":
                if len(vendor_runtime_samples) < 40:
                    vendor_runtime_samples.append(sample)

    return {
        "dispatchCallCount": call_count,
        "runtimeDispatchCallCount": runtime_call_count,
        "dispatchCallerFileCount": files_with_dispatch,
        "runtimeDispatchCallerFileCount": runtime_files_with_dispatch,
        "dispatchCallsWithoutThreadGroupQueryCount": unchecked_call_count,
        "runtimeDispatchCallsWithoutThreadGroupQueryCount": runtime_unchecked_call_count,
        "firstPartyRuntimeDispatchCallsWithoutThreadGroupQueryCount": first_party_runtime_unchecked_call_count,
        "firstPartyRuntimePayloadSizedDispatchCallsWithoutThreadGroupQueryCount": payload_sized_first_party_runtime_unchecked_call_count,
        "vendorRuntimeDispatchCallsWithoutThreadGroupQueryCount": vendor_runtime_unchecked_call_count,
        "dispatchCallerFilesWithoutThreadGroupQueryCount": unchecked_file_count,
        "runtimeDispatchCallerFilesWithoutThreadGroupQueryCount": runtime_unchecked_file_count,
        "firstPartyRuntimeDispatchCallerFilesWithoutThreadGroupQueryCount": first_party_runtime_unchecked_file_count,
        "firstPartyRuntimePayloadSizedDispatchCallerFilesWithoutThreadGroupQueryCount": payload_sized_first_party_runtime_unchecked_file_count,
        "vendorRuntimeDispatchCallerFilesWithoutThreadGroupQueryCount": vendor_runtime_unchecked_file_count,
        "dispatchCallsWithoutThreadGroupQueryByExecutionSurface": dict(sorted(unchecked_by_surface.items())),
        "dispatchCallsWithoutThreadGroupQueryByOwner": dict(sorted(unchecked_by_owner.items())),
        "dispatchCallersWithoutThreadGroupQuerySamples": samples,
        "firstPartyRuntimeDispatchCallersWithoutThreadGroupQuerySamples": first_party_runtime_samples,
        "firstPartyRuntimePayloadSizedDispatchCallersWithoutThreadGroupQuerySamples": payload_sized_first_party_runtime_samples,
        "vendorRuntimeDispatchCallersWithoutThreadGroupQuerySamples": vendor_runtime_samples,
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
    addressables_data = count_files(root, "Assets/AddressableAssetsData", ignore_meta=True)
    addressables_settings = root / "Assets" / "AddressableAssetsData"
    content_validator = root / "Assets" / "_Project" / "Scripts" / "Core" / "Content" / "Editor" / "ContentAuthorityBuildValidators.cs"
    content_hash_map = root / "Assets" / "_Project" / "Scripts" / "Core" / "Content" / "ContentAssetHashMap.cs"
    game_bootstrapper = root / "Assets" / "_Project" / "Scripts" / "Bootstrap" / "GameBootstrapper.cs"
    asset_lifecycle_governor = root / "Assets" / "_Project" / "Scripts" / "Optimization" / "AssetLifecycleGovernor.cs"
    texture_import_dictator = root / "Assets" / "_Project" / "Scripts" / "Editor" / "HectonTextureImportDictator.cs"
    content_validator_text = read_text(content_validator)
    content_hash_map_text = read_text(content_hash_map)
    game_bootstrapper_text = read_text(game_bootstrapper)
    asset_lifecycle_text = read_text(asset_lifecycle_governor)
    texture_import_text = read_text(texture_import_dictator)
    monolith = root / "Assets" / "StreamingAssets" / "Hecton8" / "DataMonolith" / "static_data.h8bin"
    data_monolith_compiler = root / "Assets" / "_Project" / "Scripts" / "Editor" / "DataMonolith" / "H8DataMonolithCompiler.cs"
    data_monolith_validator = root / "Tools" / "h8bin_validator.py"
    data_monolith_source_folder = root / "Assets" / "_SourceData" / "DataMonolith"
    data_monolith_balance_folder = root / "Data" / "Balance"
    compiler_text = read_text(data_monolith_compiler)
    validator_text = read_text(data_monolith_validator)
    builds = count_files(root, "Builds")
    build_result_logs = sorted((root / "Docs" / "AgentLogs").glob("Build_Result_*.txt"))
    command_line_bake_present = (
        "BakeFromCommandLine" in compiler_text
        and "BakeAll(logSummary: true)" in compiler_text
        and "TryValidateOutputBlob" in compiler_text
        and "EditorApplication.Exit" in compiler_text
    )
    prebuild_gate_present = (
        "H8DataMonolithBuildPreprocessor" in compiler_text
        and "IPreprocessBuildWithReport" in compiler_text
        and "BuildFailedException" in compiler_text
        and "BakeAll(logSummary: false)" in compiler_text
        and "TryValidateOutputBlob" in compiler_text
    )
    output_validation_present = (
        "TryValidateOutputBlob" in compiler_text
        and "TryValidateBlobFile" in compiler_text
        and "XXHash3 checksum mismatch" in compiler_text
    )
    atomic_temp_validate_write_present = (
        "TempOutputSuffix" in compiler_text
        and "TryWriteValidatedBlob" in compiler_text
        and "TryValidateBlobFile(tempPath" in compiler_text
        and "File.Replace" in compiler_text
        and "File.Move" in compiler_text
        and "Atomic output write failed" in compiler_text
    )
    little_endian_guard_present = (
        "EnsureLittleEndianEditorHost" in compiler_text
        and "BitConverter.IsLittleEndian" in compiler_text
        and "Big-endian editor hosts" in compiler_text
    )
    production_coverage_gate_present = (
        "ValidateProductionSectionCoverage" in compiler_text
        and "BuildProductionCoverageError" in compiler_text
        and "Production static-data coverage gate failed" in compiler_text
    )
    external_validator_present = (
        "DEFAULT_STATIC_DATA_RELATIVE" in validator_text
        and "STATIC_DATA_MISSING" in validator_text
        and "validate_h8bin_file" in validator_text
        and "checksum" in validator_text
        and "AUP_BOUND_METERS" in validator_text
    )
    content_authority_validator_present = (
        "ContentAuthorityBuildValidators" in content_validator_text
        and "RunAllBuildValidators" in content_validator_text
        and "ValidateAddressableGroups" in content_validator_text
        and "ValidateComputeShaderThreadGroups" in content_validator_text
    )
    content_authority_prebuild_gate_present = (
        "ContentAuthorityBuildPreprocessor" in content_validator_text
        and "IPreprocessBuildWithReport" in content_validator_text
        and "RunAllBuildValidators" in content_validator_text
        and "BuildFailedException" in content_validator_text
    )
    addressables_tier_group_gate_present = (
        'CoreGroupName = "Core"' in content_validator_text
        and 'HighResGroupName = "High_Res"' in content_validator_text
        and 'OverkillGroupName = "Overkill"' in content_validator_text
        and "Addressables tier group missing: Core" in content_validator_text
        and "Addressables tier group missing: High_Res" in content_validator_text
        and "Addressables tier group missing: Overkill" in content_validator_text
    )
    content_hash_map_route_present = (
        "ContentAssetHashMap" in content_hash_map_text
        and "Addressables address or GUID" in content_hash_map_text
        and "Runtime callers must resolve by Hash first" in content_hash_map_text
    )
    bootstrap_dependency_prewarm_present = (
        "DownloadDependenciesAsync" in game_bootstrapper_text
        and "TryReleaseBootstrapDependencyHandle" in game_bootstrapper_text
        and "HasEditorAddressablesRuntimeSettingsFile" in game_bootstrapper_text
        and "PublishAddressableDependencyGroupLoaded" in game_bootstrapper_text
    )
    lifecycle_async_load_route_present = (
        "TryAcquireAddressableGameObject" in asset_lifecycle_text
        and "Addressables.LoadAssetAsync<GameObject>" in asset_lifecycle_text
        and "RegisterAddressableHandleSlot" in asset_lifecycle_text
        and "MarkAddressableLoaded" in asset_lifecycle_text
    )
    lifecycle_blind_frame_release_present = (
        "TryExecuteOrDeferBlindFrameRelease" in asset_lifecycle_text
        and "SetHeapSanitizerBlindFrameWindow" in asset_lifecycle_text
        and "Addressables.Release(handle)" in asset_lifecycle_text
        and "EnqueueDetachedAddressableRelease" in asset_lifecycle_text
    )
    lifecycle_telemetry_dump_present = "Dump_SHINOBU_101_Addressables.bin" in asset_lifecycle_text
    texture_tier_authoring_route_present = (
        "Sync Texture Addressables Tier Labels" in texture_import_text
        and "AddressableAssetSettingsDefaultObject.GetSettings(true)" in texture_import_text
        and "ResolveTieredTextureGroup" in texture_import_text
    )
    return {
        "addressables": addressables_data,
        "addressablesRoute": {
            "settingsPath": normalize_path(addressables_settings),
            "settingsFolderExists": addressables_settings.exists(),
            "contentAuthorityValidatorPath": normalize_path(content_validator),
            "contentAuthorityValidatorPresent": content_authority_validator_present,
            "contentAuthorityPrebuildGatePresent": content_authority_prebuild_gate_present,
            "addressablesTierGroupGatePresent": addressables_tier_group_gate_present,
            "contentHashMapPath": normalize_path(content_hash_map),
            "contentHashMapRoutePresent": content_hash_map_route_present,
            "bootstrapDependencyPrewarmPath": normalize_path(game_bootstrapper),
            "bootstrapDependencyPrewarmPresent": bootstrap_dependency_prewarm_present,
            "assetLifecycleGovernorPath": normalize_path(asset_lifecycle_governor),
            "lifecycleAsyncLoadRoutePresent": lifecycle_async_load_route_present,
            "lifecycleBlindFrameReleasePresent": lifecycle_blind_frame_release_present,
            "lifecycleTelemetryDumpPresent": lifecycle_telemetry_dump_present,
            "textureTierAuthoringPath": normalize_path(texture_import_dictator),
            "textureTierAuthoringRoutePresent": texture_tier_authoring_route_present,
        },
        "dataMonolith": {
            "path": normalize_path(monolith),
            "exists": monolith.exists(),
            "bytes": monolith.stat().st_size if monolith.exists() else 0,
        },
        "dataMonolithBakeRoute": {
            "compilerPath": normalize_path(data_monolith_compiler),
            "compilerPresent": bool(compiler_text),
            "outputAssetPathTokenPresent": "Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin" in compiler_text,
            "sourceFolderTokenPresent": "Assets/_SourceData/DataMonolith" in compiler_text,
            "balanceFolderTokenPresent": "Data/Balance" in compiler_text,
            "commandLineBakePresent": command_line_bake_present,
            "prebuildGatePresent": prebuild_gate_present,
            "outputValidationPresent": output_validation_present,
            "atomicTempValidateWritePresent": atomic_temp_validate_write_present,
            "littleEndianGuardPresent": little_endian_guard_present,
            "productionCoverageGatePresent": production_coverage_gate_present,
            "externalValidatorPath": normalize_path(data_monolith_validator),
            "externalValidatorPresent": external_validator_present,
            "sourceFolderPath": normalize_path(data_monolith_source_folder),
            "sourceFolderExists": data_monolith_source_folder.exists(),
            "sourceFileCount": int(count_files(root, "Assets/_SourceData/DataMonolith", ignore_meta=True)["fileCount"]),
            "balanceFolderPath": normalize_path(data_monolith_balance_folder),
            "balanceFolderExists": data_monolith_balance_folder.exists(),
            "balanceFileCount": int(count_files(root, "Data/Balance", ignore_meta=True)["fileCount"]),
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


def readiness_surface(
    packages: dict[str, object],
    settings: dict[str, object],
    artifacts: dict[str, object],
    quality: dict[str, object],
    shaders: dict[str, object],
    compute: dict[str, object],
) -> dict[str, object]:
    android_scaffold = bool(
        packages["allRequiredXrPackagesInManifest"]
        and packages["allRequiredXrPackagesInLock"]
        and settings["androidArm64OnlySerialized"]
        and settings["androidIl2CppSerialized"]
        and settings["androidTargetSdkVersion"]
    )
    addressables = artifacts["addressables"]
    addressables_route = artifacts["addressablesRoute"]
    monolith = artifacts["dataMonolith"]
    monolith_route = artifacts["dataMonolithBakeRoute"]
    builds = artifacts["builds"]
    if (
        not isinstance(addressables, dict)
        or not isinstance(addressables_route, dict)
        or not isinstance(monolith, dict)
        or not isinstance(monolith_route, dict)
        or not isinstance(builds, dict)
    ):
        raise TypeError("artifact payload malformed")
    addressables_package_present = bool(packages["addressablesPackageInManifest"] and packages["addressablesPackageInLock"])
    addressables_content_route_present = bool(
        addressables_package_present
        and addressables_route["contentAuthorityValidatorPresent"]
        and addressables_route["contentAuthorityPrebuildGatePresent"]
        and addressables_route["addressablesTierGroupGatePresent"]
        and addressables_route["contentHashMapRoutePresent"]
    )
    addressables_runtime_lifecycle_route_present = bool(
        addressables_package_present
        and addressables_route["bootstrapDependencyPrewarmPresent"]
        and addressables_route["lifecycleAsyncLoadRoutePresent"]
        and addressables_route["lifecycleBlindFrameReleasePresent"]
        and addressables_route["lifecycleTelemetryDumpPresent"]
    )
    data_monolith_bake_route_present = bool(
        monolith_route["compilerPresent"]
        and monolith_route["outputAssetPathTokenPresent"]
        and monolith_route["sourceFolderTokenPresent"]
        and monolith_route["balanceFolderTokenPresent"]
        and monolith_route["commandLineBakePresent"]
        and monolith_route["prebuildGatePresent"]
    )
    data_monolith_validation_route_present = bool(
        monolith_route["outputValidationPresent"]
        and monolith_route["atomicTempValidateWritePresent"]
        and monolith_route["littleEndianGuardPresent"]
        and monolith_route["productionCoverageGatePresent"]
        and monolith_route["externalValidatorPresent"]
    )
    return {
        "androidQuestScaffold": android_scaffold,
        "androidQuestXrRouteRepairerPresent": bool(settings["androidQuestXrRouteRepairerPresent"]),
        "xrProviderSerializedProof": bool(settings["xrProviderSerializedProof"]),
        "xrProviderRouteFixerPresent": bool(settings["xrProviderRouteFixerPresent"]),
        "xrProviderRouteValidatorPresent": bool(settings["xrProviderRouteValidatorPresent"]),
        "androidSustainedPerformanceEnabled": bool(settings["androidSustainedPerformanceEnabled"]),
        "androidVulkanOnlySerialized": bool(settings["androidVulkanOnlySerialized"]),
        "questUrpAssetPresent": bool(quality["questUrpAssetPresent"]),
        "questUrpWiredToAndroidQuality": bool(quality["androidDefaultQualityUsesQuestUrp"]),
        "graphicsSettingsShaderPreloadBypassDisabled": int(shaders["preloadedShaderEntries"]) == 0,
        "shaderVariantCollectionsPresent": int(shaders["shaderVariantCollectionFiles"]) > 0,
        "bootstrapExplicitShaderWarmup": (
            int(shaders["bootstrapExplicitWarmUpCallCount"]) > 0
            or int(shaders["bootstrapShaderWarmupFromCollectionCallCount"]) > 0
        ),
        "shaderWarmupRoutePresent": (
            int(shaders["shaderVariantCollectionFiles"]) > 0
            and (
                int(shaders["bootstrapExplicitWarmUpCallCount"]) > 0
                or int(shaders["bootstrapShaderWarmupFromCollectionCallCount"]) > 0
            )
        ),
        "noRuntimeAssetHighRiskComputeThreadGroups": int(compute["runtimeAssetRiskyThreadGroupCount"]) == 0,
        "noRuntimeReferencedHighRiskComputeThreadGroups": int(compute["runtimeRiskyThreadGroupCount"]) == 0,
        "noRuntimeHighRiskComputeThreadGroups": int(compute["runtimeRiskyThreadGroupCount"]) == 0,
        "noHighRiskComputeThreadGroups": int(compute["runtimeRiskyThreadGroupCount"]) == 0,
        "noFirstPartyRuntimeComputeDispatchWithoutThreadGroupQuery": int(compute["firstPartyRuntimeDispatchCallsWithoutThreadGroupQueryCount"]) == 0,
        "noRuntimeComputeDispatchWithoutThreadGroupQuery": int(compute["firstPartyRuntimeDispatchCallsWithoutThreadGroupQueryCount"]) == 0,
        "addressablesPackagePresent": addressables_package_present,
        "addressablesContentPresent": int(addressables["fileCount"]) > 0,
        "addressablesContentRoutePresent": addressables_content_route_present,
        "addressablesRuntimeLifecycleRoutePresent": addressables_runtime_lifecycle_route_present,
        "dataMonolithPresent": bool(monolith["exists"]),
        "dataMonolithBakeRoutePresent": data_monolith_bake_route_present,
        "dataMonolithValidationRoutePresent": data_monolith_validation_route_present,
        "buildArtifactPresent": int(builds["fileCount"]) > 0 or int(artifacts["buildResultLogCount"]) > 0,
    }


def build_payload(root: Path) -> dict[str, object]:
    packages = package_surface(root)
    settings = project_settings_surface(root)
    quality = quality_pipeline_surface(root)
    shaders = shader_warmup_surface(root)
    compute = compute_thread_surface(root)
    compute.update(compute_dispatch_caller_surface(root))
    artifacts = artifact_surface(root)
    native_plugins = native_plugin_surface(root)
    readiness = readiness_surface(packages, settings, artifacts, quality, shaders, compute)
    return {
        "schema": SCHEMA,
        "root": normalize_path(root),
        "packages": packages,
        "projectSettings": settings,
        "qualityPipeline": quality,
        "shaderWarmup": shaders,
        "computeThreads": compute,
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
    quality = payload["qualityPipeline"]
    shaders = payload["shaderWarmup"]
    compute = payload["computeThreads"]
    artifacts = payload["artifacts"]
    native_plugins = payload["nativePlugins"]
    readiness = payload["readiness"]
    if not all(isinstance(item, dict) for item in (packages, settings, quality, shaders, compute, artifacts, native_plugins, readiness)):
        raise TypeError("payload malformed")
    addressables = artifacts["addressables"]
    addressables_route = artifacts["addressablesRoute"]
    monolith = artifacts["dataMonolith"]
    monolith_route = artifacts["dataMonolithBakeRoute"]
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
        f"- Addressables package in manifest: `{yes_no(packages['addressablesPackageInManifest'])}`",
        f"- Addressables package in lock: `{yes_no(packages['addressablesPackageInLock'])}`",
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
            f"- Android sustained performance: `{settings['androidSustainedPerformanceMode']}` / `{yes_no(settings['androidSustainedPerformanceEnabled'])}`",
            f"- Android graphics API raw: `{settings['androidGraphicsApisRaw']}`, automatic: `{settings['androidGraphicsAutomatic']}`, Vulkan-only: `{yes_no(settings['androidVulkanOnlySerialized'])}`",
            f"- `m_BuildTargetVRSettings` empty: `{yes_no(settings['buildTargetVrSettingsEmpty'])}`",
            f"- XR provider serialized proof: `{yes_no(settings['xrProviderSerializedProof'])}`",
            f"- XR legacy provider proof: `{yes_no(settings['xrLegacyProviderSerializedProof'])}`",
            f"- XR Management provider proof: `{yes_no(settings['xrManagementProviderSerializedProof'])}`",
            f"- XR Management OpenXR settings: `{settings['xrManagementOpenXrSettingsPath']}`, present: `{yes_no(settings['xrManagementOpenXrSettingsAssetPresent'])}`, registered: `{yes_no(settings['xrManagementOpenXrSettingsRegistered'])}`",
            f"- XR Management OpenXR loader: `{settings['xrManagementOpenXrLoaderPath']}`, present: `{yes_no(settings['xrManagementOpenXrLoaderAssetPresent'])}`, serialized references: `{settings['xrManagementOpenXrLoaderGuidReferenceCount']}`",
            f"- XR Management Single Pass Instanced serialized: `{yes_no(settings['xrManagementSinglePassInstancedSerialized'])}`",
            f"- XR Management Quest feature present/enabled: `{yes_no(settings['xrManagementQuestFeaturePresent'])}` / `{yes_no(settings['xrManagementQuestFeatureEnabled'])}`",
            f"- XR readiness validator present: `{yes_no(settings['xrReadinessValidatorPresent'])}`",
            f"- XR provider route validator present: `{yes_no(settings['xrProviderRouteValidatorPresent'])}`",
            f"- XR provider route fixer present: `{yes_no(settings['xrProviderRouteFixerPresent'])}`",
            f"- Android Quest/XR route repairer present: `{yes_no(settings['androidQuestXrRouteRepairerPresent'])}`",
            "",
            "## Quality / Quest URP Wiring",
            "",
            f"- Quest URP asset: `{quality['questUrpAssetPath']}`, present: `{yes_no(quality['questUrpAssetPresent'])}`, guid: `{quality['questUrpGuid']}`",
            f"- Quality settings count: `{quality['qualitySettingCount']}`",
            f"- Android default quality index: `{quality['androidDefaultQualityIndex']}`",
            f"- Android default quality render pipeline guid: `{quality['androidDefaultQualityRenderPipelineGuid']}`",
            f"- Quest URP referenced in QualitySettings: `{yes_no(quality['questUrpReferencedInQualitySettings'])}`",
            f"- Quest URP referenced in GraphicsSettings: `{yes_no(quality['questUrpReferencedInGraphicsSettings'])}`",
            f"- Android default quality uses Quest URP: `{yes_no(quality['androidDefaultQualityUsesQuestUrp'])}`",
            f"- Quest configurator present: `{yes_no(quality['questConfiguratorPresent'])}`",
            f"- Quest configurator reports quality route: `{yes_no(quality['questConfiguratorQualityRouteAuditPresent'])}`",
            f"- Quest configurator can wire Android route: `{yes_no(quality['questConfiguratorQualityRouteFixerPresent'])}`",
            "",
            "## Shader / Compute Static Risk",
            "",
            f"- Preloaded shader entries: `{shaders['preloadedShaderEntries']}`",
            f"- ShaderVariantCollection files: `{shaders['shaderVariantCollectionFiles']}`",
            f"- Bootstrap shader collection field present: `{yes_no(shaders['bootstrapShaderCollectionFieldPresent'])}`",
            f"- Bootstrap legacy `ShaderVariantCollection.WarmUp()` calls: `{shaders['bootstrapExplicitWarmUpCallCount']}`",
            f"- Bootstrap `ShaderWarmup.WarmupShaderFromCollection()` calls: `{shaders['bootstrapShaderWarmupFromCollectionCallCount']}`",
            f"- Bootstrap `WarmUpProgressively()` calls: `{shaders['bootstrapGraphicsStateWarmUpProgressivelyCallCount']}`",
            f"- Bootstrap `isWarmedUp` reads: `{shaders['bootstrapIsWarmedUpReadCount']}`",
            f"- `Shader.WarmupAllShaders()` call sites in bootstrap: `{shaders['warmupAllShadersCallSites']}`",
            f"- Shader source files: `{shaders['shaderSourceFiles']}`",
            f"- `shader_feature`/`multi_compile` pragmas: `{shaders['shaderFeaturePragmaCount']}`",
            f"- `#pragma target >= 4.5`: `{shaders['shaderTarget45OrHigherPragmas']}`",
            f"- `#pragma target >= 5.0`: `{shaders['shaderTarget50OrHigherPragmas']}`",
            f"- Compute files: `{compute['computeFileCount']}`",
            f"- Compute reference files scanned: `{compute['referenceFilesScanned']}`",
            f"- Compute reference files skipped over `{compute['referenceFileMaxBytes']}` bytes: `{compute['skippedReferenceFileCount']}` / bytes `{compute['skippedReferenceBytes']}`",
            f"- `numthreads` declarations: `{compute['numthreadsDeclarations']}`",
            f"- Risky numeric thread groups > `{compute['riskyThreadGroupThreshold']}`: `{compute['riskyThreadGroupCount']}`",
            f"- Risky numeric thread groups by execution surface: `{compute['riskyThreadGroupCountByExecutionSurface']}`",
            f"- Risky numeric thread groups by runtime reachability: `{compute['riskyThreadGroupCountByRuntimeReachability']}`",
            f"- Risky numeric thread groups by owner: `{compute['riskyThreadGroupCountByOwner']}`",
            f"- Runtime asset risky numeric thread groups > `{compute['riskyThreadGroupThreshold']}`: `{compute['runtimeAssetRiskyThreadGroupCount']}`",
            f"- Editor/test-only runtime asset risky numeric thread groups > `{compute['riskyThreadGroupThreshold']}`: `{compute['editorOrTestOnlyRuntimeAssetRiskyThreadGroupCount']}`",
            f"- Runtime-referenced risky numeric thread groups > `{compute['riskyThreadGroupThreshold']}`: `{compute['runtimeRiskyThreadGroupCount']}`",
            f"- Compute target 5.0 files: `{compute['target50ComputeFileCount']}`",
            f"- C# compute dispatch calls: `{compute['dispatchCallCount']}`; runtime: `{compute['runtimeDispatchCallCount']}`",
            f"- C# compute dispatch caller files: `{compute['dispatchCallerFileCount']}`; runtime: `{compute['runtimeDispatchCallerFileCount']}`",
            f"- Dispatch calls without file-level `GetKernelThreadGroupSizes`: `{compute['dispatchCallsWithoutThreadGroupQueryCount']}`; runtime: `{compute['runtimeDispatchCallsWithoutThreadGroupQueryCount']}`",
            f"- First-party runtime dispatch calls without local query and without payload-sized dispatch proof: `{compute['firstPartyRuntimeDispatchCallsWithoutThreadGroupQueryCount']}`",
            f"- First-party runtime payload-sized dispatch bridge calls without local query: `{compute['firstPartyRuntimePayloadSizedDispatchCallsWithoutThreadGroupQueryCount']}`",
            f"- Vendor/external runtime dispatch calls without local query: `{compute['vendorRuntimeDispatchCallsWithoutThreadGroupQueryCount']}`",
            f"- Dispatch caller files without file-level `GetKernelThreadGroupSizes`: `{compute['dispatchCallerFilesWithoutThreadGroupQueryCount']}`; runtime: `{compute['runtimeDispatchCallerFilesWithoutThreadGroupQueryCount']}`",
            f"- First-party runtime dispatch caller files without local query and without payload-sized dispatch proof: `{compute['firstPartyRuntimeDispatchCallerFilesWithoutThreadGroupQueryCount']}`",
            f"- First-party runtime payload-sized dispatch bridge files without local query: `{compute['firstPartyRuntimePayloadSizedDispatchCallerFilesWithoutThreadGroupQueryCount']}`",
            f"- Vendor/external runtime dispatch caller files without local query: `{compute['vendorRuntimeDispatchCallerFilesWithoutThreadGroupQueryCount']}`",
            f"- Dispatch calls without thread-group query by execution surface: `{compute['dispatchCallsWithoutThreadGroupQueryByExecutionSurface']}`",
            f"- Dispatch calls without thread-group query by owner: `{compute['dispatchCallsWithoutThreadGroupQueryByOwner']}`",
            "",
            "## Payload / Build Artifacts",
            "",
            f"- Addressables data path: `{addressables['path']}`, files: `{addressables['fileCount']}`",
            f"- Addressables settings folder exists: `{yes_no(addressables_route['settingsFolderExists'])}`",
            f"- ContentAuthority validator: `{addressables_route['contentAuthorityValidatorPath']}`, present: `{yes_no(addressables_route['contentAuthorityValidatorPresent'])}`",
            f"- ContentAuthority prebuild gate: `{yes_no(addressables_route['contentAuthorityPrebuildGatePresent'])}`",
            f"- Addressables tier group gate: `{yes_no(addressables_route['addressablesTierGroupGatePresent'])}`",
            f"- Content hash map route: `{addressables_route['contentHashMapPath']}`, present: `{yes_no(addressables_route['contentHashMapRoutePresent'])}`",
            f"- Bootstrap dependency prewarm route: `{yes_no(addressables_route['bootstrapDependencyPrewarmPresent'])}`",
            f"- AssetLifecycleGovernor async load route: `{yes_no(addressables_route['lifecycleAsyncLoadRoutePresent'])}`",
            f"- AssetLifecycleGovernor blind-frame release route: `{yes_no(addressables_route['lifecycleBlindFrameReleasePresent'])}`",
            f"- Addressables telemetry dump route: `{yes_no(addressables_route['lifecycleTelemetryDumpPresent'])}`",
            f"- Texture tier Addressables authoring route: `{yes_no(addressables_route['textureTierAuthoringRoutePresent'])}`",
            f"- Data Monolith path: `{monolith['path']}`, exists: `{yes_no(monolith['exists'])}`, bytes: `{monolith['bytes']}`",
            f"- Data Monolith compiler: `{monolith_route['compilerPath']}`, present: `{yes_no(monolith_route['compilerPresent'])}`",
            f"- Data Monolith command-line bake route: `{yes_no(monolith_route['commandLineBakePresent'])}`",
            f"- Data Monolith prebuild bake/validation gate: `{yes_no(monolith_route['prebuildGatePresent'])}`",
            f"- Data Monolith output validation route: `{yes_no(monolith_route['outputValidationPresent'])}`",
            f"- Data Monolith atomic temp-write/validate route: `{yes_no(monolith_route['atomicTempValidateWritePresent'])}`",
            f"- Data Monolith little-endian guard: `{yes_no(monolith_route['littleEndianGuardPresent'])}`",
            f"- Data Monolith production coverage gate: `{yes_no(monolith_route['productionCoverageGatePresent'])}`",
            f"- External `.h8bin` validator: `{monolith_route['externalValidatorPath']}`, present: `{yes_no(monolith_route['externalValidatorPresent'])}`",
            f"- Data Monolith source folder: `{monolith_route['sourceFolderPath']}`, exists: `{yes_no(monolith_route['sourceFolderExists'])}`, files: `{monolith_route['sourceFileCount']}`",
            f"- Data Monolith balance folder: `{monolith_route['balanceFolderPath']}`, exists: `{yes_no(monolith_route['balanceFolderExists'])}`, files: `{monolith_route['balanceFileCount']}`",
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

    risky_groups = compute["riskyThreadGroups"]
    if risky_groups:
        lines.append("Risky numeric compute thread groups:")
        lines.append("")
        for item in risky_groups[:20]:
            if isinstance(item, dict):
                lines.append(
                    f"- `{item['path']}:{item['line']}` `{item['args']}` => `{item['threadGroupSize']}` threads "
                    f"(`{item['executionSurface']}`, `{item['runtimeReachability']}`, runtime refs `{item['runtimeReferenceCount']}`)"
                )
        lines.append("")

    unchecked_dispatch = compute["dispatchCallersWithoutThreadGroupQuerySamples"]
    if unchecked_dispatch:
        lines.append("C# compute dispatch callers without file-level thread-group query:")
        lines.append("")

    first_party_unchecked = compute["firstPartyRuntimeDispatchCallersWithoutThreadGroupQuerySamples"]
    if first_party_unchecked:
        lines.append("First-party runtime dispatch callers without local query and without payload-sized dispatch proof:")
        lines.append("")
        for item in first_party_unchecked[:20]:
            if isinstance(item, dict):
                lines.append(
                    f"- `{item['path']}:{item['line']}` (`{item['executionSurface']}`) `{item['call']}`"
                )
        lines.append("")

    payload_sized_dispatch = compute["firstPartyRuntimePayloadSizedDispatchCallersWithoutThreadGroupQuerySamples"]
    if payload_sized_dispatch:
        lines.append("First-party runtime payload-sized dispatch bridges without local query:")
        lines.append("")
        for item in payload_sized_dispatch[:20]:
            if isinstance(item, dict):
                lines.append(
                    f"- `{item['path']}:{item['line']}` (`{item['executionSurface']}`) `{item['call']}`"
                )
        lines.append("")
        for item in unchecked_dispatch[:20]:
            if isinstance(item, dict):
                lines.append(
                    f"- `{item['path']}:{item['line']}` (`{item['executionSurface']}`) `{item['call']}`"
                )
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
            "- Android sustained-performance mode, Vulkan serialization, Quest URP wiring, bootstrap-owned shader warmup, and compute thread-group risk are static readiness gates, not runtime proof.",
            "- Empty `GraphicsSettings.m_PreloadedShaders` is expected when bootstrap owns shader/PSO warmup; global preloads bypass fail-closed telemetry.",
            "- Missing serialized XR provider proof, missing Addressables data, missing Data Monolith, and missing build artifacts block any GREEN platform claim.",
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
        ("fail_on_missing_sustained_performance", "androidSustainedPerformanceEnabled", "Android sustained-performance mode disabled"),
        ("fail_on_unwired_quest_urp", "questUrpWiredToAndroidQuality", "Quest URP asset is not wired to Android default quality"),
        ("fail_on_missing_shader_warmup", "shaderVariantCollectionsPresent", "missing ShaderVariantCollection files"),
        ("fail_on_missing_bootstrap_shader_warmup", "bootstrapExplicitShaderWarmup", "missing explicit bootstrap shader warmup route"),
        ("fail_on_runtime_asset_high_risk_compute", "noRuntimeAssetHighRiskComputeThreadGroups", "high-risk runtime asset numeric compute thread group detected"),
        ("fail_on_high_risk_compute", "noRuntimeHighRiskComputeThreadGroups", "high-risk runtime-referenced numeric compute thread group detected"),
        ("fail_on_runtime_compute_dispatch_without_threadgroup_query", "noRuntimeComputeDispatchWithoutThreadGroupQuery", "first-party runtime compute dispatch without thread-group proof detected"),
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
    quality = payload["qualityPipeline"]
    shaders = payload["shaderWarmup"]
    compute = payload["computeThreads"]
    artifacts = payload["artifacts"]
    native_plugins = payload["nativePlugins"]
    readiness = payload["readiness"]
    print("Platform portability proof audit")
    print(f"schema={payload['schema']}")
    print(f"root={payload['root']}")
    if isinstance(packages, dict):
        print(f"xrPackagesManifest={packages['allRequiredXrPackagesInManifest']}")
        print(f"xrPackagesLock={packages['allRequiredXrPackagesInLock']}")
        print(f"addressablesPackageManifest={packages['addressablesPackageInManifest']}")
        print(f"addressablesPackageLock={packages['addressablesPackageInLock']}")
        print(f"picoPackageCandidates={len(packages['picoPackageCandidates'])}")
    if isinstance(settings, dict):
        print(f"androidArm64Only={settings['androidArm64OnlySerialized']}")
        print(f"androidIl2Cpp={settings['androidIl2CppSerialized']}")
        print(f"androidTargetSdk={settings['androidTargetSdkVersion']}")
        print(f"androidQuestXrRouteRepairerPresent={settings['androidQuestXrRouteRepairerPresent']}")
        print(f"androidSustainedPerformanceEnabled={settings['androidSustainedPerformanceEnabled']}")
        print(f"androidVulkanOnlySerialized={settings['androidVulkanOnlySerialized']}")
        print(f"buildTargetVrSettingsEmpty={settings['buildTargetVrSettingsEmpty']}")
        print(f"xrProviderSerializedProof={settings['xrProviderSerializedProof']}")
        print(f"xrLegacyProviderSerializedProof={settings['xrLegacyProviderSerializedProof']}")
        print(f"xrManagementProviderSerializedProof={settings['xrManagementProviderSerializedProof']}")
        print(f"xrManagementOpenXrSettingsRegistered={settings['xrManagementOpenXrSettingsRegistered']}")
        print(f"xrManagementOpenXrLoaderGuidReferenceCount={settings['xrManagementOpenXrLoaderGuidReferenceCount']}")
        print(f"xrManagementQuestFeatureEnabled={settings['xrManagementQuestFeatureEnabled']}")
        print(f"xrReadinessValidatorPresent={settings['xrReadinessValidatorPresent']}")
        print(f"xrProviderRouteValidatorPresent={settings['xrProviderRouteValidatorPresent']}")
        print(f"xrProviderRouteFixerPresent={settings['xrProviderRouteFixerPresent']}")
    if isinstance(quality, dict):
        print(f"questUrpAssetPresent={quality['questUrpAssetPresent']}")
        print(f"questUrpWiredToAndroidQuality={quality['androidDefaultQualityUsesQuestUrp']}")
        print(f"androidDefaultQualityIndex={quality['androidDefaultQualityIndex']}")
        print(f"questConfiguratorQualityRouteAuditPresent={quality['questConfiguratorQualityRouteAuditPresent']}")
        print(f"questConfiguratorQualityRouteFixerPresent={quality['questConfiguratorQualityRouteFixerPresent']}")
    if isinstance(shaders, dict):
        print(f"preloadedShaderEntries={shaders['preloadedShaderEntries']}")
        print(f"shaderVariantCollectionFiles={shaders['shaderVariantCollectionFiles']}")
        print(f"bootstrapExplicitWarmUpCallCount={shaders['bootstrapExplicitWarmUpCallCount']}")
        print(f"bootstrapShaderWarmupFromCollectionCallCount={shaders['bootstrapShaderWarmupFromCollectionCallCount']}")
        print(f"bootstrapGraphicsStateWarmUpProgressivelyCallCount={shaders['bootstrapGraphicsStateWarmUpProgressivelyCallCount']}")
        print(f"shaderFeaturePragmaCount={shaders['shaderFeaturePragmaCount']}")
        print(f"shaderTarget50OrHigherPragmas={shaders['shaderTarget50OrHigherPragmas']}")
    if isinstance(compute, dict):
        print(f"riskyComputeThreadGroups={compute['riskyThreadGroupCount']}")
        print(f"computeReferenceFilesScanned={compute['referenceFilesScanned']}")
        print(f"computeReferenceFilesSkipped={compute['skippedReferenceFileCount']}")
        print(f"riskyComputeThreadGroupsByExecutionSurface={compute['riskyThreadGroupCountByExecutionSurface']}")
        print(f"riskyComputeThreadGroupsByRuntimeReachability={compute['riskyThreadGroupCountByRuntimeReachability']}")
        print(f"riskyComputeThreadGroupsByOwner={compute['riskyThreadGroupCountByOwner']}")
        print(f"runtimeAssetRiskyComputeThreadGroups={compute['runtimeAssetRiskyThreadGroupCount']}")
        print(f"editorOrTestOnlyRuntimeAssetRiskyComputeThreadGroups={compute['editorOrTestOnlyRuntimeAssetRiskyThreadGroupCount']}")
        print(f"runtimeReferencedRiskyComputeThreadGroups={compute['runtimeRiskyThreadGroupCount']}")
        print(f"target50ComputeFiles={compute['target50ComputeFileCount']}")
        print(f"computeDispatchCalls={compute['dispatchCallCount']}")
        print(f"runtimeComputeDispatchCalls={compute['runtimeDispatchCallCount']}")
        print(f"dispatchCallsWithoutThreadGroupQuery={compute['dispatchCallsWithoutThreadGroupQueryCount']}")
        print(f"runtimeDispatchCallsWithoutThreadGroupQuery={compute['runtimeDispatchCallsWithoutThreadGroupQueryCount']}")
        print(f"firstPartyRuntimeDispatchCallsWithoutThreadGroupQuery={compute['firstPartyRuntimeDispatchCallsWithoutThreadGroupQueryCount']}")
        print(f"firstPartyRuntimePayloadSizedDispatchCallsWithoutThreadGroupQuery={compute['firstPartyRuntimePayloadSizedDispatchCallsWithoutThreadGroupQueryCount']}")
        print(f"vendorRuntimeDispatchCallsWithoutThreadGroupQuery={compute['vendorRuntimeDispatchCallsWithoutThreadGroupQueryCount']}")
        print(f"dispatchCallerFilesWithoutThreadGroupQuery={compute['dispatchCallerFilesWithoutThreadGroupQueryCount']}")
        print(f"runtimeDispatchCallerFilesWithoutThreadGroupQuery={compute['runtimeDispatchCallerFilesWithoutThreadGroupQueryCount']}")
        print(f"firstPartyRuntimeDispatchCallerFilesWithoutThreadGroupQuery={compute['firstPartyRuntimeDispatchCallerFilesWithoutThreadGroupQueryCount']}")
        print(f"firstPartyRuntimePayloadSizedDispatchCallerFilesWithoutThreadGroupQuery={compute['firstPartyRuntimePayloadSizedDispatchCallerFilesWithoutThreadGroupQueryCount']}")
        print(f"vendorRuntimeDispatchCallerFilesWithoutThreadGroupQuery={compute['vendorRuntimeDispatchCallerFilesWithoutThreadGroupQueryCount']}")
    if isinstance(artifacts, dict):
        addressables = artifacts["addressables"]
        monolith = artifacts["dataMonolith"]
        builds = artifacts["builds"]
        if isinstance(addressables, dict) and isinstance(monolith, dict) and isinstance(builds, dict):
            print(f"addressablesFiles={addressables['fileCount']}")
            addressables_route = artifacts["addressablesRoute"]
            if isinstance(addressables_route, dict):
                print(f"addressablesSettingsFolderExists={addressables_route['settingsFolderExists']}")
                print(f"contentAuthorityValidatorPresent={addressables_route['contentAuthorityValidatorPresent']}")
                print(f"contentAuthorityPrebuildGatePresent={addressables_route['contentAuthorityPrebuildGatePresent']}")
                print(f"addressablesTierGroupGatePresent={addressables_route['addressablesTierGroupGatePresent']}")
                print(f"contentHashMapRoutePresent={addressables_route['contentHashMapRoutePresent']}")
                print(f"bootstrapDependencyPrewarmPresent={addressables_route['bootstrapDependencyPrewarmPresent']}")
                print(f"addressablesLifecycleAsyncLoadRoutePresent={addressables_route['lifecycleAsyncLoadRoutePresent']}")
                print(f"addressablesLifecycleBlindFrameReleasePresent={addressables_route['lifecycleBlindFrameReleasePresent']}")
                print(f"addressablesLifecycleTelemetryDumpPresent={addressables_route['lifecycleTelemetryDumpPresent']}")
                print(f"textureTierAddressablesAuthoringRoutePresent={addressables_route['textureTierAuthoringRoutePresent']}")
            print(f"dataMonolithExists={monolith['exists']}")
            route = artifacts["dataMonolithBakeRoute"]
            if isinstance(route, dict):
                print(f"dataMonolithCompilerPresent={route['compilerPresent']}")
                print(f"dataMonolithCommandLineBakePresent={route['commandLineBakePresent']}")
                print(f"dataMonolithPrebuildGatePresent={route['prebuildGatePresent']}")
                print(f"dataMonolithOutputValidationPresent={route['outputValidationPresent']}")
                print(f"dataMonolithAtomicWriteValidationPresent={route['atomicTempValidateWritePresent']}")
                print(f"dataMonolithLittleEndianGuardPresent={route['littleEndianGuardPresent']}")
                print(f"dataMonolithProductionCoverageGatePresent={route['productionCoverageGatePresent']}")
                print(f"externalH8binValidatorPresent={route['externalValidatorPresent']}")
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
    parser.add_argument("--fail-on-missing-sustained-performance", action="store_true")
    parser.add_argument("--fail-on-unwired-quest-urp", action="store_true")
    parser.add_argument("--fail-on-missing-shader-warmup", action="store_true")
    parser.add_argument("--fail-on-missing-bootstrap-shader-warmup", action="store_true")
    parser.add_argument("--fail-on-runtime-asset-high-risk-compute", action="store_true")
    parser.add_argument("--fail-on-high-risk-compute", action="store_true")
    parser.add_argument("--fail-on-runtime-compute-dispatch-without-threadgroup-query", action="store_true")
    parser.add_argument("--fail-on-missing-addressables", action="store_true")
    parser.add_argument("--fail-on-missing-data-monolith", action="store_true")
    parser.add_argument("--fail-on-missing-build-artifact", action="store_true")
    return parser


def main() -> int:
    return run(build_parser().parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
