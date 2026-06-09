#!/usr/bin/env python3
"""Headless Aegir gas giant source/binding contract validator.

This is static proof only. It does not import Unity assets, repair scenes,
render screenshots, run the Frame Debugger, or replace the Editor validator.
It catches the project drift that makes Aegir look like a flat decal or a
Unity built-in sphere before a Unity slot is spent on visual proof.
"""

from __future__ import annotations

import argparse
import json
import math
import re
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]

SKY_MATERIAL = "Assets/_Project/Art/Materials/Sky/MAT_AegirSky_Master.mat"
GAS_MATERIAL = "Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat"
PRODUCTION_PREFAB = "Assets/_Project/Prefabs/GasGiant_Aegir.prefab"
LEGACY_PROLOGUE_PREFAB = "Assets/_Project/_PROLOGUE_CONTENT/Prefabs/GasGiant_Aegir.prefab"
ORBIT_SCENE = "Assets/_Project/Scenes/01_ORBIT.unity"
EDITOR_VALIDATOR = "Assets/_Project/Scripts/Editor/AegirGasGiantSourceValidator.cs"
PRODUCT_FACE_VALIDATOR = "Assets/_Project/Scripts/Editor/ProductFaceSkyOceanSourceValidator.cs"
CELESTIAL_ENGINE = "Assets/_Project/Scripts/HectonCelestialEngine.cs"
ORBITAL_RELATIVITY_DIRECTOR = "Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs"
SURFACE_WEATHER_DIRECTOR = "Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs"
AEGIR_SKY_SHADER = "Assets/_Project/Art/Shaders/Sky/Hecton_AegirSky.shader"
AEGIR_IMPOSTOR_SHADER = "Assets/_Project/Art/Shaders/H8_AegirGasGiantImpostor_1428.shader"
PROOF_TOOL = "Tools/BuildAegirGasGiantProofContactSheet.py"
PROOF_IMAGE = "Docs/GeneratedAssets/AegirGasGiantProof/AegirGasGiantProofContactSheet_20260608.png"
PROOF_MANIFEST = "Docs/GeneratedAssets/AegirGasGiantProof/AegirGasGiantProofContactSheet_20260608.json"
SURFACE_WEATHER_PROFILE_ROOT = "Assets/_Project/Data/Atmosphere/SurfaceWeather"

CANONICAL_BAND_TEXTURE = "Assets/_Project/Art/TEXTURES/clouds0_diff.png"
CANONICAL_DETAIL_TEXTURE = "Assets/_Project/Art/TEXTURES/Sky/oblakajip.png"
CANONICAL_STORM_TEXTURE = "Assets/_Project/Art/TEXTURES/Aegir_storms.png"
CANONICAL_PRODUCTION_MESH = "Assets/_Project/Art/Models/gasgiant.asset"

CANONICAL_BAND_GUID = "6c173d4e1a858b34ca1b7e5610aae988"
CANONICAL_DETAIL_GUID = "e1aefa60ab4517644bb884257440872b"
CANONICAL_STORM_GUID = "d9d11072e85a2b54cacd11eaad6614a8"
CANONICAL_GAS_MATERIAL_GUID = "ab7b03af667690149bdc7be9a1ae023c"
CANONICAL_PRODUCTION_MESH_GUID = "fc0e817ab0eb67648b9a823825236a85"
CANONICAL_SKY_SHADER_GUID = "6a3f1601ae9165f4a001000000000001"
CANONICAL_IMPOSTOR_SHADER_GUID = "0661c64fe7dfd77469f3bd686cbc254e"
PRODUCTION_PREFAB_GUID = "9bafceacd557491409f6134514063ff4"
UNITY_BUILTIN_PRIMITIVE_GUID = "0000000000000000e000000000000000"
ALLOWED_CELESTIAL_SNAPSHOT_PUBLISHERS = frozenset(
    (
        CELESTIAL_ENGINE,
        "Assets/_Project/Scripts/Core/GlobalRegistry.cs",
    )
)
REQUIRED_PROOF_VIEW_IDS = frozenset(
    (
        "surface_clear_full",
        "surface_cloud_fog_half",
        "underwater_up",
        "horizon_veil",
        "crescent_low_light",
        "heavy_fog_occlusion",
    )
)
SURFACE_WEATHER_PROFILE_CONTRACTS = (
    ("SurfaceWeatherProfile_ClearCalm.asset", 0, 0.70, 1.05),
    ("SurfaceWeatherProfile_ClearBreeze.asset", 1, 0.90, 1.15),
    ("SurfaceWeatherProfile_Overcast.asset", 2, 1.10, 1.35),
    ("SurfaceWeatherProfile_HeavyRain.asset", 3, 1.35, 1.75),
    ("SurfaceWeatherProfile_ElectricalStorm.asset", 4, 1.65, 2.25),
)
SURFACE_WEATHER_FALLBACK_STORM_CONTRACTS = (
    ("FallbackClearCalmStormEmissionMultiplier", 0.70, 1.05),
    ("FallbackClearBreezeStormEmissionMultiplier", 0.90, 1.15),
    ("FallbackOvercastStormEmissionMultiplier", 1.10, 1.35),
    ("FallbackHeavyRainStormEmissionMultiplier", 1.35, 1.75),
    ("FallbackElectricalStormStormEmissionMultiplier", 1.65, 2.25),
)


@dataclass(frozen=True)
class Finding:
    severity: str
    code: str
    path: str
    message: str
    line: int | None = None

    def to_dict(self) -> dict[str, object]:
        payload: dict[str, object] = {
            "severity": self.severity,
            "code": self.code,
            "path": self.path,
            "message": self.message,
        }
        if self.line is not None:
            payload["line"] = self.line
        return payload


@dataclass(frozen=True)
class ValidationReport:
    findings: tuple[Finding, ...]
    checked_materials: int
    checked_textures: int
    checked_prefabs: int
    checked_scenes: int
    checked_source_files: int
    checked_proofs: int
    checked_weather_profiles: int

    @property
    def error_count(self) -> int:
        return sum(1 for finding in self.findings if finding.severity == "ERROR")

    @property
    def warning_count(self) -> int:
        return sum(1 for finding in self.findings if finding.severity == "WARNING")

    @property
    def status(self) -> str:
        return "AEGIR_GAS_GIANT_SOURCE_CONTRACT_PASS" if self.error_count == 0 else "AEGIR_GAS_GIANT_SOURCE_CONTRACT_FAIL"

    def to_dict(self) -> dict[str, object]:
        return {
            "status": self.status,
            "errors": self.error_count,
            "warnings": self.warning_count,
            "checkedMaterials": self.checked_materials,
            "checkedTextures": self.checked_textures,
            "checkedPrefabs": self.checked_prefabs,
            "checkedScenes": self.checked_scenes,
            "checkedSourceFiles": self.checked_source_files,
            "checkedProofs": self.checked_proofs,
            "checkedWeatherProfiles": self.checked_weather_profiles,
            "findings": [finding.to_dict() for finding in self.findings],
        }


