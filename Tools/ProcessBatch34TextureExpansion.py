#!/usr/bin/env python3
"""Intake Batch34 external texture-source images without touching Unity assets."""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
import math
import shutil
from dataclasses import dataclass, asdict
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageOps, ImageStat


ROOT = Path(__file__).resolve().parents[1]
DOWNLOADS = Path(r"C:\Users\danat\Downloads")
OUTPUT_ROOT = ROOT / "Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion"

SOURCE_TYPES_WITH_PBR = {"SEAMLESS_TILE", "TRIM_SHEET"}
IMAGE_EXTS = {".jpg", ".jpeg", ".png", ".webp"}
BATCH34_DOWNLOAD_CANDIDATE_GLOB = "*_2026060803*.jpeg"


@dataclass(frozen=True)
class JobSpec:
    bid: str
    slug: str
    title: str
    source_type: str
    source_glob: str
    family: str
    use: str
    metallic: float
    roughness: float
    normal_strength: float


JOBS = [
    JobSpec("3401", "photic_limestone_rubble_shelf", "Photic Limestone Rubble Shelf", "SEAMLESS_TILE", "Limestone_rubble_shelf_material_202606080323.jpeg", "terrain_photic", "bright shallow route terrain, limestone ledges, coral rubble shelves", 0.0, 0.72, 2.2),
    JobSpec("3402", "shallow_seagrass_root_mat_substrate", "Shallow Seagrass Root-Mat Substrate", "SEAMLESS_TILE", "Seagrass_alien_root-mat_material_202606080323.jpeg", "terrain_photic", "shallow flora root transitions, sand/rock blend", 0.0, 0.78, 1.7),
    JobSpec("3403", "brine_canyon_salt_crust_silt", "Brine Canyon Salt-Crust Silt", "SEAMLESS_TILE", "Salt_crust_silt_material_tile_202606080323.jpeg", "terrain_brine", "brine canyon floors, density-layer margins, hazard terrain", 0.0, 0.82, 2.0),
    JobSpec("3404", "abyssal_manganese_nodule_plain", "Abyssal Manganese Nodule Plain", "SEAMLESS_TILE", "Seafloor_clay_with_manganese_nod*_202606080323.jpeg", "terrain_abyssal", "abyssal plain terrain and nodule scatter base", 0.04, 0.76, 2.1),
    JobSpec("3405", "methane_hydrate_crack_vein", "Methane Hydrate Crack Vein", "SEAMLESS_TILE", "Seabed_mud_with_hydrate_crust_202606080323.jpeg", "terrain_cold_seep", "cold seep terrain and hydrate hazard substrate", 0.0, 0.68, 2.4),
    JobSpec("3406", "serpentinite_fault_rock", "Serpentinite Fault Rock", "SEAMLESS_TILE", "Serpentinite_fault_rock_material*_202606080323.jpeg", "terrain_geology", "greenish fault rock distinct from basalt", 0.0, 0.62, 2.7),
    JobSpec("3407", "iron_oxide_seep_crust", "Iron-Oxide Seep Crust", "SEAMLESS_TILE", "Iron-oxide_seep_crust_material_202606080323.jpeg", "terrain_cold_seep", "iron bacteria and oxidized seep terrain", 0.0, 0.84, 2.1),
    JobSpec("3408", "clay_silt_turbidity_slope", "Clay Silt Turbidity Slope", "SEAMLESS_TILE", "Seamless_PBR_material_tile_202606080323.jpeg", "terrain_sediment", "silt-heavy traversal slopes and disturbed sediment", 0.0, 0.88, 1.25),
    JobSpec("3409", "limestone_cave_ceiling_mineral_drip", "Limestone Cave Ceiling Mineral Drip", "SEAMLESS_TILE", "Limestone_cave_ceiling_material_*_202606080323.jpeg", "terrain_cave", "submerged limestone cave ceiling and mineral drip detail", 0.0, 0.74, 2.2),
    JobSpec("3410", "drowned_concrete_rubble", "Drowned Concrete Rubble", "SEAMLESS_TILE", "Concrete_rubble_substrate_materi*_202606080322.jpeg", "hard_surface_ruin", "old colony floor/wall rubble and infrastructure blends", 0.05, 0.78, 2.4),
    JobSpec("3411", "pressure_base_exterior_hull_trim_sheet", "Pressure Base Exterior Hull Trim Sheet", "TRIM_SHEET", "Modular_trim_sheet_texture_202606080322.jpeg", "hard_surface_trim", "base exteriors, module edges, hatch frames, pressure ribs", 0.48, 0.46, 2.3),
    JobSpec("3412", "pressure_base_interior_wall_trim_sheet", "Pressure Base Interior Wall Trim Sheet", "TRIM_SHEET", "Modular_trim_sheet_interior_walls_202606080322.jpeg", "hard_surface_trim", "corridors, safe rooms, wreck interiors, wall panels", 0.32, 0.50, 2.0),
    JobSpec("3413", "wet_service_deck_anti_slip_floor", "Wet Service Deck Anti-Slip Floor", "SEAMLESS_TILE", "Industrial_metal_floor_material_202606080322.jpeg", "hard_surface_floor", "service decks, corridors, airlocks, exterior platforms", 0.58, 0.42, 2.8),
    JobSpec("3414", "rubber_gasket_ring_trim_sheet", "Rubber Gasket Ring Trim Sheet", "TRIM_SHEET", "Modular_trim_sheet_rubber_gaskets_202606080322.jpeg", "rubber_trim", "hatches, viewport rims, pressure doors, pipe sockets", 0.0, 0.68, 2.6),
    JobSpec("3415", "cable_jacket_repair_wrap_tile", "Cable Jacket Repair Wrap Tile", "SEAMLESS_TILE", "Seamless_PBR_material_tile_202606080322.jpeg", "rubber_cable", "cables, hoses, tether surfaces, repair wrapped props", 0.0, 0.62, 2.2),
    JobSpec("3416", "ribbed_flexible_hose_material", "Ribbed Flexible Hose Material", "SEAMLESS_TILE", "Ribbed_flexible_hose_material_tile_202606080322.jpeg", "rubber_cable", "oxygen hoses, coolant lines, base pipes, tool cords", 0.0, 0.58, 3.0),
    JobSpec("3417", "amber_emergency_lens_material", "Amber Emergency Lens Material", "SEAMLESS_TILE", "Amber_emergency_light_lens_material_202606080322.jpeg", "glass_lens", "warning lights, service lamps, emissive masks, physical lenses", 0.0, 0.28, 1.35),
    JobSpec("3418", "thick_viewport_glass_edge_decal_atlas", "Thick Viewport Glass Edge Decal Atlas", "DECAL_ATLAS", "Decal_atlas_for_viewport_glass_202606080322.jpeg", "glass_decal", "viewport rims, cockpit glass, pressure windows, glass edge wear", 0.0, 0.35, 1.0),
    JobSpec("3419", "welded_seam_and_rivet_row_trim_sheet", "Welded Seam And Rivet Row Trim Sheet", "TRIM_SHEET", "Modular_trim_sheet_for_seams_202606080320.jpeg", "hard_surface_trim", "wreckage, base hulls, repaired panels, industrial seams", 0.62, 0.56, 3.0),
    JobSpec("3420", "salvage_cut_cross_section_trim_atlas", "Salvage Cut Cross-Section Trim Atlas", "TRIM_SHEET", "Trim_atlas_for_HECTON-8_salvage-*_202606080320.jpeg", "hard_surface_trim", "cut panels, opened wrecks, salvage resource surfaces", 0.48, 0.58, 2.7),
    JobSpec("3421", "damped_insulation_blanket_material", "Damped Insulation Blanket Material", "SEAMLESS_TILE", "Quilted_fabric_insulation_materi*_202606080320.jpeg", "fabric_insulation", "module interiors, damaged insulation, equipment backing", 0.0, 0.86, 1.9),
    JobSpec("3422", "pressure_suit_patch_trim_sheet", "Pressure Suit Patch Trim Sheet", "TRIM_SHEET", "Square_trim_sheet_for_suit_202606080319.jpeg", "suit_fabric", "suit meshes, tool pouches, wearable repair patches", 0.0, 0.72, 2.0),
    JobSpec("3423", "leak_rust_biofilm_decal_atlas", "Leak Rust Biofilm Decal Atlas", "DECAL_ATLAS", "Decal_atlas_for_underwater_leaks_202606080319.jpeg", "damage_decal", "base/wreck damage, damp panels, old machinery", 0.0, 0.82, 1.0),
    JobSpec("3424", "paint_chip_scratch_decal_atlas", "Paint Chip Scratch Decal Atlas", "DECAL_ATLAS", "Decal_atlas_chipped_paint_scratches_202606080319.jpeg", "damage_decal", "hard-surface edge wear, tools, modules, salvage", 0.0, 0.78, 1.0),
    JobSpec("3425", "salt_mineral_deposit_decal_atlas", "Salt Mineral Deposit Decal Atlas", "DECAL_ATLAS", "Square_decal_atlas_salt_mineral_202606080319.jpeg", "damage_decal", "wet edges, cave rocks, hull seams, old base interiors", 0.0, 0.86, 1.0),
    JobSpec("3426", "instrument_glass_smudge_alpha_decal_atlas", "Instrument Glass Smudge Alpha Decal Atlas", "DECAL_ATLAS", "Decal_atlas_for_instrument_glass_202606080325.jpeg", "glass_decal", "visor, cockpit, scanner glass, terminals", 0.0, 0.38, 1.0),
    JobSpec("3427", "pressure_crack_glass_decal_atlas", "Pressure Crack Glass Decal Atlas", "DECAL_ATLAS", "Square_decal_atlas_pressure_cracks_202606080325.jpeg", "glass_decal", "damaged viewports, scanner lenses, cockpit glass", 0.0, 0.42, 1.0),
    JobSpec("3428", "warning_paint_stripe_decal_atlas", "Warning Paint Stripe Decal Atlas", "DECAL_ATLAS", "Decal_atlas_worn_warning_paint_202606080325.jpeg", "damage_decal", "hazard stripes, panel markings without text, trim variation", 0.0, 0.72, 1.0),
    JobSpec("3429", "cutter_burn_scorch_decal_atlas", "Cutter Burn Scorch Decal Atlas", "DECAL_ATLAS", "Decal_atlas_for_cutter_burn_202606080325.jpeg", "damage_decal", "cutter tool impact, salvage cuts, repair/weld states", 0.0, 0.70, 1.0),
    JobSpec("3430", "barnacle_colony_decal_variants", "Barnacle Colony Decal Variants", "DECAL_ATLAS", "Decal_atlas_alien_barnacle_colony_202606080325.jpeg", "organic_decal", "rocks, hulls, cave surfaces, colony overgrowth", 0.0, 0.76, 1.0),
    JobSpec("3431", "wetness_rivulet_decal_atlas", "Wetness Rivulet Decal Atlas", "DECAL_ATLAS", "Decal_atlas_for_wetness_rivulets_202606080326.jpeg", "damage_decal", "interiors, hatches, floors, wet rock, leak presentation", 0.0, 0.34, 1.0),
    JobSpec("3432", "contamination_biohazard_stain_atlas", "Contamination Biohazard Stain Atlas", "DECAL_ATLAS", "Decal_atlas_contamination_stains_202606080326.jpeg", "organic_decal", "contaminated rooms, organism stains, procedural evidence", 0.0, 0.82, 1.0),
    JobSpec("3433", "brine_vane_flora_uv_atlas", "Brine Vane Flora UV Atlas", "UV_ATLAS", "Alien_flora_texture_reference_ma*_202606080325.jpeg", "flora_uv", "brine-zone plants and alien flora mesh materials", 0.0, 0.68, 1.0),
    JobSpec("3434", "shallow_alien_seagrass_blade_atlas", "Shallow Alien Seagrass Blade Atlas", "UV_ATLAS", "Seagrass_blades_texture_referenc*_202606080325.jpeg", "flora_uv", "shallow flora cards/meshes, photic route grass/seagrass", 0.0, 0.62, 1.0),
    JobSpec("3435", "plate_coral_rim_uv_atlas", "Plate Coral Rim UV Atlas", "UV_ATLAS", "Coral_material_reference_for_UVs_202606080326.jpeg", "flora_uv", "shallow plate coral meshes, reef geometry, coral trim", 0.0, 0.70, 1.0),
    JobSpec("3436", "sponge_pore_organic_atlas", "Sponge Pore Organic Atlas", "UV_ATLAS", "UV_atlas_texture_reference_sponge_202606080326.jpeg", "flora_uv", "sponge/coral variants, organic rock overgrowth", 0.0, 0.74, 1.0),
    JobSpec("3437", "kelp_holdfast_root_atlas", "Kelp Holdfast Root Atlas", "UV_ATLAS", "Kelp_holdfast_roots_anchor_pads_202606080326.jpeg", "flora_uv", "kelp anchors/roots, flora root geometry, terrain contact", 0.0, 0.72, 1.0),
    JobSpec("3438", "tube_worm_soft_crown_atlas", "Tube Worm Soft Crown Atlas", "UV_ATLAS", "Tube_worm_texture_reference_mate*_202606080326.jpeg", "flora_uv", "tube worm and vent organism meshes", 0.0, 0.70, 1.0),
    JobSpec("3439", "spore_pod_and_seed_sac_atlas", "Spore Pod And Seed Sac Atlas", "UV_ATLAS", "Alien_spore_pods_seed_sacs_202606080328.jpeg", "flora_uv", "harvestable flora pods, scan targets, small organic props", 0.0, 0.58, 1.0),
    JobSpec("3440", "cave_lichen_biofilm_sheet_atlas", "Cave Lichen Biofilm Sheet Atlas", "UV_ATLAS", "UV_decal_atlas_texture_reference_202606080326.jpeg", "organic_decal", "cave wall overlays, decals, lichen meshes, biofilm props", 0.0, 0.76, 1.0),
    JobSpec("3441", "neutral_grazer_skin_uv_atlas", "Neutral Grazer Skin UV Atlas", "UV_ATLAS", "Creature_UV_atlas_texture_reference_202606080326.jpeg", "fauna_uv", "first-route neutral fauna body material zones", 0.0, 0.62, 1.0),
    JobSpec("3442", "filter_feeder_gill_membrane_atlas", "Filter Feeder Gill Membrane Atlas", "UV_ATLAS", "Creature_gill_membranes_UV_atlas_202606080326.jpeg", "fauna_uv", "filter-feeder creature organs and gill/frond materials", 0.0, 0.48, 1.0),
    JobSpec("3443", "small_predator_jaw_fin_eye_atlas", "Small Predator Jaw Fin Eye Atlas", "UV_ATLAS", "Creature_jaw_fin_eye_material_202606080326.jpeg", "fauna_uv", "small aggressive fauna jaw, fin, and eye material zones", 0.0, 0.44, 1.0),
    JobSpec("3444", "armored_benthic_shell_atlas", "Armored Benthic Shell Atlas", "UV_ATLAS", "Shell_plates_reference_material_202606080326.jpeg", "fauna_uv", "crablike/scavenger shell plates and armor", 0.0, 0.68, 1.0),
    JobSpec("3445", "translucent_larva_egg_sac_atlas", "Translucent Larva Egg Sac Atlas", "UV_ATLAS", "Larva_egg_sac_texture_reference_202606080327.jpeg", "fauna_uv", "ecology props, creature nests, scan/evidence objects", 0.0, 0.42, 1.0),
    JobSpec("3446", "scavenged_carcass_bone_flesh_atlas", "Scavenged Carcass Bone Flesh Atlas", "UV_ATLAS", "UV_atlas_texture_reference_material_202606080327.jpeg", "fauna_uv", "environmental evidence, carcass props, creature remains", 0.0, 0.64, 1.0),
    JobSpec("3447", "creature_eye_lens_wet_organ_atlas", "Creature Eye Lens Wet Organ Atlas", "UV_ATLAS", "Creature_eye_texture_reference_m*_202606080327.jpeg", "fauna_uv", "fauna eyes, sensory organs, scanner closeups", 0.0, 0.30, 1.0),
    JobSpec("3448", "resource_nodule_pickup_uv_atlas", "Resource Nodule Pickup UV Atlas", "PICKUP_ATLAS", "Resource_pickup_UV_atlas_texture_202606080327.jpeg", "pickup_resource", "3D pickup/resource meshes, not inventory icons", 0.08, 0.58, 1.0),
    JobSpec("3449", "industrial_salvage_small_parts_atlas", "Industrial Salvage Small Parts Atlas", "PICKUP_ATLAS", "Industrial_salvage_small-parts_U*_202606080327.jpeg", "pickup_salvage", "salvage pickups, small mesh parts, crafting visuals", 0.35, 0.56, 1.0),
    JobSpec("3450", "data_core_wet_circuit_ceramic_atlas", "Data Core Wet Circuit Ceramic Atlas", "PICKUP_ATLAS", "Ceramic_UV_atlas_texture_reference_202606080327.jpeg", "pickup_salvage", "black-box/data core props, electronics salvage", 0.22, 0.46, 1.0),
]


