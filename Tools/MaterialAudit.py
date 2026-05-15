#!/usr/bin/env python3
"""Offline HECTON-8 material audit.

Checks imported texture names, material texture slots, ORM/detail-map usage, and
albedo luminance for PBR energy-conservation violations.
"""

from __future__ import annotations

import argparse
import csv
import json
import math
import os
import re
from pathlib import Path
from typing import Any

try:
    from PIL import Image
except ImportError as exc:  # pragma: no cover - environment guard
    raise SystemExit("Pillow is required: python -m pip install pillow") from exc


IMAGE_EXTS = {".png", ".tga", ".jpg", ".jpeg", ".tif", ".tiff", ".exr", ".psd"}
MATERIAL_EXTS = {".mat"}
FIRST_PARTY_ASSET_DIRS = {"_Project"}
DEFAULT_EXCLUDED_DIRS = {
    ".git",
    "Adaptive Performance",
    "AmplifyImpostors",
    "AstarPathfindingProject",
    "Bakery",
    "Candice AI for Games",
    "Crest",
    "Dynamic Decals",
    "Eazy Sound Manager",
    "Feel",
    "GPUInstancer",
    "Graphy - Ultimate Stats Monitor",
    "MapMagic",
    "MeshBaker",
    "Packages",
    "Plugins",
    "RealtimeCSG",
    "Shapes",
    "Technie",
    "TextMesh Pro",
    "VolumetricFogBundle",
    "VolumetricLightBeam",
    "_Archive",
    "_Recovery",
    "_ThirdParty",
}
DETAIL_TOKENS = (
    "detail",
    "scratch",
    "scratches",
    "dust",
    "grime",
    "carbon",
    "fiber",
    "fibre",
    "wear",
    "worn",
    "edge",
    "stain",
    "rust",
    "oxid",
    "dent",
    "pitted",
    "noise",
    "micro",
)
ALBEDO_EXCLUDE_TOKENS = (
    "normal",
    "norm",
    "nrm",
    "bump",
    "height",
    "rough",
    "metal",
    "ao",
    "orm",
    "mask",
    "spec",
    "smooth",
    "emiss",
    "emission",
    "cloud",
    "lut",
    "noise",
    "blue",
)
NORMAL_TOKENS = ("normal", "norm", "nrm", "bump")
NON_SURFACE_PATH_PARTS = ("/sprites/ui/", "/skyboxes/")
NON_SURFACE_MATERIAL_NAME_TOKENS = {
    "celestial",
    "hud",
    "moon",
    "ui",
    "gasgiant",
    "skybox",
    "terrain",
}
GENERATED_LIGHTING_TEXTURE_PREFIXES = ("reflectionprobe", "lightmap", "lightingdata")
BASE_MAP_PROPS = {"_BaseMap", "_MainTex", "_BaseColorMap"}
NORMAL_MAP_PROPS = {"_BumpMap", "_NormalMap"}
PROMPT_ORM_PROPS = {"_ORMMap", "_OrmMap", "_OcclusionRoughnessMetallicMap"}
LEGACY_PACKED_MASK_PROPS = {"_MaskMap", "_MetallicGlossMap", "_SpecGlossMap"}
SEPARATE_OCCLUSION_PROPS = {"_OcclusionMap", "_AOMap", "_AmbientOcclusionMap"}
SEPARATE_ROUGHNESS_PROPS = {"_RoughnessMap", "_SmoothnessMap", "_GlossMap", "_SpecGlossMap"}
SEPARATE_METALLIC_PROPS = {"_MetallicMap", "_MetallicGlossMap"}
DETAIL_MAP_PROPS = {"_DetailAlbedoMap", "_DetailNormalMap", "_DetailMask"}
IGNORED_TEXTURE_PROP_PREFIXES = ("unity_",)
STANDARD_MATERIAL_MIB = 6.65
OPTIMIZED_MATERIAL_MIB = 2.99
TEXTURE_BUDGET_MIB = 900.0
TEXTURE_BUDGET_WARNING_RATIO = 0.90
GATE_EXIT_CODES = {
    "energy_failures": 1,
    "import_issues": 2,
    "material_issues": 3,
    "unresolved_texture_refs": 4,
    "texture_budget": 5,
    "albedo_read_errors": 6,
    "energy_warnings": 7,
    "channel_packing_candidates": 8,
    "detail_map_missing": 9,
    "surface_unresolved_texture_refs": 10,
}
CI_SURFACE_GATE_PROFILE = (
    "energy_warnings",
    "albedo_read_errors",
    "texture_budget",
)
GOD_MODE_TEXTURE_OVERRIDES = [
    {
        "asset_class": "Hero cockpit albedo",
        "toaster_max": 1024,
        "deck_max": 2048,
        "pro_max": 2048,
        "god_mode_max": 4096,
        "format": "BC7 sRGB",
        "fallback": "Demote one mip tier when VRAM used/total > 0.90.",
    },
    {
        "asset_class": "Hero cockpit normal",
        "toaster_max": 1024,
        "deck_max": 2048,
        "pro_max": 2048,
        "god_mode_max": 4096,
        "format": "BC5 linear",
        "fallback": "Prefer shared detail normal before unique 4K normal.",
    },
    {
        "asset_class": "Hero cockpit ORM",
        "toaster_max": 512,
        "deck_max": 1024,
        "pro_max": 1024,
        "god_mode_max": 2048,
        "format": "BC7/BC3 linear",
        "fallback": "Keep ORM below albedo unless mask aliasing is visible.",
    },
    {
        "asset_class": "World module albedo",
        "toaster_max": 1024,
        "deck_max": 1024,
        "pro_max": 2048,
        "god_mode_max": 2048,
        "format": "BC7 sRGB",
        "fallback": "Do not promote all panels; reserve for inspection-radius sets.",
    },
    {
        "asset_class": "World module normal",
        "toaster_max": 1024,
        "deck_max": 1024,
        "pro_max": 2048,
        "god_mode_max": 2048,
        "format": "BC5 linear",
        "fallback": "Shared trimsheet normal before unique resolution increase.",
    },
    {
        "asset_class": "Terrain albedo",
        "toaster_max": 1024,
        "deck_max": 2048,
        "pro_max": 2048,
        "god_mode_max": 4096,
        "format": "BC7/BC1 sRGB",
        "fallback": "Near hero terrain only; macro terrain stays 2048 tiled.",
    },
    {
        "asset_class": "Terrain ORM",
        "toaster_max": 512,
        "deck_max": 1024,
        "pro_max": 1024,
        "god_mode_max": 2048,
        "format": "BC7/BC3 linear",
        "fallback": "Shared packed masks; no separate AO/roughness/metallic.",
    },
    {
        "asset_class": "Flora albedo atlas",
        "toaster_max": 1024,
        "deck_max": 1024,
        "pro_max": 2048,
        "god_mode_max": 2048,
        "format": "BC7 sRGB",
        "fallback": "Wire detail overlays before increasing atlas size.",
    },
    {
        "asset_class": "Flora detail atlas",
        "toaster_max": 512,
        "deck_max": 512,
        "pro_max": 1024,
        "god_mode_max": 1024,
        "format": "BC4/BC5 linear",
        "fallback": "Global tiling; no per-family duplication above 1024.",
    },
    {
        "asset_class": "Decal sheet",
        "toaster_max": 512,
        "deck_max": 1024,
        "pro_max": 1024,
        "god_mode_max": 1024,
        "format": "BC7/BC3",
        "fallback": "Damage and wear decals outrank raw base-map resolution.",
    },
    {
        "asset_class": "Brush/scratch globals",
        "toaster_max": 512,
        "deck_max": 1024,
        "pro_max": 1024,
        "god_mode_max": 1024,
        "format": "BC4/BC5 linear",
        "fallback": "Shared globally across cockpit, habitat, and vehicle materials.",
    },
    {
        "asset_class": "Diegetic UI atlas",
        "toaster_max": 1024,
        "deck_max": 1024,
        "pro_max": 2048,
        "god_mode_max": 2048,
        "format": "BC7 sRGB",
        "fallback": "Close-read UI only; regular UI is outside world PBR budget.",
    },
]
GLOBAL_DETAIL_OVERLAY_PLAN = [
    {
        "overlay_role": "fine_cockpit_scratches",
        "source_status": "MISSING_AUTHORING",
        "target_surfaces": "Cockpit glass, painted metal, polished hand-contact panels",
        "toaster_rule": "Disabled except inspection props.",
        "god_mode_rule": "BC4/BC5 1024 overlay at 8x-16x tiling.",
        "expected_detail_gain_percent": 20,
    },
    {
        "overlay_role": "panel_dust_grit",
        "source_status": "MISSING_AUTHORING",
        "target_surfaces": "Habitat panels, wall seams, low-traffic module floors",
        "toaster_rule": "Use baked albedo dirt only.",
        "god_mode_rule": "BC4 1024 mask blended into roughness and albedo breakup.",
        "expected_detail_gain_percent": 20,
    },
    {
        "overlay_role": "carbon_fiber_weave",
        "source_status": "MISSING_AUTHORING",
        "target_surfaces": "Tool grips, suit hardpoints, high-end cockpit inserts",
        "toaster_rule": "Normal overlay disabled.",
        "god_mode_rule": "BC5 1024 tangent-aligned weave normal.",
        "expected_detail_gain_percent": 25,
    },
    {
        "overlay_role": "worn_rubber",
        "source_status": "MISSING_AUTHORING",
        "target_surfaces": "Gaskets, grips, boot contact zones, black utility trim",
        "toaster_rule": "Use scalar roughness only.",
        "god_mode_rule": "BC4 1024 pitted roughness detail plus low-strength normal.",
        "expected_detail_gain_percent": 20,
    },
    {
        "overlay_role": "brushed_steel_streaks",
        "source_status": "MISSING_AUTHORING",
        "target_surfaces": "Cockpit frames, rails, latches, exposed machined metal",
        "toaster_rule": "Use anisotropic fake without detail sample.",
        "god_mode_rule": "BC4 1024 directional streak mask for anisotropic fake.",
        "expected_detail_gain_percent": 25,
    },
    {
        "overlay_role": "oxidized_aluminum_pitting",
        "source_status": "MISSING_AUTHORING",
        "target_surfaces": "Exterior module shells, old brackets, pressure fittings",
        "toaster_rule": "Baked AO/roughness only.",
        "god_mode_rule": "BC4 1024 pitting mask into roughness and edge darkening.",
        "expected_detail_gain_percent": 20,
    },
    {
        "overlay_role": "salt_deposit_speckle",
        "source_status": "MISSING_AUTHORING",
        "target_surfaces": "Wet glass edges, flooded doors, submarine exterior seams",
        "toaster_rule": "Use static decal or base-map stain.",
        "god_mode_rule": "BC4 1024 speckle mask blended by wetness depth.",
        "expected_detail_gain_percent": 20,
    },
    {
        "overlay_role": "grease_hand_smudges",
        "source_status": "MISSING_AUTHORING",
        "target_surfaces": "Switch panels, handles, lockers, tool drawers",
        "toaster_rule": "Use low-frequency decal only.",
        "god_mode_rule": "BC4 1024 roughness-darkening overlay in interaction zones.",
        "expected_detail_gain_percent": 20,
    },
    {
        "overlay_role": "edge_chipped_paint",
        "source_status": "MISSING_AUTHORING",
        "target_surfaces": "Painted industrial panels, doors, crates, railings",
        "toaster_rule": "Vertex color or baked mask only.",
        "god_mode_rule": "BC4 1024 edge wear mask multiplied by curvature/AO author data.",
        "expected_detail_gain_percent": 20,
    },
    {
        "overlay_role": "condensation_micro_droplets",
        "source_status": "MISSING_AUTHORING",
        "target_surfaces": "Cold glass, wet acrylic, instrument covers, exterior windows",
        "toaster_rule": "Disabled; rely on fake clearcoat.",
        "god_mode_rule": "BC5 1024 tiny droplet normal at 0.05-0.12 strength.",
        "expected_detail_gain_percent": 25,
    },
]

