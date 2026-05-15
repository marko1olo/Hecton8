#!/usr/bin/env python3
"""Static VRAM and mesh budget scanner for HECTON-8.

Evidence boundary:
    STATIC_SOURCE / FILESYSTEM only. This tool does not prove Unity import,
    runtime residency, Memory Profiler state, or player-build VRAM usage.
"""

from __future__ import annotations

import argparse
from concurrent.futures import ThreadPoolExecutor
import csv
import datetime as _dt
import json
import os
import re
import struct
import sys
import zlib
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, Dict, Iterable, Iterator, List, Optional, Sequence, Tuple, TypeVar


TEXTURE_EXTS = {".png", ".jpg", ".jpeg", ".tga", ".bmp", ".psd", ".tif", ".tiff", ".dds", ".hdr", ".exr", ".gif"}
MESH_EXTS = {".fbx", ".obj", ".gltf", ".glb"}
RENDER_TEXTURE_EXTS = {".rendertexture"}
SKIP_DIRS = {".git", ".vs", ".codex-build", ".codex-artifacts", "Library", "Temp", "Obj", "Build", "Builds"}
SKIP_DIR_NAMES_LOWER = {name.lower() for name in SKIP_DIRS}
DEFAULT_SCAN_ROOT_NAMES = ("Assets", "Packages", "Data")
JPEG_SOF_MARKERS = {0xC0, 0xC1, 0xC2, 0xC3, 0xC5, 0xC6, 0xC7, 0xC9, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF}
BC7_BYTES_PER_PIXEL = 1.0
FULL_MIP_FACTOR = 4.0 / 3.0
TEXTURE_BUDGET_MIB = 900.0
MX350_HARD_CEILING_MIB = 1800.0
CRITICAL_TEXTURE_POOL_MIB = 1228.8
MAX_TEXTURE_DIM = 2048
LOW_TIER_TARGET_DIM = 1024
MESH_TRI_REDLINE = 50000
MESH_ABSOLUTE_REDLINE = 80000
FBX_SIZE_RISK_BYTES = 10 * 1024 * 1024
GEOMETRY_BUFFER_BUDGET_MIB = 200.0
RENDER_TARGET_BUDGET_MIB = 320.0
STATIC_GEOMETRY_VERTEX_STRIDE_BYTES = 48
STATIC_GEOMETRY_INDEX_BYTES = 4
MESH_GEOMETRY_SINGLE_ASSET_REDLINE_MIB = 16.0
RENDER_TEXTURE_SINGLE_ASSET_REDLINE_MIB = 32.0
TEXTURE_CONTAINER_RISK_EXTS = {".hdr", ".exr", ".psd", ".gif", ".tga", ".tif", ".tiff", ".bmp"}
DEFAULT_AUDIT_WORKERS = 1
MAX_AUDIT_WORKERS = 32
T = TypeVar("T")
U = TypeVar("U")
RENDER_TEXTURE_COLOR_FORMAT_BYTES = {
    0: 4,   # ARGB32
    2: 8,   # ARGBHalf
    4: 2,   # RGB565
    5: 2,   # ARGB4444
    6: 2,   # ARGB1555
    7: 4,   # Default, conservative LDR fallback
    8: 4,   # ARGB2101010
    9: 8,   # DefaultHDR, conservative half fallback
    11: 4,  # RFloat
    12: 4,  # RGFloat
    13: 8,  # RGHalf/RGBA half-family fallback
    14: 16, # ARGBFloat
    15: 1,  # R8
    16: 2,  # RG16
    17: 1,  # RHalf fallback
}
RENDER_TEXTURE_DEPTH_FORMAT_BYTES = {
    0: 0,
    90: 2,
    91: 4,
    92: 4,
    93: 4,
    94: 4,
    95: 4,
}
RENDER_TEXTURE_SOURCE_PATTERNS = (
    ("new RenderTexture", re.compile(r"\bnew\s+RenderTexture\s*\(")),
    ("RenderTextureDescriptor", re.compile(r"\bRenderTextureDescriptor\b")),
    ("RTHandles.Alloc", re.compile(r"\bRTHandles\.Alloc\s*\(")),
    ("RenderTexture.GetTemporary", re.compile(r"\bRenderTexture\.GetTemporary\s*\(")),
    ("GetTemporaryRT", re.compile(r"\bGetTemporaryRT\s*\(")),
)
BROAD_REPORT_COLUMNS = (
    "asset_type",
    "path",
    "extension",
    "width",
    "height",
    "source_mode",
    "meta_max_texture_size",
    "meta_texture_compression",
    "meta_texture_format",
    "meta_streaming_mipmaps",
    "meta_is_readable",
    "meta_texture_type",
    "bc7_bytes",
    "bc7_mib",
    "bc7_full_mip_mib",
    "file_bytes",
    "file_mib",
    "triangles",
    "mesh_geometry_estimate_bytes",
    "mesh_geometry_estimate_mib",
    "lod_detected",
    "mesh_meta_is_readable",
    "mesh_meta_compression",
    "mesh_meta_optimize_mesh",
    "mesh_meta_import_blend_shapes",
    "mesh_meta_add_colliders",
    "mesh_meta_generate_secondary_uv",
    "mesh_meta_keep_quads",
    "rt_color_format",
    "rt_depth_stencil_format",
    "rt_anti_aliasing",
    "rt_mipmap",
    "rt_generate_mips",
    "rt_texture_dimension",
    "rt_volume_depth",
    "rt_dynamic_scale",
    "rt_random_write",
    "rt_estimate_bytes",
    "rt_estimate_mib",
    "redline_flags",
    "atlas_group",
    "recommendation",
    "evidence_class",
)
TEXTURE_REDLINE_COLUMNS = (
    "path",
    "width",
    "height",
    "bc7_full_mip_mib",
    "first_party_production",
    "flags",
    "recommendation",
)
MESH_REDLINE_COLUMNS = (
    "path",
    "file_mib",
    "triangles",
    "geometry_estimate_mib",
    "lod_detected",
    "meta_is_readable",
    "meta_mesh_compression",
    "meta_optimize_mesh",
    "meta_import_blend_shapes",
    "meta_add_colliders",
    "meta_generate_secondary_uv",
    "meta_keep_quads",
    "flags",
    "recommendation",
)
RENDER_TEXTURE_REDLINE_COLUMNS = (
    "path",
    "width",
    "height",
    "estimate_mib",
    "color_format",
    "depth_stencil_format",
    "anti_aliasing",
    "mipmap",
    "random_write",
    "flags",
    "recommendation",
)
RENDER_TEXTURE_HOTSPOT_COLUMNS = (
    "path",
    "line",
    "pattern",
    "editor_only",
    "profiler_priority",
    "snippet",
    "required_action",
    "evidence_class",
)


@dataclass
class TextureRecord:
    path: Path
    width: int = 0
    height: int = 0
    mode: str = "UNKNOWN"
    error: str = ""
    meta_max_texture_size: str = ""
    meta_texture_compression: str = ""
    meta_texture_format: str = ""
    meta_streaming_mipmaps: str = ""
    meta_is_readable: str = ""
    meta_texture_type: str = ""
    bc7_bytes: int = 0
    flags: List[str] = field(default_factory=list)
    atlas_group: str = ""
    recommendation: str = ""


@dataclass
class MeshRecord:
    path: Path
    file_bytes: int = 0
    triangles: Optional[int] = None
    estimated_geometry_bytes: int = 0
    lod_detected: bool = False
    meta_is_readable: str = ""
    meta_mesh_compression: str = ""
    meta_optimize_mesh: str = ""
    meta_import_blend_shapes: str = ""
    meta_add_colliders: str = ""
    meta_generate_secondary_uv: str = ""
    meta_keep_quads: str = ""
    flags: List[str] = field(default_factory=list)
    recommendation: str = ""


@dataclass
class RenderTextureRecord:
    path: Path
    width: int = 0
    height: int = 0
    color_format: str = ""
    depth_stencil_format: str = ""
    anti_aliasing: int = 1
    mipmap: str = ""
    generate_mips: str = ""
    texture_dimension: str = ""
    volume_depth: int = 1
    dynamic_scale: str = ""
    random_write: str = ""
    estimated_bytes: int = 0
    flags: List[str] = field(default_factory=list)
    recommendation: str = ""


@dataclass
class RenderTextureSourceHit:
    path: Path
    line: int
    pattern: str
    snippet: str
    editor_only: bool = False


def rel(path: Path, root: Path) -> str:
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return path.as_posix()


def is_runtime_candidate(path: Path, root: Path) -> bool:
    value = rel(path, root).replace("\\", "/")
    return value.startswith("Assets/") or value.startswith("Packages/") or value.startswith("Data/")


def is_first_party_production_candidate(path: Path, root: Path) -> bool:
    value = rel(path, root).replace("\\", "/")
    return value.startswith("Assets/_Project/") or value.startswith("Data/")


def resolve_scan_roots(root: Path) -> List[Path]:
    scan_roots = [root / name for name in DEFAULT_SCAN_ROOT_NAMES if (root / name).exists()]
    if scan_roots:
        return scan_roots
    return [root]


def iter_asset_and_link_paths(root: Path) -> Tuple[List[Path], List[Path], List[Path], List[Path]]:
    textures: List[Path] = []
    meshes: List[Path] = []
    render_textures: List[Path] = []
    link_xml_paths: List[Path] = []
    for scan_root in resolve_scan_roots(root):
        for current_root, dirs, files in os.walk(scan_root):
            dirs[:] = [d for d in dirs if d.lower() not in SKIP_DIR_NAMES_LOWER]
            current = Path(current_root)
            for filename in files:
                path = current / filename
                ext = path.suffix.lower()
                if ext in TEXTURE_EXTS:
                    textures.append(path)
                elif ext in MESH_EXTS:
                    meshes.append(path)
                elif ext in RENDER_TEXTURE_EXTS:
                    render_textures.append(path)
                elif filename.lower() == "link.xml":
                    link_xml_paths.append(path)
    textures.sort(key=lambda p: rel(p, root).lower())
    meshes.sort(key=lambda p: rel(p, root).lower())
    render_textures.sort(key=lambda p: rel(p, root).lower())
    link_xml_paths.sort(key=lambda p: rel(p, root).lower())
    return textures, meshes, render_textures, link_xml_paths


def iter_assets(root: Path) -> Tuple[List[Path], List[Path], List[Path]]:
    textures, meshes, render_textures, _link_xml_paths = iter_asset_and_link_paths(root)
    return textures, meshes, render_textures


def normalize_worker_count(value: int) -> int:
    if value <= 0:
        return DEFAULT_AUDIT_WORKERS
    return max(1, min(value, MAX_AUDIT_WORKERS))


def ordered_parallel_map(function: Callable[[T], U], items: Sequence[T], workers: int) -> List[U]:
    if workers <= 1 or len(items) <= 1:
        return [function(item) for item in items]
    with ThreadPoolExecutor(max_workers=workers) as executor:
        return list(executor.map(function, items))


def read_png_size(path: Path) -> Tuple[int, int, str]:
    with path.open("rb") as handle:
        header = handle.read(33)
    if len(header) < 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("invalid png header")
    width, height = struct.unpack(">II", header[16:24])
    color_type = header[25] if len(header) > 25 else 255
    mode = {
        0: "L",
        2: "RGB",
        3: "P",
        4: "LA",
        6: "RGBA",
    }.get(color_type, f"PNG_COLOR_{color_type}")
    return width, height, mode


def read_jpeg_size(path: Path) -> Tuple[int, int, str]:
    with path.open("rb") as handle:
        if handle.read(2) != b"\xff\xd8":
            raise ValueError("invalid jpeg header")
        while True:
            prefix = handle.read(1)
            while prefix and prefix != b"\xff":
                prefix = handle.read(1)
            if not prefix:
                break
            marker_byte = handle.read(1)
            while marker_byte == b"\xff":
                marker_byte = handle.read(1)
            if not marker_byte:
                break
            marker = marker_byte[0]
            if marker == 0x00 or marker in (0xD8, 0xD9) or 0xD0 <= marker <= 0xD7:
                continue
            length_bytes = handle.read(2)
            if len(length_bytes) != 2:
                break
            length = struct.unpack(">H", length_bytes)[0]
            if length < 2:
                raise ValueError("bad jpeg segment length")
            payload_length = length - 2
            if marker in JPEG_SOF_MARKERS:
                header = handle.read(min(payload_length, 6))
                if len(header) < 5:
                    break
                height, width = struct.unpack(">HH", header[1:5])
                components = header[5] if len(header) > 5 else 3
                mode = "RGB" if components >= 3 else "L"
                return width, height, mode
            handle.seek(payload_length, os.SEEK_CUR)
    raise ValueError("jpeg dimensions not found")


def read_tga_size(path: Path) -> Tuple[int, int, str]:
    with path.open("rb") as handle:
        header = handle.read(18)
    if len(header) < 18:
        raise ValueError("invalid tga header")
    width = struct.unpack_from("<H", header, 12)[0]
    height = struct.unpack_from("<H", header, 14)[0]
    depth = header[16]
    mode = "RGBA" if depth >= 32 else "RGB" if depth >= 24 else f"TGA_{depth}"
    return width, height, mode


def read_bmp_size(path: Path) -> Tuple[int, int, str]:
    with path.open("rb") as handle:
        header = handle.read(30)
    if len(header) < 30 or header[:2] != b"BM":
        raise ValueError("invalid bmp header")
    width = abs(struct.unpack_from("<i", header, 18)[0])
    height = abs(struct.unpack_from("<i", header, 22)[0])
    depth = struct.unpack_from("<H", header, 28)[0]
    mode = "RGBA" if depth >= 32 else "RGB" if depth >= 24 else f"BMP_{depth}"
    return width, height, mode


def read_psd_size(path: Path) -> Tuple[int, int, str]:
    with path.open("rb") as handle:
        header = handle.read(26)
    if len(header) < 26 or header[:4] != b"8BPS":
        raise ValueError("invalid psd header")
    channels = struct.unpack_from(">H", header, 12)[0]
    height = struct.unpack_from(">I", header, 14)[0]
    width = struct.unpack_from(">I", header, 18)[0]
    return width, height, f"PSD_{channels}CH"


def read_dds_size(path: Path) -> Tuple[int, int, str]:
    with path.open("rb") as handle:
        header = handle.read(128)
    if len(header) < 128 or header[:4] != b"DDS ":
        raise ValueError("invalid dds header")
    height = struct.unpack_from("<I", header, 12)[0]
    width = struct.unpack_from("<I", header, 16)[0]
    return width, height, "DDS"


def read_gif_size(path: Path) -> Tuple[int, int, str]:
    with path.open("rb") as handle:
        header = handle.read(10)
    if len(header) < 10 or header[:6] not in (b"GIF87a", b"GIF89a"):
        raise ValueError("invalid gif header")
    width = struct.unpack_from("<H", header, 6)[0]
    height = struct.unpack_from("<H", header, 8)[0]
    return width, height, "GIF"


def read_hdr_size(path: Path) -> Tuple[int, int, str]:
    with path.open("rb") as handle:
        for _ in range(80):
            line = handle.readline(256)
            if not line:
                break
            text = line.decode("ascii", errors="ignore").strip()
            match = re.search(r"([+-])Y\s+(\d+)\s+([+-])X\s+(\d+)", text)
            if match:
                height = int(match.group(2))
                width = int(match.group(4))
                return width, height, "HDR"
    raise ValueError("hdr dimensions not found")