def project_path(root: Path, rel_path: str) -> Path:
    return root / rel_path.replace("/", "\\")


def read_text(root: Path, rel_path: str) -> str | None:
    path = project_path(root, rel_path)
    if not path.is_file():
        return None
    return path.read_text(encoding="utf-8-sig", errors="replace").replace("\r\n", "\n")


def line_number(text: str, index: int) -> int:
    return text.count("\n", 0, index) + 1


def first_line_with(text: str, token: str) -> int | None:
    index = text.find(token)
    return line_number(text, index) if index >= 0 else None


def csharp_method_body(source: str, signature: str) -> tuple[str | None, int | None]:
    signature_index = source.find(signature)
    if signature_index < 0:
        return None, None

    brace_index = source.find("{", signature_index)
    if brace_index < 0:
        return None, line_number(source, signature_index)

    depth = 0
    for index in range(brace_index, len(source)):
        char = source[index]
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return source[brace_index + 1:index], line_number(source, signature_index)

    return None, line_number(source, signature_index)


def material_texture_guid(source: str, property_name: str) -> tuple[str, int | None]:
    property_token = f"- {property_name}:"
    property_index = source.find(property_token)
    if property_index < 0:
        return "", None
    texture_index = source.find("m_Texture:", property_index)
    if texture_index < 0:
        return "", line_number(source, property_index)
    guid_match = re.search(r"guid:\s*([0-9a-fA-F]{32})", source[texture_index:texture_index + 220])
    if guid_match is None:
        return "", line_number(source, texture_index)
    return guid_match.group(1).lower(), line_number(source, texture_index + guid_match.start())


def material_shader_guid(source: str) -> tuple[str, int | None]:
    shader_match = re.search(r"(?m)^\s*m_Shader:\s*\{[^\n]*guid:\s*([0-9a-fA-F]{32})", source)
    if shader_match is None:
        return "", None
    return shader_match.group(1).lower(), line_number(source, shader_match.start())


def meta_scalar(source: str, key: str) -> str:
    match = re.search(rf"(?m)^\s*{re.escape(key)}:\s*([^\n\r]+)", source)
    return match.group(1).strip() if match else ""


def asset_float_scalar(source: str, key: str) -> float | None:
    raw = meta_scalar(source, key)
    if not raw:
        return None
    try:
        return float(raw)
    except ValueError:
        return None


def csharp_const_float(source: str, name: str) -> tuple[float | None, int | None]:
    match = re.search(rf"(?m)\bconst\s+float\s+{re.escape(name)}\s*=\s*([-+]?\d+(?:\.\d+)?)f\s*;", source)
    if match is None:
        return None, None
    try:
        return float(match.group(1)), line_number(source, match.start())
    except ValueError:
        return None, line_number(source, match.start())


def add_error(findings: list[Finding], code: str, path: str, message: str, line: int | None = None) -> None:
    findings.append(Finding("ERROR", code, path, message, line))


def add_warning(findings: list[Finding], code: str, path: str, message: str, line: int | None = None) -> None:
    findings.append(Finding("WARNING", code, path, message, line))


def validate_material_slot(
    root: Path,
    findings: list[Finding],
    material_path: str,
    property_name: str,
    expected_guid: str,
) -> bool:
    source = read_text(root, material_path)
    if source is None:
        add_error(findings, "MISSING_MATERIAL", material_path, "Material file is missing.")
        return False

    actual_guid, line = material_texture_guid(source, property_name)
    if not actual_guid:
        add_error(findings, "MISSING_MATERIAL_TEXTURE_SLOT", material_path, f"Missing texture slot {property_name}.", line)
        return False

    if actual_guid.lower() != expected_guid:
        add_error(
            findings,
            "BAD_MATERIAL_TEXTURE_GUID",
            material_path,
            f"{property_name} expected {expected_guid}, got {actual_guid}.",
            line,
        )
        return False

    return True


def validate_material_shader(
    root: Path,
    findings: list[Finding],
    material_path: str,
    expected_guid: str,
) -> bool:
    source = read_text(root, material_path)
    if source is None:
        add_error(findings, "MISSING_MATERIAL", material_path, "Material file is missing.")
        return False

    actual_guid, line = material_shader_guid(source)
    if not actual_guid:
        add_error(findings, "MISSING_MATERIAL_SHADER", material_path, "Material m_Shader binding is missing.", line)
        return False

    if actual_guid.lower() != expected_guid:
        add_error(
            findings,
            "BAD_MATERIAL_SHADER_GUID",
            material_path,
            f"Expected shader GUID {expected_guid}, got {actual_guid}.",
            line,
        )
        return False

    return True


def validate_texture_meta(
    root: Path,
    findings: list[Finding],
    texture_path: str,
    expected_guid: str,
) -> bool:
    texture = project_path(root, texture_path)
    meta = project_path(root, texture_path + ".meta")
    ok = True
    if not texture.is_file():
        add_error(findings, "MISSING_TEXTURE", texture_path, "Texture file is missing.")
        ok = False
    if not meta.is_file():
        add_error(findings, "MISSING_TEXTURE_META", texture_path + ".meta", "Texture meta file is missing.")
        return False

    source = meta.read_text(encoding="utf-8-sig", errors="replace").replace("\r\n", "\n")
    actual_guid = meta_scalar(source, "guid").lower()
    if actual_guid != expected_guid:
        add_error(findings, "BAD_TEXTURE_GUID", texture_path + ".meta", f"Expected GUID {expected_guid}, got {actual_guid}.", first_line_with(source, "guid:"))
        ok = False
    if meta_scalar(source, "enableMipMap") != "1":
        add_error(findings, "BAD_TEXTURE_IMPORT", texture_path + ".meta", "enableMipMap must be 1 for sky/horizon/underwater stability.", first_line_with(source, "enableMipMap"))
        ok = False
    if meta_scalar(source, "isReadable") != "0":
        add_error(findings, "BAD_TEXTURE_IMPORT", texture_path + ".meta", "isReadable must be 0; runtime does not need CPU-readable gas giant textures.", first_line_with(source, "isReadable"))
        ok = False
    if meta_scalar(source, "streamingMipmaps") != "1":
        add_warning(findings, "TEXTURE_STREAMING_MIPS_OFF", texture_path + ".meta", "streamingMipmaps should be 1 to keep the always-visible sky asset from competing with water/terrain VRAM.", first_line_with(source, "streamingMipmaps"))
    max_size_raw = meta_scalar(source, "maxTextureSize")
    try:
        max_size = int(max_size_raw)
    except ValueError:
        max_size = 0
    if max_size < 2048:
        add_error(findings, "BAD_TEXTURE_IMPORT", texture_path + ".meta", f"maxTextureSize must be at least 2048, got {max_size_raw or '<missing>'}.", first_line_with(source, "maxTextureSize"))
        ok = False
    return ok


