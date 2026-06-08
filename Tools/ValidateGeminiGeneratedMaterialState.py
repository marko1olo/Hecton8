#!/usr/bin/env python3
"""Validate generated Gemini material/source state before Unity import/apply."""

from __future__ import annotations

import json
import time
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
GENERATED_ROOT = ROOT / "Assets/_Project/Art/TEXTURES/Generated"
CATALOG = GENERATED_ROOT / "GeminiMaterialCatalog_Latest.json"
LEGACY_CATALOG = GENERATED_ROOT / "GeminiMaterialCatalog_20260607.json"
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
REQUIRED_MAPS = ("BaseColor", "NormalGL", "ARM_AO_Rough_Metal", "Height", "MaskMap_UnityURP")
PANELIZED_HELD_SURFACES = {"safety_composite_panel"}
MAX_BASECOLOR_BYTES = int(1.6 * 1024 * 1024)
MAX_SOURCE_ATLAS_BYTES = int(1.6 * 1024 * 1024)
MAX_PADDED_ATLAS_BYTES = int(16.0 * 1024 * 1024)
EXPECTED_PADDED_SIZE = 1536
MIN_SPLIT_ISLAND_SIZE = 256
MAX_SPLIT_ISLAND_SIZE = 1024
MAX_PADDED_EDGE_ALPHA_PCT = 0.01
MAX_SPLIT_EDGE_ALPHA_PCT = 2.0
SOURCE_TYPE_REQUIRED_CONSUMER_LANES = {
    "DECAL_ATLAS": {"world_support_generated_decal_material"},
    "UV_ATLAS": {"batch34_uv_atlas_material_handoff"},
    "PICKUP_ATLAS": {"batch34_uv_atlas_material_handoff"},
}
VISOR_TRAUMA_LANE = "visor_trauma_decal_array_slice"


def display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def project_path(raw: str) -> Path:
    path = Path(raw)
    return path if path.is_absolute() else ROOT / path


def load_json(path: Path) -> dict:
    last_error: json.JSONDecodeError | None = None
    for _ in range(10):
        try:
            return json.loads(path.read_text(encoding="utf-8-sig"))
        except json.JSONDecodeError as exc:
            last_error = exc
            time.sleep(0.1)
    if last_error is not None:
        raise last_error
    return {}


def manifest_float(value: object, default: float = -1.0) -> float:
    if value is None:
        return default
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


def material_manifest_paths() -> list[Path]:
    manifests = [path for path in (GEMINI_SINGLE_MANIFEST, GEMINI_BIOME_MANIFEST) if path.exists()]
    if GEMINI_ATLAS_ROOT.exists():
        manifests.extend(sorted(GEMINI_ATLAS_ROOT.rglob("GeminiMaterialAtlas_Manifest.json")))
    return sorted(dict.fromkeys(manifests), key=lambda p: display_path(p))


def validate_material_manifests(errors: list[str], warnings: list[str]) -> list[tuple[Path, dict]]:
    assets: list[tuple[Path, dict]] = []
    for manifest_path in material_manifest_paths():
        payload = load_json(manifest_path)
        for asset in payload.get("assets", []) or []:
            assets.append((manifest_path, asset))

    for manifest_path, asset in assets:
        asset_id = str(asset.get("id", "")).strip() or "<missing-id>"
        maps = asset.get("maps", {}) or {}

        if bool(asset.get("watermarkRisk", False)):
            errors.append(f"{asset_id}: watermarkRisk=true in {display_path(manifest_path)}")

        for map_key in REQUIRED_MAPS:
            raw_path = str(maps.get(map_key, "")).strip()
            if not raw_path:
                errors.append(f"{asset_id}: missing map key {map_key}")
                continue
            resolved_map = project_path(raw_path)
            if not resolved_map.exists():
                errors.append(f"{asset_id}: missing map file {map_key}: {raw_path}")
                continue
            if map_key == "BaseColor" and resolved_map.stat().st_size > MAX_BASECOLOR_BYTES:
                mb = resolved_map.stat().st_size / (1024 * 1024)
                warnings.append(f"{asset_id}: BaseColor source is {mb:.2f} MB; target is roughly 0.5-1.5 MB")

        tiling_scale = asset.get("tilingScale")
        if not isinstance(tiling_scale, (int, float)):
            errors.append(f"{asset_id}: tilingScale must be numeric")
            continue
        if float(tiling_scale) < 0.25 or float(tiling_scale) > 16.0:
            errors.append(f"{asset_id}: tilingScale out of validator range: {tiling_scale}")

        seam_after = asset.get("seamScoreAfter")
        if isinstance(seam_after, (int, float)) and seam_after > 1.6:
            surface_class = str(asset.get("surfaceClass", ""))
            held_tool = bool(asset.get("heldToolAllowed", False))
            if held_tool and surface_class in PANELIZED_HELD_SURFACES and float(tiling_scale) > 1.5:
                errors.append(
                    f"{asset_id}: panelized held-tool material has high seam score {seam_after} and tilingScale {tiling_scale}; lower scale or replace source"
                )
            else:
                warnings.append(f"{asset_id}: high seamScoreAfter={seam_after} tilingScale={tiling_scale}")

    return assets