TEXTURE_PROPERTY_RE = re.compile(r"^\s*-\s+([A-Za-z0-9_]+):\s*$")
GUID_RE = re.compile(r"guid:\s*([0-9a-fA-F]{32})")
HEX_GUID_RE = re.compile(r"^[0-9a-fA-F]{32}$")


def normalized(path: Path) -> str:
    return path.as_posix()


def contains_any(text: str, tokens: tuple[str, ...]) -> bool:
    return any(token in text for token in tokens)


def tokenize_name(text: str) -> list[str]:
    return re.findall(r"[a-z0-9]+", text.lower())


def is_surface_excluded_path(path: Path) -> bool:
    lowered = "/" + path.as_posix().lower().replace("\\", "/")
    return any(part in lowered for part in NON_SURFACE_PATH_PARTS)


def is_generated_lighting_texture(path: Path) -> bool:
    lowered = "/" + path.as_posix().lower().replace("\\", "/")
    if "/scenes/" not in lowered or path.suffix.lower() not in {".exr", ".hdr"}:
        return False
    stem = path.stem.lower()
    return any(stem.startswith(prefix) for prefix in GENERATED_LIGHTING_TEXTURE_PREFIXES)


def is_surface_material_candidate(path: str, props: dict[str, str]) -> bool:
    material_terms = set(tokenize_name(path))
    if material_terms.intersection(NON_SURFACE_MATERIAL_NAME_TOKENS):
        return False

    base_paths = property_paths(props, BASE_MAP_PROPS)
    if not base_paths:
        return False

    for base_path in base_paths:
        lowered = base_path.lower().replace("\\", "/")
        if lowered.endswith(".rendertexture"):
            return False
        if "/sprites/ui/" in lowered or "/skyboxes/" in lowered:
            return False
    return True


def has_orm_token(terms: list[str]) -> bool:
    for term in terms:
        if term == "orm" or term.startswith("orm"):
            return True
        if term in {"mask", "packed", "ao", "occlusion", "rough", "roughness"}:
            return True
        if term in {"metal", "metallic", "smooth", "smoothness", "spec", "specular"}:
            return True
    return False


def has_detail_token(terms: list[str]) -> bool:
    return any(term in DETAIL_TOKENS for term in terms)


def has_albedo_token(terms: list[str]) -> bool:
    return any(term in {"albedo", "basecolor", "diffuse", "diff", "color", "colour"} for term in terms)


def classify_texture(path: Path) -> dict[str, bool]:
    name = path.stem.lower()
    terms = tokenize_name(name)
    is_surface_excluded = is_surface_excluded_path(path)
    is_normal = contains_any(name, NORMAL_TOKENS)
    is_orm_candidate = has_orm_token(terms) and not is_normal and not is_surface_excluded
    is_detail_candidate = has_detail_token(terms) and not is_surface_excluded
    is_albedo_candidate = (
        has_albedo_token(terms)
        and not contains_any(name, ALBEDO_EXCLUDE_TOKENS)
        and not is_surface_excluded
    )
    return {
        "is_albedo_candidate": is_albedo_candidate,
        "is_detail_candidate": is_detail_candidate,
        "is_normal": is_normal,
        "is_orm_candidate": is_orm_candidate,
    }


def srgb_to_linear(channel: float) -> float:
    if channel <= 0.04045:
        return channel / 12.92
    return math.pow((channel + 0.055) / 1.055, 2.4)


def read_meta(path: Path) -> dict[str, str]:
    meta_path = Path(str(path) + ".meta")
    result: dict[str, str] = {}
    if not meta_path.exists():
        return result

    wanted = {
        "aniso",
        "enableMipMap",
        "guid",
        "isReadable",
        "maxTextureSize",
        "sRGBTexture",
        "streamingMipmaps",
        "textureCompression",
        "textureFormat",
        "textureType",
    }
    current_platform = ""
    try:
        with meta_path.open("r", encoding="utf-8", errors="ignore") as handle:
            for line in handle:
                stripped = line.strip()
                if ":" not in stripped:
                    continue
                key, value = stripped.split(":", 1)
                key = key.strip()
                value = value.strip()
                if key == "buildTarget":
                    current_platform = value
                    continue
                if key in wanted:
                    if key not in result:
                        result[key] = value
                    if current_platform:
                        result[f"{current_platform}.{key}"] = value
    except OSError:
        return result
    return result


def meta_value(meta: dict[str, str], key: str) -> str:
    standalone_key = f"Standalone.{key}"
    if standalone_key in meta:
        return meta[standalone_key]
    return meta.get(key, "")


def estimate_texture_mib(width: int, height: int, bits_per_pixel: int = 8, include_mips: bool = True) -> float:
    pixel_count = max(1, width) * max(1, height)
    byte_count = pixel_count * max(1, bits_per_pixel) / 8.0
    if include_mips:
        byte_count *= 4.0 / 3.0
    return round(byte_count / (1024.0 * 1024.0), 3)