def validate_prefab(
    root: Path,
    findings: list[Finding],
    prefab_path: str,
    require_mesh_guid: str | None,
) -> bool:
    source = read_text(root, prefab_path)
    if source is None:
        add_error(findings, "MISSING_PREFAB", prefab_path, "Gas giant prefab file is missing.")
        return False

    ok = True
    for match in re.finditer(r"(?m)^\s*m_Mesh:\s*\{[^\n]*guid:\s*([0-9a-fA-F]{32})", source):
        mesh_guid = match.group(1).lower()
        if mesh_guid == UNITY_BUILTIN_PRIMITIVE_GUID:
            add_error(findings, "BUILTIN_PRIMITIVE_MESH", prefab_path, "Gas giant prefab uses Unity built-in primitive mesh.", line_number(source, match.start()))
            ok = False
        elif require_mesh_guid is not None and mesh_guid != require_mesh_guid:
            add_warning(findings, "NON_CANONICAL_PREFAB_MESH", prefab_path, f"Prefab mesh GUID is {mesh_guid}; expected {require_mesh_guid}.", line_number(source, match.start()))

    if CANONICAL_GAS_MATERIAL_GUID not in source:
        add_error(findings, "BAD_PREFAB_MATERIAL", prefab_path, f"Prefab does not reference canonical gas giant material GUID {CANONICAL_GAS_MATERIAL_GUID}.")
        ok = False

    required_renderer_flags = {
        "m_CastShadows: 0": "Renderer must not cast shadows.",
        "m_ReceiveShadows: 0": "Renderer must not receive shadows.",
        "m_DynamicOccludee: 0": "Renderer dynamic occlusion should be off for sky-scale source.",
        "m_MotionVectors: 0": "Renderer motion vectors should be off for sky-scale source.",
        "m_LightProbeUsage: 0": "Renderer must not use light probes.",
        "m_ReflectionProbeUsage: 0": "Renderer must not use reflection probes.",
    }
    for token, message in required_renderer_flags.items():
        if token not in source:
            add_error(findings, "BAD_PREFAB_RENDERER_STATE", prefab_path, message)
            ok = False
    return ok


def scene_prefab_override(source: str, property_path: str, expected_following_token: str) -> tuple[bool, int | None]:
    lines = source.splitlines()
    for index, line in enumerate(lines):
        if f"guid: {PRODUCTION_PREFAB_GUID}" not in line:
            continue
        block = "\n".join(lines[index:index + 7])
        if f"propertyPath: {property_path}" in block and expected_following_token in block:
            return True, index + 1
    return False, None


def validate_orbit_scene(root: Path, findings: list[Finding]) -> bool:
    source = read_text(root, ORBIT_SCENE)
    if source is None:
        add_error(findings, "MISSING_SCENE", ORBIT_SCENE, "Orbit scene is missing.")
        return False

    ok = True
    source_prefab_token = f"m_SourcePrefab: {{fileID: 100100000, guid: {PRODUCTION_PREFAB_GUID}"
    source_prefab_count = source.count(source_prefab_token)
    if source_prefab_count <= 0:
        add_error(findings, "MISSING_AEGIR_SCENE_PREFAB", ORBIT_SCENE, f"Orbit scene does not reference production Aegir prefab GUID {PRODUCTION_PREFAB_GUID}.")
        return False
    if source_prefab_count > 1:
        add_error(
            findings,
            "DUPLICATE_AEGIR_SCENE_PREFAB",
            ORBIT_SCENE,
            f"Orbit scene references {source_prefab_count} production Aegir prefab instances; keep one celestial renderer/source owner.",
            first_line_with(source, source_prefab_token),
        )
        ok = False

    checks = (
        ("m_Mesh", f"guid: {UNITY_BUILTIN_PRIMITIVE_GUID}", "Scene overrides Aegir mesh back to Unity built-in primitive."),
        ("m_CastShadows", "value: 1", "Scene overrides Aegir renderer to cast shadows."),
        ("m_LightProbeUsage", "value: 1", "Scene overrides Aegir renderer to use light probes."),
    )
    for property_path, token, message in checks:
        found, line = scene_prefab_override(source, property_path, token)
        if found:
            add_error(findings, "BAD_AEGIR_SCENE_OVERRIDE", ORBIT_SCENE, message, line)
            ok = False
    return ok


def validate_source_tooling(root: Path, findings: list[Finding]) -> bool:
    editor = read_text(root, EDITOR_VALIDATOR)
    product_face = read_text(root, PRODUCT_FACE_VALIDATOR)
    ok = True
    required_editor_tokens = (
        "Aegir Gas Giant Source Contract",
        "ValidateTextureImport",
        "ValidateOrbitSceneOverrides",
        "RepairPrefabSource",
        "RepairOrbitSceneFromMenu",
        "RevertAegirScenePrefabOverrides",
        "PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);",
        "ValidateMaterialFloat(gasMaterial, \"_StormEmission\", 1f",
        "EnsureMaterialFloat(GasGiantMaterialPath, \"_StormEmission\", 1f);",
        "private static int EnsureMaterialFloat",
        "ValidateRuntimeSourceContracts(report);",
        "CheckedSourceCount",
        "BadRuntimeSourceContract",
        "ResolveAegirSkyProjectionStormEmission()",
        "_AegirStormEmissionInvalidWarningHash",
        "AegirStormEmissionWarningCooldownFrames",
        "ReportAegirStormEmissionInvalidIfNeeded",
        "Shader.SetGlobalFloat(_ID_H8AegirStormEmission, ResolveAegirSkyProjectionStormEmission());",
        "Shader.SetGlobalFloat(_aegirStormEmissionId, 1f);",
        "stormBand * cloudTexture * 0.15 * stormEmission",
        "stormEmissionMultiplier",
        "weather-driven storm emission",
        "RestoreCelestialTextureDefaults();",
        "ClearAegirMaterialRuntimeCache();",
        "aegirRenderer.SetPropertyBlock(null);",
        "_aegirSharedMaterial = null;",
        "PublishOceanCelestialProjectionGlobals(aegirDirection)",
        "_ID_HectonEclipseWaterShadowParams",
        "_ID_HectonEclipseWaterShadowDirection",
        "_ID_HectonRingCausticsParams",
        "_ID_HectonRingCausticsDirection",
        "ResolveAupOceanShadowCenterRuntimeXZ",
        "TryResolvePlayerAup",
        UNITY_BUILTIN_PRIMITIVE_GUID,
        PRODUCTION_PREFAB_GUID,
    )
    if editor is None:
        add_error(findings, "MISSING_EDITOR_VALIDATOR", EDITOR_VALIDATOR, "Editor source validator is missing.")
        ok = False
    else:
        for token in required_editor_tokens:
            if token not in editor:
                add_error(findings, "EDITOR_VALIDATOR_CONTRACT_DRIFT", EDITOR_VALIDATOR, f"Editor validator missing token: {token}")
                ok = False

    if product_face is None:
        add_error(findings, "MISSING_PRODUCT_FACE_VALIDATOR", PRODUCT_FACE_VALIDATOR, "Product-face validator is missing.")
        ok = False
    elif "ValidateAegirGasGiantSource(report);" not in product_face:
        add_error(findings, "PRODUCT_FACE_GATE_NOT_WIRED", PRODUCT_FACE_VALIDATOR, "Product-face source gate does not call Aegir validator.")
        ok = False
    return ok


