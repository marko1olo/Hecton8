#!/usr/bin/env python3
"""SHINOBU_361 texture audit and bake queue compiler.

Static-only pass. It does not mutate Unity assets.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

try:
    from PIL import Image
except ImportError as exc:  # pragma: no cover - environment guard
    raise SystemExit("Pillow is required: python -m pip install pillow") from exc


AGENT_ID = "SHINOBU_361"
TARGET_EXTS = {".mat", ".shader", ".shadergraph", ".prefab", ".fbx"}
IMAGE_EXTS = {".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".exr", ".hdr", ".webp"}
FORBIDDEN_SOURCE_EXTS = {".tga", ".psd"}
TEXTURE_SLOT_HINTS = (
    "_BaseMap",
    "_MainTex",
    "_BaseColorMap",
    "_BumpMap",
    "_NormalMap",
    "_MetallicGlossMap",
    "_OcclusionMap",
    "_ORM",
    "_ORMMap",
    "_MaskMap",
    "_RoughnessMap",
    "_SmoothnessMap",
    "_DetailAlbedoMap",
    "_DetailNormalMap",
    "_EmissionMap",
    "_EmissiveMap",
    "_GlobalBrushDetail",
)
BUILTIN_DEFAULTS = {"white", "black", "gray", "grey", "lineargrey", "lineargray", "bump", "red"}
STUB_PATH_TOKENS = ("stub", "placeholder", "temp", "tmp", "test", "dummy", "quarantine", "prototype", "wip")
MOJIBAKE_TOKENS = ("�", "Ã", "Ð", "Ñ", "â")
ZERO_GUID = "00000000000000000000000000000000"
GENERATED_LIGHTING_PREFIXES = ("reflectionprobe", "lightmap", "lightingdata")


@dataclass
class TextureInfo:
    guid: str
    path: str
    extension: str
    width: int
    height: int
    is_stub: bool
    stub_reasons: str
    import_issues: str


@dataclass
class SlotRecord:
    record_id: str
    source_asset_path: str
    source_type: str
    material_name: str
    slot: str
    slot_role: str
    category: str
    reference_state: str
    reference_guid: str
    resolved_texture_path: str
    target_texture_path: str
    generated_asset_guid: str
    priority: str
    target_resolution: int
    albedo_compression: str
    normal_compression: str
    orm_compression: str
    estimated_vram_mib: float
    stub_reasons: str
    prompt_id: str
    issue_detail: str


def normalized(path: Path) -> str:
    return path.as_posix()


def rel(path: Path, root: Path) -> str:
    try:
        return normalized(path.relative_to(root))
    except ValueError:
        return normalized(path)


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return ""


def read_meta_guid(meta_path: Path) -> str:
    try:
        for line in meta_path.read_text(encoding="utf-8", errors="ignore").splitlines():
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    except OSError:
        return ""
    return ""


def parse_texture_meta(asset_path: Path) -> dict[str, str]:
    meta_path = asset_path.with_name(asset_path.name + ".meta")
    data: dict[str, str] = {}
    if not meta_path.exists():
        data["missingMeta"] = "1"
        return data

    text = read_text(meta_path)
    for key in ("sRGBTexture", "enableMipMap", "textureType", "textureCompression", "isReadable"):
        match = re.search(rf"\b{key}:\s*([^\s]+)", text)
        if match:
            data[key] = match.group(1)
    return data


def build_guid_map(root: Path) -> dict[str, Path]:
    result: dict[str, Path] = {}
    for meta_path in root.rglob("*.meta"):
        guid = read_meta_guid(meta_path)
        if not guid:
            continue
        asset_path = meta_path.with_name(meta_path.name[:-5])
        result[guid.lower()] = asset_path
    return result


def classify_slot(slot: str) -> str:
    name = slot.lower()
    if "normal" in name or "bump" in name or "norm" in name or name.endswith("_n") or name.endswith("_nrm"):
        return "NORMAL"
    if "orm" in name or "mask" in name:
        return "ORM"
    if "occlusion" in name or name.endswith("_ao"):
        return "AO"
    if "rough" in name or "smooth" in name or "gloss" in name:
        return "ROUGHNESS"
    if "metal" in name:
        return "METALLIC"
    if "emiss" in name or "emission" in name:
        return "EMISSIVE"
    if "detail" in name:
        return "DETAIL"
    if "base" in name or "maintex" in name or "albedo" in name or "color" in name or "diffuse" in name:
        return "ALBEDO"
    return "TEXTURE"


def classify_category(source_path: str, slot: str) -> str:
    hay = f"{source_path} {slot}".lower()
    if any(token in hay for token in ("decal", "crack", "blood", "splatter", "weld", "scorch", "acid", "drip", "rust")):
        return "DECAL_SHEETS"
    if any(token in hay for token in ("flora", "kelp", "coral", "algae", "anemone", "plant", "membrane", "biolum")):
        return "FLORA_EPIDERMIS"
    if any(token in hay for token in ("geolog", "rock", "basalt", "terrain", "cave", "cliff", "ash", "mineral", "silt", "volcan", "trench", "sediment")):
        return "GEOLOGY_TRIPLANAR"
    if any(token in hay for token in ("cockpit", "console", "instrument", "helm", "bridge", "command", "dashboard", "terminal")):
        return "COCKPIT_SURFACES"
    return "HABITAT_INTERIORS"


def priority_for(category: str, source_path: str) -> str:
    hay = source_path.lower()
    immediate_tokens = (
        "_prologue_content",
        "cockpit",
        "habitat",
        "main_menu",
        "bootstrap",
        "start",
        "terminal",
        "airlock",
        "interior",
        "suit_hud",
        "visor",
        "hud",
    )
    distant_background_tokens = (
        "skybox",
        "planet",
        "gasgiant",
        "gas_giant",
        "star",
        "celestial",
        "background",
        "panorama",
    )
    if any(token in hay for token in immediate_tokens) and category in {"COCKPIT_SURFACES", "HABITAT_INTERIORS", "DECAL_SHEETS"}:
        return "BLOCKER"
    if any(token in hay for token in distant_background_tokens):
        return "LOW"
    if category in {"COCKPIT_SURFACES", "HABITAT_INTERIORS"}:
        return "MEDIUM"
    if category in {"GEOLOGY_TRIPLANAR", "FLORA_EPIDERMIS"}:
        return "MEDIUM"
    return "LOW"


def target_resolution_for(category: str, slot_role: str, priority: str) -> int:
    if category == "DECAL_SHEETS":
        return 1024 if priority != "LOW" else 512
    if slot_role in {"ORM", "AO", "ROUGHNESS", "METALLIC", "EMISSIVE"}:
        return 1024 if priority in {"BLOCKER", "MEDIUM"} else 512
    if category in {"COCKPIT_SURFACES", "GEOLOGY_TRIPLANAR", "FLORA_EPIDERMIS"} and priority == "BLOCKER":
        return 2048
    if category == "GEOLOGY_TRIPLANAR":
        return 2048
    return 1024 if priority != "LOW" else 512


def compression_for_role(slot_role: str) -> tuple[str, str, str, str]:
    albedo = "BC7 sRGB, mipmaps on, Read/Write off"
    normal = "BC5 linear normal, mipmaps on, Read/Write off"
    orm = "BC7 linear packed ORM, mipmaps on, Read/Write off"
    if slot_role == "NORMAL":
        active = normal
    elif slot_role in {"ORM", "AO", "ROUGHNESS", "METALLIC", "EMISSIVE"}:
        active = orm
    else:
        active = albedo
    return active, albedo, normal, orm


def estimate_mib(resolution: int, bits_per_pixel: int = 8, include_mips: bool = True) -> float:
    mip_factor = 4.0 / 3.0 if include_mips else 1.0
    return round((resolution * resolution * bits_per_pixel / 8.0 * mip_factor) / (1024.0 * 1024.0), 3)


def detect_import_issues(path: Path, slot_role: str) -> list[str]:
    issues: list[str] = []
    lowered_path = normalized(path).lower()
    sprite_or_ui = "/sprites/" in lowered_path or "/resources/ui/" in lowered_path or "/ui/" in lowered_path
    if path.suffix.lower() in FORBIDDEN_SOURCE_EXTS:
        issues.append(f"FORBIDDEN_SOURCE_FORMAT_{path.suffix.lower()[1:].upper()}")
    meta = parse_texture_meta(path)
    if meta.get("missingMeta") == "1":
        issues.append("MISSING_META")
    srgb = meta.get("sRGBTexture")
    texture_type = meta.get("textureType")
    mip = meta.get("enableMipMap")
    compression = meta.get("textureCompression")
    if slot_role == "NORMAL":
        if srgb == "1":
            issues.append("NORMAL_SRGB_ON")
        if texture_type and texture_type != "1":
            issues.append("NORMAL_NOT_TEXTURETYPE_NORMAL")
    elif slot_role in {"ORM", "AO", "ROUGHNESS", "METALLIC", "EMISSIVE"}:
        if srgb == "1":
            issues.append("DATA_TEXTURE_SRGB_ON")
    if mip == "0" and not sprite_or_ui:
        issues.append("MIPMAPS_OFF")
    if compression == "0":
        issues.append("UNCOMPRESSED_TEXTURE_IMPORT")
    return issues


def is_generated_lighting_texture(path: Path) -> bool:
    lowered = path.name.lower()
    return lowered.startswith(GENERATED_LIGHTING_PREFIXES)


def is_checkerboard(image: Image.Image) -> bool:
    small = image.convert("RGB").resize((min(image.width, 8), min(image.height, 8)))
    pixels = list(small.getdata())
    colors = list(dict.fromkeys(pixels))
    if len(colors) != 2:
        return False
    width, height = small.size
    for y in range(height):
        for x in range(width):
            expected = colors[(x + y) & 1]
            if pixels[y * width + x] != expected:
                return False
    return True


def inspect_texture(path: Path, root: Path, guid: str) -> TextureInfo:
    reasons: list[str] = []
    width = 0
    height = 0
    lowered = normalized(path).lower()
    if any(token in lowered for token in STUB_PATH_TOKENS):
        reasons.append("PLACEHOLDER_PATH_TOKEN")
    if any(token in normalized(path) for token in MOJIBAKE_TOKENS):
        reasons.append("MOJIBAKE_NAME")
    try:
        with Image.open(path) as image:
            width = int(image.width)
            height = int(image.height)
            if width <= 1 and height <= 1:
                reasons.append("ONE_BY_ONE_STUB")
            if width <= 4 and height <= 4:
                pixels = list(image.convert("RGBA").getdata())
                if pixels and len(set(pixels)) == 1:
                    reasons.append("SOLID_COLOR_STUB")
            if width <= 64 and height <= 64 and is_checkerboard(image):
                reasons.append("CHECKERBOARD_STUB")
    except Exception as exc:  # noqa: BLE001 - image codecs vary by workstation
        reasons.append(f"IMAGE_READ_ERROR:{type(exc).__name__}")

    slot_role = classify_slot(path.stem)
    import_issues = detect_import_issues(path, slot_role)
    return TextureInfo(
        guid=guid,
        path=rel(path, root),
        extension=path.suffix.lower(),
        width=width,
        height=height,
        is_stub=bool(reasons),
        stub_reasons=";".join(reasons),
        import_issues=";".join(import_issues),
    )


def embedded_basename(value: str) -> str:
    normalized_value = value.replace("\\", "/").strip().strip("\"'")
    if not normalized_value:
        return ""
    return normalized_value.rsplit("/", 1)[-1].lower()


def embedded_stem(value: str) -> str:
    name = embedded_basename(value)
    if not name:
        return ""
    return name.rsplit(".", 1)[0]


def build_texture_name_index(texture_by_guid: dict[str, TextureInfo]) -> dict[str, TextureInfo]:
    index: dict[str, TextureInfo] = {}
    for info in texture_by_guid.values():
        name = embedded_basename(info.path)
        if not name:
            continue
        if name not in index:
            index[name] = info
    return index


def parse_guid_payload(payload: str) -> tuple[str, str, str]:
    file_id = ""
    guid = ""
    type_id = ""
    file_match = re.search(r"fileID:\s*([-0-9]+)", payload)
    guid_match = re.search(r"guid:\s*([0-9a-fA-F]{32})", payload)
    type_match = re.search(r"type:\s*([0-9]+)", payload)
    if file_match:
        file_id = file_match.group(1)
    if guid_match:
        guid = guid_match.group(1).lower()
    if type_match:
        type_id = type_match.group(1)
    return file_id, guid, type_id


def parse_material_slots(path: Path, root: Path) -> list[dict[str, str]]:
    text = read_text(path)
    records: list[dict[str, str]] = []
    pattern = re.compile(r"-\s*([A-Za-z0-9_]+):\s*(?:\r?\n\s*)+m_Texture:\s*\{([^}]*)\}", re.MULTILINE)
    for match in pattern.finditer(text):
        slot = match.group(1)
        payload = match.group(2)
        file_id, guid, type_id = parse_guid_payload(payload)
        records.append(
            {
                "source_asset_path": rel(path, root),
                "source_type": "material",
                "material_name": path.stem,
                "slot": slot,
                "file_id": file_id,
                "guid": guid,
                "type_id": type_id,
            }
        )
    return records


def parse_shader_slots(path: Path, root: Path) -> list[dict[str, str]]:
    text = read_text(path)
    records: list[dict[str, str]] = []
    pattern = re.compile(r"([A-Za-z_][A-Za-z0-9_]*)\s*\(\s*\"[^\"]*\"\s*,\s*(?:2D|Cube|3D|2DArray)\s*\)\s*=\s*\"([^\"]*)\"", re.MULTILINE)
    for match in pattern.finditer(text):
        slot = match.group(1)
        default = match.group(2).strip().lower()
        if slot.startswith("unity_"):
            continue
        records.append(
            {
                "source_asset_path": rel(path, root),
                "source_type": "shader",
                "material_name": path.stem,
                "slot": slot,
                "file_id": "",
                "guid": "",
                "type_id": "",
                "default": default,
            }
        )
    return records


def parse_shadergraph_slots(path: Path, root: Path) -> list[dict[str, str]]:
    text = read_text(path)
    records: list[dict[str, str]] = []
    names = set(re.findall(r"\"(?:m_DefaultReferenceName|referenceName|m_ReferenceName|m_Name)\"\s*:\s*\"([A-Za-z_][A-Za-z0-9_]*)\"", text))
    guids = re.findall(r"guid:\s*([0-9a-fA-F]{32})", text)
    guid = guids[0].lower() if guids else ""
    for slot in sorted(names):
        role = classify_slot(slot)
        if role == "TEXTURE" and not any(token.lower() in slot.lower() for token in ("tex", "map", "mask")):
            continue
        records.append(
            {
                "source_asset_path": rel(path, root),
                "source_type": "shadergraph",
                "material_name": path.stem,
                "slot": slot,
                "file_id": "",
                "guid": guid,
                "type_id": "",
                "default": "",
            }
        )
    return records


def parse_prefab_slots(path: Path, root: Path, guid_map: dict[str, Path]) -> list[dict[str, str]]:
    text = read_text(path)
    records: list[dict[str, str]] = []
    for index, match in enumerate(re.finditer(r"guid:\s*([0-9a-fA-F]{32})", text)):
        guid = match.group(1).lower()
        target = guid_map.get(guid)
        window = text[max(0, match.start() - 120) : match.end() + 120].lower()
        sprite_context = "m_sprite" in window or "spriterenderer" in window or "sprite renderer" in window
        if target is None:
            script_context = "m_script" in window
            texture_context = "m_texture" in window or "texture:" in window or "textureguid" in window or "_tex" in window
            material_only_context = "material" in window and not texture_context
            if script_context or sprite_context or material_only_context or not texture_context:
                continue
            slot = "BrokenTextureGuidRef"
        elif target.suffix.lower() in IMAGE_EXTS:
            if sprite_context:
                continue
            slot = "DirectTextureRef"
        elif target.suffix.lower() == ".mat":
            slot = "MaterialRef"
        else:
            continue
        records.append(
            {
                "source_asset_path": rel(path, root),
                "source_type": "prefab",
                "material_name": path.stem,
                "slot": f"{slot}_{index}",
                "file_id": "",
                "guid": guid,
                "type_id": "",
            }
        )
    return records


def parse_fbx_slots(path: Path, root: Path) -> list[dict[str, str]]:
    text = read_text(path)
    records: list[dict[str, str]] = []
    for index, match in enumerate(re.finditer(r"(?i)(?:Texture|FileName).*?([A-Za-z0-9_./\\ -]+\.(?:png|jpg|jpeg|tga|tif|tiff|psd))", text)):
        records.append(
            {
                "source_asset_path": rel(path, root),
                "source_type": "fbx",
                "material_name": path.stem,
                "slot": f"EmbeddedTexturePath_{index}",
                "file_id": "",
                "guid": "",
                "type_id": "",
                "default": match.group(1),
            }
        )
    return records


def collect_slot_records(asset_root: Path, project_root: Path, guid_map: dict[str, Path]) -> tuple[list[dict[str, str]], dict[str, int]]:
    slots: list[dict[str, str]] = []
    counts = {ext: 0 for ext in sorted(TARGET_EXTS)}
    for path in asset_root.rglob("*"):
        ext = path.suffix.lower()
        if ext not in TARGET_EXTS:
            continue
        counts[ext] += 1
        if ext == ".mat":
            slots.extend(parse_material_slots(path, project_root))
        elif ext == ".shader":
            slots.extend(parse_shader_slots(path, project_root))
        elif ext == ".shadergraph":
            slots.extend(parse_shadergraph_slots(path, project_root))
        elif ext == ".prefab":
            slots.extend(parse_prefab_slots(path, project_root, guid_map))
        elif ext == ".fbx":
            slots.extend(parse_fbx_slots(path, project_root))
    return slots, counts


def target_path_for(slot: dict[str, str], category: str, slot_role: str) -> str:
    family_source = slot.get("material_name") or "Material"
    if slot.get("source_type") == "fbx":
        embedded = embedded_stem(slot.get("default", ""))
        if embedded:
            family_source = f"{family_source}_{embedded}"
    family = re.sub(r"[^A-Za-z0-9_]+", "_", family_source).strip("_")
    if not family:
        family = "Material"
    if slot_role == "NORMAL":
        suffix = "Normal"
    elif slot_role in {"ORM", "AO", "ROUGHNESS", "METALLIC", "EMISSIVE"}:
        suffix = "ORM" if slot_role != "EMISSIVE" else "Emissive"
    else:
        suffix = "Albedo"
    return f"Assets/_Project/Art/Textures/Generated/SHINOBU_361/{category}/{family}_{suffix}.png"


def state_for_slot(
    slot: dict[str, str],
    guid_map: dict[str, Path],
    texture_by_guid: dict[str, TextureInfo],
    texture_name_index: dict[str, TextureInfo],
    project_root: Path,
) -> tuple[str, str, str, str]:
    guid = slot.get("guid", "").lower()
    file_id = slot.get("file_id", "")
    default = slot.get("default", "").lower()
    role = classify_slot(f"{slot.get('slot', '')} {default}")
    if slot.get("source_type") == "shader" and default in BUILTIN_DEFAULTS:
        return "DECLARED_SHADER_DEFAULT", "", "", f"default={default}"
    if slot.get("source_type") == "fbx" and default and not guid:
        embedded_name = embedded_basename(default)
        texture = texture_name_index.get(embedded_name)
        if texture:
            if texture.is_stub:
                return "STUB_TEXTURE", texture.path, texture.stub_reasons, f"embedded texture basename resolved: {embedded_name}"
            if texture.import_issues:
                return "IMPORT_ISSUE", texture.path, texture.import_issues, f"embedded texture basename resolved with import issue: {embedded_name}"
            return "RESOLVED_EMBEDDED_TEXTURE", texture.path, "", f"embedded texture basename resolved: {embedded_name}"
        return "MISSING_EMBEDDED_TEXTURE", "", default, f"embedded texture path not found in Assets/_Project: {default}"
    if default and not guid:
        return "EMBEDDED_TEXTURE_PATH", "", default, f"default={default}"
    if not guid:
        if file_id == "0":
            if role == "ALBEDO":
                return "EMPTY_REQUIRED_SLOT", "", "", "fileID=0"
            return "EMPTY_OPTIONAL_SLOT", "", "", "fileID=0"
        return "DECLARED_SLOT", "", "", "no direct texture GUID"
    if guid == ZERO_GUID:
        if file_id and file_id != "0":
            return "BUILTIN_DEFAULT_TEXTURE", "", "", f"fileID={file_id}; type={slot.get('type_id', '')}"
        return "EMPTY_OPTIONAL_SLOT", "", "", "zero GUID"
    resolved = guid_map.get(guid)
    if resolved is None:
        return "MISSING_GUID", "", "", "GUID not found in scanned .meta files"
    resolved_path = rel(resolved, project_root)
    if resolved.suffix.lower() in IMAGE_EXTS:
        texture = texture_by_guid.get(guid)
        if texture and texture.is_stub:
            return "STUB_TEXTURE", resolved_path, texture.stub_reasons, "referenced texture is placeholder/stub"
        import_issues = detect_import_issues(resolved, role)
        if import_issues:
            return "IMPORT_ISSUE", resolved_path, ";".join(import_issues), "referenced texture import/source format issue"
        return "RESOLVED_TEXTURE", resolved_path, "", "resolved texture"
    if resolved.suffix.lower() == ".mat":
        return "RESOLVED_MATERIAL_REF", resolved_path, "", "prefab material reference"
    return "RESOLVED_NON_TEXTURE_GUID", resolved_path, "", f"resolved non-texture asset {resolved.suffix.lower()}"


def build_prompt(category: str, slot_role: str, material_name: str) -> str:
    base = material_name.replace("_", " ").replace("-", " ").strip() or "HECTON 8 surface"
    if category == "COCKPIT_SURFACES":
        subject = f"Material Subject: Premium NASA Punk deep sea cockpit surface for {base}, pearl white ceramic composite, brushed titanium rails, smoked glass inserts, pressure rated graphite rubber seams, champagne anodized fasteners, and restrained teal and amber instrument accent paint."
        details = "Surface Wear and Details: Precise micro scratches, clean machined bevels, subtle salt bloom in seams, careful edge wear, small service labels, soft hand-polished contact zones, gasket compression marks, and baked panel seams that feel like expensive expedition hardware instead of dark scrap metal."
    elif category == "HABITAT_INTERIORS":
        subject = f"Material Subject: High-end modular ocean habitat material for {base}, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base."
        details = "Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim."
    elif category == "GEOLOGY_TRIPLANAR":
        subject = f"Material Subject: Striking alien seafloor geology texture for {base}, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection."
        details = "Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill."
    elif category == "FLORA_EPIDERMIS":
        subject = f"Material Subject: Beautiful abyssal flora epidermis for {base}, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask."
        details = "Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge."
    else:
        subject = f"Material Subject: Polished cinematic decal sheet source for {base}, precision glass fracture marks, clean welding halos, elegant rust streaks, controlled fluid traces, mineral stains, and believable emergency repair markings for NASA Punk Deep Sea Noir surfaces."
        details = "Surface Wear and Details: Sharp readable crack islands, refined soot gradients, warm heat discoloration, neat weld scars, thin salt trails, oil sheen edges, and isolated decal islands rendered on a perfectly solid black background for reliable alpha extraction."

    view = "Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction."
    finish = "Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene."
    if slot_role == "NORMAL":
        finish += " The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction."
    if slot_role in {"ORM", "AO", "ROUGHNESS", "METALLIC"}:
        finish += " The surface must separate cavities, worn rough patches, and metallic substrate clearly enough to derive packed ORM masks."
    return f"{subject} {details} {view} {finish}"


def normal_plan(category: str, slot_role: str) -> str:
    if category == "DECAL_SHEETS":
        return "Do not derive a strong tangent normal from the decal albedo. Extract alpha from the black background, then author a shallow BC5 normal only for glass crack ridges, weld lips, or dried fluid edges if the decal will be inspected within arm reach."
    if slot_role == "NORMAL":
        return "Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth."
    return "Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast."


def orm_plan(category: str) -> str:
    if category == "GEOLOGY_TRIPLANAR":
        return "Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on."
    if category == "COCKPIT_SURFACES":
        return "Pack _ORM as Red equals tight AO under labels, seams, screws, and baked fake rivets, Green equals roughness 0.25 on polished worn metal rising to 0.78 on scratched paint and salt crust, Blue equals metallic 1.0 on exposed titanium or aluminum and 0.0 on rubber or plastic zones. Use BC7 linear, mipmaps on."
    if category == "FLORA_EPIDERMIS":
        return "Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear."
    if category == "DECAL_SHEETS":
        return "Pack _ORM as Red equals local decal occlusion, Green equals roughness 0.15 for wet blood or acid and 0.85 for soot or dry rust, Blue equals metallic 0.0 except weld scar metal flecks at 0.3. Extract decal alpha from the black background into the decal opacity channel."
    return "Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on."


def build_records(slots: list[dict[str, str]], guid_map: dict[str, Path], texture_by_guid: dict[str, TextureInfo], project_root: Path) -> list[SlotRecord]:
    records: list[SlotRecord] = []
    texture_name_index = build_texture_name_index(texture_by_guid)
    for index, slot in enumerate(slots):
        slot_role = classify_slot(f"{slot.get('slot', '')} {slot.get('default', '')}")
        category = classify_category(slot.get("source_asset_path", ""), slot.get("slot", ""))
        state, resolved_path, extra, detail = state_for_slot(slot, guid_map, texture_by_guid, texture_name_index, project_root)
        priority = priority_for(category, slot.get("source_asset_path", ""))
        resolution = target_resolution_for(category, slot_role, priority)
        active_compression, albedo_compression, normal_compression, orm_compression = compression_for_role(slot_role)
        target_path = resolved_path if state == "RESOLVED_TEXTURE" else target_path_for(slot, category, slot_role)
        prompt_id = ""
        estimated = 0.0
        generated_guid = ""
        if state in {"EMPTY_REQUIRED_SLOT", "MISSING_GUID", "MISSING_EMBEDDED_TEXTURE", "STUB_TEXTURE", "BUILTIN_DEFAULT_TEXTURE", "IMPORT_ISSUE"}:
            prompt_id = f"{AGENT_ID}_PROMPT_{index + 1:04d}"
            estimated = estimate_mib(resolution)
            generated_guid = "PENDING_UNITY_META_GUID"
        record_hash = hashlib.sha1(f"{slot.get('source_asset_path')}|{slot.get('slot')}|{index}".encode("utf-8")).hexdigest()[:12]
        records.append(
            SlotRecord(
                record_id=record_hash,
                source_asset_path=slot.get("source_asset_path", ""),
                source_type=slot.get("source_type", ""),
                material_name=slot.get("material_name", ""),
                slot=slot.get("slot", ""),
                slot_role=slot_role,
                category=category,
                reference_state=state,
                reference_guid=slot.get("guid", ""),
                resolved_texture_path=resolved_path,
                target_texture_path=target_path,
                generated_asset_guid=generated_guid,
                priority=priority,
                target_resolution=resolution,
                albedo_compression=albedo_compression,
                normal_compression=normal_compression,
                orm_compression=orm_compression if active_compression else orm_compression,
                estimated_vram_mib=estimated,
                stub_reasons=extra,
                prompt_id=prompt_id,
                issue_detail=detail,
            )
        )
    return records


def prompt_entries(records: list[SlotRecord]) -> list[dict[str, Any]]:
    entries: list[dict[str, Any]] = []
    for record in records:
        if not record.prompt_id:
            continue
        entries.append(
            {
                "prompt_id": record.prompt_id,
                "category": record.category,
                "source_asset_path": record.source_asset_path,
                "slot": record.slot,
                "slot_role": record.slot_role,
                "reference_state": record.reference_state,
                "priority": record.priority,
                "target_texture_path": record.target_texture_path,
                "target_resolution": record.target_resolution,
                "compression": {
                    "albedo": record.albedo_compression,
                    "normal": record.normal_compression,
                    "orm": record.orm_compression,
                },
                "albedo_prompt": build_prompt(record.category, record.slot_role, record.material_name),
                "normal_plan": normal_plan(record.category, record.slot_role),
                "orm_plan": orm_plan(record.category),
            }
        )
    return entries


def priority_rank(priority: str) -> int:
    if priority == "BLOCKER":
        return 3
    if priority == "MEDIUM":
        return 2
    if priority == "LOW":
        return 1
    return 0


def production_action_for_states(states: list[str]) -> str:
    state_set = set(states)
    generation_states = {
        "EMPTY_REQUIRED_SLOT",
        "MISSING_GUID",
        "MISSING_EMBEDDED_TEXTURE",
        "STUB_TEXTURE",
        "BUILTIN_DEFAULT_TEXTURE",
    }
    needs_generation = bool(state_set & generation_states)
    needs_import_fix = "IMPORT_ISSUE" in state_set
    if needs_generation and needs_import_fix:
        return "GENERATE_REPLACEMENT_PBR_AND_FIX_IMPORT"
    if needs_generation:
        return "GENERATE_REPLACEMENT_PBR"
    if needs_import_fix:
        return "REBAKE_SOURCE_TO_PNG_AND_FIX_IMPORT"
    return "REVIEW_STATIC_REFERENCE"


def build_unique_texture_queue(prompts: list[dict[str, Any]]) -> list[dict[str, Any]]:
    queue_by_target: dict[str, dict[str, Any]] = {}
    for entry in prompts:
        target = entry["target_texture_path"]
        row = queue_by_target.get(target)
        if row is None:
            row = {
                "queue_id": f"{AGENT_ID}_QUEUE_{len(queue_by_target) + 1:04d}",
                "target_texture_path": target,
                "action": "",
                "texture_role": entry["slot_role"],
                "category": entry["category"],
                "priority": "LOW",
                "target_resolution": entry["target_resolution"],
                "source_count": 0,
                "source_asset_paths": [],
                "slots": [],
                "reference_states": [],
                "albedo_prompt": entry["albedo_prompt"],
                "normal_plan": entry["normal_plan"],
                "orm_plan": entry["orm_plan"],
                "compression_albedo": entry["compression"]["albedo"],
                "compression_normal": entry["compression"]["normal"],
                "compression_orm": entry["compression"]["orm"],
            }
            queue_by_target[target] = row
        row["source_count"] += 1
        if entry["source_asset_path"] not in row["source_asset_paths"]:
            row["source_asset_paths"].append(entry["source_asset_path"])
        if entry["slot"] not in row["slots"]:
            row["slots"].append(entry["slot"])
        if entry["reference_state"] not in row["reference_states"]:
            row["reference_states"].append(entry["reference_state"])
        priority = str(entry.get("priority", ""))
        if priority_rank(priority) > priority_rank(row["priority"]):
            row["priority"] = priority
    queue = list(queue_by_target.values())
    for row in queue:
        row["action"] = production_action_for_states(row["reference_states"])
        row["source_asset_paths"] = ";".join(row["source_asset_paths"])
        row["slots"] = ";".join(row["slots"])
        row["reference_states"] = ";".join(row["reference_states"])
    queue.sort(key=lambda item: (-priority_rank(str(item["priority"])), str(item["category"]), str(item["target_texture_path"])))
    return queue


def count_queue_field(queue: list[dict[str, Any]], field: str) -> dict[str, int]:
    counts: dict[str, int] = {}
    for row in queue:
        key = str(row.get(field, "UNKNOWN"))
        counts[key] = counts.get(key, 0) + 1
    return dict(sorted(counts.items()))


def write_unique_queue_csv(path: Path, queue: list[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if queue:
        fields = list(queue[0].keys())
    else:
        fields = [
            "queue_id",
            "target_texture_path",
            "action",
            "texture_role",
            "category",
            "priority",
            "target_resolution",
            "source_count",
            "source_asset_paths",
            "slots",
            "reference_states",
            "albedo_prompt",
            "normal_plan",
            "orm_plan",
            "compression_albedo",
            "compression_normal",
            "compression_orm",
        ]
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        for row in queue:
            writer.writerow(row)


def write_readable_queue(path: Path, queue: list[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    category_counts: dict[str, int] = {}
    role_counts: dict[str, int] = {}
    resolution_counts: dict[str, int] = {}
    action_counts: dict[str, int] = {}
    for row in queue:
        category = str(row.get("category", "UNKNOWN"))
        role = str(row.get("texture_role", "UNKNOWN"))
        resolution = str(row.get("target_resolution", "UNKNOWN"))
        action = str(row.get("action", "UNKNOWN"))
        category_counts[category] = category_counts.get(category, 0) + 1
        role_counts[role] = role_counts.get(role, 0) + 1
        resolution_counts[resolution] = resolution_counts.get(resolution, 0) + 1
        action_counts[action] = action_counts.get(action, 0) + 1

    lines: list[str] = []
    lines.append("# SHINOBU_361 Texture Production Queue - Readable")
    lines.append("")
    lines.append("Status: PENDING VERIFICATION")
    lines.append("Evidence class: STATIC_SOURCE")
    lines.append("Purpose: readable art-production companion to `TextureProductionQueue_SHINOBU_361.csv`.")
    lines.append("")
    lines.append("## Summary")
    lines.append(f"- Unique target textures: {len(queue)}")
    lines.append(f"- Source slot references collapsed: {sum(int(row.get('source_count', 0)) for row in queue)}")
    lines.append("- Prompt contract: natural English only; no weighted, bracketed, or legacy generator syntax.")
    lines.append("- View contract: every prompt requires flat, top-down, orthogonal orthographic view, uniform diffuse lighting, zero directional shadows, and seamless tiling.")
    lines.append("- Dear Lie contract: rivets, seams, salt, pores, membrane veins, cracks, and weld lips are baked into albedo, BC5 normal, and packed ORM maps instead of geometry.")
    lines.append("")
    lines.append("## Category Counts")
    for category, count in sorted(category_counts.items()):
        lines.append(f"- {category}: {count}")
    lines.append("")
    lines.append("## Texture Role Counts")
    for role, count in sorted(role_counts.items()):
        lines.append(f"- {role}: {count}")
    lines.append("")
    lines.append("## Resolution Counts")
    for resolution, count in sorted(resolution_counts.items(), key=lambda item: (int(item[0]) if item[0].isdigit() else 0, item[0])):
        lines.append(f"- {resolution}: {count}")
    lines.append("")
    lines.append("## Action Counts")
    for action, count in sorted(action_counts.items()):
        lines.append(f"- {action}: {count}")
    lines.append("")
    lines.append("## Generation Rules")
    lines.append("- Albedo: generate exactly the prompt paragraph; keep diffuse dark and flat-lit; do not paint directional highlights.")
    lines.append("- Normal: follow the BC5 normal plan; use luminance extraction only for shallow detail and dedicated normal prompts for rivets, grates, bevels, and deep cracks.")
    lines.append("- ORM: pack AO in Red, Roughness in Green, Metallic in Blue; keep linear, mipmapped, and BC7 on Standalone.")
    lines.append("- Android/mobile: import generated maps with ASTC_6x6 unless a platform capture proves a tighter format is required.")
    lines.append("")

    current_category = None
    for index, row in enumerate(queue, start=1):
        category = str(row.get("category", "UNKNOWN"))
        if category != current_category:
            current_category = category
            lines.append("")
            lines.append(f"## {category}")
        lines.append("")
        lines.append(f"### {index:03d}. {Path(str(row.get('target_texture_path', 'unknown'))).name}")
        lines.append("")
        lines.append(f"- Queue ID: `{row.get('queue_id', '')}`")
        lines.append(f"- Action: `{row.get('action', '')}`")
        lines.append(f"- Texture role: `{row.get('texture_role', '')}`")
        lines.append(f"- Priority: `{row.get('priority', '')}`")
        lines.append(f"- Resolution: `{row.get('target_resolution', '')}`")
        lines.append(f"- Source count: `{row.get('source_count', '')}`")
        lines.append(f"- Target path: `{row.get('target_texture_path', '')}`")
        lines.append(f"- Source paths: `{row.get('source_asset_paths', '')}`")
        lines.append(f"- Slots: `{row.get('slots', '')}`")
        lines.append(f"- Reference states: `{row.get('reference_states', '')}`")
        lines.append(f"- Albedo compression: `{row.get('compression_albedo', '')}`")
        lines.append(f"- Normal compression: `{row.get('compression_normal', '')}`")
        lines.append(f"- ORM compression: `{row.get('compression_orm', '')}`")
        lines.append("")
        lines.append("Prompt:")
        lines.append(str(row.get("albedo_prompt", "")))
        lines.append("")
        lines.append("Normal plan:")
        lines.append(str(row.get("normal_plan", "")))
        lines.append("")
        lines.append("ORM plan:")
        lines.append(str(row.get("orm_plan", "")))
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_manifest(path: Path, records: list[SlotRecord]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fields = list(asdict(records[0]).keys()) if records else [field for field in SlotRecord.__dataclass_fields__]
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        for record in records:
            writer.writerow(asdict(record))


def write_markdown(path: Path, report: dict[str, Any], prompts: list[dict[str, Any]]) -> None:
    lines: list[str] = []
    summary = report["summary"]
    lines.append("# SHINOBU_361 Texture Audit and Bake Queue")
    lines.append("")
    lines.append("Status: PENDING VERIFICATION")
    lines.append("Evidence class: STATIC_SOURCE")
    lines.append("")
    lines.append("## Summary")
    for key in (
        "target_files_scanned",
        "audited_slots",
        "deficiency_slots",
        "stub_texture_count",
        "forbidden_format_texture_count",
        "import_issue_texture_count",
        "estimated_missing_texture_vram_mib",
        "texture_budget_mib",
        "texture_budget_status",
    ):
        lines.append(f"- {key}: {summary[key]}")
    lines.append("")
    unique_queue = report.get("unique_texture_queue_summary", {})
    if unique_queue:
        lines.append("## Unique Production Queue")
        lines.append(f"- unique_target_textures: {unique_queue['unique_target_textures']}")
        lines.append(f"- duplicate_slot_references_collapsed: {unique_queue['duplicate_slot_references_collapsed']}")
        lines.append(f"- queue_csv: `{unique_queue['queue_csv']}`")
        lines.append(f"- queue_json: `{unique_queue['queue_json']}`")
        if "queue_readable" in unique_queue:
            lines.append(f"- queue_readable: `{unique_queue['queue_readable']}`")
        lines.append("")
        for title, key in (
            ("Unique Queue Priority Counts", "priority_counts"),
            ("Unique Queue Category Counts", "category_counts"),
            ("Unique Queue Action Counts", "action_counts"),
        ):
            counts = unique_queue.get(key, {})
            if counts:
                lines.append(f"### {title}")
                for name, count in sorted(counts.items()):
                    lines.append(f"- {name}: {count}")
                lines.append("")
    lines.append("## Forensic Category Counts")
    for category, count in sorted(report["category_counts"].items()):
        lines.append(f"- {category}: {count}")
    lines.append("")
    lines.append("## Forensic Priority Counts")
    for priority, count in sorted(report["priority_counts"].items()):
        lines.append(f"- {priority}: {count}")
    lines.append("")
    lines.append("## Production Prompts")
    for entry in prompts:
        lines.append("")
        lines.append(f"### {entry['prompt_id']} {entry['category']}")
        lines.append("")
        lines.append(f"- Source: `{entry['source_asset_path']}`")
        lines.append(f"- Slot: `{entry['slot']}`")
        lines.append(f"- State: `{entry['reference_state']}`")
        lines.append(f"- Target: `{entry['target_texture_path']}`")
        lines.append(f"- Resolution: {entry['target_resolution']}")
        lines.append("")
        lines.append(entry["albedo_prompt"])
        lines.append("")
        lines.append(f"Normal plan: {entry['normal_plan']}")
        lines.append("")
        lines.append(f"ORM plan: {entry['orm_plan']}")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def prompt_syntax_audit(prompts: list[dict[str, Any]]) -> dict[str, Any]:
    banned_patterns = ("--", "::", "[", "]")
    required_phrases = (
        "flat, top-down, orthogonal orthographic view",
        "zero directional shadows",
        "Perfectly seamless",
        "completely uniform overhead illumination",
    )
    failures: list[dict[str, str]] = []
    required_failures: list[dict[str, str]] = []
    for entry in prompts:
        text = entry["albedo_prompt"]
        for pattern in banned_patterns:
            if pattern in text:
                failures.append({"prompt_id": entry["prompt_id"], "pattern": pattern})
        for phrase in required_phrases:
            if phrase not in text:
                required_failures.append({"prompt_id": entry["prompt_id"], "missing_phrase": phrase})
    return {
        "prompt_count": len(prompts),
        "banned_pattern_failures": failures,
        "required_phrase_failures": required_failures,
        "status": "PASS" if not failures and not required_failures else "FAIL",
    }


def manifest_rle_summary(records: list[SlotRecord], manifest_path: Path) -> dict[str, Any]:
    runs = 0
    last_key = None
    for record in records:
        key = (record.category, record.priority, record.target_resolution, record.reference_state)
        if key != last_key:
            runs += 1
            last_key = key
    size = manifest_path.stat().st_size if manifest_path.exists() else 0
    estimated_rle_bytes = runs * 32
    return {
        "source_csv_bytes": size,
        "rle_run_count": runs,
        "estimated_rle_index_bytes": estimated_rle_bytes,
        "runtime_loading_note": "Editor/debug CSV only; runtime parser not introduced.",
    }


def build_report(project_root: Path, asset_root: Path, output_dir: Path) -> tuple[dict[str, Any], list[SlotRecord], list[dict[str, Any]]]:
    guid_map = build_guid_map(project_root / "Assets")
    texture_by_guid: dict[str, TextureInfo] = {}
    for guid, asset_path in guid_map.items():
        if (
            asset_path.suffix.lower() in IMAGE_EXTS
            and asset_root in asset_path.parents
            and not is_generated_lighting_texture(asset_path)
        ):
            texture_by_guid[guid] = inspect_texture(asset_path, project_root, guid)

    slots, file_counts = collect_slot_records(asset_root, project_root, guid_map)
    records = build_records(slots, guid_map, texture_by_guid, project_root)
    prompts = prompt_entries(records)

    category_counts: dict[str, int] = {}
    priority_counts: dict[str, int] = {}
    state_counts: dict[str, int] = {}
    for record in records:
        category_counts[record.category] = category_counts.get(record.category, 0) + 1
        priority_counts[record.priority] = priority_counts.get(record.priority, 0) + 1
        state_counts[record.reference_state] = state_counts.get(record.reference_state, 0) + 1

    deficient_states = {"EMPTY_REQUIRED_SLOT", "MISSING_GUID", "MISSING_EMBEDDED_TEXTURE", "STUB_TEXTURE", "BUILTIN_DEFAULT_TEXTURE", "IMPORT_ISSUE"}
    deficiency_count = sum(1 for record in records if record.reference_state in deficient_states)
    estimated_missing = round(sum(record.estimated_vram_mib for record in records), 3)
    texture_budget = 900.0
    budget_status = "PASS" if estimated_missing <= texture_budget * 0.9 else "WARN" if estimated_missing <= texture_budget else "FAIL"
    forbidden_count = sum(1 for info in texture_by_guid.values() if info.extension in FORBIDDEN_SOURCE_EXTS)
    import_issue_count = sum(1 for info in texture_by_guid.values() if info.import_issues)
    stub_count = sum(1 for info in texture_by_guid.values() if info.is_stub)
    prompt_audit = prompt_syntax_audit(prompts)

    report = {
        "schema": "hecton8.texture_audit_and_bake_director.v1",
        "agent": AGENT_ID,
        "evidenceClass": "STATIC_SOURCE",
        "projectRoot": normalized(project_root),
        "assetRoot": rel(asset_root, project_root),
        "summary": {
            "target_files_scanned": sum(file_counts.values()),
            "target_file_counts": file_counts,
            "texture_assets_scanned": len(texture_by_guid),
            "audited_slots": len(records),
            "deficiency_slots": deficiency_count,
            "prompt_count": len(prompts),
            "stub_texture_count": stub_count,
            "forbidden_format_texture_count": forbidden_count,
            "import_issue_texture_count": import_issue_count,
            "missing_embedded_texture_count": state_counts.get("MISSING_EMBEDDED_TEXTURE", 0),
            "estimated_missing_texture_vram_mib": estimated_missing,
            "texture_budget_mib": texture_budget,
            "texture_budget_status": budget_status,
            "status": "PENDING_VERIFICATION",
        },
        "category_counts": category_counts,
        "priority_counts": priority_counts,
        "reference_state_counts": state_counts,
        "stub_textures": [asdict(info) for info in texture_by_guid.values() if info.is_stub],
        "forbidden_format_textures": [asdict(info) for info in texture_by_guid.values() if info.extension in FORBIDDEN_SOURCE_EXTS or info.import_issues],
        "prompt_syntax_audit": prompt_audit,
        "normal_generation_policy": "Use Tools/MaterialAudit.py after texture import as compatibility audit. Generate BC5 normals from controlled luminance only for fine detail; dedicated normal generation is required for rivets, grates, deep cracks, and panel bevels.",
        "orm_packing_policy": "Prompt convention: _ORM Red equals AO, Green equals Roughness, Blue equals Metallic. Use BC7 linear with mipmaps. Do not ship separate AO, roughness, and metallic texture samplers unless an existing shader ABI forces legacy compatibility.",
        "quality_scaling": {
            "Low": "512 to 1024 maps, aggressive streaming mip bias, baked AO only, no extra detail samplers.",
            "Middle": "1024 broad surfaces, 2048 only for triplanar or near-field blockers, packed ORM.",
            "High": "2048 hero surfaces, richer normal detail, wet/spec masks for flora and cockpit inspection surfaces.",
            "Ultra": "Longer mip residency and optional presentation-only emissive/spec masks, still capped to project max size and still packed.",
        },
        "priority_policy": {
            "BLOCKER": "Immediate cockpit, starting habitat/prologue, terminal, airlock, visor, or HUD-facing rows.",
            "MEDIUM": "Terrain, vegetation, broad habitat/world surfaces, and normal first-party production surfaces.",
            "LOW": "Distant background, skybox, planet, star, panorama, and non-immediate decal/background rows.",
        },
    }
    return report, records, prompts


def main() -> int:
    parser = argparse.ArgumentParser(description="Compile SHINOBU_361 texture audit, prompts, and manifest.")
    parser.add_argument("--project-root", default=".", help="Unity project root.")
    parser.add_argument("--asset-root", default="Assets/_Project", help="First-party asset root to scan.")
    parser.add_argument("--output-dir", default="Docs/Reports", help="Report output directory.")
    parser.add_argument("--manifest", default="Docs/Reports/production_texture_manifest.csv", help="CSV manifest path.")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    asset_root = (project_root / args.asset_root).resolve()
    output_dir = (project_root / args.output_dir).resolve()
    manifest_path = (project_root / args.manifest).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    report, records, prompts = build_report(project_root, asset_root, output_dir)
    unique_queue = build_unique_texture_queue(prompts)
    write_manifest(manifest_path, records)
    report["manifest"] = rel(manifest_path, project_root)
    report["manifest_rle_summary"] = manifest_rle_summary(records, manifest_path)
    json_path = output_dir / "TextureAudit_SHINOBU_361.json"
    markdown_path = output_dir / "TextureAudit_SHINOBU_361.md"
    prompt_json_path = output_dir / "TexturePrompts_SHINOBU_361.json"
    queue_json_path = output_dir / "TextureProductionQueue_SHINOBU_361.json"
    queue_csv_path = output_dir / "TextureProductionQueue_SHINOBU_361.csv"
    queue_readable_path = output_dir / "TextureProductionQueue_SHINOBU_361_READABLE.md"
    report["summary"]["unique_target_texture_count"] = len(unique_queue)
    report["unique_texture_queue_summary"] = {
        "unique_target_textures": len(unique_queue),
        "duplicate_slot_references_collapsed": max(0, len(prompts) - len(unique_queue)),
        "queue_csv": rel(queue_csv_path, project_root),
        "queue_json": rel(queue_json_path, project_root),
        "queue_readable": rel(queue_readable_path, project_root),
        "priority_counts": count_queue_field(unique_queue, "priority"),
        "category_counts": count_queue_field(unique_queue, "category"),
        "action_counts": count_queue_field(unique_queue, "action"),
        "texture_role_counts": count_queue_field(unique_queue, "texture_role"),
        "resolution_counts": count_queue_field(unique_queue, "target_resolution"),
    }
    json_path.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    prompt_json_path.write_text(json.dumps(prompts, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    queue_json_path.write_text(json.dumps(unique_queue, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    write_unique_queue_csv(queue_csv_path, unique_queue)
    write_readable_queue(queue_readable_path, unique_queue)
    write_markdown(markdown_path, report, prompts)

    print("TEXTURE_AUDIT_SHINOBU_361")
    print(f"target_files_scanned={report['summary']['target_files_scanned']}")
    print(f"audited_slots={report['summary']['audited_slots']}")
    print(f"deficiency_slots={report['summary']['deficiency_slots']}")
    print(f"prompt_count={report['summary']['prompt_count']}")
    print(f"unique_target_texture_count={report['summary']['unique_target_texture_count']}")
    print(f"stub_texture_count={report['summary']['stub_texture_count']}")
    print(f"forbidden_format_texture_count={report['summary']['forbidden_format_texture_count']}")
    print(f"estimated_missing_texture_vram_mib={report['summary']['estimated_missing_texture_vram_mib']}")
    print(f"texture_budget_status={report['summary']['texture_budget_status']}")
    print(f"prompt_syntax_status={report['prompt_syntax_audit']['status']}")
    print(f"manifest={rel(manifest_path, project_root)}")
    print(f"json={rel(json_path, project_root)}")
    print(f"markdown={rel(markdown_path, project_root)}")
    return 0 if report["prompt_syntax_audit"]["status"] == "PASS" else 2


if __name__ == "__main__":
    raise SystemExit(main())