def texture_memory_role(record: dict[str, Any]) -> str:
    if record.get("is_normal"):
        return "BC5_NORMAL_8BPP"
    if record.get("is_albedo_candidate"):
        return "BC7_ALBEDO_8BPP"
    if record.get("is_orm_candidate"):
        return "BC7_ORM_LINEAR_8BPP"
    if record.get("is_detail_candidate"):
        return "BC4_BC5_DETAIL_8BPP"
    return "BC7_UNKNOWN_8BPP"


def append_texture_memory_estimate(record: dict[str, Any], width: int, height: int) -> None:
    enable_mip = record.get("meta", {}).get("enableMipMap", "")
    include_mips = enable_mip != "0"
    record["width"] = width
    record["height"] = height
    record["memory_role"] = texture_memory_role(record)
    record["estimated_resident_mib"] = estimate_texture_mib(width, height, 8, include_mips)


def append_texture_import_issues(record: dict[str, Any]) -> None:
    meta = record.get("meta", {})
    issues: list[str] = []
    if not meta:
        record["import_issues"] = ["MISSING_META"]
        return

    srgb = meta.get("sRGBTexture", "")
    enable_mip = meta.get("enableMipMap", "")
    texture_type = meta.get("textureType", "")
    compression = meta_value(meta, "textureCompression")
    is_readable = meta.get("isReadable", "")

    if is_readable == "1":
        issues.append("READ_WRITE_ENABLED")
    if compression == "0":
        issues.append("UNCOMPRESSED_TEXTURE")

    if record.get("is_albedo_candidate"):
        if srgb != "1":
            issues.append("ALBEDO_SRGB_OFF")
        if enable_mip != "1":
            issues.append("ALBEDO_MIPS_OFF")
    if record.get("is_normal"):
        if srgb != "0":
            issues.append("NORMAL_SRGB_ON")
        if texture_type != "1":
            issues.append("NORMAL_NOT_TEXTURETYPE_NORMAL")
        if enable_mip != "1":
            issues.append("NORMAL_MIPS_OFF")
    if record.get("is_orm_candidate") or record.get("is_detail_candidate"):
        if srgb != "0":
            issues.append("DATA_TEXTURE_SRGB_ON")
        if enable_mip != "1":
            issues.append("DATA_TEXTURE_MIPS_OFF")

    record["import_issues"] = issues


def recommend_texture_fix(issue: str) -> str:
    if issue == "MISSING_META":
        return "Regenerate or restore Unity .meta before import enforcement."
    if issue == "READ_WRITE_ENABLED":
        return "Disable Read/Write unless CPU readback is explicitly required."
    if issue == "UNCOMPRESSED_TEXTURE":
        return "Enable platform compression; use BC7/BC5/BC4 class formats by texture role."
    if issue == "ALBEDO_SRGB_OFF":
        return "Enable sRGB for albedo/base-color maps."
    if issue == "ALBEDO_MIPS_OFF":
        return "Enable mipmaps for world albedo."
    if issue == "NORMAL_SRGB_ON":
        return "Disable sRGB for normal maps."
    if issue == "NORMAL_NOT_TEXTURETYPE_NORMAL":
        return "Set Texture Type to Normal Map."
    if issue == "NORMAL_MIPS_OFF":
        return "Enable mipmaps for world normals."
    if issue == "DATA_TEXTURE_SRGB_ON":
        return "Disable sRGB; data/mask/detail maps must be sampled linear."
    if issue == "DATA_TEXTURE_MIPS_OFF":
        return "Enable mipmaps unless this is a UI-only or non-tiled data texture."
    return "Manual technical-art review required."


def recommend_material_fix(issue: str) -> str:
    if issue == "UNRESOLVED_TEXTURE_GUID":
        return "Resolve the texture GUID inside first-party assets or quarantine the external dependency."
    if issue == "NO_PROMPT_ORM_SLOT":
        return "Add prompt ORM slot using R=AO, G=Roughness, B=Metallic after shader convention is resolved."
    if issue == "NO_PACKED_ORM_OR_MASK_SLOT":
        return "Pack AO/Roughness/Metallic into one ORM map after shader convention is resolved."
    if issue == "LEGACY_MASK_SLOT_REQUIRES_CHANNEL_REVIEW":
        return "Review legacy mask/gloss channel order before treating it as prompt ORM."
    if issue == "SEPARATE_OCCLUSION_AND_METALLIC_MAPS":
        return "Collapse separate occlusion and metallic maps into packed ORM/mask data."
    if issue == "NO_DETAIL_MAP_SLOT":
        return "Wire shared detail albedo/normal or explicitly mark material as too distant/low-tier."
    return "Manual material review required."


def inspect_image(path: Path, root: Path, sample_size: int) -> dict[str, Any]:
    record: dict[str, Any] = {
        "path": normalized(path.relative_to(root)),
        "extension": path.suffix.lower(),
        "meta": read_meta(path),
    }
    record.update(classify_texture(path))
    append_texture_import_issues(record)

    try:
        with Image.open(path) as image:
            record["mode"] = image.mode
            append_texture_memory_estimate(record, image.width, image.height)
            if not record["is_albedo_candidate"]:
                return record
            image.draft("RGB", (sample_size, sample_size))
            sample = image.convert("RGB")
            sample.thumbnail((sample_size, sample_size))
            pixels = list(sample.getdata())
    except Exception as exc:  # noqa: BLE001 - audit must continue
        record["read_error"] = str(exc)
        return record

    if not pixels:
        record["energy_status"] = "ERROR_EMPTY_IMAGE"
        return record

    srgb_luma: list[float] = []
    linear_luma_sum = 0.0
    bright_pixels = 0
    for red, green, blue in pixels:
        r = red / 255.0
        g = green / 255.0
        b = blue / 255.0
        luma = (0.2126 * r) + (0.7152 * g) + (0.0722 * b)
        srgb_luma.append(luma)
        if luma > 0.90:
            bright_pixels += 1
        linear_luma_sum += (
            (0.2126 * srgb_to_linear(r))
            + (0.7152 * srgb_to_linear(g))
            + (0.0722 * srgb_to_linear(b))
        )

    srgb_luma.sort()
    count = len(srgb_luma)
    p95_index = min(count - 1, int(count * 0.95))
    mean_srgb = sum(srgb_luma) / count
    mean_linear = linear_luma_sum / count
    p95_srgb = srgb_luma[p95_index]
    bright_ratio = bright_pixels / count

    status = "PASS"
    reason = "within albedo luminance budget"
    if mean_srgb > 0.75 or mean_linear > 0.60:
        status = "FAIL"
        reason = "mean albedo too bright for energy conservation"
    elif p95_srgb > 0.92 and bright_ratio > 0.10:
        status = "WARN"
        reason = "large bright albedo area risks baked-light/spec blowout"

    record["energy_status"] = status
    record["energy_reason"] = reason
    record["mean_srgb_luma"] = round(mean_srgb, 5)
    record["mean_linear_luma"] = round(mean_linear, 5)
    record["p95_srgb_luma"] = round(p95_srgb, 5)
    record["bright_pixel_ratio"] = round(bright_ratio, 5)
    return record


def prune_dirs(root: Path, current: Path, dirnames: list[str], include_third_party: bool) -> None:
    if include_third_party:
        dirnames[:] = [name for name in dirnames if name not in {".git", "__pycache__"}]
        return

    if current == root and root.name == "Assets":
        dirnames[:] = [name for name in dirnames if name in FIRST_PARTY_ASSET_DIRS]
        return

    dirnames[:] = [name for name in dirnames if name not in DEFAULT_EXCLUDED_DIRS]


def build_guid_map(root: Path, needed_guids: set[str], include_third_party: bool) -> dict[str, str]:
    guid_map: dict[str, str] = {}
    if not needed_guids:
        return guid_map

    for dirpath, dirnames, filenames in os.walk(root):
        prune_dirs(root, Path(dirpath), dirnames, include_third_party)
        for filename in filenames:
            if not filename.endswith(".meta"):
                continue
            meta_path = Path(dirpath) / filename
            try:
                with meta_path.open("r", encoding="utf-8", errors="ignore") as handle:
                    first_lines = [next(handle, "") for _ in range(8)]
            except OSError:
                continue
            for line in first_lines:
                if line.startswith("guid:"):
                    guid = line.split(":", 1)[1].strip()
                    if guid not in needed_guids:
                        break
                    asset_path = meta_path.with_suffix("")
                    guid_map[guid] = normalized(asset_path.relative_to(root))
                    break
    return guid_map