def validate_runtime_source_contract(root: Path, findings: list[Finding]) -> int:
    checked = 0
    source_contracts = (
        (
            CELESTIAL_ENGINE,
            (
                "TryClaimCelestialRuntimeAuthority()",
                "DisableDuplicateCelestialPresentation()",
                "PublishCelestialRuntimeSnapshot(!usingPublishedCelestialSnapshot)",
                "GlobalRegistry.PublishCelestialRuntimeSnapshot(in snapshot)",
                "PublishAegirSkyProjectionGlobals(aegirDirection)",
                "PublishOceanCelestialProjectionGlobals(aegirDirection)",
                "ResolveAegirSkyProjectionQuality01",
                "return math.max(math.saturate(profile.minimumQuality), quality);",
                "ResolveAegirSkyProjectionVisibility01",
                "ValidateAegirRendererMaterialCold",
                "TryRaiseCelestialSunAngleChanged",
                "TryRaiseCelestialPlanetPhaseChanged",
                "TryRaiseCelestialEclipseStarted",
                "TryRaiseCelestialEclipseEnded",
                "ReportCelestialEventDropIfBackpressured",
                "_CelestialEventDropWarningHash",
                "QueueDeferredRegister",
                "QueueDeferredUnregister",
                "ApplyDeferredListenerMutations",
                "DispatchToListener",
                "ReportQueueOverflow",
                "ReportDuplicateListenerRegistration",
                "ReportListenerRejected",
                "ReportListenerDispatchException",
                "ReportUnregisterMiss",
                "DrainQueuedEvents",
                "CelestialTruthReadFailure",
                "ReportCelestialTruthFallbackIfNeeded",
                "ResolveCelestialTruthFailureContextHash",
                "_CelestialTruthFallbackWarningHash",
                "PublishAegirPresentationWarning",
                "_AegirDuplicateOwnerWarningHash",
                "_AegirMissingMaterialWarningHash",
                "_AegirMissingBandTextureWarningHash",
                "_AegirStormEmissionInvalidWarningHash",
                "AegirStormEmissionWarningCooldownFrames",
                "ReportAegirStormEmissionInvalidIfNeeded",
                "ResolveAegirSkyProjectionStormEmission()",
                "block.SetFloat(_ID_StormEmission, ResolveAegirSkyProjectionStormEmission());",
                "ClearAegirSkyProjectionGlobals",
                "_H8AegirSunDirection",
                "_H8AegirPlanetCenterRadius",
                "_H8AegirRingPlaneInner",
                "_H8AegirOrbitScalars",
                "_H8AegirFlowPhaseValid",
                "_H8AegirStormEmission",
                "_H8GlobalQualityWeight",
                "_ID_HectonEclipseWaterShadowParams",
                "_ID_HectonEclipseWaterShadowDirection",
                "_ID_HectonRingCausticsParams",
                "_ID_HectonRingCausticsDirection",
                "ResolveAupOceanShadowCenterRuntimeXZ",
                "TryResolvePlayerAup",
            ),
        ),
        (
            ORBITAL_RELATIVITY_DIRECTOR,
            (
                "ICelestialRuntimeSnapshotReadModel readModel = ResolveCelestialRuntimeSnapshotReadModel();",
                "TryReadPublishedCelestialSnapshot(",
                "IsCelestialSnapshotReadable(in snapshot)",
                "ReportCelestialSnapshotFallbackIfNeeded(failure)",
                "CelestialSnapshotReadFailure.MissingService",
                "CelestialSnapshotReadFailure.InvalidSnapshot",
                "ResolveCelestialSnapshotFallbackSeverity",
                "CelestialSnapshotFallbackAnomalyCooldownFrames",
                "Shader.SetGlobalVector(_aegirSunDirectionId",
                "Shader.SetGlobalVector(_aegirPlanetCenterRadiusId",
                "Shader.SetGlobalFloat(_aegirStormEmissionId, 1f)",
                "Shader.SetGlobalFloat(_aegirFlowPhaseValidId, 1f)",
                "CacheCelestialRuntimeSnapshotReadModel(currentService as ICelestialRuntimeSnapshotReadModel)",
            ),
        ),
        (
            SURFACE_WEATHER_DIRECTOR,
            (
                "stormEmissionMultiplier = asset.StormEmissionMultiplier,",
                "activeCelestialEngine.SetSurfaceWeatherOverride(",
                "_currentState.stormEmissionMultiplier,",
                "activeCelestialEngine.ClearSurfaceWeatherOverride();",
                "ClearCelestialSurfaceWeatherOverride(previousCelestialEngine);",
                "CacheCelestialEngine(currentCelestialEngine);",
                "FallbackClearCalmStormEmissionMultiplier",
                "FallbackElectricalStormStormEmissionMultiplier",
            ),
        ),
        (
            AEGIR_SKY_SHADER,
            (
                "_H8AegirSunDirection",
                "_H8AegirPlanetCenterRadius",
                "_H8AegirRingPlaneInner",
                "_H8AegirOrbitScalars",
                "_H8AegirFlowPhaseValid",
                "_H8AegirStormEmission",
                "_H8GlobalQualityWeight",
                "float AegirStormEmission()",
                "clamp(_H8AegirStormEmission, 0.0, 4.0)",
                "bool RaySphere(",
                "bool RayRingPlane(",
                "float RingShadow(",
                "float3 DrawAegir(",
                "SAMPLE_TEXTURE2D(_AegirBandTex",
                "AegirFlowPhase(flowSpeed)",
                "float hardTerminator = smoothstep(-0.08, 0.18, ndotl);",
                "float limbDarken = lerp(1.0, 0.58",
                "color += _AtmosphereTint.rgb * scatter;",
                "RingShadow(hitPoint, lightDir",
                "systemVisibility = saturate(1.0 - _H8AegirSunDirection.w)",
                "float3 planetColor = DrawAegir",
                "stormBand * cloudTexture * 0.15 * stormEmission",
                "bands += float3(0.095, 0.052, 0.022) * stormSignal * stormEmission",
            ),
        ),
        (
            AEGIR_IMPOSTOR_SHADER,
            (
                "_PlanetPhase",
                "_H8GlobalQualityWeight",
                "_H8AegirSunDirection",
                "_HectonCelestialLightReadability0",
                "TEXTURE2D(_MainTex);",
                "TEXTURE2D(_DetailTex);",
                "TEXTURE2D(_StormTex);",
                "_StormEmission (\"Runtime Storm Emission\"",
                "half _StormEmission;",
                "_WarmTint.rgb * stormMask * _StormStrength * _StormEmission",
                "baseUv.x = frac(baseUv.x + _Rotation + _GlobalRotation + syncTime * _AutoRotationSpeed);",
                "half phase = smoothstep(_PhaseCenter - _PhaseSoftness",
                "half limbDarken = lerp(1.0h, 0.58h, pow(limb, 1.25h));",
                "_HectonCelestialLightReadability0.w / 112.0",
                "horizonVeil = 1.0h - smoothstep(",
                "color = lerp(color, veilColor, atmosphereMask);",
                "systemVisibility = min(systemVisibility",
            ),
        ),
    )

    for rel_path, tokens in source_contracts:
        source = read_text(root, rel_path)
        if source is None:
            add_error(findings, "MISSING_RUNTIME_SOURCE", rel_path, "Runtime source/shader contract file is missing.")
            continue

        checked += 1
        for token in tokens:
            if token not in source:
                add_error(
                    findings,
                    "RUNTIME_SOURCE_CONTRACT_DRIFT",
                    rel_path,
                    f"Runtime Aegir source contract token missing: {token}",
                )
    return checked


