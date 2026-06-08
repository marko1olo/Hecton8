#!/usr/bin/env python3
"""Validate construction-module Gemini material assignment table."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from typing import NamedTuple


ROOT = Path(__file__).resolve().parents[1]
APPLIER = ROOT / "Assets/_Project/Scripts/Editor/ConstructionGeminiMaterialApplier.cs"
BOOTSTRAP = ROOT / "Assets/_Project/Scripts/Editor/ConstructionBootstrapAuthoring.cs"
MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/GeminiSingleMaterials_Manifest.json"
GEMINI_ATLAS_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases"
REQUIRED_MAPS = ("BaseColor", "NormalGL", "MaskMap_UnityURP", "ARM_AO_Rough_Metal", "Height")
MAX_HIGH_SEAM_CONSTRUCTION_SCALE = 1.6
SCALE_EPSILON = 0.001
TEXTURE_SCALE_PATTERN = re.compile(
    r"m_Texture:\s*\{fileID:\s*\d+,\s*guid:\s*(?P<guid>[0-9a-fA-F]+),\s*type:\s*\d+\}\s*\n"
    r"\s*m_Scale:\s*\{x:\s*(?P<x>-?[0-9.]+),\s*y:\s*(?P<y>-?[0-9.]+)\}",
    re.MULTILINE,
)


class Assignment(NamedTuple):
    material_path: str
    material_id: str
    tiling_multiplier: float
    normal_scale: float
    metallic: float
    smoothness: float
    height_scale: float


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


def texture_scale_for_guid(material_text: str, guid: str) -> tuple[float, float] | None:
    for match in TEXTURE_SCALE_PATTERN.finditer(material_text):
        if match.group("guid").lower() == guid.lower():
            return float(match.group("x")), float(match.group("y"))
    return None


def sanitize_provider_name(value: str) -> str:
    if not value.strip():
        return "Atlas"
    return "".join(char if char.isalnum() or char in "_-" else "_" for char in value)


def collect_manifest_paths() -> dict[str, Path]:
    manifests = {"GeminiSingles_20260607": MANIFEST}
    if GEMINI_ATLAS_ROOT.exists():
        for manifest_path in sorted(GEMINI_ATLAS_ROOT.rglob("GeminiMaterialAtlas_Manifest.json")):
            provider = f"Gemini_{sanitize_provider_name(manifest_path.parent.name)}"
            manifests[provider] = manifest_path
    return manifests


def load_manifest_assets(manifest_paths: dict[str, Path]) -> dict[str, dict]:
    assets: dict[str, dict] = {}
    for manifest_path in manifest_paths.values():
        payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        for asset in payload.get("assets", []) or []:
            asset_id = str(asset.get("id", "")).strip()
            if asset_id and asset_id not in assets:
                assets[asset_id] = asset
    return assets


def duplicate_material_ids(manifest_paths: dict[str, Path]) -> dict[str, list[str]]:
    owners: dict[str, list[str]] = {}
    for provider, manifest_path in manifest_paths.items():
        payload = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        for asset in payload.get("assets", []) or []:
            asset_id = str(asset.get("id", "")).strip()
            if not asset_id:
                continue
            owners.setdefault(asset_id, []).append(provider)
    return {asset_id: providers for asset_id, providers in owners.items() if len(providers) > 1}


def parse_assignments() -> list[Assignment]:
    text = APPLIER.read_text(encoding="utf-8-sig")
    pattern = re.compile(
        r"new\s+Assignment\(\s*\"(?P<material>[^\"]+)\"\s*,\s*\"(?P<material_id>[^\"]+)\"\s*,"
        r"\s*(?P<tiling>[0-9.]+)f\s*,\s*(?P<normal>[0-9.]+)f\s*,"
        r"\s*(?P<metallic>[0-9.]+)f\s*,\s*(?P<smoothness>[0-9.]+)f\s*,"
        r"\s*(?P<height>[0-9.]+)f",
        re.MULTILINE,
    )
    return [
        Assignment(
            material_path=match.group("material"),
            material_id=match.group("material_id"),
            tiling_multiplier=float(match.group("tiling")),
            normal_scale=float(match.group("normal")),
            metallic=float(match.group("metallic")),
            smoothness=float(match.group("smoothness")),
            height_scale=float(match.group("height")),
        )
        for match in pattern.finditer(text)
    ]


def validate_assignment(
    assignment: Assignment,
    asset: dict,
    post_apply: bool,
    errors: list[str],
    warnings: list[str],
) -> None:
    if not assignment.material_path.startswith("Assets/_Project/Art/Materials/Construction/"):
        errors.append(f"{assignment.material_path}: construction assignment outside Construction material folder")

    target_material = project_path(assignment.material_path)
    if not target_material.exists():
        errors.append(f"Missing target construction material: {assignment.material_path}")

    if asset.get("watermarkRisk"):
        errors.append(f"Watermark-risk material assigned to construction material: {assignment.material_id}")

    if not bool(asset.get("stationPropAllowed", False)):
        errors.append(f"{assignment.material_id}: stationPropAllowed must be true for construction assignment")

    maps = asset.get("maps", {}) or {}
    for map_key in REQUIRED_MAPS:
        map_path = str(maps.get(map_key, "")).strip()
        if not map_path:
            errors.append(f"Missing map key {map_key}: {assignment.material_id}")
            continue
        if not project_path(map_path).exists():
            errors.append(f"Missing map file {map_key}: {assignment.material_id}: {map_path}")

    source_tiling = asset.get("tilingScale")
    if not isinstance(source_tiling, (int, float)):
        errors.append(f"{assignment.material_id}: missing numeric source tilingScale")
        return

    effective_tiling = float(source_tiling) * assignment.tiling_multiplier
    if effective_tiling < 0.25 or effective_tiling > 16.0:
        errors.append(f"{assignment.material_id}: effective tiling out of range {effective_tiling:.3f}")

    for key, value, lower, upper in (
        ("tilingMultiplier", assignment.tiling_multiplier, 0.1, 4.0),
        ("normalScale", assignment.normal_scale, 0.0, 2.0),
        ("metallic", assignment.metallic, 0.0, 1.0),
        ("smoothness", assignment.smoothness, 0.0, 1.0),
        ("heightScale", assignment.height_scale, 0.0, 0.05),
    ):
        if value < lower or value > upper:
            errors.append(f"{assignment.material_id}:{key}: {value} outside {lower}..{upper}")

    seam_after = asset.get("seamScoreAfter")
    if isinstance(seam_after, (int, float)) and float(seam_after) > MAX_HIGH_SEAM_CONSTRUCTION_SCALE:
        if effective_tiling > MAX_HIGH_SEAM_CONSTRUCTION_SCALE:
            errors.append(
                f"{assignment.material_id}: high seamScoreAfter={seam_after} effectiveTiling={effective_tiling:.3f}; lower assignment tiling multiplier"
            )
        else:
            warnings.append(
                f"{assignment.material_id}: high seamScoreAfter={seam_after} constrained by effectiveTiling={effective_tiling:.3f}"
            )

    if not post_apply or not target_material.exists():
        return

    target_text = target_material.read_text(encoding="utf-8-sig")
    for map_key in ("BaseColor", "NormalGL", "MaskMap_UnityURP", "Height"):
        map_path = str(maps.get(map_key, "")).strip()
        if not map_path:
            continue

        guid = read_guid(project_path(map_path))
        if not guid:
            errors.append(f"{assignment.material_id}:{map_key}: missing texture .meta guid")
        elif guid not in target_text:
            errors.append(
                f"{assignment.material_path}: missing applied {map_key} texture guid for {assignment.material_id}"
            )
        else:
            scale = texture_scale_for_guid(target_text, guid)
            if scale is None:
                errors.append(f"{assignment.material_path}: missing applied {map_key} texture scale")
            elif abs(scale[0] - effective_tiling) > SCALE_EPSILON or abs(scale[1] - effective_tiling) > SCALE_EPSILON:
                errors.append(
                    f"{assignment.material_path}: applied {map_key} scale {scale[0]:.3f},{scale[1]:.3f} expected {effective_tiling:.3f}"
                )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--post-apply",
        action="store_true",
        help="also verify construction .mat files contain expected texture GUIDs after Unity apply",
    )
    args = parser.parse_args()

    errors: list[str] = []
    warnings: list[str] = []
    assignments = parse_assignments()
    manifest_paths = collect_manifest_paths()
    assets = load_manifest_assets(manifest_paths)
    duplicates = duplicate_material_ids(manifest_paths)
    for asset_id, providers in sorted(duplicates.items()):
        errors.append(f"Duplicate Gemini material id across providers: {asset_id} providers={','.join(providers)}")

    if not BOOTSTRAP.exists():
        errors.append(f"Missing construction bootstrap authoring source: {display_path(BOOTSTRAP)}")
    else:
        bootstrap_text = BOOTSTRAP.read_text(encoding="utf-8-sig")
        if "ScifiFacility/Prefabs/decals" in bootstrap_text:
            errors.append("ConstructionBootstrapAuthoring must not depend on vendor ScifiFacility decal prefabs")
        if "IndustrialStripeDecalPrefabPath" in bootstrap_text or "IndustrialScuffDecalPrefabPath" in bootstrap_text:
            errors.append("ConstructionBootstrapAuthoring must not keep vendor industrial decal prefab path constants")
        if "AttachPrefabDecal" in bootstrap_text:
            errors.append("ConstructionBootstrapAuthoring must use generated quad decal helper, not prefab decal helper")
        for required in (
            "WorldSupportGeneratedDecalMaterialBuilder.AreSourceTexturesAvailable()",
            "WorldSupportGeneratedDecalMaterialBuilder.Build()",
            "WorldSupportGeneratedDecalMaterialBuilder.WarningStripeMaterialPath",
            "WorldSupportGeneratedDecalMaterialBuilder.CutterScorchMaterialPath",
            "AttachGeneratedDecal",
            "GameObject.CreatePrimitive(PrimitiveType.Quad)",
        ):
            if required not in bootstrap_text:
                errors.append(f"ConstructionBootstrapAuthoring missing generated decal route token: {required}")

    if not assignments:
        errors.append(f"No assignments parsed from {display_path(APPLIER)}")
    else:
        text = APPLIER.read_text(encoding="utf-8-sig")
        for required in (
            "ExternalPbrTexturePackImporter.ImportExternalPbrTexturePacks();",
            "LoadAllManifestAssets()",
            "ValidateAssignments(assets);",
            "RequireAsset(assets, assignment)",
            "RequireTargetMaterial(assignment)",
            "RequireTexture(asset.maps.BaseColor",
            "Missing required map {mapKey}",
            "Missing map payload",
            "Invalid material asset entry",
            "ResolveProjectFilePath",
            "GeminiAtlasRoot",
            "GeminiMaterialAtlas_Manifest.json",
            "SetTextureScaleIfPresent",
            "TilingScale(asset, assignment)",
            'SetTextureIfPresent(target, "_MetallicGlossMap", maskMap)',
            'SetTextureIfPresent(target, "_OcclusionMap", maskMap)',
        ):
            if required not in text:
                errors.append(f"Construction applier missing required binding token: {required}")
        if "Debug.LogWarning($" in text:
            errors.append("Construction applier must not downgrade missing manifest/material/map failures to warnings")
        if "out string failure" in text or "RecordFailure(" in text:
            errors.append("Construction applier must not use bool/out-string failure masking")
        if "Skipped=" in text:
            errors.append("Construction applier must not report skipped assignments instead of failing the stage")
        if "File.Exists(manifestPath)" in text or "File.ReadAllText(manifestPath)" in text or "Directory.Exists(GeminiAtlasRoot)" in text:
            errors.append("Construction applier must resolve project-root file paths instead of using cwd-relative manifest paths")

    seen_materials: set[str] = set()
    for assignment in assignments:
        if assignment.material_path in seen_materials:
            errors.append(f"Duplicate construction material assignment: {assignment.material_path}")
        seen_materials.add(assignment.material_path)

        asset = assets.get(assignment.material_id)
        if asset is None:
            errors.append(f"Missing Gemini material id: {assignment.material_id}")
            continue

        validate_assignment(assignment, asset, args.post_apply, errors, warnings)

    print("CONSTRUCTION_GEMINI_MATERIAL_ASSIGNMENT_VALIDATOR")
    print(f"applier={display_path(APPLIER)}")
    print(f"bootstrap={display_path(BOOTSTRAP)}")
    for provider, path in manifest_paths.items():
        print(f"manifest[{provider}]={display_path(path)}")
    print(f"postApply={args.post_apply}")
    print(f"assignments={len(assignments)}")
    print(f"errors={len(errors)}")
    print(f"warnings={len(warnings)}")
    for error in errors:
        print(f"ERROR: {error}")
    for warning in warnings:
        print(f"WARN: {warning}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