def parse_material(path: Path, root: Path) -> dict[str, Any]:
    props: dict[str, str] = {}
    current_prop = ""
    try:
        lines = path.read_text(encoding="utf-8", errors="ignore").splitlines()
    except OSError as exc:
        return {"path": normalized(path.relative_to(root)), "read_error": str(exc)}

    for line in lines:
        prop_match = TEXTURE_PROPERTY_RE.match(line)
        if prop_match:
            current_prop = prop_match.group(1)
            continue
        if not current_prop:
            continue
        guid_match = GUID_RE.search(line)
        if guid_match:
            guid = guid_match.group(1)
            if guid != "00000000000000000000000000000000":
                props[current_prop] = guid
            current_prop = ""

    return {
        "path": normalized(path.relative_to(root)),
        "texture_properties": props,
    }


def property_paths(props: dict[str, str], names: set[str]) -> list[str]:
    return [f"{name}:{props[name]}" for name in sorted(names.intersection(props.keys()))]


def unresolved_texture_refs(props: dict[str, str]) -> list[str]:
    unresolved: list[str] = []
    for prop, value in sorted(props.items()):
        if prop.startswith(IGNORED_TEXTURE_PROP_PREFIXES):
            continue
        if HEX_GUID_RE.fullmatch(value):
            unresolved.append(f"{prop}:{value}")
    return unresolved


def unresolved_slot_summary(unresolved_refs: list[str]) -> dict[str, Any]:
    """Group unresolved material refs by the shader slot class they block."""
    base_refs: list[str] = []
    normal_refs: list[str] = []
    data_refs: list[str] = []
    detail_refs: list[str] = []
    other_refs: list[str] = []

    data_props = PROMPT_ORM_PROPS.union(
        LEGACY_PACKED_MASK_PROPS,
        SEPARATE_OCCLUSION_PROPS,
        SEPARATE_ROUGHNESS_PROPS,
        SEPARATE_METALLIC_PROPS,
    )

    for ref in unresolved_refs:
        prop, _, _ = ref.partition(":")
        if prop in BASE_MAP_PROPS:
            base_refs.append(ref)
        elif prop in NORMAL_MAP_PROPS:
            normal_refs.append(ref)
        elif prop in data_props:
            data_refs.append(ref)
        elif prop in DETAIL_MAP_PROPS:
            detail_refs.append(ref)
        else:
            other_refs.append(ref)

    severity = "NONE"
    recommendation = "No unresolved material texture refs."
    if base_refs or normal_refs:
        severity = "BLOCKER"
        recommendation = "Restore source base/normal textures or clear invalid slots before ORM/detail migration."
    elif data_refs:
        severity = "HIGH"
        recommendation = "Restore mask data or author prompt ORM before channel-packing migration."
    elif detail_refs:
        severity = "MEDIUM"
        recommendation = "Restore detail texture or assign a shared global detail overlay."
    elif other_refs:
        severity = "LOW"
        recommendation = "Verify the slot owner; remove stale refs if this is not a surface texture."

    return {
        "severity": severity,
        "base_color_refs": base_refs,
        "normal_refs": normal_refs,
        "data_refs": data_refs,
        "detail_refs": detail_refs,
        "other_refs": other_refs,
        "recommendation": recommendation,
    }


def build_channel_packing_candidate(
    path: str,
    props: dict[str, str],
    prop_names: set[str],
    has_base: bool,
    has_prompt_orm: bool,
    has_detail: bool,
    is_surface_material: bool,
) -> dict[str, Any] | None:
    if not is_surface_material or not has_base or has_prompt_orm:
        return None

    occlusion = property_paths(props, SEPARATE_OCCLUSION_PROPS)
    roughness = property_paths(props, SEPARATE_ROUGHNESS_PROPS)
    metallic = property_paths(props, SEPARATE_METALLIC_PROPS)
    legacy = property_paths(props, LEGACY_PACKED_MASK_PROPS)

    if occlusion and (roughness or metallic):
        priority = "HIGH"
        reason = "Separate AO plus metallic/roughness data can collapse into prompt ORM."
    elif legacy:
        priority = "MEDIUM"
        reason = "Legacy packed/gloss slot exists, but prompt ORM slot is absent."
    else:
        priority = "LOW"
        reason = "Base material has no prompt ORM slot; author or reuse ORM if the material is near-field."

    return {
        "path": path,
        "priority": priority,
        "reason": reason,
        "base_maps": property_paths(props, BASE_MAP_PROPS),
        "normal_maps": property_paths(props, NORMAL_MAP_PROPS),
        "occlusion_sources": occlusion,
        "roughness_sources": roughness,
        "metallic_sources": metallic,
        "legacy_mask_sources": legacy,
        "detail_sources": property_paths(props, DETAIL_MAP_PROPS),
        "has_detail": has_detail,
    }


def resolve_material(raw: dict[str, Any], guid_map: dict[str, str]) -> dict[str, Any]:
    props = {
        prop: guid_map.get(guid_or_path, guid_or_path)
        for prop, guid_or_path in raw.get("texture_properties", {}).items()
    }
    prop_names = set(props.keys())
    has_base = bool(prop_names.intersection(BASE_MAP_PROPS))
    has_prompt_orm = bool(prop_names.intersection(PROMPT_ORM_PROPS))
    has_legacy_packed_mask = bool(prop_names.intersection(LEGACY_PACKED_MASK_PROPS))
    has_packed = has_prompt_orm or has_legacy_packed_mask
    has_separate_occlusion = bool(prop_names.intersection(SEPARATE_OCCLUSION_PROPS))
    has_separate_metallic = bool(prop_names.intersection(SEPARATE_METALLIC_PROPS))
    has_detail = bool(prop_names.intersection(DETAIL_MAP_PROPS))
    has_normal = bool(prop_names.intersection(NORMAL_MAP_PROPS))
    path = raw["path"]
    is_surface_material = is_surface_material_candidate(path, props)

    issues: list[str] = []
    unresolved_refs = unresolved_texture_refs(props)
    unresolved_summary = unresolved_slot_summary(unresolved_refs)
    if unresolved_refs:
        issues.append("UNRESOLVED_TEXTURE_GUID")
    if is_surface_material and has_base and not has_prompt_orm:
        issues.append("NO_PROMPT_ORM_SLOT")
    if is_surface_material and has_base and not has_packed:
        issues.append("NO_PACKED_ORM_OR_MASK_SLOT")
    if is_surface_material and has_legacy_packed_mask and not has_prompt_orm:
        issues.append("LEGACY_MASK_SLOT_REQUIRES_CHANNEL_REVIEW")
    if is_surface_material and has_separate_occlusion and has_separate_metallic:
        issues.append("SEPARATE_OCCLUSION_AND_METALLIC_MAPS")
    if is_surface_material and has_base and not has_detail:
        issues.append("NO_DETAIL_MAP_SLOT")

    channel_candidate = build_channel_packing_candidate(
        path,
        props,
        prop_names,
        has_base,
        has_prompt_orm,
        has_detail,
        is_surface_material,
    )

    return {
        "path": path,
        "texture_properties": props,
        "has_base_map": has_base,
        "has_normal": has_normal,
        "has_prompt_orm": has_prompt_orm,
        "has_legacy_packed_mask": has_legacy_packed_mask,
        "has_packed_mask": has_packed,
        "has_detail": has_detail,
        "is_surface_material_candidate": is_surface_material,
        "unresolved_texture_refs": unresolved_refs,
        "unresolved_texture_ref_summary": unresolved_summary,
        "channel_packing_candidate": channel_candidate,
        "issues": issues,
    }


def summarize_textures(textures: list[dict[str, Any]]) -> dict[str, Any]:
    albedo = [item for item in textures if item.get("is_albedo_candidate")]
    detail = [item for item in textures if item.get("is_detail_candidate")]
    orm = [item for item in textures if item.get("is_orm_candidate")]
    normals = [item for item in textures if item.get("is_normal")]
    energy_fail = [item for item in albedo if item.get("energy_status") == "FAIL"]
    energy_warn = [item for item in albedo if item.get("energy_status") == "WARN"]
    read_error_textures = [item for item in textures if item.get("read_error")]
    albedo_read_errors = [item for item in albedo if item.get("read_error")]
    import_issue_textures = [item for item in textures if item.get("import_issues")]
    import_issue_counts: dict[str, int] = {}
    for item in import_issue_textures:
        for issue in item.get("import_issues", []):
            import_issue_counts[issue] = import_issue_counts.get(issue, 0) + 1
    estimated_texture_mib = round(
        sum(float(item.get("estimated_resident_mib", 0.0)) for item in textures),
        3,
    )
    largest_estimated = sorted(
        [item for item in textures if "estimated_resident_mib" in item],
        key=lambda item: float(item.get("estimated_resident_mib", 0.0)),
        reverse=True,
    )

    detail_sorted = sorted(
        detail,
        key=lambda item: (
            0 if "detail" in Path(item["path"]).stem.lower() else 1,
            Path(item["path"]).stem.lower(),
        ),
    )

    return {
        "texture_count": len(textures),
        "albedo_candidate_count": len(albedo),
        "normal_candidate_count": len(normals),
        "orm_candidate_count": len(orm),
        "detail_candidate_count": len(detail),
        "energy_fail_count": len(energy_fail),
        "energy_warn_count": len(energy_warn),
        "read_error_count": len(read_error_textures),
        "read_error_textures": read_error_textures[:100],
        "albedo_read_error_count": len(albedo_read_errors),
        "albedo_read_error_textures": albedo_read_errors[:100],
        "import_issue_count": len(import_issue_textures),
        "import_issue_counts": import_issue_counts,
        "detail_suggestions": detail_sorted[:10],
        "energy_failures": energy_fail[:50],
        "energy_warnings": energy_warn[:50],
        "import_issue_textures": import_issue_textures[:100],
        "estimated_texture_mib": estimated_texture_mib,
        "largest_estimated_textures": largest_estimated[:50],
    }