def validate_celestial_runtime_snapshot_single_writer(root: Path, findings: list[Finding]) -> None:
    scripts_root = project_path(root, "Assets/_Project/Scripts")
    if not scripts_root.is_dir():
        return

    for path in scripts_root.rglob("*.cs"):
        rel_path = path.relative_to(root).as_posix()
        source = path.read_text(encoding="utf-8-sig", errors="replace")
        if "PublishCelestialRuntimeSnapshot(" not in source:
            continue
        if rel_path in ALLOWED_CELESTIAL_SNAPSHOT_PUBLISHERS:
            continue
        add_error(
            findings,
            "DUPLICATE_CELESTIAL_RUNTIME_SNAPSHOT_OWNER",
            rel_path,
            "Only HectonCelestialEngine may publish the global CelestialRuntimeSnapshot; other systems must consume ICelestialRuntimeSnapshotReadModel.",
            first_line_with(source.replace("\r\n", "\n"), "PublishCelestialRuntimeSnapshot("),
        )


def validate_proof_artifacts(root: Path, findings: list[Finding]) -> int:
    checked = 0
    tool = read_text(root, PROOF_TOOL)
    if tool is None:
        add_error(findings, "MISSING_AEGIR_PROOF_TOOL", PROOF_TOOL, "Aegir proof contact-sheet builder is missing.")
    else:
        checked += 1
        required_tool_tokens = (
            "CANONICAL_BAND_TEXTURE",
            "CANONICAL_DETAIL_TEXTURE",
            "CANONICAL_STORM_TEXTURE",
            "surface_clear_full",
            "underwater_up",
            "horizon_veil",
            "phase_degrees",
            "limb",
            "storm_emission",
            "stormEmissionMultiplier",
            "quality_weight",
            "qualityWeight",
            "underwater-up readability",
            "quality-tier fallback",
            "AEGIR_GAS_GIANT_PROOF_CONTACT_SHEET_BUILT",
        )
        for token in required_tool_tokens:
            if token not in tool:
                add_error(findings, "AEGIR_PROOF_TOOL_CONTRACT_DRIFT", PROOF_TOOL, f"Proof tool missing token: {token}")

    manifest_path = project_path(root, PROOF_MANIFEST)
    image_path = project_path(root, PROOF_IMAGE)
    if not manifest_path.is_file():
        add_error(findings, "MISSING_AEGIR_PROOF_MANIFEST", PROOF_MANIFEST, "Aegir proof manifest is missing.")
        return checked

    checked += 1
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as exc:
        add_error(findings, "BAD_AEGIR_PROOF_MANIFEST_JSON", PROOF_MANIFEST, f"Manifest JSON is invalid: {exc}.")
        return checked

    if manifest.get("status") != "AEGIR_GAS_GIANT_PROOF_CONTACT_SHEET_BUILT":
        add_error(findings, "BAD_AEGIR_PROOF_STATUS", PROOF_MANIFEST, "Proof manifest status is not built.")
    if manifest.get("image") != PROOF_IMAGE:
        add_error(findings, "BAD_AEGIR_PROOF_IMAGE_BINDING", PROOF_MANIFEST, f"Proof manifest image must be {PROOF_IMAGE}.")

    source_textures = manifest.get("sourceTextures")
    if not isinstance(source_textures, list):
        add_error(findings, "BAD_AEGIR_PROOF_TEXTURES", PROOF_MANIFEST, "Proof manifest sourceTextures must be a list.")
    else:
        expected = {
            ("bands", CANONICAL_BAND_TEXTURE, CANONICAL_BAND_GUID),
            ("detail", CANONICAL_DETAIL_TEXTURE, CANONICAL_DETAIL_GUID),
            ("storms", CANONICAL_STORM_TEXTURE, CANONICAL_STORM_GUID),
        }
        actual = {
            (str(item.get("role", "")), str(item.get("path", "")), str(item.get("guid", "")).lower())
            for item in source_textures
            if isinstance(item, dict)
        }
        missing = sorted(expected - actual)
        for role, path, guid in missing:
            add_error(findings, "BAD_AEGIR_PROOF_TEXTURE_BINDING", PROOF_MANIFEST, f"Proof manifest missing {role} texture {path} {guid}.")

    views = manifest.get("views")
    if not isinstance(views, list):
        add_error(findings, "BAD_AEGIR_PROOF_VIEWS", PROOF_MANIFEST, "Proof manifest views must be a list.")
    else:
        view_ids = {str(view.get("id", "")) for view in views if isinstance(view, dict)}
        missing_views = sorted(REQUIRED_PROOF_VIEW_IDS - view_ids)
        for view_id in missing_views:
            add_error(findings, "MISSING_AEGIR_PROOF_VIEW", PROOF_MANIFEST, f"Proof manifest missing view {view_id}.")

        modes = {str(view.get("mode", "")) for view in views if isinstance(view, dict)}
        for mode in ("surface", "underwater", "horizon"):
            if mode not in modes:
                add_error(findings, "MISSING_AEGIR_PROOF_MODE", PROOF_MANIFEST, f"Proof manifest has no {mode} view.")

        phases = [
            float(view.get("phaseDegrees", 0.0))
            for view in views
            if isinstance(view, dict) and isinstance(view.get("phaseDegrees"), (int, float))
        ]
        if len({round(phase) for phase in phases}) < 4 or not phases or min(phases) > 30.0 or max(phases) < 110.0:
            add_error(findings, "BAD_AEGIR_PROOF_PHASE_COVERAGE", PROOF_MANIFEST, "Proof manifest must cover full/half/crescent phase angles.")
        if not any(float(view.get("underwater", 0.0)) > 0.5 for view in views if isinstance(view, dict)):
            add_error(findings, "MISSING_AEGIR_UNDERWATER_PROOF", PROOF_MANIFEST, "Proof manifest has no underwater-up visibility case.")
        if not any(float(view.get("horizonOcclusion", 0.0)) > 0.2 for view in views if isinstance(view, dict)):
            add_error(findings, "MISSING_AEGIR_HORIZON_PROOF", PROOF_MANIFEST, "Proof manifest has no horizon occlusion case.")
        if not any(float(view.get("cloud", 0.0)) > 0.5 or float(view.get("fog", 0.0)) > 0.5 for view in views if isinstance(view, dict)):
            add_error(findings, "MISSING_AEGIR_FOG_CLOUD_PROOF", PROOF_MANIFEST, "Proof manifest has no heavy fog/cloud case.")
        storm_emissions: list[float] = []
        for view in views:
            if not isinstance(view, dict):
                continue
            raw = view.get("stormEmissionMultiplier")
            if not isinstance(raw, (int, float)) or not math.isfinite(float(raw)):
                add_error(findings, "BAD_AEGIR_PROOF_STORM_EMISSION", PROOF_MANIFEST, "Proof view has no finite stormEmissionMultiplier.")
                continue
            storm_emissions.append(float(raw))
        if not storm_emissions or min(storm_emissions) > 0.95 or max(storm_emissions) < 1.5:
            add_error(
                findings,
                "BAD_AEGIR_PROOF_STORM_EMISSION_COVERAGE",
                PROOF_MANIFEST,
                "Proof manifest must cover reduced and elevated weather storm emission.",
            )
        quality_weights: list[float] = []
        for view in views:
            if not isinstance(view, dict):
                continue
            raw = view.get("qualityWeight")
            if not isinstance(raw, (int, float)) or not math.isfinite(float(raw)):
                add_error(findings, "BAD_AEGIR_PROOF_QUALITY_WEIGHT", PROOF_MANIFEST, "Proof view has no finite qualityWeight.")
                continue
            quality_weights.append(float(raw))
        if not quality_weights or min(quality_weights) > 0.6 or max(quality_weights) < 0.95:
            add_error(
                findings,
                "BAD_AEGIR_PROOF_QUALITY_COVERAGE",
                PROOF_MANIFEST,
                "Proof manifest must cover both low and high quality-tier Aegir fallback.",
            )

    contract = manifest.get("contract")
    if not isinstance(contract, dict) or contract.get("offlineProof") is not True or contract.get("unityRuntimeProof") is not False:
        add_error(findings, "BAD_AEGIR_PROOF_CONTRACT", PROOF_MANIFEST, "Proof manifest must mark offline proof and leave Unity runtime proof false.")
    else:
        covers = contract.get("covers")
        if not isinstance(covers, list) or "weather-driven storm emission" not in {str(item) for item in covers}:
            add_error(findings, "BAD_AEGIR_PROOF_CONTRACT", PROOF_MANIFEST, "Proof manifest must cover weather-driven storm emission.")
        if not isinstance(covers, list) or "quality-tier fallback" not in {str(item) for item in covers}:
            add_error(findings, "BAD_AEGIR_PROOF_CONTRACT", PROOF_MANIFEST, "Proof manifest must cover quality-tier fallback.")

    if not image_path.is_file():
        add_error(findings, "MISSING_AEGIR_PROOF_IMAGE", PROOF_IMAGE, "Aegir proof image is missing.")
    else:
        header = image_path.read_bytes()[:8]
        if header != b"\x89PNG\r\n\x1a\n":
            add_error(findings, "BAD_AEGIR_PROOF_IMAGE", PROOF_IMAGE, "Aegir proof image must be a PNG.")
        if image_path.stat().st_size <= 0:
            add_error(findings, "BAD_AEGIR_PROOF_IMAGE", PROOF_IMAGE, "Aegir proof image is empty.")

    return checked


