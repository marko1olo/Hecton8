#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path
from typing import Any


TOUCHED_CSHARP_FILES = (
    "Assets/_Project/Scripts/Core/Memory/H8Memory.cs",
    "Assets/_Project/Scripts/Core/HardwareTierDetector.cs",
    "Assets/_Project/Scripts/Core/InstanceCullingServiceRegistryBridge.cs",
    "Assets/_Project/Scripts/Core/SystemDispatcher.cs",
    "Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.cs",
    "Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs",
    "Assets/_Project/Scripts/Construction/DroneFleetManager.cs",
    "Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs",
    "Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs",
    "Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs",
    "Assets/_Project/Scripts/Dev/HabitatStressSmokeTester.cs",
    "Assets/_Project/Scripts/Graphics/Culling/InstanceCullingService.cs",
    "Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs",
    "Assets/_Project/Scripts/HectonCelestialEngine.cs",
    "Assets/_Project/Scripts/HectonBoidController.cs",
    "Assets/_Project/Scripts/HectonFluidEngine.cs",
    "Assets/_Project/Scripts/HectonRockManager.cs",
    "Assets/_Project/Scripts/HectonUnderwaterVisuals.cs",
    "Assets/_Project/Scripts/WorldProceduralScatterDirector.cs",
    "Assets/_Project/Scripts/Networking/RollbackNetcodeContracts.cs",
    "Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs",
    "Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs",
    "Assets/_Project/Scripts/Plugins/Crest/Crest4KinematicsAdapter.cs",
    "Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerFeature.cs",
    "Assets/_Project/Scripts/Rendering/OceanSinglePass/HectonSinglePassOceanFeature.cs",
    "Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs",
    "Assets/_Project/Scripts/SubmarineStructuralGrid.cs",
    "Assets/_Project/Scripts/UI/PDAMapTab.cs",
    "Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs",
    "Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs",
    "Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs",
    "Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs",
    "Assets/_Project/Scripts/VFX/JacobianFoam/HectonJacobianFoamRenderFeature.cs",
    "Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs",
    "Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmGpuRuntime.cs",
    "Assets/_Project/Scripts/VFX/VfxComputeParticleBudgetCatalog.cs",
    "Assets/_Project/Scripts/Visor/HectonBiolumSSGIFeature.cs",
    "Assets/_Project/Scripts/Visor/HectonScooterVolumetricShaftsFeature.cs",
    "Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs",
    "Assets/_Project/Scripts/Visor/HectonVoxelSsaoFeature.cs",
    "Assets/_Project/Scripts/Visor/VolumetricLightFeature.cs",
    "Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs",
    "Assets/_Project/Scripts/World/FloraInteractionManager.cs",
    "Assets/_Project/Scripts/World/GPUScatterDirector.cs",
    "Assets/_Project/Scripts/World/HectonOctahedralImpostorRenderer.cs",
    "Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs",
    "Assets/_Project/Scripts/World/ScatterInstancingService.cs",
    "Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs",
    "Assets/_Project/Scripts/World/SargassumCrestDampingController.cs",
    "Assets/_Project/Scripts/World/SargassumCutManager.cs",
    "Assets/_Project/Tests/Editor/ComputeDispatchSizingEditTests.cs",
)

VAULT_OWNER_NATIVE_FIELD_FILES = {
    "Assets/_Project/Scripts/Core/Memory/H8Memory.cs",
    "Assets/_Project/Scripts/Graphics/Culling/TBDRPipelineSurgeonTypes.cs",
}

AGENT_OWNED_BUFFER_ID_CONSTS = {
    "FloraAgeBufferId",
    "CpuCullingMatricesBufferId",
    "CpuCullingDataBufferId",
}

NATIVE_TYPES = (
    "NativeArray",
    "NativeList",
    "NativeQueue",
    "NativeParallelHashMap",
    "NativeParallelMultiHashMap",
    "UnsafeList",
)

HOT_METHOD_NAMES = (
    "Update",
    "FixedUpdate",
    "LateUpdate",
    "OnPerformCulling",
    "Tick",
    "SlowTick",
    "LateFrameTick",
    "Execute",
)
VALUE_TYPE_NEW_PREFIXES = (
    "float2",
    "float3",
    "float4",
    "float4x4",
    "double2",
    "double3",
    "double4",
    "int2",
    "int3",
    "int4",
    "uint2",
    "uint3",
    "uint4",
    "bool2",
    "bool3",
    "bool4",
    "Vector2",
    "Vector3",
    "Vector4",
    "Bounds",
    "Rect",
    "Quaternion",
    "RenderParams",
    "NativeArray",
    "NativeList",
    "NativeQueue",
    "NativeParallelHashMap",
    "NativeParallelMultiHashMap",
    "UnsafeList",
)

