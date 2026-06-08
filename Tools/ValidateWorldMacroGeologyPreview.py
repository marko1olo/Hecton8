#!/usr/bin/env python3
"""Validate a WorldMacroGeology preview manifest as a live terrain proof."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from BuildWorldMacroGeologyPreview import ARTIFACT_VERSION

ROOT = Path(__file__).resolve().parents[1]
MIN_AREA_KM2 = 900.0
MIN_CHUNK_AXIS = 59
REQUIRED_FILES = {
    "terrain_relief_proof",
    "raw_elevation",
    "hillshade",
    "local_relief",
    "geology_zones",
    "depth_strata",
    "waterline_sweep",
    "material_regions",
    "slope",
    "curvature",
    "erosion_flow",
    "shelf_mask",
    "trench_mask",
    "ridge_fault_mask",
    "sediment_seep_mask",
    "meso_terrain_controls",
    "scatter_eligibility",
    "voxel_seam_mask",
    "contact_sheet",
    "terrain_contact_sheet",
    "chunk_manifest",
}
REQUIRED_ZONES = {
    "PhoticShelf": 1000,
    "ShelfBreak": 800,
    "FaultRidge": 1000,
    "BrineTrench": 100,
    "AbyssalPlain": 1000,
    "SedimentFan": 100,
    "ColdSeepField": 50,
    "HadalBasin": 100,
}
REQUIRED_STRATA = {
    "Photic": 1000,
    "Mesophotic": 1000,
    "Bathyal": 1000,
    "Abyssal": 1000,
    "Hadal": 50,
}
REQUIRED_WAYPOINTS = {
    "shelf_approach",
    "shelf_break_descent",
    "canyon_sediment_fan",
    "ridge_flank",
    "trench_floor",
    "basin_floor",
    "voxel_seam_candidate",
}
MIN_CONTINUATION_HEIGHT_RANGE_METERS = 650.0
MAX_UNKNOWN_ZONE_PERCENT = 0.5


def fail(errors: list[str], message: str) -> None:
    errors.append(message)


def load_manifest(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def validate(path: Path) -> int:
    errors: list[str] = []
    path = path.resolve()
    manifest = load_manifest(path)
    files = manifest.get("files", {})
    hashes = manifest.get("sha256", {})

    if manifest.get("artifact") != "WorldMacroGeologyPreview":
        fail(errors, "artifact is not WorldMacroGeologyPreview")
    if int(manifest.get("artifactVersion", -1)) != ARTIFACT_VERSION:
        fail(errors, f"artifactVersion must be {ARTIFACT_VERSION}")
    if float(manifest.get("areaSquareKilometers", 0.0)) < MIN_AREA_KM2:
        fail(errors, f"areaSquareKilometers must be at least {MIN_AREA_KM2}")
    if int(manifest.get("chunkCountAxis", 0)) < MIN_CHUNK_AXIS:
        fail(errors, f"chunkCountAxis must be at least {MIN_CHUNK_AXIS}")
    depth_range = manifest.get("depthMeters", {})
    if float(depth_range.get("max", 999999.0)) > 4700.0:
        fail(errors, "depthMeters.max exceeds 4700m; macro map should generally live in the 3-4km depth regime")

    preview_window = manifest.get("previewWindow", {})
    origin = manifest.get("originMeters")
    if not isinstance(origin, dict):
        origin = preview_window.get("originMeters", {}) if isinstance(preview_window, dict) else {}
    origin_x = float(origin.get("x", 0.0)) if isinstance(origin, dict) else 0.0
    origin_z = float(origin.get("z", 0.0)) if isinstance(origin, dict) else 0.0
    is_authored_window = abs(origin_x) < 1.0e-5 and abs(origin_z) < 1.0e-5
    if preview_window.get("proceduralContinuationBeyondWindow") is not True:
        fail(errors, "previewWindow must declare procedural continuation beyond the proof window")
    if preview_window.get("coordinateSpace") != "AUP/world XZ meters":
        fail(errors, "previewWindow coordinateSpace must be AUP/world XZ meters")

    sweep = manifest.get("waterLevelSweepMeters", {})
    for level in ("-100", "0", "100"):
        if level not in sweep:
            fail(errors, f"waterLevelSweepMeters missing level {level}")

    missing_files = sorted(REQUIRED_FILES - set(files))
    if missing_files:
        fail(errors, "missing file entries: " + ", ".join(missing_files))

    for key in sorted(REQUIRED_FILES & set(files)):
        rel = Path(files[key])
        file_path = ROOT / rel
        if not file_path.is_file():
            fail(errors, f"{key} file does not exist: {rel.as_posix()}")
            continue
        digest = hashlib.sha256(file_path.read_bytes()).hexdigest()
        if hashes.get(key) != digest:
            fail(errors, f"{key} sha256 mismatch")

    chunk_manifest_rel = files.get("chunk_manifest")
    if chunk_manifest_rel:
        chunk_manifest_path = ROOT / Path(chunk_manifest_rel)
        if chunk_manifest_path.is_file():
            chunk_manifest = json.loads(chunk_manifest_path.read_text(encoding="utf-8"))
            if chunk_manifest.get("artifact") != "WorldMacroGeologyChunkManifest":
                fail(errors, "chunk_manifest artifact is not WorldMacroGeologyChunkManifest")
            if int(chunk_manifest.get("artifactVersion", 0)) != int(manifest.get("artifactVersion", 0)):
                fail(errors, "chunk_manifest artifactVersion mismatch")
            expected_chunks = int(manifest.get("chunkCountAxis", 0)) ** 2
            if int(chunk_manifest.get("chunkCount", 0)) != expected_chunks:
                fail(errors, f"chunk_manifest chunkCount must equal {expected_chunks}")
            storage_contract = chunk_manifest.get("storageContract", {})
            for required_key in ("runtimeMissPolicy", "saveLoadPolicy", "stalePolicy"):
                if required_key not in storage_contract:
                    fail(errors, f"chunk_manifest storageContract missing {required_key}")
            chunks = chunk_manifest.get("chunks", [])
            artifact_ids = [chunk.get("artifactId") for chunk in chunks if isinstance(chunk, dict)]
            if len(set(artifact_ids)) != len(artifact_ids):
                fail(errors, "chunk_manifest contains duplicate artifactId values")
            for chunk in chunks[:16] + chunks[-16:]:
                if not isinstance(chunk, dict):
                    fail(errors, "chunk_manifest chunk entry is not an object")
                    continue
                if float(chunk.get("boundsMaxX", 0.0)) <= float(chunk.get("boundsMinX", 0.0)):
                    fail(errors, "chunk_manifest chunk has invalid X bounds")
                if float(chunk.get("boundsMaxZ", 0.0)) <= float(chunk.get("boundsMinZ", 0.0)):
                    fail(errors, "chunk_manifest chunk has invalid Z bounds")

    zone_counts = manifest.get("zoneCounts", {})
    unknown_count = int(zone_counts.get("Unknown", 0))
    resolution = int(manifest.get("resolution", 0))
    total_pixels = max(1, resolution * resolution)
    if unknown_count > total_pixels * (MAX_UNKNOWN_ZONE_PERCENT / 100.0):
        fail(errors, f"Unknown zone exceeds {MAX_UNKNOWN_ZONE_PERCENT} percent of preview")

    non_zero_zones = [zone for zone, count in zone_counts.items() if zone != "Unknown" and int(count) > 0]
    if is_authored_window:
        for zone, minimum in REQUIRED_ZONES.items():
            if int(zone_counts.get(zone, 0)) < minimum:
                fail(errors, f"{zone} count below minimum {minimum}")
    elif len(non_zero_zones) < 2:
        fail(errors, "continuation preview must contain at least two resolved geology zones")

    strata_counts = manifest.get("depthStrataCounts", {})
    if is_authored_window:
        for stratum, minimum in REQUIRED_STRATA.items():
            if int(strata_counts.get(stratum, 0)) < minimum:
                fail(errors, f"{stratum} depth stratum count below minimum {minimum}")

    hadal_pixels = int(strata_counts.get("Hadal", 0))
    if is_authored_window and hadal_pixels > total_pixels * 0.15:
        fail(errors, "Hadal stratum exceeds 15 percent of preview; macro map is too deep-biased")

    height_range = manifest.get("heightMeters", {})
    height_span = float(height_range.get("max", 0.0)) - float(height_range.get("min", 0.0))
    if not is_authored_window and height_span < MIN_CONTINUATION_HEIGHT_RANGE_METERS:
        fail(errors, f"continuation preview height range below {MIN_CONTINUATION_HEIGHT_RANGE_METERS}m")

    waypoints = manifest.get("playerApproachProofWaypoints", [])
    waypoint_ids = {entry.get("id") for entry in waypoints if isinstance(entry, dict)}
    if is_authored_window:
        missing_waypoints = sorted(REQUIRED_WAYPOINTS - waypoint_ids)
        if missing_waypoints:
            fail(errors, "missing proof waypoints: " + ", ".join(missing_waypoints))
    elif not waypoint_ids:
        fail(errors, "continuation preview must expose at least one proof waypoint")

    for entry in waypoints:
        if not isinstance(entry, dict):
            fail(errors, "waypoint entry is not an object")
            continue
        if "worldX" not in entry or "worldZ" not in entry:
            fail(errors, f"waypoint {entry.get('id', '<unknown>')} missing world coordinates")
        if float(entry.get("depthMeters", -1.0)) < 0.0:
            fail(errors, f"waypoint {entry.get('id', '<unknown>')} has invalid depth")

    if errors:
        for error in errors:
            print("ERROR:", error)
        return 1

    print(
        json.dumps(
            {
                "status": "ok",
                "validationMode": "authored-window" if is_authored_window else "continuation-window",
                "manifest": str(path.relative_to(ROOT).as_posix()),
                "areaSquareKilometers": manifest["areaSquareKilometers"],
                "chunkCountAxis": manifest["chunkCountAxis"],
                "resolution": manifest["resolution"],
            },
            indent=2,
        )
    )
    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", help="Path to WorldMacroGeologyPreviewManifest.json")
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()
    raise SystemExit(validate(Path(args.manifest)))