def validate_surface_weather_profiles(root: Path, findings: list[Finding]) -> int:
    checked = 0
    previous_multiplier: float | None = None
    previous_path = ""
    for file_name, expected_kind, min_multiplier, max_multiplier in SURFACE_WEATHER_PROFILE_CONTRACTS:
        rel_path = f"{SURFACE_WEATHER_PROFILE_ROOT}/{file_name}"
        source = read_text(root, rel_path)
        if source is None:
            add_error(findings, "MISSING_SURFACE_WEATHER_PROFILE", rel_path, "Surface weather profile asset is missing.")
            continue

        checked += 1
        raw_kind = meta_scalar(source, "weatherKind")
        if raw_kind != str(expected_kind):
            add_error(
                findings,
                "BAD_SURFACE_WEATHER_KIND",
                rel_path,
                f"Expected weatherKind {expected_kind}, got {raw_kind or '<missing>'}.",
                first_line_with(source, "weatherKind:"),
            )

        multiplier = asset_float_scalar(source, "stormEmissionMultiplier")
        if multiplier is None or not math.isfinite(multiplier):
            add_error(
                findings,
                "BAD_AEGIR_WEATHER_STORM_MULTIPLIER",
                rel_path,
                "stormEmissionMultiplier must be a finite float.",
                first_line_with(source, "stormEmissionMultiplier:"),
            )
            continue

        if multiplier < min_multiplier or multiplier > max_multiplier:
            add_error(
                findings,
                "BAD_AEGIR_WEATHER_STORM_MULTIPLIER",
                rel_path,
                f"stormEmissionMultiplier {multiplier} must stay in [{min_multiplier}, {max_multiplier}] for believable Aegir weather response.",
                first_line_with(source, "stormEmissionMultiplier:"),
            )

        if previous_multiplier is not None and multiplier < previous_multiplier:
            add_error(
                findings,
                "BAD_AEGIR_WEATHER_STORM_MULTIPLIER_ORDER",
                rel_path,
                f"stormEmissionMultiplier {multiplier} is below previous profile {previous_path} value {previous_multiplier}.",
                first_line_with(source, "stormEmissionMultiplier:"),
            )

        previous_multiplier = multiplier
        previous_path = rel_path

    return checked