def validate_source_atlases(errors: list[str], warnings: list[str]) -> int:
    entry_count = 0
    for manifest_path in SOURCE_ATLAS_MANIFESTS:
        if not manifest_path.exists():
            continue
        payload = load_json(manifest_path)
        for entry in payload.get("entries", []) or []:
            entry_count += 1
            entry_id = str(entry.get("id", "")).strip() or "<missing-id>"
            if str(entry.get("curationStatus", "")).strip() == "":
                errors.append(f"{entry_id}: source atlas missing curationStatus")
            if str(entry.get("productionBindingStatus", "")).strip() == "":
                errors.append(f"{entry_id}: source atlas missing productionBindingStatus")
            width = int(entry.get("width", 0) or 0)
            height = int(entry.get("height", 0) or 0)
            if width != height or width < 1024:
                errors.append(f"{entry_id}: source atlas must stay square and at least 1024, got {width}x{height}")
            source_path = project_path(str(entry.get("source", "")).strip())
            if not source_path.exists():
                errors.append(f"{entry_id}: source atlas file missing: {entry.get('source', '')}")
            elif source_path.stat().st_size > MAX_SOURCE_ATLAS_BYTES:
                mb = source_path.stat().st_size / (1024 * 1024)
                warnings.append(f"{entry_id}: source atlas compressed candidate is {mb:.2f} MB")
    return entry_count


def validate_alpha_candidates(errors: list[str], warnings: list[str]) -> int:
    entry_count = 0
    for manifest_path in ALPHA_CANDIDATE_MANIFESTS:
        if not manifest_path.exists():
            continue
        payload = load_json(manifest_path)
        for entry in payload.get("entries", []) or []:
            entry_count += 1
            entry_id = str(entry.get("id", "")).strip() or "<missing-id>"
            status = str(entry.get("status", "")).strip()
            alpha_path = project_path(str(entry.get("alphaCandidate", "")).strip())
            if not status:
                errors.append(f"{entry_id}: alpha candidate missing status")
            if status != "ALPHA_CANDIDATE_UNITY_SOURCE_PENDING_REVIEW":
                warnings.append(f"{entry_id}: alpha candidate has nonstandard status={status}")
            if not alpha_path.exists():
                errors.append(f"{entry_id}: alpha candidate file missing: {entry.get('alphaCandidate', '')}")
    return entry_count


