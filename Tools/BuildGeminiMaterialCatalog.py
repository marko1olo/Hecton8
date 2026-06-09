#!/usr/bin/env python3
"""Build concise generated Gemini material/source memory for future agents."""

from __future__ import annotations

import ast
import json
import re
import time
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
GENERATED_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated"

CATALOG_PATH = GENERATED_ROOT / "GeminiMaterialCatalog_20260608.json"
CATALOG_LATEST_PATH = GENERATED_ROOT / "GeminiMaterialCatalog_Latest.json"
LEGACY_CATALOG_ALIAS_PATH = GENERATED_ROOT / "GeminiMaterialCatalog_20260607.json"
DOC_PATH = ROOT / "Docs/GeneratedAssets/GeminiMaterialCatalog_20260608.md"

GEMINI_SINGLE_MANIFEST = GENERATED_ROOT / "GeminiMaterialIntake_20260607/GeminiSingleMaterials_Manifest.json"
GEMINI_BIOME_MANIFEST = GENERATED_ROOT / "GeminiBiomeMaterialIntake_20260607/GeminiBiomeMaterials_Manifest.json"
GEMINI_ATLAS_ROOT = GENERATED_ROOT / "GeminiMaterialAtlases"
SOURCE_ATLAS_MANIFESTS = (
    GENERATED_ROOT / "GeminiBatch34SourceAtlases_20260608/GeminiBatch34SourceAtlases_Manifest.json",
)
ALPHA_CANDIDATE_MANIFESTS = (
    GENERATED_ROOT
    / "GeminiBatch34SourceAtlases_20260608/AlphaCandidates/GeminiBatch34AlphaCandidates_Manifest.json",
)
PADDED_ATLAS_MANIFESTS = (
    GENERATED_ROOT
    / "GeminiBatch34PaddedAtlasSources_20260608/GeminiBatch34PaddedAtlasSources_Manifest.json",
)
SPLIT_ATLAS_MANIFESTS = (
    GENERATED_ROOT
    / "GeminiBatch34SplitAtlasCandidates_20260608/GeminiBatch34SplitAtlasCandidates_Manifest.json",
)
BATCH34_INTAKE_MANIFEST = (
    ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/QA/Batch34_TextureExpansion_IntakeManifest.json"
)
BATCH34_CURATION_MANIFEST = (
    ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/QA/Batch34_TextureExpansion_CurationManifest.json"
)
BATCH34_REGEN_TARGETS_MANIFEST = (
    ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/RegenTargets/QA/Batch34_RegenTargets_IntakeManifest.json"
)

INTEGRATION_REPORTS = (
    GENERATED_ROOT / "GeminiBiomeFloraIntegration_20260607.json",
    GENERATED_ROOT / "GeneratedTextureOptimization_20260607.json",
    GENERATED_ROOT / "GeneratedFloraImportedNormalization_20260607.json",
    GENERATED_ROOT / "GeneratedSingleTextureNormalization_20260607.json",
    GENERATED_ROOT / "GeminiReplacementTextureIntegration_20260607.json",
    BATCH34_INTAKE_MANIFEST,
    BATCH34_CURATION_MANIFEST,
    BATCH34_REGEN_TARGETS_MANIFEST,
)

PREVIEW_SHEETS = (
    GENERATED_ROOT / "GeminiMaterialIntake_20260607/PREVIEW_GeminiSingles_LitMaterialSheet.png",
    GENERATED_ROOT / "GeminiBiomeMaterialIntake_20260607/PREVIEW_GeminiBiome_LitMaterialSheet.png",
)

