#!/usr/bin/env python3
"""Static VRAM and mesh budget scanner for HECTON-8.

Evidence boundary:
    STATIC_SOURCE / FILESYSTEM only. This tool does not prove Unity import,
    runtime residency, Memory Profiler state, or player-build VRAM usage.
"""

from __future__ import annotations

import argparse
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
from typing import Dict, Iterable, Iterator, List, Optional, Sequence, Tuple


TEXTURE_EXTS = {".png", ".jpg", ".jpeg"}
MESH_EXTS = {".fbx", ".obj"}
SKIP_DIRS = {".git", ".vs", ".codex-build", ".codex-artifacts", "Library", "Temp", "Obj", "Build", "Builds"}
SKIP_DIR_NAMES_LOWER = {name.lower() for name in SKIP_DIRS}
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


def iter_assets(root: Path) -> Tuple[List[Path], List[Path]]:
    textures: List[Path] = []
    meshes: List[Path] = []
    for current_root, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d.lower() not in SKIP_DIR_NAMES_LOWER]
        current = Path(current_root)
        for filename in files:
            path = current / filename
            ext = path.suffix.lower()
            if ext in TEXTURE_EXTS:
                textures.append(path)
            elif ext in MESH_EXTS:
                meshes.append(path)
    textures.sort(key=lambda p: rel(p, root).lower())
    meshes.sort(key=lambda p: rel(p, root).lower())
    return textures, meshes


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


def read_image_size(path: Path) -> Tuple[int, int, str]:
    ext = path.suffix.lower()
    if ext == ".png":
        return read_png_size(path)
    if ext in (".jpg", ".jpeg"):
        return read_jpeg_size(path)
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
    if width <= 0 or height <= 0:
        flags.append("VRAM CRIME: TEXTURE_DIMENSIONS_UNREADABLE")
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
    if "normal" in lower_name or "_norm" in lower_name or lower_name.endswith("_n.png"):
        recommendation = "Use BC5 normal import; Low tier cap 1024 unless hero close-read asset."
    elif any(token in lower_name for token in ("ao", "rough", "smooth", "metal", "spec", "mask")):
        recommendation = "Channel-pack masks into one RGBA texture; avoid separate AO/spec maps."
    elif max_dim > LOW_TIER_TARGET_DIM:
        recommendation = "Low tier should halve source/import max; keep high mips only with hero or streaming proof."
    elif path.suffix.lower() in (".jpg", ".jpeg"):
        recommendation = "Verify Unity import compression; JPG disk compression does not reduce VRAM."
    else:
        recommendation = "Keep compressed; atlas if grouped with small sibling textures."
    return flags, recommendation


def audit_textures(paths: Sequence[Path], root: Path) -> List[TextureRecord]:
    records: List[TextureRecord] = []
    for path in paths:
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
        records.append(record)
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


def audit_meshes(paths: Sequence[Path]) -> List[MeshRecord]:
    lod_map = build_lod_map(paths)
    records: List[MeshRecord] = []
    for path in paths:
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
        if record.triangles is None:
            record.flags.append("TRIANGLE_COUNT_UNREADABLE_STATIC")
            if record.file_bytes > FBX_SIZE_RISK_BYTES and not record.lod_detected:
                record.flags.append("MESH_SIZE_RISK_NO_LOD_STATIC")
        else:
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
        records.append(record)
    return records


def mib(value: float) -> float:
    return value / (1024.0 * 1024.0)


def write_csv(path: Path, root: Path, textures: Sequence[TextureRecord], meshes: Sequence[MeshRecord]) -> None:
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
                "lod_detected",
                "mesh_meta_is_readable",
                "mesh_meta_compression",
                "mesh_meta_optimize_mesh",
                "mesh_meta_import_blend_shapes",
                "mesh_meta_add_colliders",
                "mesh_meta_generate_secondary_uv",
                "mesh_meta_keep_quads",
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
                    str(record.lod_detected).lower(),
                    record.meta_is_readable,
                    record.meta_mesh_compression,
                    record.meta_optimize_mesh,
                    record.meta_import_blend_shapes,
                    record.meta_add_colliders,
                    record.meta_generate_secondary_uv,
                    record.meta_keep_quads,
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