def validate_padded_atlases(errors: list[str], warnings: list[str]) -> int:
    entry_count = 0
    for manifest_path in PADDED_ATLAS_MANIFESTS:
        if not manifest_path.exists():
            continue
        payload = load_json(manifest_path)
        if payload.get("schema") != "hecton8.batch34.padded_atlas_sources.v1":
            errors.append(f"{display_path(manifest_path)}: unexpected padded atlas schema={payload.get('schema')}")
        if payload.get("productionBindingStatus") != "PADDED_SOURCE_ATLAS_PENDING_UV_BINDING":
            errors.append(f"{display_path(manifest_path)}: unexpected padded productionBindingStatus={payload.get('productionBindingStatus')}")

        for entry in payload.get("entries", []) or []:
            entry_count += 1
            entry_id = str(entry.get("id", "")).strip() or "<missing-id>"
            if entry.get("productionBindingStatus") != "PADDED_SOURCE_ATLAS_PENDING_UV_BINDING":
                errors.append(f"{entry_id}: padded atlas has unexpected productionBindingStatus={entry.get('productionBindingStatus')}")
            width = int(entry.get("width", 0) or 0)
            height = int(entry.get("height", 0) or 0)
            if width != EXPECTED_PADDED_SIZE or height != EXPECTED_PADDED_SIZE:
                errors.append(f"{entry_id}: padded atlas must be {EXPECTED_PADDED_SIZE}x{EXPECTED_PADDED_SIZE}, got {width}x{height}")
            edge_alpha = manifest_float(entry.get("edgeAlphaNonZeroPct"))
            if edge_alpha > MAX_PADDED_EDGE_ALPHA_PCT:
                errors.append(f"{entry_id}: padded atlas edge alpha too high: {edge_alpha:.3f}%")

            source_path = project_path(str(entry.get("source", "")).strip())
            padded_path = project_path(str(entry.get("paddedAtlas", "")).strip())
            if not source_path.exists():
                errors.append(f"{entry_id}: padded source input missing: {entry.get('source', '')}")
            if not padded_path.exists():
                errors.append(f"{entry_id}: padded atlas file missing: {entry.get('paddedAtlas', '')}")
            elif padded_path.stat().st_size > MAX_PADDED_ATLAS_BYTES:
                mb = padded_path.stat().st_size / (1024 * 1024)
                warnings.append(f"{entry_id}: padded atlas source is {mb:.2f} MB before Unity compression")
    return entry_count


def validate_split_atlas_candidates(errors: list[str], warnings: list[str]) -> tuple[int, int]:
    entry_count = 0
    island_count = 0
    for manifest_path in SPLIT_ATLAS_MANIFESTS:
        if not manifest_path.exists():
            continue
        payload = load_json(manifest_path)
        if payload.get("schema") != "hecton8.batch34.split_atlas_candidates.v1":
            errors.append(f"{display_path(manifest_path)}: unexpected split atlas schema={payload.get('schema')}")
        if payload.get("productionBindingStatus") != "SPLIT_ISLAND_CANDIDATE_PENDING_UV_BINDING":
            errors.append(f"{display_path(manifest_path)}: unexpected split productionBindingStatus={payload.get('productionBindingStatus')}")

        for entry in payload.get("entries", []) or []:
            entry_count += 1
            entry_id = str(entry.get("id", "")).strip() or "<missing-id>"
            islands = entry.get("islands", []) or []
            declared_count = int(entry.get("islandCount", 0) or 0)
            if declared_count != len(islands):
                errors.append(f"{entry_id}: split islandCount={declared_count} but manifest has {len(islands)} islands")
            if declared_count <= 0:
                errors.append(f"{entry_id}: split atlas has no candidate islands")
            if entry.get("productionBindingStatus") != "SPLIT_ISLAND_CANDIDATE_PENDING_UV_BINDING":
                errors.append(f"{entry_id}: split atlas has unexpected productionBindingStatus={entry.get('productionBindingStatus')}")

            source_path = project_path(str(entry.get("source", "")).strip())
            if not source_path.exists():
                errors.append(f"{entry_id}: split source input missing: {entry.get('source', '')}")

            for island in islands:
                island_count += 1
                island_path = project_path(str(island.get("path", "")).strip())
                if not island_path.exists():
                    errors.append(f"{entry_id}: split island file missing: {island.get('path', '')}")
                    continue
                width = int(island.get("width", 0) or 0)
                height = int(island.get("height", 0) or 0)
                if width != height or width < MIN_SPLIT_ISLAND_SIZE or width > MAX_SPLIT_ISLAND_SIZE:
                    errors.append(
                        f"{entry_id}: split island must be square {MIN_SPLIT_ISLAND_SIZE}-{MAX_SPLIT_ISLAND_SIZE}, got {width}x{height}"
                    )
                edge_alpha = manifest_float(island.get("edgeAlphaNonZeroPct"))
                if edge_alpha > MAX_SPLIT_EDGE_ALPHA_PCT:
                    errors.append(f"{entry_id}: split island edge alpha too high {edge_alpha:.3f}%: {island.get('path', '')}")
    return entry_count, island_count


