#!/usr/bin/env python3
"""Validate Batch34 UV/pickup atlas material handoff builder and Unity runner wiring."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BUILDER = ROOT / "Assets/_Project/Scripts/Editor/Batch34UvAtlasMaterialHandoffBuilder.cs"
UNITY_APPLIER = ROOT / "Assets/_Project/Scripts/Editor/GeminiMaterialIntegrationApplier.cs"
UNITY_APPLY_RUNNER = ROOT / "Tools/RunGeminiMaterialUnityApplyAll.ps1"
STATIC_PREFLIGHT = ROOT / "Tools/RunGeminiMaterialStaticPreflight.ps1"
SOURCE_ATLAS_MANIFEST = (
    ROOT
    / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/GeminiBatch34SourceAtlases_Manifest.json"
)
ALPHA_MANIFEST = (
    ROOT
    / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/GeminiBatch34AlphaCandidates_Manifest.json"
)
OUTPUT_FOLDER = "Assets/_Project/Art/Materials/WorldProceduralProxy/Batch34UvAtlasHandoff"
REQUIRED_SOURCES = {
    "B34-3433": {
        "texture": "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/flora_uv/TX_B34-3433_brine_vane_flora_uv_atlas_AlphaCandidate.png",
        "template": "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_abyssal.mat",
        "material": f"{OUTPUT_FOLDER}/MAT_B34_Flora_BrineVaneAtlas_Cutout.mat",
        "alpha": True,
    },
    "B34-3434": {
        "texture": "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/flora_uv/TX_B34-3434_shallow_alien_seagrass_blade_atlas_AlphaCandidate.png",
        "template": "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_patch_dense.mat",
        "material": f"{OUTPUT_FOLDER}/MAT_B34_Flora_SeagrassBladeAtlas_Cutout.mat",
        "alpha": True,
    },
    "B34-3435": {
        "texture": "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/flora_uv/TX_B34-3435_plate_coral_rim_uv_atlas_AlphaCandidate.png",
        "template": "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_plate.mat",
        "material": f"{OUTPUT_FOLDER}/MAT_B34_Flora_PlateCoralRimAtlas_Cutout.mat",
        "alpha": True,
    },
    "B34-3436": {
        "texture": "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/flora_uv/TX_B34-3436_sponge_pore_organic_atlas_AlphaCandidate.png",
        "template": "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat",
        "material": f"{OUTPUT_FOLDER}/MAT_B34_Flora_SpongePoreOrganicAtlas_Cutout.mat",
        "alpha": True,
    },
    "B34-3437": {
        "texture": "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/UvAtlases/TX_B34_3437_kelp_holdfast_root_atlas_BaseColorCandidate.jpg",
        "template": "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_abyssal.mat",
        "material": f"{OUTPUT_FOLDER}/MAT_B34_Flora_KelpHoldfastRootAtlas_Opaque.mat",
        "alpha": False,
    },
    "B34-3439": {
        "texture": "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/flora_uv/TX_B34-3439_spore_pod_and_seed_sac_atlas_AlphaCandidate.png",
        "template": "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_plant_giant.mat",
        "material": f"{OUTPUT_FOLDER}/MAT_B34_Flora_SporePodSeedSacAtlas_Cutout.mat",
        "alpha": True,
    },
    "B34-3441": {
        "texture": "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/fauna_uv/TX_B34-3441_neutral_grazer_skin_uv_atlas_AlphaCandidate.png",
        "template": "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_spawn_passive.mat",
        "material": f"{OUTPUT_FOLDER}/MAT_B34_Fauna_NeutralGrazerSkinAtlas_Cutout.mat",
        "alpha": True,
    },
    "B34-3442": {
        "texture": "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/fauna_uv/TX_B34-3442_filter_feeder_gill_membrane_atlas_AlphaCandidate.png",
        "template": "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_spawn_passive.mat",
        "material": f"{OUTPUT_FOLDER}/MAT_B34_Fauna_FilterFeederGillMembraneAtlas_Cutout.mat",
        "alpha": True,
    },
    "B34-3445": {
        "texture": "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/fauna_uv/TX_B34-3445_translucent_larva_egg_sac_atlas_AlphaCandidate.png",
        "template": "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_egg_cluster.mat",
        "material": f"{OUTPUT_FOLDER}/MAT_B34_Fauna_LarvaEggSacAtlas_Cutout.mat",
        "alpha": True,
    },
    "B34-3446": {
        "texture": "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/fauna_uv/TX_B34-3446_scavenged_carcass_bone_flesh_atlas_AlphaCandidate.png",
        "template": "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_large_threat.mat",
        "material": f"{OUTPUT_FOLDER}/MAT_B34_Fauna_CarcassBoneFleshAtlas_Cutout.mat",
        "alpha": True,
    },
    "B34-3448": {
        "texture": "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/pickup_resource/TX_B34-3448_resource_nodule_pickup_uv_atlas_AlphaCandidate.png",
        "template": "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_pocket_resource.mat",
        "material": f"{OUTPUT_FOLDER}/MAT_B34_Pickup_ResourceNoduleAtlas_Cutout.mat",
        "alpha": True,
    },
    "B34-3449": {
        "texture": "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/PickupAtlases/TX_B34_3449_industrial_salvage_small_parts_atlas_BaseColorCandidate.jpg",
        "template": "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_debris_scatter.mat",
        "material": f"{OUTPUT_FOLDER}/MAT_B34_Pickup_IndustrialSalvageSmallPartsAtlas_Opaque.mat",
        "alpha": False,
    },
    "B34-3450": {
        "texture": "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/pickup_salvage/TX_B34-3450_data_core_wet_circuit_ceramic_atlas_AlphaCandidate.png",
        "template": "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_debris_scatter.mat",
        "material": f"{OUTPUT_FOLDER}/MAT_B34_Pickup_DataCoreCircuitAtlas_Cutout.mat",
        "alpha": True,
    },
}
SPEC_PATTERN = re.compile(
    r'new\s+AtlasMaterialSpec\(\s*"(?P<source>B34-\d+)",\s*"(?P<texture>Assets/[^"]+)",\s*"(?P<template>Assets/[^"]+\.mat)",\s*"(?P<material>Assets/[^"]+\.mat)",\s*(?P<alpha>true|false)',
    re.MULTILINE,
)


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


def load_entries(path: Path) -> dict[str, dict]:
    if not path.exists():
        return {}
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    return {str(entry.get("id", "")).strip(): entry for entry in payload.get("entries", []) or [] if entry.get("id")}


def validate_static(errors: list[str], warnings: list[str]) -> list[dict]:
    if not BUILDER.exists():
        errors.append(f"Missing builder: {display_path(BUILDER)}")
        return []

    source_entries = load_entries(SOURCE_ATLAS_MANIFEST)
    alpha_entries = load_entries(ALPHA_MANIFEST)
    text = BUILDER.read_text(encoding="utf-8-sig")
    for token in (
        "Batch34UvAtlasMaterialHandoffBuilder",
        f'public const string OutputFolder = "{OUTPUT_FOLDER}"',
        "ValidateSpecs();",
        "CreateOrUpdateMaterial(Specs[i]);",
        "RequireTexture(spec)",
        "RequireTemplateMaterial(spec)",
        "new Material(template)",
        "material.CopyPropertiesFromMaterial(template)",
        "AssetDatabase.CreateAsset(material, spec.MaterialPath)",
        "SetTextureIfPresent(material, \"_BaseMap\", texture)",
        "SetFloatIfPresent(material, \"_AlphaClip\", spec.UseAlphaClip ? 1f : 0f)",
        "SetKeyword(material, \"_ALPHATEST_ON\", spec.UseAlphaClip)",
    ):
        if token not in text:
            errors.append(f"builder missing required token: {token}")

    if "Debug.LogWarning($" in text:
        errors.append("Batch34 UV atlas handoff builder must not downgrade missing texture/template failures to warnings")
    if "out string failure" in text or "RecordFailure(" in text:
        errors.append("Batch34 UV atlas handoff builder must not use bool/out-string failure masking")

    specs = []
    seen_sources: set[str] = set()
    for match in SPEC_PATTERN.finditer(text):
        source_id = match.group("source")
        texture = match.group("texture")
        template = match.group("template")
        material = match.group("material")
        use_alpha = match.group("alpha") == "true"
        specs.append(
            {
                "sourceId": source_id,
                "texture": texture,
                "template": template,
                "material": material,
                "alpha": use_alpha,
            }
        )
        seen_sources.add(source_id)

        expected = REQUIRED_SOURCES.get(source_id)
        if expected is None:
            errors.append(f"unexpected handoff source id: {source_id}")
            continue
        if texture != expected["texture"]:
            errors.append(f"{source_id}: wrong texture path. expected={expected['texture']} actual={texture}")
        if template != expected["template"]:
            errors.append(f"{source_id}: wrong template path. expected={expected['template']} actual={template}")
        if material != expected["material"]:
            errors.append(f"{source_id}: wrong material path. expected={expected['material']} actual={material}")
        if use_alpha != expected["alpha"]:
            errors.append(f"{source_id}: wrong alpha mode. expected={expected['alpha']} actual={use_alpha}")
        if not project_path(texture).exists():
            errors.append(f"{source_id}: missing texture asset: {texture}")
        if not project_path(template).exists():
            errors.append(f"{source_id}: missing template material asset: {template}")
        if source_id not in source_entries:
            errors.append(f"{source_id}: missing source atlas manifest entry")
        if use_alpha and source_id not in alpha_entries:
            errors.append(f"{source_id}: alpha handoff missing alpha manifest entry")

    for source_id in sorted(set(REQUIRED_SOURCES) - seen_sources):
        errors.append(f"missing required handoff source id: {source_id}")

    if len(specs) != len(REQUIRED_SOURCES):
        errors.append(f"expected {len(REQUIRED_SOURCES)} handoff specs, got {len(specs)}")

    if not UNITY_APPLIER.exists():
        errors.append(f"Missing Unity applier: {display_path(UNITY_APPLIER)}")
    else:
        applier_text = UNITY_APPLIER.read_text(encoding="utf-8-sig")
        if "Batch34UvAtlasMaterialHandoffBuilder.Apply()" not in applier_text:
            errors.append("GeminiMaterialIntegrationApplier.ApplyAll must execute Batch34UvAtlasMaterialHandoffBuilder.Apply")

    if not UNITY_APPLY_RUNNER.exists():
        errors.append(f"Missing apply runner: {display_path(UNITY_APPLY_RUNNER)}")
    else:
        runner_text = UNITY_APPLY_RUNNER.read_text(encoding="utf-8-sig")
        if "Hecton8.EditorTools.Batch34UvAtlasMaterialHandoffBuilder.Apply" in runner_text:
            errors.append("Unity apply-all runner must not launch Batch34UvAtlasMaterialHandoffBuilder separately; central applier owns this stage")
        expected_post_apply_call = 'Invoke-PythonValidator -ValidatorPath $uvAtlasMaterialHandoffValidator -Arguments @("--post-apply")'
        if expected_post_apply_call not in runner_text:
            errors.append("Unity apply-all runner must post-apply validate Batch34 UV atlas handoff")

    if not STATIC_PREFLIGHT.exists():
        errors.append(f"Missing static preflight runner: {display_path(STATIC_PREFLIGHT)}")
    elif "ValidateBatch34UvAtlasMaterialHandoff.py" not in STATIC_PREFLIGHT.read_text(encoding="utf-8-sig"):
        errors.append("static preflight runner must include ValidateBatch34UvAtlasMaterialHandoff.py")

    return specs


def validate_post_apply(specs: list[dict], errors: list[str], warnings: list[str]) -> None:
    for spec in specs:
        texture_path = project_path(spec["texture"])
        template_path = project_path(spec["template"])
        material_path = project_path(spec["material"])
        if not material_path.exists():
            errors.append(f"{spec['sourceId']}: missing handoff material asset: {spec['material']}")
            continue

        texture_guid = read_guid(texture_path)
        if not texture_guid:
            errors.append(f"{spec['sourceId']}: missing texture guid: {spec['texture']}")
            continue

        material_text = material_path.read_text(encoding="utf-8-sig")
        if texture_guid not in material_text:
            errors.append(f"{spec['sourceId']}: material does not reference texture guid {texture_guid}")
        if spec["alpha"] and "_ALPHATEST_ON" not in material_text:
            errors.append(f"{spec['sourceId']}: cutout material missing _ALPHATEST_ON keyword")
        template_shader_guid = read_shader_guid(template_path)
        if template_shader_guid and template_shader_guid not in material_text:
            errors.append(
                f"{spec['sourceId']}: material does not preserve template shader guid {template_shader_guid}"
            )


def read_shader_guid(material_path: Path) -> str:
    if not material_path.exists():
        return ""
    match = re.search(
        r"m_Shader:\s*\{fileID:\s*4800000,\s*guid:\s*([0-9a-fA-F]+),\s*type:\s*3\}",
        material_path.read_text(encoding="utf-8-sig"),
    )
    return match.group(1) if match else ""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--post-apply", action="store_true")
    args = parser.parse_args()

    errors: list[str] = []
    warnings: list[str] = []
    specs = validate_static(errors, warnings)
    if args.post_apply:
        validate_post_apply(specs, errors, warnings)

    print("BATCH34_UV_ATLAS_MATERIAL_HANDOFF_VALIDATOR")
    print(f"builder={display_path(BUILDER)}")
    print(f"specs={len(specs)}")
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