def build_texture_budget_model(texture_summary: dict[str, Any], budget_mib: float) -> dict[str, Any]:
    safe_budget = max(0.001, budget_mib)
    estimated_mib = float(texture_summary.get("estimated_texture_mib", 0.0))
    used_ratio = estimated_mib / safe_budget
    status = "PASS"
    if estimated_mib > safe_budget:
        status = "FAIL"
    elif used_ratio >= TEXTURE_BUDGET_WARNING_RATIO:
        status = "WARN"

    return {
        "estimated_mib": round(estimated_mib, 3),
        "budget_mib": round(safe_budget, 3),
        "warning_threshold_mib": round(safe_budget * TEXTURE_BUDGET_WARNING_RATIO, 3),
        "used_ratio": round(used_ratio, 4),
        "status": status,
    }


def summarize_materials(materials: list[dict[str, Any]]) -> dict[str, Any]:
    issue_counts: dict[str, int] = {}
    issue_materials: list[dict[str, Any]] = []
    channel_candidates: list[dict[str, Any]] = []
    channel_priority_counts: dict[str, int] = {}
    unresolved_materials: list[dict[str, Any]] = []
    surface_unresolved_materials: list[dict[str, Any]] = []
    detail_missing_materials: list[dict[str, Any]] = []
    unresolved_ref_count = 0
    surface_unresolved_ref_count = 0
    for material in materials:
        issues = material.get("issues", [])
        for issue in issues:
            issue_counts[issue] = issue_counts.get(issue, 0) + 1
        if issues:
            issue_materials.append(material)
        if "NO_DETAIL_MAP_SLOT" in issues:
            detail_missing_materials.append(material)
        unresolved_refs = material.get("unresolved_texture_refs", [])
        if unresolved_refs:
            unresolved_materials.append(material)
            unresolved_ref_count += len(unresolved_refs)
            if material.get("is_surface_material_candidate"):
                surface_unresolved_materials.append(material)
                surface_unresolved_ref_count += len(unresolved_refs)
        candidate = material.get("channel_packing_candidate")
        if candidate:
            channel_candidates.append(candidate)
            priority = candidate.get("priority", "UNKNOWN")
            channel_priority_counts[priority] = channel_priority_counts.get(priority, 0) + 1

    return {
        "material_count": len(materials),
        "materials_with_prompt_orm": sum(1 for item in materials if item.get("has_prompt_orm")),
        "materials_with_legacy_mask": sum(1 for item in materials if item.get("has_legacy_packed_mask")),
        "materials_with_packed_mask": sum(1 for item in materials if item.get("has_packed_mask")),
        "materials_with_detail": sum(1 for item in materials if item.get("has_detail")),
        "detail_map_missing_count": len(detail_missing_materials),
        "detail_map_missing_materials": detail_missing_materials[:100],
        "materials_with_issues": len(issue_materials),
        "materials_with_unresolved_texture_refs": len(unresolved_materials),
        "unresolved_texture_ref_count": unresolved_ref_count,
        "unresolved_texture_ref_materials": unresolved_materials[:100],
        "surface_materials_with_unresolved_texture_refs": len(surface_unresolved_materials),
        "surface_unresolved_texture_ref_count": surface_unresolved_ref_count,
        "surface_unresolved_texture_ref_materials": surface_unresolved_materials[:100],
        "channel_packing_candidate_count": len(channel_candidates),
        "channel_packing_priority_counts": channel_priority_counts,
        "channel_packing_candidates": channel_candidates[:100],
        "vram_model": {
            "standard_mib_per_material": STANDARD_MATERIAL_MIB,
            "optimized_mib_per_material": OPTIMIZED_MATERIAL_MIB,
            "candidate_standard_mib": round(len(channel_candidates) * STANDARD_MATERIAL_MIB, 2),
            "candidate_optimized_mib": round(len(channel_candidates) * OPTIMIZED_MATERIAL_MIB, 2),
            "candidate_saved_mib": round(len(channel_candidates) * (STANDARD_MATERIAL_MIB - OPTIMIZED_MATERIAL_MIB), 2),
            "candidate_reduction_percent": round(
                ((STANDARD_MATERIAL_MIB - OPTIMIZED_MATERIAL_MIB) / STANDARD_MATERIAL_MIB) * 100.0,
                1,
            ),
        },
        "issue_counts": issue_counts,
        "issue_materials": issue_materials[:100],
    }


def run_audit(
    root: Path,
    sample_size: int,
    include_third_party: bool,
    resolve_root: Path | None = None,
    texture_budget_mib: float = TEXTURE_BUDGET_MIB,
) -> dict[str, Any]:
    effective_resolve_root = resolve_root if resolve_root is not None else root
    textures: list[dict[str, Any]] = []
    material_paths: list[Path] = []

    for dirpath, dirnames, filenames in os.walk(root):
        prune_dirs(root, Path(dirpath), dirnames, include_third_party)
        for filename in filenames:
            path = Path(dirpath) / filename
            suffix = path.suffix.lower()
            if suffix in IMAGE_EXTS:
                if is_generated_lighting_texture(path):
                    continue
                textures.append(inspect_image(path, root, sample_size))
            elif suffix in MATERIAL_EXTS:
                material_paths.append(path)

    raw_materials = [parse_material(path, root) for path in material_paths]
    needed_guids: set[str] = set()
    for material in raw_materials:
        for guid_or_path in material.get("texture_properties", {}).values():
            if isinstance(guid_or_path, str) and re.fullmatch(r"[0-9a-fA-F]{32}", guid_or_path):
                needed_guids.add(guid_or_path)

    guid_map = build_guid_map(effective_resolve_root, needed_guids, include_third_party)
    materials = [resolve_material(material, guid_map) for material in raw_materials]
    texture_summary = summarize_textures(textures)

    return {
        "root": normalized(root),
        "resolve_root": normalized(effective_resolve_root),
        "sample_size": sample_size,
        "include_third_party": include_third_party,
        "doctrine": {
            "orm_layout": "R=AO, G=Roughness, B=Metallic",
            "albedo_energy_fail": "mean_srgb_luma > 0.75 or mean_linear_luma > 0.60",
            "albedo_energy_warn": "p95_srgb_luma > 0.92 and bright_pixel_ratio > 0.10",
        },
        "gate_exit_codes": GATE_EXIT_CODES,
        "gate_profiles": {
            "surface_safe": list(CI_SURFACE_GATE_PROFILE),
        },
        "active_gate_profiles": [],
        "active_gates": ["energy_failures"],
        "texture_budget": build_texture_budget_model(texture_summary, texture_budget_mib),
        "texture_summary": texture_summary,
        "material_summary": summarize_materials(materials),
        "god_mode_texture_overrides": GOD_MODE_TEXTURE_OVERRIDES,
        "global_detail_overlay_plan": GLOBAL_DETAIL_OVERLAY_PLAN,
    }


def markdown_row(values: list[Any]) -> str:
    escaped = [str(value).replace("|", "\\|").replace("\n", " ") for value in values]
    return "| " + " | ".join(escaped) + " |"