def validate_catalog(
    errors: list[str],
    warnings: list[str],
    material_assets: list[tuple[Path, dict]],
    source_atlas_count: int,
    alpha_candidate_count: int,
    padded_atlas_count: int,
    split_atlas_entry_count: int,
    split_island_count: int,
) -> None:
    catalog_path = CATALOG if CATALOG.exists() else LEGACY_CATALOG
    if not catalog_path.exists():
        warnings.append(f"catalog missing: {display_path(CATALOG)}")
        return

    catalog = load_json(catalog_path)
    material_count = int(catalog.get("materialCount", -1))
    if material_count != len(material_assets):
        errors.append(f"catalog materialCount={material_count} but material manifests contain {len(material_assets)} assets")

    catalog_source_count = int(catalog.get("sourceAtlasCount", -1))
    if source_atlas_count and catalog_source_count != source_atlas_count:
        errors.append(f"catalog sourceAtlasCount={catalog_source_count} but source atlas manifests contain {source_atlas_count} entries")

    catalog_alpha_count = int(catalog.get("alphaCandidateCount", -1))
    if alpha_candidate_count and catalog_alpha_count != alpha_candidate_count:
        errors.append(f"catalog alphaCandidateCount={catalog_alpha_count} but alpha manifests contain {alpha_candidate_count} entries")

    catalog_padded_count = int(catalog.get("paddedAtlasCount", -1))
    if padded_atlas_count and catalog_padded_count != padded_atlas_count:
        errors.append(f"catalog paddedAtlasCount={catalog_padded_count} but padded manifests contain {padded_atlas_count} entries")

    catalog_split_entry_count = int(catalog.get("splitAtlasEntryCount", -1))
    if split_atlas_entry_count and catalog_split_entry_count != split_atlas_entry_count:
        errors.append(
            f"catalog splitAtlasEntryCount={catalog_split_entry_count} but split manifests contain {split_atlas_entry_count} entries"
        )

    catalog_split_island_count = int(catalog.get("splitIslandCount", -1))
    if split_island_count and catalog_split_island_count != split_island_count:
        errors.append(f"catalog splitIslandCount={catalog_split_island_count} but split manifests contain {split_island_count} islands")

    source_consumer_count = int(catalog.get("sourceConsumerBindingCount", -1))
    actual_source_consumer_count = sum(
        len(entry.get("consumers", []) or [])
        for entry in catalog.get("sourceConsumerBindings", []) or []
    )
    if source_consumer_count != actual_source_consumer_count:
        errors.append(
            f"catalog sourceConsumerBindingCount={source_consumer_count} but sourceConsumerBindings contain {actual_source_consumer_count}"
        )
    if actual_source_consumer_count <= 0 and alpha_candidate_count:
        warnings.append("catalog has alpha candidates but no source consumer bindings")

    material_ids = {
        str(entry.get("id", "")).strip()
        for entry in catalog.get("materials", []) or []
        if str(entry.get("id", "")).strip()
    }
    material_consumer_ids = {
        str(entry.get("materialId", "")).strip()
        for entry in catalog.get("consumerBindings", []) or []
        if str(entry.get("materialId", "")).strip() and entry.get("consumers")
    }
    for material_id in sorted(material_ids - material_consumer_ids):
        errors.append(f"catalog material has no consumer binding: {material_id}")

    source_ids = {
        str(entry.get("id", "")).strip()
        for entry in catalog.get("sourceAtlases", []) or []
        if str(entry.get("id", "")).strip()
    }
    source_consumer_ids = {
        str(entry.get("sourceId", "")).strip()
        for entry in catalog.get("sourceConsumerBindings", []) or []
        if str(entry.get("sourceId", "")).strip() and entry.get("consumers")
    }
    for source_id in sorted(source_ids - source_consumer_ids):
        errors.append(f"catalog source atlas has no consumer binding: {source_id}")

    source_consumer_lanes_by_id: dict[str, set[str]] = {}
    for entry in catalog.get("sourceConsumerBindings", []) or []:
        source_id = str(entry.get("sourceId", "")).strip()
        if not source_id:
            continue
        lanes = {
            str(consumer.get("lane", "")).strip()
            for consumer in entry.get("consumers", []) or []
            if str(consumer.get("lane", "")).strip()
        }
        source_consumer_lanes_by_id[source_id] = lanes

    for entry in catalog.get("sourceAtlases", []) or []:
        source_id = str(entry.get("id", "")).strip()
        source_type = str(entry.get("sourceType", "")).strip()
        if not source_id:
            continue
        lanes = source_consumer_lanes_by_id.get(source_id, set())
        required_lanes = SOURCE_TYPE_REQUIRED_CONSUMER_LANES.get(source_type)
        if required_lanes and not required_lanes.issubset(lanes):
            missing_lanes = ", ".join(sorted(required_lanes - lanes))
            present_lanes = ", ".join(sorted(lanes)) if lanes else "<none>"
            errors.append(
                f"catalog source atlas {source_id} ({source_type}) missing required source consumer lane(s): {missing_lanes}; present={present_lanes}"
            )
        if source_type in {"UV_ATLAS", "PICKUP_ATLAS"} and VISOR_TRAUMA_LANE in lanes:
            if "batch34_uv_atlas_material_handoff" not in lanes:
                errors.append(
                    f"catalog source atlas {source_id} ({source_type}) is bound to {VISOR_TRAUMA_LANE} without mesh/material handoff"
                )

    expected_manifests = {display_path(path) for path in material_manifest_paths()}
    catalog_manifests = {str(entry.get("path", "")) for entry in catalog.get("materialManifests", []) or []}
    missing = sorted(expected_manifests - catalog_manifests)
    for path in missing:
        errors.append(f"catalog missing material manifest entry: {path}")

    for preview_path in catalog.get("previewSheets", []) or []:
        if not project_path(str(preview_path)).exists():
            errors.append(f"catalog preview sheet missing: {preview_path}")

    for field_name, manifests in (
        ("sourceAtlasManifests", SOURCE_ATLAS_MANIFESTS),
        ("alphaCandidateManifests", ALPHA_CANDIDATE_MANIFESTS),
        ("paddedAtlasManifests", PADDED_ATLAS_MANIFESTS),
        ("splitAtlasManifests", SPLIT_ATLAS_MANIFESTS),
    ):
        expected = {display_path(path) for path in manifests if path.exists()}
        actual = {str(path) for path in catalog.get(field_name, []) or []}
        for missing_path in sorted(expected - actual):
            errors.append(f"catalog missing {field_name} entry: {missing_path}")


