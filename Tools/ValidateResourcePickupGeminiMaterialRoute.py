#!/usr/bin/env python3
"""Validate generated Gemini PBR handoff for current resource pickup materials."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
APPLIER = ROOT / "Assets/_Project/Scripts/Editor/ResourcePickupGeminiMaterialApplier.cs"
APPLY_ALL = ROOT / "Assets/_Project/Scripts/Editor/GeminiMaterialIntegrationApplier.cs"
BOOTSTRAP = ROOT / "Assets/_Project/Scripts/Editor/ResourceWorldBootstrapAuthoring.cs"
UNITY_APPLY_RUNNER = ROOT / "Tools/RunGeminiMaterialUnityApplyAll.ps1"
STATIC_PREFLIGHT = ROOT / "Tools/RunGeminiMaterialStaticPreflight.ps1"
RESOURCE_MATERIAL_ROOT = ROOT / "Assets/_Project/Art/Materials/Resources"
GENERATED_MATERIAL_ROOT = ROOT / "Assets/_Project/Art/Materials/Generated/ExternalPBR_20260607"
GEMINI_SINGLE_MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/GeminiSingleMaterials_Manifest.json"
GEMINI_BIOME_MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBiomeMaterialIntake_20260607/GeminiBiomeMaterials_Manifest.json"
GEMINI_ATLAS_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases"


EXPECTED_ASSIGNMENTS = [
    (
        "Assets/_Project/Art/Materials/Resources/Mat_Resource_Scrap.mat",
        "gemini_Batch20260607_MicroPanel_repaired_salvage_metal",
        "Gemini_Batch20260607_MicroPanel",
    ),
    (
        "Assets/_Project/Art/Materials/Resources/Mat_Resource_Copper.mat",
        "gemini_Batch20260608_TextureExpansion_b34_3404_abyssal_manganese_nodule_plain",
        "Gemini_Batch20260608_TextureExpansion",
    ),
    (
        "Assets/_Project/Art/Materials/Resources/Mat_Resource_Silica.mat",
        "gemini_biome_20260607_pale_tube_coral_calcium",
        "GeminiBiome_20260607",
    ),
    (
        "Assets/_Project/Art/Materials/Resources/Mat_Resource_Fiber.mat",
        "gemini_biome_20260607_living_kelp_frond_surface",
        "GeminiBiome_20260607",
    ),
    (
        "Assets/_Project/Art/Materials/Resources/Mat_Resource_Membrane.mat",
        "gemini_biome_20260607_soft_jelly_membrane",
        "GeminiBiome_20260607",
    ),
    (
        "Assets/_Project/Art/Materials/Resources/Mat_Resource_Silver.mat",
        "gemini_Batch20260607_MicroPanel_worn_steel_inset",
        "Gemini_Batch20260607_MicroPanel",
    ),
    (
        "Assets/_Project/Art/Materials/Resources/Mat_Resource_Sulfur.mat",
        "gemini_biome_20260607_hydrothermal_vent_mineral_crust",
        "GeminiBiome_20260607",
    ),
    (
        "Assets/_Project/Art/Materials/Resources/Mat_Resource_Resin.mat",
        "gemini_biome_20260607_bioluminescent_coral_flesh",
        "GeminiBiome_20260607",
    ),
]
FORBIDDEN_PICKUP_ATLAS_DIRECT_ASSIGNMENT_IDS = {
    "B34-3448",
    "B34-3449",
    "B34-3450",
    "gemini_Batch20260608_TextureExpansion_b34_3448_resource_nodule_pickup_uv_atlas",
    "gemini_Batch20260608_TextureExpansion_b34_3449_industrial_salvage_small_parts_atlas",
    "gemini_Batch20260608_TextureExpansion_b34_3450_data_core_wet_circuit_ceramic_atlas",
}


def display(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sanitize_provider_name(value: str) -> str:
    if not value.strip():
        return "Atlas"
    return "".join(c if c.isalnum() or c in "_-" else "_" for c in value)


def iter_material_assets() -> dict[tuple[str, str], dict]:
    manifests: list[tuple[str, Path]] = []
    if GEMINI_SINGLE_MANIFEST.exists():
        manifests.append(("GeminiSingles_20260607", GEMINI_SINGLE_MANIFEST))
    if GEMINI_BIOME_MANIFEST.exists():
        manifests.append(("GeminiBiome_20260607", GEMINI_BIOME_MANIFEST))
    if GEMINI_ATLAS_ROOT.exists():
        for path in sorted(GEMINI_ATLAS_ROOT.rglob("GeminiMaterialAtlas_Manifest.json")):
            provider = "Gemini_" + sanitize_provider_name(path.parent.name)
            manifests.append((provider, path))

    records: dict[tuple[str, str], dict] = {}
    for provider, path in manifests:
        payload = load_json(path)
        for asset in payload.get("assets", []) or []:
            asset_id = str(asset.get("id", "")).strip()
            if asset_id:
                records[(provider, asset_id)] = asset
    return records


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


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def extract_assignments(applier_text: str) -> list[tuple[str, str, str]]:
    pattern = re.compile(
        r"new\s+Assignment\(\s*"
        r'"(?P<target>Assets/[^"]+\.mat)",\s*'
        r'"(?P<material>[^"]+)",\s*'
        r'"(?P<provider>[^"]+)"',
        re.MULTILINE,
    )
    return [
        (match.group("target"), match.group("material"), match.group("provider"))
        for match in pattern.finditer(applier_text)
    ]


def validate_static(errors: list[str]) -> None:
    for path in (APPLIER, APPLY_ALL, BOOTSTRAP):
        if not path.exists():
            errors.append(f"missing required source: {display(path)}")
    if errors:
        return

    applier_text = APPLIER.read_text(encoding="utf-8-sig")
    apply_all_text = APPLY_ALL.read_text(encoding="utf-8-sig")
    bootstrap_text = BOOTSTRAP.read_text(encoding="utf-8-sig")

    assignments = extract_assignments(applier_text)
    if assignments != EXPECTED_ASSIGNMENTS:
        errors.append("resource pickup Gemini assignment contract changed or reordered unexpectedly")

    if "Batch34 pickup atlases remain UV/mesh source assets, not cube material maps." not in applier_text:
        errors.append("resource pickup applier must preserve the Batch34 pickup-atlas lane boundary comment")

    for target, material_id, provider in assignments:
        if material_id in FORBIDDEN_PICKUP_ATLAS_DIRECT_ASSIGNMENT_IDS:
            errors.append(f"resource pickup material must not directly bind Batch34 pickup atlas source: {target} <- {material_id}")

    if "ValidateSourceMaterials();" not in applier_text:
        errors.append("resource pickup applier must validate all generated source materials before writing target assets")
    if "Missing generated source materials" not in applier_text:
        errors.append("resource pickup applier must fail fast with missing source material count and first missing path")
    if "ApplyAssignment(assignment);" not in applier_text:
        errors.append("resource pickup applier must apply each assignment directly after source validation")
    if "Missing generated source material: " not in applier_text:
        errors.append("resource pickup applier must throw with generated source path for stale source handles")
    if "Resource material apply had failures=" in applier_text:
        errors.append("resource pickup applier must not downgrade apply failures to warnings")
    if "TryApply(" in applier_text or "out string failure" in applier_text or "report.Failures" in applier_text:
        errors.append("resource pickup applier must not use bool/out-string failure masking")
    if "Debug.LogWarning" in applier_text:
        errors.append("resource pickup applier must not downgrade apply failures to warnings")

    if "AreSourceMaterialsAvailable()" not in applier_text:
        errors.append("resource pickup applier must expose guarded source-availability check for bootstrap rebuild path")

    if "ResourcePickupGeminiMaterialApplier.AreSourceMaterialsAvailable()" not in bootstrap_text:
        errors.append("ResourceWorldBootstrapAuthoring must guard generated resource material self-heal by source availability")
    if "ResourcePickupGeminiMaterialApplier.Apply(false)" not in bootstrap_text:
        errors.append("ResourceWorldBootstrapAuthoring must reapply Gemini resource materials when generated sources are available")

    importer_index = apply_all_text.find("ExternalPbrTexturePackImporter.ImportExternalPbrTexturePacks()")
    suit_index = apply_all_text.find("ProductFace.ProductFacePlayerSuitGeminiMaterialApplier.Apply(false)")
    resource_index = apply_all_text.find("ResourcePickupGeminiMaterialApplier.Apply(false)")
    held_index = apply_all_text.find("HeldToolExternalPbrMaterialApplier.ApplyExternalPbrToHeldTools(false)")
    if importer_index < 0 or suit_index < 0 or resource_index < 0 or held_index < 0:
        errors.append("Gemini apply-all is missing importer, player-suit, resource-pickup, or held-tool call")
    elif not (importer_index < suit_index < resource_index < held_index):
        errors.append("resource pickup material applier must run after generated material import/player-suit palette and before downstream tool appliers")

    if not UNITY_APPLY_RUNNER.exists():
        errors.append(f"missing Unity apply runner: {display(UNITY_APPLY_RUNNER)}")
    else:
        runner_text = UNITY_APPLY_RUNNER.read_text(encoding="utf-8-sig")
        expected_post_apply_call = 'Invoke-PythonValidator -ValidatorPath $resourcePickupMaterialValidator -Arguments @("--post-apply")'
        if expected_post_apply_call not in runner_text:
            errors.append("Unity apply-all runner must post-apply validate resource pickup material route")

    if not STATIC_PREFLIGHT.exists():
        errors.append(f"missing static preflight runner: {display(STATIC_PREFLIGHT)}")
    elif "ValidateResourcePickupGeminiMaterialRoute.py" not in STATIC_PREFLIGHT.read_text(encoding="utf-8-sig"):
        errors.append("static preflight runner must include ValidateResourcePickupGeminiMaterialRoute.py")

    material_assets = iter_material_assets()
    for target, material_id, provider in EXPECTED_ASSIGNMENTS:
        asset = material_assets.get((provider, material_id))
        if asset is None:
            errors.append(f"missing generated material source in manifests: {provider}/{material_id}")
            continue
        if str(asset.get("sourceType", "")).strip().upper() == "PICKUP_ATLAS":
            errors.append(f"resource pickup primitive material assignment uses a PICKUP_ATLAS source: {provider}/{material_id}")
        target_path = ROOT / target
        if not target_path.exists():
            errors.append(f"missing target resource material asset: {target}")
        maps = asset.get("maps", {}) or {}
        for map_key in ("BaseColor", "NormalGL", "MaskMap_UnityURP"):
            raw_path = str(maps.get(map_key, "")).strip()
            if not raw_path:
                errors.append(f"{provider}/{material_id}: missing source map key {map_key}")
                continue
            if not project_path(raw_path).exists():
                errors.append(f"{provider}/{material_id}: missing source map file {map_key}: {raw_path}")


def validate_post_apply(errors: list[str]) -> None:
    material_assets = iter_material_assets()
    for target, material_id, provider in EXPECTED_ASSIGNMENTS:
        generated_source = GENERATED_MATERIAL_ROOT / provider / f"MAT_EXT_{provider}_{material_id}.mat"
        target_path = ROOT / target
        if not generated_source.exists():
            errors.append(f"post-apply missing generated source material: {display(generated_source)}")
        if not target_path.exists():
            errors.append(f"post-apply missing target resource material: {target}")
            continue

        material_text = target_path.read_text(encoding="utf-8-sig")
        asset = material_assets.get((provider, material_id), {})
        maps = asset.get("maps", {}) or {}
        for map_key in ("BaseColor", "NormalGL", "MaskMap_UnityURP"):
            raw_path = str(maps.get(map_key, "")).strip()
            guid = read_guid(project_path(raw_path)) if raw_path else ""
            if not guid:
                errors.append(f"post-apply missing texture guid for {provider}/{material_id} {map_key}")
            elif guid not in material_text:
                errors.append(f"post-apply target {target} missing {map_key} texture guid from {material_id}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--post-apply", action="store_true")
    args = parser.parse_args()

    errors: list[str] = []
    validate_static(errors)
    if args.post_apply:
        validate_post_apply(errors)

    print("RESOURCE_PICKUP_GEMINI_MATERIAL_ROUTE_VALIDATOR")
    print(f"applier={display(APPLIER)}")
    print(f"bootstrap={display(BOOTSTRAP)}")
    print(f"assignments={len(EXPECTED_ASSIGNMENTS)}")
    print(f"postApply={args.post_apply}")
    print(f"errors={len(errors)}")
    for error in errors:
        print(f"ERROR {error}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