WORLD_PROXY_APPLIER_PATH = ROOT / "Assets/_Project/Scripts/Editor/WorldProxyGeminiBiomeMaterialApplier.cs"
WORLD_PROXY_VALIDATOR_PATH = ROOT / "Tools/ValidateWorldProxyGeminiBiomeAssignments.py"
EXTERNAL_PBR_IMPORTER_PATH = ROOT / "Assets/_Project/Scripts/Editor/ExternalPbrTexturePackImporter.cs"
EXTERNAL_PBR_BINDING_VALIDATOR_PATH = ROOT / "Tools/ValidateExternalPbrImporterBindings.py"
EXTERNAL_PBR_MATERIAL_ASSET_VALIDATOR_PATH = ROOT / "Tools/ValidateExternalPbrMaterialAssets.py"
EXTERNAL_PBR_LIT_PREVIEW_BUILDER_PATH = ROOT / "Tools/BuildExternalPbrLitPreview.py"
EXTERNAL_PBR_LIT_PREVIEW_VALIDATOR_PATH = ROOT / "Tools/ValidateExternalPbrLitPreview.py"
GEMINI_STATE_VALIDATOR_PATH = ROOT / "Tools/ValidateGeminiGeneratedMaterialState.py"
GEMINI_STATIC_PREFLIGHT_RUNNER_PATH = ROOT / "Tools/RunGeminiMaterialStaticPreflight.ps1"
EXTERNAL_PBR_IMPORT_RUNNER_PATH = ROOT / "Tools/RunExternalPbrUnityImport.ps1"
WORLD_PROXY_RUNNER_PATH = ROOT / "Tools/RunWorldProxyGeminiBiomeApply.ps1"
HELD_TOOL_APPLIER_PATH = ROOT / "Assets/_Project/Scripts/Editor/HeldToolExternalPbrMaterialApplier.cs"
HELD_TOOL_VALIDATOR_PATH = ROOT / "Tools/ValidateHeldToolExternalPbrRules.py"
HELD_TOOL_RUNNER_PATH = ROOT / "Tools/RunHeldToolExternalPbrApply.ps1"
WORLD_TOOL_APPLIER_PATH = ROOT / "Assets/_Project/Scripts/Editor/WorldToolExternalPbrMaterialApplier.cs"
WORLD_TOOL_VALIDATOR_PATH = ROOT / "Tools/ValidateWorldToolExternalPbrRules.py"
WORLD_TOOL_RUNNER_PATH = ROOT / "Tools/RunWorldToolExternalPbrApply.ps1"
TOOL_SURFACE_DETAIL_INTEGRATOR_PATH = ROOT / "Assets/_Project/Scripts/Editor/ToolSurfaceDetailGeminiIntegrator.cs"
TOOL_SURFACE_DETAIL_VALIDATOR_PATH = ROOT / "Tools/ValidateToolSurfaceDetailGeminiRoute.py"
UV_ATLAS_MATERIAL_HANDOFF_BUILDER_PATH = ROOT / "Assets/_Project/Scripts/Editor/Batch34UvAtlasMaterialHandoffBuilder.cs"
UV_ATLAS_MATERIAL_HANDOFF_VALIDATOR_PATH = ROOT / "Tools/ValidateBatch34UvAtlasMaterialHandoff.py"
GEMINI_APPLY_ALL_PATH = ROOT / "Assets/_Project/Scripts/Editor/GeminiMaterialIntegrationApplier.cs"
GEMINI_APPLY_ALL_RUNNER_PATH = ROOT / "Tools/RunGeminiMaterialUnityApplyAll.ps1"
GEMINI_APPLY_ALL_RUNNER_VALIDATOR_PATH = ROOT / "Tools/ValidateGeminiUnityApplyRunner.py"
CONSTRUCTION_APPLIER_PATH = ROOT / "Assets/_Project/Scripts/Editor/ConstructionGeminiMaterialApplier.cs"
CONSTRUCTION_VALIDATOR_PATH = ROOT / "Tools/ValidateConstructionGeminiMaterialAssignments.py"
CONSTRUCTION_RUNNER_PATH = ROOT / "Tools/RunConstructionGeminiMaterialApply.ps1"
CONSTRUCTION_INSULATION_INTEGRATOR_PATH = ROOT / "Assets/_Project/Scripts/Editor/ConstructionInsulationBackingIntegrator.cs"
CONSTRUCTION_INSULATION_VALIDATOR_PATH = ROOT / "Tools/ValidateConstructionInsulationBackingRoute.py"
FLORA_IMPORTED_APPLIER_PATH = ROOT / "Tools/ApplyGeminiBiomeToFloraImported.py"
FLORA_IMPORTED_VALIDATOR_PATH = ROOT / "Tools/ValidateGeminiBiomeFloraImportedAssignments.py"
BATCH34_DIRECT_PROMPT_QUEUE_VALIDATOR_PATH = ROOT / "Tools/ValidateBatch34DirectPromptQueue.py"
BATCH34_TARGETED_PROMPT_QUEUE_VALIDATOR_PATH = ROOT / "Tools/ValidateBatch34TargetedPromptQueues.py"
BATCH34_REGEN_TARGETS_PROCESSOR_PATH = ROOT / "Tools/ProcessBatch34RegenTargets.py"
BATCH34_REGEN_TARGETS_VALIDATOR_PATH = ROOT / "Tools/ValidateBatch34RegenTargets.py"
SOURCE_ATLAS_IMPORTER_PATH = ROOT / "Assets/_Project/Scripts/Editor/Batch34SourceAtlasImporter.cs"
SOURCE_ATLAS_VALIDATOR_PATH = ROOT / "Tools/ValidateBatch34SourceAtlasPack.py"
SOURCE_ATLAS_IMPORTER_VALIDATOR_PATH = ROOT / "Tools/ValidateBatch34SourceAtlasImporter.py"
TERRAIN_LAYER_BUILDER_PATH = ROOT / "Assets/_Project/Scripts/Editor/Batch34TerrainLayerAssetBuilder.cs"
TERRAIN_LAYER_BUILDER_VALIDATOR_PATH = ROOT / "Tools/ValidateBatch34TerrainLayerBuilder.py"
PLAYER_SUIT_MATERIAL_APPLIER_PATH = ROOT / "Assets/_Project/Scripts/Editor/ProductFacePlayerSuitGeminiMaterialApplier.cs"
PLAYER_SUIT_MATERIAL_VALIDATOR_PATH = ROOT / "Tools/ValidateProductFacePlayerSuitGeminiMaterialRoute.py"
RESOURCE_PICKUP_MATERIAL_APPLIER_PATH = ROOT / "Assets/_Project/Scripts/Editor/ResourcePickupGeminiMaterialApplier.cs"
RESOURCE_PICKUP_MATERIAL_VALIDATOR_PATH = ROOT / "Tools/ValidateResourcePickupGeminiMaterialRoute.py"
WORLD_SUPPORT_MATERIAL_APPLIER_PATH = ROOT / "Assets/_Project/Scripts/Editor/WorldSupportGeminiMaterialApplier.cs"
WORLD_SUPPORT_MATERIAL_VALIDATOR_PATH = ROOT / "Tools/ValidateWorldSupportGeminiMaterialRoute.py"
WORLD_SUPPORT_DECAL_BUILDER_PATH = ROOT / "Assets/_Project/Scripts/Editor/WorldSupportGeneratedDecalMaterialBuilder.cs"
WORLD_SUPPORT_AUTHORING_PATH = ROOT / "Assets/_Project/Scripts/Editor/WorldProceduralSupportFinalAuthoring.cs"
ALPHA_CANDIDATE_PROMOTER_PATH = ROOT / "Tools/PromoteBatch34AlphaCandidatesToUnitySources.py"
ALPHA_CANDIDATE_VALIDATOR_PATH = ROOT / "Tools/ValidateBatch34AlphaCandidatePack.py"
ALPHA_CANDIDATE_EXTRACTOR_PATH = ROOT / "Tools/ExtractBatch34SourceAtlasAlphaCandidates.py"
ALPHA_CANDIDATE_OPTIMIZER_PATH = ROOT / "Tools/OptimizeBatch34AlphaPngSources.py"
PADDED_ATLAS_BUILDER_PATH = ROOT / "Tools/BuildBatch34PaddedNeedsWorkAtlases.py"
PADDED_ATLAS_VALIDATOR_PATH = ROOT / "Tools/ValidateBatch34PaddedAtlasSources.py"
SPLIT_ATLAS_BUILDER_PATH = ROOT / "Tools/SplitBatch34NeedsWorkAtlases.py"
SPLIT_ATLAS_VALIDATOR_PATH = ROOT / "Tools/ValidateBatch34SplitAtlasCandidates.py"
VISOR_TRAUMA_DECAL_ARRAY_INTEGRATOR_PATH = ROOT / "Assets/_Project/Scripts/Editor/Batch34VisorTraumaDecalArrayIntegrator.cs"
VISOR_TRAUMA_DECAL_ARRAY_VALIDATOR_PATH = ROOT / "Tools/ValidateBatch34VisorTraumaDecalArrayRoute.py"
VISOR_TRAUMA_PROFILE_CSV_VALIDATOR_PATH = ROOT / "Tools/ValidateBatch34VisorTraumaProfileCsv.py"


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def load_manifest(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_text_resilient(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    last_error: PermissionError | None = None
    for _ in range(10):
        try:
            path.write_text(text, encoding="utf-8")
            return
        except PermissionError as exc:
            last_error = exc
            time.sleep(0.25)
    if last_error is not None:
        raise last_error


def sanitize_provider_name(value: str) -> str:
    if not value.strip():
        return "Atlas"
    return "".join(c if c.isalnum() or c in "_-" else "_" for c in value)


def material_manifest_paths() -> list[Path]:
    paths = [path for path in (GEMINI_SINGLE_MANIFEST, GEMINI_BIOME_MANIFEST) if path.exists()]
    if GEMINI_ATLAS_ROOT.exists():
        paths.extend(sorted(GEMINI_ATLAS_ROOT.rglob("GeminiMaterialAtlas_Manifest.json")))
    return sorted(dict.fromkeys(paths), key=lambda p: display_path(p))


def provider_for_material_manifest(path: Path, payload: dict) -> str:
    if path == GEMINI_SINGLE_MANIFEST:
        return "GeminiSingles_20260607"
    if path == GEMINI_BIOME_MANIFEST:
        return "GeminiBiome_20260607"
    if path.name == "GeminiMaterialAtlas_Manifest.json":
        return "Gemini_" + sanitize_provider_name(path.parent.name)
    return str(payload.get("sourceProvider", "")).strip() or path.stem


def material_records(payload: dict, manifest_path: Path, provider: str) -> list[dict]:
    records: list[dict] = []
    seen_sources: set[str] = set()
    for asset in payload.get("assets", []) or []:
        asset_id = str(asset.get("id", "")).strip()
        if not asset_id:
            continue

        source = str(asset.get("source", "")).strip()
        if not source:
            source = str(payload.get("atlasSource", "")).strip()
        source_key = source or asset_id
        duplicate_source = source_key in seen_sources
        seen_sources.add(source_key)

        records.append(
            {
                "id": asset_id,
                "title": asset.get("title", ""),
                "provider": provider,
                "manifest": display_path(manifest_path),
                "source": source,
                "sourceBatchId": asset.get("sourceBatchId", ""),
                "sourceProvider": payload.get("sourceProvider", ""),
                "role": asset.get("role", ""),
                "integrationNote": asset.get("integrationNote", ""),
                "materialFamily": asset.get("materialFamily", ""),
                "surfaceClass": asset.get("surfaceClass", ""),
                "sourceType": asset.get("sourceType", ""),
                "sourceFamily": asset.get("sourceFamily", ""),
                "heldToolAllowed": bool(asset.get("heldToolAllowed", False)),
                "stationPropAllowed": bool(asset.get("stationPropAllowed", False)),
                "salvageAllowed": bool(asset.get("salvageAllowed", False)),
                "worldPanelAllowed": bool(asset.get("worldPanelAllowed", False)),
                "floraAllowed": bool(asset.get("floraAllowed", False)),
                "faunaAllowed": bool(asset.get("faunaAllowed", False)),
                "geologyAllowed": bool(asset.get("geologyAllowed", False)),
                "playerGearAllowed": bool(asset.get("playerGearAllowed", False)),
                "tilingScale": asset.get("tilingScale"),
                "metallic": asset.get("metallic"),
                "smoothness": asset.get("smoothness"),
                "seamScoreBefore": asset.get("seamScoreBefore"),
                "seamScoreAfter": asset.get("seamScoreAfter"),
                "seamRepaired": bool(asset.get("seamRepaired", False)),
                "watermarkRepaired": bool(asset.get("watermarkRepaired", False)),
                "watermarkRisk": bool(asset.get("watermarkRisk", False)),
                "provisionalPbrMaps": bool(asset.get("provisionalPbrMaps", False)),
                "duplicateSourceInManifest": duplicate_source,
            }
        )
    return records


def source_atlas_records() -> list[dict]:
    records: list[dict] = []
    for manifest_path in SOURCE_ATLAS_MANIFESTS:
        if not manifest_path.exists():
            continue
        payload = load_manifest(manifest_path)
        for entry in payload.get("entries", []) or []:
            records.append(
                {
                    "id": entry.get("id", ""),
                    "title": entry.get("title", ""),
                    "manifest": display_path(manifest_path),
                    "source": entry.get("source", ""),
                    "sourceType": entry.get("sourceType", ""),
                    "family": entry.get("family", ""),
                    "curationStatus": entry.get("curationStatus", ""),
                    "targetRole": entry.get("targetRole", ""),
                    "integrationNote": entry.get("integrationNote", ""),
                    "downstreamRoute": entry.get("downstreamRoute", ""),
                    "productionBindingStatus": entry.get("productionBindingStatus", ""),
                    "unityImportStatus": entry.get("unityImportStatus", ""),
                    "width": entry.get("width"),
                    "height": entry.get("height"),
                }
            )
    return records


def alpha_candidate_records() -> list[dict]:
    records: list[dict] = []
    for manifest_path in ALPHA_CANDIDATE_MANIFESTS:
        if not manifest_path.exists():
            continue
        payload = load_manifest(manifest_path)
        for entry in payload.get("entries", []) or []:
            stats = entry.get("alphaStats", {}) or {}
            records.append(
                {
                    "id": entry.get("id", ""),
                    "title": entry.get("title", ""),
                    "manifest": display_path(manifest_path),
                    "source": entry.get("source", ""),
                    "alphaCandidate": entry.get("alphaCandidate", ""),
                    "sourceType": entry.get("sourceType", ""),
                    "family": entry.get("family", ""),
                    "status": entry.get("status", ""),
                    "productionBindingStatus": entry.get("productionBindingStatus", ""),
                    "unityImportStatus": entry.get("unityImportStatus", ""),
                    "alphaNonZeroPct": stats.get("alphaNonZeroPct"),
                    "alphaOpaquePct": stats.get("alphaOpaquePct"),
                    "alphaMean": stats.get("alphaMean"),
                }
            )
    return records


def padded_atlas_records() -> list[dict]:
    records: list[dict] = []
    for manifest_path in PADDED_ATLAS_MANIFESTS:
        if not manifest_path.exists():
            continue
        payload = load_manifest(manifest_path)
        for entry in payload.get("entries", []) or []:
            records.append(
                {
                    "id": entry.get("id", ""),
                    "title": entry.get("title", ""),
                    "manifest": display_path(manifest_path),
                    "source": entry.get("source", ""),
                    "paddedAtlas": entry.get("paddedAtlas", ""),
                    "sourceType": entry.get("sourceType", ""),
                    "family": entry.get("family", ""),
                    "sourceCurationStatus": entry.get("sourceCurationStatus", ""),
                    "productionBindingStatus": entry.get("productionBindingStatus", ""),
                    "unityImportStatus": payload.get("unityImportStatus", ""),
                    "width": entry.get("width"),
                    "height": entry.get("height"),
                    "sourceWidth": entry.get("sourceWidth"),
                    "sourceHeight": entry.get("sourceHeight"),
                    "alphaNonZeroPct": entry.get("alphaNonZeroPct"),
                    "edgeAlphaNonZeroPct": entry.get("edgeAlphaNonZeroPct"),
                }
            )
    return records


def split_atlas_candidate_records() -> list[dict]:
    records: list[dict] = []
    for manifest_path in SPLIT_ATLAS_MANIFESTS:
        if not manifest_path.exists():
            continue
        payload = load_manifest(manifest_path)
        for entry in payload.get("entries", []) or []:
            islands = entry.get("islands", []) or []
            records.append(
                {
                    "id": entry.get("id", ""),
                    "title": entry.get("title", ""),
                    "manifest": display_path(manifest_path),
                    "source": entry.get("source", ""),
                    "sourceType": entry.get("sourceType", ""),
                    "family": entry.get("family", ""),
                    "sourceCurationStatus": entry.get("sourceCurationStatus", ""),
                    "productionBindingStatus": entry.get("productionBindingStatus", ""),
                    "unityImportStatus": payload.get("unityImportStatus", ""),
                    "islandCount": entry.get("islandCount", len(islands)),
                    "sampleIslands": [island.get("path", "") for island in islands[:8]],
                }
            )
    return records


def load_constants(text: str) -> dict[str, str]:
    return {
        match.group("name"): match.group("value")
        for match in re.finditer(
            r'private\s+const\s+string\s+(?P<name>[A-Za-z0-9_]+)\s*=\s*"(?P<value>[^"]+)";',
            text,
        )
    }


def resolve_provider(raw: str, constants: dict[str, str]) -> str:
    value = raw.strip()
    if value.startswith('"') and value.endswith('"'):
        return value.strip('"')
    return constants.get(value, value)


def add_consumer(
    consumers: dict[str, list[dict]],
    material_id: str,
    lane: str,
    target: str,
    provider: str = "",
    note: str = "",
) -> None:
    consumers.setdefault(material_id, []).append(
        {
            "lane": lane,
            "target": target,
            "provider": provider,
            "note": note,
        }
    )


def parse_prefab_renderer_consumers(consumers: dict[str, list[dict]], source_path: Path, lane: str) -> None:
    if not source_path.exists():
        return

    text = source_path.read_text(encoding="utf-8-sig")
    constants = load_constants(text)
    pattern = re.compile(
        r'new\("(?P<prefab>Assets/[^"]+\.prefab)",\s*"(?P<renderer>[^"]+)",\s*(?P<provider>[^,]+),\s*"(?P<material>[^"]+)"\s*(?:,|\))'
    )
    for match in pattern.finditer(text):
        provider = resolve_provider(match.group("provider"), constants)
        target = f"{match.group('prefab')}::{match.group('renderer')}"
        add_consumer(consumers, match.group("material"), lane, target, provider)


def parse_assignment_class_consumers(
    consumers: dict[str, list[dict]],
    source_path: Path,
    lane: str,
    provider_by_material: dict[str, str],
) -> None:
    if not source_path.exists():
        return

    text = source_path.read_text(encoding="utf-8-sig")
    pattern = re.compile(
        r'new\s+Assignment\(\s*"(?P<target>Assets/[^"]+\.mat)"\s*,\s*"(?P<material>[^"]+)"',
        re.MULTILINE,
    )
    for match in pattern.finditer(text):
        material_id = match.group("material")
        add_consumer(
            consumers,
            material_id,
            lane,
            match.group("target"),
            provider_by_material.get(material_id, ""),
        )


def parse_terrain_layer_consumers(
    consumers: dict[str, list[dict]],
    provider_by_material: dict[str, str],
) -> None:
    if not TERRAIN_LAYER_BUILDER_PATH.exists():
        return

    text = TERRAIN_LAYER_BUILDER_PATH.read_text(encoding="utf-8-sig")
    output_root_match = re.search(
        r'private\s+const\s+string\s+OutputRoot\s*=\s*"(?P<output>Assets/[^"]+)";',
        text,
    )
    output_root = output_root_match.group("output") if output_root_match else ""
    pattern = re.compile(
        r'new\s+TerrainLayerSpec\(\s*"(?P<material>[^"]+)"\s*,\s*"(?P<output>[^"]+\.terrainlayer)"',
        re.MULTILINE,
    )
    for match in pattern.finditer(text):
        material_id = match.group("material")
        output_name = match.group("output")
        target = f"{output_root}/{output_name}" if output_root else output_name
        add_consumer(
            consumers,
            material_id,
            "terrain_layer_builder",
            target,
            provider_by_material.get(material_id, ""),
        )


def parse_player_suit_material_consumers(
    consumers: dict[str, list[dict]],
    provider_by_material: dict[str, str],
) -> None:
    if not PLAYER_SUIT_MATERIAL_APPLIER_PATH.exists():
        return

    text = PLAYER_SUIT_MATERIAL_APPLIER_PATH.read_text(encoding="utf-8-sig")
    output_root_match = re.search(
        r'public\s+const\s+string\s+OutputFolder\s*=\s*"(?P<output>Assets/[^"]+)";',
        text,
    )
    output_root = output_root_match.group("output") if output_root_match else ""
    pattern = re.compile(
        r"new\s+PlayerSuitGeminiMaterialSpec\(\s*"
        r"(?P<slot>-?\d+),\s*"
        r'"(?P<slotName>[^"]+)",\s*'
        r'"(?P<output>[^"]+)",\s*'
        r'"(?P<provider>[^"]+)",\s*'
        r'"(?P<material>[^"]+)"',
        re.MULTILINE,
    )
    for match in pattern.finditer(text):
        material_id = match.group("material")
        output_name = match.group("output")
        slot = int(match.group("slot"))
        lane = "player_suit_material_palette" if slot >= 0 else "player_suit_aux_material_palette"
        target = f"{output_root}/{output_name}.mat" if output_root else output_name + ".mat"
        add_consumer(
            consumers,
            material_id,
            lane,
            target,
            provider_by_material.get(material_id, match.group("provider")),
            f"slot={slot}; role={match.group('slotName')}",
        )


def parse_flora_imported_consumers(
    consumers: dict[str, list[dict]],
    provider_by_material: dict[str, str],
) -> None:
    if not FLORA_IMPORTED_APPLIER_PATH.exists():
        return

    tree = ast.parse(FLORA_IMPORTED_APPLIER_PATH.read_text(encoding="utf-8-sig"))
    assignments: list[tuple[str, str]] = []
    for node in ast.walk(tree):
        if not isinstance(node, ast.Assign):
            continue
        names = [target.id for target in node.targets if isinstance(target, ast.Name)]
        if "ASSIGNMENTS" not in names:
            continue
        assignments = [(str(family_id), str(material_id)) for family_id, material_id in ast.literal_eval(node.value)]
        break

    for family_id, material_id in assignments:
        add_consumer(
            consumers,
            material_id,
            "imported_flora_texture_slots",
            f"Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/{family_id}",
            provider_by_material.get(material_id, ""),
        )


def add_source_consumer(
    consumers: dict[str, list[dict]],
    source_id: str,
    lane: str,
    target: str,
    note: str = "",
) -> None:
    consumers.setdefault(source_id, []).append(
        {
            "lane": lane,
            "target": target,
            "note": note,
        }
    )


def parse_world_support_decal_source_consumers(consumers: dict[str, list[dict]]) -> None:
    if not WORLD_SUPPORT_DECAL_BUILDER_PATH.exists():
        return

    text = WORLD_SUPPORT_DECAL_BUILDER_PATH.read_text(encoding="utf-8-sig")
    output_match = re.search(
        r'public\s+const\s+string\s+OutputFolder\s*=\s*"(?P<output>Assets/[^"]+)"',
        text,
    )
    output_root = output_match.group("output") if output_match else ""
    constant_paths = {
        match.group("name"): match.group("suffix")
        for match in re.finditer(
            r'public\s+const\s+string\s+(?P<name>[A-Za-z0-9_]+MaterialPath)\s*=\s*OutputFolder\s*\+\s*"(?P<suffix>/[^"]+\.mat)"',
            text,
        )
    }
    pattern = re.compile(
        r"new\s+DecalMaterialSpec\(\s*"
        r"(?P<materialPath>[A-Za-z0-9_]+MaterialPath),\s*"
        r'"(?P<sourceId>B34-\d+)",\s*'
        r'"(?P<sourceTexture>Assets/[^"]+\.png)"',
        re.MULTILINE,
    )
    source_by_constant: dict[str, str] = {}
    for match in pattern.finditer(text):
        constant_name = match.group("materialPath")
        source_by_constant[constant_name] = match.group("sourceId")
        suffix = constant_paths.get(constant_name, "")
        target = output_root + suffix if output_root and suffix else constant_name
        add_source_consumer(
            consumers,
            match.group("sourceId"),
            "world_support_generated_decal_material",
            target,
            match.group("sourceTexture"),
        )

    parse_world_support_authoring_decal_consumers(consumers, source_by_constant, constant_paths, output_root)


def parse_world_support_authoring_decal_consumers(
    consumers: dict[str, list[dict]],
    source_by_constant: dict[str, str],
    material_suffixes: dict[str, str],
    output_root: str,
) -> None:
    if not WORLD_SUPPORT_AUTHORING_PATH.exists():
        return

    text = WORLD_SUPPORT_AUTHORING_PATH.read_text(encoding="utf-8-sig")
    child_names = {
        match.group("name"): match.group("value")
        for match in re.finditer(
            r'private\s+const\s+string\s+(?P<name>[A-Za-z0-9_]+ChildName)\s*=\s*"(?P<value>[^"]+)"',
            text,
        )
    }
    material_constants_by_variable = {
        match.group("variable"): match.group("constant")
        for match in re.finditer(
            r"Material\s+(?P<variable>[A-Za-z0-9_]+)\s*=\s*AssetDatabase\.LoadAssetAtPath<Material>\("
            r"\s*"
            r"WorldSupportGeneratedDecalMaterialBuilder\.(?P<constant>[A-Za-z0-9_]+MaterialPath)\)",
            text,
        )
    }
    attach_pattern = re.compile(
        r"AttachSupportDecal\(\s*"
        r"lod0,\s*"
        r"(?P<variable>[A-Za-z0-9_]+),\s*"
        r"(?P<child>[A-Za-z0-9_]+ChildName)\s*,",
        re.MULTILINE,
    )
    for match in attach_pattern.finditer(text):
        material_constant = material_constants_by_variable.get(match.group("variable"), "")
        source_id = source_by_constant.get(material_constant, "")
        child_name = child_names.get(match.group("child"), match.group("child"))
        if not source_id:
            continue

        material_suffix = material_suffixes.get(material_constant, "")
        material_path = output_root + material_suffix if output_root and material_suffix else material_constant
        add_source_consumer(
            consumers,
            source_id,
            "world_support_generated_decal_prefab_child",
            f"Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Zone_RuinApex.prefab::{child_name}",
            f"material={material_path}",
        )


def parse_visor_trauma_source_consumers(consumers: dict[str, list[dict]]) -> None:
    if not VISOR_TRAUMA_DECAL_ARRAY_INTEGRATOR_PATH.exists():
        return

    text = VISOR_TRAUMA_DECAL_ARRAY_INTEGRATOR_PATH.read_text(encoding="utf-8-sig")
    output_match = re.search(
        r'private\s+const\s+string\s+OutputArrayPath\s*=\s*"(?P<output>Assets/[^"]+)"',
        text,
    )
    output = output_match.group("output") if output_match else "TX_B34_VisorTrauma_DecalArray.asset"
    pattern = re.compile(
        r'new\s+SliceBinding\(\s*(?P<slice>\d+),\s*"(?P<sourceId>B34-\d+)",\s*"(?P<label>[^"]+)"\s*\)',
        re.MULTILINE,
    )
    for match in pattern.finditer(text):
        add_source_consumer(
            consumers,
            match.group("sourceId"),
            "visor_trauma_decal_array_slice",
            output,
            f"slice={match.group('slice')}; {match.group('label')}",
        )


def parse_uv_atlas_material_handoff_source_consumers(consumers: dict[str, list[dict]]) -> None:
    if not UV_ATLAS_MATERIAL_HANDOFF_BUILDER_PATH.exists():
        return

    text = UV_ATLAS_MATERIAL_HANDOFF_BUILDER_PATH.read_text(encoding="utf-8-sig")
    pattern = re.compile(
        r"new\s+AtlasMaterialSpec\(\s*"
        r'"(?P<sourceId>B34-\d+)",\s*'
        r'"(?P<sourceTexture>Assets/[^"]+)",\s*'
        r'"(?P<template>Assets/[^"]+\.mat)",\s*'
        r'"(?P<target>Assets/[^"]+\.mat)"',
        re.MULTILINE,
    )
    for match in pattern.finditer(text):
        add_source_consumer(
            consumers,
            match.group("sourceId"),
            "batch34_uv_atlas_material_handoff",
            match.group("target"),
            f"texture={match.group('sourceTexture')}; template={match.group('template')}",
        )


def consumer_bindings(provider_by_material: dict[str, str]) -> list[dict]:
    consumers: dict[str, list[dict]] = {}
    parse_prefab_renderer_consumers(consumers, HELD_TOOL_APPLIER_PATH, "held_tool_prefab_renderer")
    parse_prefab_renderer_consumers(consumers, WORLD_TOOL_APPLIER_PATH, "world_tool_prefab_renderer")
    parse_prefab_renderer_consumers(consumers, TOOL_SURFACE_DETAIL_INTEGRATOR_PATH, "tool_surface_detail_primitives")
    parse_assignment_class_consumers(consumers, WORLD_PROXY_APPLIER_PATH, "world_proxy_material", provider_by_material)
    parse_assignment_class_consumers(consumers, CONSTRUCTION_APPLIER_PATH, "construction_material", provider_by_material)
    parse_assignment_class_consumers(
        consumers,
        CONSTRUCTION_INSULATION_INTEGRATOR_PATH,
        "construction_insulation_backing_material",
        provider_by_material,
    )
    parse_assignment_class_consumers(consumers, RESOURCE_PICKUP_MATERIAL_APPLIER_PATH, "resource_pickup_material", provider_by_material)
    parse_assignment_class_consumers(consumers, WORLD_SUPPORT_MATERIAL_APPLIER_PATH, "world_support_material", provider_by_material)
    parse_terrain_layer_consumers(consumers, provider_by_material)
    parse_player_suit_material_consumers(consumers, provider_by_material)
    parse_flora_imported_consumers(consumers, provider_by_material)
    return [
        {
            "materialId": material_id,
            "consumers": entries,
        }
        for material_id, entries in sorted(consumers.items())
    ]


def source_consumer_bindings() -> list[dict]:
    consumers: dict[str, list[dict]] = {}
    parse_world_support_decal_source_consumers(consumers)
    parse_visor_trauma_source_consumers(consumers)
    parse_uv_atlas_material_handoff_source_consumers(consumers)
    return [
        {
            "sourceId": source_id,
            "consumers": entries,
        }
        for source_id, entries in sorted(consumers.items())
    ]


def preview_paths(material_payloads: list[tuple[Path, dict]]) -> list[str]:
    paths: list[Path] = [path for path in PREVIEW_SHEETS if path.exists()]
    for _, payload in material_payloads:
        raw = str(payload.get("preview", "")).strip()
        if raw:
            resolved = project_path(raw)
            if resolved.exists():
                paths.append(resolved)
    for manifest_path in (
        SOURCE_ATLAS_MANIFESTS
        + ALPHA_CANDIDATE_MANIFESTS
        + PADDED_ATLAS_MANIFESTS
        + SPLIT_ATLAS_MANIFESTS
    ):
        if not manifest_path.exists():
            continue
        raw = str(load_manifest(manifest_path).get("preview", "")).strip()
        if raw:
            resolved = project_path(raw)
            if resolved.exists():
                paths.append(resolved)
    return [display_path(path) for path in sorted(dict.fromkeys(paths), key=lambda p: display_path(p))]


def existing_paths(paths: tuple[Path, ...]) -> list[str]:
    return [display_path(path) for path in paths if path.exists()]


def count_by(entries: list[dict], field_name: str) -> dict[str, int]:
    counts: dict[str, int] = {}
    for entry in entries:
        key = str(entry.get(field_name, "")).strip() or "<missing>"
        counts[key] = counts.get(key, 0) + 1
    return dict(sorted(counts.items()))


def batch34_texture_expansion_summary() -> dict:
    intake_payload = load_manifest(BATCH34_INTAKE_MANIFEST) if BATCH34_INTAKE_MANIFEST.exists() else {}
    curation_payload = load_manifest(BATCH34_CURATION_MANIFEST) if BATCH34_CURATION_MANIFEST.exists() else {}
    entries = list(curation_payload.get("entries", []) or intake_payload.get("entries", []) or [])
    source_audit = intake_payload.get("sourceAudit", {}) or {}

    issue_count = 0
    warning_count = 0
    entries_with_issues = 0
    entries_with_warnings = 0
    watermark_detected = 0
    watermark_repaired = 0
    seam_refined = 0
    for entry in entries:
        issues = entry.get("issues", []) or []
        warnings = entry.get("warnings", []) or []
        issue_count += len(issues)
        warning_count += len(warnings)
        if issues:
            entries_with_issues += 1
        if warnings:
            entries_with_warnings += 1
        if (entry.get("watermarkDetection") or {}).get("detected"):
            watermark_detected += 1
        if entry.get("watermarkRepaired"):
            watermark_repaired += 1
        if entry.get("seamRefined"):
            seam_refined += 1

    return {
        "intakeManifest": display_path(BATCH34_INTAKE_MANIFEST) if BATCH34_INTAKE_MANIFEST.exists() else "",
        "curationManifest": display_path(BATCH34_CURATION_MANIFEST) if BATCH34_CURATION_MANIFEST.exists() else "",
        "expectedJobCount": int(source_audit.get("expectedJobCount", 0) or 0),
        "downloadCandidateCount": int(source_audit.get("downloadCandidateCount", 0) or 0),
        "selectedDownloadSourceCount": int(source_audit.get("selectedDownloadSourceCount", 0) or 0),
        "uniqueSelectedDownloadSourceCount": int(source_audit.get("uniqueSelectedDownloadSourceCount", 0) or 0),
        "ignoredDownloadCandidateCount": len(source_audit.get("ignoredDownloadCandidates", []) or []),
        "entryCount": len(entries),
        "curationStatusCounts": count_by(entries, "curationStatus"),
        "verdictCounts": count_by(entries, "verdict"),
        "sourceTypeCounts": count_by(entries, "sourceType"),
        "visualStatusCounts": count_by(entries, "visualStatus"),
        "unityImportStatusCounts": count_by(entries, "unityImportStatus"),
        "entriesWithIssues": entries_with_issues,
        "issueCount": issue_count,
        "entriesWithWarnings": entries_with_warnings,
        "warningCount": warning_count,
        "watermarkDetectedCount": watermark_detected,
        "watermarkRepairedCount": watermark_repaired,
        "seamRefinedCount": seam_refined,
    }


def build() -> int:
    material_payloads = [(path, load_manifest(path)) for path in material_manifest_paths()]
    material_entries: list[dict] = []
    manifest_summaries: list[dict] = []
    provider_by_material: dict[str, str] = {}

    for manifest_path, payload in material_payloads:
        provider = provider_for_material_manifest(manifest_path, payload)
        assets = payload.get("assets", []) or []
        manifest_summaries.append(
            {
                "path": display_path(manifest_path),
                "schema": payload.get("schema", ""),
                "sourceProvider": payload.get("sourceProvider", ""),
                "provider": provider,
                "assetCount": len(assets),
                "unityImportStatus": payload.get("unityImportStatus", ""),
                "reviewStatus": payload.get("reviewStatus", ""),
            }
        )
        for record in material_records(payload, manifest_path, provider):
            material_entries.append(record)
            provider_by_material.setdefault(str(record["id"]), provider)

    source_entries = source_atlas_records()
    alpha_entries = alpha_candidate_records()
    padded_entries = padded_atlas_records()
    split_entries = split_atlas_candidate_records()
    split_island_count = sum(int(entry.get("islandCount", 0) or 0) for entry in split_entries)
    consumers = consumer_bindings(provider_by_material)
    consumer_count = sum(len(entry["consumers"]) for entry in consumers)
    source_consumers = source_consumer_bindings()
    source_consumer_count = sum(len(entry["consumers"]) for entry in source_consumers)
    alpha_status_counts: dict[str, int] = {}
    for entry in alpha_entries:
        status = str(entry.get("status", ""))
        alpha_status_counts[status] = alpha_status_counts.get(status, 0) + 1
    batch34_summary = batch34_texture_expansion_summary()

    catalog = {
        "schema": "hecton8.generated_material_catalog.v2",
        "catalogDate": "2026-06-08",
        "catalogPath": display_path(CATALOG_PATH),
        "aliases": [display_path(CATALOG_LATEST_PATH), display_path(LEGACY_CATALOG_ALIAS_PATH)],
        "status": "STATIC_VALIDATED_PENDING_UNITY_IMPORT",
        "first20RouteMoment": "surface/cave/first-tool/world-proxy readability material library",
        "materialManifestCount": len(material_payloads),
        "materialCount": len(material_entries),
        "sourceAtlasCount": len(source_entries),
        "alphaCandidateCount": len(alpha_entries),
        "paddedAtlasCount": len(padded_entries),
        "splitAtlasEntryCount": len(split_entries),
        "splitIslandCount": split_island_count,
        "alphaStatusCounts": alpha_status_counts,
        "batch34TextureExpansionSummary": batch34_summary,
        "consumerBindingCount": consumer_count,
        "sourceConsumerBindingCount": source_consumer_count,
        "notes": [
            "Catalog is a live routing index for generated Gemini materials, material atlases, source atlases, alpha candidates, and current consumers.",
            "Unity import/material assignment remains pending until the Unity apply-all runner creates .meta/material assets and post-apply validators pass.",
            "Batch34 material tiles/trim sheets are importer-discoverable through GeminiMaterialAtlases/*/GeminiMaterialAtlas_Manifest.json.",
            "Batch34 decal/UV/pickup atlases are source-only; do not auto-create Lit materials from them.",
            "Alpha candidates are review/input assets. High-coverage candidates stay rejected until manually split or regenerated.",
            "Batch34 alpha/padded PNG source optimization is lossless only; lossy/quantized alpha changes require visual proof before replacement.",
            "Padded atlases are whole-atlas transparent handoff sources for UV/decal binding; prefer them over raw split islands when island extraction is visually noisy.",
            "Split atlas candidates are support/raw candidate islands only. They require manual review or downstream mesh/UV selection before production binding.",
            "World proxy assignments now resolve provider from material id, so Batch34 consumers are visible in this catalog.",
            "Batch34 terrain seamless tiles have an editor TerrainLayer handoff route for MapMagic/terrain owners; terrain graph mutation is still Unity-owner work.",
            "Player suit material palette is an editor ProductFace handoff from Gemini PBR source materials into generated suit slot materials; prefab binding remains Unity-owner work.",
            "Resource pickup materials are updated through existing Mat_Resource_* assets so current pickup prefabs keep stable material GUIDs while gaining generated PBR maps.",
            "World-support final materials are updated through existing Mat_Support_* assets and self-healed by WorldProceduralSupportFinalAuthoring when generated sources are available.",
            "World-support ruin decals are first-party generated quad decals sourced from Batch34 alpha candidates, replacing vendor ScifiFacility decal prefab dependency in that route; source consumer bindings include both generated decal materials and authored prefab children.",
            "Tool surface detail primitives bind old Gemini single/micro-panel materials as small no-collider tool accents instead of repainting whole tools.",
            "Batch34 UV/pickup atlas source handoff creates explicit Unity material assets for flora/fauna/pickup mesh owners without mutating current prefabs.",
            "Construction damp-insulation backing adds physical no-collider backing panels to damage/ruin prefabs and binds B34-3421.",
            "Gemini material Unity apply is a single gated batch through GeminiMaterialIntegrationApplier.ApplyAll; do not re-add per-stage Unity batch launches.",
            "Legacy generated-material runner names are compatibility wrappers that delegate to RunGeminiMaterialUnityApplyAll.ps1.",
            "Batch34 direct service prompt files are checked as two exact 25-job queues with non-empty negative prompts before source intake work.",
            "Batch34 targeted fix and regen prompt files are checked together so newer regen prompts explicitly supersede overlapping older fix prompts.",
            "Batch34 targeted regen outputs are processed into a manifest with explicit selected/rejected variants before any old source is replaced.",
        ],
        "materialManifests": manifest_summaries,
        "sourceAtlasManifests": existing_paths(SOURCE_ATLAS_MANIFESTS),
        "alphaCandidateManifests": existing_paths(ALPHA_CANDIDATE_MANIFESTS),
        "paddedAtlasManifests": existing_paths(PADDED_ATLAS_MANIFESTS),
        "splitAtlasManifests": existing_paths(SPLIT_ATLAS_MANIFESTS),
        "integrationReports": existing_paths(INTEGRATION_REPORTS),
        "previewSheets": preview_paths(material_payloads),
        "integrationTools": existing_paths(
            (
                EXTERNAL_PBR_IMPORTER_PATH,
                EXTERNAL_PBR_BINDING_VALIDATOR_PATH,
                EXTERNAL_PBR_MATERIAL_ASSET_VALIDATOR_PATH,
                EXTERNAL_PBR_LIT_PREVIEW_BUILDER_PATH,
                EXTERNAL_PBR_LIT_PREVIEW_VALIDATOR_PATH,
                GEMINI_STATE_VALIDATOR_PATH,
                GEMINI_STATIC_PREFLIGHT_RUNNER_PATH,
                EXTERNAL_PBR_IMPORT_RUNNER_PATH,
                WORLD_PROXY_RUNNER_PATH,
                WORLD_PROXY_APPLIER_PATH,
                WORLD_PROXY_VALIDATOR_PATH,
                HELD_TOOL_APPLIER_PATH,
                HELD_TOOL_VALIDATOR_PATH,
                HELD_TOOL_RUNNER_PATH,
                WORLD_TOOL_APPLIER_PATH,
                WORLD_TOOL_VALIDATOR_PATH,
                WORLD_TOOL_RUNNER_PATH,
                TOOL_SURFACE_DETAIL_INTEGRATOR_PATH,
                TOOL_SURFACE_DETAIL_VALIDATOR_PATH,
                UV_ATLAS_MATERIAL_HANDOFF_BUILDER_PATH,
                UV_ATLAS_MATERIAL_HANDOFF_VALIDATOR_PATH,
                GEMINI_APPLY_ALL_PATH,
                GEMINI_APPLY_ALL_RUNNER_PATH,
                GEMINI_APPLY_ALL_RUNNER_VALIDATOR_PATH,
                CONSTRUCTION_APPLIER_PATH,
                CONSTRUCTION_VALIDATOR_PATH,
                CONSTRUCTION_RUNNER_PATH,
                CONSTRUCTION_INSULATION_INTEGRATOR_PATH,
                CONSTRUCTION_INSULATION_VALIDATOR_PATH,
                FLORA_IMPORTED_APPLIER_PATH,
                FLORA_IMPORTED_VALIDATOR_PATH,
                BATCH34_DIRECT_PROMPT_QUEUE_VALIDATOR_PATH,
                BATCH34_TARGETED_PROMPT_QUEUE_VALIDATOR_PATH,
                BATCH34_REGEN_TARGETS_PROCESSOR_PATH,
                BATCH34_REGEN_TARGETS_VALIDATOR_PATH,
            SOURCE_ATLAS_IMPORTER_PATH,
            SOURCE_ATLAS_VALIDATOR_PATH,
            SOURCE_ATLAS_IMPORTER_VALIDATOR_PATH,
            TERRAIN_LAYER_BUILDER_PATH,
            TERRAIN_LAYER_BUILDER_VALIDATOR_PATH,
            PLAYER_SUIT_MATERIAL_APPLIER_PATH,
            PLAYER_SUIT_MATERIAL_VALIDATOR_PATH,
            RESOURCE_PICKUP_MATERIAL_APPLIER_PATH,
            RESOURCE_PICKUP_MATERIAL_VALIDATOR_PATH,
            WORLD_SUPPORT_MATERIAL_APPLIER_PATH,
            WORLD_SUPPORT_MATERIAL_VALIDATOR_PATH,
            WORLD_SUPPORT_DECAL_BUILDER_PATH,
            WORLD_SUPPORT_AUTHORING_PATH,
            ALPHA_CANDIDATE_PROMOTER_PATH,
                ALPHA_CANDIDATE_VALIDATOR_PATH,
                ALPHA_CANDIDATE_EXTRACTOR_PATH,
                ALPHA_CANDIDATE_OPTIMIZER_PATH,
                PADDED_ATLAS_BUILDER_PATH,
                PADDED_ATLAS_VALIDATOR_PATH,
                SPLIT_ATLAS_BUILDER_PATH,
                SPLIT_ATLAS_VALIDATOR_PATH,
                VISOR_TRAUMA_DECAL_ARRAY_INTEGRATOR_PATH,
                VISOR_TRAUMA_DECAL_ARRAY_VALIDATOR_PATH,
                VISOR_TRAUMA_PROFILE_CSV_VALIDATOR_PATH,
            )
        ),
        "consumerBindings": consumers,
        "sourceConsumerBindings": source_consumers,
        "materials": material_entries,
        "sourceAtlases": source_entries,
        "alphaCandidates": alpha_entries,
        "paddedAtlases": padded_entries,
        "splitAtlasCandidates": split_entries,
    }

    catalog_text = json.dumps(catalog, indent=2) + "\n"
    write_text_resilient(CATALOG_PATH, catalog_text)
    write_text_resilient(CATALOG_LATEST_PATH, catalog_text)
    write_text_resilient(LEGACY_CATALOG_ALIAS_PATH, catalog_text)

    lines = [
        "# Gemini Material Catalog 2026-06-08",
        "",
        "Generated-material memory for future agents. Static validated; Unity import remains pending until Editor proof.",
        "",
        f"- catalog: `{display_path(CATALOG_PATH)}`",
        f"- latest alias: `{display_path(CATALOG_LATEST_PATH)}`",
        f"- material manifests: {len(material_payloads)}",
        f"- material assets: {len(material_entries)}",
        f"- source atlases: {len(source_entries)}",
        f"- alpha candidates: {len(alpha_entries)}",
        f"- padded atlas sources: {len(padded_entries)}",
        f"- split atlas candidate sources: {len(split_entries)} entries / {split_island_count} islands",
        f"- consumer bindings: {consumer_count}",
        f"- source consumer bindings: {source_consumer_count}",
        f"- Batch34 selected Gemini sources: {batch34_summary.get('selectedDownloadSourceCount', 0)} / {batch34_summary.get('expectedJobCount', 0)}",
        "",
        "## Material Manifests",
        "",
    ]
    for summary in manifest_summaries:
        lines.append(
            f"- `{summary['path']}` - provider `{summary['provider']}`, assets={summary['assetCount']}, "
            f"unity={summary['unityImportStatus'] or 'n/a'}"
        )

    if batch34_summary:
        lines.extend(["", "## Batch34 Intake And Curation", ""])
        lines.append(f"- entries: {batch34_summary.get('entryCount', 0)}")
        lines.append(
            f"- selected download sources: {batch34_summary.get('selectedDownloadSourceCount', 0)} "
            f"/ {batch34_summary.get('expectedJobCount', 0)}"
        )
        lines.append(f"- ignored duplicate download candidates: {batch34_summary.get('ignoredDownloadCandidateCount', 0)}")
        lines.append(
            f"- warnings: {batch34_summary.get('entriesWithWarnings', 0)} entries / "
            f"{batch34_summary.get('warningCount', 0)} warnings"
        )
        lines.append(
            f"- issues: {batch34_summary.get('entriesWithIssues', 0)} entries / "
            f"{batch34_summary.get('issueCount', 0)} issues"
        )
        lines.append(
            f"- watermark detected/repaired: {batch34_summary.get('watermarkDetectedCount', 0)} "
            f"/ {batch34_summary.get('watermarkRepairedCount', 0)}"
        )
        for title, field_name in (
            ("curation", "curationStatusCounts"),
            ("verdict", "verdictCounts"),
            ("source type", "sourceTypeCounts"),
        ):
            counts = batch34_summary.get(field_name, {}) or {}
            formatted = ", ".join(f"{key}={value}" for key, value in sorted(counts.items()))
            lines.append(f"- {title}: {formatted}")

    if source_entries:
        lines.extend(["", "## Source Atlases", ""])
        by_family: dict[str, int] = {}
        for entry in source_entries:
            family = str(entry.get("family", "unknown")) or "unknown"
            by_family[family] = by_family.get(family, 0) + 1
        for family, count in sorted(by_family.items()):
            lines.append(f"- `{family}`: {count}")

    if alpha_entries:
        lines.extend(["", "## Alpha Candidate Status", ""])
        for status, count in sorted(alpha_status_counts.items()):
            lines.append(f"- `{status}`: {count}")

    if padded_entries:
        lines.extend(["", "## Padded Atlas Sources", ""])
        by_family: dict[str, int] = {}
        for entry in padded_entries:
            family = str(entry.get("family", "unknown")) or "unknown"
            by_family[family] = by_family.get(family, 0) + 1
        for family, count in sorted(by_family.items()):
            lines.append(f"- `{family}`: {count}")

    if split_entries:
        lines.extend(["", "## Split Atlas Candidates", ""])
        lines.append(f"- raw candidate atlases: {len(split_entries)}")
        lines.append(f"- raw candidate islands: {split_island_count}")
        lines.append("- use only after manual/UV review; padded atlases are the safer whole-atlas handoff.")

    if consumers:
        lines.extend(["", "## Consumer Bindings", ""])
        by_lane: dict[str, int] = {}
        for entry in consumers:
            for consumer in entry["consumers"]:
                by_lane[consumer["lane"]] = by_lane.get(consumer["lane"], 0) + 1
        for lane, count in sorted(by_lane.items()):
            lines.append(f"- `{lane}`: {count}")

    if source_consumers:
        lines.extend(["", "## Source Consumer Bindings", ""])
        by_lane: dict[str, int] = {}
        for entry in source_consumers:
            for consumer in entry["consumers"]:
                by_lane[consumer["lane"]] = by_lane.get(consumer["lane"], 0) + 1
        for lane, count in sorted(by_lane.items()):
            lines.append(f"- `{lane}`: {count}")

    lines.extend(["", "## Integration Tools", ""])
    for path in catalog["integrationTools"]:
        lines.append(f"- `{path}`")

    lines.extend(["", "## Material Sources", ""])
    for entry in material_entries:
        status = "watermarkRisk" if entry.get("watermarkRisk") else "ok"
        seam = ""
        if entry.get("seamScoreBefore") is not None:
            seam = f", seam {entry.get('seamScoreBefore')}->{entry.get('seamScoreAfter')}"
        consumer_total = sum(1 for item in consumers if item["materialId"] == entry["id"])
        bound = "bound" if consumer_total else "unbound"
        lines.append(
            f"- `{entry['title'] or entry['id']}` - `{entry.get('provider', '')}`, "
            f"{entry.get('surfaceClass', '')}, {status}, {bound}{seam}"
        )

    lines.extend(["", "## Source-Only Atlases", ""])
    for entry in source_entries:
        lines.append(
            f"- `{entry['id']}` {entry['title']} - `{entry.get('family', '')}`, "
            f"{entry.get('productionBindingStatus', '')}"
        )

    lines.extend(["", "## Padded Atlas Handoff", ""])
    for entry in padded_entries:
        lines.append(
            f"- `{entry['id']}` {entry['title']} - `{entry.get('family', '')}`, "
            f"{entry.get('productionBindingStatus', '')}, edge alpha {entry.get('edgeAlphaNonZeroPct')}"
        )

    lines.extend(["", "## Split Atlas Raw Candidates", ""])
    for entry in split_entries:
        lines.append(
            f"- `{entry['id']}` {entry['title']} - `{entry.get('family', '')}`, "
            f"{entry.get('islandCount', 0)} islands, review before binding"
        )

    lines.append("")
    write_text_resilient(DOC_PATH, "\n".join(lines))

    print("GEMINI_MATERIAL_CATALOG_STATUS: PASS")
    print(f"catalog={display_path(CATALOG_PATH)}")
    print(f"latest={display_path(CATALOG_LATEST_PATH)}")
    print(f"doc={display_path(DOC_PATH)}")
    print(f"materialManifests={len(material_payloads)}")
    print(f"materials={len(material_entries)}")
    print(f"sourceAtlases={len(source_entries)}")
    print(f"alphaCandidates={len(alpha_entries)}")
    print(f"paddedAtlases={len(padded_entries)}")
    print(f"splitAtlasEntries={len(split_entries)}")
    print(f"splitIslands={split_island_count}")
    print(f"consumerBindings={consumer_count}")
    print(f"sourceConsumerBindings={source_consumer_count}")
    print(f"batch34Entries={batch34_summary.get('entryCount', 0)}")
    print(
        "batch34SelectedSources="
        f"{batch34_summary.get('selectedDownloadSourceCount', 0)}/{batch34_summary.get('expectedJobCount', 0)}"
    )
    print(f"batch34CurationStatusCounts={json.dumps(batch34_summary.get('curationStatusCounts', {}), sort_keys=True)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(build())