def write_remediation_plan(
    path: Path,
    root: Path,
    textures: Sequence[TextureRecord],
    meshes: Sequence[MeshRecord],
    atlas_groups: Sequence[Tuple[str, List[TextureRecord], int]],
) -> None:
    texture_crimes = [record for record in textures if any(flag.startswith("VRAM CRIME") for flag in record.flags)]
    mesh_redlines = [record for record in meshes if record.flags]
    mesh_import_risks = [record for record in meshes if any(flag.endswith("_STATIC_SUSPECT") for flag in record.flags)]
    first_party_mesh_import_risks = [record for record in mesh_import_risks if is_first_party_production_candidate(record.path, root)]
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
    lines.append(f"- Mesh redline/risk rows: {len(mesh_redlines)}")
    lines.append(f"- Mesh importer risk rows: {len(mesh_import_risks)}")
    lines.append(f"- First-party mesh importer risk rows: {len(first_party_mesh_import_risks)}")
    lines.append("- CI behavior: `python Tools/MemoryBudgetCheck.py --root . --ci` must fail until redlines are resolved or explicitly suppressed by future policy.")
    lines.append("")
    lines.append("## Priority 1 - Quarantine Non-Production Runtime Payloads")
    lines.append("")
    lines.append("| Directory | Count | BC7 full mip MiB | VRAM crime rows | Required action |")
    lines.append("|---|---:|---:|---:|---|")
    for directory, count, total, crimes in non_first_party_runtime_costs(textures, root):
        lines.append(f"| {directory} | {count} | {mib(total):.2f} | {crimes} | Prove production use, move to editor-only/demo quarantine, or exclude from Addressables/build payload. |")
    lines.append("")
    lines.append("## Priority 2 - Clamp First-Party Large Textures")
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
    lines.append("## Priority 3 - Enable Streaming Mipmaps On Large First-Party Textures")
    lines.append("")
    lines.append("| Path | Source | Streaming metadata | Required action |")
    lines.append("|---|---:|---|---|")
    for record in large_streaming_mipmap_off(textures, root):
        lines.append(f"| {rel(record.path, root)} | {record.width}x{record.height} | {record.meta_streaming_mipmaps} | Enable streaming mips unless UI/non-mipped proof exists. |")
    lines.append("")
    lines.append("## Priority 4 - Atlas Small First-Party Texture Families")
    lines.append("")
    lines.append("| Group | Count | Combined BC7 MiB | Required action |")
    lines.append("|---|---:|---:|---|")
    for key, items, area in atlas_groups[:5]:
        lines.append(f"| {key} | {len(items)} | {mib(area):.2f} | Build one atlas/material family or justify separate residency. |")
    lines.append("")
    lines.append("## Priority 5 - Mesh LOD And Importer Redlines")
    lines.append("")
    lines.append("| Path | Triangles | LOD detected | Readable | Compression | BlendShapes | Flags | Required action |")
    lines.append("|---|---:|---:|---:|---:|---:|---|---|")
    for record in sorted(mesh_redlines, key=lambda item: (item.triangles or 0, item.file_bytes), reverse=True):
        tri = "UNKNOWN" if record.triangles is None else str(record.triangles)
        lines.append(
            f"| {rel(record.path, root)} | {tri} | {str(record.lod_detected).lower()} | {record.meta_is_readable} | {record.meta_mesh_compression} | {record.meta_import_blend_shapes} | {';'.join(record.flags)} | {record.recommendation} |"
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
    atlas_groups: Sequence[Tuple[str, List[TextureRecord], int]],
    link_status: str,
    link_notes: Sequence[str],
) -> Dict[str, object]:
    total_bc7 = sum(record.bc7_bytes for record in textures)
    runtime_bc7 = sum(record.bc7_bytes for record in textures if is_runtime_candidate(record.path, root))
    first_party_bc7 = sum(record.bc7_bytes for record in textures if is_first_party_production_candidate(record.path, root))
    texture_crimes = [record for record in textures if any(flag.startswith("VRAM CRIME") for flag in record.flags)]
    texture_flagged = [record for record in textures if record.flags]
    mesh_redlines = [record for record in meshes if record.flags]
    mesh_import_risks = [record for record in meshes if any(flag.endswith("_STATIC_SUSPECT") for flag in record.flags)]
    first_party_mesh_import_risks = [record for record in mesh_import_risks if is_first_party_production_candidate(record.path, root)]
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
    return {
        "schema_version": 1,
        "generated": _dt.datetime.now().isoformat(timespec="seconds"),
        "generated_utc": _dt.datetime.now(_dt.timezone.utc).isoformat(timespec="seconds"),
        "evidence_class": "STATIC_SOURCE/FILESYSTEM/PY_UNIT_TEST",
        "root": str(root),
        "skipped_directory_names": sorted(SKIP_DIRS, key=str.lower),
        "texture_count": len(textures),
        "mesh_count": len(meshes),
        "bc7_no_mip_mib": round(mib(total_bc7), 3),
        "bc7_full_mip_total_mib": round(mib(total_full_mips), 3),
        "bc7_full_mip_runtime_candidate_mib": round(mib(runtime_full_mips), 3),
        "bc7_full_mip_first_party_production_mib": round(mib(first_party_bc7 * FULL_MIP_FACTOR), 3),
        "mx350_texture_budget_mib": TEXTURE_BUDGET_MIB,
        "critical_texture_pool_mib": CRITICAL_TEXTURE_POOL_MIB,
        "critical_vram_overflow": critical,
        "texture_vram_crime_rows": len(texture_crimes),
        "texture_flagged_rows": len(texture_flagged),
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
        "atlas_suggestions": [
            {
                "group": key,
                "count": len(items),
                "combined_bc7_mib": round(mib(area), 3),
                "members": [item.path.name for item in items],
            }
            for key, items, area in atlas_groups[:5]
        ],
        "mesh_redlines": [
            {
                "path": rel(record.path, root),
                "triangles": record.triangles,
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
    atlas_groups: Sequence[Tuple[str, List[TextureRecord], int]],
    link_status: str,
    link_notes: Sequence[str],
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = build_summary_payload(root, textures, meshes, atlas_groups, link_status, link_notes)
    path.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def write_summary(
    path: Path,
    root: Path,
    textures: Sequence[TextureRecord],
    meshes: Sequence[MeshRecord],
    atlas_groups: Sequence[Tuple[str, List[TextureRecord], int]],
    link_status: str,
    link_notes: Sequence[str],
) -> None:
    total_bc7 = sum(record.bc7_bytes for record in textures)
    runtime_bc7 = sum(record.bc7_bytes for record in textures if is_runtime_candidate(record.path, root))
    first_party_bc7 = sum(record.bc7_bytes for record in textures if is_first_party_production_candidate(record.path, root))
    total_bc7_mips = total_bc7 * FULL_MIP_FACTOR
    runtime_bc7_mips = runtime_bc7 * FULL_MIP_FACTOR
    first_party_bc7_mips = first_party_bc7 * FULL_MIP_FACTOR
    texture_crimes = [record for record in textures if any(flag.startswith("VRAM CRIME") for flag in record.flags)]
    mesh_redlines = [record for record in meshes if record.flags]
    mesh_import_risks = [record for record in meshes if any(flag.endswith("_STATIC_SUSPECT") for flag in record.flags)]
    first_party_mesh_import_risks = [record for record in mesh_import_risks if is_first_party_production_candidate(record.path, root)]
    first_party_streaming_off = large_streaming_mipmap_off(textures, root, limit=100000)
    overflow = total_bc7_mips > CRITICAL_TEXTURE_POOL_MIB * 1024 * 1024
    runtime_overflow = runtime_bc7_mips > CRITICAL_TEXTURE_POOL_MIB * 1024 * 1024
    now = _dt.datetime.now().isoformat(timespec="seconds")
    lines: List[str] = []
    lines.append("# VRAM Budget Audit Summary")
    lines.append("")
    lines.append(f"Generated: {now}")
    lines.append("Evidence class: STATIC_SOURCE / FILESYSTEM. Runtime residency is PENDING VERIFICATION.")
    lines.append("")
    lines.append("## Summary")
    lines.append("")
    lines.append(f"- Texture files scanned: {len(textures)}")
    lines.append(f"- Mesh files scanned: {len(meshes)}")
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
    lines.append(f"- Mesh redline/risk rows: {len(mesh_redlines)}")
    lines.append(f"- Mesh importer risk rows: {len(mesh_import_risks)}")
    lines.append(f"- First-party mesh importer risk rows: {len(first_party_mesh_import_risks)}")
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
    lines.append("| Path | File MiB | Triangles | LOD | Readable | Compression | BlendShapes | Flags |")
    lines.append("|---|---:|---:|---:|---:|---:|---:|---|")
    for record in sorted(mesh_redlines, key=lambda item: (item.triangles or 0, item.file_bytes), reverse=True)[:40]:
        tri = "UNKNOWN" if record.triangles is None else str(record.triangles)
        lines.append(
            f"| {rel(record.path, root)} | {mib(record.file_bytes):.2f} | {tri} | {str(record.lod_detected).lower()} | {record.meta_is_readable} | {record.meta_mesh_compression} | {record.meta_import_blend_shapes} | {';'.join(record.flags)} |"
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
    lines.append(f"- Scan excludes generated/scratch directories by name: {', '.join(sorted(SKIP_DIRS, key=str.lower))}.")
    lines.append("- PENDING VERIFICATION: Unity importer compression, actual texture residency, mesh import settings, Memory Profiler VRAM, scene wiring, player-build behavior.")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def print_console_summary(root: Path, textures: Sequence[TextureRecord], meshes: Sequence[MeshRecord], link_status: str) -> bool:
    total_full_mips = sum(record.bc7_bytes for record in textures) * FULL_MIP_FACTOR
    runtime_full_mips = sum(record.bc7_bytes for record in textures if is_runtime_candidate(record.path, root)) * FULL_MIP_FACTOR
    texture_crime_count = sum(1 for record in textures if any(flag.startswith("VRAM CRIME") for flag in record.flags))
    mesh_risk_count = sum(1 for record in meshes if record.flags)
    critical_overflow = total_full_mips > CRITICAL_TEXTURE_POOL_MIB * 1024 * 1024 or runtime_full_mips > CRITICAL_TEXTURE_POOL_MIB * 1024 * 1024
    print(f"textures={len(textures)} meshes={len(meshes)}")
    print(f"bc7_full_mip_total_mib={mib(total_full_mips):.2f}")
    print(f"bc7_full_mip_runtime_candidate_mib={mib(runtime_full_mips):.2f}")
    if critical_overflow:
        print("[CRITICAL_VRAM_OVERFLOW]")
    print(f"texture_vram_crimes={texture_crime_count}")
    print(f"mesh_redline_or_unknown_rows={mesh_risk_count}")
    print(f"link_xml_status={link_status}")
    return texture_crime_count > 0 or mesh_risk_count > 0 or critical_overflow


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="Audit textures and meshes against HECTON-8 MX350 budgets.")
    parser.add_argument("--root", default=".", help="Project root. Default: current directory.")
    parser.add_argument("--csv", default="Docs/Reports/VRAM_Budget_Audit.csv", help="CSV report path.")
    parser.add_argument("--summary", default="Docs/Reports/VRAM_Budget_Audit_Summary.md", help="Markdown summary path.")
    parser.add_argument("--plan", default="Docs/Reports/VRAM_Remediation_Plan.md", help="Markdown remediation plan path.")
    parser.add_argument("--json", default="Docs/Reports/VRAM_Budget_Audit.json", help="Machine-readable summary path.")
    parser.add_argument("--texture-redlines", default="Docs/Reports/VRAM_Texture_Redlines.csv", help="Texture redline CSV path.")
    parser.add_argument("--mesh-redlines", default="Docs/Reports/VRAM_Mesh_Redlines.csv", help="Mesh redline CSV path.")
    parser.add_argument("--ci", action="store_true", help="Exit non-zero if static redlines or overflow are detected.")
    args = parser.parse_args(argv)

    root = Path(args.root).resolve()
    textures_paths, mesh_paths = iter_assets(root)
    textures = audit_textures(textures_paths, root)
    meshes = audit_meshes(mesh_paths)
    link_status, link_notes = summarize_link_xml(find_link_xml(root), root)
    ci_failure = print_console_summary(root, textures, meshes, link_status)
    if args.ci:
        return 2 if ci_failure else 0

    atlas_groups = assign_atlas_groups(textures, root)

    write_csv(root / args.csv, root, textures, meshes)
    write_texture_redlines_csv(root / args.texture_redlines, root, textures)
    write_mesh_redlines_csv(root / args.mesh_redlines, root, meshes)
    write_summary(root / args.summary, root, textures, meshes, atlas_groups, link_status, link_notes)
    write_remediation_plan(root / args.plan, root, textures, meshes, atlas_groups)
    write_summary_json(root / args.json, root, textures, meshes, atlas_groups, link_status, link_notes)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