def validate_surface_weather_fallback_profiles(root: Path, findings: list[Finding]) -> int:
    source = read_text(root, SURFACE_WEATHER_DIRECTOR)
    if source is None:
        add_error(findings, "MISSING_RUNTIME_SOURCE", SURFACE_WEATHER_DIRECTOR, "Surface weather director source is missing.")
        return 0

    checked = 0
    previous_multiplier: float | None = None
    previous_name = ""
    for constant_name, min_multiplier, max_multiplier in SURFACE_WEATHER_FALLBACK_STORM_CONTRACTS:
        multiplier, line = csharp_const_float(source, constant_name)
        if multiplier is None or not math.isfinite(multiplier):
            add_error(
                findings,
                "BAD_AEGIR_FALLBACK_STORM_MULTIPLIER",
                SURFACE_WEATHER_DIRECTOR,
                f"Fallback constant {constant_name} must be a finite float.",
                line,
            )
            continue

        checked += 1
        if multiplier < min_multiplier or multiplier > max_multiplier:
            add_error(
                findings,
                "BAD_AEGIR_FALLBACK_STORM_MULTIPLIER",
                SURFACE_WEATHER_DIRECTOR,
                f"{constant_name} {multiplier} must stay in [{min_multiplier}, {max_multiplier}] for no-data Aegir weather fallback.",
                line,
            )

        if previous_multiplier is not None and multiplier < previous_multiplier:
            add_error(
                findings,
                "BAD_AEGIR_FALLBACK_STORM_MULTIPLIER_ORDER",
                SURFACE_WEATHER_DIRECTOR,
                f"{constant_name} {multiplier} is below previous fallback {previous_name} value {previous_multiplier}.",
                line,
            )

        if f"{constant_name}," not in source:
            add_error(
                findings,
                "BAD_AEGIR_FALLBACK_STORM_MULTIPLIER_BINDING",
                SURFACE_WEATHER_DIRECTOR,
                f"{constant_name} is declared but not passed into CreateFallbackProfile.",
                line,
            )

        previous_multiplier = multiplier
        previous_name = constant_name

    return checked


def validate_surface_weather_teardown_lifecycle(root: Path, findings: list[Finding]) -> None:
    source = read_text(root, SURFACE_WEATHER_DIRECTOR)
    if source is None:
        return

    body, line = csharp_method_body(source, "private void OnDestroy()")
    if body is None:
        add_error(
            findings,
            "BAD_SURFACE_WEATHER_TEARDOWN_CONTRACT",
            SURFACE_WEATHER_DIRECTOR,
            "Surface weather director must keep an OnDestroy teardown path for scene unload/domain reload.",
            line,
        )
        return

    required_tokens = (
        "DisposeWeatherMathBuffers(forceCompletePendingJob: true);",
        "ClearWeatherBindings();",
        "FlushWeatherShaderGlobals();",
        "TryUnregisterService();",
    )
    missing = [token for token in required_tokens if token not in body]
    if missing:
        add_error(
            findings,
            "BAD_SURFACE_WEATHER_TEARDOWN_CONTRACT",
            SURFACE_WEATHER_DIRECTOR,
            "OnDestroy must clear weather bindings and shader globals before unregistering runtime services. Missing: "
            + ", ".join(missing),
            line,
        )
        return

    clear_index = body.find("ClearWeatherBindings();")
    flush_index = body.find("FlushWeatherShaderGlobals();")
    unregister_index = body.find("TryUnregisterService();")
    if clear_index > unregister_index or flush_index > unregister_index:
        add_error(
            findings,
            "BAD_SURFACE_WEATHER_TEARDOWN_CONTRACT",
            SURFACE_WEATHER_DIRECTOR,
            "OnDestroy must clear weather overrides and shader globals before TryUnregisterService so stale Aegir weather state cannot survive scene unload.",
            line,
        )


def validate_celestial_surface_weather_clear_lifecycle(root: Path, findings: list[Finding]) -> None:
    source = read_text(root, CELESTIAL_ENGINE)
    if source is None:
        return

    body, line = csharp_method_body(source, "internal void ClearSurfaceWeatherOverride()")
    if body is None:
        add_error(
            findings,
            "BAD_CELESTIAL_SURFACE_WEATHER_CLEAR_CONTRACT",
            CELESTIAL_ENGINE,
            "Celestial engine must keep a surface-weather clear path for weather director teardown.",
            line,
        )
        return

    required_tokens = (
        "_surfaceWeatherOverrideActive = false;",
        "_surfaceWeatherFogOverrideActive = false;",
        "_surfaceWeatherStormEmissionMultiplier = 1f;",
        "if (Application.isPlaying)",
        "UpdateSkyMaterial();",
        "UpdateAegirMaterial();",
    )
    missing = [token for token in required_tokens if token not in body]
    if missing:
        add_error(
            findings,
            "BAD_CELESTIAL_SURFACE_WEATHER_CLEAR_CONTRACT",
            CELESTIAL_ENGINE,
            "ClearSurfaceWeatherOverride must reset weather state and immediately refresh sky/Aegir materials in play mode. Missing: "
            + ", ".join(missing),
            line,
        )
        return

    storm_reset_index = body.find("_surfaceWeatherStormEmissionMultiplier = 1f;")
    play_gate_index = body.find("if (Application.isPlaying)")
    update_sky_index = body.find("UpdateSkyMaterial();")
    update_aegir_index = body.find("UpdateAegirMaterial();")
    if storm_reset_index > update_aegir_index or play_gate_index > update_sky_index or play_gate_index > update_aegir_index:
        add_error(
            findings,
            "BAD_CELESTIAL_SURFACE_WEATHER_CLEAR_CONTRACT",
            CELESTIAL_ENGINE,
            "ClearSurfaceWeatherOverride must reset storm emission before refreshing materials, and material refresh must stay behind the play-mode guard.",
            line,
        )