def read_tiff_size(path: Path) -> Tuple[int, int, str]:
    with path.open("rb") as handle:
        header = handle.read(4096)
    if len(header) < 8:
        raise ValueError("invalid tiff header")
    if header[:2] == b"II":
        endian = "<"
    elif header[:2] == b"MM":
        endian = ">"
    else:
        raise ValueError("invalid tiff endian")
    if struct.unpack_from(endian + "H", header, 2)[0] != 42:
        raise ValueError("unsupported tiff header")
    ifd_offset = struct.unpack_from(endian + "I", header, 4)[0]
    if ifd_offset + 2 > len(header):
        raise ValueError("tiff ifd outside header window")
    tag_count = struct.unpack_from(endian + "H", header, ifd_offset)[0]
    width = 0
    height = 0
    offset = ifd_offset + 2
    for _ in range(tag_count):
        if offset + 12 > len(header):
            break
        tag = struct.unpack_from(endian + "H", header, offset)[0]
        field_type = struct.unpack_from(endian + "H", header, offset + 2)[0]
        count = struct.unpack_from(endian + "I", header, offset + 4)[0]
        value_offset = offset + 8
        if count == 1 and tag in (256, 257):
            if field_type == 3:
                value = struct.unpack_from(endian + "H", header, value_offset)[0]
            elif field_type == 4:
                value = struct.unpack_from(endian + "I", header, value_offset)[0]
            else:
                value = 0
            if tag == 256:
                width = value
            else:
                height = value
        offset += 12
    if width <= 0 or height <= 0:
        raise ValueError("tiff dimensions not found")
    return width, height, "TIFF"


def read_exr_size(path: Path) -> Tuple[int, int, str]:
    with path.open("rb") as handle:
        data = handle.read(8192)
    if len(data) < 16 or data[:4] != b"\x76\x2f\x31\x01":
        raise ValueError("invalid exr header")
    offset = 8
    while offset < len(data):
        name_end = data.find(b"\x00", offset)
        if name_end < 0:
            break
        if name_end == offset:
            break
        name = data[offset:name_end].decode("ascii", errors="ignore")
        offset = name_end + 1
        type_end = data.find(b"\x00", offset)
        if type_end < 0 or type_end + 5 > len(data):
            break
        attr_type = data[offset:type_end].decode("ascii", errors="ignore")
        offset = type_end + 1
        size = struct.unpack_from("<I", data, offset)[0]
        offset += 4
        if offset + size > len(data):
            break
        if name == "dataWindow" and attr_type == "box2i" and size >= 16:
            min_x, min_y, max_x, max_y = struct.unpack_from("<iiii", data, offset)
            return max_x - min_x + 1, max_y - min_y + 1, "EXR"
        offset += size
    raise ValueError("exr dataWindow not found")


def read_image_size(path: Path) -> Tuple[int, int, str]:
    ext = path.suffix.lower()
    if ext == ".png":
        return read_png_size(path)
    if ext in (".jpg", ".jpeg"):
        return read_jpeg_size(path)
    if ext == ".tga":
        return read_tga_size(path)
    if ext == ".bmp":
        return read_bmp_size(path)
    if ext == ".psd":
        return read_psd_size(path)
    if ext in (".tif", ".tiff"):
        return read_tiff_size(path)
    if ext == ".dds":
        return read_dds_size(path)
    if ext == ".hdr":
        return read_hdr_size(path)
    if ext == ".exr":
        return read_exr_size(path)
    if ext == ".gif":
        return read_gif_size(path)
    raise ValueError(f"unsupported image type {ext}")


def parse_meta_fields(path: Path) -> Tuple[str, str, str, str, str, str]:
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        return "", "", "", "", "", ""
    try:
        text = meta.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return "", "", "", "", "", ""
    max_sizes = sorted(set(re.findall(r"\bmaxTextureSize:\s*([-0-9]+)", text)))
    compressions = sorted(set(re.findall(r"\btextureCompression:\s*([-0-9]+)", text)))
    formats = sorted(set(re.findall(r"\btextureFormat:\s*([-0-9]+)", text)))
    streaming = sorted(set(re.findall(r"\bstreamingMipmaps:\s*([-0-9]+)", text)))
    readable = sorted(set(re.findall(r"\bisReadable:\s*([-0-9]+)", text)))
    texture_type = sorted(set(re.findall(r"\btextureType:\s*([-0-9]+)", text)))
    return "|".join(max_sizes), "|".join(compressions), "|".join(formats), "|".join(streaming), "|".join(readable), "|".join(texture_type)


def parse_mesh_meta_fields(path: Path) -> Tuple[str, str, str, str, str, str, str]:
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        return "", "", "", "", "", "", ""
    try:
        text = meta.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return "", "", "", "", "", "", ""
    readable = sorted(set(re.findall(r"\bisReadable:\s*([-0-9]+)", text)))
    compression = sorted(set(re.findall(r"\bmeshCompression:\s*([-0-9]+)", text)))
    optimize = sorted(set(re.findall(r"\boptimizeMesh:\s*([-0-9]+)", text)))
    blend_shapes = sorted(set(re.findall(r"\bimportBlendShapes:\s*([-0-9]+)", text)))
    colliders = sorted(set(re.findall(r"\baddColliders:\s*([-0-9]+)", text)))
    secondary_uv = sorted(set(re.findall(r"\bgenerateSecondaryUV:\s*([-0-9]+)", text)))
    keep_quads = sorted(set(re.findall(r"\bkeepQuads:\s*([-0-9]+)", text)))
    return (
        "|".join(readable),
        "|".join(compression),
        "|".join(optimize),
        "|".join(blend_shapes),
        "|".join(colliders),
        "|".join(secondary_uv),
        "|".join(keep_quads),
    )


def meta_contains(meta_value: str, expected: str) -> bool:
    return expected in {part for part in meta_value.split("|") if part}


def has_texture_container_risk(record: TextureRecord) -> bool:
    return any(flag.endswith("_SOURCE_CONTAINER_STATIC_SUSPECT") or flag == "HDR_TEXTURE_CONTAINER_STATIC_SUSPECT" for flag in record.flags)


def classify_texture(
    path: Path,
    width: int,
    height: int,
    mode: str,
    meta_max: str,
    meta_compression: str,
    meta_format: str,
    meta_streaming: str,
    meta_readable: str,
) -> Tuple[List[str], str]:
    flags: List[str] = []
    max_dim = max(width, height)
    lower_name = path.name.lower()
    ext = path.suffix.lower()
    if width <= 0 or height <= 0:
        flags.append("VRAM CRIME: TEXTURE_DIMENSIONS_UNREADABLE")
    if ext in (".hdr", ".exr"):
        flags.append("HDR_TEXTURE_CONTAINER_STATIC_SUSPECT")
    elif ext == ".psd":
        flags.append("PSD_SOURCE_CONTAINER_STATIC_SUSPECT")
    elif ext == ".gif":
        flags.append("GIF_SOURCE_CONTAINER_STATIC_SUSPECT")
    elif ext == ".tga":
        flags.append("TGA_SOURCE_CONTAINER_STATIC_SUSPECT")
    elif ext in (".tif", ".tiff"):
        flags.append("TIFF_SOURCE_CONTAINER_STATIC_SUSPECT")
    elif ext == ".bmp":
        flags.append("BMP_SOURCE_CONTAINER_STATIC_SUSPECT")
    if max_dim > MAX_TEXTURE_DIM:
        flags.append("VRAM CRIME: TEXTURE_GT_2048")
    if meta_max:
        try:
            if max(int(part) for part in meta_max.split("|") if part) > MAX_TEXTURE_DIM:
                flags.append("VRAM CRIME: IMPORT_MAX_GT_2048")
        except ValueError:
            flags.append("IMPORT_MAX_PARSE_FAILED")
    else:
        flags.append("MISSING_META_IMPORT_UNKNOWN")
    if "0" in meta_compression.split("|") and mode in ("RGBA", "LA", "P"):
        flags.append("VRAM CRIME: UNCOMPRESSED_RGBA32_STATIC_SUSPECT")
    if meta_format and any(item in {"4", "5", "17", "18"} for item in meta_format.split("|")):
        flags.append("VRAM CRIME: RGBA32_TEXTURE_FORMAT_STATIC_SUSPECT")
    if max_dim > LOW_TIER_TARGET_DIM and meta_streaming and not meta_contains(meta_streaming, "1"):
        flags.append("STREAMING_MIPMAPS_OFF_LARGE")
    if max_dim > LOW_TIER_TARGET_DIM and meta_readable and meta_contains(meta_readable, "1"):
        flags.append("READ_WRITE_ENABLED_LARGE_STATIC_SUSPECT")
    if ext in (".hdr", ".exr"):
        recommendation = "Verify HDR import format/probe or skybox residency; keep only tier-gated or baked proof for MX350."
    elif ext == ".psd":
        recommendation = "Flatten/export production texture and import compressed; keep PSD source out of runtime payload."
    elif ext == ".gif":
        recommendation = "Convert GIF to explicit sprite sheet/texture or quarantine; prove runtime importer behavior."
    elif ext in (".tga", ".tif", ".tiff", ".bmp"):
        recommendation = "Verify production import compression; convert to standard compressed source if kept in runtime payload."
    elif "normal" in lower_name or "_norm" in lower_name or lower_name.endswith("_n.png"):
        recommendation = "Use BC5 normal import; Low tier cap 1024 unless hero close-read asset."
    elif any(token in lower_name for token in ("ao", "rough", "smooth", "metal", "spec", "mask")):
        recommendation = "Channel-pack masks into one RGBA texture; avoid separate AO/spec maps."
    elif max_dim > LOW_TIER_TARGET_DIM:
        recommendation = "Low tier should halve source/import max; keep high mips only with hero or streaming proof."
    elif ext in (".jpg", ".jpeg"):
        recommendation = "Verify Unity import compression; JPG disk compression does not reduce VRAM."
    else:
        recommendation = "Keep compressed; atlas if grouped with small sibling textures."
    return flags, recommendation


def audit_texture_record(path: Path) -> TextureRecord:
    record = TextureRecord(path=path)
    try:
        record.width, record.height, record.mode = read_image_size(path)
        record.bc7_bytes = int(record.width * record.height * BC7_BYTES_PER_PIXEL)
    except Exception as exc:  # noqa: BLE001 - report must keep scanning.
        record.error = str(exc)
        record.flags.append("VRAM CRIME: TEXTURE_PARSE_FAILED")
    (
        record.meta_max_texture_size,
        record.meta_texture_compression,
        record.meta_texture_format,
        record.meta_streaming_mipmaps,
        record.meta_is_readable,
        record.meta_texture_type,
    ) = parse_meta_fields(path)
    if not record.error:
        flags, recommendation = classify_texture(
            path,
            record.width,
            record.height,
            record.mode,
            record.meta_max_texture_size,
            record.meta_texture_compression,
            record.meta_texture_format,
            record.meta_streaming_mipmaps,
            record.meta_is_readable,
        )
        record.flags.extend(flags)
        record.recommendation = recommendation
    else:
        record.recommendation = "Open in Unity importer or asset tool; dimensions unavailable to static scanner."
    return record


def audit_textures(paths: Sequence[Path], root: Path, workers: int = 1) -> List[TextureRecord]:
    records = ordered_parallel_map(audit_texture_record, paths, normalize_worker_count(workers))
    assign_atlas_groups(records, root)
    return records


def small_texture_group_key(record: TextureRecord, root: Path) -> Optional[str]:
    if record.width <= 0 or record.height <= 0:
        return None
    if max(record.width, record.height) > 1024:
        return None
    if not is_runtime_candidate(record.path, root):
        return None
    value = rel(record.path, root).replace("\\", "/").lower()
    excluded_fragments = (
        "/editor/",
        "assets/plugins/",
        "assets/screenshots/",
        "assets/ast pathfindingproject/",
        "assets/astarpathfindingproject/",
        "assets/demigiant/",
        "assets/realtimecsg/",
        "assets/editor default resources/",
        "packages/com.unity.",
    )
    if any(fragment in value for fragment in excluded_fragments):
        return None
    return rel(record.path.parent, root).replace("\\", "/")


def assign_atlas_groups(records: List[TextureRecord], root: Path) -> List[Tuple[str, List[TextureRecord], int]]:
    groups: Dict[str, List[TextureRecord]] = {}
    for record in records:
        key = small_texture_group_key(record, root)
        if key is None:
            continue
        groups.setdefault(key, []).append(record)
    ranked: List[Tuple[str, List[TextureRecord], int]] = []
    for key, items in groups.items():
        if len(items) < 2:
            continue
        area = sum(item.width * item.height for item in items)
        ranked.append((key, items, area))
    def priority(entry: Tuple[str, List[TextureRecord], int]) -> Tuple[int, int, int]:
        key, items, area = entry
        normalized = key.lower().replace("\\", "/")
        if normalized.startswith("assets/_project/"):
            owner_rank = 0
        elif normalized.startswith("assets/scififacility/"):
            owner_rank = 1
        elif normalized.startswith("assets/"):
            owner_rank = 2
        elif normalized.startswith("data/"):
            owner_rank = 3
        else:
            owner_rank = 4
        return owner_rank, -len(items), -area

    ranked.sort(key=priority)
    for key, items, _area in ranked[:5]:
        for item in items:
            item.atlas_group = key
            if "atlas" not in item.recommendation.lower():
                item.recommendation += " Atlas with sibling material maps."
    return ranked[:5]


def count_obj_triangles(path: Path) -> Optional[int]:
    triangles = 0
    try:
        with path.open("r", encoding="utf-8", errors="replace") as handle:
            for line in handle:
                stripped = line.lstrip()
                if not stripped.startswith("f "):
                    continue
                vertex_count = len(stripped.split()) - 1
                if vertex_count >= 3:
                    triangles += vertex_count - 2
    except OSError:
        return None
    return triangles


def count_triangles_from_indices(values: Iterable[int]) -> int:
    triangles = 0
    vertices_in_face = 0
    for value in values:
        vertices_in_face += 1
        if value < 0:
            if vertices_in_face >= 3:
                triangles += vertices_in_face - 2
            vertices_in_face = 0
    return triangles


def estimate_geometry_bytes(triangles: Optional[int]) -> int:
    if triangles is None or triangles <= 0:
        return 0
    vertex_count_estimate = triangles * 3
    return int(vertex_count_estimate * (STATIC_GEOMETRY_VERTEX_STRIDE_BYTES + STATIC_GEOMETRY_INDEX_BYTES))


def iter_int32_values(raw: bytes) -> Iterator[int]:
    count = len(raw) // 4
    for index in range(count):
        yield struct.unpack_from("<i", raw, index * 4)[0]


