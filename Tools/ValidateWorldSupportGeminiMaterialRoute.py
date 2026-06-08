#!/usr/bin/env python3
"""Validate generated Gemini PBR handoff for procedural world-support final materials."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
APPLIER = ROOT / "Assets/_Project/Scripts/Editor/WorldSupportGeminiMaterialApplier.cs"
DECAL_BUILDER = ROOT / "Assets/_Project/Scripts/Editor/WorldSupportGeneratedDecalMaterialBuilder.cs"
APPLY_ALL = ROOT / "Assets/_Project/Scripts/Editor/GeminiMaterialIntegrationApplier.cs"
AUTHORING = ROOT / "Assets/_Project/Scripts/Editor/WorldProceduralSupportFinalAuthoring.cs"
WORLD_SUPPORT_ROOT = ROOT / "Assets/_Project/Art/Materials/WorldSupport"
GENERATED_MATERIAL_ROOT = ROOT / "Assets/_Project/Art/Materials/Generated/ExternalPBR_20260607"
ALPHA_MANIFEST = (
    ROOT
    / "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/GeminiBatch34AlphaCandidates_Manifest.json"
)
GEMINI_SINGLE_MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/GeminiSingleMaterials_Manifest.json"
GEMINI_BIOME_MANIFEST = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiBiomeMaterialIntake_20260607/GeminiBiomeMaterials_Manifest.json"
GEMINI_ATLAS_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases"


EXPECTED_ASSIGNMENTS = [
    (
        "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_ResourcePocket.mat",
        "gemini_Batch20260608_TextureExpansion_b34_3404_abyssal_manganese_nodule_plain",
        "Gemini_Batch20260608_TextureExpansion",
    ),
    (
        "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_HazardPocket.mat",
        "gemini_biome_20260607_hydrothermal_vent_mineral_crust",
        "GeminiBiome_20260607",
    ),
    (
        "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_SafePocket.mat",
        "gemini_Batch20260608_TextureExpansion_b34_3401_photic_limestone_rubble_shelf",
        "Gemini_Batch20260608_TextureExpansion",
    ),
    (
        "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_CreaturePassive.mat",
        "gemini_biome_20260607_soft_jelly_membrane",
        "GeminiBiome_20260607",
    ),
    (
        "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_CreaturePredator.mat",
        "gemini_biome_20260607_abyssal_predator_hide",
        "GeminiBiome_20260607",
    ),
    (
        "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_AbyssApex.mat",
        "gemini_biome_20260607_abyssal_predator_hide",
        "GeminiBiome_20260607",
    ),
    (
        "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_ReefApex.mat",
        "gemini_biome_20260607_bioluminescent_coral_flesh",
        "GeminiBiome_20260607",
    ),
    (
        "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_RuinApex.mat",
        "gemini_Batch20260608_TextureExpansion_b34_3411_pressure_base_exterior_hull_trim_sheet",
        "Gemini_Batch20260608_TextureExpansion",
    ),
]

EXPECTED_DECAL_SPECS = [
    (
        "Assets/_Project/Art/Materials/WorldSupport/Generated/MAT_B34_WorldSupport_LeakRustBiofilmDecal.mat",
        "B34-3423",
        "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/damage_decal/TX_B34-3423_leak_rust_biofilm_decal_atlas_AlphaCandidate.png",
    ),
    (
        "Assets/_Project/Art/Materials/WorldSupport/Generated/MAT_B34_WorldSupport_SaltMineralDepositDecal.mat",
        "B34-3425",
        "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/damage_decal/TX_B34-3425_salt_mineral_deposit_decal_atlas_AlphaCandidate.png",
    ),
    (
        "Assets/_Project/Art/Materials/WorldSupport/Generated/MAT_B34_WorldSupport_InstrumentGlassSmudgeDecal.mat",
        "B34-3426",
        "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/glass_decal/TX_B34-3426_instrument_glass_smudge_alpha_decal_atlas_AlphaCandidate.png",
    ),
    (
        "Assets/_Project/Art/Materials/WorldSupport/Generated/MAT_B34_WorldSupport_PressureGlassCrackDecal.mat",
        "B34-3427",
        "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/glass_decal/TX_B34-3427_pressure_crack_glass_decal_atlas_AlphaCandidate.png",
    ),
    (
        "Assets/_Project/Art/Materials/WorldSupport/Generated/MAT_B34_WorldSupport_WarningStripeDecal.mat",
        "B34-3428",
        "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/damage_decal/TX_B34-3428_warning_paint_stripe_decal_atlas_AlphaCandidate.png",
    ),
    (
        "Assets/_Project/Art/Materials/WorldSupport/Generated/MAT_B34_WorldSupport_CutterScorchDecal.mat",
        "B34-3429",
        "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/damage_decal/TX_B34-3429_cutter_burn_scorch_decal_atlas_AlphaCandidate.png",
    ),
    (
        "Assets/_Project/Art/Materials/WorldSupport/Generated/MAT_B34_WorldSupport_BarnacleColonyDecal.mat",
        "B34-3430",
        "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/organic_decal/TX_B34-3430_barnacle_colony_decal_variants_AlphaCandidate.png",
    ),
    (
        "Assets/_Project/Art/Materials/WorldSupport/Generated/MAT_B34_WorldSupport_WetnessRivuletDecal.mat",
        "B34-3431",
        "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/damage_decal/TX_B34-3431_wetness_rivulet_decal_atlas_AlphaCandidate.png",
    ),
    (
        "Assets/_Project/Art/Materials/WorldSupport/Generated/MAT_B34_WorldSupport_ContaminationStainDecal.mat",
        "B34-3432",
        "Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/organic_decal/TX_B34-3432_contamination_biohazard_stain_atlas_AlphaCandidate.png",
    ),
]


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


def extract_decal_specs(builder_text: str) -> list[tuple[str, str, str]]:
    pattern = re.compile(
        r"new\s+DecalMaterialSpec\(\s*"
        r"(?P<materialPath>[A-Za-z0-9_]+MaterialPath),\s*"
        r'"(?P<sourceId>B34-\d+)",\s*'
        r'"(?P<sourceTexture>Assets/[^"]+\.png)"',
        re.MULTILINE,
    )
    constant_paths = {
        match.group("name"): match.group("suffix")
        for match in re.finditer(
            r'public\s+const\s+string\s+(?P<name>[A-Za-z0-9_]+MaterialPath)\s*=\s*OutputFolder\s*\+\s*"(?P<suffix>/[^"]+\.mat)"',
            builder_text,
        )
    }
    output_match = re.search(
        r'public\s+const\s+string\s+OutputFolder\s*=\s*"(?P<output>Assets/[^"]+)"',
        builder_text,
    )
    output_folder = output_match.group("output") if output_match else ""
    specs: list[tuple[str, str, str]] = []
    for match in pattern.finditer(builder_text):
        raw_material = match.group("materialPath")
        suffix = constant_paths.get(raw_material, "")
        material_path = output_folder + suffix if output_folder and suffix else raw_material
        specs.append((material_path, match.group("sourceId"), match.group("sourceTexture")))
    return specs


def alpha_candidate_entries() -> dict[str, dict]:
    if not ALPHA_MANIFEST.exists():
        return {}
    payload = load_json(ALPHA_MANIFEST)
    entries: dict[str, dict] = {}
    for entry in payload.get("entries", []) or []:
        entry_id = str(entry.get("id", "")).strip()
        if entry_id:
            entries[entry_id] = entry
    return entries


def validate_static(errors: list[str]) -> None:
    for path in (APPLIER, DECAL_BUILDER, APPLY_ALL, AUTHORING):
        if not path.exists():
            errors.append(f"missing required source: {display(path)}")
    if errors:
        return

    applier_text = APPLIER.read_text(encoding="utf-8-sig")
    decal_builder_text = DECAL_BUILDER.read_text(encoding="utf-8-sig")
    apply_all_text = APPLY_ALL.read_text(encoding="utf-8-sig")
    authoring_text = AUTHORING.read_text(encoding="utf-8-sig")

    if extract_assignments(applier_text) != EXPECTED_ASSIGNMENTS:
        errors.append("world-support Gemini assignment contract changed or reordered unexpectedly")

    if "ValidateSourceMaterials();" not in applier_text:
        errors.append("world-support applier must validate all generated source materials before writing target assets")
    if "Missing generated source materials" not in applier_text:
        errors.append("world-support applier must fail fast with missing source material count and first missing path")
    if "ApplyAssignment(assignment);" not in applier_text:
        errors.append("world-support applier must apply each assignment directly after source validation")
    if "Missing generated source material: " not in applier_text:
        errors.append("world-support applier must throw with generated source path for stale source handles")
    if "World-support material apply had failures=" in applier_text:
        errors.append("world-support applier must not downgrade apply failures to warnings")
    if "TryApply(" in applier_text or "out string failure" in applier_text or "report.Failures" in applier_text:
        errors.append("world-support applier must not use bool/out-string failure masking")
    if "Debug.LogWarning" in applier_text:
        errors.append("world-support applier must not downgrade apply failures to warnings")

    if "AreSourceMaterialsAvailable()" not in applier_text:
        errors.append("world-support applier must expose guarded source-availability check for support-final rebuild path")

    if extract_decal_specs(decal_builder_text) != EXPECTED_DECAL_SPECS:
        errors.append("world-support generated decal spec contract changed or reordered unexpectedly")
    if "AreSourceTexturesAvailable()" not in decal_builder_text:
        errors.append("world-support decal builder must expose guarded source-texture availability check")
    if "BuildSpec(spec);" not in decal_builder_text:
        errors.append("world-support decal builder must fail at the exact decal spec, not aggregate skipped failures")
    if "Missing generated decal source texture: " not in decal_builder_text:
        errors.append("world-support decal builder must throw with source id and path when decal source texture is missing")
    if "No supported decal shader found for " not in decal_builder_text:
        errors.append("world-support decal builder must throw with source id when no decal shader is available")
    if "TryBuild(" in decal_builder_text or "out string failure" in decal_builder_text:
        errors.append("world-support decal builder must not use bool/out-string failure masking")
    if "report.Failures" in decal_builder_text:
        errors.append("world-support decal builder must not aggregate failures after losing source id context")
    if "Debug.LogWarning" in decal_builder_text:
        errors.append("world-support decal builder must not downgrade build failures to warnings")
    if "Debug.LogWarning($\"[WorldSupportGeneratedDecalMaterialBuilder] Decal material build had failures=" in decal_builder_text:
        errors.append("world-support decal builder must not downgrade build failures to warnings")
    for token in (
        'material.DisableKeyword("_ALPHATEST_ON")',
        'material.DisableKeyword("_ALPHAPREMULTIPLY_ON")',
        'material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT")',
        "material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent",
        'SetFloatIfPresent(material, "_Surface", 1f)',
        'SetFloatIfPresent(material, "_Blend", 0f)',
        'SetFloatIfPresent(material, "_AlphaClip", 0f)',
        'SetFloatIfPresent(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha)',
        'SetFloatIfPresent(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha)',
        'SetFloatIfPresent(material, "_ZWrite", 0f)',
    ):
        if token not in decal_builder_text:
            errors.append(f"world-support decal builder must force transparent keyword/render state token: {token}")
    if "ScifiFacility/Prefabs/decals" in authoring_text:
        errors.append("WorldProceduralSupportFinalAuthoring must not depend on vendor ScifiFacility decal prefabs")
    if "PrefabUtility.InstantiatePrefab(decalPrefab" in authoring_text:
        errors.append("WorldProceduralSupportFinalAuthoring must build first-party quad decals, not instantiate vendor decal prefabs")
    if "WorldSupportGeneratedDecalMaterialBuilder.AreSourceTexturesAvailable()" not in authoring_text:
        errors.append("WorldProceduralSupportFinalAuthoring must guard generated decal material self-heal by source texture availability")
    if "WorldSupportGeneratedDecalMaterialBuilder.Build()" not in authoring_text:
        errors.append("WorldProceduralSupportFinalAuthoring must build generated support decal materials when source textures are available")
    if "WorldSupportGeneratedDecalMaterialBuilder.WarningStripeMaterialPath" not in authoring_text:
        errors.append("WorldProceduralSupportFinalAuthoring must bind generated warning stripe decal material")
    if "WorldSupportGeneratedDecalMaterialBuilder.CutterScorchMaterialPath" not in authoring_text:
        errors.append("WorldProceduralSupportFinalAuthoring must bind generated cutter/scorch decal material")
    if "EnsureQuadDecalChild" not in authoring_text:
        errors.append("WorldProceduralSupportFinalAuthoring must create deterministic first-party quad decal children")

    if "WorldSupportGeminiMaterialApplier.AreSourceMaterialsAvailable()" not in authoring_text:
        errors.append("WorldProceduralSupportFinalAuthoring must guard generated world-support material self-heal by source availability")
    if "WorldSupportGeminiMaterialApplier.Apply(false)" not in authoring_text:
        errors.append("WorldProceduralSupportFinalAuthoring must reapply Gemini support materials when generated sources are available")

    importer_index = apply_all_text.find("ExternalPbrTexturePackImporter.ImportExternalPbrTexturePacks()")
    atlas_importer_index = apply_all_text.find("Batch34SourceAtlasImporter.ImportBatch34SourceAtlases()")
    decal_builder_index = apply_all_text.find("WorldSupportGeneratedDecalMaterialBuilder.Build()")
    visor_index = apply_all_text.find("Batch34VisorTraumaDecalArrayIntegrator.BakeAndBindVisorTraumaArray()")
    resource_index = apply_all_text.find("ResourcePickupGeminiMaterialApplier.Apply(false)")
    support_index = apply_all_text.find("WorldSupportGeminiMaterialApplier.Apply(false)")
    held_index = apply_all_text.find("HeldToolExternalPbrMaterialApplier.ApplyExternalPbrToHeldTools(false)")
    if importer_index < 0 or atlas_importer_index < 0 or decal_builder_index < 0 or visor_index < 0:
        errors.append("Gemini apply-all is missing source import, generated support decal material build, or visor decal call")
    elif not (importer_index < atlas_importer_index < decal_builder_index < visor_index):
        errors.append("generated support decal material build must run after source atlas import and before downstream decal array binding")
    if importer_index < 0 or resource_index < 0 or support_index < 0 or held_index < 0:
        errors.append("Gemini apply-all is missing importer, resource-pickup, world-support, or held-tool material call")
    elif not (importer_index < resource_index < support_index < held_index):
        errors.append("world-support material applier must run after generated import/resource pickup and before downstream tool/world appliers")

    material_assets = iter_material_assets()
    seen_targets: set[str] = set()
    for target, material_id, provider in EXPECTED_ASSIGNMENTS:
        if target in seen_targets:
            errors.append(f"duplicate world-support target material assignment: {target}")
        seen_targets.add(target)

        if (provider, material_id) not in material_assets:
            errors.append(f"missing generated material source in manifests: {provider}/{material_id}")
            continue
        target_path = ROOT / target
        if not target_path.exists():
            errors.append(f"missing target world-support material asset: {target}")
        maps = material_assets[(provider, material_id)].get("maps", {}) or {}
        for map_key in ("BaseColor", "NormalGL", "MaskMap_UnityURP"):
            raw_path = str(maps.get(map_key, "")).strip()
            if not raw_path:
                errors.append(f"{provider}/{material_id}: missing source map key {map_key}")
                continue
            if not project_path(raw_path).exists():
                errors.append(f"{provider}/{material_id}: missing source map file {map_key}: {raw_path}")

    alpha_entries = alpha_candidate_entries()
    for material_path, source_id, source_texture in EXPECTED_DECAL_SPECS:
        entry = alpha_entries.get(source_id)
        if entry is None:
            errors.append(f"missing alpha candidate manifest entry: {source_id}")
        elif str(entry.get("alphaCandidate", "")).strip() != source_texture:
            errors.append(f"{source_id}: alpha candidate path mismatch")
        if not project_path(source_texture).exists():
            errors.append(f"{source_id}: generated alpha candidate texture missing: {source_texture}")
        if not material_path.startswith("Assets/_Project/Art/Materials/WorldSupport/Generated/"):
            errors.append(f"{source_id}: decal material must stay under first-party WorldSupport/Generated folder: {material_path}")


def validate_post_apply(errors: list[str]) -> None:
    material_assets = iter_material_assets()
    for target, material_id, provider in EXPECTED_ASSIGNMENTS:
        generated_source = GENERATED_MATERIAL_ROOT / provider / f"MAT_EXT_{provider}_{material_id}.mat"
        target_path = ROOT / target
        if not generated_source.exists():
            errors.append(f"post-apply missing generated source material: {display(generated_source)}")
        if not target_path.exists():
            errors.append(f"post-apply missing target world-support material: {target}")
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

    for material_path, source_id, source_texture in EXPECTED_DECAL_SPECS:
        target_path = ROOT / material_path
        if not target_path.exists():
            errors.append(f"post-apply missing generated world-support decal material: {material_path}")
            continue
        source_guid = read_guid(project_path(source_texture))
        if not source_guid:
            errors.append(f"post-apply missing generated decal texture guid for {source_id}")
            continue
        material_text = target_path.read_text(encoding="utf-8-sig")
        if source_guid not in material_text:
            errors.append(f"post-apply decal material {material_path} missing source texture guid from {source_id}")
        validate_decal_transparent_render_state(material_text, material_path, source_id, errors)


def validate_decal_transparent_render_state(
    material_text: str,
    material_path: str,
    source_id: str,
    errors: list[str],
) -> None:
    required_tokens = (
        "m_CustomRenderQueue: 3000",
        "RenderType: Transparent",
        "_SURFACE_TYPE_TRANSPARENT",
        "- _Surface: 1",
        "- _Blend: 0",
        "- _AlphaClip: 0",
        "- _SrcBlend: 5",
        "- _DstBlend: 10",
        "- _ZWrite: 0",
    )
    for token in required_tokens:
        if token not in material_text:
            errors.append(f"post-apply decal material {material_path} from {source_id} missing transparent token {token}")

    forbidden_tokens = (
        "- _ALPHATEST_ON",
        "- _ALPHAPREMULTIPLY_ON",
        "RenderType: TransparentCutout",
        "m_CustomRenderQueue: -1",
        "- _Surface: 0",
        "- _ZWrite: 1",
    )
    for token in forbidden_tokens:
        if token in material_text:
            errors.append(f"post-apply decal material {material_path} from {source_id} contains stale opaque/cutout token {token}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--post-apply", action="store_true")
    args = parser.parse_args()

    errors: list[str] = []
    validate_static(errors)
    if args.post_apply:
        validate_post_apply(errors)

    print("WORLD_SUPPORT_GEMINI_MATERIAL_ROUTE_VALIDATOR")
    print(f"applier={display(APPLIER)}")
    print(f"decalBuilder={display(DECAL_BUILDER)}")
    print(f"authoring={display(AUTHORING)}")
    print(f"assignments={len(EXPECTED_ASSIGNMENTS)}")
    print(f"decalSpecs={len(EXPECTED_DECAL_SPECS)}")
    print(f"postApply={args.post_apply}")
    print(f"errors={len(errors)}")
    for error in errors:
        print(f"ERROR {error}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