def main() -> int:
    errors: list[str] = []
    warnings: list[str] = []

    material_assets = validate_material_manifests(errors, warnings)
    source_atlas_count = validate_source_atlases(errors, warnings)
    alpha_candidate_count = validate_alpha_candidates(errors, warnings)
    padded_atlas_count = validate_padded_atlases(errors, warnings)
    split_atlas_entry_count, split_island_count = validate_split_atlas_candidates(errors, warnings)
    validate_catalog(
        errors,
        warnings,
        material_assets,
        source_atlas_count,
        alpha_candidate_count,
        padded_atlas_count,
        split_atlas_entry_count,
        split_island_count,
    )

    print("GEMINI_GENERATED_MATERIAL_STATE_VALIDATOR")
    print(f"materialManifests={len(material_manifest_paths())}")
    print(f"materialAssets={len(material_assets)}")
    print(f"sourceAtlases={source_atlas_count}")
    print(f"alphaCandidates={alpha_candidate_count}")
    print(f"paddedAtlases={padded_atlas_count}")
    print(f"splitAtlasEntries={split_atlas_entry_count}")
    print(f"splitIslands={split_island_count}")
    print(f"errors={len(errors)}")
    print(f"warnings={len(warnings)}")
    for error in errors:
        print(f"ERROR {error}")
    for warning in warnings:
        print(f"WARN {warning}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