def project_rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def ensure_dirs() -> dict[str, Path]:
    paths = {
        "originals": OUTPUT_ROOT / "Originals",
        "cleaned": OUTPUT_ROOT / "SourceCleaned",
        "compressed": OUTPUT_ROOT / "SourceCompressed",
        "derived": OUTPUT_ROOT / "Derived",
        "tile_previews": OUTPUT_ROOT / "TilePreviews",
        "crop_previews": OUTPUT_ROOT / "WatermarkReview",
        "contact": OUTPUT_ROOT / "ContactSheets",
        "qa": OUTPUT_ROOT / "QA",
    }
    for path in paths.values():
        path.mkdir(parents=True, exist_ok=True)
    return paths


def normalized_path_key(path: Path) -> str:
    return str(path.resolve()).casefold()


def discover_download_candidates() -> list[Path]:
    return sorted(
        [path for path in DOWNLOADS.glob(BATCH34_DOWNLOAD_CANDIDATE_GLOB) if path.suffix.lower() in IMAGE_EXTS],
        key=lambda p: (p.stat().st_mtime, p.name.lower()),
    )


def file_sha256(path: Path) -> str:
    sha = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            chunk = handle.read(1024 * 1024)
            if not chunk:
                break
            sha.update(chunk)
    return sha.hexdigest().upper()