def count_fbx_binary_triangles(data: bytes) -> Optional[int]:
    if not data.startswith(b"Kaydara FBX Binary  \x00\x1a\x00"):
        return None
    if len(data) < 27:
        return None
    version = struct.unpack_from("<I", data, 23)[0]
    wide = version >= 7500
    null_record_size = 25 if wide else 13
    triangles = 0

    def read_uint(offset: int) -> Tuple[int, int]:
        if wide:
            if offset + 8 > len(data):
                return 0, len(data)
            return struct.unpack_from("<Q", data, offset)[0], offset + 8
        if offset + 4 > len(data):
            return 0, len(data)
        return struct.unpack_from("<I", data, offset)[0], offset + 4

    def skip_property(offset: int, node_name: bytes) -> Tuple[int, int]:
        nonlocal triangles
        if offset >= len(data):
            return offset, 0
        code = chr(data[offset])
        offset += 1
        if code == "Y":
            return offset + 2, 0
        if code in ("C",):
            return offset + 1, 0
        if code in ("I", "F"):
            return offset + 4, 0
        if code in ("D", "L"):
            return offset + 8, 0
        if code in ("S", "R"):
            if offset + 4 > len(data):
                return len(data), 0
            length = struct.unpack_from("<I", data, offset)[0]
            return offset + 4 + length, 0
        if code in ("f", "d", "l", "i", "b"):
            if offset + 12 > len(data):
                return len(data), 0
            array_len, encoding, compressed_len = struct.unpack_from("<III", data, offset)
            offset += 12
            raw = data[offset:offset + compressed_len]
            offset += compressed_len
            if node_name == b"PolygonVertexIndex" and code == "i":
                if encoding == 1:
                    try:
                        raw = zlib.decompress(raw)
                    except zlib.error:
                        return offset, 0
                elif encoding != 0:
                    return offset, 0
                expected_len = array_len * 4
                raw = raw[:expected_len]
                add = count_triangles_from_indices(iter_int32_values(raw))
                triangles += add
                return offset, add
            return offset, 0
        return offset, 0

    def walk(offset: int, limit: int) -> int:
        while offset + null_record_size <= limit and offset < len(data):
            start = offset
            end_offset, offset = read_uint(offset)
            property_count, offset = read_uint(offset)
            _property_len, offset = read_uint(offset)
            if offset >= len(data):
                return len(data)
            name_len = data[offset]
            offset += 1
            if end_offset == 0 and property_count == 0 and name_len == 0:
                return start + null_record_size
            if offset + name_len > len(data) or end_offset <= start or end_offset > len(data):
                return len(data)
            node_name = data[offset:offset + name_len]
            offset += name_len
            for _ in range(int(property_count)):
                offset, _ = skip_property(offset, node_name)
                if offset > len(data):
                    return len(data)
            child_limit = int(end_offset) - null_record_size
            if offset < child_limit:
                offset = walk(offset, child_limit)
            offset = int(end_offset)
        return offset

    walk(27, len(data))
    return triangles if triangles > 0 else None


def count_fbx_ascii_triangles(text: str) -> Optional[int]:
    total = 0
    found = False
    pattern = re.compile(r"PolygonVertexIndex:\s*\*\d+\s*\{\s*a:\s*([^}]*)\}", re.IGNORECASE | re.DOTALL)
    for match in pattern.finditer(text):
        found = True
        values = (int(item) for item in re.findall(r"-?\d+", match.group(1)))
        total += count_triangles_from_indices(values)
    return total if found else None


def count_fbx_triangles(path: Path) -> Optional[int]:
    try:
        data = path.read_bytes()
    except OSError:
        return None
    binary_count = count_fbx_binary_triangles(data)
    if binary_count is not None:
        return binary_count
    try:
        text = data.decode("utf-8", errors="replace")
    except UnicodeDecodeError:
        return None
    return count_fbx_ascii_triangles(text)


def as_non_negative_int(value: object) -> Optional[int]:
    try:
        number = int(value)  # type: ignore[arg-type]
    except (TypeError, ValueError):
        return None
    if number < 0:
        return None
    return number


def gltf_accessor_count(accessors: object, index: object) -> Optional[int]:
    accessor_index = as_non_negative_int(index)
    if accessor_index is None or not isinstance(accessors, list) or accessor_index >= len(accessors):
        return None
    accessor = accessors[accessor_index]
    if not isinstance(accessor, dict):
        return None
    return as_non_negative_int(accessor.get("count"))


def gltf_primitive_triangle_count(primitive: object, accessors: object) -> Optional[int]:
    if not isinstance(primitive, dict):
        return None
    mode = as_non_negative_int(primitive.get("mode", 4))
    if mode is None:
        return None
    if mode not in (4, 5, 6):
        return 0
    if "indices" in primitive:
        vertex_count = gltf_accessor_count(accessors, primitive.get("indices"))
    else:
        attributes = primitive.get("attributes")
        position_index = attributes.get("POSITION") if isinstance(attributes, dict) else None
        vertex_count = gltf_accessor_count(accessors, position_index)
    if vertex_count is None:
        return None
    if mode == 4:
        return vertex_count // 3
    if vertex_count < 3:
        return 0
    return vertex_count - 2


def count_gltf_document_triangles(document: object) -> Optional[int]:
    if not isinstance(document, dict):
        return None
    accessors = document.get("accessors")
    meshes = document.get("meshes")
    if not isinstance(meshes, list):
        return None
    total = 0
    triangle_primitive_seen = False
    for mesh in meshes:
        if not isinstance(mesh, dict):
            continue
        primitives = mesh.get("primitives")
        if not isinstance(primitives, list):
            continue
        for primitive in primitives:
            if isinstance(primitive, dict):
                mode = as_non_negative_int(primitive.get("mode", 4))
                if mode not in (4, 5, 6):
                    continue
            triangle_primitive_seen = True
            triangle_count = gltf_primitive_triangle_count(primitive, accessors)
            if triangle_count is None:
                return None
            total += triangle_count
    return total if triangle_primitive_seen else 0


def read_glb_json_document(path: Path) -> Optional[object]:
    try:
        with path.open("rb") as handle:
            header = handle.read(12)
            if len(header) != 12:
                return None
            magic, version, total_length = struct.unpack("<III", header)
            if magic != 0x46546C67 or version != 2 or total_length < 20:
                return None
            bytes_read = 12
            while bytes_read + 8 <= total_length:
                chunk_header = handle.read(8)
                if len(chunk_header) != 8:
                    return None
                chunk_length, chunk_type = struct.unpack("<II", chunk_header)
                bytes_read += 8
                if chunk_type == 0x4E4F534A:
                    payload = handle.read(chunk_length)
                    if len(payload) != chunk_length:
                        return None
                    return json.loads(payload.decode("utf-8", errors="replace").rstrip("\x00 \t\r\n"))
                handle.seek(chunk_length, os.SEEK_CUR)
                bytes_read += chunk_length
    except (OSError, json.JSONDecodeError, UnicodeDecodeError, struct.error):
        return None
    return None


def count_gltf_triangles(path: Path) -> Optional[int]:
    ext = path.suffix.lower()
    if ext == ".glb":
        document = read_glb_json_document(path)
    else:
        try:
            document = json.loads(path.read_text(encoding="utf-8", errors="replace"))
        except (OSError, json.JSONDecodeError, UnicodeDecodeError):
            return None
    return count_gltf_document_triangles(document)


LOD_RE = re.compile(r"(^|[_\-. ])lod[_\-. ]?([0-9])($|[_\-. ])", re.IGNORECASE)


def mesh_lod_key(path: Path) -> str:
    stem = path.stem.lower()
    stem = re.sub(r"(^|[_\-. ])lod[_\-. ]?[0-9]($|[_\-. ])", "_", stem)
    stem = re.sub(r"[_\-. ]+", "_", stem).strip("_")
    return f"{path.parent.as_posix().lower()}/{stem}"


def build_lod_map(paths: Sequence[Path]) -> Dict[str, int]:
    result: Dict[str, int] = {}
    for path in paths:
        key = mesh_lod_key(path)
        if LOD_RE.search(path.stem):
            result[key] = result.get(key, 0) + 1
        else:
            result.setdefault(key, 0)
    return result


def detect_lod(path: Path, lod_map: Dict[str, int]) -> bool:
    if LOD_RE.search(path.stem):
        return True
    if "lod" in path.parent.as_posix().lower():
        return True
    return lod_map.get(mesh_lod_key(path), 0) >= 2


def append_mesh_import_flags(record: MeshRecord) -> None:
    if record.meta_is_readable and meta_contains(record.meta_is_readable, "1"):
        record.flags.append("MESH_READ_WRITE_ENABLED_STATIC_SUSPECT")
    if record.meta_mesh_compression and meta_contains(record.meta_mesh_compression, "0"):
        record.flags.append("MESH_COMPRESSION_OFF_STATIC_SUSPECT")
    if record.meta_import_blend_shapes and meta_contains(record.meta_import_blend_shapes, "1"):
        record.flags.append("MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT")
    if record.meta_add_colliders and meta_contains(record.meta_add_colliders, "1"):
        record.flags.append("MESH_IMPORT_COLLIDERS_ENABLED_STATIC_SUSPECT")
    if record.meta_keep_quads and meta_contains(record.meta_keep_quads, "1"):
        record.flags.append("MESH_KEEP_QUADS_ENABLED_STATIC_SUSPECT")


def audit_mesh_record(path: Path, lod_map: Dict[str, int]) -> MeshRecord:
    record = MeshRecord(path=path)
    (
        record.meta_is_readable,
        record.meta_mesh_compression,
        record.meta_optimize_mesh,
        record.meta_import_blend_shapes,
        record.meta_add_colliders,
        record.meta_generate_secondary_uv,
        record.meta_keep_quads,
    ) = parse_mesh_meta_fields(path)
    try:
        record.file_bytes = path.stat().st_size
    except OSError:
        record.file_bytes = 0
    record.lod_detected = detect_lod(path, lod_map)
    ext = path.suffix.lower()
    if ext == ".obj":
        record.triangles = count_obj_triangles(path)
    elif ext == ".fbx":
        record.triangles = count_fbx_triangles(path)
    elif ext in (".gltf", ".glb"):
        record.triangles = count_gltf_triangles(path)
    record.estimated_geometry_bytes = estimate_geometry_bytes(record.triangles)
    if record.triangles is None:
        record.flags.append("TRIANGLE_COUNT_UNREADABLE_STATIC")
        if record.file_bytes > FBX_SIZE_RISK_BYTES and not record.lod_detected:
            record.flags.append("MESH_SIZE_RISK_NO_LOD_STATIC")
    else:
        if mib(record.estimated_geometry_bytes) > MESH_GEOMETRY_SINGLE_ASSET_REDLINE_MIB:
            record.flags.append("MESH_GEOMETRY_ESTIMATE_GT_16MIB_STATIC")
        if record.triangles > MESH_ABSOLUTE_REDLINE:
            record.flags.append("MESH_GT_80K_ABSOLUTE_STATIC")
        if record.triangles > MESH_TRI_REDLINE and not record.lod_detected:
            record.flags.append("MESH_REDLINE_GT_50K_NO_LOD")
    append_mesh_import_flags(record)
    has_lod_redline = "MESH_REDLINE_GT_50K_NO_LOD" in record.flags or "MESH_SIZE_RISK_NO_LOD_STATIC" in record.flags
    has_import_risk = any(flag.endswith("_STATIC_SUSPECT") for flag in record.flags)
    if has_lod_redline:
        record.recommendation = "Add LOD0/LOD1/LOD2 or cull/impostor path before production visibility beyond 20m."
    elif record.triangles is not None and record.triangles > MESH_TRI_REDLINE:
        record.recommendation = "Keep LOD chain; verify LOD1 <= 50 percent and LOD2 <= 25 percent of LOD0."
    elif has_import_risk:
        record.recommendation = "Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists."
    elif not record.lod_detected:
        record.recommendation = "Verify size and production visibility; props above 0.5m need LOD/cull or impostor proof."
    else:
        record.recommendation = "Static mesh budget requires Unity import/readback for final proof."
    return record


def audit_meshes(paths: Sequence[Path], workers: int = 1) -> List[MeshRecord]:
    lod_map = build_lod_map(paths)

    def build_record(path: Path) -> MeshRecord:
        return audit_mesh_record(path, lod_map)

    return ordered_parallel_map(build_record, paths, normalize_worker_count(workers))


def parse_yaml_int(text: str, key: str, default: int = 0) -> int:
    match = re.search(rf"\b{re.escape(key)}:\s*([-0-9]+)", text)
    if not match:
        return default
    try:
        return int(match.group(1))
    except ValueError:
        return default


def parse_yaml_str(text: str, key: str) -> str:
    match = re.search(rf"\b{re.escape(key)}:\s*([-0-9]+)", text)
    return match.group(1) if match else ""


def render_texture_slice_count(texture_dimension: str, volume_depth: int) -> int:
    dimension = as_non_negative_int(texture_dimension)
    if dimension == 4:
        return 6
    if dimension in (3, 5):
        return max(1, volume_depth)
    return 1


def estimate_render_texture_bytes(record: RenderTextureRecord) -> int:
    if record.width <= 0 or record.height <= 0:
        return 0
    color_format = as_non_negative_int(record.color_format)
    depth_format = as_non_negative_int(record.depth_stencil_format)
    color_bytes = RENDER_TEXTURE_COLOR_FORMAT_BYTES.get(color_format if color_format is not None else -1, 4)
    depth_bytes = RENDER_TEXTURE_DEPTH_FORMAT_BYTES.get(depth_format if depth_format is not None else -1, 4 if depth_format else 0)
    aa = max(1, record.anti_aliasing)
    slices = render_texture_slice_count(record.texture_dimension, record.volume_depth)
    bytes_estimate = record.width * record.height * slices * aa * (color_bytes + depth_bytes)
    if meta_contains(record.mipmap, "1"):
        bytes_estimate = int(bytes_estimate * FULL_MIP_FACTOR)
    return bytes_estimate


def audit_render_texture_record(path: Path) -> RenderTextureRecord:
    record = RenderTextureRecord(path=path)
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        record.flags.append("RENDER_TEXTURE_YAML_UNREADABLE_STATIC")
        record.recommendation = "Open asset in Unity and verify render target dimensions/format manually."
        return record
    record.width = parse_yaml_int(text, "m_Width")
    record.height = parse_yaml_int(text, "m_Height")
    record.color_format = parse_yaml_str(text, "m_ColorFormat")
    record.depth_stencil_format = parse_yaml_str(text, "m_DepthStencilFormat")
    record.anti_aliasing = max(1, parse_yaml_int(text, "m_AntiAliasing", 1))
    record.mipmap = parse_yaml_str(text, "m_MipMap")
    record.generate_mips = parse_yaml_str(text, "m_GenerateMips")
    record.texture_dimension = parse_yaml_str(text, "m_TextureDimension")
    record.volume_depth = max(1, parse_yaml_int(text, "m_VolumeDepth", 1))
    record.dynamic_scale = parse_yaml_str(text, "m_UseDynamicScale")
    record.random_write = parse_yaml_str(text, "m_EnableRandomWrite")
    record.estimated_bytes = estimate_render_texture_bytes(record)
    if record.width <= 0 or record.height <= 0:
        record.flags.append("RENDER_TEXTURE_DIMENSIONS_UNREADABLE_STATIC")
    if max(record.width, record.height) > MAX_TEXTURE_DIM:
        record.flags.append("RENDER_TEXTURE_GT_2048_STATIC")
    if mib(record.estimated_bytes) > RENDER_TEXTURE_SINGLE_ASSET_REDLINE_MIB:
        record.flags.append("RENDER_TEXTURE_SINGLE_ASSET_GT_32MIB_STATIC")
    if record.anti_aliasing > 1:
        record.flags.append("RENDER_TEXTURE_MSAA_GT1_STATIC_SUSPECT")
    if meta_contains(record.mipmap, "1") or meta_contains(record.generate_mips, "1"):
        record.flags.append("RENDER_TEXTURE_MIPMAPS_ENABLED_STATIC_SUSPECT")
    if meta_contains(record.random_write, "1"):
        record.flags.append("RENDER_TEXTURE_RANDOM_WRITE_ENABLED_STATIC_SUSPECT")
    if as_non_negative_int(record.depth_stencil_format):
        record.flags.append("RENDER_TEXTURE_DEPTH_STENCIL_PRESENT_STATIC_SUSPECT")
    if record.flags:
        record.recommendation = "Verify RT necessity in Unity; remove depth/MSAA/mips/random-write unless required and keep RT+Depth under 320 MiB."
    else:
        record.recommendation = "Static RT estimate only; verify live RT+Depth budget in Memory Profiler."
    return record


