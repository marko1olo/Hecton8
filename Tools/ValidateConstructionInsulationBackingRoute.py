#!/usr/bin/env python3
"""Validate Batch34 damp-insulation backing integration for construction prefabs."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
INTEGRATOR = ROOT / "Assets/_Project/Scripts/Editor/ConstructionInsulationBackingIntegrator.cs"
UNITY_APPLIER = ROOT / "Assets/_Project/Scripts/Editor/GeminiMaterialIntegrationApplier.cs"
UNITY_APPLY_RUNNER = ROOT / "Tools/RunGeminiMaterialUnityApplyAll.ps1"
STATIC_PREFLIGHT = ROOT / "Tools/RunGeminiMaterialStaticPreflight.ps1"
MATERIAL_PATH = ROOT / "Assets/_Project/Art/Materials/Construction/Mat_Module_InsulationBacking.mat"
MATERIAL_ID = "gemini_Batch20260608_TextureExpansion_b34_3421_damped_insulation_blanket_material"
GEMINI_ATLAS_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases"
REQUIRED_MAPS = ("BaseColor", "NormalGL", "MaskMap_UnityURP", "Height")
PREFAB_CHILDREN = {
    "Assets/_Project/Prefabs/Construction/Final/PFB_Debris_WreckField.prefab": (
        "InsulationBacking_HullInterior",
        "InsulationBacking_ServicePlateTear",
    ),
    "Assets/_Project/Prefabs/Construction/Final/PFB_Ruin_ClusterMedium.prefab": (
        "InsulationBacking_ModuleBOpenFace",
        "InsulationBacking_BridgeUnderside",
    ),
    "Assets/_Project/Prefabs/Construction/Final/PFB_Ruin_Megastructure.prefab": (
        "InsulationBacking_TowerCutFace",
        "InsulationBacking_BridgeCutFace",
    ),
}


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def read_guid(asset_path: Path) -> str:
    meta_path = asset_path.with_suffix(asset_path.suffix + ".meta")
    if not meta_path.exists():
        return ""
    match = re.search(
        r"^guid:\s*([0-9a-fA-F]+)\s*$",
        meta_path.read_text(encoding="utf-8-sig"),
        re.MULTILINE,
    )
    return match.group(1) if match else ""


def load_material_asset() -> dict | None:
    if not GEMINI_ATLAS_ROOT.exists():
        return None

    matches: list[dict] = []
    for manifest_path in sorted(GEMINI_ATLAS_ROOT.rglob("GeminiMaterialAtlas_Manifest.json")):
        payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        for asset in payload.get("assets", []) or []:
            if str(asset.get("id", "")).strip() == MATERIAL_ID:
                matches.append(asset)
    if len(matches) > 1:
        return {"__duplicateCount": len(matches)}
    return matches[0] if matches else None


def validate_static(errors: list[str], warnings: list[str]) -> dict | None:
    if not INTEGRATOR.exists():
        errors.append(f"Missing integrator: {display_path(INTEGRATOR)}")
        return None

    text = INTEGRATOR.read_text(encoding="utf-8-sig")
    for token in (
        "ConstructionInsulationBackingIntegrator",
        f'public const string MaterialPath = "{display_path(MATERIAL_PATH)}"',
        f'public const string MaterialId = "{MATERIAL_ID}"',
        "ValidateRoute(assets);",
        "Invalid insulation backing route",
        "Insulation backing apply failed",
        "Missing map payload",
        "Invalid material asset entry",
        "ResolveProjectFilePath",
        "PrefabUtility.LoadPrefabContents",
        "PrefabUtility.SaveAsPrefabAsset",
        "PrefabUtility.UnloadPrefabContents",
        "GameObject.CreatePrimitive(PrimitiveType.Cube)",
        "UnityEngine.Object.DestroyImmediate(collider)",
        "renderer.sharedMaterial = material",
        "SetTextureIfPresent(material, \"_BaseMap\", baseColor)",
        "SetTextureIfPresent(material, \"_BumpMap\", normal)",
        "SetTextureIfPresent(material, \"_MetallicGlossMap\", maskMap)",
        "SetTextureIfPresent(material, \"_OcclusionMap\", maskMap)",
    ):
        if token not in text:
            errors.append(f"integrator missing required token: {token}")

    if "Debug.LogWarning($" in text:
        errors.append("construction insulation backing integrator must not downgrade missing source/prefab/map failures to warnings")
    if "Directory.Exists(GeminiAtlasRoot)" in text or "File.Exists(manifestPath)" in text or "File.ReadAllText(manifestPath)" in text:
        errors.append("construction insulation backing integrator must resolve project-root file paths instead of using cwd-relative manifest paths")

    for prefab_path, children in PREFAB_CHILDREN.items():
        if prefab_path not in text:
            errors.append(f"integrator missing target prefab path: {prefab_path}")
        for child_name in children:
            if child_name not in text:
                errors.append(f"integrator missing backing panel child: {child_name}")

    asset = load_material_asset()
    if asset is None:
        errors.append(f"Missing material source in Batch34 atlas manifests: {MATERIAL_ID}")
        return None
    if "__duplicateCount" in asset:
        errors.append(f"Duplicate material source in Batch34 atlas manifests: {MATERIAL_ID} count={asset['__duplicateCount']}")
        return None

    if asset.get("watermarkRisk"):
        errors.append(f"{MATERIAL_ID}: watermarkRisk=true")
    if not bool(asset.get("stationPropAllowed", False)):
        errors.append(f"{MATERIAL_ID}: stationPropAllowed must be true")
    maps = asset.get("maps", {}) or {}
    for key in REQUIRED_MAPS:
        raw = str(maps.get(key, "")).strip()
        if not raw:
            errors.append(f"{MATERIAL_ID}: missing map key {key}")
            continue
        if not project_path(raw).exists():
            errors.append(f"{MATERIAL_ID}: missing map file {key}: {raw}")

    if not UNITY_APPLIER.exists():
        errors.append(f"Missing Unity applier: {display_path(UNITY_APPLIER)}")
    else:
        applier_text = UNITY_APPLIER.read_text(encoding="utf-8-sig")
        if "ConstructionInsulationBackingIntegrator.Apply()" not in applier_text:
            errors.append("GeminiMaterialIntegrationApplier.ApplyAll must execute ConstructionInsulationBackingIntegrator.Apply")

    if not UNITY_APPLY_RUNNER.exists():
        errors.append(f"Missing apply runner: {display_path(UNITY_APPLY_RUNNER)}")
    else:
        runner_text = UNITY_APPLY_RUNNER.read_text(encoding="utf-8-sig")
        if "Hecton8.EditorTools.ConstructionInsulationBackingIntegrator.Apply" in runner_text:
            errors.append("Unity apply-all runner must not launch ConstructionInsulationBackingIntegrator separately; central applier owns this stage")
        expected_post_apply_call = 'Invoke-PythonValidator -ValidatorPath $constructionInsulationValidator -Arguments @("--post-apply")'
        if expected_post_apply_call not in runner_text:
            errors.append("Unity apply-all runner must post-apply validate construction insulation backing")

    if not STATIC_PREFLIGHT.exists():
        errors.append(f"Missing static preflight runner: {display_path(STATIC_PREFLIGHT)}")
    elif "ValidateConstructionInsulationBackingRoute.py" not in STATIC_PREFLIGHT.read_text(encoding="utf-8-sig"):
        errors.append("static preflight runner must include ValidateConstructionInsulationBackingRoute.py")

    return asset


def validate_post_apply(asset: dict, errors: list[str], warnings: list[str]) -> None:
    if not MATERIAL_PATH.exists():
        errors.append(f"Missing generated material after Unity apply: {display_path(MATERIAL_PATH)}")
        return

    material_text = MATERIAL_PATH.read_text(encoding="utf-8-sig")
    for key in REQUIRED_MAPS:
        raw = str((asset.get("maps", {}) or {}).get(key, "")).strip()
        if not raw:
            continue
        guid = read_guid(project_path(raw))
        if not guid:
            errors.append(f"{MATERIAL_ID}:{key}: missing texture .meta guid")
        elif guid not in material_text:
            errors.append(f"{display_path(MATERIAL_PATH)}: missing applied {key} texture guid")

    material_guid = read_guid(MATERIAL_PATH)
    if not material_guid:
        errors.append(f"{display_path(MATERIAL_PATH)}: missing material .meta guid")
        return

    for prefab_path, children in PREFAB_CHILDREN.items():
        prefab = project_path(prefab_path)
        if not prefab.exists():
            errors.append(f"Missing target prefab after Unity apply: {prefab_path}")
            continue
        prefab_text = prefab.read_text(encoding="utf-8-sig")
        for child_name in children:
            if child_name not in prefab_text:
                errors.append(f"{prefab_path}: missing backing panel child {child_name}")
        if material_guid not in prefab_text:
            errors.append(f"{prefab_path}: missing insulation backing material guid")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--post-apply", action="store_true")
    args = parser.parse_args()

    errors: list[str] = []
    warnings: list[str] = []
    asset = validate_static(errors, warnings)
    if args.post_apply and asset is not None:
        validate_post_apply(asset, errors, warnings)

    for warning in warnings:
        print(f"WARNING: {warning}")
    for error in errors:
        print(f"ERROR: {error}")

    if errors:
        print(f"Construction insulation backing validation failed. errors={len(errors)} warnings={len(warnings)}")
        return 1

    print(f"Construction insulation backing validation passed. warnings={len(warnings)} postApply={args.post_apply}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
