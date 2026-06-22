#!/usr/bin/env python3
"""Validate generated Gemini detail primitives on held/world tool prefabs."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ROOT_PATH = ROOT
INTEGRATOR = ROOT / "Assets/_Project/Scripts/Editor/ToolSurfaceDetailGeminiIntegrator.cs"
UNITY_APPLIER = ROOT / "Assets/_Project/Scripts/Editor/GeminiMaterialIntegrationApplier.cs"
UNITY_APPLY_RUNNER = ROOT / "Tools/RunGeminiMaterialUnityApplyAll.ps1"
STATIC_PREFLIGHT = ROOT / "Tools/RunGeminiMaterialStaticPreflight.ps1"
MATERIAL_ROOT = ROOT / "Assets/_Project/Art/Materials/Generated/ExternalPBR_20260607"
GEMINI_ATLAS_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases"
STATIC_MANIFESTS = {
    "GeminiSingles_20260607": ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/GeminiSingleMaterials_Manifest.json",
}
REQUIRED_MATERIAL_IDS = {
    "gemini_Batch20260607_MicroPanel_gray_polymer",
    "gemini_Batch20260607_MicroPanel_aged_green_service_metal",
    "gemini_20260607_transparent_pressure_glass_edge_wear",
    "gemini_20260607_white_ceramic_sensor_casing",
    "gemini_20260607_fine_ribbed_metal_trim",
    "gemini_20260607_black_waterproof_grip_rubber",
    "gemini_20260607_clean_nasa_punk_tool_housing_metal",
    "gemini_Batch20260608_TextureExpansion_b34_3417_amber_emergency_lens_material",
}
DETAIL_PATTERN = re.compile(
    r'new\("(?P<prefab>Assets/[^"]+\.prefab)",\s*"(?P<child>[^"]+)",\s*(?P<provider>[^,]+),\s*"(?P<material>[^"]+)"',
    re.MULTILINE,
)
CONST_PATTERN = re.compile(r'private\s+const\s+string\s+(?P<name>[A-Za-z0-9_]+)\s*=\s*"(?P<value>[^"]+)";')


class ValidationError(Exception):
    pass


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str) -> Path:
    if raw.startswith("Assets/"):
        return ROOT_PATH / raw
    return Path(raw).resolve()


def read_guid(asset_path: Path) -> str:
    meta_path = asset_path.with_name(asset_path.name + ".meta")
    if not meta_path.exists():
        raise ValidationError(f"Missing meta file: {display_path(asset_path)}")
    match = re.search(
        r"^guid:\s*([0-9a-fA-F]+)\s*$",
        meta_path.read_text(encoding="utf-8-sig"),
        re.MULTILINE,
    )
    return match.group(1) if match else ""


def sanitize_provider_name(value: str) -> str:
    if not value.strip():
        return "Atlas"
    return "".join(char if char.isalnum() or char in "_-" else "_" for char in value)


def collect_manifest_paths() -> dict[str, Path]:
    manifests = dict(STATIC_MANIFESTS)
    if GEMINI_ATLAS_ROOT.exists():
        for manifest_path in sorted(GEMINI_ATLAS_ROOT.rglob("GeminiMaterialAtlas_Manifest.json")):
            manifests[f"Gemini_{sanitize_provider_name(manifest_path.parent.name)}"] = manifest_path
    return manifests


def load_manifest_assets(manifest_path: Path) -> dict[str, dict]:
    payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    return {
        str(asset.get("id", "")).strip(): asset
        for asset in payload.get("assets", []) or []
        if asset.get("id")
    }


def load_constants(text: str) -> dict[str, str]:
    return {match.group("name"): match.group("value") for match in CONST_PATTERN.finditer(text)}


def resolve_provider(raw: str, constants: dict[str, str]) -> str:
    value = raw.strip()
    if value.startswith('"') and value.endswith('"'):
        return value.strip('"')
    return constants.get(value, value)


def material_asset_path(provider: str, material_id: str) -> Path:
    return MATERIAL_ROOT / provider / f"MAT_EXT_{provider}_{material_id}.mat"


def validate_static(errors: list[str], warnings: list[str]) -> tuple[list[dict], dict[str, dict[str, dict]]]:
    if not INTEGRATOR.exists():
        errors.append(f"Missing integrator: {display_path(INTEGRATOR)}")
        return [], {}

    text = INTEGRATOR.read_text(encoding="utf-8-sig")
    for token in (
        "ToolSurfaceDetailGeminiIntegrator",
        "ValidateDetails();",
        "RequirePrefab(spec)",
        "RequireMaterial(spec)",
        "GameObject.CreatePrimitive(PrimitiveType.Cube)",
        "UnityEngine.Object.DestroyImmediate(collider)",
        "renderer.shadowCastingMode = ShadowCastingMode.Off",
        "renderer.sharedMaterial = material",
        "PrefabUtility.LoadPrefabContents",
        "PrefabUtility.SaveAsPrefabAsset",
    ):
        if token not in text:
            errors.append(f"integrator missing required token: {token}")

    if "File.Exists(spec.PrefabPath)" in text:
        errors.append("tool surface detail integrator must use Unity AssetDatabase prefab resolution, not cwd-relative File.Exists")
    if "Debug.LogWarning($" in text:
        errors.append("tool surface detail integrator must not downgrade missing prefab/material failures to warnings")
    if "RecordFailure(" in text or "failures=" in text:
        errors.append("tool surface detail integrator must not aggregate required detail failures after validation")

    constants = load_constants(text)
    manifest_paths = collect_manifest_paths()
    manifests = {provider: load_manifest_assets(path) for provider, path in manifest_paths.items()}
    details: list[dict] = []
    seen_pairs: set[tuple[str, str]] = set()
    used_materials: set[str] = set()

    for index, match in enumerate(DETAIL_PATTERN.finditer(text)):
        provider = resolve_provider(match.group("provider"), constants)
        prefab = match.group("prefab")
        child = match.group("child")
        material = match.group("material")
        details.append({"prefab": prefab, "child": child, "provider": provider, "material": material})
        used_materials.add(material)

        pair = (prefab, child)
        if pair in seen_pairs:
            errors.append(f"duplicate detail child assignment: prefab={prefab} child={child}")
        seen_pairs.add(pair)

        if not project_path(prefab).exists():
            errors.append(f"detail[{index}] missing target prefab: {prefab}")
        if not prefab.startswith("Assets/_Project/Prefabs/Tools/Held/") and not prefab.startswith("Assets/_Project/Prefabs/Items/Tools/"):
            errors.append(f"detail[{index}] prefab outside held/world tool routes: {prefab}")

        provider_assets = manifests.get(provider)
        if provider_assets is None:
            errors.append(f"detail[{index}] unknown provider: {provider}")
            continue
        asset = provider_assets.get(material)
        if asset is None:
            errors.append(f"detail[{index}] material id not in provider manifest: provider={provider} material={material}")
            continue
        if not bool(asset.get("heldToolAllowed", False)):
            errors.append(f"detail[{index}] material is not held-tool allowed: provider={provider} material={material}")
        if bool(asset.get("watermarkRisk", False)):
            errors.append(f"detail[{index}] material has watermark risk: provider={provider} material={material}")

    missing_materials = sorted(REQUIRED_MATERIAL_IDS - used_materials)
    for material_id in missing_materials:
        errors.append(f"required previously-unbound detail material is not used: {material_id}")

    if len(details) < 14:
        errors.append(f"expected at least 14 held/world tool detail specs, got {len(details)}")

    if not UNITY_APPLIER.exists():
        errors.append(f"Missing Unity applier: {display_path(UNITY_APPLIER)}")
    else:
        applier_text = UNITY_APPLIER.read_text(encoding="utf-8-sig")
        if "ToolSurfaceDetailGeminiIntegrator.Apply()" not in applier_text:
            errors.append("GeminiMaterialIntegrationApplier.ApplyAll must execute ToolSurfaceDetailGeminiIntegrator.Apply")

    if not UNITY_APPLY_RUNNER.exists():
        errors.append(f"Missing apply runner: {display_path(UNITY_APPLY_RUNNER)}")
    else:
        runner_text = UNITY_APPLY_RUNNER.read_text(encoding="utf-8-sig")
        if "Hecton8.EditorTools.ToolSurfaceDetailGeminiIntegrator.Apply" in runner_text:
            errors.append("Unity apply-all runner must not launch ToolSurfaceDetailGeminiIntegrator separately; central applier owns this stage")
        expected_post_apply_call = 'Invoke-PythonValidator -ValidatorPath $toolSurfaceDetailValidator -Arguments @("--post-apply")'
        if expected_post_apply_call not in runner_text:
            errors.append("Unity apply-all runner must post-apply validate tool surface detail route")

    if not STATIC_PREFLIGHT.exists():
        errors.append(f"Missing static preflight runner: {display_path(STATIC_PREFLIGHT)}")
    elif "ValidateToolSurfaceDetailGeminiRoute.py" not in STATIC_PREFLIGHT.read_text(encoding="utf-8-sig"):
        errors.append("static preflight runner must include ValidateToolSurfaceDetailGeminiRoute.py")

    return details, manifests


def validate_post_apply(details: list[dict], errors: list[str], warnings: list[str]) -> None:
    for detail in details:
        prefab_path = project_path(detail["prefab"])
        material_path = material_asset_path(detail["provider"], detail["material"])
        if not material_path.exists():
            errors.append(f"post-apply missing generated material asset: {display_path(material_path)}")
            continue
        material_guid = read_guid(material_path)
        if not material_guid:
            errors.append(f"post-apply missing generated material guid: {display_path(material_path)}")
            continue
        if not prefab_path.exists():
            continue

        prefab_text = prefab_path.read_text(encoding="utf-8-sig")
        if detail["child"] not in prefab_text:
            errors.append(f"{detail['prefab']}: missing detail child {detail['child']}")
        if material_guid not in prefab_text:
            errors.append(
                f"{detail['prefab']}: missing detail material guid child={detail['child']} provider={detail['provider']} material={detail['material']}"
            )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--post-apply", action="store_true")
    args = parser.parse_args()

    errors: list[str] = []
    warnings: list[str] = []
    details, _ = validate_static(errors, warnings)
    if args.post_apply:
        validate_post_apply(details, errors, warnings)

    print("TOOL_SURFACE_DETAIL_GEMINI_ROUTE_VALIDATOR")
    print(f"integrator={display_path(INTEGRATOR)}")
    print(f"details={len(details)}")
    print(f"postApply={args.post_apply}")
    print(f"errors={len(errors)}")
    print(f"warnings={len(warnings)}")
    for error in errors:
        print(f"ERROR {error}")
    for warning in warnings:
        print(f"WARN {warning}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