def audit_render_textures(paths: Sequence[Path], workers: int = 1) -> List[RenderTextureRecord]:
    return ordered_parallel_map(audit_render_texture_record, paths, normalize_worker_count(workers))


def is_editor_source_path(path: Path, root: Path) -> bool:
    value = rel(path, root).replace("\\", "/").lower()
    return "/editor/" in value or value.endswith("/editor")


def find_render_texture_source_hotspots_in_paths(root: Path, paths: Sequence[Path]) -> List[RenderTextureSourceHit]:
    hits: List[RenderTextureSourceHit] = []
    for path in paths:
        if any(part.lower() in SKIP_DIR_NAMES_LOWER for part in path.parts):
            continue
        editor_only = is_editor_source_path(path, root)
        try:
            with path.open("r", encoding="utf-8", errors="replace") as handle:
                for line_number, line in enumerate(handle, start=1):
                    stripped = line.strip()
                    if not stripped or stripped.startswith("//"):
                        continue
                    for pattern_name, pattern in RENDER_TEXTURE_SOURCE_PATTERNS:
                        if pattern.search(stripped):
                            hits.append(
                                RenderTextureSourceHit(
                                    path=path,
                                    line=line_number,
                                    pattern=pattern_name,
                                    snippet=stripped[:180],
                                    editor_only=editor_only,
                                )
                            )
                            break
        except OSError:
            continue
    hits.sort(key=lambda item: (item.editor_only, rel(item.path, root).lower(), item.line))
    return hits


def find_render_texture_source_hotspots(root: Path) -> List[RenderTextureSourceHit]:
    scripts_root = root / "Assets" / "_Project" / "Scripts"
    if not scripts_root.exists():
        return []
    return find_render_texture_source_hotspots_in_paths(root, sorted(scripts_root.rglob("*.cs"), key=lambda path: rel(path, root).lower()))


def resolve_render_texture_hotspots(
    root: Path,
    hotspots: Optional[Sequence[RenderTextureSourceHit]],
) -> Sequence[RenderTextureSourceHit]:
    if hotspots is not None:
        return hotspots
    return find_render_texture_source_hotspots(root)


def mib(value: float) -> float:
    return value / (1024.0 * 1024.0)