def write_markdown_report(report: dict[str, Any], output: Path) -> None:
    texture_summary = report["texture_summary"]
    material_summary = report["material_summary"]
    lines: list[str] = [
        "# Material Audit - TECHNICAL_ARTIST_DATA",
        "",
        f"Root: `{report['root']}`",
        f"Resolve root: `{report.get('resolve_root', report['root'])}`",
        f"Sample size: `{report['sample_size']}`",
        f"Include third-party: `{report['include_third_party']}`",
        "",
        "## Summary",
        "",
        markdown_row(["Metric", "Value"]),
        markdown_row(["---", "---"]),
        markdown_row(["Textures", texture_summary["texture_count"]]),
        markdown_row(["Albedo candidates", texture_summary["albedo_candidate_count"]]),
        markdown_row(["Albedo energy failures", texture_summary["energy_fail_count"]]),
        markdown_row(["Albedo energy warnings", texture_summary["energy_warn_count"]]),
        markdown_row(["Texture read errors", texture_summary.get("read_error_count", 0)]),
        markdown_row(["Albedo read errors", texture_summary.get("albedo_read_error_count", 0)]),
        markdown_row(["Import issue textures", texture_summary["import_issue_count"]]),
        markdown_row(["Estimated texture residency MiB", texture_summary.get("estimated_texture_mib", 0)]),
        markdown_row(["ORM candidates", texture_summary["orm_candidate_count"]]),
        markdown_row(["Detail candidates", texture_summary["detail_candidate_count"]]),
        markdown_row(["Materials", material_summary["material_count"]]),
        markdown_row(["Materials with prompt ORM", material_summary.get("materials_with_prompt_orm", 0)]),
        markdown_row(["Materials with legacy mask", material_summary.get("materials_with_legacy_mask", 0)]),
        markdown_row(["Materials with packed mask", material_summary["materials_with_packed_mask"]]),
        markdown_row(["Materials with detail", material_summary["materials_with_detail"]]),
        markdown_row(["Materials missing detail maps", material_summary.get("detail_map_missing_count", 0)]),
        markdown_row(["Materials with issues", material_summary["materials_with_issues"]]),
        markdown_row([
            "Materials with unresolved texture refs",
            material_summary.get("materials_with_unresolved_texture_refs", 0),
        ]),
        markdown_row(["Unresolved texture refs", material_summary.get("unresolved_texture_ref_count", 0)]),
        markdown_row([
            "Surface materials with unresolved texture refs",
            material_summary.get("surface_materials_with_unresolved_texture_refs", 0),
        ]),
        markdown_row(["Surface unresolved texture refs", material_summary.get("surface_unresolved_texture_ref_count", 0)]),
        markdown_row(["Channel packing candidates", material_summary.get("channel_packing_candidate_count", 0)]),
        "",
    ]
    gate_exit_codes = report.get("gate_exit_codes", {})
    if gate_exit_codes:
        lines.extend([
            "## Gate Exit Codes",
            "",
            markdown_row(["Gate", "Exit code"]),
            markdown_row(["---", "---"]),
        ])
        for gate, exit_code in gate_exit_codes.items():
            lines.append(markdown_row([gate, exit_code]))
        lines.append("")

    gate_profiles = report.get("gate_profiles", {})
    if gate_profiles:
        lines.extend([
            "## Gate Profiles",
            "",
            markdown_row(["Profile", "Enabled gates"]),
            markdown_row(["---", "---"]),
        ])
        for profile, gates in gate_profiles.items():
            lines.append(markdown_row([profile, ", ".join(gates)]))
        lines.append("")

    active_gate_profiles = report.get("active_gate_profiles", [])
    active_gates = report.get("active_gates", [])
    if active_gate_profiles or active_gates:
        lines.extend([
            "## Active Gates",
            "",
            markdown_row(["Field", "Value"]),
            markdown_row(["---", "---"]),
            markdown_row(["Active profiles", ", ".join(active_gate_profiles) if active_gate_profiles else "none"]),
            markdown_row(["Active gates", ", ".join(active_gates) if active_gates else "none"]),
            "",
        ])

    texture_budget = report.get("texture_budget", {})
    if texture_budget:
        lines.extend([
            "## Texture Budget Model",
            "",
            markdown_row(["Metric", "Value"]),
            markdown_row(["---", "---"]),
            markdown_row(["Estimated MiB", texture_budget["estimated_mib"]]),
            markdown_row(["Budget MiB", texture_budget["budget_mib"]]),
            markdown_row(["Warning threshold MiB", texture_budget["warning_threshold_mib"]]),
            markdown_row(["Used ratio", texture_budget["used_ratio"]]),
            markdown_row(["Status", texture_budget["status"]]),
            "",
        ])

    vram_model = material_summary.get("vram_model", {})
    if vram_model:
        lines.extend([
            "## Channel Packing VRAM Model",
            "",
            markdown_row(["Metric", "Value"]),
            markdown_row(["---", "---"]),
            markdown_row(["Standard MiB/material", vram_model["standard_mib_per_material"]]),
            markdown_row(["Optimized MiB/material", vram_model["optimized_mib_per_material"]]),
            markdown_row(["Candidate standard MiB", vram_model["candidate_standard_mib"]]),
            markdown_row(["Candidate optimized MiB", vram_model["candidate_optimized_mib"]]),
            markdown_row(["Candidate saved MiB", vram_model["candidate_saved_mib"]]),
            markdown_row(["Candidate reduction percent", vram_model["candidate_reduction_percent"]]),
            "",
        ])

    lines.extend(["## GOD_MODE Texture Overrides", ""])
    overrides = report.get("god_mode_texture_overrides", [])
    if overrides:
        lines.extend([
            markdown_row(["Asset class", "TOASTER", "DECK", "PRO", "GOD_MODE", "Format", "Fallback"]),
            markdown_row(["---", "---", "---", "---", "---", "---", "---"]),
        ])
        for item in overrides:
            lines.append(markdown_row([
                item["asset_class"],
                item["toaster_max"],
                item["deck_max"],
                item["pro_max"],
                item["god_mode_max"],
                item["format"],
                item["fallback"],
            ]))
    else:
        lines.append("No GOD_MODE texture overrides defined.")
    lines.append("")

    lines.extend(["## Global Detail Overlay Plan", ""])
    overlay_plan = report.get("global_detail_overlay_plan", [])
    if overlay_plan:
        lines.extend([
            markdown_row(["Role", "Status", "Targets", "TOASTER", "GOD_MODE", "Detail gain %"]),
            markdown_row(["---", "---", "---", "---", "---", "---"]),
        ])
        for item in overlay_plan:
            lines.append(markdown_row([
                item["overlay_role"],
                item["source_status"],
                item["target_surfaces"],
                item["toaster_rule"],
                item["god_mode_rule"],
                item["expected_detail_gain_percent"],
            ]))
    else:
        lines.append("No global detail overlay plan defined.")
    lines.append("")

    lines.extend(["## Import Issue Counts", ""])
    if texture_summary["import_issue_counts"]:
        lines.extend([markdown_row(["Issue", "Count"]), markdown_row(["---", "---"])])
        for issue, count in sorted(texture_summary["import_issue_counts"].items()):
            lines.append(markdown_row([issue, count]))
    else:
        lines.append("No texture import issues detected by this offline pass.")

    lines.extend(["", "## Material Issue Counts", ""])
    if material_summary["issue_counts"]:
        lines.extend([markdown_row(["Issue", "Count"]), markdown_row(["---", "---"])])
        for issue, count in sorted(material_summary["issue_counts"].items()):
            lines.append(markdown_row([issue, count]))
    else:
        lines.append("No material slot issues detected by this offline pass.")

    lines.extend(["", "## Detail Candidates", ""])
    lines.extend([markdown_row(["Path", "Import issues"]), markdown_row(["---", "---"])])
    for item in texture_summary["detail_suggestions"]:
        lines.append(markdown_row([item["path"], ", ".join(item.get("import_issues", []))]))

    lines.extend(["", "## Detail Map Missing Materials", ""])
    detail_missing = material_summary.get("detail_map_missing_materials", [])
    if detail_missing:
        lines.extend([
            markdown_row(["Material", "Base maps", "Normal maps"]),
            markdown_row(["---", "---", "---"]),
        ])
        for item in detail_missing:
            props = item.get("texture_properties", {})
            lines.append(markdown_row([
                item["path"],
                "; ".join(property_paths(props, BASE_MAP_PROPS)),
                "; ".join(property_paths(props, NORMAL_MAP_PROPS)),
            ]))
    else:
        lines.append("No missing detail-map slots detected.")

    lines.extend(["", "## Texture Import Issues", ""])
    if texture_summary["import_issue_textures"]:
        lines.extend([markdown_row(["Path", "Issues", "Recommendations"]), markdown_row(["---", "---", "---"])])
        for item in texture_summary["import_issue_textures"]:
            issues = item.get("import_issues", [])
            recommendations = "; ".join(recommend_texture_fix(issue) for issue in issues)
            lines.append(markdown_row([item["path"], ", ".join(issues), recommendations]))
    else:
        lines.append("No import issues detected.")

    lines.extend(["", "## Texture Read Errors", ""])
    read_error_textures = texture_summary.get("read_error_textures", [])
    if read_error_textures:
        lines.extend([markdown_row(["Path", "Error"]), markdown_row(["---", "---"])])
        for item in read_error_textures:
            lines.append(markdown_row([item["path"], item.get("read_error", "")]))
    else:
        lines.append("No texture read errors detected.")

    lines.extend(["", "## Material Slot Issues", ""])
    if material_summary["issue_materials"]:
        lines.extend([markdown_row(["Material", "Issues", "Recommendations"]), markdown_row(["---", "---", "---"])])
        for item in material_summary["issue_materials"]:
            issues = item.get("issues", [])
            recommendations = "; ".join(recommend_material_fix(issue) for issue in issues)
            lines.append(markdown_row([item["path"], ", ".join(issues), recommendations]))
    else:
        lines.append("No material slot issues detected.")

    lines.extend(["", "## Unresolved Material Texture GUIDs", ""])
    unresolved_materials = material_summary.get("unresolved_texture_ref_materials", [])
    if unresolved_materials:
        lines.extend([markdown_row(["Material", "Unresolved refs"]), markdown_row(["---", "---"])])
        for item in unresolved_materials:
            lines.append(markdown_row([item["path"], "; ".join(item.get("unresolved_texture_refs", []))]))
    else:
        lines.append("No unresolved material texture GUIDs detected.")

    lines.extend(["", "## Surface Material Texture GUIDs", ""])
    surface_unresolved = material_summary.get("surface_unresolved_texture_ref_materials", [])
    if surface_unresolved:
        lines.extend([
            markdown_row(["Material", "Severity", "Base", "Normal", "Data", "Other", "Recommendation"]),
            markdown_row(["---", "---", "---", "---", "---", "---", "---"]),
        ])
        for item in surface_unresolved:
            summary = item.get("unresolved_texture_ref_summary") or unresolved_slot_summary(
                item.get("unresolved_texture_refs", []),
            )
            lines.append(markdown_row([
                item["path"],
                summary["severity"],
                "; ".join(summary["base_color_refs"]),
                "; ".join(summary["normal_refs"]),
                "; ".join(summary["data_refs"]),
                "; ".join(summary["other_refs"]),
                summary["recommendation"],
            ]))
    else:
        lines.append("No unresolved surface-material texture GUIDs detected.")

    lines.extend(["", "## Texture Memory Hotspots", ""])
    hotspots = texture_summary.get("largest_estimated_textures", [])
    if hotspots:
        lines.extend([
            markdown_row(["Texture", "MiB", "Role", "Size"]),
            markdown_row(["---", "---", "---", "---"]),
        ])
        for item in hotspots[:20]:
            lines.append(markdown_row([
                item["path"],
                item.get("estimated_resident_mib", 0),
                item.get("memory_role", ""),
                f"{item.get('width', '?')}x{item.get('height', '?')}",
            ]))
    else:
        lines.append("No texture memory estimates available.")

    lines.extend(["", "## Channel Packing Candidates", ""])
    candidates = material_summary.get("channel_packing_candidates", [])
    if candidates:
        lines.extend([
            markdown_row(["Material", "Priority", "Reason", "Mask sources", "Has detail"]),
            markdown_row(["---", "---", "---", "---", "---"]),
        ])
        for item in candidates:
            mask_sources = (
                item.get("occlusion_sources", [])
                + item.get("roughness_sources", [])
                + item.get("metallic_sources", [])
                + item.get("legacy_mask_sources", [])
            )
            lines.append(markdown_row([
                item["path"],
                item["priority"],
                item["reason"],
                "; ".join(mask_sources),
                item["has_detail"],
            ]))
    else:
        lines.append("No channel-packing migration candidates detected.")

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_csv_reports(report: dict[str, Any], prefix: Path) -> None:
    texture_summary = report["texture_summary"]
    material_summary = report["material_summary"]
    overrides = report.get("god_mode_texture_overrides", [])
    prefix.parent.mkdir(parents=True, exist_ok=True)

    with Path(f"{prefix}_texture_import_issues.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["path", "issues", "recommendations"])
        for item in texture_summary["import_issue_textures"]:
            issues = item.get("import_issues", [])
            writer.writerow([
                item["path"],
                ";".join(issues),
                " | ".join(recommend_texture_fix(issue) for issue in issues),
            ])

    with Path(f"{prefix}_texture_read_errors.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["path", "read_error"])
        for item in texture_summary.get("read_error_textures", []):
            writer.writerow([item["path"], item.get("read_error", "")])

    with Path(f"{prefix}_material_issues.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["path", "issues", "recommendations"])
        for item in material_summary["issue_materials"]:
            issues = item.get("issues", [])
            writer.writerow([
                item["path"],
                ";".join(issues),
                " | ".join(recommend_material_fix(issue) for issue in issues),
            ])

    with Path(f"{prefix}_unresolved_texture_refs.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["path", "unresolved_texture_refs"])
        for item in material_summary.get("unresolved_texture_ref_materials", []):
            writer.writerow([item["path"], ";".join(item.get("unresolved_texture_refs", []))])

    with Path(f"{prefix}_surface_unresolved_texture_refs.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow([
            "path",
            "severity",
            "base_color_refs",
            "normal_refs",
            "data_refs",
            "detail_refs",
            "other_refs",
            "recommendation",
            "unresolved_texture_refs",
        ])
        for item in material_summary.get("surface_unresolved_texture_ref_materials", []):
            unresolved_refs = item.get("unresolved_texture_refs", [])
            summary = item.get("unresolved_texture_ref_summary") or unresolved_slot_summary(unresolved_refs)
            writer.writerow([
                item["path"],
                summary["severity"],
                ";".join(summary["base_color_refs"]),
                ";".join(summary["normal_refs"]),
                ";".join(summary["data_refs"]),
                ";".join(summary["detail_refs"]),
                ";".join(summary["other_refs"]),
                summary["recommendation"],
                ";".join(unresolved_refs),
            ])

    with Path(f"{prefix}_detail_candidates.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["path", "import_issues"])
        for item in texture_summary["detail_suggestions"]:
            writer.writerow([item["path"], ";".join(item.get("import_issues", []))])

    with Path(f"{prefix}_detail_map_missing_materials.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["path", "base_maps", "normal_maps"])
        for item in material_summary.get("detail_map_missing_materials", []):
            props = item.get("texture_properties", {})
            writer.writerow([
                item["path"],
                ";".join(property_paths(props, BASE_MAP_PROPS)),
                ";".join(property_paths(props, NORMAL_MAP_PROPS)),
            ])

    with Path(f"{prefix}_texture_memory_hotspots.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["path", "estimated_resident_mib", "memory_role", "width", "height"])
        for item in texture_summary.get("largest_estimated_textures", []):
            writer.writerow([
                item["path"],
                item.get("estimated_resident_mib", 0),
                item.get("memory_role", ""),
                item.get("width", ""),
                item.get("height", ""),
            ])

    with Path(f"{prefix}_channel_packing_candidates.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow([
            "path",
            "priority",
            "reason",
            "base_maps",
            "normal_maps",
            "occlusion_sources",
            "roughness_sources",
            "metallic_sources",
            "legacy_mask_sources",
            "detail_sources",
            "has_detail",
        ])
        for item in material_summary.get("channel_packing_candidates", []):
            writer.writerow([
                item["path"],
                item["priority"],
                item["reason"],
                ";".join(item.get("base_maps", [])),
                ";".join(item.get("normal_maps", [])),
                ";".join(item.get("occlusion_sources", [])),
                ";".join(item.get("roughness_sources", [])),
                ";".join(item.get("metallic_sources", [])),
                ";".join(item.get("legacy_mask_sources", [])),
                ";".join(item.get("detail_sources", [])),
                item.get("has_detail", False),
            ])

    with Path(f"{prefix}_god_mode_texture_overrides.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow([
            "asset_class",
            "toaster_max",
            "deck_max",
            "pro_max",
            "god_mode_max",
            "format",
            "fallback",
        ])
        for item in overrides:
            writer.writerow([
                item["asset_class"],
                item["toaster_max"],
                item["deck_max"],
                item["pro_max"],
                item["god_mode_max"],
                item["format"],
                item["fallback"],
            ])

    with Path(f"{prefix}_global_detail_overlay_plan.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow([
            "overlay_role",
            "source_status",
            "target_surfaces",
            "toaster_rule",
            "god_mode_rule",
            "expected_detail_gain_percent",
        ])
        for item in report.get("global_detail_overlay_plan", []):
            writer.writerow([
                item["overlay_role"],
                item["source_status"],
                item["target_surfaces"],
                item["toaster_rule"],
                item["god_mode_rule"],
                item["expected_detail_gain_percent"],
            ])


def main() -> int:
    parser = argparse.ArgumentParser(description="Audit HECTON-8 surface textures/materials.")
    parser.add_argument("--root", default="Assets", help="Asset root to scan.")
    parser.add_argument(
        "--resolve-root",
        help="Optional wider asset root used only for resolving material texture GUIDs.",
    )
    parser.add_argument("--sample-size", type=int, default=512, help="Max image sample dimension.")
    parser.add_argument(
        "--texture-budget-mib",
        type=float,
        default=TEXTURE_BUDGET_MIB,
        help="Offline texture residency budget in MiB.",
    )
    parser.add_argument(
        "--include-third-party",
        action="store_true",
        help="Include third-party/vendor folders. Slow and not default for owned doctrine.",
    )
    parser.add_argument("--json", help="Optional JSON report path.")
    parser.add_argument("--markdown", help="Optional Markdown report path.")
    parser.add_argument("--csv-prefix", help="Optional CSV prefix for issue exports.")
    parser.add_argument(
        "--fail-on-import-issues",
        action="store_true",
        help="Return non-zero when texture import-setting issues are found.",
    )
    parser.add_argument(
        "--fail-on-energy-warnings",
        action="store_true",
        help="Return non-zero when albedo bright-area energy warnings are found.",
    )
    parser.add_argument(
        "--fail-on-material-issues",
        action="store_true",
        help="Return non-zero when material slot issues are found.",
    )
    parser.add_argument(
        "--fail-on-channel-packing-candidates",
        action="store_true",
        help="Return non-zero when materials are missing prompt ORM channel packing.",
    )
    parser.add_argument(
        "--fail-on-detail-map-missing",
        action="store_true",
        help="Return non-zero when base materials do not have detail-map slots.",
    )
    parser.add_argument(
        "--fail-on-unresolved-refs",
        action="store_true",
        help="Return non-zero when material texture GUIDs cannot be resolved.",
    )
    parser.add_argument(
        "--fail-on-surface-unresolved-refs",
        action="store_true",
        help="Return non-zero when surface-material texture GUIDs cannot be resolved.",
    )
    parser.add_argument(
        "--fail-on-texture-budget",
        action="store_true",
        help="Return non-zero when estimated texture residency exceeds --texture-budget-mib.",
    )
    parser.add_argument(
        "--fail-on-texture-read-errors",
        action="store_true",
        help="Return non-zero when albedo candidates cannot be decoded for energy validation.",
    )
    parser.add_argument(
        "--ci-surface-gates",
        action="store_true",
        help=(
            "Enable current-corpus safe CI gates: energy warnings, albedo read errors, "
            "and texture budget."
        ),
    )
    args = parser.parse_args()

    root = Path(args.root).resolve()
    if not root.exists():
        raise SystemExit(f"Root not found: {root}")
    resolve_root = Path(args.resolve_root).resolve() if args.resolve_root else root
    if not resolve_root.exists():
        raise SystemExit(f"Resolve root not found: {resolve_root}")

    report = run_audit(
        root,
        max(16, args.sample_size),
        args.include_third_party,
        resolve_root,
        args.texture_budget_mib,
    )

    fail_on_energy_warnings = args.fail_on_energy_warnings or args.ci_surface_gates
    fail_on_texture_read_errors = args.fail_on_texture_read_errors or args.ci_surface_gates
    fail_on_texture_budget = args.fail_on_texture_budget or args.ci_surface_gates
    active_gate_profiles = ["surface_safe"] if args.ci_surface_gates else []
    active_gates = ["energy_failures"]
    if fail_on_energy_warnings:
        active_gates.append("energy_warnings")
    if fail_on_texture_read_errors:
        active_gates.append("albedo_read_errors")
    if args.fail_on_import_issues:
        active_gates.append("import_issues")
    if args.fail_on_unresolved_refs:
        active_gates.append("unresolved_texture_refs")
    if args.fail_on_surface_unresolved_refs:
        active_gates.append("surface_unresolved_texture_refs")
    if fail_on_texture_budget:
        active_gates.append("texture_budget")
    if args.fail_on_channel_packing_candidates:
        active_gates.append("channel_packing_candidates")
    if args.fail_on_detail_map_missing:
        active_gates.append("detail_map_missing")
    if args.fail_on_material_issues:
        active_gates.append("material_issues")
    report["active_gate_profiles"] = active_gate_profiles
    report["active_gates"] = active_gates

    if args.json:
        output = Path(args.json)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    if args.markdown:
        write_markdown_report(report, Path(args.markdown))
    if args.csv_prefix:
        write_csv_reports(report, Path(args.csv_prefix))

    texture_summary = report["texture_summary"]
    material_summary = report["material_summary"]
    print("MATERIAL_AUDIT_SUMMARY")
    print(f"root={report['root']}")
    print(f"resolve_root={report['resolve_root']}")
    print(f"ci_surface_gates={'enabled' if args.ci_surface_gates else 'disabled'}")
    print(f"active_gate_profiles={','.join(active_gate_profiles) if active_gate_profiles else 'none'}")
    print(f"active_gates={','.join(active_gates)}")
    print(f"textures={texture_summary['texture_count']}")
    print(f"albedo_candidates={texture_summary['albedo_candidate_count']}")
    print(f"energy_failures={texture_summary['energy_fail_count']}")
    print(f"energy_warnings={texture_summary['energy_warn_count']}")
    print(f"texture_read_errors={texture_summary['read_error_count']}")
    print(f"albedo_read_errors={texture_summary['albedo_read_error_count']}")
    print(f"import_issue_textures={texture_summary['import_issue_count']}")
    print(f"estimated_texture_mib={texture_summary['estimated_texture_mib']}")
    print(f"texture_budget_mib={report['texture_budget']['budget_mib']}")
    print(f"texture_budget_status={report['texture_budget']['status']}")
    print(f"detail_candidates={texture_summary['detail_candidate_count']}")
    print(f"orm_candidates={texture_summary['orm_candidate_count']}")
    print(f"materials={material_summary['material_count']}")
    print(f"materials_with_prompt_orm={material_summary['materials_with_prompt_orm']}")
    print(f"materials_with_legacy_mask={material_summary['materials_with_legacy_mask']}")
    print(f"materials_with_packed_mask={material_summary['materials_with_packed_mask']}")
    print(f"materials_with_detail={material_summary['materials_with_detail']}")
    print(f"detail_map_missing_materials={material_summary['detail_map_missing_count']}")
    print(f"materials_with_issues={material_summary['materials_with_issues']}")
    print(f"materials_with_unresolved_texture_refs={material_summary['materials_with_unresolved_texture_refs']}")
    print(f"unresolved_texture_refs={material_summary['unresolved_texture_ref_count']}")
    print(f"surface_materials_with_unresolved_texture_refs={material_summary['surface_materials_with_unresolved_texture_refs']}")
    print(f"surface_unresolved_texture_refs={material_summary['surface_unresolved_texture_ref_count']}")
    print(f"channel_packing_candidates={material_summary['channel_packing_candidate_count']}")
    print(f"channel_candidate_saved_mib={material_summary['vram_model']['candidate_saved_mib']}")
    print(f"god_mode_override_count={len(report['god_mode_texture_overrides'])}")
    print(f"global_detail_overlay_count={len(report['global_detail_overlay_plan'])}")
    if args.json:
        print(f"json={args.json}")
    if args.markdown:
        print(f"markdown={args.markdown}")
    if args.csv_prefix:
        print(f"csv_prefix={args.csv_prefix}")
    if texture_summary["energy_fail_count"]:
        return 1
    if fail_on_energy_warnings and texture_summary["energy_warn_count"]:
        return 7
    if fail_on_texture_read_errors and texture_summary["albedo_read_error_count"]:
        return 6
    if args.fail_on_import_issues and texture_summary["import_issue_count"]:
        return 2
    if args.fail_on_unresolved_refs and material_summary["unresolved_texture_ref_count"]:
        return 4
    if args.fail_on_surface_unresolved_refs and material_summary["surface_unresolved_texture_ref_count"]:
        return 10
    if fail_on_texture_budget and report["texture_budget"]["status"] == "FAIL":
        return 5
    if args.fail_on_channel_packing_candidates and material_summary["channel_packing_candidate_count"]:
        return 8
    if args.fail_on_detail_map_missing and material_summary["detail_map_missing_count"]:
        return 9
    if args.fail_on_material_issues and material_summary["materials_with_issues"]:
        return 3
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