def validate_celestial_aegir_material_cache_lifecycle(root: Path, findings: list[Finding]) -> None:
    source = read_text(root, CELESTIAL_ENGINE)
    if source is None:
        return

    clear_body, clear_line = csharp_method_body(source, "private void ClearAegirMaterialRuntimeCache()")
    if clear_body is None:
        add_error(
            findings,
            "BAD_CELESTIAL_AEGIR_CACHE_LIFECYCLE",
            CELESTIAL_ENGINE,
            "Celestial engine must keep an Aegir material-cache teardown path for scene unload/domain reload.",
            clear_line,
        )
        return

    required_clear_tokens = (
        "aegirRenderer.SetPropertyBlock(null);",
        "_aegirMPB.Clear();",
        "_aegirSharedMaterial = null;",
        "_aegirMainTexDefault = null;",
        "_aegirDetailTexDefault = null;",
        "_aegirEmissionMapDefault = null;",
        "_aegirCelestialOcclusionTexDefault = null;",
    )
    missing_clear = [token for token in required_clear_tokens if token not in clear_body]
    if missing_clear:
        add_error(
            findings,
            "BAD_CELESTIAL_AEGIR_CACHE_LIFECYCLE",
            CELESTIAL_ENGINE,
            "ClearAegirMaterialRuntimeCache must clear renderer property blocks and cached Aegir material/default texture handles. Missing: "
            + ", ".join(missing_clear),
            clear_line,
        )

    for signature in ("private void OnDisable()", "private void OnDestroy()"):
        body, line = csharp_method_body(source, signature)
        if body is None:
            add_error(
                findings,
                "BAD_CELESTIAL_AEGIR_CACHE_LIFECYCLE",
                CELESTIAL_ENGINE,
                f"Celestial engine must keep {signature} for Aegir material-cache teardown.",
                line,
            )
            continue

        restore_index = body.find("RestoreCelestialTextureDefaults();")
        clear_index = body.find("ClearAegirMaterialRuntimeCache();")
        if restore_index < 0 or clear_index < 0:
            add_error(
                findings,
                "BAD_CELESTIAL_AEGIR_CACHE_LIFECYCLE",
                CELESTIAL_ENGINE,
                f"{signature} must restore Aegir textures and clear material runtime cache before owner teardown completes.",
                line,
            )
            continue
        if restore_index > clear_index:
            add_error(
                findings,
                "BAD_CELESTIAL_AEGIR_CACHE_LIFECYCLE",
                CELESTIAL_ENGINE,
                f"{signature} must restore texture defaults before clearing cached Aegir material handles.",
                line,
            )


def validate(root: Path = ROOT) -> ValidationReport:
    root = root.resolve()
    findings: list[Finding] = []
    checked_materials = 0
    checked_textures = 0
    checked_prefabs = 0
    checked_scenes = 0
    checked_source_files = 0
    checked_proofs = 0
    checked_weather_profiles = 0

    material_checks = (
        (SKY_MATERIAL, "_AegirBandTex", CANONICAL_BAND_GUID),
        (GAS_MATERIAL, "_MainTex", CANONICAL_BAND_GUID),
        (GAS_MATERIAL, "_DetailTex", CANONICAL_DETAIL_GUID),
        (GAS_MATERIAL, "_StormTex", CANONICAL_STORM_GUID),
    )
    shader_checks = (
        (SKY_MATERIAL, CANONICAL_SKY_SHADER_GUID),
        (GAS_MATERIAL, CANONICAL_IMPOSTOR_SHADER_GUID),
    )
    for material_path, shader_guid in shader_checks:
        validate_material_shader(root, findings, material_path, shader_guid)

    checked_material_paths: set[str] = set()
    for material_path, slot, guid in material_checks:
        validate_material_slot(root, findings, material_path, slot, guid)
        checked_material_paths.add(material_path)
    checked_materials = len(checked_material_paths)
    gas_material_source = read_text(root, GAS_MATERIAL)
    if gas_material_source is not None and "- _StormEmission: 1" not in gas_material_source:
        add_error(
            findings,
            "BAD_MATERIAL_STORM_EMISSION_DEFAULT",
            GAS_MATERIAL,
            "Aegir gas giant material must keep neutral _StormEmission default at 1 so runtime weather can scale it predictably.",
            first_line_with(gas_material_source, "_StormEmission"),
        )

    texture_checks = (
        (CANONICAL_BAND_TEXTURE, CANONICAL_BAND_GUID),
        (CANONICAL_DETAIL_TEXTURE, CANONICAL_DETAIL_GUID),
        (CANONICAL_STORM_TEXTURE, CANONICAL_STORM_GUID),
    )
    for texture_path, guid in texture_checks:
        validate_texture_meta(root, findings, texture_path, guid)
        checked_textures += 1

    validate_prefab(root, findings, PRODUCTION_PREFAB, CANONICAL_PRODUCTION_MESH_GUID)
    checked_prefabs += 1
    validate_prefab(root, findings, LEGACY_PROLOGUE_PREFAB, CANONICAL_PRODUCTION_MESH_GUID)
    checked_prefabs += 1

    validate_orbit_scene(root, findings)
    checked_scenes += 1
    validate_source_tooling(root, findings)
    checked_source_files += validate_runtime_source_contract(root, findings)
    validate_celestial_surface_weather_clear_lifecycle(root, findings)
    validate_celestial_aegir_material_cache_lifecycle(root, findings)
    validate_surface_weather_teardown_lifecycle(root, findings)
    validate_celestial_runtime_snapshot_single_writer(root, findings)
    checked_proofs += validate_proof_artifacts(root, findings)
    checked_weather_profiles += validate_surface_weather_profiles(root, findings)
    checked_weather_profiles += validate_surface_weather_fallback_profiles(root, findings)

    return ValidationReport(
        findings=tuple(findings),
        checked_materials=checked_materials,
        checked_textures=checked_textures,
        checked_prefabs=checked_prefabs,
        checked_scenes=checked_scenes,
        checked_source_files=checked_source_files,
        checked_proofs=checked_proofs,
        checked_weather_profiles=checked_weather_profiles,
    )


def print_report(report: ValidationReport, json_output: bool) -> None:
    if json_output:
        print(json.dumps(report.to_dict(), indent=2, sort_keys=True))
        return

    print(report.status)
    print(
        f"checkedMaterials={report.checked_materials} checkedTextures={report.checked_textures} "
        f"checkedPrefabs={report.checked_prefabs} checkedScenes={report.checked_scenes} "
        f"checkedSourceFiles={report.checked_source_files} "
        f"checkedProofs={report.checked_proofs} checkedWeatherProfiles={report.checked_weather_profiles} "
        f"errors={report.error_count} warnings={report.warning_count}"
    )
    for finding in report.findings:
        location = finding.path if finding.line is None else f"{finding.path}:{finding.line}"
        print(f"{finding.severity} {finding.code} {location} {finding.message}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=str(ROOT), help="Repository root to validate.")
    parser.add_argument("--json", action="store_true", help="Emit machine-readable JSON.")
    parser.add_argument("--no-fail", action="store_true", help="Return 0 even when the contract is red.")
    args = parser.parse_args()

    report = validate(Path(args.root))
    print_report(report, json_output=bool(args.json))
    return 0 if args.no_fail or report.error_count == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