def write_csv(
    path: Path,
    root: Path,
    textures: Sequence[TextureRecord],
    meshes: Sequence[MeshRecord],
    render_textures: Sequence[RenderTextureRecord],
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(
            [
                "asset_type",
                "path",
                "extension",
                "width",
                "height",
                "source_mode",
                "meta_max_texture_size",
                "meta_texture_compression",
                "meta_texture_format",
                "meta_streaming_mipmaps",
                "meta_is_readable",
                "meta_texture_type",
                "bc7_bytes",
                "bc7_mib",
                "bc7_full_mip_mib",
                "file_bytes",
                "file_mib",
                "triangles",
                "mesh_geometry_estimate_bytes",
                "mesh_geometry_estimate_mib",
                "lod_detected",
                "mesh_meta_is_readable",
                "mesh_meta_compression",
                "mesh_meta_optimize_mesh",
                "mesh_meta_import_blend_shapes",
                "mesh_meta_add_colliders",
                "mesh_meta_generate_secondary_uv",
                "mesh_meta_keep_quads",
                "rt_color_format",
                "rt_depth_stencil_format",
                "rt_anti_aliasing",
                "rt_mipmap",
                "rt_generate_mips",
                "rt_texture_dimension",
                "rt_volume_depth",
                "rt_dynamic_scale",
                "rt_random_write",
                "rt_estimate_bytes",
                "rt_estimate_mib",
                "redline_flags",
                "atlas_group",
                "recommendation",
                "evidence_class",
            ]
        )
        for record in textures:
            file_bytes = record.path.stat().st_size if record.path.exists() else 0
            writer.writerow(
                [
                    "texture",
                    rel(record.path, root),
                    record.path.suffix.lower(),
                    record.width,
                    record.height,
                    record.mode,
                    record.meta_max_texture_size,
                    record.meta_texture_compression,
                    record.meta_texture_format,
                    record.meta_streaming_mipmaps,
                    record.meta_is_readable,
                    record.meta_texture_type,
                    record.bc7_bytes,
                    f"{mib(record.bc7_bytes):.3f}",
                    f"{mib(record.bc7_bytes * FULL_MIP_FACTOR):.3f}",
                    file_bytes,
                    f"{mib(file_bytes):.3f}",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    ";".join(record.flags),
                    record.atlas_group,
                    record.recommendation,
                    "STATIC_SOURCE",
                ]
            )
        for record in meshes:
            writer.writerow(
                [
                    "mesh",
                    rel(record.path, root),
                    record.path.suffix.lower(),
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    record.file_bytes,
                    f"{mib(record.file_bytes):.3f}",
                    "" if record.triangles is None else record.triangles,
                    record.estimated_geometry_bytes,
                    f"{mib(record.estimated_geometry_bytes):.3f}",
                    str(record.lod_detected).lower(),
                    record.meta_is_readable,
                    record.meta_mesh_compression,
                    record.meta_optimize_mesh,
                    record.meta_import_blend_shapes,
                    record.meta_add_colliders,
                    record.meta_generate_secondary_uv,
                    record.meta_keep_quads,
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    ";".join(record.flags),
                    "",
                    record.recommendation,
                    "STATIC_SOURCE",
                ]
            )
        for record in render_textures:
            file_bytes = record.path.stat().st_size if record.path.exists() else 0
            writer.writerow(
                [
                    "render_texture",
                    rel(record.path, root),
                    record.path.suffix.lower(),
                    record.width,
                    record.height,
                    "RenderTexture",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    file_bytes,
                    f"{mib(file_bytes):.3f}",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    record.color_format,
                    record.depth_stencil_format,
                    record.anti_aliasing,
                    record.mipmap,
                    record.generate_mips,
                    record.texture_dimension,
                    record.volume_depth,
                    record.dynamic_scale,
                    record.random_write,
                    record.estimated_bytes,
                    f"{mib(record.estimated_bytes):.3f}",
                    ";".join(record.flags),
                    "",
                    record.recommendation,
                    "STATIC_SOURCE",
                ]
            )


def write_texture_redlines_csv(path: Path, root: Path, textures: Sequence[TextureRecord]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(
            [
                "path",
                "width",
                "height",
                "bc7_full_mip_mib",
                "first_party_production",
                "flags",
                "recommendation",
            ]
        )
        for record in sorted(textures, key=lambda item: item.bc7_bytes, reverse=True):
            if not record.flags:
                continue
            writer.writerow(
                [
                    rel(record.path, root),
                    record.width,
                    record.height,
                    f"{mib(record.bc7_bytes * FULL_MIP_FACTOR):.3f}",
                    str(is_first_party_production_candidate(record.path, root)).lower(),
                    ";".join(record.flags),
                    record.recommendation,
                ]
            )


def write_mesh_redlines_csv(path: Path, root: Path, meshes: Sequence[MeshRecord]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(
            [
                "path",
                "file_mib",
                "triangles",
                "geometry_estimate_mib",
                "lod_detected",
                "meta_is_readable",
                "meta_mesh_compression",
                "meta_optimize_mesh",
                "meta_import_blend_shapes",
                "meta_add_colliders",
                "meta_generate_secondary_uv",
                "meta_keep_quads",
                "flags",
                "recommendation",
            ]
        )
        for record in sorted(meshes, key=lambda item: (item.triangles or 0, item.file_bytes), reverse=True):
            if not record.flags:
                continue
            writer.writerow(
                [
                    rel(record.path, root),
                    f"{mib(record.file_bytes):.3f}",
                    "" if record.triangles is None else record.triangles,
                    f"{mib(record.estimated_geometry_bytes):.3f}",
                    str(record.lod_detected).lower(),
                    record.meta_is_readable,
                    record.meta_mesh_compression,
                    record.meta_optimize_mesh,
                    record.meta_import_blend_shapes,
                    record.meta_add_colliders,
                    record.meta_generate_secondary_uv,
                    record.meta_keep_quads,
                    ";".join(record.flags),
                    record.recommendation,
                ]
            )


def write_render_texture_redlines_csv(path: Path, root: Path, render_textures: Sequence[RenderTextureRecord]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(
            [
                "path",
                "width",
                "height",
                "estimate_mib",
                "color_format",
                "depth_stencil_format",
                "anti_aliasing",
                "mipmap",
                "random_write",
                "flags",
                "recommendation",
            ]
        )
        for record in sorted(render_textures, key=lambda item: item.estimated_bytes, reverse=True):
            if not record.flags:
                continue
            writer.writerow(
                [
                    rel(record.path, root),
                    record.width,
                    record.height,
                    f"{mib(record.estimated_bytes):.3f}",
                    record.color_format,
                    record.depth_stencil_format,
                    record.anti_aliasing,
                    record.mipmap,
                    record.random_write,
                    ";".join(record.flags),
                    record.recommendation,
                ]
            )


def write_render_texture_source_hotspots_csv(path: Path, root: Path, hotspots: Sequence[RenderTextureSourceHit]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(
            [
                "path",
                "line",
                "pattern",
                "editor_only",
                "profiler_priority",
                "snippet",
                "required_action",
                "evidence_class",
            ]
        )
        for hit in hotspots:
            runtime = not hit.editor_only
            priority = "P1_RUNTIME_PROFILER" if runtime else "P3_EDITOR_ONLY_VERIFY_EXCLUDED"
            action = "Capture owner lifetime, dimensions, format, and residency in Unity Memory Profiler; static source cannot prove RT+Depth cost."
            if hit.editor_only:
                action = "Confirm editor-only assembly/folder exclusion from player build; no runtime profiler action unless referenced by player code."
            writer.writerow(
                [
                    rel(hit.path, root),
                    hit.line,
                    hit.pattern,
                    str(hit.editor_only).lower(),
                    priority,
                    hit.snippet,
                    action,
                    "STATIC_SOURCE",
                ]
            )


def find_link_xml(root: Path) -> List[Path]:
    result: List[Path] = []
    for current_root, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d.lower() not in SKIP_DIR_NAMES_LOWER]
        for filename in files:
            if filename.lower() == "link.xml":
                result.append(Path(current_root) / filename)
    result.sort(key=lambda p: rel(p, root).lower())
    return result


def summarize_link_xml(paths: Sequence[Path], root: Path) -> Tuple[str, List[str]]:
    if not paths:
        return "LINK_XML_MISSING", ["No link.xml found. Asset files are not stripped by IL2CPP, but managed loader/reflection preservation remains unproven."]
    notes: List[str] = []
    for path in paths:
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError as exc:
            notes.append(f"{rel(path, root)} unreadable: {exc}")
            continue
        preserve_all_count = len(re.findall(r'preserve\s*=\s*"all"', text))
        assembly_count = len(re.findall(r"<assembly\b", text))
        type_count = len(re.findall(r"<type\b", text))
        notes.append(f"{rel(path, root)} assemblies={assembly_count} types={type_count} preserve_all={preserve_all_count}")
    return "LINK_XML_PRESENT_STATIC_ONLY", notes


def top_texture_records(records: Sequence[TextureRecord], root: Path, limit: int = 25) -> List[TextureRecord]:
    runtime = [record for record in records if is_runtime_candidate(record.path, root)]
    runtime.sort(key=lambda record: record.bc7_bytes, reverse=True)
    return runtime[:limit]


def low_tier_halving(records: Sequence[TextureRecord], root: Path, limit: int = 20) -> List[Tuple[TextureRecord, float]]:
    candidates: List[Tuple[TextureRecord, float]] = []
    for record in records:
        if not is_runtime_candidate(record.path, root):
            continue
        if max(record.width, record.height) <= LOW_TIER_TARGET_DIM:
            continue
        reduced_width = max(1, record.width // 2)
        reduced_height = max(1, record.height // 2)
        saved = record.bc7_bytes - int(reduced_width * reduced_height * BC7_BYTES_PER_PIXEL)
        candidates.append((record, mib(saved * FULL_MIP_FACTOR)))
    candidates.sort(key=lambda item: item[1], reverse=True)
    return candidates[:limit]


def texture_directory_costs(records: Sequence[TextureRecord], root: Path, first_party_only: bool, limit: int = 12) -> List[Tuple[str, int, float, int]]:
    groups: Dict[str, Tuple[int, float, int]] = {}
    for record in records:
        if record.bc7_bytes <= 0:
            continue
        if first_party_only and not is_first_party_production_candidate(record.path, root):
            continue
        if not first_party_only and not is_runtime_candidate(record.path, root):
            continue
        key = rel(record.path.parent, root).replace("\\", "/")
        count, total, crimes = groups.get(key, (0, 0.0, 0))
        has_crime = any(flag.startswith("VRAM CRIME") for flag in record.flags)
        groups[key] = (count + 1, total + record.bc7_bytes * FULL_MIP_FACTOR, crimes + (1 if has_crime else 0))
    ranked = [(key, count, total, crimes) for key, (count, total, crimes) in groups.items()]
    ranked.sort(key=lambda item: item[2], reverse=True)
    return ranked[:limit]


def large_streaming_mipmap_off(records: Sequence[TextureRecord], root: Path, limit: int = 25) -> List[TextureRecord]:
    candidates = [
        record
        for record in records
        if is_first_party_production_candidate(record.path, root)
        and max(record.width, record.height) > LOW_TIER_TARGET_DIM
        and record.meta_streaming_mipmaps
        and not meta_contains(record.meta_streaming_mipmaps, "1")
    ]
    candidates.sort(key=lambda record: record.bc7_bytes, reverse=True)
    return candidates[:limit]


def non_first_party_runtime_costs(records: Sequence[TextureRecord], root: Path, limit: int = 12) -> List[Tuple[str, int, float, int]]:
    groups: Dict[str, Tuple[int, float, int]] = {}
    for record in records:
        if not is_runtime_candidate(record.path, root):
            continue
        if is_first_party_production_candidate(record.path, root):
            continue
        key = rel(record.path.parent, root).replace("\\", "/")
        count, total, crimes = groups.get(key, (0, 0.0, 0))
        has_crime = any(flag.startswith("VRAM CRIME") for flag in record.flags)
        groups[key] = (count + 1, total + record.bc7_bytes * FULL_MIP_FACTOR, crimes + (1 if has_crime else 0))
    ranked = [(key, count, total, crimes) for key, (count, total, crimes) in groups.items()]
    ranked.sort(key=lambda item: item[2], reverse=True)
    return ranked[:limit]


def texture_extension_costs(records: Sequence[TextureRecord], root: Path, limit: int = 16) -> List[Tuple[str, int, float, int, int]]:
    groups: Dict[str, Tuple[int, float, int, int]] = {}
    for record in records:
        if not is_runtime_candidate(record.path, root):
            continue
        ext = record.path.suffix.lower() or "<none>"
        count, total, crimes, container_risks = groups.get(ext, (0, 0.0, 0, 0))
        has_crime = any(flag.startswith("VRAM CRIME") for flag in record.flags)
        groups[ext] = (
            count + 1,
            total + record.bc7_bytes * FULL_MIP_FACTOR,
            crimes + (1 if has_crime else 0),
            container_risks + (1 if has_texture_container_risk(record) else 0),
        )
    ranked = [(ext, count, total, crimes, container_risks) for ext, (count, total, crimes, container_risks) in groups.items()]
    ranked.sort(key=lambda item: item[2], reverse=True)
    return ranked[:limit]


def mesh_extension_costs(records: Sequence[MeshRecord], root: Path, limit: int = 16) -> List[Tuple[str, int, int, int, float, int]]:
    groups: Dict[str, Tuple[int, int, int, float, int]] = {}
    for record in records:
        if not is_runtime_candidate(record.path, root):
            continue
        ext = record.path.suffix.lower() or "<none>"
        count, known_triangles, unreadable_rows, geometry_bytes, flagged_rows = groups.get(ext, (0, 0, 0, 0.0, 0))
        groups[ext] = (
            count + 1,
            known_triangles + (record.triangles or 0),
            unreadable_rows + (1 if record.triangles is None else 0),
            geometry_bytes + record.estimated_geometry_bytes,
            flagged_rows + (1 if record.flags else 0),
        )
    ranked = [
        (ext, count, known_triangles, unreadable_rows, geometry_bytes, flagged_rows)
        for ext, (count, known_triangles, unreadable_rows, geometry_bytes, flagged_rows) in groups.items()
    ]
    ranked.sort(key=lambda item: item[4], reverse=True)
    return ranked[:limit]


def write_remediation_plan(
    path: Path,
    root: Path,
    textures: Sequence[TextureRecord],
    meshes: Sequence[MeshRecord],
    render_textures: Sequence[RenderTextureRecord],
    atlas_groups: Sequence[Tuple[str, List[TextureRecord], int]],
    render_texture_hotspots: Optional[Sequence[RenderTextureSourceHit]] = None,
) -> None:
    texture_crimes = [record for record in textures if any(flag.startswith("VRAM CRIME") for flag in record.flags)]
    texture_container_risks = [record for record in textures if has_texture_container_risk(record)]
    first_party_texture_container_risks = [record for record in texture_container_risks if is_first_party_production_candidate(record.path, root)]
    mesh_redlines = [record for record in meshes if record.flags]
    mesh_import_risks = [record for record in meshes if any(flag.endswith("_STATIC_SUSPECT") for flag in record.flags)]
    first_party_mesh_import_risks = [record for record in mesh_import_risks if is_first_party_production_candidate(record.path, root)]
    mesh_geometry_bytes = sum(record.estimated_geometry_bytes for record in meshes)
    first_party_mesh_geometry_bytes = sum(record.estimated_geometry_bytes for record in meshes if is_first_party_production_candidate(record.path, root))
    mesh_geometry_redlines = [record for record in meshes if "MESH_GEOMETRY_ESTIMATE_GT_16MIB_STATIC" in record.flags]
    render_texture_bytes = sum(record.estimated_bytes for record in render_textures)
    render_texture_redlines = [record for record in render_textures if record.flags]
    render_texture_source_hits = resolve_render_texture_hotspots(root, render_texture_hotspots)
    runtime_render_texture_source_hits = [hit for hit in render_texture_source_hits if not hit.editor_only]
    runtime_full_mips = sum(record.bc7_bytes for record in textures if is_runtime_candidate(record.path, root)) * FULL_MIP_FACTOR
    first_party_full_mips = sum(record.bc7_bytes for record in textures if is_first_party_production_candidate(record.path, root)) * FULL_MIP_FACTOR
    halving_candidates = low_tier_halving(textures, root, limit=25)
    halving_total = sum(saved for _record, saved in low_tier_halving(textures, root, limit=100000))
    now = _dt.datetime.now().isoformat(timespec="seconds")
    lines: List[str] = []
    lines.append("# VRAM Remediation Plan")
    lines.append("")
    lines.append(f"Generated: {now}")
    lines.append("Evidence class: STATIC_SOURCE / FILESYSTEM / PY_UNIT_TEST. No asset/import mutation performed.")
    lines.append("")
    lines.append("## Gate Status")
    lines.append("")
    lines.append(f"- Runtime-candidate full-mip BC7: {mib(runtime_full_mips):.2f} MiB")
    lines.append(f"- First-party production full-mip BC7: {mib(first_party_full_mips):.2f} MiB")
    lines.append(f"- Texture VRAM crime rows: {len(texture_crimes)}")
    lines.append(f"- Texture source-container risk rows: {len(texture_container_risks)}")
    lines.append(f"- First-party texture source-container risk rows: {len(first_party_texture_container_risks)}")
    lines.append(f"- Static mesh geometry estimate: {mib(mesh_geometry_bytes):.2f} MiB / {GEOMETRY_BUFFER_BUDGET_MIB:.0f} MiB geometry budget")
    lines.append(f"- First-party static mesh geometry estimate: {mib(first_party_mesh_geometry_bytes):.2f} MiB")
    lines.append(f"- Mesh single-asset geometry estimate redlines: {len(mesh_geometry_redlines)}")
    lines.append(f"- Mesh redline/risk rows: {len(mesh_redlines)}")
    lines.append(f"- Mesh importer risk rows: {len(mesh_import_risks)}")
    lines.append(f"- First-party mesh importer risk rows: {len(first_party_mesh_import_risks)}")
    lines.append(f"- Static RenderTexture estimate: {mib(render_texture_bytes):.2f} MiB / {RENDER_TARGET_BUDGET_MIB:.0f} MiB RT+Depth budget")
    lines.append(f"- RenderTexture redline/risk rows: {len(render_texture_redlines)}")
    lines.append(f"- Runtime RenderTexture source hotspots: {len(runtime_render_texture_source_hits)}")
    lines.append("- CI behavior: `python Tools/MemoryBudgetCheck.py --root . --ci` must fail until redlines are resolved or explicitly suppressed by future policy.")
    lines.append("")
    lines.append("## Priority 1 - Quarantine Non-Production Runtime Payloads")
    lines.append("")
    lines.append("| Directory | Count | BC7 full mip MiB | VRAM crime rows | Required action |")
    lines.append("|---|---:|---:|---:|---|")
    for directory, count, total, crimes in non_first_party_runtime_costs(textures, root):
        lines.append(f"| {directory} | {count} | {mib(total):.2f} | {crimes} | Prove production use, move to editor-only/demo quarantine, or exclude from Addressables/build payload. |")
    lines.append("")
    lines.append("## Priority 2 - Convert Risky Texture Source Containers")
    lines.append("")
    lines.append("| Extension | Runtime count | BC7 full mip MiB | VRAM crime rows | Container risk rows | Required action |")
    lines.append("|---|---:|---:|---:|---:|---|")
    for ext, count, total, crimes, container_risks in texture_extension_costs(textures, root):
        if container_risks <= 0:
            continue
        lines.append(f"| {ext} | {count} | {mib(total):.2f} | {crimes} | {container_risks} | Convert/quarantine source container or prove importer compression and residency. |")
    lines.append("")
    lines.append("## Priority 3 - RenderTexture Static Assets")
    lines.append("")
    lines.append("| Path | Size | Estimate MiB | Color | Depth | AA | Flags | Required action |")
    lines.append("|---|---:|---:|---:|---:|---:|---|---|")
    for record in sorted(render_textures, key=lambda item: item.estimated_bytes, reverse=True):
        lines.append(
            f"| {rel(record.path, root)} | {record.width}x{record.height} | {mib(record.estimated_bytes):.2f} | {record.color_format} | {record.depth_stencil_format} | {record.anti_aliasing} | {';'.join(record.flags)} | {record.recommendation} |"
        )
    lines.append("")
    lines.append("## Priority 4 - Runtime RenderTexture Source Hotspots")
    lines.append("")
    lines.append("| Path | Line | Pattern | Editor-only | Required action |")
    lines.append("|---|---:|---|---:|---|")
    for hit in render_texture_source_hits[:80]:
        action = "Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely."
        if hit.editor_only:
            action = "Editor-only; keep out of player build and ignore for runtime budget unless referenced by player assembly."
        lines.append(f"| {rel(hit.path, root)} | {hit.line} | {hit.pattern} | {str(hit.editor_only).lower()} | {action} |")
    lines.append("")
    lines.append("## Priority 5 - Clamp First-Party Large Textures")
    lines.append("")
    lines.append("| Path | Source | Est. full-mip MiB saved by halving | Current flags | Required action |")
    lines.append("|---|---:|---:|---|---|")
    for record, saved in halving_candidates:
        if not is_first_party_production_candidate(record.path, root):
            continue
        lines.append(
            f"| {rel(record.path, root)} | {record.width}x{record.height} | {saved:.2f} | {';'.join(record.flags)} | Low tier import cap 1024 or lower; keep higher variant only behind streaming/tier proof. |"
        )
    lines.append("")
    lines.append(f"Static halving relief if every runtime-candidate >1024 texture is halved: {halving_total:.2f} MiB full-mip BC7.")
    lines.append("")
    lines.append("## Priority 6 - Enable Streaming Mipmaps On Large First-Party Textures")
    lines.append("")
    lines.append("| Path | Source | Streaming metadata | Required action |")
    lines.append("|---|---:|---|---|")
    for record in large_streaming_mipmap_off(textures, root):
        lines.append(f"| {rel(record.path, root)} | {record.width}x{record.height} | {record.meta_streaming_mipmaps} | Enable streaming mips unless UI/non-mipped proof exists. |")
    lines.append("")
    lines.append("## Priority 7 - Atlas Small First-Party Texture Families")
    lines.append("")
    lines.append("| Group | Count | Combined BC7 MiB | Required action |")
    lines.append("|---|---:|---:|---|")
    for key, items, area in atlas_groups[:5]:
        lines.append(f"| {key} | {len(items)} | {mib(area):.2f} | Build one atlas/material family or justify separate residency. |")
    lines.append("")
    lines.append("## Priority 8 - Mesh LOD And Importer Redlines")
    lines.append("")
    lines.append("| Path | Triangles | Geometry MiB | LOD detected | Readable | Compression | BlendShapes | Flags | Required action |")
    lines.append("|---|---:|---:|---:|---:|---:|---:|---|---|")
    for record in sorted(mesh_redlines, key=lambda item: (item.triangles or 0, item.file_bytes), reverse=True):
        tri = "UNKNOWN" if record.triangles is None else str(record.triangles)
        lines.append(
            f"| {rel(record.path, root)} | {tri} | {mib(record.estimated_geometry_bytes):.2f} | {str(record.lod_detected).lower()} | {record.meta_is_readable} | {record.meta_mesh_compression} | {record.meta_import_blend_shapes} | {';'.join(record.flags)} | {record.recommendation} |"
        )
    lines.append("")
    lines.append("## Verification Required After Asset Fixes")
    lines.append("")
    lines.append("- Rerun `python Tools/MemoryBudgetCheck.py --root . --ci`.")
    lines.append("- Open Unity and verify importer settings for every changed texture/mesh.")
    lines.append("- Capture Memory Profiler texture memory and graphics memory in target scene.")
    lines.append("- Run MX350/LOW profile and prove frame-time/VRAM with player or profiler artifact.")
    lines.append("- Do not mark runtime VRAM solved from this static plan alone.")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def build_summary_payload(
    root: Path,
    textures: Sequence[TextureRecord],
    meshes: Sequence[MeshRecord],
    render_textures: Sequence[RenderTextureRecord],
    atlas_groups: Sequence[Tuple[str, List[TextureRecord], int]],
    link_status: str,
    link_notes: Sequence[str],
    render_texture_hotspots: Optional[Sequence[RenderTextureSourceHit]] = None,
) -> Dict[str, object]:
    total_bc7 = sum(record.bc7_bytes for record in textures)
    runtime_bc7 = sum(record.bc7_bytes for record in textures if is_runtime_candidate(record.path, root))
    first_party_bc7 = sum(record.bc7_bytes for record in textures if is_first_party_production_candidate(record.path, root))
    texture_crimes = [record for record in textures if any(flag.startswith("VRAM CRIME") for flag in record.flags)]
    texture_flagged = [record for record in textures if record.flags]
    texture_container_risks = [record for record in textures if has_texture_container_risk(record)]
    first_party_texture_container_risks = [record for record in texture_container_risks if is_first_party_production_candidate(record.path, root)]
    mesh_redlines = [record for record in meshes if record.flags]
    mesh_import_risks = [record for record in meshes if any(flag.endswith("_STATIC_SUSPECT") for flag in record.flags)]
    first_party_mesh_import_risks = [record for record in mesh_import_risks if is_first_party_production_candidate(record.path, root)]
    mesh_geometry_bytes = sum(record.estimated_geometry_bytes for record in meshes)
    first_party_mesh_geometry_bytes = sum(record.estimated_geometry_bytes for record in meshes if is_first_party_production_candidate(record.path, root))
    mesh_geometry_redlines = [record for record in meshes if "MESH_GEOMETRY_ESTIMATE_GT_16MIB_STATIC" in record.flags]
    render_texture_bytes = sum(record.estimated_bytes for record in render_textures)
    render_texture_redlines = [record for record in render_textures if record.flags]
    render_texture_source_hits = resolve_render_texture_hotspots(root, render_texture_hotspots)
    runtime_render_texture_source_hits = [hit for hit in render_texture_source_hits if not hit.editor_only]
    first_party_streaming_off = large_streaming_mipmap_off(textures, root, limit=100000)
    all_streaming_off = [record for record in textures if "STREAMING_MIPMAPS_OFF_LARGE" in record.flags]
    runtime_full_mips = runtime_bc7 * FULL_MIP_FACTOR
    total_full_mips = total_bc7 * FULL_MIP_FACTOR
    critical = total_full_mips > CRITICAL_TEXTURE_POOL_MIB * 1024 * 1024 or runtime_full_mips > CRITICAL_TEXTURE_POOL_MIB * 1024 * 1024
    gate_reasons: List[str] = []
    if critical:
        gate_reasons.append("CRITICAL_VRAM_OVERFLOW")
    if texture_crimes:
        gate_reasons.append("TEXTURE_VRAM_CRIMES")
    if mesh_redlines:
        gate_reasons.append("MESH_REDLINE_OR_RISK")
    if render_texture_redlines:
        gate_reasons.append("RENDER_TEXTURE_REDLINE_OR_RISK")
    return {
        "schema_version": 1,
        "generated": _dt.datetime.now().isoformat(timespec="seconds"),
        "generated_utc": _dt.datetime.now(_dt.timezone.utc).isoformat(timespec="seconds"),
        "evidence_class": "STATIC_SOURCE/FILESYSTEM/PY_UNIT_TEST",
        "root": str(root),
        "scan_root_names": list(DEFAULT_SCAN_ROOT_NAMES),
        "resolved_scan_roots": [rel(path, root) for path in resolve_scan_roots(root)],
        "skipped_directory_names": sorted(SKIP_DIRS, key=str.lower),
        "texture_count": len(textures),
        "mesh_count": len(meshes),
        "render_texture_count": len(render_textures),
        "geometry_buffer_budget_mib": GEOMETRY_BUFFER_BUDGET_MIB,
        "render_target_budget_mib": RENDER_TARGET_BUDGET_MIB,
        "mesh_geometry_static_estimate_mib": round(mib(mesh_geometry_bytes), 3),
        "first_party_mesh_geometry_static_estimate_mib": round(mib(first_party_mesh_geometry_bytes), 3),
        "render_texture_static_estimate_mib": round(mib(render_texture_bytes), 3),
        "render_texture_redline_rows": len(render_texture_redlines),
        "render_texture_depth_stencil_rows": sum(1 for record in render_textures if "RENDER_TEXTURE_DEPTH_STENCIL_PRESENT_STATIC_SUSPECT" in record.flags),
        "render_texture_source_hotspot_rows": len(render_texture_source_hits),
        "runtime_render_texture_source_hotspot_rows": len(runtime_render_texture_source_hits),
        "mesh_geometry_single_asset_redline_mib": MESH_GEOMETRY_SINGLE_ASSET_REDLINE_MIB,
        "mesh_geometry_single_asset_redline_rows": len(mesh_geometry_redlines),
        "bc7_no_mip_mib": round(mib(total_bc7), 3),
        "bc7_full_mip_total_mib": round(mib(total_full_mips), 3),
        "bc7_full_mip_runtime_candidate_mib": round(mib(runtime_full_mips), 3),
        "bc7_full_mip_first_party_production_mib": round(mib(first_party_bc7 * FULL_MIP_FACTOR), 3),
        "mx350_texture_budget_mib": TEXTURE_BUDGET_MIB,
        "critical_texture_pool_mib": CRITICAL_TEXTURE_POOL_MIB,
        "critical_vram_overflow": critical,
        "texture_vram_crime_rows": len(texture_crimes),
        "texture_flagged_rows": len(texture_flagged),
        "texture_source_container_risk_rows": len(texture_container_risks),
        "first_party_texture_source_container_risk_rows": len(first_party_texture_container_risks),
        "mesh_redline_rows": len(mesh_redlines),
        "mesh_import_risk_rows": len(mesh_import_risks),
        "mesh_read_write_enabled_rows": sum(1 for record in meshes if "MESH_READ_WRITE_ENABLED_STATIC_SUSPECT" in record.flags),
        "mesh_blendshapes_enabled_rows": sum(1 for record in meshes if "MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT" in record.flags),
        "mesh_compression_off_rows": sum(1 for record in meshes if "MESH_COMPRESSION_OFF_STATIC_SUSPECT" in record.flags),
        "mesh_import_colliders_enabled_rows": sum(1 for record in meshes if "MESH_IMPORT_COLLIDERS_ENABLED_STATIC_SUSPECT" in record.flags),
        "first_party_mesh_import_risk_rows": len(first_party_mesh_import_risks),
        "first_party_mesh_read_write_enabled_rows": sum(1 for record in first_party_mesh_import_risks if "MESH_READ_WRITE_ENABLED_STATIC_SUSPECT" in record.flags),
        "first_party_mesh_blendshapes_enabled_rows": sum(1 for record in first_party_mesh_import_risks if "MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT" in record.flags),
        "first_party_mesh_compression_off_rows": sum(1 for record in first_party_mesh_import_risks if "MESH_COMPRESSION_OFF_STATIC_SUSPECT" in record.flags),
        "first_party_large_streaming_mips_off": len(first_party_streaming_off),
        "all_large_streaming_mips_off": len(all_streaming_off),
        "link_xml_status": link_status,
        "link_xml_notes": list(link_notes),
        "gate_reasons": gate_reasons,
        "ci_expected_exit_code": 2 if gate_reasons else 0,
        "top_non_first_party_runtime_directories": [
            {
                "directory": directory,
                "count": count,
                "bc7_full_mip_mib": round(mib(total), 3),
                "vram_crime_rows": crimes,
            }
            for directory, count, total, crimes in non_first_party_runtime_costs(textures, root)
        ],
        "texture_extension_summary": [
            {
                "extension": ext,
                "count": count,
                "bc7_full_mip_mib": round(mib(total), 3),
                "vram_crime_rows": crimes,
                "source_container_risk_rows": container_risks,
            }
            for ext, count, total, crimes, container_risks in texture_extension_costs(textures, root)
        ],
        "mesh_extension_summary": [
            {
                "extension": ext,
                "count": count,
                "known_triangles": known_triangles,
                "triangle_unreadable_rows": unreadable_rows,
                "geometry_estimate_mib": round(mib(geometry_bytes), 3),
                "flagged_rows": flagged_rows,
            }
            for ext, count, known_triangles, unreadable_rows, geometry_bytes, flagged_rows in mesh_extension_costs(meshes, root)
        ],
        "render_textures": [
            {
                "path": rel(record.path, root),
                "width": record.width,
                "height": record.height,
                "estimate_mib": round(mib(record.estimated_bytes), 3),
                "color_format": record.color_format,
                "depth_stencil_format": record.depth_stencil_format,
                "anti_aliasing": record.anti_aliasing,
                "mipmap": record.mipmap,
                "random_write": record.random_write,
                "flags": list(record.flags),
            }
            for record in render_textures
        ],
        "render_texture_source_hotspots": [
            {
                "path": rel(hit.path, root),
                "line": hit.line,
                "pattern": hit.pattern,
                "editor_only": hit.editor_only,
                "snippet": hit.snippet,
            }
            for hit in render_texture_source_hits[:120]
        ],
        "atlas_suggestions": [
            {
                "group": key,
                "count": len(items),
                "combined_bc7_mib": round(mib(area), 3),
                "members": [item.path.name for item in items],
            }
            for key, items, area in atlas_groups[:5]
        ],
        "texture_redlines": [
            {
                "path": rel(record.path, root),
                "width": record.width,
                "height": record.height,
                "bc7_full_mip_mib": round(mib(record.bc7_bytes * FULL_MIP_FACTOR), 3),
                "first_party_production": is_first_party_production_candidate(record.path, root),
                "flags": list(record.flags),
                "recommendation": record.recommendation,
            }
            for record in sorted(texture_flagged, key=lambda item: item.bc7_bytes, reverse=True)
        ],
        "mesh_redlines": [
            {
                "path": rel(record.path, root),
                "triangles": record.triangles,
                "geometry_estimate_mib": round(mib(record.estimated_geometry_bytes), 3),
                "lod_detected": record.lod_detected,
                "meta_is_readable": record.meta_is_readable,
                "meta_mesh_compression": record.meta_mesh_compression,
                "meta_optimize_mesh": record.meta_optimize_mesh,
                "meta_import_blend_shapes": record.meta_import_blend_shapes,
                "meta_add_colliders": record.meta_add_colliders,
                "meta_generate_secondary_uv": record.meta_generate_secondary_uv,
                "meta_keep_quads": record.meta_keep_quads,
                "flags": list(record.flags),
            }
            for record in mesh_redlines
        ],
    }


def write_summary_json(
    path: Path,
    root: Path,
    textures: Sequence[TextureRecord],
    meshes: Sequence[MeshRecord],
    render_textures: Sequence[RenderTextureRecord],
    atlas_groups: Sequence[Tuple[str, List[TextureRecord], int]],
    link_status: str,
    link_notes: Sequence[str],
    render_texture_hotspots: Optional[Sequence[RenderTextureSourceHit]] = None,
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = build_summary_payload(root, textures, meshes, render_textures, atlas_groups, link_status, link_notes, render_texture_hotspots)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def validate_generated_reports(
    root: Path,
    csv_path: Path,
    json_path: Path,
    texture_redlines_path: Optional[Path] = None,
    mesh_redlines_path: Optional[Path] = None,
    render_texture_redlines_path: Optional[Path] = None,
    render_texture_hotspots_path: Optional[Path] = None,
    summary_path: Optional[Path] = None,
    plan_path: Optional[Path] = None,
) -> Tuple[bool, List[str]]:
    messages: List[str] = []
    if not csv_path.exists():
        return False, [f"missing CSV report: {rel(csv_path, root)}"]
    if not json_path.exists():
        return False, [f"missing JSON report: {rel(json_path, root)}"]

    try:
        payload = json.loads(json_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        return False, [f"invalid JSON report: {rel(json_path, root)} line={exc.lineno} column={exc.colno}"]

    with csv_path.open(newline="", encoding="utf-8") as handle:
        reader = csv.DictReader(handle)
        broad_fieldnames = list(reader.fieldnames or [])
        fieldnames = set(broad_fieldnames)
        rows = list(reader)

    if "asset_type" not in fieldnames or "path" not in fieldnames:
        return False, [f"CSV report missing asset_type/path columns: {rel(csv_path, root)}"]
    if tuple(broad_fieldnames) != BROAD_REPORT_COLUMNS:
        messages.append("CSV report schema drift")

    def read_split_report(path: Optional[Path], label: str, required_columns: Sequence[str]) -> List[Dict[str, str]]:
        if path is None:
            return []
        if not path.exists():
            messages.append(f"missing {label} report: {rel(path, root)}")
            return []
        with path.open(newline="", encoding="utf-8") as handle:
            reader = csv.DictReader(handle)
            split_fieldnames = list(reader.fieldnames or [])
            split_rows = list(reader)
        if tuple(split_fieldnames) != tuple(required_columns):
            messages.append(f"{label} report schema drift")
        return split_rows

    def read_text_report(path: Optional[Path], label: str) -> str:
        if path is None:
            return ""
        if not path.exists():
            messages.append(f"missing {label} report: {rel(path, root)}")
            return ""
        return path.read_text(encoding="utf-8")

    def require_snippets(text: str, label: str, snippets: Sequence[str]) -> None:
        if not text:
            return
        for snippet in snippets:
            if snippet not in text:
                messages.append(f"{label} report missing snippet: {snippet}")

    texture_rows = [row for row in rows if row.get("asset_type") == "texture"]
    mesh_rows = [row for row in rows if row.get("asset_type") == "mesh"]
    render_texture_rows = [row for row in rows if row.get("asset_type") == "render_texture"]
    unknown_type_rows = [row for row in rows if row.get("asset_type") not in {"texture", "mesh", "render_texture"}]
    texture_redline_rows = read_split_report(texture_redlines_path, "texture redline", TEXTURE_REDLINE_COLUMNS)
    mesh_redline_rows = read_split_report(mesh_redlines_path, "mesh redline", MESH_REDLINE_COLUMNS)
    render_texture_redline_rows = read_split_report(render_texture_redlines_path, "RenderTexture redline", RENDER_TEXTURE_REDLINE_COLUMNS)
    render_texture_hotspot_rows = read_split_report(render_texture_hotspots_path, "RenderTexture hotspot", RENDER_TEXTURE_HOTSPOT_COLUMNS)
    summary_text = read_text_report(summary_path, "summary")
    plan_text = read_text_report(plan_path, "remediation plan")
    runtime_hotspot_rows = [
        row
        for row in render_texture_hotspot_rows
        if row.get("editor_only", "").strip().lower() not in {"1", "true", "yes"}
    ]
    expected_roots = [rel(path, root) for path in resolve_scan_roots(root)]
    allowed_prefixes = tuple(f"{name}/" for name in expected_roots)
    texture_paths = [row.get("path", "") for row in texture_rows]
    mesh_paths = [row.get("path", "") for row in mesh_rows]
    render_texture_paths = [row.get("path", "") for row in render_texture_rows]
    texture_flags_by_path = {row.get("path", ""): row.get("redline_flags", "") for row in texture_rows}
    mesh_flags_by_path = {row.get("path", ""): row.get("redline_flags", "") for row in mesh_rows}
    render_texture_flags_by_path = {row.get("path", ""): row.get("redline_flags", "") for row in render_texture_rows}
    render_texture_dimensions_by_path = {
        row.get("path", ""): (
            row.get("width", ""),
            row.get("height", ""),
            row.get("rt_estimate_mib", ""),
        )
        for row in render_texture_rows
    }
    texture_path_set = set(texture_paths)
    mesh_path_set = set(mesh_paths)
    render_texture_path_set = set(render_texture_paths)
    broad_texture_redline_paths = {path for path, flags in texture_flags_by_path.items() if flags}
    broad_mesh_redline_paths = {path for path, flags in mesh_flags_by_path.items() if flags}
    broad_render_texture_redline_paths = {path for path, flags in render_texture_flags_by_path.items() if flags}
    texture_redline_paths = [row.get("path", "") for row in texture_redline_rows]
    mesh_redline_paths = [row.get("path", "") for row in mesh_redline_rows]
    render_texture_redline_paths = [row.get("path", "") for row in render_texture_redline_rows]
    render_texture_hotspot_keys = {
        (
            row.get("path", ""),
            row.get("line", ""),
            row.get("pattern", ""),
            row.get("editor_only", "").strip().lower() in {"1", "true", "yes"},
        )
        for row in render_texture_hotspot_rows
    }
    json_hotspot_keys = {
        (
            str(item.get("path", "")),
            str(item.get("line", "")),
            str(item.get("pattern", "")),
            bool(item.get("editor_only", False)),
        )
        for item in payload.get("render_texture_source_hotspots", [])
    }
    json_texture_redline_flags_by_path = {
        str(item.get("path", "")): ";".join(str(flag) for flag in item.get("flags", []))
        for item in payload.get("texture_redlines", [])
    }
    texture_redline_dimensions_by_path = {
        row.get("path", ""): (
            row.get("width", ""),
            row.get("height", ""),
            row.get("bc7_full_mip_mib", ""),
            row.get("first_party_production", ""),
        )
        for row in texture_redline_rows
    }
    json_texture_redline_dimensions_by_path = {
        str(item.get("path", "")): (
            str(item.get("width", "")),
            str(item.get("height", "")),
            f"{float(item.get('bc7_full_mip_mib', 0.0)):.3f}",
            str(bool(item.get("first_party_production", False))).lower(),
        )
        for item in payload.get("texture_redlines", [])
    }
    json_mesh_redline_flags_by_path = {
        str(item.get("path", "")): ";".join(str(flag) for flag in item.get("flags", []))
        for item in payload.get("mesh_redlines", [])
    }
    json_render_texture_flags_by_path = {
        str(item.get("path", "")): ";".join(str(flag) for flag in item.get("flags", []))
        for item in payload.get("render_textures", [])
    }
    json_render_texture_dimensions_by_path = {
        str(item.get("path", "")): (
            str(item.get("width", "")),
            str(item.get("height", "")),
            f"{float(item.get('estimate_mib', 0.0)):.3f}",
        )
        for item in payload.get("render_textures", [])
    }

    if payload.get("texture_count") != len(texture_rows):
        messages.append(f"texture_count mismatch json={payload.get('texture_count')} csv={len(texture_rows)}")
    if payload.get("mesh_count") != len(mesh_rows):
        messages.append(f"mesh_count mismatch json={payload.get('mesh_count')} csv={len(mesh_rows)}")
    if payload.get("render_texture_count") != len(render_texture_rows):
        messages.append(f"render_texture_count mismatch json={payload.get('render_texture_count')} csv={len(render_texture_rows)}")
    if payload.get("schema_version") != 1:
        messages.append("JSON schema_version drift")
    if payload.get("evidence_class") != "STATIC_SOURCE/FILESYSTEM/PY_UNIT_TEST":
        messages.append("JSON evidence_class drift")
    if payload.get("scan_root_names") != list(DEFAULT_SCAN_ROOT_NAMES):
        messages.append("JSON scan_root_names drift")
    expected_ci_exit_code = 2 if payload.get("gate_reasons") else 0
    if payload.get("ci_expected_exit_code") != expected_ci_exit_code:
        messages.append("JSON ci_expected_exit_code drift")
    if payload.get("resolved_scan_roots") != expected_roots:
        messages.append(f"resolved_scan_roots mismatch json={payload.get('resolved_scan_roots')} expected={expected_roots}")
    if unknown_type_rows:
        messages.append(f"unknown asset_type rows={len(unknown_type_rows)}")
    if any(row.get("evidence_class") != "STATIC_SOURCE" for row in rows):
        messages.append("CSV report evidence_class drift")
    if render_texture_hotspots_path is not None and any(row.get("evidence_class") != "STATIC_SOURCE" for row in render_texture_hotspot_rows):
        messages.append("RenderTexture hotspot evidence_class drift")
    if texture_redlines_path is not None and payload.get("texture_flagged_rows") != len(texture_redline_rows):
        messages.append(f"texture redline mismatch json={payload.get('texture_flagged_rows')} csv={len(texture_redline_rows)}")
    if mesh_redlines_path is not None and payload.get("mesh_redline_rows") != len(mesh_redline_rows):
        messages.append(f"mesh redline mismatch json={payload.get('mesh_redline_rows')} csv={len(mesh_redline_rows)}")
    if render_texture_redlines_path is not None and payload.get("render_texture_redline_rows") != len(render_texture_redline_rows):
        messages.append(f"RenderTexture redline mismatch json={payload.get('render_texture_redline_rows')} csv={len(render_texture_redline_rows)}")
    if render_texture_hotspots_path is not None and payload.get("render_texture_source_hotspot_rows") != len(render_texture_hotspot_rows):
        messages.append(f"RenderTexture hotspot mismatch json={payload.get('render_texture_source_hotspot_rows')} csv={len(render_texture_hotspot_rows)}")
    if render_texture_hotspots_path is not None and payload.get("runtime_render_texture_source_hotspot_rows") != len(runtime_hotspot_rows):
        messages.append(f"runtime RenderTexture hotspot mismatch json={payload.get('runtime_render_texture_source_hotspot_rows')} csv={len(runtime_hotspot_rows)}")
    if payload.get("texture_flagged_rows") != len(broad_texture_redline_paths):
        messages.append(f"broad texture redline mismatch json={payload.get('texture_flagged_rows')} csv={len(broad_texture_redline_paths)}")
    if payload.get("mesh_redline_rows") != len(broad_mesh_redline_paths):
        messages.append(f"broad mesh redline mismatch json={payload.get('mesh_redline_rows')} csv={len(broad_mesh_redline_paths)}")
    if payload.get("render_texture_redline_rows") != len(broad_render_texture_redline_paths):
        messages.append(f"broad RenderTexture redline mismatch json={payload.get('render_texture_redline_rows')} csv={len(broad_render_texture_redline_paths)}")
    if len(texture_paths) != len(texture_path_set):
        messages.append("duplicate texture paths in broad CSV")
    if len(mesh_paths) != len(mesh_path_set):
        messages.append("duplicate mesh paths in broad CSV")
    if len(render_texture_paths) != len(render_texture_path_set):
        messages.append("duplicate RenderTexture paths in broad CSV")
    if texture_redlines_path is not None and len(texture_redline_paths) != len(set(texture_redline_paths)):
        messages.append("duplicate texture redline paths")
    if mesh_redlines_path is not None and len(mesh_redline_paths) != len(set(mesh_redline_paths)):
        messages.append("duplicate mesh redline paths")
    if render_texture_redlines_path is not None and len(render_texture_redline_paths) != len(set(render_texture_redline_paths)):
        messages.append("duplicate RenderTexture redline paths")
    if texture_redlines_path is not None and any(path not in texture_path_set for path in texture_redline_paths):
        messages.append("texture redline paths missing from broad CSV")
    if mesh_redlines_path is not None and any(path not in mesh_path_set for path in mesh_redline_paths):
        messages.append("mesh redline paths missing from broad CSV")
    if render_texture_redlines_path is not None and any(path not in render_texture_path_set for path in render_texture_redline_paths):
        messages.append("RenderTexture redline paths missing from broad CSV")
    if texture_redlines_path is not None and set(texture_redline_paths) != broad_texture_redline_paths:
        messages.append("texture redline path set mismatch broad CSV")
    if mesh_redlines_path is not None and set(mesh_redline_paths) != broad_mesh_redline_paths:
        messages.append("mesh redline path set mismatch broad CSV")
    if render_texture_redlines_path is not None and set(render_texture_redline_paths) != broad_render_texture_redline_paths:
        messages.append("RenderTexture redline path set mismatch broad CSV")
    if texture_redlines_path is not None and any(texture_flags_by_path.get(row.get("path", ""), "") != row.get("flags", "") for row in texture_redline_rows):
        messages.append("texture redline flags mismatch broad CSV")
    if mesh_redlines_path is not None and any(mesh_flags_by_path.get(row.get("path", ""), "") != row.get("flags", "") for row in mesh_redline_rows):
        messages.append("mesh redline flags mismatch broad CSV")
    if render_texture_redlines_path is not None and any(render_texture_flags_by_path.get(row.get("path", ""), "") != row.get("flags", "") for row in render_texture_redline_rows):
        messages.append("RenderTexture redline flags mismatch broad CSV")
    if render_texture_hotspots_path is not None and len(render_texture_hotspot_rows) != len(render_texture_hotspot_keys):
        messages.append("duplicate RenderTexture hotspot keys")
    if render_texture_hotspots_path is not None and render_texture_hotspot_keys != json_hotspot_keys:
        messages.append("RenderTexture hotspot identity mismatch between CSV and JSON")
    if texture_redlines_path is not None and set(texture_redline_paths) != set(json_texture_redline_flags_by_path.keys()):
        messages.append("texture redline path set mismatch JSON")
    if texture_redlines_path is not None and any(
        json_texture_redline_flags_by_path.get(row.get("path", ""), "") != row.get("flags", "")
        for row in texture_redline_rows
    ):
        messages.append("texture redline flags mismatch JSON")
    if texture_redlines_path is not None and any(
        json_texture_redline_dimensions_by_path.get(row.get("path", ""), ("", "", "", "")) != texture_redline_dimensions_by_path.get(row.get("path", ""), ("", "", "", ""))
        for row in texture_redline_rows
    ):
        messages.append("texture redline dimensions/estimate mismatch JSON")
    if mesh_redlines_path is not None and set(mesh_redline_paths) != set(json_mesh_redline_flags_by_path.keys()):
        messages.append("mesh redline path set mismatch JSON")
    if mesh_redlines_path is not None and any(
        json_mesh_redline_flags_by_path.get(row.get("path", ""), "") != row.get("flags", "")
        for row in mesh_redline_rows
    ):
        messages.append("mesh redline flags mismatch JSON")
    if set(json_render_texture_flags_by_path.keys()) != render_texture_path_set:
        messages.append("RenderTexture path set mismatch JSON")
    if any(
        json_render_texture_flags_by_path.get(path, "") != render_texture_flags_by_path.get(path, "")
        for path in render_texture_path_set
    ):
        messages.append("RenderTexture flags mismatch JSON")
    if any(
        json_render_texture_dimensions_by_path.get(path, ("", "", "")) != render_texture_dimensions_by_path.get(path, ("", "", ""))
        for path in render_texture_path_set
    ):
        messages.append("RenderTexture dimensions/estimate mismatch JSON")
    if any(not row.get("path", "").startswith(allowed_prefixes) for row in texture_rows):
        messages.append("texture rows outside import roots")
    if any(not row.get("path", "").startswith(allowed_prefixes) for row in mesh_rows):
        messages.append("mesh rows outside import roots")
    if any(not row.get("path", "").startswith(allowed_prefixes) for row in render_texture_rows):
        messages.append("RenderTexture rows outside import roots")
    if any(row.get("path", "").startswith("Docs/") for row in texture_rows):
        messages.append("texture rows include Docs/ paths")
    if any("_agent_screen_capture" in row.get("path", "") for row in texture_rows):
        messages.append("texture rows include _agent_screen_capture")
    if payload.get("critical_vram_overflow") and "CRITICAL_VRAM_OVERFLOW" not in payload.get("gate_reasons", []):
        messages.append("critical_vram_overflow missing CRITICAL_VRAM_OVERFLOW gate reason")
    require_snippets(
        summary_text,
        "summary",
        (
            "# VRAM Budget Audit Summary",
            "Evidence class: STATIC_SOURCE / FILESYSTEM. Runtime residency is PENDING VERIFICATION.",
            f"Scan roots: {', '.join(expected_roots)}.",
            f"- Texture files scanned: {len(texture_rows)}",
            f"- Mesh files scanned: {len(mesh_rows)}",
            f"- RenderTexture assets scanned: {len(render_texture_rows)}",
            f"- Texture VRAM crime rows: {payload.get('texture_vram_crime_rows')}",
            f"- Texture source-container risk rows: {payload.get('texture_source_container_risk_rows')}",
            f"- Mesh redline/risk rows: {payload.get('mesh_redline_rows')}",
            f"- RenderTexture redline/risk rows: {payload.get('render_texture_redline_rows')}",
            f"- Runtime RenderTexture source hotspots: {payload.get('runtime_render_texture_source_hotspot_rows')}",
            f"- link.xml status: {payload.get('link_xml_status')}",
        ),
    )
    require_snippets(
        plan_text,
        "remediation plan",
        (
            "# VRAM Remediation Plan",
            "Evidence class: STATIC_SOURCE / FILESYSTEM / PY_UNIT_TEST. No asset/import mutation performed.",
            f"- Runtime-candidate full-mip BC7: {float(payload.get('bc7_full_mip_runtime_candidate_mib', 0.0)):.2f} MiB",
            f"- First-party production full-mip BC7: {float(payload.get('bc7_full_mip_first_party_production_mib', 0.0)):.2f} MiB",
            f"- Texture VRAM crime rows: {payload.get('texture_vram_crime_rows')}",
            f"- Mesh redline/risk rows: {payload.get('mesh_redline_rows')}",
            f"- RenderTexture redline/risk rows: {payload.get('render_texture_redline_rows')}",
            f"- Runtime RenderTexture source hotspots: {payload.get('runtime_render_texture_source_hotspot_rows')}",
            "## Priority 1 - Quarantine Non-Production Runtime Payloads",
            "## Priority 2 - Convert Risky Texture Source Containers",
            "## Priority 3 - RenderTexture Static Assets",
            "## Priority 4 - Runtime RenderTexture Source Hotspots",
            "CI behavior: `python Tools/MemoryBudgetCheck.py --root . --ci` must fail until redlines are resolved or explicitly suppressed by future policy.",
        ),
    )

    if messages:
        return False, messages

    messages.append(
        "reports valid: "
        f"textures={len(texture_rows)} meshes={len(mesh_rows)} "
        f"render_textures={len(render_texture_rows)} "
        f"texture_redlines={len(texture_redline_rows)} mesh_redlines={len(mesh_redline_rows)} "
        f"rt_redlines={len(render_texture_redline_rows)} rt_hotspots={len(render_texture_hotspot_rows)} "
        f"scan_roots={','.join(expected_roots)}"
    )
    return True, messages


def write_summary(
    path: Path,
    root: Path,
    textures: Sequence[TextureRecord],
    meshes: Sequence[MeshRecord],
    render_textures: Sequence[RenderTextureRecord],
    atlas_groups: Sequence[Tuple[str, List[TextureRecord], int]],
    link_status: str,
    link_notes: Sequence[str],
    render_texture_hotspots: Optional[Sequence[RenderTextureSourceHit]] = None,
) -> None:
    total_bc7 = sum(record.bc7_bytes for record in textures)
    runtime_bc7 = sum(record.bc7_bytes for record in textures if is_runtime_candidate(record.path, root))
    first_party_bc7 = sum(record.bc7_bytes for record in textures if is_first_party_production_candidate(record.path, root))
    total_bc7_mips = total_bc7 * FULL_MIP_FACTOR
    runtime_bc7_mips = runtime_bc7 * FULL_MIP_FACTOR
    first_party_bc7_mips = first_party_bc7 * FULL_MIP_FACTOR
    texture_crimes = [record for record in textures if any(flag.startswith("VRAM CRIME") for flag in record.flags)]
    texture_container_risks = [record for record in textures if has_texture_container_risk(record)]
    first_party_texture_container_risks = [record for record in texture_container_risks if is_first_party_production_candidate(record.path, root)]
    mesh_redlines = [record for record in meshes if record.flags]
    mesh_import_risks = [record for record in meshes if any(flag.endswith("_STATIC_SUSPECT") for flag in record.flags)]
    first_party_mesh_import_risks = [record for record in mesh_import_risks if is_first_party_production_candidate(record.path, root)]
    mesh_geometry_bytes = sum(record.estimated_geometry_bytes for record in meshes)
    first_party_mesh_geometry_bytes = sum(record.estimated_geometry_bytes for record in meshes if is_first_party_production_candidate(record.path, root))
    mesh_geometry_redlines = [record for record in meshes if "MESH_GEOMETRY_ESTIMATE_GT_16MIB_STATIC" in record.flags]
    render_texture_bytes = sum(record.estimated_bytes for record in render_textures)
    render_texture_redlines = [record for record in render_textures if record.flags]
    render_texture_source_hits = resolve_render_texture_hotspots(root, render_texture_hotspots)
    runtime_render_texture_source_hits = [hit for hit in render_texture_source_hits if not hit.editor_only]
    first_party_streaming_off = large_streaming_mipmap_off(textures, root, limit=100000)
    overflow = total_bc7_mips > CRITICAL_TEXTURE_POOL_MIB * 1024 * 1024
    runtime_overflow = runtime_bc7_mips > CRITICAL_TEXTURE_POOL_MIB * 1024 * 1024
    now = _dt.datetime.now().isoformat(timespec="seconds")
    lines: List[str] = []
    lines.append("# VRAM Budget Audit Summary")
    lines.append("")
    lines.append(f"Generated: {now}")
    lines.append("Evidence class: STATIC_SOURCE / FILESYSTEM. Runtime residency is PENDING VERIFICATION.")
    lines.append(f"Scan roots: {', '.join(rel(path, root) for path in resolve_scan_roots(root))}. Non-import roots such as Docs/AgentLogs are excluded from asset residency totals.")
    lines.append("")
    lines.append("## Summary")
    lines.append("")
    lines.append(f"- Texture files scanned: {len(textures)}")
    lines.append(f"- Mesh files scanned: {len(meshes)}")
    lines.append(f"- RenderTexture assets scanned: {len(render_textures)}")
    lines.append(f"- Total BC7 no-mip estimate: {mib(total_bc7):.2f} MiB")
    lines.append(f"- Total BC7 full-mip estimate: {mib(total_bc7_mips):.2f} MiB")
    lines.append(f"- Runtime-candidate BC7 full-mip estimate: {mib(runtime_bc7_mips):.2f} MiB")
    lines.append(f"- First-party production BC7 full-mip estimate: {mib(first_party_bc7_mips):.2f} MiB")
    lines.append(f"- MX350 texture budget: {TEXTURE_BUDGET_MIB:.0f} MiB")
    lines.append(f"- Critical overflow trigger: {CRITICAL_TEXTURE_POOL_MIB:.1f} MiB")
    if overflow:
        lines.append("- [CRITICAL_VRAM_OVERFLOW] All scanned textures exceed 1.2GB static full-mip BC7 threshold.")
    if runtime_overflow:
        lines.append("- [CRITICAL_VRAM_OVERFLOW] Runtime-candidate textures exceed 1.2GB static full-mip BC7 threshold.")
    if not overflow and not runtime_overflow:
        lines.append("- No static BC7 full-mip overflow against 1.2GB trigger.")
    lines.append(f"- Texture VRAM crime rows: {len(texture_crimes)}")
    lines.append(f"- Texture source-container risk rows: {len(texture_container_risks)}")
    lines.append(f"- First-party texture source-container risk rows: {len(first_party_texture_container_risks)}")
    lines.append(f"- Static mesh geometry estimate: {mib(mesh_geometry_bytes):.2f} MiB / {GEOMETRY_BUFFER_BUDGET_MIB:.0f} MiB geometry budget")
    lines.append(f"- First-party static mesh geometry estimate: {mib(first_party_mesh_geometry_bytes):.2f} MiB")
    lines.append(f"- Mesh single-asset geometry estimate redlines: {len(mesh_geometry_redlines)}")
    lines.append(f"- Mesh redline/risk rows: {len(mesh_redlines)}")
    lines.append(f"- Mesh importer risk rows: {len(mesh_import_risks)}")
    lines.append(f"- First-party mesh importer risk rows: {len(first_party_mesh_import_risks)}")
    lines.append(f"- Static RenderTexture estimate: {mib(render_texture_bytes):.2f} MiB / {RENDER_TARGET_BUDGET_MIB:.0f} MiB RT+Depth budget")
    lines.append(f"- RenderTexture redline/risk rows: {len(render_texture_redlines)}")
    lines.append(f"- Runtime RenderTexture source hotspots: {len(runtime_render_texture_source_hits)}")
    lines.append(f"- First-party large textures with streaming mips off: {len(first_party_streaming_off)}")
    lines.append(f"- link.xml status: {link_status}")
    lines.append("")
    lines.append("## Top First-Party Texture Directories")
    lines.append("")
    lines.append("| Directory | Count | BC7 full mip MiB | VRAM crime rows |")
    lines.append("|---|---:|---:|---:|")
    for directory, count, total, crimes in texture_directory_costs(textures, root, first_party_only=True):
        lines.append(f"| {directory} | {count} | {mib(total):.2f} | {crimes} |")
    lines.append("")
    lines.append("## Top Runtime-Candidate Texture Directories")
    lines.append("")
    lines.append("| Directory | Count | BC7 full mip MiB | VRAM crime rows |")
    lines.append("|---|---:|---:|---:|")
    for directory, count, total, crimes in texture_directory_costs(textures, root, first_party_only=False):
        lines.append(f"| {directory} | {count} | {mib(total):.2f} | {crimes} |")
    lines.append("")
    lines.append("## Runtime Texture Extension Pressure")
    lines.append("")
    lines.append("| Extension | Count | BC7 full mip MiB | VRAM crime rows | Container risk rows |")
    lines.append("|---|---:|---:|---:|---:|")
    for ext, count, total, crimes, container_risks in texture_extension_costs(textures, root):
        lines.append(f"| {ext} | {count} | {mib(total):.2f} | {crimes} | {container_risks} |")
    lines.append("")
    lines.append("## Runtime Mesh Extension Pressure")
    lines.append("")
    lines.append("| Extension | Count | Known triangles | Triangle-unreadable rows | Geometry MiB | Flagged rows |")
    lines.append("|---|---:|---:|---:|---:|---:|")
    for ext, count, known_triangles, unreadable_rows, geometry_bytes, flagged_rows in mesh_extension_costs(meshes, root):
        lines.append(f"| {ext} | {count} | {known_triangles} | {unreadable_rows} | {mib(geometry_bytes):.2f} | {flagged_rows} |")
    lines.append("")
    lines.append("## RenderTexture Static Assets")
    lines.append("")
    lines.append("| Path | Size | Estimate MiB | Color | Depth | AA | Flags |")
    lines.append("|---|---:|---:|---:|---:|---:|---|")
    for record in sorted(render_textures, key=lambda item: item.estimated_bytes, reverse=True):
        lines.append(
            f"| {rel(record.path, root)} | {record.width}x{record.height} | {mib(record.estimated_bytes):.2f} | {record.color_format} | {record.depth_stencil_format} | {record.anti_aliasing} | {';'.join(record.flags)} |"
        )
    lines.append("")
    lines.append("## Runtime RenderTexture Source Hotspots")
    lines.append("")
    lines.append("| Path | Line | Pattern | Editor-only | Static evidence |")
    lines.append("|---|---:|---|---:|---|")
    for hit in render_texture_source_hits[:80]:
        lines.append(f"| {rel(hit.path, root)} | {hit.line} | {hit.pattern} | {str(hit.editor_only).lower()} | {hit.snippet} |")
    lines.append("")
    lines.append("## Top Runtime Texture Costs")
    lines.append("")
    lines.append("| Path | Size | BC7 full mip MiB | Flags |")
    lines.append("|---|---:|---:|---|")
    for record in top_texture_records(textures, root):
        lines.append(
            f"| {rel(record.path, root)} | {record.width}x{record.height} | {mib(record.bc7_bytes * FULL_MIP_FACTOR):.2f} | {';'.join(record.flags)} |"
        )
    lines.append("")
    lines.append("## Mesh Redlines")
    lines.append("")
    lines.append("| Path | File MiB | Triangles | Geometry MiB | LOD | Readable | Compression | BlendShapes | Flags |")
    lines.append("|---|---:|---:|---:|---:|---:|---:|---:|---|")
    for record in sorted(mesh_redlines, key=lambda item: (item.triangles or 0, item.file_bytes), reverse=True)[:40]:
        tri = "UNKNOWN" if record.triangles is None else str(record.triangles)
        lines.append(
            f"| {rel(record.path, root)} | {mib(record.file_bytes):.2f} | {tri} | {mib(record.estimated_geometry_bytes):.2f} | {str(record.lod_detected).lower()} | {record.meta_is_readable} | {record.meta_mesh_compression} | {record.meta_import_blend_shapes} | {';'.join(record.flags)} |"
        )
    lines.append("")
    lines.append("## Atlas Suggestions")
    lines.append("")
    lines.append("| Group | Count | Combined BC7 MiB | Members |")
    lines.append("|---|---:|---:|---|")
    for key, items, area in atlas_groups[:5]:
        members = ", ".join(item.path.name for item in items[:8])
        lines.append(f"| {key} | {len(items)} | {mib(area):.2f} | {members} |")
    lines.append("")
    lines.append("## Low-Tier Halving Candidates")
    lines.append("")
    lines.append("| Path | Source | Est. full-mip MiB saved by halving | Rationale |")
    lines.append("|---|---:|---:|---|")
    for record, saved in low_tier_halving(textures, root):
        lines.append(
            f"| {rel(record.path, root)} | {record.width}x{record.height} | {saved:.2f} | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |"
        )
    lines.append("")
    lines.append("## link.xml Check")
    lines.append("")
    for note in link_notes:
        lines.append(f"- {note}")
    lines.append("")
    lines.append("## Evidence Boundary")
    lines.append("")
    lines.append("- STATIC_SOURCE: file dimensions, file sizes, source metadata, and parser-readable mesh triangle counts.")
    lines.append(f"- Static geometry estimate assumes {STATIC_GEOMETRY_VERTEX_STRIDE_BYTES} byte vertices plus {STATIC_GEOMETRY_INDEX_BYTES} byte indices and no vertex sharing; Unity imported geometry must be verified in Memory Profiler.")
    lines.append("- Static RenderTexture estimates use YAML dimensions, MSAA, mip flag, color format, and depth-stencil format; transient and code-created RTs still require Unity runtime capture.")
    lines.append("- Runtime RenderTexture source hotspots are static code evidence only; dimensions and residency require Unity profiler capture.")
    lines.append(f"- Scan excludes generated/scratch directories by name: {', '.join(sorted(SKIP_DIRS, key=str.lower))}.")
    lines.append("- PENDING VERIFICATION: Unity importer compression, actual texture residency, mesh import settings, Memory Profiler VRAM, scene wiring, player-build behavior.")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def print_console_summary(
    root: Path,
    textures: Sequence[TextureRecord],
    meshes: Sequence[MeshRecord],
    render_textures: Sequence[RenderTextureRecord],
    link_status: str,
) -> bool:
    total_full_mips = sum(record.bc7_bytes for record in textures) * FULL_MIP_FACTOR
    runtime_full_mips = sum(record.bc7_bytes for record in textures if is_runtime_candidate(record.path, root)) * FULL_MIP_FACTOR
    mesh_geometry_bytes = sum(record.estimated_geometry_bytes for record in meshes)
    render_texture_bytes = sum(record.estimated_bytes for record in render_textures)
    texture_crime_count = sum(1 for record in textures if any(flag.startswith("VRAM CRIME") for flag in record.flags))
    mesh_risk_count = sum(1 for record in meshes if record.flags)
    render_texture_risk_count = sum(1 for record in render_textures if record.flags)
    critical_overflow = total_full_mips > CRITICAL_TEXTURE_POOL_MIB * 1024 * 1024 or runtime_full_mips > CRITICAL_TEXTURE_POOL_MIB * 1024 * 1024
    print(f"textures={len(textures)} meshes={len(meshes)} render_textures={len(render_textures)}")
    print(f"bc7_full_mip_total_mib={mib(total_full_mips):.2f}")
    print(f"bc7_full_mip_runtime_candidate_mib={mib(runtime_full_mips):.2f}")
    print(f"mesh_geometry_static_estimate_mib={mib(mesh_geometry_bytes):.2f}")
    print(f"render_texture_static_estimate_mib={mib(render_texture_bytes):.2f}")
    if critical_overflow:
        print("[CRITICAL_VRAM_OVERFLOW]")
    print(f"texture_vram_crimes={texture_crime_count}")
    print(f"mesh_redline_or_unknown_rows={mesh_risk_count}")
    print(f"render_texture_redline_or_risk_rows={render_texture_risk_count}")
    print(f"link_xml_status={link_status}")
    return texture_crime_count > 0 or mesh_risk_count > 0 or render_texture_risk_count > 0 or critical_overflow


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="Audit textures and meshes against HECTON-8 MX350 budgets.")
    parser.add_argument("--root", default=".", help="Project root. Default: current directory.")
    parser.add_argument("--csv", default="Docs/Reports/VRAM_Budget_Audit.csv", help="CSV report path.")
    parser.add_argument("--summary", default="Docs/Reports/VRAM_Budget_Audit_Summary.md", help="Markdown summary path.")
    parser.add_argument("--plan", default="Docs/Reports/VRAM_Remediation_Plan.md", help="Markdown remediation plan path.")
    parser.add_argument("--json", default="Docs/Reports/VRAM_Budget_Audit.json", help="Machine-readable summary path.")
    parser.add_argument("--texture-redlines", default="Docs/Reports/VRAM_Texture_Redlines.csv", help="Texture redline CSV path.")
    parser.add_argument("--mesh-redlines", default="Docs/Reports/VRAM_Mesh_Redlines.csv", help="Mesh redline CSV path.")
    parser.add_argument("--render-texture-redlines", default="Docs/Reports/VRAM_RenderTexture_Redlines.csv", help="RenderTexture redline CSV path.")
    parser.add_argument("--render-texture-hotspots", default="Docs/Reports/VRAM_RenderTexture_SourceHotspots.csv", help="RenderTexture source hotspot CSV path.")
    parser.add_argument("--workers", type=int, default=DEFAULT_AUDIT_WORKERS, help=f"Parallel asset audit workers, 1-{MAX_AUDIT_WORKERS}; <=0 uses default {DEFAULT_AUDIT_WORKERS}.")
    parser.add_argument("--ci", action="store_true", help="Exit non-zero if static redlines or overflow are detected.")
    parser.add_argument("--validate-reports", action="store_true", help="Validate existing generated reports without scanning assets.")
    args = parser.parse_args(argv)

    root = Path(args.root).resolve()
    if args.validate_reports:
        ok, messages = validate_generated_reports(
            root,
            root / args.csv,
            root / args.json,
            root / args.texture_redlines,
            root / args.mesh_redlines,
            root / args.render_texture_redlines,
            root / args.render_texture_hotspots,
            root / args.summary,
            root / args.plan,
        )
        for message in messages:
            print(message)
        return 0 if ok else 2

    workers = normalize_worker_count(args.workers)
    textures_paths, mesh_paths, render_texture_paths, link_xml_paths = iter_asset_and_link_paths(root)
    textures = audit_textures(textures_paths, root, workers)
    meshes = audit_meshes(mesh_paths, workers)
    render_textures = audit_render_textures(render_texture_paths, workers)
    link_status, link_notes = summarize_link_xml(link_xml_paths, root)
    ci_failure = print_console_summary(root, textures, meshes, render_textures, link_status)
    if args.ci:
        return 2 if ci_failure else 0

    render_texture_hotspots = find_render_texture_source_hotspots(root)
    atlas_groups = assign_atlas_groups(textures, root)

    write_csv(root / args.csv, root, textures, meshes, render_textures)
    write_texture_redlines_csv(root / args.texture_redlines, root, textures)
    write_mesh_redlines_csv(root / args.mesh_redlines, root, meshes)
    write_render_texture_redlines_csv(root / args.render_texture_redlines, root, render_textures)
    write_render_texture_source_hotspots_csv(root / args.render_texture_hotspots, root, render_texture_hotspots)
    write_summary(root / args.summary, root, textures, meshes, render_textures, atlas_groups, link_status, link_notes, render_texture_hotspots)
    write_remediation_plan(root / args.plan, root, textures, meshes, render_textures, atlas_groups, render_texture_hotspots)
    write_summary_json(root / args.json, root, textures, meshes, render_textures, atlas_groups, link_status, link_notes, render_texture_hotspots)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