def resolve_sources(spec: JobSpec) -> list[Path]:
    return sorted(DOWNLOADS.glob(spec.source_glob), key=lambda p: (p.stat().st_mtime, p.name.lower()))


def resolve_source(spec: JobSpec) -> Path | None:
    matches = resolve_sources(spec)
    if not matches:
        return None
    return matches[-1]


def mean_abs_delta(a: Image.Image, b: Image.Image) -> float:
    diff = ImageChops.difference(a.convert("RGB"), b.convert("RGB"))
    stat = ImageStat.Stat(diff)
    return float(sum(stat.mean) / len(stat.mean))


def seam_metrics(image: Image.Image, band_width: int = 16) -> dict[str, float]:
    rgb = image.convert("RGB")
    width, height = rgb.size
    band = max(1, min(band_width, width // 8, height // 8))
    left_edge = rgb.crop((0, 0, 1, height))
    right_edge = rgb.crop((width - 1, 0, width, height))
    top_edge = rgb.crop((0, 0, width, 1))
    bottom_edge = rgb.crop((0, height - 1, width, height))
    left_band = rgb.crop((0, 0, band, height))
    right_band = rgb.crop((width - band, 0, width, height)).transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    top_band = rgb.crop((0, 0, width, band))
    bottom_band = rgb.crop((0, height - band, width, height)).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    return {
        "edgeLR": round(mean_abs_delta(left_edge, right_edge), 4),
        "edgeTB": round(mean_abs_delta(top_edge, bottom_edge), 4),
        "bandLR": round(mean_abs_delta(left_band, right_band), 4),
        "bandTB": round(mean_abs_delta(top_band, bottom_band), 4),
    }


def clipping_stats(image: Image.Image) -> dict[str, float]:
    rgb = image.convert("RGB")
    gray = rgb.convert("L")
    values = np.asarray(gray, dtype=np.uint8)
    stat = ImageStat.Stat(gray)
    return {
        "lumMean": round(float(stat.mean[0]), 4),
        "lumMin": int(values.min()),
        "lumMax": int(values.max()),
        "blackPct": round(float(np.mean(values <= 1) * 100.0), 4),
        "whitePct": round(float(np.mean(values >= 254) * 100.0), 4),
    }


def periodic_component(channel: np.ndarray) -> np.ndarray:
    height, width = channel.shape
    boundary = np.zeros_like(channel, dtype=np.float32)
    lr = channel[:, 0] - channel[:, -1]
    boundary[:, 0] += lr
    boundary[:, -1] -= lr
    tb = channel[0, :] - channel[-1, :]
    boundary[0, :] += tb
    boundary[-1, :] -= tb

    x = np.arange(width, dtype=np.float32)
    y = np.arange(height, dtype=np.float32)
    denom = (
        2.0 * np.cos((2.0 * np.pi * x)[None, :] / width)
        + 2.0 * np.cos((2.0 * np.pi * y)[:, None] / height)
        - 4.0
    )
    denom[0, 0] = 1.0
    smooth_hat = np.fft.fft2(boundary) / denom
    smooth_hat[0, 0] = 0.0
    smooth = np.fft.ifft2(smooth_hat).real.astype(np.float32)
    return channel - smooth


def preserve_channel_mean(source: np.ndarray, refined: np.ndarray) -> np.ndarray:
    source_mean = source.reshape(-1, source.shape[2]).mean(axis=0)
    refined_mean = refined.reshape(-1, refined.shape[2]).mean(axis=0)
    return refined + (source_mean - refined_mean)[None, None, :]


def pin_outer_edges(rgb: np.ndarray) -> np.ndarray:
    pinned = rgb.copy()
    lr = (pinned[:, 0, :] + pinned[:, -1, :]) * 0.5
    pinned[:, 0, :] = lr
    pinned[:, -1, :] = lr
    tb = (pinned[0, :, :] + pinned[-1, :, :]) * 0.5
    pinned[0, :, :] = tb
    pinned[-1, :, :] = tb
    return pinned


def make_periodic_candidate(image: Image.Image, edge_pin: bool) -> Image.Image:
    rgba = image.convert("RGBA")
    data = np.asarray(rgba).astype(np.float32)
    rgb = data[:, :, :3]
    alpha = data[:, :, 3:4]
    channels = [periodic_component(rgb[:, :, channel]) for channel in range(3)]
    refined = preserve_channel_mean(rgb, np.stack(channels, axis=2))
    if edge_pin:
        refined = pin_outer_edges(refined)
    result = np.concatenate([np.clip(refined, 0, 255), alpha], axis=2).astype(np.uint8)
    return Image.fromarray(result, "RGBA")


def detect_watermark(image: Image.Image) -> dict[str, object]:
    rgb = image.convert("RGB")
    width, height = rgb.size
    x0 = int(width * 0.72)
    y0 = int(height * 0.72)
    region = np.asarray(rgb.crop((x0, y0, width, height)), dtype=np.float32) / 255.0
    bright = region.max(axis=2)
    dark = region.min(axis=2)
    saturation = np.where(bright > 0.001, (bright - dark) / np.maximum(bright, 0.001), 0.0)
    mask = (bright > 0.70) & (saturation < 0.18)
    ys, xs = np.where(mask)
    if xs.size == 0:
        return {"detected": False, "confidence": 0.0, "bbox": None}
    area = int(xs.size)
    bx0 = int(xs.min()) + x0
    by0 = int(ys.min()) + y0
    bx1 = int(xs.max()) + 1 + x0
    by1 = int(ys.max()) + 1 + y0
    box_w = bx1 - bx0
    box_h = by1 - by0
    area_ratio = area / float(max(1, (width - x0) * (height - y0)))
    compact = area / float(max(1, box_w * box_h))
    size_ok = 0.00015 <= area_ratio <= 0.045 and 12 <= box_w <= width * 0.22 and 12 <= box_h <= height * 0.22
    confidence = min(1.0, (compact * 0.8) + (0.25 if size_ok else 0.0))
    return {
        "detected": bool(size_ok and confidence >= 0.36),
        "confidence": round(float(confidence), 4),
        "bbox": [bx0, by0, bx1, by1],
        "areaRatio": round(float(area_ratio), 6),
    }


def feather_mask(size: tuple[int, int]) -> Image.Image:
    width, height = size
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    margin = max(3, int(min(width, height) * 0.08))
    draw.rounded_rectangle((margin, margin, width - margin, height - margin), radius=margin, fill=255)
    return mask.filter(ImageFilter.GaussianBlur(radius=max(5, int(min(width, height) * 0.08))))


def repair_watermark_if_needed(image: Image.Image, detection: dict[str, object]) -> tuple[Image.Image, bool]:
    if not detection.get("detected"):
        return image.convert("RGB"), False
    bbox = detection.get("bbox")
    if not isinstance(bbox, list):
        return image.convert("RGB"), False
    rgb = image.convert("RGB")
    width, height = rgb.size
    x0, y0, x1, y1 = [int(v) for v in bbox]
    pad = max(8, int(min(width, height) * 0.012))
    x0 = max(0, x0 - pad)
    y0 = max(0, y0 - pad)
    x1 = min(width, x1 + pad)
    y1 = min(height, y1 + pad)
    patch_w = x1 - x0
    patch_h = y1 - y0
    if patch_w <= 0 or patch_h <= 0:
        return rgb, False

    candidates = []
    for dx, dy in (
        (-(patch_w + pad * 2), 0),
        (-(patch_w * 2 + pad * 2), 0),
        (0, -(patch_h + pad * 2)),
        (-(patch_w + pad * 2), -(patch_h + pad * 2)),
    ):
        sx0 = max(0, min(width - patch_w, x0 + dx))
        sy0 = max(0, min(height - patch_h, y0 + dy))
        candidates.append(rgb.crop((sx0, sy0, sx0 + patch_w, sy0 + patch_h)))
    target = rgb.crop((x0, y0, x1, y1))
    best = min(candidates, key=lambda c: mean_abs_delta(target, c))
    repaired = rgb.copy()
    repaired.paste(best.filter(ImageFilter.GaussianBlur(radius=0.35)), (x0, y0), feather_mask((patch_w, patch_h)))
    return repaired, True


def save_jpeg_target(image: Image.Image, path: Path, max_size: int, start_quality: int, max_bytes: int) -> Image.Image:
    output = image.convert("RGB")
    if max(output.size) > max_size:
        output.thumbnail((max_size, max_size), Image.Resampling.LANCZOS)
    path.parent.mkdir(parents=True, exist_ok=True)
    best: bytes | None = None
    for quality in range(start_quality, 70, -3):
        buffer = io.BytesIO()
        output.save(buffer, "JPEG", quality=quality, optimize=True, progressive=True, subsampling=0)
        blob = buffer.getvalue()
        best = blob
        if len(blob) <= max_bytes:
            path.write_bytes(blob)
            return output
    if best is not None:
        path.write_bytes(best)
    else:
        output.save(path, "JPEG", quality=start_quality, optimize=True, progressive=True, subsampling=0)
    return output


def save_tile_preview(image: Image.Image, path: Path, tile_size: int) -> str:
    source = image.convert("RGB")
    if max(source.size) > tile_size:
        source.thumbnail((tile_size, tile_size), Image.Resampling.LANCZOS)
    canvas = Image.new("RGB", (source.width * 2, source.height * 2), (0, 0, 0))
    canvas.paste(source, (0, 0))
    canvas.paste(source, (source.width, 0))
    canvas.paste(source, (0, source.height))
    canvas.paste(source, (source.width, source.height))
    path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(path, "PNG")
    return project_rel(path)


def save_lower_right_crop(image: Image.Image, spec: JobSpec, paths: dict[str, Path]) -> str:
    source = image.convert("RGB")
    width, height = source.size
    crop_size = min(width, height) // 3
    crop = source.crop((width - crop_size, height - crop_size, width, height))
    out = paths["crop_previews"] / f"B34_{spec.bid}_{spec.slug}_lower_right.png"
    crop.save(out, "PNG")
    return project_rel(out)


def height_from_base(base: Image.Image) -> Image.Image:
    gray = base.convert("L").filter(ImageFilter.GaussianBlur(radius=0.85))
    values = np.asarray(gray, dtype=np.float32)
    low, high = np.percentile(values, [4, 96])
    if high <= low:
        high = low + 1.0
    normalized = np.clip((values - low) / (high - low), 0.0, 1.0)
    return Image.fromarray(np.uint8(normalized * 255.0), "L")


def normal_from_height(height: Image.Image, strength: float) -> Image.Image:
    data = np.asarray(height, dtype=np.float32) / 255.0
    dx = np.zeros_like(data)
    dy = np.zeros_like(data)
    dx[:, 1:-1] = data[:, 2:] - data[:, :-2]
    dy[1:-1, :] = data[2:, :] - data[:-2, :]
    nx = -dx * strength
    ny = -dy * strength
    nz = np.ones_like(data)
    length = np.maximum(np.sqrt(nx * nx + ny * ny + nz * nz), 0.0001)
    normal = np.stack((nx / length, ny / length, nz / length), axis=2)
    return Image.fromarray(np.uint8(np.clip((normal * 0.5 + 0.5) * 255.0, 0, 255)), "RGB")


def derive_maps(base: Image.Image, spec: JobSpec, paths: dict[str, Path]) -> dict[str, str]:
    derived_dir = paths["derived"] / f"B34_{spec.bid}_{spec.slug}"
    derived_dir.mkdir(parents=True, exist_ok=True)
    height = height_from_base(base)
    normal = normal_from_height(height, spec.normal_strength)
    height_values = np.asarray(height, dtype=np.float32) / 255.0
    ao = np.clip(0.70 + height_values * 0.30, 0.0, 1.0)
    metallic = np.full_like(ao, spec.metallic)
    roughness = np.full_like(ao, spec.roughness)
    emission = np.zeros_like(ao)

    normal_path = derived_dir / f"TX_B34_{spec.bid}_{spec.slug}_NormalGL.jpg"
    height_path = derived_dir / f"TX_B34_{spec.bid}_{spec.slug}_Height.jpg"
    mrao_path = derived_dir / f"TX_B34_{spec.bid}_{spec.slug}_MRAO_Provisional.png"
    normal.save(normal_path, "JPEG", quality=90, optimize=True, progressive=True, subsampling=0)
    height.save(height_path, "JPEG", quality=88, optimize=True, progressive=True)
    Image.fromarray(np.uint8(np.stack((metallic, roughness, ao, emission), axis=2) * 255.0), "RGBA").save(mrao_path, "PNG")
    return {
        "NormalGL": project_rel(normal_path),
        "Height": project_rel(height_path),
        "MRAO_Provisional_RGBA_Metal_Rough_AO_Emission": project_rel(mrao_path),
    }


def foreground_edge_risk(image: Image.Image) -> dict[str, object]:
    rgb = image.convert("RGB")
    width, height = rgb.size
    border = max(8, min(width, height) // 48)
    data = np.asarray(rgb, dtype=np.float32)
    corners = np.concatenate(
        [
            data[:border, :border].reshape(-1, 3),
            data[:border, -border:].reshape(-1, 3),
            data[-border:, :border].reshape(-1, 3),
            data[-border:, -border:].reshape(-1, 3),
        ],
        axis=0,
    )
    bg = np.median(corners, axis=0)
    edge_pixels = np.concatenate(
        [
            data[:border, :, :].reshape(-1, 3),
            data[-border:, :, :].reshape(-1, 3),
            data[:, :border, :].reshape(-1, 3),
            data[:, -border:, :].reshape(-1, 3),
        ],
        axis=0,
    )
    dist = np.linalg.norm(edge_pixels - bg[None, :], axis=1)
    foreground_pct = float(np.mean(dist > 28.0) * 100.0)
    return {
        "foregroundEdgePct": round(foreground_pct, 4),
        "edgeIslandRisk": foreground_pct > 8.0,
    }


def write_csv(path: Path, entries: list[dict]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    keys = sorted({key for entry in entries for key in entry.keys() if not isinstance(entry.get(key), (dict, list))})
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=keys)
        writer.writeheader()
        for entry in entries:
            writer.writerow({key: entry.get(key, "") for key in keys})


def contact_sheet(entries: list[dict], source_key: str, out_path: Path, label_key: str = "id", thumb: int = 180) -> None:
    items = []
    for entry in entries:
        rel_path = entry.get(source_key)
        if not rel_path:
            continue
        path = ROOT / str(rel_path)
        if not path.exists():
            continue
        try:
            with Image.open(path) as image:
                im = image.convert("RGB")
                im.thumbnail((thumb, thumb), Image.Resampling.LANCZOS)
                items.append((str(entry.get(label_key, "")), im.copy(), entry.get("verdict", "")))
        except OSError:
            continue
    if not items:
        return
    label_h = 34
    gap = 10
    columns = min(5, len(items))
    rows = int(math.ceil(len(items) / columns))
    canvas = Image.new("RGB", (columns * thumb + (columns - 1) * gap, rows * (thumb + label_h) + (rows - 1) * gap), (9, 12, 14))
    draw = ImageDraw.Draw(canvas)
    for index, (label, image, verdict) in enumerate(items):
        cell_x = (index % columns) * (thumb + gap)
        cell_y = (index // columns) * (thumb + label_h + gap)
        x = cell_x + (thumb - image.width) // 2
        y = cell_y + (thumb - image.height) // 2
        canvas.paste(image, (x, y))
        color = (42, 16, 12) if "REJECT" in verdict else ((42, 36, 10) if "REVIEW" in verdict else (6, 13, 15))
        draw.rectangle((cell_x, cell_y + thumb, cell_x + thumb, cell_y + thumb + label_h), fill=color)
        draw.text((cell_x + 5, cell_y + thumb + 6), label[:31], fill=(220, 230, 230))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(out_path, "PNG")


def process_one(spec: JobSpec, paths: dict[str, Path], args: argparse.Namespace) -> dict:
    matches = resolve_sources(spec)
    source = matches[-1] if matches else None
    entry: dict[str, object] = {
        "id": f"B34-{spec.bid}",
        "slug": spec.slug,
        "title": spec.title,
        "sourceType": spec.source_type,
        "family": spec.family,
        "use": spec.use,
        "sourceGlob": spec.source_glob,
        "matchedDownloadSources": [str(path) for path in matches],
        "ignoredDownloadSources": [str(path) for path in matches[:-1]],
        "unityImportStatus": "PENDING UNITY IMPORT",
        "visualStatus": "PENDING VISUAL REVIEW",
        "proofClass": "STATIC_IMAGE_PROCESSING",
        "issues": [],
        "warnings": [],
        "notes": [],
        "maps": {},
    }
    if source is None:
        entry["verdict"] = "REJECT_SOURCE_MISSING"
        entry["issues"] = ["missing_download_source"]
        return entry

    if len(matches) > 1:
        entry["warnings"].append("multiple_download_sources_matched_latest_selected")

    entry["downloadSource"] = str(source)
    original_path = paths["originals"] / f"B34_{spec.bid}_{spec.slug}_Original{source.suffix.lower()}"
    shutil.copy2(source, original_path)
    entry["originalPath"] = project_rel(original_path)
    entry["originalBytes"] = original_path.stat().st_size

    with Image.open(source) as image:
        image = ImageOps.exif_transpose(image)
        image.load()
        source_image = image.convert("RGB")

    entry["sourceWidth"], entry["sourceHeight"] = source_image.size
    if source_image.width != source_image.height:
        entry["issues"].append("not_square")
    if min(source_image.size) < 1024:
        entry["warnings"].append("below_1024_source")
    if source.suffix.lower() in {".jpg", ".jpeg"}:
        entry["notes"].append("lossy_service_source")

    detection = detect_watermark(source_image)
    entry["watermarkDetection"] = detection
    repaired, did_repair = repair_watermark_if_needed(source_image, detection if not args.no_watermark_repair else {"detected": False})
    entry["watermarkRepaired"] = did_repair
    entry["lowerRightCropPreview"] = save_lower_right_crop(source_image, spec, paths)

    cleaned_path = paths["cleaned"] / f"TX_B34_{spec.bid}_{spec.slug}_Cleaned_Source.jpg"
    cleaned_image = save_jpeg_target(repaired, cleaned_path, args.max_source_size, args.source_quality, args.source_max_mb * 1024 * 1024)
    entry["cleanedPath"] = project_rel(cleaned_path)
    entry["cleanedBytes"] = cleaned_path.stat().st_size

    primary = cleaned_image
    if spec.source_type == "SEAMLESS_TILE":
        metrics_before = seam_metrics(cleaned_image)
        periodic = make_periodic_candidate(cleaned_image, edge_pin=args.edge_pin)
        metrics_after = seam_metrics(periodic)
        before_score = metrics_before["bandLR"] + metrics_before["bandTB"]
        after_score = metrics_after["bandLR"] + metrics_after["bandTB"]
        if after_score < before_score:
            primary = periodic.convert("RGB")
            entry["seamRefined"] = True
        else:
            entry["seamRefined"] = False
        entry["seamBefore"] = metrics_before
        entry["seamAfter"] = seam_metrics(primary)
        if entry["seamAfter"]["bandLR"] > args.seam_band_review or entry["seamAfter"]["bandTB"] > args.seam_band_review:
            entry["warnings"].append("seam_band_review_required")
        entry["tilePreviewPath"] = save_tile_preview(primary, paths["tile_previews"] / f"B34_{spec.bid}_{spec.slug}_Tile2x2.png", args.tile_preview_size)
    elif spec.source_type in {"DECAL_ATLAS", "UV_ATLAS", "PICKUP_ATLAS"}:
        risk = foreground_edge_risk(cleaned_image)
        entry["atlasEdgeRisk"] = risk
        if risk["edgeIslandRisk"]:
            entry["warnings"].append("atlas_foreground_touches_edge_review")

    compressed_path = paths["compressed"] / f"TX_B34_{spec.bid}_{spec.slug}_BaseColorCandidate.jpg"
    compressed = save_jpeg_target(primary, compressed_path, args.max_source_size, args.base_quality, args.base_max_mb * 1024 * 1024)
    entry["baseColorCandidatePath"] = project_rel(compressed_path)
    entry["baseColorCandidateBytes"] = compressed_path.stat().st_size

    if spec.source_type in SOURCE_TYPES_WITH_PBR:
        entry["maps"] = derive_maps(compressed, spec, paths)

    entry["luminance"] = clipping_stats(compressed)
    if entry["luminance"]["blackPct"] > 3.0 or entry["luminance"]["whitePct"] > 3.0:
        entry["warnings"].append("clipping_review_required")
    if entry["luminance"]["lumMean"] < 35.0 and spec.family.startswith("terrain_photic"):
        entry["issues"].append("photic_material_too_dark")

    if entry["issues"]:
        entry["verdict"] = "REJECT_SOURCE"
    elif entry["warnings"]:
        entry["verdict"] = "REVIEW_REQUIRED"
    else:
        entry["verdict"] = "INTAKE_READY_STATIC"
    return entry


def build_source_audit(entries: list[dict]) -> dict[str, object]:
    candidates = discover_download_candidates()
    selected_paths = []
    duplicate_selected: list[str] = []
    seen: set[str] = set()
    for entry in entries:
        raw = entry.get("downloadSource")
        if not raw:
            continue
        key = normalized_path_key(Path(str(raw)))
        if key in seen:
            duplicate_selected.append(str(raw))
        seen.add(key)
        selected_paths.append(str(raw))

    selected_keys = {normalized_path_key(Path(raw)) for raw in selected_paths}
    selected_hashes: dict[str, str] = {}
    for raw in selected_paths:
        path = Path(raw)
        try:
            digest = file_sha256(path)
        except OSError:
            continue
        selected_hashes.setdefault(digest, str(path))

    unmatched: list[str] = []
    ignored_unmatched: list[dict[str, str]] = []
    for path in candidates:
        if normalized_path_key(path) in selected_keys:
            continue
        try:
            digest = file_sha256(path)
        except OSError:
            unmatched.append(str(path))
            continue
        selected_duplicate = selected_hashes.get(digest)
        if selected_duplicate:
            ignored_unmatched.append(
                {
                    "path": str(path),
                    "reason": "byte_identical_duplicate_selected_source",
                    "selectedPath": selected_duplicate,
                    "sha256": digest,
                }
            )
            continue
        unmatched.append(str(path))
    missing_job_ids = [str(entry["id"]) for entry in entries if not entry.get("downloadSource")]
    multiple_match_job_ids = [
        str(entry["id"])
        for entry in entries
        if len(entry.get("matchedDownloadSources", []) or []) > 1
    ]

    return {
        "downloadRoot": str(DOWNLOADS),
        "downloadCandidateGlob": BATCH34_DOWNLOAD_CANDIDATE_GLOB,
        "expectedJobCount": len(JOBS),
        "downloadCandidateCount": len(candidates),
        "selectedDownloadSourceCount": len(selected_paths),
        "uniqueSelectedDownloadSourceCount": len(selected_keys),
        "missingJobIds": missing_job_ids,
        "multipleMatchJobIds": multiple_match_job_ids,
        "duplicateSelectedDownloadSources": duplicate_selected,
        "unmatchedDownloadCandidates": unmatched,
        "ignoredDownloadCandidates": ignored_unmatched,
    }


def write_markdown(path: Path, entries: list[dict], manifest_path: Path, csv_path: Path) -> None:
    counts: dict[str, int] = {}
    for entry in entries:
        counts[str(entry["verdict"])] = counts.get(str(entry["verdict"]), 0) + 1
    lines = [
        "# Batch34 Texture Expansion Intake",
        "",
        "Evidence class: STATIC_IMAGE_PROCESSING.",
        "Unity was not run. No files were promoted to Assets/**.",
        "",
        f"Manifest: `{project_rel(manifest_path)}`",
        f"CSV: `{project_rel(csv_path)}`",
        f"Images expected: {len(JOBS)}",
        f"Images processed: {len(entries)}",
        f"INTAKE_READY_STATIC: {counts.get('INTAKE_READY_STATIC', 0)}",
        f"REVIEW_REQUIRED: {counts.get('REVIEW_REQUIRED', 0)}",
        f"REJECT_SOURCE: {counts.get('REJECT_SOURCE', 0)}",
        f"REJECT_SOURCE_MISSING: {counts.get('REJECT_SOURCE_MISSING', 0)}",
        "",
        "## Integration Boundary",
        "",
        "These files are source candidates. Production use still requires Unity import settings, material manifests, map/channel review, renderer preview, and route screenshots.",
        "",
        "## Findings",
        "",
        "| ID | Verdict | Type | Family | Size | Base candidate | Tile preview | Issues | Warnings | Notes |",
        "|---|---|---|---|---:|---|---|---|---|---|",
    ]
    for entry in entries:
        issues = ";".join(entry.get("issues", []))
        warnings = ";".join(entry.get("warnings", []))
        notes = ";".join(entry.get("notes", []))
        size = f"{entry.get('sourceWidth', '?')}x{entry.get('sourceHeight', '?')}"
        lines.append(
            f"| {entry['id']} | {entry['verdict']} | {entry['sourceType']} | {entry['family']} | {size} | "
            f"`{entry.get('baseColorCandidatePath', '')}` | `{entry.get('tilePreviewPath', '')}` | "
            f"{issues} | {warnings} | {notes} |"
        )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def run(args: argparse.Namespace) -> int:
    paths = ensure_dirs()
    entries = [process_one(spec, paths, args) for spec in JOBS]
    source_audit = build_source_audit(entries)

    manifest = {
        "schema": "hecton8.batch34.texture_expansion_intake.v1",
        "date": "2026-06-08",
        "outputRoot": project_rel(OUTPUT_ROOT),
        "sourcePromptPack": "Docs/GeneratedAssets/Gemini/Prompts/Batch34/3401_TEXTURE_SOURCE_EXPANSION_PROMPT_PACK_20260608.md",
        "serviceAgentInstructions": "Docs/GeneratedAssets/Gemini/Prompts/Batch34/3402_TEXTURE_SERVICE_AGENT_INSTRUCTIONS_20260608.md",
        "unityImportStatus": "PENDING UNITY IMPORT",
        "visualStatus": "PENDING VISUAL REVIEW",
        "mapPolicy": {
            "BaseColorCandidate": "sRGB source candidate, no Unity import proof",
            "NormalGL": "provisional offline derivation for SEAMLESS_TILE/TRIM_SHEET only",
            "MRAO_Provisional": "RGBA = Metallic, Roughness, AO, Emission/wetness placeholder; shader contract review required",
        },
        "sourceAudit": source_audit,
        "entries": entries,
    }
    manifest_path = paths["qa"] / "Batch34_TextureExpansion_IntakeManifest.json"
    csv_path = paths["qa"] / "Batch34_TextureExpansion_Intake.csv"
    summary_path = paths["qa"] / "Batch34_TextureExpansion_Intake.md"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    write_csv(csv_path, entries)
    write_markdown(summary_path, entries, manifest_path, csv_path)

    contact_sheet(entries, "baseColorCandidatePath", paths["contact"] / "Batch34_BaseColorCandidates_Contact.png")
    contact_sheet([entry for entry in entries if entry.get("tilePreviewPath")], "tilePreviewPath", paths["contact"] / "Batch34_SeamlessTile2x2_Contact.png", thumb=220)
    contact_sheet(entries, "lowerRightCropPreview", paths["contact"] / "Batch34_LowerRightWatermarkReview_Contact.png", thumb=180)

    ready = sum(1 for entry in entries if entry["verdict"] == "INTAKE_READY_STATIC")
    review = sum(1 for entry in entries if entry["verdict"] == "REVIEW_REQUIRED")
    reject = sum(1 for entry in entries if str(entry["verdict"]).startswith("REJECT"))
    print("BATCH34_TEXTURE_EXPANSION_INTAKE_DONE")
    print(f"processed={len(entries)} ready_static={ready} review={review} reject={reject}")
    print(f"manifest={project_rel(manifest_path)}")
    print(f"summary={project_rel(summary_path)}")
    return 1 if reject else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-quality", type=int, default=90)
    parser.add_argument("--base-quality", type=int, default=88)
    parser.add_argument("--source-max-mb", type=float, default=1.5)
    parser.add_argument("--base-max-mb", type=float, default=1.1)
    parser.add_argument("--max-source-size", type=int, default=2048)
    parser.add_argument("--tile-preview-size", type=int, default=512)
    parser.add_argument("--seam-band-review", type=float, default=22.0)
    parser.add_argument("--edge-pin", action="store_true")
    parser.add_argument("--no-watermark-repair", action="store_true")
    return run(parser.parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