FIELD_TYPE_SIZE = {
    "byte": 1,
    "sbyte": 1,
    "bool": 1,
    "short": 2,
    "ushort": 2,
    "SystemID": 2,
    "int": 4,
    "uint": 4,
    "float": 4,
    "Allocator": 4,
    "BufferID": 4,
    "Vector2": 8,
    "float2": 8,
    "int2": 8,
    "uint2": 8,
    "long": 8,
    "ulong": 8,
    "double": 8,
    "IntPtr": 8,
    "float3": 12,
    "Vector3": 12,
    "int3": 12,
    "uint3": 12,
    "float4": 16,
    "Vector4": 16,
    "quaternion": 16,
    "Quaternion": 16,
    "int4": 16,
    "uint4": 16,
    "double2": 16,
    "double3": 24,
    "AbsoluteUniversePosition": 48,
    "AbsoluteUniversePositionBlit128": 48,
    "float4x4": 64,
    "Matrix4x4": 64,
    "FixedString64Bytes": 64,
}

POINTER_FIRST_8BYTE_TYPES = {"long", "ulong", "double", "IntPtr"}
ALIGNED_AGGREGATE_TYPES = {"double2", "double3", "AbsoluteUniversePosition", "AbsoluteUniversePositionBlit128"}


def read_text(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8-sig")
    except UnicodeDecodeError:
        return path.read_text(encoding="utf-8", errors="replace")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def strip_line_comments(line: str) -> str:
    index = line.find("//")
    return line if index < 0 else line[:index]


def line_delta(line: str) -> int:
    stripped = strip_line_comments(line)
    return stripped.count("{") - stripped.count("}")


def native_type_in(line: str) -> str | None:
    for native_type in NATIVE_TYPES:
        if re.search(rf"\b{native_type}\s*<", line):
            return native_type
    return None


def normalize_type_name(type_name: str) -> str:
    clean = type_name.strip()
    clean = clean.replace("readonly ", "")
    clean = clean.replace("ref ", "")
    clean = clean.replace("in ", "")
    clean = clean.rstrip("*")
    clean = clean.split("<", 1)[0]
    clean = clean.split(".", 1)[-1]
    return clean


def is_padding_field(field_name: str) -> bool:
    lower = field_name.lower()
    return (
        lower.startswith("_pad") or
        lower.startswith("pad") or
        "padding" in lower or
        lower.startswith("reserved") or
        lower.startswith("_reserved")
    )


def is_8byte_layout_field(type_name: str, field_name: str) -> bool:
    if is_padding_field(field_name):
        return False
    normalized = normalize_type_name(type_name)
    return (
        normalized in POINTER_FIRST_8BYTE_TYPES or
        normalized in ALIGNED_AGGREGATE_TYPES or
        type_name.strip().endswith("*")
    )


def is_smaller_layout_field(type_name: str, field_name: str) -> bool:
    if is_padding_field(field_name):
        return False
    normalized = normalize_type_name(type_name)
    size = FIELD_TYPE_SIZE.get(normalized)
    return size is not None and size < 8


def is_type_start(line: str) -> tuple[str, str] | None:
    match = re.search(
        r"\b(?:(?:public|private|protected|internal|static|sealed|partial|abstract|unsafe|readonly)\s+)*"
        r"(class|struct)\s+([A-Za-z_][A-Za-z0-9_]*)\b",
        line,
    )
    if not match:
        return None
    return match.group(1), match.group(2)


def is_method_start(line: str) -> str | None:
    stripped = strip_line_comments(line).strip()
    if not stripped or stripped.startswith(("if ", "if(", "for ", "for(", "foreach", "while ", "while(", "switch ", "switch(")):
        return None
    match = re.search(
        r"\b(?:(?:public|private|protected|internal|static|sealed|override|virtual|unsafe|readonly|async|extern)\s+)*"
        r"(?:[A-Za-z_][A-Za-z0-9_<>,\[\]\.\?]*|void)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\([^;]*\)",
        stripped,
    )
    if not match:
        return None
    if "=>" in stripped and "{" not in stripped:
        return None
    return match.group(1)


def looks_like_field(line: str) -> bool:
    stripped = strip_line_comments(line).strip()
    if not stripped.endswith(";"):
        return False
    if "(" in stripped or "=>" in stripped:
        return False
    if not re.match(r"(?:\[.*\]\s*)*(public|private|protected|internal|static|readonly|const|volatile)\b", stripped):
        return False
    return True


def classify_native_collections(path: str, text: str) -> tuple[list[dict[str, Any]], list[dict[str, Any]], list[dict[str, Any]]]:
    declarations: list[dict[str, Any]] = []
    persistent_fields: list[dict[str, Any]] = []
    job_views: list[dict[str, Any]] = []
    type_stack: list[dict[str, Any]] = []
    method_stack: list[dict[str, Any]] = []
    pending_type: tuple[str, str, int] | None = None
    pending_method: tuple[str, int, bool] | None = None
    depth = 0
    lines = text.splitlines()

    for line_index, line in enumerate(lines, start=1):
        native_type = native_type_in(line)
        current_method = method_stack[-1] if method_stack else None
        current_type = type_stack[-1] if type_stack else None
        if native_type is not None:
            scope = "method_or_local"
            if current_method is None and current_type is not None and looks_like_field(line):
                type_kind = current_type["kind"]
                scope = f"{type_kind}_field"
                record = {
                    "path": path,
                    "line": line_index,
                    "nativeType": native_type,
                    "scope": scope,
                    "isField": True,
                    "ownerType": current_type["name"],
                    "declaration": line.strip(),
                }
                declarations.append(record)
                if type_kind == "class":
                    if path in VAULT_OWNER_NATIVE_FIELD_FILES:
                        record["scope"] = "vault_owner_internal_field"
                    else:
                        persistent_fields.append(record)
                else:
                    job_views.append(record)
            else:
                scope = "job_or_transient_view" if current_type and current_type["kind"] == "struct" else "method_or_local"
                record = {
                    "path": path,
                    "line": line_index,
                    "nativeType": native_type,
                    "scope": scope,
                    "isField": False,
                    "ownerType": current_type["name"] if current_type else None,
                    "method": current_method["name"] if current_method else None,
                    "declaration": line.strip(),
                }
                declarations.append(record)
                if scope == "job_or_transient_view":
                    job_views.append(record)

        if pending_type is None:
            type_match = is_type_start(line)
            if type_match:
                pending_type = (type_match[0], type_match[1], line_index)
        if pending_method is None:
            method_name = is_method_start(line)
            if method_name:
                pending_method = (
                    method_name,
                    line_index,
                    method_name in HOT_METHOD_NAMES or method_name.endswith("Tick") or method_name == "Execute",
                )

        opens = strip_line_comments(line).count("{")
        closes = strip_line_comments(line).count("}")
        for _ in range(opens):
            depth += 1
            if pending_type is not None:
                kind, name, start_line = pending_type
                type_stack.append({"kind": kind, "name": name, "startLine": start_line, "depth": depth})
                pending_type = None
                continue
            if pending_method is not None:
                name, start_line, hot = pending_method
                method_stack.append({"name": name, "startLine": start_line, "depth": depth, "hot": hot})
                pending_method = None

        for _ in range(closes):
            while method_stack and method_stack[-1]["depth"] == depth:
                method_stack.pop()
            while type_stack and type_stack[-1]["depth"] == depth:
                type_stack.pop()
            depth = max(0, depth - 1)

        if ";" in strip_line_comments(line):
            if pending_type is not None and "{" not in line:
                pending_type = None
            if pending_method is not None and "{" not in line:
                pending_method = None

    return declarations, persistent_fields, job_views


def collect_method_blocks(text: str) -> list[dict[str, Any]]:
    blocks: list[dict[str, Any]] = []
    lines = text.splitlines()
    pending: tuple[str, int, bool] | None = None
    depth = 0
    active: dict[str, Any] | None = None
    content: list[str] = []
    for line_index, line in enumerate(lines, start=1):
        if pending is None:
            method_name = is_method_start(line)
            if method_name:
                pending = (
                    method_name,
                    line_index,
                    method_name in HOT_METHOD_NAMES or method_name.endswith("Tick") or method_name == "Execute",
                )
        opens = strip_line_comments(line).count("{")
        closes = strip_line_comments(line).count("}")
        for _ in range(opens):
            depth += 1
            if pending is not None and active is None:
                name, start_line, hot = pending
                active = {"name": name, "startLine": start_line, "depth": depth, "hot": hot}
                content = []
                pending = None
        if active is not None:
            content.append(line)
        for _ in range(closes):
            if active is not None and active["depth"] == depth:
                active["body"] = "\n".join(content)
                active["endLine"] = line_index
                blocks.append(active)
                active = None
                content = []
            depth = max(0, depth - 1)
        if ";" in strip_line_comments(line) and pending is not None and "{" not in line:
            pending = None
    return blocks


def scan_hot_path(path: str, text: str) -> list[dict[str, Any]]:
    hits: list[dict[str, Any]] = []
    patterns = (
        ("new_keyword", r"\bnew\s+([A-Za-z_][A-Za-z0-9_<>,\[\]\.]*)\s*[\(\{]"),
        ("array_allocation", r"\bnew\s+[A-Za-z_][A-Za-z0-9_<>,\[\]\.]*\s*\["),
        ("string_format", r"\bstring\.Format\s*\("),
        ("to_string", r"\.ToString\s*\("),
        ("string_interpolation", r'\$"'),
        ("string_concat", r'"[^"]*"\s*\+|\+\s*"[^"]*"'),
        ("linq", r"\.(Select|Where|Any|ToList|ToArray|First|FirstOrDefault)\s*\("),
        ("managed_foreach", r"\bforeach\s*\("),
        ("shader_params_array", r"\.Set(?:Ints|Floats)\s*\([^,\n]+,\s*[^,\n]+,\s*[^,\n]+"),
        ("throw_new", r"\bthrow\s+new\b"),
        ("catch_exception", r"\bcatch\s*\(\s*Exception\b"),
    )
    for block in collect_method_blocks(text):
        if not block["hot"]:
            continue
        body_lines = block["body"].splitlines()
        for local_index, line in enumerate(body_lines, start=block["startLine"]):
            stripped = strip_line_comments(line)
            for name, pattern in patterns:
                match = re.search(pattern, stripped)
                if not match:
                    continue
                if name == "new_keyword":
                    raw_type_name = match.group(1)
                    if raw_type_name == "Unity.Mathematics.Random":
                        continue
                    type_name = raw_type_name.split(".", 1)[-1]
                    type_name = re.sub(r"<.*", "", type_name)
                    if type_name.startswith(VALUE_TYPE_NEW_PREFIXES):
                        continue
                    if type_name and type_name[0].islower():
                        continue
                if name == "array_allocation" and "stackalloc" in stripped:
                    continue
                if True:
                    hits.append(
                        {
                            "path": path,
                            "line": local_index,
                            "method": block["name"],
                            "kind": name,
                            "code": line.strip(),
                        }
                    )
    return hits


def validate_layout_fields(size: int, fields: list[dict[str, Any]]) -> list[dict[str, Any]]:
    violations: list[dict[str, Any]] = []
    if size % 8 != 0:
        violations.append({"kind": "size_not_multiple_of_8", "sizeBytes": size})

    sorted_fields = sorted(fields, key=lambda item: (item["offset"], item["line"]))
    for index, field in enumerate(sorted_fields):
        offset = field["offset"]
        type_name = field["type"]
        field_name = field["name"]
        if is_8byte_layout_field(type_name, field_name) and offset % 8 != 0:
            violations.append(
                {
                    "kind": "unaligned_8byte_field",
                    "field": field_name,
                    "offset": offset,
                    "type": type_name,
                }
            )
        if is_8byte_layout_field(type_name, field_name):
            for previous in sorted_fields[:index]:
                if is_smaller_layout_field(previous["type"], previous["name"]):
                    violations.append(
                        {
                            "kind": "pointer_first_order",
                            "field": field_name,
                            "offset": offset,
                            "type": type_name,
                            "previousField": previous["name"],
                            "previousOffset": previous["offset"],
                            "previousType": previous["type"],
                        }
                    )
                    break

    for index, field in enumerate(sorted_fields):
        normalized = normalize_type_name(field["type"])
        field_size = FIELD_TYPE_SIZE.get(normalized)
        if field_size is None:
            continue

        current_end = field["offset"] + field_size
        next_offset = size
        if index + 1 < len(sorted_fields):
            next_offset = sorted_fields[index + 1]["offset"]

        if current_end > size:
            violations.append(
                {
                    "kind": "field_exceeds_struct_size",
                    "field": field["name"],
                    "offset": field["offset"],
                    "type": field["type"],
                    "end": current_end,
                    "sizeBytes": size,
                }
            )
        elif current_end < next_offset:
            violations.append(
                {
                    "kind": "uncovered_alignment_hole",
                    "afterField": field["name"],
                    "holeStart": current_end,
                    "holeEnd": next_offset,
                }
            )

    return violations


def scan_struct_layouts(path: str, text: str) -> list[dict[str, Any]]:
    maps: list[dict[str, Any]] = []
    lines = text.splitlines()
    for index, line in enumerate(lines):
        layout_match = re.search(r"\[StructLayout\(LayoutKind\.Explicit,\s*Size\s*=\s*([0-9]+)\)\]", line)
        if not layout_match:
            continue
        size = int(layout_match.group(1))
        struct_name = None
        fields: dict[str, str] = {}
        parsed_fields: list[dict[str, Any]] = []
        brace_depth = 0
        in_struct = False
        pending_offset: str | None = None
        for inner_index in range(index + 1, min(len(lines), index + 240)):
            inner = lines[inner_index]
            if struct_name is None:
                name_match = re.search(r"\bstruct\s+([A-Za-z_][A-Za-z0-9_]*)\b", inner)
                if name_match:
                    struct_name = name_match.group(1)
            if "{" in strip_line_comments(inner):
                brace_depth += strip_line_comments(inner).count("{")
                in_struct = True
            if in_struct:
                pending_match = re.search(r"\[FieldOffset\(([0-9]+)\)\]", inner)
                if pending_match:
                    pending_offset = pending_match.group(1)
                offset_match = re.search(r"\[FieldOffset\(([0-9]+)\)\]\s*(?:public|private|internal|protected)?\s*([A-Za-z_][A-Za-z0-9_<>,\[\]\.\*]*)\s+([A-Za-z_][A-Za-z0-9_]*)\s*;", inner)
                if offset_match:
                    offset = int(offset_match.group(1))
                    type_name = offset_match.group(2)
                    field_name = offset_match.group(3)
                    fields[field_name] = f"{offset}:{type_name}"
                    parsed_fields.append(
                        {
                            "name": field_name,
                            "type": type_name,
                            "offset": offset,
                            "line": inner_index + 1,
                        }
                    )
                    pending_offset = None
                elif pending_offset is not None:
                    split_match = re.search(
                        r"\b(?:public|private|internal|protected)\s+([A-Za-z_][A-Za-z0-9_<>,\[\]\.\*]*)\s+([A-Za-z_][A-Za-z0-9_]*)\s*;",
                        inner,
                    )
                    if split_match:
                        offset = int(pending_offset)
                        type_name = split_match.group(1)
                        field_name = split_match.group(2)
                        fields[field_name] = f"{offset}:{type_name}"
                        parsed_fields.append(
                            {
                                "name": field_name,
                                "type": type_name,
                                "offset": offset,
                                "line": inner_index + 1,
                            }
                        )
                        pending_offset = None
            if "}" in strip_line_comments(inner):
                brace_depth -= strip_line_comments(inner).count("}")
                if in_struct and brace_depth <= 0:
                    break
        maps.append(
            {
                "path": path,
                "structName": struct_name or "UNKNOWN",
                "sizeBytes": size,
                "sizeMultipleOf8": size % 8 == 0,
                "offsets": fields,
                "fieldRecords": parsed_fields,
                "layoutViolations": validate_layout_fields(size, parsed_fields),
            }
        )
    return maps


def scan_telemetry_evidence(path: str, text: str) -> dict[str, Any] | None:
    has_300_ring = bool(re.search(r"\b\w*(?:Telemetry|BlackBox)FrameCount\s*=\s*300\b", text))
    has_dump_path = "Dump_" in text
    has_native_ring = "VaultGenerationHandle<" in text and (
        "Telemetry" in text or "BlackBox" in text
    )
    has_writer = any(
        marker in text
        for marker in (
            "RecordFloraGrowthTelemetry",
            "RecordScatterCullTelemetry",
            "WriteCelestialBlackBoxTelemetry",
            "TryAcquireTelemetryBuffer",
        )
    )
    if not (has_300_ring and has_dump_path and has_native_ring and has_writer):
        return None

    return {
        "path": path,
        "frameCount300": has_300_ring,
        "dumpPath": has_dump_path,
        "vaultBacked": has_native_ring,
        "writer": has_writer,
    }


def method_body_contains(text: str, method_name: str, needle: str) -> bool:
    signature_pattern = re.compile(
        rf"\b{re.escape(method_name)}(?:\s*<[^>]+>)?\s*\(",
    )
    for method_match in signature_pattern.finditer(text):
        search_end = min(len(text), method_match.end() + 700)
        brace_index = text.find("{", method_match.end(), search_end)
        semicolon_index = text.find(";", method_match.end(), search_end)
        if brace_index < 0 or (semicolon_index >= 0 and semicolon_index < brace_index):
            continue

        depth = 0
        for index in range(brace_index, len(text)):
            char = text[index]
            if char == "{":
                depth += 1
            elif char == "}":
                depth -= 1
                if depth == 0:
                    if needle in text[brace_index:index]:
                        return True
                    break
    return False


def scan_aup_runtime_conversion_proof(root: Path) -> dict[str, Any]:
    path = root / "Assets/_Project/Scripts/World/PersistentWorldRegistry.cs"
    aup_math_path = root / "Assets/_Project/Scripts/World/AUPMath.cs"
    if not path.exists():
        return {
            "path": "Assets/_Project/Scripts/World/PersistentWorldRegistry.cs",
            "aupMathPath": "Assets/_Project/Scripts/World/AUPMath.cs",
            "runtimeOriginResolved": False,
            "resolveCameraRelativeRoute": False,
            "resolveCameraRelativeUsesDelta": False,
            "doubleOriginSubtraction": False,
            "safe": False,
        }

    text = read_text(path)
    aup_math_text = read_text(aup_math_path) if aup_math_path.exists() else ""
    runtime_origin_resolved = (
        method_body_contains(text, "ToRuntimeFloat3", "RuntimeOriginRoute.CurrentRuntimeOriginAup()") or
        method_body_contains(text, "TryToRuntimeFloat3", "RuntimeOriginRoute.CurrentRuntimeOriginAup()")
    )
    legacy_double_origin_subtraction = (
        (
            method_body_contains(text, "ToRuntimeFloat3", "originAup.ToAbsoluteDouble3()") or
            method_body_contains(text, "TryToRuntimeFloat3", "originAup.ToAbsoluteDouble3()")
        ) and
        (
            method_body_contains(text, "ToRuntimeFloat3", "AUPMath.ToRuntimeFloat3") or
            method_body_contains(text, "TryToRuntimeFloat3", "AUPMath.ToRuntimeFloat3")
        )
    )
    resolve_camera_relative_route = (
        method_body_contains(text, "ToRuntimeFloat3", "AUPMath.ResolveCameraRelative") or
        method_body_contains(text, "TryToRuntimeFloat3", "AUPMath.ResolveCameraRelative")
    )
    resolve_camera_relative_uses_delta = (
        method_body_contains(aup_math_text, "ResolveCameraRelative", "AUPDeltaClamped(in target, in camera)") and
        method_body_contains(aup_math_text, "ResolveCameraRelative", "new float3((float)delta.x")
    )
    double_origin_subtraction = legacy_double_origin_subtraction or (
        resolve_camera_relative_route and resolve_camera_relative_uses_delta
    )
    return {
        "path": "Assets/_Project/Scripts/World/PersistentWorldRegistry.cs",
        "aupMathPath": "Assets/_Project/Scripts/World/AUPMath.cs",
        "runtimeOriginResolved": runtime_origin_resolved,
        "resolveCameraRelativeRoute": resolve_camera_relative_route,
        "resolveCameraRelativeUsesDelta": resolve_camera_relative_uses_delta,
        "doubleOriginSubtraction": double_origin_subtraction,
        "safe": runtime_origin_resolved and double_origin_subtraction,
    }


def scan_lock_lifetimes(path: str, text: str) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    records: list[dict[str, Any]] = []
    violations: list[dict[str, Any]] = []
    methods = collect_method_blocks(text)
    lock_helpers: set[str] = set()

    def release_signal_for_helper(helper_name: str) -> str | None:
        if helper_name.startswith("TryAcquire"):
            return "Release" + helper_name[len("TryAcquire"):]
        if helper_name.startswith("TryLock"):
            return "Unlock" + helper_name[len("TryLock"):]
        if helper_name.startswith("TryResolve"):
            return "Release" + helper_name[len("TryResolve"):]
        return None

    def has_release_signal(body: str, helper_name: str | None = None) -> bool:
        helper_release = release_signal_for_helper(helper_name) if helper_name else None
        if helper_release and helper_release in body:
            return True
        return any(
            marker in body
            for marker in (
                "ReleaseWriteLock",
                "TryUnlockBuffer",
                "UnlockLocked",
                "UnlockJobBuffers",
                "ReleaseOrbitOutputVaultLock",
                "ReleaseTelemetryWriteBuffer",
            )
        )

    def has_finally_release_signal(body: str, helper_name: str | None = None) -> bool:
        return "try" in body and "finally" in body and has_release_signal(body, helper_name)

    def has_job_pin_protocol(body: str) -> bool:
        if ".Schedule(" not in body:
            return False
        if "H8Memory.RegisterActiveJob" in body and "_jobLocksHeld = true" in body:
            return "TryFinalizeFrameJobNoWait" in text and "FinishFrameJobCompletion" in text and "UnlockJobBuffers();" in text
        if "_orbitJobScheduled = true" in body:
            return "TryFinalizeCompletedOrbitMathJob" in text and "ReleaseOrbitOutputVaultLock();" in text
        return False

    for block in methods:
        body = block["body"]
        if "TryAcquireWriteLock" not in body and "TryLockBuffer" not in body:
            continue

        helper = (
            block["name"].startswith(("TryAcquire", "TryLock", "TryResolve")) and
            ".Schedule(" not in body and
            ".Dispatch(" not in body
        )
        has_finally_release = has_finally_release_signal(body)
        job_pin_protocol = has_job_pin_protocol(body)
        releases_on_failure = has_release_signal(body)
        status = "acquire_helper" if helper else "finally_released" if has_finally_release else "job_pin_lifetime_tracked" if job_pin_protocol else "unproven"
        record = {
            "path": path,
            "method": block["name"],
            "startLine": block["startLine"],
            "endLine": block["endLine"],
            "status": status,
            "helper": helper,
            "hasFinallyRelease": has_finally_release,
            "hasJobPinProtocol": job_pin_protocol,
            "hasFailureRelease": releases_on_failure,
        }
        records.append(record)
        if helper:
            lock_helpers.add(block["name"])
            continue
        if not has_finally_release and not job_pin_protocol:
            violations.append({**record, "kind": "lock_acquisition_without_finally_release"})

    for helper_name in sorted(lock_helpers):
        helper_call = re.compile(rf"\b{re.escape(helper_name)}\s*\(")
        for block in methods:
            if block["name"] == helper_name:
                continue
            body = block["body"]
            if not helper_call.search(body):
                continue

            has_finally_release = has_finally_release_signal(body, helper_name)
            job_pin_protocol = has_job_pin_protocol(body)
            status = "helper_call_finally_released" if has_finally_release else "helper_call_job_pin_lifetime_tracked" if job_pin_protocol else "helper_call_unproven"
            record = {
                "path": path,
                "method": block["name"],
                "startLine": block["startLine"],
                "endLine": block["endLine"],
                "helper": helper_name,
                "status": status,
                "hasFinallyRelease": has_finally_release,
                "hasJobPinProtocol": job_pin_protocol,
            }
            records.append(record)
            if not has_finally_release and not job_pin_protocol:
                violations.append({**record, "kind": "lock_helper_call_without_finally_release"})

    return records, violations


def scan_compaction_lock_proof(root: Path) -> dict[str, Any]:
    vault_path = root / "Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs"
    if not vault_path.exists():
        return {
            "path": vault_path.as_posix(),
            "tryAcquireWriteLockFence": False,
            "tryLockBufferFence": False,
        }

    text = read_text(vault_path)
    return {
        "path": "Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs",
        "tryAcquireWriteLockFence": method_body_contains(text, "TryAcquireWriteLock", "Volatile.Read(ref _compactionFence)"),
        "tryLockBufferFence": method_body_contains(text, "TryLockBuffer", "Volatile.Read(ref _compactionFence)"),
    }


def scan_buffer_id_conflicts(root: Path) -> dict[str, Any]:
    owners_by_value: dict[int, list[dict[str, Any]]] = {}
    agent_owned: list[dict[str, Any]] = []
    h8_memory_path = root / "Assets/_Project/Scripts/Core/Memory/H8Memory.cs"
    if h8_memory_path.exists():
        text = read_text(h8_memory_path)
        for line_index, line in enumerate(text.splitlines(), start=1):
            match = re.match(r"\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*([0-9]+)\s*,", line)
            if not match:
                continue
            value = int(match.group(2))
            owners_by_value.setdefault(value, []).append(
                {
                    "path": "Assets/_Project/Scripts/Core/Memory/H8Memory.cs",
                    "line": line_index,
                    "name": match.group(1),
                    "kind": "BufferIDEnum",
                }
            )

    scripts_root = root / "Assets/_Project/Scripts"
    const_pattern = re.compile(r"\bconst\s+BufferID\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*\(BufferID\)\s*([0-9]+)\s*;")
    if scripts_root.exists():
        for path in scripts_root.rglob("*.cs"):
            relative = path.relative_to(root).as_posix()
            text = read_text(path)
            for line_index, line in enumerate(text.splitlines(), start=1):
                match = const_pattern.search(line)
                if not match:
                    continue
                record = {
                    "path": relative,
                    "line": line_index,
                    "name": match.group(1),
                    "kind": "BufferIDConst",
                }
                value = int(match.group(2))
                owners_by_value.setdefault(value, []).append(record)
                if match.group(1) in AGENT_OWNED_BUFFER_ID_CONSTS:
                    agent_owned.append({"value": value, **record})

    conflicts: list[dict[str, Any]] = []
    for owned in agent_owned:
        owners = [
            owner
            for owner in owners_by_value.get(owned["value"], [])
            if not (
                owner["path"] == owned["path"] and
                owner["line"] == owned["line"] and
                owner["name"] == owned["name"]
            )
        ]
        if owners:
            conflicts.append({"agentOwned": owned, "conflicts": owners})

    return {
        "agentOwnedBufferIds": sorted(agent_owned, key=lambda item: item["value"]),
        "conflicts": conflicts,
    }


def scan_file(root: Path, relative: str) -> dict[str, Any]:
    path = root / relative
    text = read_text(path)
    native_declarations, persistent_fields, job_views = classify_native_collections(relative, text)
    lock_lifetime_records, lock_lifetime_violations = scan_lock_lifetimes(relative, text)
    vault_views = [
        item
        for item in native_declarations
        if "TryResolve" in item["declaration"] or "TryAcquire" in item["declaration"] or item["scope"] == "method_or_local"
    ]
    locks = [
        {"line": index + 1, "code": line.strip()}
        for index, line in enumerate(text.splitlines())
        if "TryAcquireWriteLock" in line or "TryLockBuffer" in line
    ]
    return {
        "path": relative,
        "sha256": sha256_file(path),
        "nativeDeclarations": native_declarations,
        "persistentNativeFields": persistent_fields,
        "transientJobViews": job_views,
        "vaultOwnerNativeFields": [
            declaration
            for declaration in native_declarations
            if declaration.get("scope") == "vault_owner_internal_field"
        ],
        "transientVaultViews": vault_views,
        "hotPathHits": scan_hot_path(relative, text),
        "absoluteAupCasts": [
            {"line": index + 1, "code": line.strip()}
            for index, line in enumerate(text.splitlines())
            if re.search(r"\(\s*float[234]\s*\)[^;\n]*(?:Aup|AUP|ToAbsoluteDouble3)|\bnew\s+float[234]\s*\([^;\n]*(?:Aup|AUP|ToAbsoluteDouble3)", line)
        ],
        "locks": locks,
        "lockLifetimeRecords": lock_lifetime_records,
        "lockLifetimeViolations": lock_lifetime_violations,
        "tryFinallyCount": len(re.findall(r"\btry\b[\s\S]{0,500}?\bfinally\b", text)),
        "throwNew": [
            {"path": relative, "line": index + 1, "code": line.strip()}
            for index, line in enumerate(text.splitlines())
            if re.search(r"\bthrow\s+new\b", strip_line_comments(line))
        ],
        "catchException": [
            {"path": relative, "line": index + 1, "code": line.strip()}
            for index, line in enumerate(text.splitlines())
            if re.search(r"\bcatch\s*\(\s*Exception\b", strip_line_comments(line))
        ],
        "byteOffsetMaps": scan_struct_layouts(relative, text),
        "telemetryEvidence": scan_telemetry_evidence(relative, text),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".")
    parser.add_argument("--json", default="Docs/Reports/COMPUTE_PURGE_AUDIT_1333.json")
    args = parser.parse_args()
    root = Path(args.root).resolve()
    files = [scan_file(root, relative) for relative in TOUCHED_CSHARP_FILES]
    all_bytes = hashlib.sha256()
    for file_report in files:
        all_bytes.update(file_report["sha256"].encode("ascii"))
    persistent = [field for file in files for field in file["persistentNativeFields"]]
    native_field_declarations = [
        declaration
        for file in files
        for declaration in file["nativeDeclarations"]
        if declaration.get("isField")
    ]
    vault_owner_native_fields = [field for file in files for field in file["vaultOwnerNativeFields"]]
    hot_hits = [hit for file in files for hit in file["hotPathHits"]]
    hot_throw_or_catch = [hit for hit in hot_hits if hit["kind"] in ("throw_new", "catch_exception")]
    aup_casts = [hit for file in files for hit in file["absoluteAupCasts"]]
    locks = [lock for file in files for lock in file["locks"]]
    throw_new = [hit for file in files for hit in file["throwNew"]]
    catch_exception = [hit for file in files for hit in file["catchException"]]
    byte_maps = [item for file in files for item in file["byteOffsetMaps"]]
    layout_violations = [
        {
            "path": item["path"],
            "structName": item["structName"],
            "sizeBytes": item["sizeBytes"],
            "violation": violation,
        }
        for item in byte_maps
        for violation in item["layoutViolations"]
    ]
    telemetry_evidence = [file["telemetryEvidence"] for file in files if file["telemetryEvidence"]]
    compaction_proof = scan_compaction_lock_proof(root)
    aup_runtime_conversion_proof = scan_aup_runtime_conversion_proof(root)
    buffer_id_proof = scan_buffer_id_conflicts(root)
    lock_lifetime_records = [record for file in files for record in file["lockLifetimeRecords"]]
    lock_lifetime_violations = [record for file in files for record in file["lockLifetimeViolations"]]
    compaction_callers_proven = bool(locks) and not lock_lifetime_violations
    compaction_locks_proven = (
        compaction_proof["tryAcquireWriteLockFence"] and
        compaction_proof["tryLockBufferFence"] and
        compaction_callers_proven
    )
    telemetry_integrated = len(telemetry_evidence) >= 2
    report = {
        "agentId": "1333",
        "task": "1333_PURGE",
        "status": "AUDIT_RED",
        "scannedFiles": len(files),
        "failedGates": [],
        "totalNativeFieldDeclarations": len(native_field_declarations),
        "persistentNativeFieldsRemaining": len(persistent),
        "transientVaultViews": sum(len(file["transientVaultViews"]) for file in files),
        "transientJobViews": sum(len(file["transientJobViews"]) for file in files),
        "byteOffsetMaps": byte_maps,
        "zeroGcHotPathHits": len(hot_hits),
        "absoluteAupCastsFound": len(aup_casts),
        "compactionAwareLocksProven": compaction_locks_proven,
        "telemetryRingIntegrated": telemetry_integrated,
        "verificationHashSha256": all_bytes.hexdigest(),
        "files": files,
        "nativeFieldDeclarations": native_field_declarations,
        "persistentNativeFields": persistent,
        "hotPathHits": hot_hits,
        "absoluteAupCasts": aup_casts,
        "locks": locks,
        "throwNew": throw_new,
        "catchException": catch_exception,
        "coldThrowOrCatch": throw_new + catch_exception,
        "hotThrowOrCatch": hot_throw_or_catch,
        "layoutViolations": layout_violations,
        "vaultOwnerNativeFields": vault_owner_native_fields,
        "telemetryEvidence": telemetry_evidence,
        "compactionProof": compaction_proof,
        "lockLifetimeRecords": lock_lifetime_records,
        "lockLifetimeViolations": lock_lifetime_violations,
        "aupRuntimeConversionProof": aup_runtime_conversion_proof,
        "bufferIdProof": buffer_id_proof,
    }
    if persistent:
        report["failedGates"].append("native_persistent_fields")
    if hot_hits:
        report["failedGates"].append("zero_gc_hot_path")
    if aup_casts:
        report["failedGates"].append("aup_casts")
    if layout_violations:
        report["failedGates"].append("arm64_layout")
    if not aup_runtime_conversion_proof["safe"]:
        report["failedGates"].append("aup_runtime_conversion_proof")
    if hot_throw_or_catch:
        report["failedGates"].append("managed_throw_or_catch")
    if not report["compactionAwareLocksProven"]:
        report["failedGates"].append("compaction_lock_proof")
    if not report["telemetryRingIntegrated"]:
        report["failedGates"].append("telemetry_ring")
    if report["bufferIdProof"]["conflicts"]:
        report["failedGates"].append("buffer_id_conflict")
    if not report["failedGates"]:
        report["status"] = "VERIFIED_GREEN"

    output = root / args.json
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2, sort_keys=True), encoding="utf-8")
    print(f"scannedFiles={report['scannedFiles']}")
    print(f"totalNativeFieldDeclarations={report['totalNativeFieldDeclarations']}")
    print(f"persistentNativeFieldsRemaining={report['persistentNativeFieldsRemaining']}")
    print(f"zeroGcHotPathHits={report['zeroGcHotPathHits']}")
    print(f"absoluteAupCastsFound={report['absoluteAupCastsFound']}")
    print(f"throwNew={len(throw_new)}")
    print(f"catchException={len(catch_exception)}")
    print(f"layoutViolations={len(layout_violations)}")
    print(f"lockLifetimeViolations={len(lock_lifetime_violations)}")
    print(f"vaultOwnerNativeFields={len(vault_owner_native_fields)}")
    print(f"bufferIdConflicts={len(report['bufferIdProof']['conflicts'])}")
    print(f"failedGates={','.join(report['failedGates'])}")
    print(f"json={output.relative_to(root).as_posix()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
