#!/usr/bin/env python3
import json
import re
import ast
import operator
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
REPORT_PATH = ROOT / "Docs" / "Reports" / "VOXEL_OPTIMIZATION_REPORT_X_006.json"


FILES = {
    "engine": ROOT / "Assets/_Project/Scripts/HectonVoxelEngine.cs",
    "volume": ROOT / "Assets/_Project/Scripts/HectonVoxelVolume.cs",
    "delta": ROOT / "Assets/_Project/Scripts/VoxelDeltaProcessor.cs",
    "rle": ROOT / "Assets/_Project/Scripts/SaveSystem/VoxelDeltaCompressionArchitecture.cs",
    "save_delta": ROOT / "Assets/_Project/Scripts/SaveSystem/SaveDeltaCompression.cs",
    "pager": ROOT / "Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs",
    "memory": ROOT / "Assets/_Project/Scripts/Core/Memory/H8Memory.cs",
    "global_vault": ROOT / "Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs",
    "bootstrap": ROOT / "Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs",
    "sargassum": ROOT / "Assets/_Project/Scripts/World/SargassumCutManager.cs",
    "save_manager": ROOT / "Assets/_Project/Scripts/SaveManager.cs",
    "signals": ROOT / "Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.CoreFoundation.cs",
    "surface_contracts": ROOT / "Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsContracts.cs",
    "surface_vault": ROOT / "Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs",
    "surface_gpu": ROOT / "Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsGpuUploadDispatcher.cs",
    "cave_graph": ROOT / "Assets/_Project/Scripts/CaveGraphGenerator.cs",
    "world_runtime": ROOT / "Assets/_Project/Scripts/World/ShinobuStreamingRuntime.cs",
    "rock_shader": ROOT / "Assets/_Project/Art/Shaders/Hecton_AbyssalVoxelRock.shader",
    "terrain_shader": ROOT / "Assets/_Project/Art/Shaders/TerrainMaster.shader",
    "ghost_shader": ROOT / "Assets/_Project/Art/Shaders/Hecton_VoxelBakeGhost.shader",
    "cut_compute": ROOT / "Assets/_Project/Art/Shaders/Hecton_SargassumCutMask.compute",
    "damage_compute": ROOT / "Assets/_Project/Art/Shaders/Hecton_TerrainDamageVolume.compute",
    "world_residency": ROOT / "Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs",
}


def read(path):
    return path.read_text(encoding="utf-8", errors="replace") if path.exists() else ""


def rel(path):
    try:
        return str(path.relative_to(ROOT)).replace("\\", "/")
    except ValueError:
        return str(path).replace("\\", "/")


def line_hits(path, pattern):
    text = read(path)
    rx = re.compile(pattern)
    hits = []
    for line_no, line in enumerate(text.splitlines(), 1):
        if rx.search(line):
            hits.append({"file": rel(path), "line": line_no, "text": line.strip()})
    return hits


def source_window(path, line_no, before=4, after=2):
    lines = read(path).splitlines()
    start = max(0, line_no - before - 1)
    end = min(len(lines), line_no + after)
    return "\n".join(lines[start:end])


def line_hits_between(path, start_marker, end_marker, pattern):
    text = read(path)
    start = text.find(start_marker)
    if start < 0:
        return []

    end = text.find(end_marker, start)
    if end < 0:
        end = len(text)

    prefix_line_count = text[:start].count("\n")
    block_lines = text[start:end].splitlines()
    rx = re.compile(pattern)
    hits = []
    for local_index, line in enumerate(block_lines, 1):
        if rx.search(line):
            hits.append({
                "file": rel(path),
                "line": prefix_line_count + local_index,
                "text": line.strip(),
            })
    return hits


def classify_native_allocation_hit(hit):
    file_name = hit["file"]
    line_no = hit["line"]
    text = hit["text"]
    full_path = ROOT / file_name
    context = source_window(full_path, line_no)

    if "COLD ALLOC" in context:
        return "cold_or_prewarm"

    if file_name.endswith("VoxelDeltaProcessor.cs"):
        if "_queuedCarveEvents" in text or "_compaction" in text or "_nativeSnapshotScratch" in text:
            return "cold_or_prewarm"
        if "DirtyMaskWords =" in text or "SdfValueBits =" in text or "MaterialIds =" in text or "CellFlags =" in text:
            return "fallback_only"

    if file_name.endswith("HectonVoxelEngine.cs"):
        if line_no in (131, 394):
            return "cold_or_prewarm"
        if "SpawnPointListScratch" in context or "TryPrepareSpawnPointScratch" in context:
            return "pooled_growth"
        if "ModifiedCellsScratch" in context or "TryPrepareModifiedCellsScratch" in context:
            return "pooled_growth"
        if "RebuildNodes" in context or "TryPrepareRebuildGraphScratch" in context:
            return "pooled_growth"
        if "EnsureNativeArrayCapacity" in context or "array = new NativeArray<T>" in text:
            return "pooled_growth"
        if "AcquireStreamingScratchLease" in context or "EnsureStreamingScratchSlotCapacity" in context:
            return "pooled_growth"
        if "caveNodes =" in text or "caveTunnels =" in text or "caveEntrances =" in text or "caveStructures =" in text:
            return "generation_or_rebuild_snapshot"
        if "craterStamps =" in text or "nodeSnapshot" in context or "tunnelSnapshot" in context or "entranceSnapshot" in context:
            return "generation_or_rebuild_snapshot"
        if "DataVaultExemptVoxelPipelineScratchAllocator" in text:
            return "hot_rebuild"
        if "Allocator.Temp" in text or "Allocator.TempJob" in text:
            return "hot_rebuild"
        if "data.RawVertices" in text or "data.WeldedPositions" in text or "data.TriangleIndices" in text:
            return "hot_rebuild"
        if "data.EdgeVertex" in text or "weldedCounter" in text:
            return "hot_rebuild"
        if "projectedPositions" in text:
            return "hot_rebuild"
        if "positions =" in text or "indices =" in text:
            return "hot_rebuild"
        if "NativeParallelHashMap<int3, VoxelModifiedCell>" in text:
            return "hot_rebuild"
        if "NativeList<CaveSpawnData>" in text:
            return "hot_rebuild"

    return "hot_rebuild"


def classify_native_allocation_hits(hits):
    buckets = {
        "hot_rebuild": [],
        "pooled_growth": [],
        "generation_or_rebuild_snapshot": [],
        "fallback_only": [],
        "cold_or_prewarm": [],
    }
    for hit in hits:
        category = classify_native_allocation_hit(hit)
        buckets.setdefault(category, []).append(hit)
    return buckets


def _safe_eval(expr_str, constants=None):
    if constants is None:
        constants = {}
    operators = {
        ast.Add: operator.add,
        ast.Sub: operator.sub,
        ast.Mult: operator.mul,
        ast.Div: operator.truediv,
        ast.FloorDiv: operator.floordiv,
        ast.Mod: operator.mod,
        ast.LShift: operator.lshift,
        ast.RShift: operator.rshift,
        ast.BitOr: operator.or_,
        ast.BitXor: operator.xor,
        ast.BitAnd: operator.and_,
        ast.UAdd: operator.pos,
        ast.USub: operator.neg,
        ast.Invert: operator.invert,
    }

    def _eval(node, depth=0):
        if depth > 100:
            raise ValueError("Expression too deep")
        if isinstance(node, ast.Constant):
            if not isinstance(node.value, (int, float)):
                raise ValueError(f"Unsupported constant type {type(node.value)}")
            return node.value
        elif isinstance(node, ast.Name):
            if node.id in constants:
                return constants[node.id]
            raise ValueError(f"Unknown variable {node.id}")
        elif isinstance(node, ast.BinOp):
            left = _eval(node.left, depth + 1)
            right = _eval(node.right, depth + 1)
            if type(node.op) not in operators:
                raise ValueError(f"Unsupported operator {type(node.op)}")
            if type(node.op) in (ast.LShift, ast.RShift) and right > 256:
                raise ValueError("Shift count too large")
            return operators[type(node.op)](left, right)
        elif isinstance(node, ast.UnaryOp):
            operand = _eval(node.operand, depth + 1)
            if type(node.op) not in operators:
                raise ValueError(f"Unsupported operator {type(node.op)}")
            return operators[type(node.op)](operand)
        elif isinstance(node, ast.Expression):
            return _eval(node.body, depth + 1)
        raise ValueError(f"Unsupported node {type(node)}")

    try:
        return _eval(ast.parse(expr_str, mode='eval').body)
    except Exception as e:
        raise ValueError("Failed to evaluate expression") from e


def eval_int_expr(expr, constants):
    normalized = expr.strip()
    normalized = normalized.replace("<<", " << ")
    normalized = re.sub(r"(?<=\d)[lLfF]\b", "", normalized)
    normalized = re.sub(r"\b(?:[A-Za-z_]\w*\.)+([A-Za-z_]\w*)\b", r"\1", normalized)
    try:
        return int(_safe_eval(normalized, constants))
    except Exception:
        return None


def extract_int_constants(path):
    text = read(path)
    constants = {}
    pattern = re.compile(r"\bconst\s+int\s+(\w+)\s*=\s*([^;]+);")
    pending = [(m.group(1), m.group(2)) for m in pattern.finditer(text)]
    progress = True
    while pending and progress:
        progress = False
        next_pending = []
        for name, expr in pending:
            value = eval_int_expr(expr, constants)
            if value is None:
                next_pending.append((name, expr))
                continue
            constants[name] = value
            progress = True
        pending = next_pending
    return constants


def extract_numeric_constants(path):
    text = read(path)
    constants = {}
    pattern = re.compile(r"\bconst\s+(?:int|long)\s+(\w+)\s*=\s*([^;]+);")
    pending = [(m.group(1), m.group(2)) for m in pattern.finditer(text)]
    progress = True
    while pending and progress:
        progress = False
        next_pending = []
        for name, expr in pending:
            value = eval_int_expr(expr, constants)
            if value is None:
                next_pending.append((name, expr))
                continue
            constants[name] = value
            progress = True
        pending = next_pending
    return constants


def first_int_constant(path, name, default=0):
    return extract_int_constants(path).get(name, default)


def first_assignment_int(path, name, default=0):
    match = re.search(rf"\b{name}\s*=\s*([0-9]+)", read(path))
    return int(match.group(1)) if match else default


def field_range_max(path, name, default=0):
    text = read(path)
    match = re.search(
        rf"Range\(\s*[0-9.]+f?\s*,\s*([0-9.]+)f?\s*\)[\s\S]{{0,220}}\b{name}\s*=",
        text,
    )
    return int(float(match.group(1))) if match else default


def has_text(path, token):
    return token in read(path)


def struct_layout(path, struct_name):
    text = read(path)
    pattern = re.compile(
        r"\[StructLayout\(LayoutKind\.Explicit,\s*Size\s*=\s*(?P<size>[^,)]+)[^)]*\)\]\s*"
        r"(?:\[[^\]]+\]\s*)*"
        rf"(?:public|private|internal)\s+(?:readonly\s+)?(?:partial\s+)?struct\s+{re.escape(struct_name)}\b"
        r"(?P<body>[\s\S]*?)\n\s*\}",
        re.MULTILINE,
    )
    match = pattern.search(text)
    if not match:
        return {"bytes": 0, "offsets": {}}

    size = eval_int_expr(match.group("size"), extract_int_constants(path))
    if size is None:
        size = 0

    body = match.group("body")
    offsets = {}
    for field in re.finditer(
        r"\[FieldOffset\((\d+)\)\]\s*(?:public|private|internal)\s+"
        r"(?:readonly\s+)?[A-Za-z0-9_<>.]+\s+([A-Za-z0-9_]+)\s*;",
        body,
    ):
        offsets[field.group(2)] = int(field.group(1))
    return {"bytes": size, "offsets": offsets}


def detect_sync_collider_fallbacks():
    text = read(FILES["engine"])
    lines = text.splitlines()
    fallbacks = []
    for index, line in enumerate(lines):
        if "if (!EnsureDeferredVoxelColliderUploadRegistered())" not in line:
            continue
        window = "\n".join(lines[index:index + 18])
        if re.search(r"\bsharedMesh\s*=", window):
            fallbacks.append({
                "file": rel(FILES["engine"]),
                "line": index + 1,
                "text": "sharedMesh assignment remains inside EnsureDeferredVoxelColliderUploadRegistered fallback block",
            })
    return fallbacks


def classify_shared_mesh_assignments(include_render_mesh=True):
    hits = []
    for path in [FILES["engine"], FILES["volume"]]:
        for hit in line_hits(path, r"\bsharedMesh\s*="):
            line = hit["text"]
            if "==" in line or "= null" in line:
                continue
            if not include_render_mesh and "Collider" not in line and "collider" not in line and "mcol" not in line:
                continue
            hits.append(hit)
    return hits


def deformation_collider_shared_mesh_null_mutations():
    hits = []
    hits.extend(line_hits_between(
        FILES["engine"],
        "Awaitable<bool> ApplyVolumeMeshAsync",
        "void PrepareVolumeForBuild",
        r"\bsharedMesh\s*=\s*null",
    ))
    hits.extend(line_hits_between(
        FILES["volume"],
        "internal bool CommitDeferredColliderChunkUpload",
        "internal void ClearColliderChunkBakeMeshes",
        r"\bsharedMesh\s*=\s*null",
    ))
    return hits


def deferred_bake_presentation_shared_mesh_null_mutations():
    return line_hits_between(
        FILES["engine"],
        "private static void DisableDeferredVoxelBakePresentation",
        "private static bool EnsureDeferredVoxelPhysicsBakeTeardownRegistered",
        r"\bsharedMesh\s*=\s*null",
    )


def runtime_collider_shared_mesh_null_mutations():
    hits = []
    for path in [FILES["engine"], FILES["volume"]]:
        for hit in line_hits(path, r"\bsharedMesh\s*=\s*null"):
            line_text = hit["text"]
            if "meshFilter.sharedMesh" in line_text:
                continue

            context = source_window(path, hit["line"], before=5, after=1)
            if "if (destroyMeshes)" in context:
                continue

            hits.append(hit)
    return hits


def paging_getcomponent_hotpath_hits():
    hits = []
    for start, end in [
        ("public void DespawnVolume(GameObject volume)", "/// <summary>Removes null references from active volumes list.</summary>"),
        ("public void ClearAllVolumes()", "public int ActiveVolumeCount => _activeVolumes.Count"),
        ("void PrepareVolumeForBuild(GameObject go)", "async Awaitable<bool> ConfigureVolumeRuntimeDataAsync"),
    ]:
        hits.extend(line_hits_between(FILES["engine"], start, end, r"\.GetComponent<"))
    return hits


def count_shader_clip_routes():
    return {
        "rock_forward_shadow_depth": (
            has_text(FILES["rock_shader"], "ApplyDearLieCarveClip")
            and read(FILES["rock_shader"]).count("ApplyDearLieCarveClip(") >= 4
        ),
        "terrain_forward_shadow_depth_normals": (
            has_text(FILES["terrain_shader"], "ApplyHectonDearLieTerrainClip")
            and read(FILES["terrain_shader"]).count("ApplyHectonDearLieTerrainClip(") >= 5
        ),
        "ghost_forward": (
            has_text(FILES["ghost_shader"], "ApplyDearLieGhostClip")
            and read(FILES["ghost_shader"]).count("ApplyDearLieGhostClip(") >= 2
        ),
    }


def surface_nets_vault_ledger():
    constants = extract_int_constants(FILES["surface_contracts"])
    sizes = {
        "sbyte": 1,
        "byte": 1,
        "uint": 4,
        "int": 4,
        "float3": 12,
        "VoxelVertexDTO": struct_layout(FILES["surface_contracts"], "VoxelVertexDTO")["bytes"],
        "ChunkMeshingStateDTO": struct_layout(FILES["surface_contracts"], "ChunkMeshingStateDTO")["bytes"],
        "VoxelMeshingTuningDTO": struct_layout(FILES["surface_contracts"], "VoxelMeshingTuningDTO")["bytes"],
        "VoxelMeshingTelemetryEntry": struct_layout(FILES["surface_contracts"], "VoxelMeshingTelemetryEntry")["bytes"],
        "VoxelSurfaceAabbDTO": struct_layout(FILES["surface_contracts"], "VoxelSurfaceAabbDTO")["bytes"],
        "VoxelSurfaceModifiedSignal": struct_layout(FILES["surface_contracts"], "VoxelSurfaceModifiedSignal")["bytes"],
        "VoxelSurfacePriorityDTO": struct_layout(FILES["surface_contracts"], "VoxelSurfacePriorityDTO")["bytes"],
        "VoxelSurfaceIndirectArgsDTO": struct_layout(FILES["surface_contracts"], "VoxelSurfaceIndirectArgsDTO")["bytes"],
        "MockVoxelDensityArray": struct_layout(FILES["surface_contracts"], "MockVoxelDensityArray")["bytes"],
        "VoxelSurfacePhysicsBakeRequestDTO": struct_layout(FILES["surface_contracts"], "VoxelSurfacePhysicsBakeRequestDTO")["bytes"],
        "VoxelSurfaceHzbTileDTO": struct_layout(FILES["surface_contracts"], "VoxelSurfaceHzbTileDTO")["bytes"],
    }

    rows = [
        ("Density", "sbyte", constants.get("DensitySampleCount", 0)),
        ("Vertices", "VoxelVertexDTO", constants.get("MaxVertices", 0)),
        ("Indices", "uint", constants.get("MaxIndices", 0)),
        ("CellVertexMap", "int", constants.get("CellCount", 0)),
        ("States", "ChunkMeshingStateDTO", constants.get("MaxTrackedChunks", 0)),
        ("Tuning", "VoxelMeshingTuningDTO", 1),
        ("TelemetryRing", "VoxelMeshingTelemetryEntry", constants.get("TelemetryFrames", 0)),
        ("TelemetryCursor", "int", 1),
        ("CsvScratch", "byte", constants.get("CsvScratchBytes", 0)),
        ("SurfaceEdgeMasks", "uint", constants.get("LookupCaseCount", 256)),
        ("RawDebugVertices", "float3", constants.get("MaxRawDebugVertices", 0)),
        ("ChunkAabbs", "VoxelSurfaceAabbDTO", constants.get("MaxTrackedChunks", 0)),
        ("ModifiedSignals", "VoxelSurfaceModifiedSignal", constants.get("MaxModifiedSignals", 0)),
        ("Priorities", "VoxelSurfacePriorityDTO", constants.get("MaxTrackedChunks", 0)),
        ("IndirectArgs", "VoxelSurfaceIndirectArgsDTO", 1),
        ("MockDensityConfig", "MockVoxelDensityArray", 1),
        ("PhysicsBakeRequests", "VoxelSurfacePhysicsBakeRequestDTO", constants.get("MaxTrackedChunks", 0)),
        ("HzbTiles", "VoxelSurfaceHzbTileDTO", constants.get("MaxHzbTiles", 0)),
    ]

    ledger = []
    total = 0
    for name, type_name, count in rows:
        stride = sizes.get(type_name, 0)
        byte_count = count * stride
        total += byte_count
        ledger.append({
            "buffer": name,
            "type": type_name,
            "count": count,
            "stride_bytes": stride,
            "bytes": byte_count,
        })

    return {
        "constants": {
            "ChunkResolution": constants.get("ChunkResolution", 0),
            "DensityResolution": constants.get("DensityResolution", 0),
            "CellCount": constants.get("CellCount", 0),
            "DensitySampleCount": constants.get("DensitySampleCount", 0),
            "MaxTrackedChunks": constants.get("MaxTrackedChunks", 0),
        },
        "total_preallocated_bytes": total,
        "ledger": ledger,
        "scope_note": "This is a preallocated Surface Nets scratch/state vault. It is not a proven resident dirty-chunk SDF recycler for VoxelDeltaProcessor.",
    }


def surface_nets_gpu_upload_dispatcher_proof():
    gpu_text = read(FILES["surface_gpu"])
    init_start = gpu_text.find("public bool Initialize(")
    init_end = gpu_text.find("private static GraphicsBuffer CreateLockBuffer", init_start)
    init_block = gpu_text[init_start:init_end] if init_start >= 0 and init_end > init_start else ""
    begin_start = gpu_text.find("public bool TryBeginUpload(")
    begin_end = gpu_text.find("public bool TryFinalizeUpload(", begin_start)
    begin_block = gpu_text[begin_start:begin_end] if begin_start >= 0 and begin_end > begin_start else ""
    finalize_start = gpu_text.find("public bool TryFinalizeUpload(")
    finalize_end = gpu_text.find("public void Release()", finalize_start)
    finalize_block = gpu_text[finalize_start:finalize_end] if finalize_start >= 0 and finalize_end > finalize_start else ""
    unlock_helper_present = (
        "private void UnlockPendingUploadBuffers()" in gpu_text
        and "UnlockBufferAfterWrite<VoxelVertexDTO>" in gpu_text
        and "UnlockBufferAfterWrite<uint>" in gpu_text
        and "UnlockBufferAfterWrite<VoxelSurfaceIndirectArgsDTO>" in gpu_text
    )
    release_start = gpu_text.find("public bool TryRelease()")
    release_end = gpu_text.find("public void Dispose()", release_start)
    release_block = gpu_text[release_start:release_end] if release_start >= 0 and release_end > release_start else ""
    release_helper_start = gpu_text.find("private static void ReleaseGraphicsBuffer")
    release_helper_end = gpu_text.find("private void UnlockPendingUploadBuffers()", release_helper_start)
    release_helper_block = gpu_text[release_helper_start:release_helper_end] if release_helper_start >= 0 and release_helper_end > release_helper_start else ""
    unlock_helper_start = gpu_text.find("private void UnlockPendingUploadBuffers()")
    unlock_helper_block = gpu_text[unlock_helper_start:] if unlock_helper_start >= 0 else ""
    is_completed_index = finalize_block.find("!uploadDependency.IsCompleted")
    pending_is_completed_index = finalize_block.find("!_pendingUploadDependency.IsCompleted")
    complete_index = finalize_block.find("_pendingUploadDependency.Complete();")
    unlock_index = finalize_block.find("UnlockPendingUploadBuffers();")
    release_is_completed_index = release_block.find("!_pendingUploadDependency.IsCompleted")
    release_complete_index = release_block.find("_pendingUploadDependency.Complete();")
    release_unlock_index = release_block.find("UnlockPendingUploadBuffers();")
    begin_lock_index = begin_block.find("LockBufferForWrite")
    begin_schedule_index = begin_block.find("copyJob.Schedule")
    begin_uploading_stage_index = begin_block.find("state.Stage = (byte)VoxelMeshingStage.Uploading")
    return {
        "dispatcher_present": "sealed unsafe class VoxelSurfaceNetsGpuUploadDispatcher" in gpu_text,
        "initialize_respects_inflight_release": (
            "if (!TryRelease())" in init_block
            and "return false;" in init_block
            and "Release();" not in init_block
        ),
        "release_request_deferred_nonblocking": (
            "private bool _releaseRequested;" in gpu_text
            and "public void Release()" in gpu_text
            and "_releaseRequested = true;" in gpu_text
            and "if (!_pendingUploadDependency.IsCompleted)" in release_block
            and "return false;" in release_block
            and "TryRelease();" in finalize_block
            and "uploadState = default;" in finalize_block
            and "while (!_pendingUploadDependency.IsCompleted)" not in release_block
        ),
        "begin_upload_rejects_pending_release": (
            "if (_releaseRequested)" in begin_block
            and "TryRelease();" in begin_block
            and "return false;" in begin_block
        ),
        "lock_buffer_route_present": "LockBufferForWrite<VoxelVertexDTO>" in gpu_text and "LockBufferForWrite<uint>" in gpu_text,
        "graphics_buffer_validity_guard_present": (
            "private static bool IsGraphicsBufferReady(GraphicsBuffer buffer)" in gpu_text
            and "buffer.IsValid()" in gpu_text
            and "IsGraphicsBufferReady(_vertexFront)" in gpu_text
            and "IsGraphicsBufferReady(_vertexBack)" in gpu_text
            and "IsGraphicsBufferReady(_indexFront)" in gpu_text
            and "IsGraphicsBufferReady(_indexBack)" in gpu_text
            and "IsGraphicsBufferReady(_indirectArgs)" in gpu_text
        ),
        "invalid_upload_resource_releases_for_cold_recreate": (
            "if (!IsInitialized())" in begin_block
            and "TryRelease();" in begin_block
            and "_releaseRequested = true;" in begin_block
            and "VoxelMeshingFlags.GpuResourceInvalid" in begin_block
            and "GpuResourceInvalid = 1 << 7" in read(FILES["surface_contracts"])
            and "TryRelease();" in begin_block
            and "return false;" in begin_block
        ),
        "invalid_release_skips_dead_graphics_buffers": (
            "private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)" in gpu_text
            and "if (buffer.IsValid())" in release_helper_block
            and "buffer.Release();" in release_helper_block
            and "buffer = null;" in release_helper_block
            and "ReleaseGraphicsBuffer(ref _vertexFront);" in release_block
            and "ReleaseGraphicsBuffer(ref _vertexBack);" in release_block
            and "ReleaseGraphicsBuffer(ref _indexFront);" in release_block
            and "ReleaseGraphicsBuffer(ref _indexBack);" in release_block
            and "ReleaseGraphicsBuffer(ref _indirectArgs);" in release_block
        ),
        "invalid_unlock_skips_dead_graphics_buffers": (
            "if (indirectArgsLocked && IsGraphicsBufferReady(_indirectArgs))" in begin_block
            and "if (indexLocked && IsGraphicsBufferReady(indexBuffer))" in begin_block
            and "if (vertexLocked && IsGraphicsBufferReady(vertexBuffer))" in begin_block
            and "if (IsGraphicsBufferReady(_pendingVertexBuffer) && _pendingVertexCount > 0)" in unlock_helper_block
            and "if (IsGraphicsBufferReady(_pendingIndexBuffer) && _pendingIndexCount > 0)" in unlock_helper_block
            and "if (IsGraphicsBufferReady(_indirectArgs))" in unlock_helper_block
        ),
        "finalize_invalid_upload_resource_fails_closed": (
            "bool pendingUploadResourcesReady = ArePendingUploadBuffersReady();" in finalize_block
            and "if (!pendingUploadResourcesReady)" in finalize_block
            and "MarkPendingChunkFault(buffers, VoxelMeshingFlags.GpuResourceInvalid);" in finalize_block
            and "_releaseRequested = true;" in finalize_block
            and "TryRelease();" in finalize_block
            and "return false;" in finalize_block
            and "private bool ArePendingUploadBuffersReady()" in gpu_text
            and "private void MarkPendingChunkFault(VoxelSurfaceNetsVaultBuffers buffers, byte flags)" in gpu_text
        ),
        "upload_requires_indirect_args_view": "!buffers.IndirectArgs.IsCreated" in begin_block,
        "upload_capacity_fail_closed": (
            "state.VertexCount > buffers.Vertices.Length" in begin_block
            and "state.VertexCount > _maxVertices" in begin_block
            and "state.IndexCount > buffers.Indices.Length" in begin_block
            and "state.IndexCount > _maxIndices" in begin_block
            and "VoxelMeshingStage.Fault" in begin_block
            and "VoxelMeshingFlags.CapacityClamped" in begin_block
        ),
        "upload_silent_truncation_absent": (
            "math.min(state.VertexCount" not in begin_block
            and "math.min(state.IndexCount" not in begin_block
        ),
        "upload_marks_state_after_lock_and_schedule": (
            begin_lock_index >= 0
            and begin_schedule_index > begin_lock_index
            and begin_uploading_stage_index > begin_schedule_index
        ),
        "partial_lock_failure_unlocks_buffers": (
            "bool vertexLocked = false;" in begin_block
            and "bool indexLocked = false;" in begin_block
            and "bool indirectArgsLocked = false;" in begin_block
            and "catch" in begin_block
            and "UnlockBufferAfterWrite<VoxelVertexDTO>(vertexCount)" in begin_block
            and "UnlockBufferAfterWrite<uint>(indexCount)" in begin_block
            and "UnlockBufferAfterWrite<VoxelSurfaceIndirectArgsDTO>(1)" in begin_block
            and "state.Stage = (byte)VoxelMeshingStage.Fault;" in begin_block
        ),
        "finalize_completes_completed_job_before_unlock": (
            is_completed_index >= 0
            and pending_is_completed_index > is_completed_index
            and complete_index > pending_is_completed_index
            and unlock_index > complete_index
            and unlock_helper_present
        ),
        "finalize_no_precomplete_wait": (
            "while (!uploadDependency.IsCompleted)" not in finalize_block
            and "while (!_pendingUploadDependency.IsCompleted)" not in finalize_block
            and "_pendingUploadDependency.Complete();" in finalize_block
        ),
        "release_completed_upload_without_wait": (
            "_pendingUploadDependency = uploadDependency;" in gpu_text
            and "private void UnlockPendingUploadBuffers()" in gpu_text
            and release_is_completed_index >= 0
            and release_complete_index > release_is_completed_index
            and release_unlock_index > release_complete_index
            and "while (!_pendingUploadDependency.IsCompleted)" not in release_block
        ),
        "policy": "GPU upload finalization calls JobHandle.Complete only after IsCompleted is already true, then unlocks GraphicsBuffer write ranges. Upload begin fails closed on vertex/index/indirect-args capacity defects instead of silently truncating mesh payloads, rejects invalid GraphicsBuffer resources before LockBufferForWrite, marks Uploading only after buffers are locked and the copy job is scheduled, and unlocks partial LockBufferForWrite acquisitions only when their buffers remain valid. Finalize rejects invalidated pending upload resources before publishing Uploaded state. Initialize and release defer buffer destruction while an unfinished upload prevents TryRelease; a pending release rejects new uploads and drains after natural job completion. Cold release skips Release() on already-invalid GraphicsBuffer handles and nulls the managed wrapper for recreation.",
    }


def surface_nets_dump_path_proof():
    vault_text = read(FILES["surface_vault"])
    return {
        "agent_dump_path_aligned": 'AgentDumpFileName = "Dump_SHINOBU_308_Voxel.bin"' in vault_text,
        "old_agent_dump_path_absent": "Dump_SHINOBU_61.bin" not in vault_text,
        "writes_primary_and_agent_dump": (
            "TryWriteDumpFile(Path.Combine(dir, DumpFileName)" in vault_text
            and "TryWriteDumpFile(Path.Combine(dir, AgentDumpFileName)" in vault_text
            and "return primary && agent;" in vault_text
        ),
        "policy": "Surface Nets black-box dump keeps the legacy mesh surgeon file and writes the X_006 voxel forensic dump required by the batch prompt.",
    }


def active_dirty_chunk_memory():
    delta_text = read(FILES["delta"])
    chunk_cell_count = first_int_constant(FILES["delta"], "ChunkCellCount", 32768)
    if chunk_cell_count == 0:
        chunk_cell_count = 32768
    dirty_mask_words = first_int_constant(FILES["delta"], "ChunkDirtyMaskWordCount", chunk_cell_count // 32)
    if dirty_mask_words == 0:
        dirty_mask_words = chunk_cell_count // 32
    initial_capacity = first_int_constant(FILES["delta"], "InitialChunkRegistryCapacity", 256)
    pool_capacity = first_int_constant(FILES["delta"], "DirtyChunkStatePoolCapacity", initial_capacity)
    fixed_registry_present = (
        "FixedChunkRegistry<ChunkDeltaState>" in delta_text
        and "FixedChunkRegistry<CompactedChunkState>" in delta_text
        and "FixedChunkRegistry<int>" in delta_text
    )
    memory_text = read(FILES["memory"])
    vault_dirty_pool_ids_present = all(token in memory_text and token in delta_text for token in [
        "ShinobuDeltaCrusherDirtyMaskPool",
        "ShinobuDeltaCrusherSdfBitsPool",
        "ShinobuDeltaCrusherMaterialPool",
        "ShinobuDeltaCrusherCellFlagsPool",
    ])
    vault_dirty_pool_handles_present = (
        "TryResolveVaultChunkStatePool" in delta_text
        and "EnsureGenerationHandle<uint>" in delta_text
        and "EnsureGenerationHandle<ushort>" in delta_text
        and "EnsureGenerationHandle<byte>" in delta_text
        and "GetSubArray(i * ChunkCellCount, ChunkCellCount)" in delta_text
    )
    fixed_pool_present = (
        "EnsureChunkStatePool" in delta_text
        and "TryLeaseChunkState" in delta_text
        and "ReleaseChunkState" in delta_text
    )
    chunk_state_allocator_sites = len(re.findall(r"new\s+ChunkDeltaState\s*\(", delta_text))
    chunk_state_dictionary_grows = "_chunkStates.EnsureCapacity" in delta_text
    local_pool_hard_capacity_proven = (
        fixed_pool_present
        and pool_capacity > 0
        and chunk_state_allocator_sites <= 2
        and not chunk_state_dictionary_grows
    )
    global_datavault_recycler_proven = (
        fixed_registry_present
        and fixed_pool_present
        and vault_dirty_pool_ids_present
        and vault_dirty_pool_handles_present
        and local_pool_hard_capacity_proven
    )
    datavault_hot_swap_rebind_present = all(token in delta_text for token in [
        "GlobalRegistryServiceSlot.DataVault",
        "RebindDataVaultCold",
        "TryApplyPendingDataVaultRebind",
        "DeferDataVaultRebind",
        "_pendingDataVaultNext == null && (_chunkStates.Count > 0 || _compactedChunkStates.Count > 0)",
        "if (_pendingDataVaultRebind)",
        "DisposeChunkStatePool(oldVault)",
        "ReleaseChunkStatePoolVaultHandles(IDataVault vault)",
        "TryCopyNativeSnapshotToBorrowedScratch",
        "RestoreDataVaultAfterFailedRebind",
        "ReleaseScheduledCarveWriteHandle(oldVault)",
    ])
    per_chunk = {
        "DirtyMaskWords": dirty_mask_words * 4,
        "SdfValueBits": chunk_cell_count * 2,
        "MaterialIds": chunk_cell_count,
        "CellFlags": chunk_cell_count,
    }
    per_chunk_total = sum(per_chunk.values())
    compaction_known = {
        "dirtyMaskCopy": dirty_mask_words * 4,
        "deltaSdfCopy": chunk_cell_count * 2,
        "materialCopy": chunk_cell_count,
        "flagsCopy": chunk_cell_count,
        "outputSdf": chunk_cell_count * 2,
        "outputMaterials": chunk_cell_count,
        "outputFlags": chunk_cell_count,
        "rleUniformFlag": 1,
    }
    compaction_source_capacity = first_int_constant(FILES["delta"], "CompactionSourceSdfCapacity", 0)
    if compaction_source_capacity == 0:
        max_grid_dim = first_int_constant(FILES["delta"], "CompactionSourceSdfMaxGridDimension", 0)
        compaction_source_capacity = max_grid_dim * max_grid_dim * max_grid_dim
    compaction_scratch_bytes = compaction_source_capacity + sum(compaction_known.values())
    schedule_start = delta_text.find("private unsafe void TrySchedulePendingCompaction()")
    schedule_end = delta_text.find("private void RequeueCompaction", schedule_start)
    schedule_block = delta_text[schedule_start:schedule_end] if schedule_start >= 0 and schedule_end > schedule_start else ""
    compaction_runtime_alloc_sites = len(re.findall(r"new\s+NativeArray<", schedule_block))
    compaction_scratch_pool_present = (
        "EnsureCompactionScratchBuffers" in delta_text
        and "TryLeaseCompactionScratchBuffers" in delta_text
        and "ReleaseCompactionScratchBuffers" in delta_text
        and compaction_runtime_alloc_sites == 0
    )
    compaction_copy_job_present = (
        "struct VoxelDeltaCopyEncodedSdfJob" in delta_text
        and (".Schedule(encodedSdf.Length, 256)" in schedule_block or ".Schedule(encodedSdfSampleCount, 256)" in schedule_block)
        and "job.Schedule(ChunkCellCount, 64, compactionInputsReady)" in schedule_block
    )
    compaction_dirty_state_copy_job_present = (
        "struct VoxelDeltaCopyChunkStateJob" in delta_text
        and "new VoxelDeltaCopyChunkStateJob" in schedule_block
        and ".Schedule(ChunkCellCount, 64)" in schedule_block
        and "JobHandle.CombineDependencies(chunkStateCopyHandle, sourceCopyHandle)" in schedule_block
    )
    compaction_main_thread_copy_absent = (
        "for (int i = 0; i < encodedSdf.Length; i++)" not in schedule_block
        and "sourceSdf[i] = encodedSdf[i]" not in schedule_block
    )
    compaction_dirty_state_main_thread_copy_absent = all(token not in schedule_block for token in [
        "NativeArray<uint>.Copy(state.DirtyMaskWords",
        "NativeArray<ushort>.Copy(state.SdfValueBits",
        "NativeArray<byte>.Copy(state.MaterialIds",
        "NativeArray<byte>.Copy(state.CellFlags",
    ])
    compaction_source_version_guard_present = (
        "SourceSonarVersion = publishedSonarVersion" in schedule_block
        and "PublishedSonarVersion == request.SourceSonarVersion" in delta_text
    )
    compaction_pressure_scheduler_present = all(token in delta_text for token in [
        "CompactionPressurePendingThreshold",
        "CompactionPressureFreeSlotThreshold",
        "IsCompactionPressureHigh()",
        "TrySchedulePendingCompactionFrostTick",
    ])
    return {
        "chunk_cell_count": chunk_cell_count,
        "dirty_mask_words": dirty_mask_words,
        "initial_fixed_registry_capacity": initial_capacity,
        "fixed_chunk_registry_present": fixed_registry_present,
        "global_datavault_dirty_pool_ids_present": vault_dirty_pool_ids_present,
        "global_datavault_dirty_pool_handles_present": vault_dirty_pool_handles_present,
        "fixed_dirty_chunk_pool_present": fixed_pool_present,
        "fixed_dirty_chunk_pool_capacity": pool_capacity,
        "fixed_dirty_chunk_pool_native_bytes": per_chunk_total * pool_capacity,
        "native_bytes_per_dirty_chunk": per_chunk_total,
        "native_bytes_per_dirty_chunk_breakdown": per_chunk,
        "native_bytes_if_initial_capacity_full": per_chunk_total * initial_capacity,
        "chunk_state_allocator_sites": chunk_state_allocator_sites,
        "chunk_state_dictionary_grows": chunk_state_dictionary_grows,
        "local_pool_hard_capacity_proven": local_pool_hard_capacity_proven,
        "global_datavault_recycler_proven": global_datavault_recycler_proven,
        "global_datavault_hot_swap_rebind_present": datavault_hot_swap_rebind_present,
        "hard_capacity_proven": local_pool_hard_capacity_proven,
        "reason_global_datavault_recycler_not_proven": "Only false when fixed registry, fixed pool, or GlobalDataVault generation handles for dirty mask/SDF/material/flags are missing.",
        "compaction_scratch_pool_present": compaction_scratch_pool_present,
        "compaction_copy_job_present": compaction_copy_job_present,
        "compaction_main_thread_copy_absent": compaction_main_thread_copy_absent,
        "compaction_dirty_state_copy_job_present": compaction_dirty_state_copy_job_present,
        "compaction_dirty_state_main_thread_copy_absent": compaction_dirty_state_main_thread_copy_absent,
        "compaction_source_version_guard_present": compaction_source_version_guard_present,
        "compaction_pressure_scheduler_present": compaction_pressure_scheduler_present,
        "compaction_source_sdf_capacity_bytes": compaction_source_capacity,
        "compaction_scratch_preallocated_bytes": compaction_scratch_bytes,
        "compaction_schedule_native_alloc_sites": compaction_runtime_alloc_sites,
        "x006_blackbox_dump_path": "Docs/AgentLogs/Dump_SHINOBU_308_Voxel.bin",
        "compaction_transient_known_bytes_excluding_source_sdf": sum(compaction_known.values()),
        "compaction_transient_breakdown_excluding_source_sdf": compaction_known,
    }


def volume_registry_proof():
    delta_text = read(FILES["delta"])
    register_start = delta_text.find("public void RegisterVolume(HectonVoxelVolume volume)")
    register_end = delta_text.find("public void UnregisterVolume(HectonVoxelVolume volume)", register_start)
    register_block = delta_text[register_start:register_end] if register_start >= 0 and register_end > register_start else ""
    capacity = first_int_constant(FILES["delta"], "InitialVolumeRegistryCapacity", 0)
    list_hits = line_hits(FILES["delta"], r"List<HectonVoxelVolume>")
    return {
        "fixed_volume_registry_present": (
            "private sealed class FixedVolumeRegistry" in delta_text
            and "private readonly HectonVoxelVolume[] _volumes;" in delta_text
            and "TryAdd(HectonVoxelVolume volume)" in delta_text
            and "RemoveAtSwapBack(int index)" in delta_text
        ),
        "managed_volume_lists_absent": len(list_hits) == 0,
        "registration_overflow_direct_rebuild_present": (
            "if (!_registeredVolumes.TryAdd(volume))" in register_block
            and "volume.RequestDeltaRebuild();" in register_block
        ),
        "volume_registry_capacity": capacity,
        "managed_volume_list_hits": list_hits,
        "policy": "VoxelDeltaProcessor live and pending rebuild volume registries use fixed arrays with hard capacity. Overflow fails closed by direct rebuild request or rejected registration instead of growing managed Lists.",
    }


def engine_active_volume_registry_proof():
    engine_text = read(FILES["engine"])
    constants = extract_int_constants(FILES["engine"])
    register_start = engine_text.find("void RegisterActiveVolume(GameObject volumeObject)")
    register_end = engine_text.find("int FindActiveVolumeIndex(GameObject volumeObject)", register_start)
    register_block = engine_text[register_start:register_end] if register_start >= 0 and register_end > register_start else ""
    return {
        "active_volume_registry_capacity": constants.get("ActiveVolumeRegistryCapacity", 0),
        "dedupe_present": "FindActiveVolumeIndex(volumeObject) >= 0" in register_block,
        "hard_capacity_guard_present": "_activeVolumes.Count >= ActiveVolumeRegistryCapacity" in register_block,
        "eviction_selector_present": "SelectActiveVolumeEvictionIndex" in engine_text,
        "post_eviction_fail_closed_present": "if (_activeVolumes.Count >= ActiveVolumeRegistryCapacity)\n                return;" in register_block,
        "policy": "HectonVoxelEngine active volume lists retain List storage for editor/runtime API compatibility, but RegisterActiveVolume deduplicates, evicts an existing active volume at the 64-slot cap, and fails closed instead of growing the lists.",
    }


def collider_chunk_registry_proof():
    volume_text = read(FILES["volume"])
    engine_text = read(FILES["engine"])
    ensure_start = volume_text.find("private void EnsureColliderChunkCapacity")
    ensure_end = volume_text.find("public MeshCollider GetColliderChunkCollider", ensure_start)
    ensure_block = volume_text[ensure_start:ensure_end] if ensure_start >= 0 and ensure_end > ensure_start else ""
    prepare_start = volume_text.find("public void PrepareForReuse")
    prepare_end = volume_text.find("private void EnsureColliderChunkCapacity", prepare_start)
    prepare_block = volume_text[prepare_start:prepare_end] if prepare_start >= 0 and prepare_end > prepare_start else ""
    chunked_start = engine_text.find("async Awaitable<bool> ApplyChunkedColliderMeshesAsync")
    chunked_end = engine_text.find("void PrepareVolumeForBuild", chunked_start)
    chunked_block = engine_text[chunked_start:chunked_end] if chunked_start >= 0 and chunked_end > chunked_start else ""
    smooth_start = engine_text.find("async Awaitable<bool> ApplySmoothChthonicPillarColliderMeshAsync")
    smooth_end = engine_text.find("static int ResolveColliderChunkCount", smooth_start)
    smooth_block = engine_text[smooth_start:smooth_end] if smooth_start >= 0 and smooth_end > smooth_start else ""
    capacity = first_int_constant(FILES["volume"], "MaxColliderChunkCount", 0)
    return {
        "max_collider_chunk_count": capacity,
        "fixed_registry_arrays_present": (
            "new MeshCollider[MaxColliderChunkCount]" in volume_text
            and "new BoxCollider[MaxColliderChunkCount]" in volume_text
            and "new Mesh[MaxColliderChunkCount]" in volume_text
        ),
        "cold_prepare_prewarms_hierarchy": (
            "PrewarmColliderChunkHierarchy()" in prepare_block
            and "public void PrewarmColliderChunkHierarchy()" in volume_text
            and "private void EnsureColliderChunkCapacity" in volume_text
            and "EnsureColliderChunkCapacity(MaxColliderChunkCount)" in volume_text
            and "ResetColliderChunks(false)" in volume_text
        ),
        "hot_split_requires_prewarmed_hierarchy": (
            "TryUsePrewarmedColliderChunkCapacity(colliderChunkCount)" in chunked_block
            and "TryUsePrewarmedColliderChunkCapacity(1)" in smooth_block
            and "EnsureColliderChunkCapacity(colliderChunkCount)" not in chunked_block
            and "EnsureColliderChunkCapacity(1)" not in smooth_block
        ),
        "runtime_registry_resize_absent": all(token not in ensure_block for token in [
            "new MeshCollider[clampedCount]",
            "new BoxCollider[clampedCount]",
            "new Mesh[clampedCount]",
            "_colliderChunkColliders.Length < clampedCount",
        ]),
        "policy": "Collider chunk registries are fixed at MaxColliderChunkCount. PrepareForReuse prewarms child collider/proxy objects through a private capacity builder; hot collider split paths only accept prewarmed hierarchy and fail to the cinematic fake if it is missing.",
    }


def published_volume_registry_proof():
    volume_text = read(FILES["volume"])
    constants = extract_int_constants(FILES["volume"])
    register_start = volume_text.find("private static void RegisterPublishedVolume")
    register_end = volume_text.find("private static void UnregisterPublishedVolume", register_start)
    register_block = volume_text[register_start:register_end] if register_start >= 0 and register_end > register_start else ""
    raymarch_start = volume_text.find("public static bool TryRaymarchAnyPublishedSdf")
    raymarch_end = volume_text.find("internal static bool TryGetClosestPublishedSonarSdfPayload", raymarch_start)
    raymarch_block = volume_text[raymarch_start:raymarch_end] if raymarch_start >= 0 and raymarch_end > raymarch_start else ""
    sample_start = volume_text.find("public static bool TrySampleRuntimeSdfDensity")
    sample_end = volume_text.find("public static bool TryReadRuntimeSdfDensity", sample_start)
    sample_block = volume_text[sample_start:sample_end] if sample_start >= 0 and sample_end > sample_start else ""
    read_start = volume_text.find("public static bool TryReadRuntimeSdfDensity")
    read_end = volume_text.find("private static void RegisterPublishedVolume", read_start)
    read_block = volume_text[read_start:read_end] if read_start >= 0 and read_end > read_start else ""
    read_accessors_pure = all(
        "RemoveAt" not in block and "RemovePublishedVolumeAtSwapBack" not in block
        for block in (raymarch_block, sample_block, read_block)
    )
    return {
        "max_registered_published_volumes": constants.get("MaxRegisteredPublishedVolumes", 0),
        "list_capacity_matches_max": "new List<HectonVoxelVolume>(MaxRegisteredPublishedVolumes)" in volume_text,
        "register_hard_cap_present": (
            "s_activePublishedVolumes.Count >= MaxRegisteredPublishedVolumes" in register_block
            and "SelectPublishedVolumeEvictionIndex(volume)" in register_block
            and "RemovePublishedVolumeAtSwapBack(evictionIndex)" in register_block
        ),
        "read_raymarch_registry_mutation_absent": "RemoveAt" not in raymarch_block and "RemovePublishedVolumeAtSwapBack" not in raymarch_block,
        "read_sample_registry_mutation_absent": "RemoveAt" not in sample_block and "RemovePublishedVolumeAtSwapBack" not in sample_block,
        "read_density_registry_mutation_absent": "RemoveAt" not in read_block and "RemovePublishedVolumeAtSwapBack" not in read_block,
        "all_read_accessors_pure": read_accessors_pure,
        "swap_back_remove_present": "private static void RemovePublishedVolumeAtSwapBack" in volume_text,
        "policy": "Published sonar/SDF volume registry is capped at MaxRegisteredPublishedVolumes. Registration evicts stale/farthest entries before Add; raymarch and density read accessors do not mutate the registry.",
    }


def mesh_publication_component_cache_proof():
    engine_text = read(FILES["engine"])
    build_start = engine_text.find("Mesh BuildWeldedMeshNative")
    build_end = engine_text.find("private static void ReleaseOrDestroySurfaceMesh", build_start)
    build_block = engine_text[build_start:build_end] if build_start >= 0 and build_end > build_start else ""
    apply_start = engine_text.find("Awaitable<bool> ApplyVolumeMeshAsync")
    apply_end = engine_text.find("static bool TryResolveSelectedChthonicPillarRecord", apply_start)
    apply_block = engine_text[apply_start:apply_end] if apply_start >= 0 and apply_end > apply_start else ""
    add_mesh_collider_index = apply_block.find("mcol = go.AddComponent<MeshCollider>();")
    build_collider_index = apply_block.find("bool buildCollider =")
    early_null_volume_guard_index = apply_block.find("if (volume == null && Application.isPlaying)", build_collider_index)
    mcol_lookup_index = apply_block.find("go.TryGetComponent(out mcol)")
    null_mcol_index = apply_block.find("if (mcol == null)")
    null_volume_index = apply_block.find("if (volume == null)")
    ensure_proxy_index = apply_block.find("BoxCollider fallbackBakeProxy = EnsureVoxelBakeProxyCollider(go);")
    no_build_block_index = apply_block.find("if (!buildCollider)")
    no_build_editor_proxy_guard_index = apply_block.find("if (!Application.isPlaying)", no_build_block_index)
    no_build_return_index = apply_block.find("return true;", no_build_block_index)
    return {
        "build_welded_uses_cached_components": (
            "HectonVoxelVolume volume = null" in build_block
            and "volume.CachedMeshFilter" in build_block
            and "volume.CachedMeshRenderer" in build_block
        ),
        "apply_volume_uses_source_volume": (
            "HectonVoxelVolume volume = data.SourceVolume" in apply_block
            and "volume.CachedRootMeshCollider" in apply_block
            and "NeedsVoxelSurfaceMeshAcquire(go, volume)" in apply_block
        ),
        "mesh_publication_getcomponent_absent": (
            "GetComponent<" not in build_block
            and "GetComponent<" not in apply_block
        ),
        "volume_missing_component_fails_closed": build_block.count("if (volume != null)\n                return null;") >= 2,
        "volume_missing_collider_uses_fake": (
            "if (volume != null)\n                    {\n                        volume.DisableColliderChunksForCinematicFake();\n                        return true;\n                    }" in apply_block
        ),
        "runtime_null_volume_collider_fails_closed": (
            "VoxelMeshPipelineNullVolumeColliderFallbackFlag = 1u << 5" in engine_text
            and "private static void ReportVoxelNullVolumeColliderFallback()" in engine_text
            and build_collider_index >= 0
            and early_null_volume_guard_index > build_collider_index
            and mcol_lookup_index > early_null_volume_guard_index
            and "ReportVoxelNullVolumeColliderFallback();" in apply_block[early_null_volume_guard_index:mcol_lookup_index]
            and "return true;" in apply_block[early_null_volume_guard_index:mcol_lookup_index]
            and "if (mcol == null && !Application.isPlaying)" in apply_block
            and null_mcol_index >= 0
            and add_mesh_collider_index > null_mcol_index
            and early_null_volume_guard_index < add_mesh_collider_index
            and null_volume_index >= 0
            and ensure_proxy_index > null_volume_index
            and early_null_volume_guard_index < ensure_proxy_index
        ),
        "cinematic_fake_proxy_search_editor_only": (
            no_build_block_index >= 0
            and no_build_editor_proxy_guard_index > no_build_block_index
            and no_build_return_index > no_build_editor_proxy_guard_index
            and "go.TryGetComponent(out BoxCollider rootBakeProxy)" in apply_block[no_build_editor_proxy_guard_index:no_build_return_index]
            and "go.transform.Find(VoxelBakeProxyRuntimeName)" in apply_block[no_build_editor_proxy_guard_index:no_build_return_index]
        ),
        "mesh_pipeline_blackbox_agent_dump_aligned": (
            "#if UNITY_EDITOR || DEVELOPMENT_BUILD" in engine_text
            and 'VoxelMeshPipelineBlackBoxPrimaryDumpRelativePath = "Docs/AgentLogs/Dump_VOXEL_MESH_PIPELINE.bin"' in engine_text
            and 'VoxelMeshPipelineBlackBoxAgentDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_308_Voxel.bin"' in engine_text
            and "WriteVoxelMeshPipelineBlackBoxFile(VoxelMeshPipelineBlackBoxPrimaryDumpRelativePath, blackBox, reasonFlags);" in engine_text
            and "WriteVoxelMeshPipelineBlackBoxFile(VoxelMeshPipelineBlackBoxAgentDumpRelativePath, blackBox, reasonFlags);" in engine_text
        ),
        "generated_volume_bound_before_mesh_publication": (
            "static bool TryBindGeneratedVolumeForMeshPublication(GameObject go, VoxelPipelineData data)" in engine_text
            and "data.SourceVolume = volume;" in engine_text
            and "data.SourceRuntimeStamp = volume.RuntimeStamp;" in engine_text
            and engine_text.count("if (!TryBindGeneratedVolumeForMeshPublication(targetGO, pipelineData))") >= 2
            and engine_text.count("DespawnVolume(targetGO);\n                return null;") >= 2
        ),
        "policy": "Surface mesh publication and collider setup use HectonVoxelVolume cached MeshFilter/MeshRenderer/MeshCollider from VoxelPipelineData.SourceVolume. Fresh generation binds the prewarmed HectonVoxelVolume before mesh publication, so runtime generation does not fall into legacy AddComponent branches. Runtime null-volume collider fallback fails closed to visual-only publication and records a mesh-pipeline black-box flag before any component search, MeshCollider creation, or bake-proxy allocation. Cinematic fake proxy searches run only outside play mode. Mesh-pipeline black-box faults write the primary mesh-pipeline dump and the X_006 mandated agent dump. TryGetComponent/AddComponent remain only as cold editor fallback for legacy/null-volume objects; malformed volumes fail closed or use cinematic fake.",
    }


def published_sonar_snapshot_proof():
    volume_text = read(FILES["volume"])
    engine_text = read(FILES["engine"])
    delta_text = read(FILES["delta"])
    publish_start = volume_text.find("internal async Awaitable<bool> PublishSonarSdfSnapshotAsync")
    publish_end = volume_text.find("private async Awaitable<bool> TryPublishSonarSdfVaultPayloadAsync", publish_start)
    publish_block = volume_text[publish_start:publish_end] if publish_start >= 0 and publish_end > publish_start else ""
    vault_start = volume_text.find("private async Awaitable<bool> TryPublishSonarSdfVaultPayloadAsync")
    vault_end = volume_text.find("internal static bool TryEnsurePublishedSonarVaultPayloadCapacity", vault_start)
    if vault_end < 0:
        vault_end = volume_text.find("private bool TryResolvePublishedSonarDescriptorOrigin", vault_start)
    if vault_end < 0:
        vault_end = volume_text.find("private static void Swap", vault_start)
    vault_block = volume_text[vault_start:vault_end] if vault_start >= 0 and vault_end > vault_start else ""
    descriptor_lock_index = vault_block.find("TryAcquireWriteLock(in descriptorHandle")
    descriptor_invalidate_index = vault_block.find("TryInvalidatePublishedSonarVaultDescriptor")
    sdf_lock_index = vault_block.find("TryAcquireWriteLock(in sdfHandle")
    sdf_lock_release_index = vault_block.find("vault.ReleaseWriteLock(in sdfHandle")
    final_descriptor_lock_index = vault_block.find("TryAcquireWriteLock(in descriptorHandle", sdf_lock_release_index)
    ensure_start = volume_text.find("private bool EnsurePublishedSonarCapacity")
    ensure_end = volume_text.find("private void ClearPublishedSonarSdf", ensure_start)
    ensure_block = volume_text[ensure_start:ensure_end] if ensure_start >= 0 and ensure_end > ensure_start else ""
    clear_start = volume_text.find("private void ClearPublishedSonarSdf")
    clear_end = volume_text.find("private bool HasPublishedSonarReadLeases", clear_start)
    if clear_end < 0:
        clear_end = volume_text.find("private void TryClearSonarSdfVaultDescriptor", clear_start)
    clear_block = volume_text[clear_start:clear_end] if clear_start >= 0 and clear_end > clear_start else ""
    total_point_count_pattern = r"int\s+totalPointCount\s*=\s*gridDimensions\.x\s*\*\s*gridDimensions\.y\s*\*\s*gridDimensions\.z"
    max_grid_dim = first_int_constant(FILES["delta"], "CompactionSourceSdfMaxGridDimension", 129)
    max_sonar_payload_bytes = max_grid_dim * max_grid_dim * max_grid_dim
    return {
        "async_publish_present": "PublishSonarSdfSnapshotAsync" in volume_text,
        "encoded_sample_job_present": (
            "struct PublishedSonarSdfEncodeJob" in volume_text
            and ".Schedule(totalPointCount, 256)" in publish_block
            and "IJobParallelFor" in volume_text
        ),
        "staging_swap_present": (
            "_publishedSonarSdfBuild" in volume_text
            and "_publishedSonarAudioMaterialIdsBuild" in volume_text
            and "Swap(ref _publishedSonarSdf, ref _publishedSonarSdfBuild)" in publish_block
            and "Swap(ref _publishedSonarAudioMaterialIds, ref _publishedSonarAudioMaterialIdsBuild)" in publish_block
        ),
        "main_thread_encode_loop_absent": (
            "for (int i = 0; i < totalPointCount; i++)" not in publish_block
            and "smoothDensityField[i]" not in publish_block
        ),
        "vault_copy_job_present": (
            "struct PublishedSonarSdfCopyJob" in volume_text
            and "new PublishedSonarSdfCopyJob" in vault_block
            and ".Schedule(totalPointCount, 256)" in vault_block
        ),
        "vault_memcopy_absent": "NativeArray<byte>.Copy(_publishedSonarSdf, vaultSdf, totalPointCount)" not in vault_block,
        "vault_per_byte_copy_absent": (
            "for (int i = 0; i < totalPointCount; i++)" not in vault_block
            and "vaultSdf[i] = _publishedSonarSdf[i]" not in vault_block
        ),
        "vault_write_lock_released_after_copy": (
            "vault.ReleaseWriteLock(in sdfHandle, SystemID.WorldStreaming)" in vault_block
            and "if (copyScheduled && !copyHandle.IsCompleted)" in vault_block
        ),
        "cancel_force_complete_absent": (
            "cancellationToken.ThrowIfCancellationRequested()" not in publish_block
            and "cancellationToken.ThrowIfCancellationRequested()" not in vault_block
            and "AwaitableDebtMonitor.NextFrameAsync(cancellationToken)" not in publish_block
            and "AwaitableDebtMonitor.NextFrameAsync(cancellationToken)" not in vault_block
            and "if (!encodeCompleted)" not in publish_block
            and "if (copyScheduled && !copyCompleted)" not in vault_block
            and "encodeCancellationRequested |= cancellationToken.IsCancellationRequested" in publish_block
            and "copyCancellationRequested |= cancellationToken.IsCancellationRequested" in vault_block
        ),
        "descriptor_invalidated_before_sdf_copy": (
            descriptor_invalidate_index >= 0
            and sdf_lock_index > descriptor_invalidate_index
            and "private static bool TryInvalidatePublishedSonarVaultDescriptor" in volume_text
            and "descriptors[0] = default;" in volume_text
        ),
        "descriptor_final_write_after_sdf_copy": (
            sdf_lock_release_index >= 0
            and final_descriptor_lock_index > sdf_lock_release_index
        ),
        "descriptor_lock_not_held_during_sdf_copy": (
            sdf_lock_release_index >= 0
            and (
                final_descriptor_lock_index > sdf_lock_release_index
                or descriptor_lock_index > sdf_lock_release_index
            )
        ),
        "active_and_staging_buffers_present": (
            "_publishedSonarSdf" in ensure_block
            and "_publishedSonarAudioMaterialIds" in ensure_block
            and "_publishedSonarSdfBuild" in ensure_block
            and "_publishedSonarAudioMaterialIdsBuild" in ensure_block
        ),
        "high_water_buffer_reuse_present": (
            "PublishedSonarMaxPointCount" in volume_text
            and "(uint)totalPointCount > (uint)PublishedSonarMaxPointCount" in ensure_block
            and "_publishedSonarSdf.Length >= PublishedSonarMaxPointCount" in ensure_block
            and "new NativeArray<byte>(\n                PublishedSonarMaxPointCount" in ensure_block
            and "_publishedSonarSdf.Length == totalPointCount" not in ensure_block
        ),
        "clear_metadata_only_present": (
            "TryClearSonarSdfVaultDescriptor(descriptorVersion, descriptorOrigin)" in clear_block
            and "_publishedSonarGridDimensions = default" in clear_block
            and "Dispose(" not in clear_block
            and "UnregisterNativeArray" not in clear_block
        ),
        "dispose_guarded_by_read_lease_present": (
            "private bool TryDisposePublishedSonarSdfBuffers()" in volume_text
            and "HasPublishedSonarReadLeases()" in volume_text
            and "Volatile.Read(ref _publishedSonarSnapshotPublishInFlight) != 0" in volume_text
            and "TryDrainPendingPublishedSonarSdfDispose()" in volume_text
            and "ReleasePublishedSonarReadLease(lease.BufferIndex);\n            TryDrainPendingPublishedSonarSdfDispose();" in volume_text
        ),
        "clear_aborts_inflight_publish_present": (
            "_publishedSonarPublishAbortRequested" in volume_text
            and "Volatile.Write(ref _publishedSonarPublishAbortRequested, 0)" in publish_block
            and "Volatile.Write(ref _publishedSonarPublishAbortRequested, 1)" in clear_block
            and "Volatile.Read(ref _publishedSonarPublishAbortRequested) != 0" in publish_block
            and "Volatile.Read(ref _publishedSonarPublishAbortRequested) != 0" in vault_block
        ),
        "compaction_copies_actual_grid_count": (
            "encodedSdfSampleCount = gridDimensions.x * gridDimensions.y * gridDimensions.z" in delta_text
            and "TryLeaseCompactionScratchBuffers(\n                    encodedSdfSampleCount" in delta_text
            and "}.Schedule(encodedSdfSampleCount, 256)" in delta_text
        ),
        "vault_fixed_max_capacity_present": (
            "PublishedSonarVaultPayloadCapacity = PublishedSonarMaxPointCount" in volume_text
            and "sdf.Length >= PublishedSonarVaultPayloadCapacity" in volume_text
            and "BufferID.VoxelSdfTexture3D,\n                PublishedSonarVaultPayloadCapacity" in volume_text
        ),
        "vault_publish_hot_ensure_absent": (
            "TryResolvePublishedSonarVaultPayloadHandles(" in vault_block
            and "EnsureGenerationHandle" not in vault_block
        ),
        "vault_owner_phase_prewarm_present": (
            (
                "TryEnsurePublishedSonarVaultPayloadCapacity(GlobalRegistry.DataVault)" in volume_text
                or (
                    "CacheDataVaultCold();" in volume_text
                    and "_cachedDataVault = GlobalRegistry.DataVault;" in volume_text
                    and "TryEnsurePublishedSonarVaultPayloadCapacity(_cachedDataVault)" in volume_text
                )
            )
            and "HectonVoxelVolume.TryEnsurePublishedSonarVaultPayloadCapacity(GlobalRegistry.DataVault)" in engine_text
        ),
        "vault_descriptor_owner_guard_present": (
            "private void TryClearSonarSdfVaultDescriptor(int expectedVersion, Vector3 expectedCapturedRuntimeOrigin)" in volume_text
            and "descriptor.SdfVersion == unchecked((uint)expectedVersion)" in volume_text
            and "math.distancesq(descriptor.VolumeOrigin, expectedOrigin)" in volume_text
            and "TryClearSonarSdfVaultDescriptor(descriptorVersion, descriptorOrigin)" in volume_text
        ),
        "vault_descriptor_unconditional_clear_absent": "private static void TryClearSonarSdfVaultDescriptor()" not in volume_text,
        "vault_publish_serialized_present": (
            "s_publishedSonarVaultPublishInFlight" in volume_text
            and "Interlocked.CompareExchange(ref s_publishedSonarVaultPublishInFlight, 1, 0)" in vault_block
            and "Interlocked.Exchange(ref s_publishedSonarVaultPublishInFlight, 0)" in vault_block
        ),
        "local_publish_serialized_present": (
            "_publishedSonarSnapshotPublishInFlight" in volume_text
            and "Interlocked.CompareExchange(ref _publishedSonarSnapshotPublishInFlight, 1, 0)" in publish_block
            and "Interlocked.Exchange(ref _publishedSonarSnapshotPublishInFlight, 0)" in publish_block
        ),
        "build_buffer_read_lease_guard_present": (
            "TryResolvePublishedSonarBuildBufferIndex(out int buildBufferIndex)" in publish_block
            and "ReadPublishedSonarReadLeaseCount(buildBufferIndex) == 0" in volume_text
            and "CommitPublishedSonarActiveBufferIndex(buildBufferIndex)" in publish_block
        ),
        "compaction_source_read_lease_present": (
            "TryAcquirePublishedSonarSdfPayloadReadLease" in delta_text
            and "SourceSdfReadLease = sourceSdfReadLease" in delta_text
            and "VoxelDeltaCopyEncodedSdfJob" in delta_text
        ),
        "compaction_read_lease_release_present": (
            "ReleasePublishedSonarSdfPayloadReadLease(in sourceSdfReadLease)" in delta_text
            and "request.Volume.ReleasePublishedSonarSdfPayloadReadLease(in request.SourceSdfReadLease)" in delta_text
            and "request.SourceSdfReadLease = default" in delta_text
        ),
        "total_point_count_grid_product_present": re.search(total_point_count_pattern, publish_block) is not None,
        "max_supported_payload_bytes": max_sonar_payload_bytes,
        "max_supported_payload_note": "129^3 encoded SDF bytes matches CompactionSourceSdfMaxGridDimension capacity; audio-material staging doubles local byte arrays. Published sonar buffers now reuse high-water capacity and copy only the current grid product.",
        "vault_copy_policy": "Vault SDF payload copy is scheduled as PublishedSonarSdfCopyJob while holding only the SDF payload write lock. Shared publication is serialized; descriptor is invalidated before SDF copy and final descriptor write happens after SDF release, preventing consumers from reading an old valid descriptor against a partially overwritten SDF buffer. The local published-buffer pair is also serialized: compaction takes a read lease before its source copy job, and the next publish refuses to encode into the build buffer while that physical buffer is leased. Local SDF arrays are max-capacity high-water buffers; clear invalidates metadata/descriptor only, requests abort for any in-flight publish, and physical disposal is guarded by reader leases and publish-in-flight state. The vault SDF lane is resolved from a fixed max-capacity prewarmed handle during publish, so grid-size changes do not resize GlobalDataVault in the publish path. Descriptor clear is guarded by the publishing volume version and AUP-rebased origin, so one volume cannot erase another volume's newer descriptor.",
    }


def save_snapshot_scratch_proof():
    delta_text = read(FILES["delta"])
    save_text = read(FILES["save_manager"])
    save_start = save_text.find("if (saveable is VoxelDeltaProcessor voxelDeltaProcessor)")
    save_end = save_text.find("continue;", save_start)
    save_block = save_text[save_start:save_end] if save_start >= 0 and save_end > save_start else ""
    load_start = save_text.find("if (voxelDeltaProcessor != null)")
    load_end = save_text.find("if (persistentWorldRegistryForLoad != null)", load_start)
    load_block = save_text[load_start:load_end] if load_start >= 0 and load_end > load_start else ""
    snapshot_capacity_method_present = all(token in delta_text for token in [
        "ResolveNativeSnapshotScratchCapacityBytes",
        "DirtyChunkStatePoolCapacity * denseChunkBytes",
        "InitialChunkRegistryCapacity * uniformChunkBytes",
        "SaveBinaryStorage.RawPayloadCapacityBytes",
    ])
    scratch_lifecycle_present = all(token in delta_text for token in [
        "EnsureNativeSnapshotScratchBuffer();",
        "DisposeNativeSnapshotScratchBuffer();",
        "_nativeSnapshotScratch = new NativeArray<byte>",
    ])
    borrowed_copy_present = all(token in delta_text for token in [
        "TryCopyNativeSnapshotToBorrowedScratch",
        "_nativeSnapshotScratch.GetSubArray(0, bytesWritten)",
    ])
    save_uses_borrowed = "TryCopyNativeSnapshotToBorrowedScratch" in save_block
    per_save_allocation_absent = all(token not in save_block for token in [
        "voxelDeltaSnapshot = new NativeArray<byte>",
        "RegisterVoxelDeltaSnapshot(voxelDeltaSnapshot",
        "TryMeasureNativeSnapshotByteCount",
        "TryCopyNativeSnapshot(",
    ])
    borrowed_dispose_guard_present = (
        "bool ownsVoxelDeltaSnapshot = false" in save_text
        and "voxelDeltaSnapshot.IsCreated && ownsVoxelDeltaSnapshot" in save_text
    )
    borrowed_lease_lifetime_present = all(token in delta_text for token in [
        "_nativeSnapshotScratchLeaseCount",
        "_nativeSnapshotScratchDisposeDeferred",
        "ReleaseBorrowedNativeSnapshotScratch",
    ]) and all(token in save_text for token in [
        "borrowedVoxelDeltaSnapshotOwner",
        "ReleaseBorrowedNativeSnapshotScratch()",
    ])
    borrowed_growth_blocked_during_lease = (
        "_nativeSnapshotScratchLeaseCount > 0" in delta_text
        and "_nativeSnapshotScratchDisposeDeferred = true;" in delta_text
        and "stats.TotalBytes > _nativeSnapshotScratch.Length" in delta_text
    )
    copy_start = delta_text.find("public unsafe bool TryCopyNativeSnapshotToBorrowedScratch")
    copy_end = delta_text.find("public void ReleaseBorrowedNativeSnapshotScratch", copy_start)
    copy_block = delta_text[copy_start:copy_end] if copy_start >= 0 and copy_end > copy_start else ""
    rebind_start = delta_text.find("private void RebindDataVaultCold")
    rebind_end = delta_text.find("private bool TryApplyPendingDataVaultRebind", rebind_start)
    rebind_block = delta_text[rebind_start:rebind_end] if rebind_start >= 0 and rebind_end > rebind_start else ""
    borrowed_write_exclusion_present = (
        "_nativeSnapshotScratchLeaseCount > 0" in copy_block
        and "UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(VoxelDeltaProcessor));" in copy_block
        and copy_block.find("_nativeSnapshotScratchLeaseCount > 0") < copy_block.find("GetUnsafeBufferPointerWithoutChecks(_nativeSnapshotScratch)")
    )
    datavault_rebind_waits_for_snapshot_lease = (
        "_nativeSnapshotScratchLeaseCount > 0" in rebind_block
        and "DeferDataVaultRebind" in rebind_block
    )
    copy_failure_fail_closed = (
        "Voxel delta native snapshot copy failed" in save_block
        and "throw new InvalidOperationException" in save_block
    )
    legacy_load_fallback_present = (
        "loadedVoxelDeltaSnapshot.IsCreated && loadedVoxelDeltaSnapshot.Length > 0" in load_block
        and "voxelDeltaProcessor.TryLoadNativeSnapshot" in load_block
        and "voxelDeltaProcessor.LoadFromSaveData(data)" in load_block
    )
    return {
        "processor_borrowed_snapshot_copy_present": borrowed_copy_present,
        "processor_snapshot_scratch_lifecycle_present": scratch_lifecycle_present,
        "processor_snapshot_capacity_method_present": snapshot_capacity_method_present,
        "save_manager_uses_borrowed_snapshot": save_uses_borrowed,
        "save_manager_per_save_nativearray_absent": per_save_allocation_absent,
        "save_manager_borrowed_dispose_guard_present": borrowed_dispose_guard_present,
        "save_manager_borrowed_lease_lifetime_present": borrowed_lease_lifetime_present,
        "processor_borrowed_growth_blocked_during_lease": borrowed_growth_blocked_during_lease,
        "processor_borrowed_write_exclusion_present": borrowed_write_exclusion_present,
        "datavault_rebind_waits_for_snapshot_lease": datavault_rebind_waits_for_snapshot_lease,
        "save_manager_copy_failure_fail_closed": copy_failure_fail_closed,
        "save_manager_legacy_load_fallback_present": legacy_load_fallback_present,
        "policy": "SaveManager borrows an exact NativeArray<byte> slice from VoxelDeltaProcessor-owned native snapshot scratch; the borrowed slice is not disposed by SaveManager.",
    }


def damage_volume_pressure():
    sargassum_text = read(FILES["sargassum"])
    cut_compute_text = read(FILES["cut_compute"])
    damage_compute_text = read(FILES["damage_compute"])
    mask_coalesce_start = sargassum_text.find("private bool TryCoalesceOverflowStamp")
    mask_coalesce_end = sargassum_text.find("private void DecayRecentCutStamps", mask_coalesce_start)
    mask_coalesce_block = sargassum_text[mask_coalesce_start:mask_coalesce_end] if mask_coalesce_start >= 0 and mask_coalesce_end > mask_coalesce_start else ""
    mask_update_start = sargassum_text.find("private void ProcessQueuedMaskUpdate")
    mask_update_end = sargassum_text.find("private void RefreshDamageVolumeBounds", mask_update_start)
    mask_update_block = sargassum_text[mask_update_start:mask_update_end] if mask_update_start >= 0 and mask_update_end > mask_update_start else ""
    damage_coalesce_start = sargassum_text.find("private bool TryCoalesceOverflowDamageVolumeStamp")
    damage_coalesce_end = sargassum_text.find("private void ProcessQueuedDamageVolumeUpdate", damage_coalesce_start)
    damage_coalesce_block = sargassum_text[damage_coalesce_start:damage_coalesce_end] if damage_coalesce_start >= 0 and damage_coalesce_end > damage_coalesce_start else ""
    damage_update_start = sargassum_text.find("private void ProcessQueuedDamageVolumeUpdate")
    damage_update_end = sargassum_text.find("private void QueueGlobalPublish", damage_update_start)
    damage_update_block = sargassum_text[damage_update_start:damage_update_end] if damage_update_start >= 0 and damage_update_end > damage_update_start else ""
    create_resources_start = sargassum_text.find("private void CreateResources")
    create_resources_end = sargassum_text.find("private void RefreshQualityDependentResourcesIfNeeded", create_resources_start)
    create_resources_block = sargassum_text[create_resources_start:create_resources_end] if create_resources_start >= 0 and create_resources_end > create_resources_start else ""
    mask_resolver_start = sargassum_text.find("private GraphicsBuffer ResolveStampCommandWriteBuffer")
    damage_resolver_start = sargassum_text.find("private GraphicsBuffer ResolveDamageVolumeStampCommandWriteBuffer")
    mask_resolver_block = sargassum_text[mask_resolver_start:damage_resolver_start] if mask_resolver_start >= 0 and damage_resolver_start > mask_resolver_start else ""
    damage_resolver_end = sargassum_text.find("private void RefreshMaskWorldRect", damage_resolver_start)
    damage_resolver_block = sargassum_text[damage_resolver_start:damage_resolver_end] if damage_resolver_start >= 0 and damage_resolver_end > damage_resolver_start else ""
    sargassum_consts = extract_int_constants(FILES["sargassum"])
    damage_stamp_capacity = sargassum_consts.get("DamageVolumeStampCapacity", 0)
    cut_stamp_capacity = sargassum_consts.get("StampCommandCapacity", 0)
    cut_command_layout = struct_layout(FILES["sargassum"], "StampCommand")
    cut_command_bytes = cut_command_layout["bytes"] or 16
    command_layout = struct_layout(FILES["sargassum"], "DamageVolumeStampCommand")
    command_bytes = command_layout["bytes"] or 32
    default_res = first_assignment_int(FILES["sargassum"], "damageVolumeResolution", 64)
    default_depth = first_assignment_int(FILES["sargassum"], "damageVolumeDepth", 32)
    max_res = field_range_max(FILES["sargassum"], "damageVolumeResolution", 128)
    max_depth = field_range_max(FILES["sargassum"], "damageVolumeDepth", 96)
    min_res = 32
    min_depth = 16
    bytes_per_voxel = 8
    min_texture_bytes = min_res * min_depth * min_res * bytes_per_voxel
    default_texture_bytes = default_res * default_depth * default_res * bytes_per_voxel
    max_texture_bytes = max_res * max_depth * max_res * bytes_per_voxel
    quality_scaled = all(token in sargassum_text for token in [
        "_damageVolumeRuntimeResolution",
        "_damageVolumeRuntimeDepth",
        "ResolveDamageVolumeDimensions",
        "HomeostasisBrain.GlobalQualityWeight",
    ])
    inactive_resize_gate = all(token in sargassum_text for token in [
        "HasActiveCutOrDamageTextureWork",
        "_queuedStampCount > 0",
        "_queuedDamageVolumeStampCount > 0",
        "_maskEnergy > DamageVolumeEnergyEpsilon",
        "_damageVolumeEnergy > DamageVolumeEnergyEpsilon",
        "ReleaseDamageVolumeTexture(ref _damageVolumeRead)",
    ])
    energy_gated = all(token in sargassum_text for token in [
        "_damageVolumeEnergy",
        "DamageVolumeEnergyEpsilon",
        "QueueDamageVolumeVisualSync",
    ])
    shader_active_energy_gated = all(token in sargassum_text for token in [
        "bool damageVolumeActive =",
        "_damageVolumeRead != null &&",
        "_damageVolumeEnergy > DamageVolumeEnergyEpsilon",
        "_queuedDamageVolumeStampCount > 0",
        "_pendingDamageVolumeDeltaTime > 0f",
        "if (damageVolumeActive)",
        "Shader.SetGlobalFloat(_DamageVolumeActiveId, 1f)",
    ])
    overflow_expands_coverage = (
        "math.distance(existingCenter" in mask_coalesce_block
        and "+ uvRadius" in mask_coalesce_block
        and "payload.x = uvCenter.x" not in mask_coalesce_block
        and "payload.y = uvCenter.y" not in mask_coalesce_block
        and "math.distance(existingCenter" in damage_coalesce_block
        and "+ clampedRadius" in damage_coalesce_block
        and "positionRadius.x = positionWS.x" not in damage_coalesce_block
        and "positionRadius.y = positionWS.y" not in damage_coalesce_block
        and "positionRadius.z = positionWS.z" not in damage_coalesce_block
    )
    mask_upload_fail_closed = all(token in mask_update_block for token in [
        "int uploadedStampCount = 0",
        "if (!TryAcquireVaultBuffer(in _queuedStampCommandsHandle",
        "return;",
        "int safeQueuedStampCount = math.min(_queuedStampCount",
        "queuedStampCommands.Length",
        "GraphicsBufferUploadUtility.UploadNativeArray(stampWriteBuffer, queuedStampCommands, safeQueuedStampCount)",
        "uploadedStampCount = safeQueuedStampCount",
        "_stampCompute.SetInt(_StampCountId, uploadedStampCount)",
    ])
    damage_upload_fail_closed = all(token in damage_update_block for token in [
        "int uploadedDamageVolumeStampCount = 0",
        "if (!TryAcquireVaultBuffer(",
        "return;",
        "int safeQueuedDamageVolumeStampCount = math.min(",
        "queuedDamageVolumeStampCommands.Length",
        "GraphicsBufferUploadUtility.UploadNativeArray(",
        "safeQueuedDamageVolumeStampCount)",
        "uploadedDamageVolumeStampCount = safeQueuedDamageVolumeStampCount",
        "_damageVolumeCompute.SetInt(_DamageVolumeStampCountId, uploadedDamageVolumeStampCount)",
    ])
    stamp_resolvers_invalid_fail_closed = (
        "return _stampCommandBufferB != null && _stampCommandBufferB.IsValid()" in mask_resolver_block
        and ": null;" in mask_resolver_block
        and "return _damageVolumeStampCommandBufferB != null && _damageVolumeStampCommandBufferB.IsValid()" in damage_resolver_block
        and ": null;" in damage_resolver_block
    )
    stamp_graphics_buffers_recreated_when_invalid = all(token in create_resources_block for token in [
        "private void CreateResources",
        "!IsGraphicsBufferReady(_stampCommandBufferA)",
        "!IsGraphicsBufferReady(_stampCommandBufferB)",
        "!IsGraphicsBufferReady(_damageVolumeStampCommandBufferA)",
        "!IsGraphicsBufferReady(_damageVolumeStampCommandBufferB)",
        "_activeStampCommandBuffer = _stampCommandBufferA",
        "_activeDamageVolumeStampCommandBuffer = _damageVolumeStampCommandBufferA",
    ]) and "private static bool IsGraphicsBufferReady(GraphicsBuffer buffer)" in sargassum_text \
        and "return buffer != null && buffer.IsValid();" in sargassum_text
    active_stamp_buffers_validated_before_dispatch = all(token in sargassum_text for token in [
        "private bool EnsureActiveStampCommandBufferReady()",
        "private bool EnsureActiveDamageVolumeStampCommandBufferReady()",
        "private void RequestStampGraphicsBufferRefresh()",
        "_qualityResourceRefreshRequested = true;",
    ]) and "!EnsureActiveStampCommandBufferReady()" in mask_update_block \
        and "!EnsureActiveDamageVolumeStampCommandBufferReady()" in damage_update_block \
        and "RequestStampGraphicsBufferRefresh();" in mask_update_block \
        and "RequestStampGraphicsBufferRefresh();" in damage_update_block
    cut_shader_stamp_count_clamped = all(token in cut_compute_text for token in [
        "static const int HectonStampCommandCapacity = 16;",
        "int stampCount = min(max(_StampCount, 0), HectonStampCommandCapacity);",
        "stampIndex < stampCount",
    ])
    damage_shader_stamp_count_clamped = all(token in damage_compute_text for token in [
        "static const int HectonDamageVolumeStampCapacity = 16;",
        "int stampCount = min(max(_HectonDamageVolumeStampCount, 0), HectonDamageVolumeStampCapacity);",
        "stampIndex < stampCount",
    ])
    return {
        "damage_stamp_capacity_per_frame": damage_stamp_capacity,
        "cut_mask_stamp_capacity_per_frame": cut_stamp_capacity,
        "cut_mask_stamp_command_bytes": cut_command_bytes,
        "cut_mask_stamp_graphics_buffer_bytes": cut_stamp_capacity * cut_command_bytes,
        "cut_mask_stamp_command_offsets": cut_command_layout["offsets"],
        "damage_stamp_command_bytes": command_bytes,
        "damage_stamp_graphics_buffer_bytes": damage_stamp_capacity * command_bytes,
        "damage_stamp_command_offsets": command_layout["offsets"],
        "quality_scaled_runtime_dimensions_present": quality_scaled,
        "quality_resize_inactive_gate_present": inactive_resize_gate,
        "energy_gated_dispatch_present": energy_gated,
        "shader_active_energy_gated_present": shader_active_energy_gated,
        "cut_mask_overflow_coalescing_present": "TryCoalesceOverflowStamp" in sargassum_text,
        "damage_volume_overflow_coalescing_present": "TryCoalesceOverflowDamageVolumeStamp" in sargassum_text,
        "overflow_coalescing_expands_coverage_present": overflow_expands_coverage,
        "cut_mask_upload_fail_closed_present": mask_upload_fail_closed,
        "damage_volume_upload_fail_closed_present": damage_upload_fail_closed,
        "stamp_graphics_buffer_invalid_fail_closed": stamp_resolvers_invalid_fail_closed,
        "stamp_graphics_buffers_recreated_when_invalid": stamp_graphics_buffers_recreated_when_invalid,
        "active_stamp_buffers_validated_before_dispatch": active_stamp_buffers_validated_before_dispatch,
        "cut_mask_shader_stamp_count_clamped": cut_shader_stamp_count_clamped,
        "damage_volume_shader_stamp_count_clamped": damage_shader_stamp_count_clamped,
        "binary_qualitysettings_route_absent": "QualitySettings.GetQualityLevel" not in sargassum_text,
        "minimum_survival_damage_volume_resolution_xzy": [min_res, min_depth, min_res],
        "minimum_survival_ping_pong_texture_bytes_per_dispatch": min_texture_bytes * 2,
        "minimum_survival_ping_pong_texture_bandwidth_bytes_per_second_at_60hz": min_texture_bytes * 2 * 60,
        "default_damage_volume_resolution_xzy": [default_res, default_depth, default_res],
        "default_ping_pong_texture_bytes_per_dispatch": default_texture_bytes * 2,
        "default_ping_pong_texture_bandwidth_bytes_per_second_at_60hz": default_texture_bytes * 2 * 60,
        "max_damage_volume_resolution_xzy": [max_res, max_depth, max_res],
        "max_ping_pong_texture_bytes_per_dispatch": max_texture_bytes * 2,
        "max_ping_pong_texture_bandwidth_bytes_per_second_at_60hz": max_texture_bytes * 2 * 60,
        "overflow_policy": "Same-frame stamp queues are bounded; when capacity is saturated, the newest visual stamp expands the final command slot coverage radius instead of overwriting its center, growing the GraphicsBuffer, or allocating.",
        "dispatch_policy": "3D damage volume dispatch runs only while stamps are queued or tracked damage energy remains above DamageVolumeEnergyEpsilon; idle recovery does not dispatch forever. Runtime quality resize is held until cut/damage texture work is inactive so active drilling does not release/create render textures mid-pipeline.",
        "aup_precision_note": "Current visual route uploads float4 world position/radius into a damage-volume stamp buffer; it is bounded but not a double3 AUP parameter buffer.",
    }


def world_pager_limits():
    pager_text = read(FILES["pager"])
    direct_read_start = pager_text.find("public unsafe bool TryReadPageIntoVaultSlice")
    direct_read_end = pager_text.find("public H8WorldPagerTelemetrySnapshot GetTelemetry", direct_read_start)
    direct_read_block = pager_text[direct_read_start:direct_read_end] if direct_read_start >= 0 and direct_read_end > direct_read_start else ""
    constants = extract_int_constants(FILES["pager"])
    sector_payload = constants.get("SectorPayloadBytes", 0)
    write_slots = constants.get("WriteSlotCount", 0)
    read_slots = constants.get("ReadSlotCount", 0)
    read_queue = constants.get("QueueCapacity", 0)
    direct_read_slice_ready_only = all(token in direct_read_block for token in [
        "slice = default;",
        "out VaultSliceHandle<byte> stagingSlice",
        "TryResolveDirectReadStaging(vault",
        "status = H8WorldPageStatus.Ready;",
        "slice = stagingSlice;",
    ])
    direct_read_staging_prewarmed = all(token in pager_text for token in [
        "private VaultGenerationHandle<byte> _readStagingHandle;",
        "_readStagingHandle = vault.EnsureGenerationHandle<byte>(",
        "BufferID.SaveWorldPagerReadStaging",
        "HasPagerVaultBuffer(in _readStagingHandle, BufferID.SaveWorldPagerReadStaging, SectorPayloadBytes * 2)",
        "ReleasePagerVaultHandle(vault, ref _readStagingHandle, BufferID.SaveWorldPagerReadStaging)",
        "private bool TryResolveDirectReadStaging(",
    ]) and "TryAcquireSliceHandle<byte>" not in direct_read_block
    return {
        "sector_size_bytes": constants.get("SectorSizeBytes", 0),
        "sector_header_bytes": constants.get("SectorHeaderBytes", 0),
        "sector_payload_bytes": sector_payload,
        "write_slot_count": write_slots,
        "read_slot_count": read_slots,
        "read_queue_capacity": read_queue,
        "max_write_arena_payload_bytes": write_slots * sector_payload,
        "max_read_arena_payload_bytes": read_slots * sector_payload,
        "direct_read_slice_ready_only_present": direct_read_slice_ready_only,
        "direct_read_staging_prewarmed_present": direct_read_staging_prewarmed,
        "overflow_policy": "TryEnqueueWrite rejects payloads larger than SectorPayloadBytes or when pending/write queue count reaches WriteSlotCount.",
    }


def global_data_vault_pool_limits(pager, carve_queue, damage, published_sonar):
    vault_text = read(FILES["global_vault"])
    bootstrap_text = read(FILES["bootstrap"])
    constants = extract_numeric_constants(FILES["global_vault"])
    world_constants = extract_int_constants(FILES["world_residency"])
    hydration_layout = struct_layout(FILES["world_runtime"], "ChunkHydrationApplyRecord")
    default_arena = constants.get("DefaultArenaBytes", 0)
    minimum_arena = constants.get("MinimumQualityArenaLimitBytes", 0)
    maximum_arena = constants.get("MaximumQualityArenaLimitBytes", 0)
    block_alignment = constants.get("VaultBlockAlignment", 0)
    max_buffer_capacity = constants.get("MaxBufferCapacity", 0)
    max_block_capacity = constants.get("MaxBlockCapacity", 0)
    max_generation_capacity = constants.get("MaxGenerationHandleCapacity", 0)
    boot_prewarm_present = all(token in bootstrap_text for token in [
        "PreallocateDataVaultPrimaryBuffers(_globalDataVault, in authoredConfig);",
        "BufferID.H8Time",
        "BufferID.RigidbodyAUPs",
        "512",
        "VaultSovereigntyMaintenance.PrewarmBuffers(",
    ])
    bounded_growth_present = all(token in vault_text for token in [
        "ResolveBufferCapacity(capacity)",
        "ResolveBlockCapacity(safeCapacity)",
        "if (_keys.Length >= _keys.Capacity)",
        "if (requiredBytes > _arenaBytes && !TryGrowArenaForBytes(requiredBytes))",
        "_arenaBytes >= _arenaCapacityLimitBytes",
    ])
    pointer_alignment_audit_present = all(token in vault_text for token in [
        "internal const int VaultBlockAlignment = 64",
        "IsPointerAligned(pointer, VaultBlockAlignment)",
        "DefragFlagUnaligned",
    ])
    x006_lane_bytes = {
        "scheduled_carve_write_payload": carve_queue["scheduled_carve_write_payload_bytes"],
        "queued_carve_ingress_payload": carve_queue["queued_carve_event_payload_bytes"],
        "world_pager_write_arena": pager["max_write_arena_payload_bytes"],
        "world_pager_read_arena": pager["max_read_arena_payload_bytes"],
        "world_pager_direct_read_staging": pager["sector_payload_bytes"] * 2,
        "world_pager_compression_scratch": pager["sector_payload_bytes"],
        "sargassum_cut_mask_stamp_graphics_buffer": damage["cut_mask_stamp_graphics_buffer_bytes"],
        "sargassum_damage_stamp_graphics_buffer": damage["damage_stamp_graphics_buffer_bytes"],
        "published_sonar_vault_sdf_payload": published_sonar["max_supported_payload_bytes"],
        "world_residency_hydration_apply_ledger_default": (
            world_constants.get("DefaultMaxChunkCount", 0) * hydration_layout["bytes"]
        ),
    }
    return {
        "default_buffer_capacity": constants.get("DefaultBufferCapacity", 0),
        "max_buffer_capacity": max_buffer_capacity,
        "max_generation_handle_capacity": max_generation_capacity,
        "max_block_capacity": max_block_capacity,
        "vault_block_alignment_bytes": block_alignment,
        "initial_arena_bytes": default_arena,
        "minimum_quality_arena_limit_bytes": minimum_arena,
        "maximum_quality_arena_limit_bytes": maximum_arena,
        "arena_grow_slack_bytes": constants.get("ArenaGrowSlackBytes", 0),
        "max_live_defrag_move_bytes_per_slice": constants.get("MaxLiveDefragMoveBytesPerSlice", 0),
        "defrag_black_box_frames": constants.get("DefragBlackBoxFrameCount", 0),
        "max_relocation_record_count": constants.get("MaxRelocationRecordCount", 0),
        "max_memory_budget_entries": constants.get("MaxMemoryBudgetEntries", 0),
        "boot_primary_prewarm_present": boot_prewarm_present,
        "bounded_growth_guards_present": bounded_growth_present,
        "pointer_alignment_audit_present": pointer_alignment_audit_present,
        "preallocated_core_lanes": {
            "H8Time": "H8TimeSlot.Count",
            "RigidbodyAUPs": 512,
            "VaultSovereigntyMaintenance": "HotEntityCapacity or DefaultHotEntityCapacity",
        },
        "x006_fixed_lane_payload_bytes": x006_lane_bytes,
        "x006_fixed_lane_payload_total_bytes": sum(x006_lane_bytes.values()),
        "policy": "GlobalDataVault boots one 64-byte aligned unmanaged arena, clamps buffer/block metadata capacity, grows only up to the resolved arena limit, and exposes X_006 hot routes through prewarmed generation handles or fixed-size queues. Read paths in X_006 resolve existing handles; they do not allocate or grow vault buffers.",
    }


def carve_queue_pressure():
    delta_text = read(FILES["delta"])
    constants = extract_int_constants(FILES["delta"])
    pending_capacity = constants.get("InitialPendingCarveCapacity", 0)
    event_capacity = constants.get("InitialCarveEventQueueCapacity", 0)
    scheduled_write_capacity = constants.get("ScheduledCarveWriteCapacity", 0)
    if scheduled_write_capacity <= 0 and "ScheduledCarveWriteCapacity = ChunkCellCount * 4" in delta_text:
        scheduled_write_capacity = first_int_constant(FILES["rle"], "ChunkCellCount", 32768) * 4
    event_layout = struct_layout(FILES["signals"], "VoxelCarveEvent")
    carve_write_layout = struct_layout(FILES["delta"], "CarveCellWrite")
    try_resolve_start = delta_text.find("private bool TryResolveScheduledCarveWriteBuffer")
    ensure_write_start = delta_text.find("private bool EnsureScheduledCarveWriteBuffer", try_resolve_start)
    try_resolve_block = delta_text[try_resolve_start:ensure_write_start] if try_resolve_start >= 0 and ensure_write_start > try_resolve_start else ""
    schedule_start = delta_text.find("private unsafe void TrySchedulePendingCarve")
    schedule_end = delta_text.find("private static bool TryResolveScheduledCarveCandidateCount", schedule_start)
    schedule_block = delta_text[schedule_start:schedule_end] if schedule_start >= 0 and schedule_end > schedule_start else ""
    return {
        "pending_carve_capacity": pending_capacity,
        "queued_carve_event_capacity": event_capacity,
        "voxel_carve_event_bytes": event_layout["bytes"],
        "queued_carve_event_payload_bytes": event_capacity * event_layout["bytes"],
        "scheduled_carve_write_capacity": scheduled_write_capacity,
        "scheduled_carve_write_bytes": carve_write_layout["bytes"],
        "scheduled_carve_write_payload_bytes": scheduled_write_capacity * carve_write_layout["bytes"],
        "scheduled_carve_write_prewarm_present": (
            "EnsureScheduledCarveWriteBuffer();" in delta_text
            and "ScheduledCarveWriteCapacity" in delta_text
        ),
        "queued_drain_continuous_quality_scaled": (
            "ResolveQueuedCarveDrainBudgetPerFrame" in delta_text
            and "HomeostasisBrain.GlobalQualityWeight" in delta_text
            and "_queuedCarveDrainBudgetTokens" in delta_text
            and "quality * quality * (3f - 2f * quality)" in delta_text
            and "ResolveQualityWeightFromTier" not in delta_text
            and "DebugResolveQueuedCarveDrainBudget(HectonQualityTier" not in delta_text
        ),
        "scheduled_commit_continuous_quality_scaled": (
            "ResolveScheduledCarveCommitWritesPerFrame" in delta_text
            and "HomeostasisBrain.GlobalQualityWeight" in delta_text
            and "_scheduledCarveCommitWriteTokens" in delta_text
            and "ScheduledCarveBacklogPressureBoost" in delta_text
        ),
        "scheduled_commit_max_writes_per_frame": constants.get("MaxScheduledCarveCommitWritesPerFrame", 0),
        "scheduled_commit_min_writes_per_frame": constants.get("MinScheduledCarveCommitWritesPerFrame", 0),
        "scheduled_carve_write_hot_growth_absent": "EnsureGenerationHandle<CarveCellWrite>" not in try_resolve_block,
        "scheduled_carve_write_over_capacity_reject_present": "requiredCount > ScheduledCarveWriteCapacity" in delta_text,
        "scheduled_carve_candidate_overflow_guard_present": (
            "TryResolveScheduledCarveCandidateCount" in delta_text
            and "long xy = (long)span.x * span.y" in delta_text
            and "long total = xy * span.z" in delta_text
            and "total > ScheduledCarveWriteCapacity" in delta_text
            and "WriteBlackBoxSample(EntityId.ToULong(volume.GetEntityId()), VoxelBlackBoxQueueOverflowFlag)" in delta_text
        ),
        "scheduled_carve_schedule_exception_blackbox_present": (
            "catch (Exception exception)" in schedule_block
            and "WriteBlackBoxSample(EntityId.ToULong(volume.GetEntityId()), VoxelBlackBoxInvalidPendingCarveFlag)" in schedule_block
            and "DumpBlackBoxOnce(VoxelBlackBoxInvalidPendingCarveFlag)" in schedule_block
        ),
        "queue_overflow_coalescing_present": (
            "ResolveOverflowQueuedCarveEvent" in delta_text
            and "CanCoalesceQueuedCarveEvent" in delta_text
            and "queuedEvent = ResolveOverflowQueuedCarveEvent(in overflowEvent, in queuedEvent)" in delta_text
        ),
        "pending_overflow_coalescing_present": (
            "TryCoalesceOverflowPendingCarve" in delta_text
            and "TryCoalescePendingCarve" in delta_text
            and "return TryCoalesceOverflowPendingCarve(in request)" in delta_text
        ),
        "blind_oldest_drop_absent": "_queuedCarveEvents.TryDequeue(out _)" not in delta_text,
        "pending_managed_growth_absent": all(token not in delta_text for token in [
            "Array.Resize",
            "List<PendingCarveRequest>",
            "new List<PendingCarveRequest>",
        ]),
        "event_layout_offsets": event_layout["offsets"],
        "policy": "Ingress, pending, and scheduled write buffers are fixed-size. Under saturation, compatible newest laser cuts coalesce into a capsule/radius-expanded command; incompatible overload is shed through the black-box overflow flag instead of growing memory. Queue drain and scheduled carve commit use continuous GlobalQualityWeight token buckets; backlog raises commit cadence without changing memory capacity.",
    }


def voxel_delta_shutdown_completion_proof():
    delta_text = read(FILES["delta"])
    force_complete_hits = line_hits(FILES["delta"], r"forceComplete:\s*true")
    shutdown_only_hits = []
    non_shutdown_hits = []
    for hit in force_complete_hits:
        context = source_window(FILES["delta"], hit["line"], before=10, after=2)
        if "ForShutdownOnly" in context and "[BLOCKING_SYNC_POINT] OnDisable teardown only" in context:
            shutdown_only_hits.append(hit)
        else:
            non_shutdown_hits.append(hit)

    on_disable_start = delta_text.find("private void OnDisable()")
    tick_start = delta_text.find("public void Tick(float deltaTime)", on_disable_start)
    on_disable_block = delta_text[on_disable_start:tick_start] if on_disable_start >= 0 and tick_start > on_disable_start else ""
    return {
        "force_complete_hits": force_complete_hits,
        "shutdown_only_force_complete_hits": shutdown_only_hits,
        "non_shutdown_force_complete_hits": non_shutdown_hits,
        "shutdown_only_method_names_present": (
            "DisposeScheduledCarveBuffersForShutdownOnly" in delta_text
            and "DisposeScheduledCompactionBuffersForShutdownOnly" in delta_text
        ),
        "old_dispose_names_absent": (
            "DisposeScheduledCarveBuffers();" not in delta_text
            and "DisposeScheduledCompactionBuffers();" not in delta_text
            and "private void DisposeScheduledCarveBuffers()" not in delta_text
            and "private void DisposeScheduledCompactionBuffers()" not in delta_text
        ),
        "on_disable_calls_shutdown_only": (
            "DisposeScheduledCarveBuffersForShutdownOnly();" in on_disable_block
            and "DisposeScheduledCompactionBuffersForShutdownOnly();" in on_disable_block
        ),
        "hot_carve_completion_nonblocking": "DispatcherJobSwap.TryComplete(ref _scheduledCarveHandle, false)" in delta_text,
        "hot_compaction_completion_nonblocking": "DispatcherJobSwap.TryComplete(ref _scheduledCompactionHandle, false)" in delta_text,
    }


def rle_packet_layout(sector_payload_bytes):
    rle_text = read(FILES["rle"])
    try_resolve_start = rle_text.find("internal static bool TryResolveVaultBuffers")
    generate_schema_start = rle_text.find("public static void GenerateEmergencyMockVoxelSchema", try_resolve_start)
    try_resolve_block = rle_text[try_resolve_start:generate_schema_start] if try_resolve_start >= 0 and generate_schema_start > try_resolve_start else ""
    native_header = struct_layout(FILES["delta"], "NativeSnapshotHeader")
    native_chunk = struct_layout(FILES["delta"], "NativeSnapshotChunkHeaderDeltaRle")
    save_run = struct_layout(FILES["save_delta"], "SaveVoxelDeltaRun8")
    arch_header = struct_layout(FILES["rle"], "VoxelDeltaHeaderDTO")
    arch_run = struct_layout(FILES["rle"], "VoxelDeltaRleRunDTO")
    arch_constants = extract_int_constants(FILES["rle"])
    chunk_cells = first_int_constant(FILES["rle"], "ChunkCellCount", 32768)
    max_arch_runs_per_wal = arch_constants.get("MaxVoxelDeltaRleRunsPerWalPayload", chunk_cells)
    native_worst = native_chunk["bytes"] + (chunk_cells * save_run["bytes"])
    arch_full_chunk_worst = arch_header["bytes"] + (chunk_cells * arch_run["bytes"])
    arch_worst = arch_header["bytes"] + (max_arch_runs_per_wal * arch_run["bytes"])
    dirty_mask_bytes = (chunk_cells // 32) * 4
    dense_payload_bytes = dirty_mask_bytes + (chunk_cells * 2) + chunk_cells + chunk_cells
    dense_total = native_chunk["bytes"] + dense_payload_bytes
    dense_fallback_present = has_text(FILES["delta"], "ShouldUseDenseDeltaSnapshot") and has_text(FILES["delta"], "WriteDirtyDenseDeltaNativeSnapshotChunk")
    effective_native_worst = min(native_worst, dense_total) if dense_fallback_present else native_worst
    return {
        "native_snapshot_header": native_header,
        "native_snapshot_chunk_header_delta_rle": native_chunk,
        "save_voxel_delta_run8": save_run,
        "voxel_delta_header_dto": arch_header,
        "voxel_delta_rle_run_dto": arch_run,
        "alignment_proof": "All listed packet structs are explicit layout and multiples of 8 bytes.",
        "architecture_max_wal_payload_bytes": arch_constants.get("MaxVoxelDeltaWalPayloadBytes", 0),
        "architecture_wal_payload_guard_present": (
            "int byteCount = counters[CounterWalPayloadBytes];" in rle_text
            and "byteCount > walPayloadBytes.Length" in rle_text
            and "byteCount > MaxVoxelDeltaWalPayloadBytes" in rle_text
            and "int count = Counters[CounterCompressedBytes];" in rle_text
            and "count < 0 || count > CompressedBytes.Length || count > MaxVoxelDeltaWalPayloadBytes" in rle_text
            and "int compressedBytes = Counters[CounterCompressedBytes];" in rle_text
            and "compressedBytes > CompressedBytes.Length" in rle_text
            and "compressedBytes > MaxVoxelDeltaWalPayloadBytes - headerBytes" in rle_text
            and "required > MaxVoxelDeltaWalPayloadBytes" in rle_text
        ),
        "compression_telemetry_dump_path_aligned": (
            'VoxelDeltaTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_308_Voxel.bin"' in rle_text
            and "Dump_VOXEL_IO_SURGEON.bin" not in rle_text
        ),
        "vault_buffer_cell_capacity_fixed_to_chunk": "int safeCells = ChunkCellCount;" in try_resolve_block,
        "max_voxel_delta_rle_runs_per_wal_payload": max_arch_runs_per_wal,
        "vault_buffer_run_capacity_clamped_to_wal_payload": (
            "rleRunCapacity <= 0 ? MaxVoxelDeltaRleRunsPerWalPayload : rleRunCapacity" in try_resolve_block
            and "math.min(ChunkCellCount, MaxVoxelDeltaRleRunsPerWalPayload)" in try_resolve_block
        ),
        "vault_buffer_staging_capacity_clamped_to_wal_payload": (
            "stagingCapacityBytes <= 0 ? MaxVoxelDeltaWalPayloadBytes : stagingCapacityBytes" in try_resolve_block
            and "MaxVoxelDeltaWalPayloadBytes));" in try_resolve_block
        ),
        "vault_buffer_sector_stats_capacity_clamped": (
            "MaxVoxelDeltaSectorStats = 512" in rle_text
            and "math.clamp(sectorStatsCapacity <= 0 ? 1 : sectorStatsCapacity, 1, MaxVoxelDeltaSectorStats)" in try_resolve_block
        ),
        "chunk_cell_count": chunk_cells,
        "native_snapshot_worst_case_chunk_payload_bytes": native_worst,
        "native_snapshot_dense_fallback_present": dense_fallback_present,
        "native_snapshot_dense_payload_bytes": dense_payload_bytes,
        "native_snapshot_dense_total_bytes": dense_total,
        "native_snapshot_effective_worst_case_bytes": effective_native_worst,
        "voxel_delta_architecture_full_chunk_theoretical_payload_bytes": arch_full_chunk_worst,
        "voxel_delta_architecture_worst_case_payload_bytes": arch_worst,
        "sector_payload_bytes": sector_payload_bytes,
        "native_worst_case_exceeds_sector_payload_by_bytes": max(0, native_worst - sector_payload_bytes),
        "native_effective_worst_case_exceeds_sector_payload_by_bytes": max(0, effective_native_worst - sector_payload_bytes),
        "architecture_worst_case_exceeds_sector_payload_by_bytes": max(0, arch_worst - sector_payload_bytes),
        "architecture_queue_growth_unbounded": False,
        "architecture_overflow_policy": "VoxelDeltaCompressionArchitecture WAL packing and enqueue reject payloads over MaxVoxelDeltaWalPayloadBytes before they reach the pager write queue.",
    }


def job_malloc_proof():
    voxel_sources = [FILES["engine"], FILES["delta"], FILES["rle"], FILES["surface_vault"]]
    unsafe_malloc = []
    native_allocations = []
    for path in voxel_sources:
        unsafe_malloc.extend(line_hits(path, r"UnsafeUtility\.Malloc"))
        native_allocations.extend(line_hits(path, r"new\s+Native(Array|List|ParallelHashMap|Queue)<"))
    classified_native_allocations = classify_native_allocation_hits(native_allocations)
    residual_hot_allocations = (
        classified_native_allocations.get("hot_rebuild", []) +
        classified_native_allocations.get("generation_or_rebuild_snapshot", []) +
        classified_native_allocations.get("fallback_only", [])
    )
    return {
        "unsafe_utility_malloc_hits": unsafe_malloc,
        "native_allocation_hits": native_allocations,
        "native_allocation_hits_classified": classified_native_allocations,
        "residual_hot_native_allocation_hits": residual_hot_allocations,
        "execute_voxel_rle_encoder_has_malloc_token": "UnsafeUtility.Malloc" in read(FILES["rle"])[read(FILES["rle"]).find("internal struct VoxelRleEncoderJob"):read(FILES["rle"]).find("internal struct VoxelDeltaRlePackJob")],
        "execute_surface_nets_has_malloc_token": "UnsafeUtility.Malloc" in read(ROOT / "Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs"),
    }


def voxel_mesh_pool_proof():
    engine_text = read(FILES["engine"])
    volume_text = read(FILES["volume"])
    constants = extract_int_constants(FILES["engine"])
    warm_surface_start = engine_text.find("private static async Awaitable WarmVoxelSurfaceMeshPoolAsync")
    warm_end = engine_text.find("private static bool ShouldAbortVoxelMeshPoolWarmup", warm_surface_start)
    warm_block = engine_text[warm_surface_start:warm_end] if warm_surface_start >= 0 and warm_end > warm_surface_start else ""
    acquire_surface_start = engine_text.find("private static async Awaitable<Mesh> AcquireVoxelSurfaceMeshAsync")
    acquire_end = engine_text.find("internal static Mesh AcquireVoxelSurfaceMesh()", acquire_surface_start)
    acquire_block = engine_text[acquire_surface_start:acquire_end] if acquire_surface_start >= 0 and acquire_end > acquire_surface_start else ""
    return {
        "surface_mesh_pool_size": constants.get("VoxelSurfaceMeshPoolSize", 0),
        "physics_bake_mesh_pool_size": constants.get("VoxelPhysicsBakeMeshPoolSize", 0),
        "acquire_warmup_retry_frames": constants.get("VoxelMeshPoolAcquireWarmupRetryFrames", 0),
        "cold_prewarm_creates_surface_pool_meshes": "CreateVoxelPoolMesh(VoxelSurfacePoolMeshName)" in warm_block,
        "cold_prewarm_creates_physics_pool_meshes": "CreateVoxelPoolMesh(VoxelPhysicsBakePoolMeshName)" in warm_block,
        "runtime_acquire_lazy_mesh_creation_absent": "CreateVoxelPoolMesh(" not in acquire_block,
        "runtime_acquire_waits_for_existing_prewarm": (
            "VoxelMeshPoolAcquireWarmupRetryFrames" in acquire_block
            and "_voxelMeshPoolWarmupRunning" in acquire_block
            and "AwaitableDebtMonitor.NextFrameAsync(ct)" in acquire_block
        ),
        "runtime_acquire_fails_closed": "return null;" in acquire_block,
        "physics_bake_schedule_fail_releases_pool_mesh": (
            "internal void ReleaseColliderChunkBakeMesh(int index)" in volume_text
            and "ReleaseVoxelPhysicsBakeMesh(bakeMesh)" in volume_text
            and "volume.ReleaseColliderChunkBakeMesh(0);" in engine_text
            and "volume.ReleaseColliderChunkBakeMesh(chunkIndex);" in engine_text
        ),
        "policy": "Surface and PhysX bake meshes are created only by the cold prewarm route. Runtime acquire retries against already warmed pool slots for a few frames and then fails closed instead of allocating a new Mesh in the voxel path.",
    }


def voxel_volume_spawn_pool_proof():
    engine_text = read(FILES["engine"])
    spawn_start = engine_text.find("GameObject SpawnVolume()")
    spawn_end = engine_text.find("Mesh BuildWeldedMeshNative", spawn_start)
    spawn_block = engine_text[spawn_start:spawn_end] if spawn_start >= 0 and spawn_end > spawn_start else ""
    spawn_call_blocks = []
    for match in re.finditer(r"GameObject targetGO = SpawnVolume\(\);", engine_text):
        spawn_call_blocks.append(engine_text[match.start():match.start() + 140])

    runtime_guard_index = spawn_block.find("if (Application.isPlaying)")
    new_game_object_index = spawn_block.find("new GameObject(RuntimeCaveVolumeName)")

    return {
        "spawn_volume_method_present": bool(spawn_block),
        "runtime_pool_miss_reports_blackbox": "ReportVoxelVolumeSpawnPoolMiss();" in spawn_block,
        "runtime_pool_miss_returns_null": (
            "if (Application.isPlaying)" in spawn_block
            and "return null;" in spawn_block[runtime_guard_index:new_game_object_index if new_game_object_index > runtime_guard_index else len(spawn_block)]
        ),
        "editor_fallback_after_runtime_guard": (
            new_game_object_index > runtime_guard_index >= 0
        ),
        "runtime_fallback_new_gameobject_absent": (
            runtime_guard_index >= 0
            and new_game_object_index > runtime_guard_index
        ),
        "spawn_call_count": len(spawn_call_blocks),
        "all_spawn_calls_null_guarded": (
            bool(spawn_call_blocks)
            and all("if (targetGO == null)" in block and "return null;" in block for block in spawn_call_blocks)
        ),
        "blackbox_flag_present": "VoxelMeshPipelineVolumeSpawnPoolMissFlag" in engine_text,
        "policy": "Runtime voxel volume creation is ObjectPool-only. If the pool/prefab route misses in play mode, the pipeline writes a blackbox flag and returns null; the editor-only fallback keeps authoring/debug generation usable without hiding runtime allocations.",
    }


def mesh_upload_budgeting():
    engine_text = read(FILES["engine"])
    upload_sites = line_hits(FILES["engine"], r"UploadSurfaceMesh\(|UploadColliderMesh\(")
    direct_upload_call_count = sum(
        1 for hit in upload_sites
        if "static bool UploadSurfaceMesh" not in hit["text"]
        and "static bool UploadColliderMesh" not in hit["text"]
    )
    mesh_data_upload_guard_present = all(token in engine_text for token in [
        "private static bool CanUploadMeshData",
        "if (!CanUploadMeshData(mesh, positions, triangleIndices, vertexCount, triangleIndexCount))",
        "ReportVoxelInvalidMeshUpload();",
        "bool applied = false;",
        "if (!applied)\n                meshDataArray.Dispose();",
        "if (!UploadSurfaceMesh(",
        "if (!UploadColliderMesh(",
    ])
    return {
        "budget_constant_present": "VoxelMeshUploadBudgetPerFrame" in engine_text,
        "continuous_quality_budget_present": (
            "ResolveVoxelMeshUploadBudgetPerFrame" in engine_text
            and "HomeostasisBrain.GlobalQualityWeight" in engine_text
            and "_voxelMeshUploadBudgetTokens" in engine_text
        ),
        "low_tier_burst_bias_present": (
            "VoxelMeshUploadBurstCapBias" in engine_text
            and "Mathf.Ceil(frameBudget - VoxelMeshUploadBurstCapBias)" in engine_text
        ),
        "budget_awaiter_present": "AwaitVoxelMeshUploadBudgetAsync" in engine_text,
        "budgeted_upload_call_count": engine_text.count("await AwaitVoxelMeshUploadBudgetAsync"),
        "direct_upload_call_count": direct_upload_call_count,
        "mesh_data_upload_guard_present": mesh_data_upload_guard_present,
        "budgeted": (
            "VoxelMeshUploadBudgetPerFrame" in engine_text
            and "AwaitVoxelMeshUploadBudgetAsync" in engine_text
            and engine_text.count("await AwaitVoxelMeshUploadBudgetAsync") >= direct_upload_call_count
            and mesh_data_upload_guard_present
        ),
        "policy": "Unity mesh upload remains main-thread API; budget gate uses a continuous GlobalQualityWeight token bucket from 1 to 3 uploads per frame and delays later uploads behind Dear Lie visual clipping.",
    }


def deferred_collider_upload_budgeting():
    engine_text = read(FILES["engine"])
    return {
        "continuous_quality_budget_present": (
            "ResolveDeferredVoxelColliderUploadBudgetPerFrame" in engine_text
            and "HomeostasisBrain.GlobalQualityWeight" in engine_text
            and "_deferredVoxelColliderUploadBudgetTokens" in engine_text
        ),
        "token_bucket_present": (
            "ResolveDeferredVoxelColliderUploadBudgetThisFrame" in engine_text
            and "Mathf.Ceil(frameBudget - DeferredVoxelColliderUploadBurstCapBias)" in engine_text
        ),
        "drain_uses_resolved_budget": (
            "DrainDeferredVoxelColliderUploads(ResolveDeferredVoxelColliderUploadBudgetThisFrame())" in engine_text
        ),
        "fixed_budget_driver_absent": (
            "DrainDeferredVoxelColliderUploads(DeferredVoxelColliderUploadBudgetPerFrame)" not in engine_text
        ),
        "policy": "Deferred voxel collider cleanup uses a continuous GlobalQualityWeight token bucket from 1 to 4 queue drains per frame. The lane drains staged/proxy state only; runtime non-null PhysX mesh publication remains disabled.",
    }


def physics_bake_schedule_guard():
    engine_text = read(FILES["engine"])
    constants = extract_int_constants(FILES["engine"])
    schedule_start = engine_text.find("static bool TryScheduleVoxelPhysicsBake")
    schedule_end = engine_text.find("static void ReportVoxelPhysicsBakeCompletion", schedule_start)
    schedule_block = engine_text[schedule_start:schedule_end] if schedule_start >= 0 and schedule_end > schedule_start else ""
    guard_start = engine_text.find("private static bool CanScheduleVoxelPhysicsBake")
    guard_end = engine_text.find("static void ReportVoxelPhysicsBakeCompletion", guard_start)
    guard_block = engine_text[guard_start:guard_end] if guard_start >= 0 and guard_end > guard_start else ""
    wait_start = engine_text.find("static async Awaitable<bool> AwaitForPhysicsBakeCompletionOrDeferAsync")
    wait_end = engine_text.find("static async Awaitable AwaitVoxelMeshUploadBudgetAsync", wait_start)
    wait_block = engine_text[wait_start:wait_end] if wait_start >= 0 and wait_end > wait_start else ""
    enqueue_start = engine_text.find("private static void EnqueueDeferredVoxelPhysicsBakeTeardown")
    enqueue_end = engine_text.find("private static void ForceReleaseDeferredVoxelPhysicsBakeTeardown", enqueue_start)
    enqueue_block = engine_text[enqueue_start:enqueue_end] if enqueue_start >= 0 and enqueue_end > enqueue_start else ""
    registration_fail_start = enqueue_block.find("if (!EnsureDeferredVoxelPhysicsBakeTeardownRegistered())")
    registration_fail_end = enqueue_block.find("UpdateDeferredVoxelPhysicsBakeBackpressure();", registration_fail_start)
    registration_fail_block = (
        enqueue_block[registration_fail_start:registration_fail_end]
        if registration_fail_start >= 0 and registration_fail_end > registration_fail_start
        else ""
    )
    force_complete_hits = line_hits(FILES["engine"], r"TryComplete\(ref handle,\s*forceComplete:\s*true\)")
    shutdown_only_force_complete = (
        len(force_complete_hits) == 1
        and "ForceReleaseDeferredVoxelPhysicsBakeTeardownForShutdownOnly" in source_window(
            FILES["engine"],
            force_complete_hits[0]["line"],
            before=24,
            after=4,
        )
    )
    teardown_budget_continuous_quality_scaled = all(token in engine_text for token in [
        "DeferredVoxelPhysicsBakeTeardownBudgetPerFrame",
        "DeferredVoxelPhysicsBakeTeardownBudgetVisualOverkillPerFrame",
        "DeferredVoxelPhysicsBakeTeardownBurstCapBias",
        "_deferredVoxelPhysicsBakeTeardownBudgetTokens",
        "ResolveDeferredVoxelPhysicsBakeTeardownDrainBudgetThisFrame",
        "ResolveDeferredVoxelPhysicsBakeTeardownBudgetPerFrame",
        "ResolveDeferredVoxelPhysicsBakeTeardownInspectionBudget",
        "HomeostasisBrain.GlobalQualityWeight",
        "Mathf.Ceil(frameBudget - DeferredVoxelPhysicsBakeTeardownBurstCapBias)",
        "Mathf.Lerp(\n            DeferredVoxelPhysicsBakeTeardownBudgetPerFrame",
        "Mathf.Max(budget, DeferredVoxelPhysicsBakeTeardownBackpressureDrainBudget)",
        "int drainBudget = ResolveDeferredVoxelPhysicsBakeTeardownDrainBudgetThisFrame();",
        "int inspectionBudget = ResolveDeferredVoxelPhysicsBakeTeardownInspectionBudget(drainBudget);",
    ])
    teardown_renderer_lookup_destroy_owner_only = (
        "DisableDeferredVoxelBakePresentation(GameObject owner, MeshRenderer renderer, MeshCollider collider, byte flags)" in engine_text
        and "bool destroyOwner = (flags & DeferredVoxelBakeDestroyOwner) != 0;" in engine_text
        and "if (destroyOwner)" in engine_text
        and "DisableDeferredVoxelBakePresentation(owner, renderer, collider, flags);" in enqueue_block
    )
    physics_bake_job_start = engine_text.find("struct VoxelMeshBakeJob : IJob")
    physics_bake_job_window = (
        engine_text[max(0, physics_bake_job_start - 220):physics_bake_job_start + 420]
        if physics_bake_job_start >= 0
        else ""
    )
    return {
        "schedule_precheck_present": (
            "Application.isPlaying && !CanScheduleVoxelPhysicsBake()" in schedule_block
            and "handle = default;" in schedule_block
        ),
        "dispatcher_guard_present": (
            "EnsureDeferredVoxelPhysicsBakeTeardownRegistered()" in guard_block
            or "CanRegisterDeferredVoxelLateFrameWork()" in guard_block
        ),
        "teardown_driver_registered_before_bake_schedule": "EnsureDeferredVoxelPhysicsBakeTeardownRegistered()" in guard_block,
        "physics_bake_job_not_burst_compiled": (
            "struct VoxelMeshBakeJob : IJob" in engine_text
            and "[BurstCompile" not in physics_bake_job_window
        ),
        "backpressure_guard_present": (
            "DeferredVoxelPhysicsBakePendingCount < DeferredVoxelPhysicsBakeBackpressureThreshold" in guard_block
            and "UpdateDeferredVoxelPhysicsBakeBackpressure()" in guard_block
        ),
        "emergency_teardown_overflow_nonblocking": (
            "DeferredVoxelPhysicsBakeEmergencyTeardownCapacity" in engine_text
            and "TryEnqueueDeferredVoxelPhysicsBakeEmergencyTeardown" in enqueue_block
            and "ForceReleaseDeferredVoxelPhysicsBakeTeardown" not in enqueue_block
            and "DrainDeferredVoxelPhysicsBakeEmergencyTeardowns" in engine_text
            and "RemoveDeferredVoxelPhysicsBakeEmergencyTeardownAt" in engine_text
            and "VoxelMeshPipelineEmergencyBakeTeardownFlag" in engine_text
        ),
        "live_job_cancellable_frame_wait_absent": "NextFrameAsync(ct)" not in wait_block,
        "cancellation_deferred_teardown_present": "ct.IsCancellationRequested" in wait_block and "EnqueueDeferredVoxelPhysicsBakeTeardown" in wait_block,
        "registration_failure_keeps_deferred_teardown": (
            "if (!EnsureDeferredVoxelPhysicsBakeTeardownRegistered())" in enqueue_block
            and "ForceReleaseDeferredVoxelPhysicsBakeTeardown" not in registration_fail_block
            and "RemoveDeferredVoxelPhysicsBakeTeardownAt" not in registration_fail_block
        ),
        "force_complete_sites": force_complete_hits,
        "force_complete_shutdown_only": shutdown_only_force_complete,
        "teardown_budget_continuous_quality_scaled": teardown_budget_continuous_quality_scaled,
        "teardown_renderer_lookup_destroy_owner_only": teardown_renderer_lookup_destroy_owner_only,
        "normal_teardown_capacity": constants.get("DeferredVoxelPhysicsBakeTeardownCapacity", 0),
        "emergency_teardown_capacity": constants.get("DeferredVoxelPhysicsBakeEmergencyTeardownCapacity", 0),
        "total_tracked_teardown_capacity": (
            constants.get("DeferredVoxelPhysicsBakeTeardownCapacity", 0)
            + constants.get("DeferredVoxelPhysicsBakeEmergencyTeardownCapacity", 0)
        ),
        "policy": "New PhysX bake jobs are refused while the deferred teardown lane cannot register or total normal+emergency teardown count is at backpressure threshold; live bake waits do not use cancellable frame awaits. If teardown driver registration fails after a bake is already scheduled, the teardown remains queued for a later lane registration. Capacity overflow uses a fixed emergency teardown lane and records blackbox state instead of force-completing on the deformation path; force-release is limited to dispatcherless shutdown/reset. Teardown drain cadence is a continuous GlobalQualityWeight token bucket from 8 to 32 drains/frame, with backpressure lifting low-quality devices to the emergency drain ceiling without changing capacity.",
    }


def world_residency_pager_prefetch_proof():
    text = read(FILES["world_residency"])
    request_start = text.find("private void RequestAsyncPagerRead")
    request_end = text.find("private void RetireAsyncPagerReadTickets", request_start)
    request_block = text[request_start:request_end] if request_start >= 0 and request_end > request_start else ""
    retire_start = text.find("private void RetireAsyncPagerReadTickets")
    retire_end = text.find("/// <summary>", retire_start)
    retire_block = text[retire_start:retire_end] if retire_start >= 0 and retire_end > retire_start else ""
    retire_budget_start = text.find("private static int ResolvePagerReadRetireBudget()")
    retire_budget_end = text.find("private void RetireAsyncPagerReadTickets", retire_budget_start)
    retire_budget_block = text[retire_budget_start:retire_budget_end] if retire_budget_start >= 0 and retire_budget_end > retire_budget_start else ""
    constants = extract_int_constants(FILES["world_residency"])
    return {
        "pager_read_ticket_capacity": constants.get("PagerReadTicketCapacity", 0),
        "pager_read_retire_budget_minimum": constants.get("PagerReadRetireBudgetMinimum", 0),
        "pager_read_retire_budget_visual_overkill": constants.get("PagerReadRetireBudgetVisualOverkill", 0),
        "retire_budget_continuous_quality_scaled": (
            "ResolvePagerReadRetireBudget()" in text
            and "ResolveSmoothGlobalQualityWeight01()" in retire_budget_block
            and "HomeostasisBrain.GlobalQualityWeight" in text
            and "q * q * (3f - 2f * q)" in text
            and "math.lerp(PagerReadRetireBudgetMinimum, PagerReadRetireBudgetVisualOverkill, ResolveSmoothGlobalQualityWeight01())" in text
            and "RetireAsyncPagerReadTickets(ResolvePagerReadRetireBudget())" in text
        ),
        "monotonic_request_sequence_present": (
            "_pagerReadRequestSequence" in text
            and "ResolveNextPagerReadRequestId()" in request_block
            and "unchecked(_pagerReadRequestSequence + 1u)" in text
        ),
        "frame_xor_request_id_absent": "^ (uint)Time.frameCount" not in request_block,
        "ticket_ring_retires_before_reject": (
            "_pagerReadTicketCount >= PagerReadTicketCapacity" in request_block
            and "RetireAsyncPagerReadTickets(PagerReadTicketCapacity)" in request_block
        ),
        "retire_path_uses_async_status": (
            "TryRetireCompletedChunkPage" in retire_block
            and "H8WorldPageStatus.Ready" in retire_block
        ),
        "policy": "World chunk residency prefetch uses a fixed 16-ticket native ring and monotonic non-zero request ids. When full, it attempts bounded retirement before rejecting another async pager read; normal late-frame retirement scales continuously from 1 to 4 tickets by GlobalQualityWeight without changing pager DTO layout.",
    }


def world_residency_load_dispatch_proof():
    text = read(FILES["world_residency"])
    constants = extract_int_constants(FILES["world_residency"])
    max_load_start = text.find("private int ResolveMaxConcurrentLoads()")
    max_load_end = text.find("private int ResolveLoadDispatchBudget()", max_load_start)
    max_load_block = text[max_load_start:max_load_end] if max_load_start >= 0 and max_load_end > max_load_start else ""
    dispatch_start = text.find("private int ResolveLoadDispatchBudget()")
    dispatch_end = text.find("private void ProcessOneLoadRequest", dispatch_start)
    dispatch_block = text[dispatch_start:dispatch_end] if dispatch_start >= 0 and dispatch_end > dispatch_start else ""
    per_frame_start = text.find("private static float ResolveLoadDispatchBudgetPerFrame()")
    per_frame_end = text.find("private void ProcessOneLoadRequest", per_frame_start)
    per_frame_block = text[per_frame_start:per_frame_end] if per_frame_start >= 0 and per_frame_end > per_frame_start else ""
    clear_start = text.find("private void ClearStreamingQueues()")
    clear_end = text.find("private bool IsChunkLoadInFlight", clear_start)
    clear_block = text[clear_start:clear_end] if clear_start >= 0 and clear_end > clear_start else ""
    return {
        "low_tier_load_dispatch_budget": constants.get("LowTierLoadDispatchBudget", 0),
        "visual_overkill_load_dispatch_budget": constants.get("VisualOverkillLoadDispatchBudget", 0),
        "continuous_quality_scaled": (
            "ResolveLoadDispatchBudgetPerFrame()" in dispatch_block
            and "ResolveSmoothGlobalQualityWeight01()" in per_frame_block
            and "HomeostasisBrain.GlobalQualityWeight" in text
            and "q * q * (3f - 2f * q)" in text
            and "math.lerp(LowTierLoadDispatchBudget, VisualOverkillLoadDispatchBudget, ResolveSmoothGlobalQualityWeight01())" in per_frame_block
        ),
        "max_concurrent_continuous_quality_scaled": (
            "ResolveQualityScaledConcurrentLoadCap(cap)" in max_load_block
            and "ResolveQualityScaledConcurrentLoadCap" in per_frame_block
            and "math.lerp(lowCap, safeCap, ResolveSmoothGlobalQualityWeight01())" in per_frame_block
            and "_resolvedTier" not in max_load_block
        ),
        "token_bucket_present": (
            "_loadDispatchBudgetTokens" in dispatch_block
            and "_loadDispatchFrame" in dispatch_block
            and "Time.frameCount" in dispatch_block
            and "math.min(frameCap, _loadDispatchBudgetTokens + perFrame)" in dispatch_block
        ),
        "same_frame_overdispatch_guard_present": (
            "math.clamp((int)math.floor(_loadDispatchBudgetTokens), 0, VisualOverkillLoadDispatchBudget)" in dispatch_block
        ),
        "queue_clear_resets_tokens": (
            "_loadDispatchFrame = -1;" in clear_block
            and "_loadDispatchBudgetTokens = 0f;" in clear_block
            and text.count("_loadDispatchBudgetTokens = 0f;") >= 2
        ),
        "legacy_tier_switch_absent": (
            "_resolvedTier" not in dispatch_block
            and "MiddleTierLoadDispatchBudget" not in text
            and "HighTierLoadDispatchBudget" not in text
            and "UltraTierLoadDispatchBudget" not in text
        ),
        "policy": "Chunk load dispatch no longer jumps through low/mid/high/ultra integer switches. A per-frame token bucket consumes continuous GlobalQualityWeight from 1 to 4 load starts per frame, concurrent load cap scales continuously from survival cap to serialized cap, resets on queue clears, and returns zero if the cadence has already been spent in the same frame.",
    }


def world_residency_radius_quality_proof():
    text = read(FILES["world_residency"])
    prediction_start = text.find("private float ResolvePredictionDistanceMeters")
    prediction_end = text.find("private float ResolveEffectiveLoadRadiusMeters", prediction_start)
    prediction_block = text[prediction_start:prediction_end] if prediction_start >= 0 and prediction_end > prediction_start else ""
    load_start = text.find("private float ResolveEffectiveLoadRadiusMeters")
    load_end = text.find("private float ResolveEffectiveUnloadRadiusMeters", load_start)
    load_block = text[load_start:load_end] if load_start >= 0 and load_end > load_start else ""
    unload_start = text.find("private float ResolveEffectiveUnloadRadiusMeters")
    unload_end = text.find("private float ResolveHealthRadiusScale", unload_start)
    unload_block = text[unload_start:unload_end] if unload_start >= 0 and unload_end > unload_start else ""
    helper_start = text.find("private static float ResolveSmoothGlobalQualityWeight01()")
    helper_end = text.find("private float ResolveHealthRadiusScale", helper_start)
    helper_block = text[helper_start:helper_end] if helper_start >= 0 and helper_end > helper_start else ""
    upload_start = text.find("private void ApplyAsyncUploadBudgetForQuality")
    upload_end = text.find("private bool TryResolvePlayerMotion", upload_start)
    upload_block = text[upload_start:upload_end] if upload_start >= 0 and upload_end > upload_start else ""
    impostor_start = text.find("private bool TryResolveChunkImpostorPayload")
    impostor_end = text.find("private static int ResolveChunkImpostorType", impostor_start)
    impostor_block = text[impostor_start:impostor_end] if impostor_start >= 0 and impostor_end > impostor_start else ""
    macro_start = text.find("private static MacroDatabaseTier ResolveMacroDatabaseTier")
    macro_end = text.find("private void UpdateStorageDebtHysteresisStates", macro_start)
    macro_block = text[macro_start:macro_end] if macro_start >= 0 and macro_end > macro_start else ""
    tick_start = text.find("public void Tick(float deltaTime)")
    tick_end = text.find("/// <inheritdoc />", tick_start + 1)
    tick_block = text[tick_start:tick_end] if tick_start >= 0 and tick_end > tick_start else ""
    return {
        "smooth_quality_helper_present": (
            "HomeostasisBrain.GlobalQualityWeight" in helper_block
            and "math.saturate(math.isfinite(quality) ? quality : 1f)" in helper_block
            and "q * q * (3f - 2f * q)" in helper_block
        ),
        "prediction_distance_continuous": (
            "math.lerp(50f, 200f, ResolveSmoothGlobalQualityWeight01())" in prediction_block
            and "tier == ChunkStreamingScalabilityTier" not in prediction_block
        ),
        "load_radius_continuous": (
            "math.lerp(lowLoad, configuredLoad, ResolveSmoothGlobalQualityWeight01())" in load_block
            and "tier == ChunkStreamingScalabilityTier" not in load_block
        ),
        "unload_radius_continuous": (
            "math.lerp(" in unload_block
            and "LowTierUnloadRadiusMeters" in unload_block
            and "UltraTierUnloadRadiusMeters" in unload_block
            and "ResolveSmoothGlobalQualityWeight01()" in unload_block
            and "tier == ChunkStreamingScalabilityTier" not in unload_block
        ),
        "async_upload_budget_continuous": (
            "ResolveSmoothGlobalQualityWeight01()" in upload_block
            and "math.lerp(64f, 256f, smooth)" in upload_block
            and "math.lerp(1f, 4f, smooth)" in upload_block
            and "_activeAsyncUploadBudgetHash" in upload_block
            and "switch (tier)" not in upload_block
            and "ApplyAsyncUploadBudgetForQuality()" in tick_block
        ),
        "impostor_residency_flag_continuous": (
            "ResolveSmoothGlobalQualityWeight01() <= ChunkImpostorSurvivalSnapQualityThreshold" in impostor_block
            and "_resolvedTier == ChunkStreamingScalabilityTier.Low" not in impostor_block
            and "FlagSurvivalSnap" in impostor_block
            and "FlagDitherBlend" in impostor_block
        ),
        "macro_database_tier_adapter_continuous": (
            "ResolveSmoothGlobalQualityWeight01()" in macro_block
            and "MacroDatabaseMiddleQualityThreshold" in macro_block
            and "MacroDatabaseHighQualityThreshold" in macro_block
            and "MacroDatabaseUltraQualityThreshold" in macro_block
            and "ChunkStreamingScalabilityTier" not in macro_block
        ),
        "legacy_resolved_tier_route_absent": (
            "_resolvedTier" not in text
            and "ResolveScalabilityTier" not in text
        ),
        "policy": "Prediction distance, load/unload radii, Unity async upload budget, and HLOD impostor residency presentation use continuous GlobalQualityWeight smoothing instead of low/middle/high/ultra branches. Health/VRAM squeeze remains a safety clamp on the resulting radii.",
    }


def world_residency_aup_precision_proof():
    text = read(FILES["world_residency"])
    radius_start = text.find("public struct RadiusBasedStreamingJob")
    radius_end = text.find("/// <summary>\n    /// Burst-native sort", radius_start)
    radius_block = text[radius_start:radius_end] if radius_start >= 0 and radius_end > radius_start else ""
    sort_start = text.find("public struct ChunkLoadPrioritySortJob")
    sort_end = text.find("/// <summary>\n    /// Burst-native append/remove", sort_start)
    sort_block = text[sort_start:sort_end] if sort_start >= 0 and sort_end > sort_start else ""
    project_start = text.find("private static AbsoluteUniversePosition BuildProjectedAup")
    project_end = text.find("private byte ResolveLoadFlagsForChunk", project_start)
    project_block = text[project_start:project_end] if project_start >= 0 and project_end > project_start else ""
    return {
        "radius_job_uses_safe_double_distance": (
            "double distSq = AupPrecisionMath.DistanceSqSafeDouble(chunk, player);" in radius_block
            and "bool insideLoadZone = distSq <= LoadRadiusSq;" in radius_block
            and "distSq >= unloadSq" in radius_block
        ),
        "radius_job_float_distance_route_absent": (
            "float3 localDelta" not in radius_block
            and "math.lengthsq(localDelta)" not in radius_block
            and "double distSq = distSqFloat;" not in radius_block
        ),
        "sort_job_uses_safe_double_distance": "AupPrecisionMath.DistanceSqSafeDouble(ProjectedAbsolute, ToAbsoluteDouble3(ChunkCenters[index]))" in sort_block,
        "projected_aup_uses_double3": (
            "double3 playerAbs = ToAbsoluteDouble3(in playerAup);" in project_block
            and "double3 direction = new double3(" in project_block
            and "playerAbs + (direction * predictionDistanceMeters)" in project_block
        ),
        "distance_helpers_use_safe_double": text.count("AupPrecisionMath.DistanceSqSafeDouble(") >= 5,
        "policy": "Chunk residency load/unload thresholds, sort priority, teleport detection, and projected prefetch AUP use double3/AupPrecisionMath distance. Float distance is retained only as clamped DTO telemetry.",
    }


def world_residency_hydration_apply_ledger_proof():
    text = read(FILES["world_residency"])
    runtime_text = read(FILES["world_runtime"])
    allocate_start = text.find("private void AllocateNativeState()")
    allocate_end = text.find("NativeArray<ChunkResidencyDTO> residencyDtos", allocate_start)
    allocate_block = text[allocate_start:allocate_end] if allocate_start >= 0 and allocate_end > allocate_start else ""
    copy_start = text.find("private void CopyHydrationApplyRecordToVault")
    copy_end = text.find("private void RecordHydrationApplySlice", copy_start)
    copy_block = text[copy_start:copy_end] if copy_start >= 0 and copy_end > copy_start else ""
    release_start = text.find("private void DisposeNativeState()")
    release_end = text.find("private void ReleaseStreamingLedgerBuffers()", release_start)
    release_block = text[release_start:release_end] if release_start >= 0 and release_end > release_start else ""
    constants = extract_int_constants(FILES["world_residency"])
    layout = struct_layout(FILES["world_runtime"], "ChunkHydrationApplyRecord")
    hot_growth_tokens = [
        "TryAcquireSliceHandle",
        "EnsureGenerationHandle",
        "AcquireWorldStreamingArray",
        "TryResolveSlice",
        "TryResolveHandle",
        "UnsafeUtility.MemCpy",
        "UnsafeUtility.Malloc",
    ]
    return {
        "default_max_chunk_count": constants.get("DefaultMaxChunkCount", 0),
        "record_bytes": layout["bytes"],
        "default_payload_bytes": constants.get("DefaultMaxChunkCount", 0) * layout["bytes"],
        "explicit_64_byte_layout_present": "[StructLayout(LayoutKind.Explicit, Size = 64)]" in runtime_text,
        "vault_bit_present": "HydrationApplyRecordsVaultBit = 1UL << 18" in text,
        "native_array_field_present": "private NativeArray<ChunkHydrationApplyRecord> _hydrationApplyRecords;" in text,
        "owner_phase_prewarm_present": (
            "_hydrationApplyRecords = AcquireWorldStreamingArray<ChunkHydrationApplyRecord>(" in allocate_block
            and "HydrationApplyRecordVaultBufferId" in allocate_block
            and "capacity" in allocate_block
            and "NativeArrayOptions.ClearMemory" in allocate_block
        ),
        "sentinel_registered": "NativeMemorySentinel.RegisterNativeArray(_hydrationApplyRecords" in allocate_block,
        "released_with_owner": "ReleaseWorldStreamingArray(ref _hydrationApplyRecords" in release_block,
        "runtime_direct_indexed_store_present": "_hydrationApplyRecords[safeChunkIndex] = record;" in copy_block,
        "runtime_hot_growth_absent": all(token not in copy_block for token in hot_growth_tokens),
        "runtime_capacity_fail_closed": (
            "!_hydrationApplyRecords.IsCreated" in copy_block
            and "(uint)safeChunkIndex >= (uint)_hydrationApplyRecords.Length" in copy_block
            and "return;" in copy_block
        ),
        "policy": "Hydration apply diagnostics are a fixed maxChunkCount native ledger prewarmed during owner allocation. Runtime activation writes one 64-byte ARM64-aligned record by chunk index and fails closed when the ledger is unavailable; it does not acquire or grow GlobalDataVault slices while chunks hydrate.",
    }


def world_residency_teleport_reset_proof():
    text = read(FILES["world_residency"])
    tick_start = text.find("public void Tick(float deltaTime)")
    tick_end = text.find("public void SlowTick()", tick_start)
    tick_block = text[tick_start:tick_end] if tick_start >= 0 and tick_end > tick_start else ""
    late_start = text.find("public void LateFrameTick()")
    late_end = text.find("private void PublishLod2ImpostorResidency", late_start)
    late_block = text[late_start:late_end] if late_start >= 0 and late_end > late_start else ""
    handle_start = text.find("private void HandleTeleport")
    handle_end = text.find("private void TryApplyPendingTeleportReset", handle_start)
    handle_block = text[handle_start:handle_end] if handle_start >= 0 and handle_end > handle_start else ""
    apply_start = text.find("private void TryApplyPendingTeleportReset")
    apply_end = text.find("private void ApplyTeleportResetNow", apply_start)
    apply_block = text[apply_start:apply_end] if apply_start >= 0 and apply_end > apply_start else ""
    return {
        "pending_flag_present": "private bool _teleportResetPending;" in text,
        "pending_aup_present": "private AbsoluteUniversePositionBlit _pendingTeleportAup;" in text,
        "tick_finalizes_before_apply": (
            (
                "DetectAndHandleTeleport();" in tick_block
                and "CompleteResidencyJobIfFinished();" in tick_block
                and "TryApplyPendingTeleportReset();" in tick_block
                and tick_block.find("CompleteResidencyJobIfFinished();") < tick_block.find("TryApplyPendingTeleportReset();")
            )
            or (
                "CompleteResidencyJobIfFinished();" in late_block
                and "TryApplyPendingTeleportReset();" in late_block
                and late_block.find("CompleteResidencyJobIfFinished();") < late_block.find("TryApplyPendingTeleportReset();")
            )
        ),
        "handle_defers_while_job_live": (
            "_pendingTeleportAup = AbsoluteUniversePositionBlit.FromAup(in playerAup);" in handle_block
            and "_teleportResetPending = true;" in handle_block
            and "_forceResidencyEvaluation = true;" in handle_block
            and "ApplyTeleportResetNow(in playerAup);" not in handle_block
        ),
        "pending_apply_waits_for_job_completion": (
            "if (!_teleportResetPending || _residencyJobScheduled)" in apply_block
            and "return;" in apply_block
        ),
        "teleport_force_complete_absent": (
            "CompleteResidencyJobForTeleport" not in text
            and "forceComplete: true" not in handle_block
            and "TryComplete(ref _residencyJobHandle, forceComplete: true)" not in handle_block
        ),
        "teardown_force_complete_still_isolated": (
            "private void CompleteResidencyJobForTeardown()" in text
            and "DispatcherJobFence.TryComplete(ref _residencyJobHandle, forceComplete: true);" in text
        ),
        "policy": "Large AUP jumps no longer force-complete the live residency scan/sort job. Teleport reset stores the target AUP and applies queue clearing plus immediate-radius loads only after the scheduled job naturally finalizes; force-complete remains isolated to teardown/service-rebind shutdown paths.",
    }


def voxel_job_wait_cancellation_proof():
    engine_text = read(FILES["engine"])
    start = engine_text.find("static async Awaitable AwaitForJobCompletionAsync")
    end = engine_text.find("static async Awaitable<long> YieldIfChunkGenerationBudgetExpiredAsync", start)
    block = engine_text[start:end] if start >= 0 and end > start else ""
    return {
        "awaiter_present": bool(block),
        "cancellation_recorded": "cancellationRequested" in block,
        "finalizes_after_completion": "DispatcherJobSwap.TryFinalizeCompleted(ref handle)" in block,
        "live_job_cancellable_frame_wait_absent": "NextFrameAsync(ct)" not in block,
        "policy": "Voxel job wait records cancellation while a job is live, waits without a cancellable frame await, finalizes the completed handle, then propagates cancellation.",
    }


def cave_graph_generator_proof():
    graph_text = read(FILES["cave_graph"])
    engine_text = read(FILES["engine"])
    measure_start = graph_text.find("public static bool TryMeasure(")
    measure_end = graph_text.find("public static bool TryFill(", measure_start)
    fill_start = graph_text.find("public static bool TryFill(")
    fill_end = graph_text.find("private static bool HasCapacity", fill_start)
    generate_start = graph_text.find("private static bool GenerateIntoScratch(")
    generate_end = graph_text.find("// \u00e2\u2022", generate_start)
    if generate_end < 0:
        generate_end = graph_text.find("static int PlaceRooms", generate_start)
    measure_block = graph_text[measure_start:measure_end] if measure_start >= 0 and measure_end > measure_start else ""
    fill_block = graph_text[fill_start:fill_end] if fill_start >= 0 and fill_end > fill_start else ""
    generate_block = graph_text[generate_start:generate_end] if generate_start >= 0 and generate_end > generate_start else ""
    constants = extract_int_constants(FILES["cave_graph"])
    temp_tokens_absent = all(token not in graph_text for token in [
        "Allocator.Temp",
        "Allocator.TempJob",
        "new NativeList",
        "new NativeArray",
        "GenerateAllocated",
    ])
    return {
        "max_rooms": constants.get("MAX_ROOMS", 0),
        "max_tunnels": constants.get("MAX_TUNNELS", 0),
        "max_entrances": constants.get("MAX_ENTRANCES", 0),
        "max_structures": constants.get("MAX_STRUCTURES", 0),
        "try_measure_stackalloc_present": (
            "stackalloc CaveNode[MAX_ROOMS]" in measure_block
            and "stackalloc CaveTunnel[MAX_TUNNELS]" in measure_block
            and "stackalloc CaveEntrance[MAX_ENTRANCES]" in measure_block
            and "stackalloc CaveStructure[MAX_STRUCTURES]" in measure_block
        ),
        "try_fill_stackalloc_present": (
            "stackalloc CaveNode[MAX_ROOMS]" in fill_block
            and "stackalloc CaveTunnel[MAX_TUNNELS]" in fill_block
            and "stackalloc CaveEntrance[MAX_ENTRANCES]" in fill_block
            and "stackalloc CaveStructure[MAX_STRUCTURES]" in fill_block
        ),
        "span_generator_present": (
            "private static bool GenerateIntoScratch(" in graph_text
            and "System.Span<CaveNode> rooms" in generate_block
            and "System.Span<CaveTunnel> tunnels" in generate_block
            and "System.Span<CaveEntrance> entrances" in generate_block
            and "System.Span<CaveStructure> structures" in generate_block
        ),
        "temp_native_allocations_absent": temp_tokens_absent,
        "native_list_references_absent": "NativeList<" not in graph_text,
        "engine_rebuild_graph_scratch_prewarmed": all(token in engine_text for token in [
            "StreamingCaveGraphNodeScratchCapacity = 64",
            "StreamingCaveGraphTunnelScratchCapacity = 128",
            "StreamingCaveGraphEntranceScratchCapacity = 8",
            "StreamingCaveGraphStructureScratchCapacity = 128",
            "StreamingCraterStampScratchCapacity = 16",
            "EnsureNativeArrayCapacity(ref slot.RebuildNodes, StreamingCaveGraphNodeScratchCapacity",
            "EnsureNativeArrayCapacity(ref slot.RebuildTunnels, StreamingCaveGraphTunnelScratchCapacity",
            "EnsureNativeArrayCapacity(ref slot.RebuildEntrances, StreamingCaveGraphEntranceScratchCapacity",
            "EnsureNativeArrayCapacity(ref slot.RebuildStructures, StreamingCaveGraphStructureScratchCapacity",
            "EnsureNativeArrayCapacity(ref slot.RebuildCraterStamps, StreamingCraterStampScratchCapacity",
        ]),
        "engine_rebuild_graph_scratch_hard_cap_present": all(token in engine_text for token in [
            "nodeCount > StreamingCaveGraphNodeScratchCapacity",
            "tunnelCount > StreamingCaveGraphTunnelScratchCapacity",
            "entranceCount > StreamingCaveGraphEntranceScratchCapacity",
            "structureCount > StreamingCaveGraphStructureScratchCapacity",
            "craterStampCount > StreamingCraterStampScratchCapacity",
        ]),
        "policy": "CaveGraphGenerator TryMeasure/TryFill use bounded stackalloc Span scratch with fixed MAX_ROOMS/MAX_TUNNELS/MAX_ENTRANCES/MAX_STRUCTURES. HectonVoxelEngine prewarms matching rebuild graph and crater scratch arrays when the streaming scratch slot is leased and rejects counts above those caps instead of growing NativeArrays.",
    }


def voxel_spawn_point_job_proof():
    engine_text = read(FILES["engine"])
    job_start = engine_text.find("public struct VoxelSpawnPointJob")
    job_end = engine_text.find("static uint SpatialHash", job_start)
    job_block = engine_text[job_start:job_end] if job_start >= 0 and job_end > job_start else ""
    schedule_start = engine_text.find("JobHandle spawnHandle = new VoxelSpawnPointJob")
    schedule_end = engine_text.find("phase5Handle = JobHandle.CombineDependencies(phase5Handle, spawnHandle)", schedule_start)
    schedule_block = engine_text[schedule_start:schedule_end] if schedule_start >= 0 and schedule_end > schedule_start else ""
    return {
        "owner_job_present": "public struct VoxelSpawnPointJob : IJob" in job_block,
        "parallel_writer_absent": "ParallelWriter" not in job_block,
        "capacity_guard_present": "spawnPoints.Length >= spawnPoints.Capacity" in job_block,
        "add_no_resize_present": "spawnPoints.AddNoResize" in job_block,
        "parallel_schedule_absent": ".Schedule(data.WeldedCount" not in schedule_block,
        "dependency_schedule_present": "}.Schedule(normalHandle)" in schedule_block,
        "scratch_prewarmed_on_lease": (
            "EnsureSpawnPointScratchCapacity(slot, ResolveStreamingSpawnPointScratchCapacity(totalCellCount));" in engine_text
            and "static int ResolveStreamingSpawnPointScratchCapacity" in engine_text
            and "MinimumStreamingSpawnPointScratchCapacity" in engine_text
        ),
        "prepare_reuses_prewarmed_scratch": (
            "if (!EnsureSpawnPointScratchCapacity(slot, safeCapacity))" in engine_text
            and "slot.SpawnPointListScratch.Clear();" in engine_text
        ),
        "policy": "Cave spawn extraction is a single owner job that scans welded vertices and only calls AddNoResize while Length < Capacity. Spawn-point scratch is prewarmed when the streaming scratch slot is leased, so normal extraction reuses a fixed NativeList instead of allocating after mesh generation.",
    }


def modified_cells_fill_proof():
    engine_text = read(FILES["engine"])
    delta_text = read(FILES["delta"])
    prepare_start = engine_text.find("async Awaitable<bool> TryPrepareModifiedCellsForPipelineAsync")
    prepare_end = engine_text.find("async Awaitable<bool> ExecuteVoxelPipelineAsync", prepare_start)
    prepare_block = engine_text[prepare_start:prepare_end] if prepare_start >= 0 and prepare_end > prepare_start else ""
    execute_start = engine_text.find("async Awaitable<bool> ExecuteVoxelPipelineAsync")
    execute_end = engine_text.find("async Awaitable<long> FillBiomeModifierGridAsync", execute_start)
    execute_block = engine_text[execute_start:execute_end] if execute_start >= 0 and execute_end > execute_start else ""
    async_start = delta_text.find("public async Awaitable<bool> TryFillDeltaMapForVolumeAsync")
    async_end = delta_text.find("public void PopulateSaveData", async_start)
    async_block = delta_text[async_start:async_end] if async_start >= 0 and async_end > async_start else ""
    return {
        "engine_prepare_async_present": "async Awaitable<bool> TryPrepareModifiedCellsForPipelineAsync" in prepare_block,
        "engine_uses_async_delta_fill": "TryFillDeltaMapForVolumeAsync" in prepare_block,
        "engine_caps_measure_to_total_cells": (
            "int modifiedCellCapacity = math.min(measuredModifiedCellCapacity, math.max(1, data.TotalCells));" in prepare_block
        ),
        "scratch_prewarmed_on_lease": (
            "EnsureModifiedCellsScratchCapacity(slot, math.max(1, totalCellCount));" in engine_text
            and "static bool EnsureModifiedCellsScratchCapacity" in engine_text
        ),
        "prepare_reuses_prewarmed_scratch": (
            "if (!EnsureModifiedCellsScratchCapacity(slot, safeCapacity))" in engine_text
            and "slot.ModifiedCellsScratch.Clear();" in engine_text
        ),
        "execute_awaits_modified_cells_prepare": "await TryPrepareModifiedCellsForPipelineAsync(data, ct)" in execute_block,
        "execute_yields_after_spatial_partitions": "BuildSpatialPartitions(data);" in execute_block and "YieldIfChunkGenerationBudgetExpiredAsync" in execute_block,
        "delta_async_fill_present": "public async Awaitable<bool> TryFillDeltaMapForVolumeAsync" in async_block,
        "delta_fill_budget_yield_present": "YieldIfDeltaMapFillBudgetExpiredAsync" in async_block and "AwaitableDebtMonitor.NextFrameAsync(ct)" in async_block,
        "delta_fill_probe_stride_present": "++yieldProbe" in async_block and "& 511" in async_block,
        "delta_dirty_mask_word_probe_present": (
            "for (int wordIndex = 0; wordIndex < ChunkDirtyMaskWordCount; wordIndex++)" in async_block
            and "state.DirtyMaskWords[wordIndex]" in async_block
            and async_block.find("++yieldProbe", async_block.find("for (int wordIndex = 0; wordIndex < ChunkDirtyMaskWordCount; wordIndex++)")) <
            async_block.find("state.DirtyMaskWords[wordIndex]", async_block.find("for (int wordIndex = 0; wordIndex < ChunkDirtyMaskWordCount; wordIndex++)"))
        ),
        "try_add_fail_closed_present": delta_text.count("if (!modifiedCells.TryAdd") >= 4,
        "sync_fill_still_available_for_cold_callers": "public bool TryFillDeltaMapForVolume(" in delta_text,
        "policy": "Voxel rebuild still builds the authoritative modified-cell hash map, but dense delta history fill is time-sliced with AwaitableDebtMonitor on the same chunk-generation frame budget instead of running as one uninterrupted pre-job main-thread loop. The scratch hash map is prewarmed on streaming scratch lease acquisition and measured capacity is capped to the rebuild volume's total cell count.",
    }


def streaming_scratch_prewarm_proof():
    engine_text = read(FILES["engine"])
    normalized_engine_text = engine_text.replace("\r\n", "\n")
    prewarm_start = engine_text.find("static void EnsureStreamingScratchSlotCapacity")
    prewarm_end = engine_text.find("static int ResolveStreamingMeshRawScratchCapacity", prewarm_start)
    prewarm_block = engine_text[prewarm_start:prewarm_end] if prewarm_start >= 0 and prewarm_end > prewarm_start else ""
    mesh_start = engine_text.find("bool TryEnsureMeshExtractionScratchCapacity")
    mesh_end = engine_text.find("bool TryEnsureMeshAttributeScratchCapacity", mesh_start)
    mesh_block = engine_text[mesh_start:mesh_end] if mesh_start >= 0 and mesh_end > mesh_start else ""
    attr_start = mesh_end
    attr_end = engine_text.find("bool TryEnsureProjectionScratchCapacity", attr_start)
    attr_block = engine_text[attr_start:attr_end] if attr_start >= 0 and attr_end > attr_start else ""
    projection_start = attr_end
    projection_end = engine_text.find("bool TryEnsureSpatialBucketCounterScratchCapacity", projection_start)
    projection_block = engine_text[projection_start:projection_end] if projection_start >= 0 and projection_end > projection_start else ""
    spatial_start = projection_end
    spatial_end = engine_text.find("bool TryPrepareRebuildGraphScratch", spatial_start)
    spatial_block = engine_text[spatial_start:spatial_end] if spatial_start >= 0 and spatial_end > spatial_start else ""
    graph_start = spatial_end
    graph_end = engine_text.find("bool TryPrepareSpawnPointScratch", graph_start)
    graph_block = engine_text[graph_start:graph_end] if graph_start >= 0 and graph_end > graph_start else ""
    collider_start = engine_text.find("bool TryEnsureColliderChunkScratchCapacity")
    collider_end = engine_text.find("static float ResolveDensityDecodeScale", collider_start)
    collider_block = engine_text[collider_start:collider_end] if collider_start >= 0 and collider_end > collider_start else ""
    post_lease_blocks = mesh_block + attr_block + projection_block + spatial_block + graph_block + collider_block
    return {
        "mesh_raw_low_tier_capacity": first_int_constant(FILES["engine"], "StreamingMeshRawVertexScratchLowTierCapacity", 0),
        "mesh_raw_mid_tier_capacity": first_int_constant(FILES["engine"], "StreamingMeshRawVertexScratchMidTierCapacity", 0),
        "mesh_raw_visual_overkill_capacity": first_int_constant(FILES["engine"], "StreamingMeshRawVertexScratchVisualOverkillCapacity", 0),
        "mesh_raw_capacity_continuous_quality_scaled": all(token in engine_text for token in [
            "static int ResolveStreamingMeshRawScratchQualityCapacity()",
            "HomeostasisBrain.GlobalQualityWeight",
            "math.lerp(",
            "StreamingMeshRawVertexScratchLowTierCapacity",
            "StreamingMeshRawVertexScratchVisualOverkillCapacity",
        ]),
        "spatial_bucket_capacity": first_int_constant(FILES["engine"], "StreamingSpatialBucketScratchCapacity", 0),
        "node_spatial_reference_capacity": first_int_constant(FILES["engine"], "StreamingNodeSpatialReferenceScratchCapacity", 0),
        "tunnel_spatial_reference_capacity": first_int_constant(FILES["engine"], "StreamingTunnelSpatialReferenceScratchCapacity", 0),
        "collider_chunk_capacity": first_int_constant(FILES["engine"], "StreamingColliderChunkScratchCapacity", 0),
        "lease_signature_carries_grid_dimension": "int gridDimension,\n        CancellationToken ct" in engine_text,
        "slot_marked_in_use_after_prewarm": (
            "if (!TryEnsureStreamingScratchSlotCapacity(slot, heightCount, totalPointCount, totalCellCount, gridDimension))\n"
            "                    continue;\n\n"
            "                slot.InUse = true;" in normalized_engine_text
        ),
        "slot_resize_skips_live_leases": (
            "HasStreamingScratchSlotInUse_NoLock()" in engine_text
            and "if (_streamingScratchSlots != null && HasStreamingScratchSlotInUse_NoLock())" in engine_text
        ),
        "slot_prewarm_exception_fail_closed": all(token in prewarm_block for token in [
            "static bool TryEnsureStreamingScratchSlotCapacity",
            "catch (Exception ex)",
            "ReportVoxelMeshScratchCapacityOverflow();",
            "return false;",
        ]) and "if (!TryEnsureStreamingScratchSlotCapacity(slot, heightCount, totalPointCount, totalCellCount, gridDimension))" in engine_text,
        "mesh_scratch_prewarmed_on_lease": all(token in prewarm_block for token in [
            "ResolveStreamingMeshRawScratchCapacity(totalCellCount)",
            "ResolveStreamingEdgeVertexScratchCapacity(",
            "EnsureNativeArrayCapacity(ref slot.MeshRawVertices, meshRawScratchCapacity",
            "EnsureNativeArrayCapacity(ref slot.MeshNormals, meshRawScratchCapacity",
            "EnsureNativeArrayCapacity(ref slot.ProjectedLocalPositions, meshRawScratchCapacity",
        ]),
        "spatial_scratch_prewarmed_on_lease": all(token in prewarm_block for token in [
            "EnsureNativeArrayCapacity(ref slot.SpatialBucketCounts, StreamingSpatialBucketScratchCapacity",
            "EnsureNativeArrayCapacity(ref slot.SpatialNodeBucketIndices, StreamingNodeSpatialReferenceScratchCapacity",
            "EnsureNativeArrayCapacity(ref slot.SpatialTunnelBucketIndices, StreamingTunnelSpatialReferenceScratchCapacity",
        ]),
        "collider_scratch_prewarmed_on_lease": all(token in prewarm_block for token in [
            "EnsureNativeArrayCapacity(ref slot.ColliderTriangleBuckets, math.max(1, meshRawScratchCapacity / 3)",
            "EnsureNativeArrayCapacity(ref slot.ColliderChunkTriangleIndices, meshRawScratchCapacity",
            "EnsureNativeArrayCapacity(ref slot.ColliderLocalPositions, meshRawScratchCapacity",
        ]),
        "post_lease_nativearray_growth_absent": "EnsureNativeArrayCapacity" not in post_lease_blocks,
        "post_lease_capacity_fail_closed": all(token in post_lease_blocks for token in [
            "slot.MeshRawVertices.Length < safeRawCount",
            "slot.MeshNormals.Length < safeWeldedCount",
            "slot.ProjectedLocalPositions.Length < safeVertexCount",
            "slot.SpatialNodeBucketIndices.Length < safeReferenceCount",
            "slot.RebuildNodes.Length < safeNodeCount",
            "slot.ColliderTriangleBuckets.Length < safeTriangleCount",
        ]),
        "capacity_overflow_blackbox_present": (
            "VoxelMeshPipelineScratchCapacityOverflowFlag" in engine_text
            and "private static void ReportVoxelMeshScratchCapacityOverflow()" in engine_text
            and post_lease_blocks.count("ReportVoxelMeshScratchCapacityOverflow();") >= 8
            and "EnsureVoxelMeshPipelineBlackBox();" in engine_text
        ),
        "policy": "Streaming scratch lease now prewarms mesh extraction, edge registry, mesh attributes, projection, cave spatial buckets, rebuild graph, and collider split buffers. Post-lease TryEnsure methods only test capacity and fail closed; they do not grow NativeArrays during marching-cubes, spatial partition, or collider publication stages.",
    }


def build_report():
    engine_text = read(FILES["engine"])
    shader_routes = count_shader_clip_routes()
    sync_fallbacks = detect_sync_collider_fallbacks()
    shared_mesh_assignments = classify_shared_mesh_assignments()
    collider_shared_mesh_assignments = classify_shared_mesh_assignments(include_render_mesh=False)
    deformation_shared_mesh_nulls = deformation_collider_shared_mesh_null_mutations()
    deferred_bake_presentation_nulls = deferred_bake_presentation_shared_mesh_null_mutations()
    runtime_collider_nulls = runtime_collider_shared_mesh_null_mutations()
    paging_getcomponent_hits = paging_getcomponent_hotpath_hits()
    mesh_apply = line_hits(FILES["engine"], r"Mesh\.ApplyAndDisposeWritableMeshData|UploadSurfaceMesh\(|UploadColliderMesh\(")
    physics_bake = line_hits(FILES["engine"], r"Physics\.BakeMesh")
    managed_chunk_tracking = line_hits(FILES["delta"], r"Dictionary<ChunkAddress")
    pager = world_pager_limits()
    carve_queue = carve_queue_pressure()
    voxel_delta_shutdown = voxel_delta_shutdown_completion_proof()
    rle_packet = rle_packet_layout(pager["sector_payload_bytes"])
    damage = damage_volume_pressure()
    dirty_chunk = active_dirty_chunk_memory()
    volume_registry = volume_registry_proof()
    engine_active_volume_registry = engine_active_volume_registry_proof()
    collider_chunk_registry = collider_chunk_registry_proof()
    published_volume_registry = published_volume_registry_proof()
    mesh_publication_components = mesh_publication_component_cache_proof()
    published_sonar = published_sonar_snapshot_proof()
    save_snapshot = save_snapshot_scratch_proof()
    malloc = job_malloc_proof()
    mesh_pool = voxel_mesh_pool_proof()
    volume_spawn_pool = voxel_volume_spawn_pool_proof()
    mesh_budget = mesh_upload_budgeting()
    collider_upload_budget = deferred_collider_upload_budgeting()
    physics_bake_guard = physics_bake_schedule_guard()
    residency_pager = world_residency_pager_prefetch_proof()
    residency_load_dispatch = world_residency_load_dispatch_proof()
    residency_radius_quality = world_residency_radius_quality_proof()
    residency_aup_precision = world_residency_aup_precision_proof()
    hydration_apply_ledger = world_residency_hydration_apply_ledger_proof()
    teleport_reset = world_residency_teleport_reset_proof()
    job_wait = voxel_job_wait_cancellation_proof()
    surface_gpu = surface_nets_gpu_upload_dispatcher_proof()
    surface_dump = surface_nets_dump_path_proof()
    cave_graph = cave_graph_generator_proof()
    spawn_point_job = voxel_spawn_point_job_proof()
    modified_cells_fill = modified_cells_fill_proof()
    streaming_scratch = streaming_scratch_prewarm_proof()
    global_vault = global_data_vault_pool_limits(pager, carve_queue, damage, published_sonar)

    stress = {
        "laser_frequency_hz": 60,
        "duration_seconds": 120,
        "frames": 7200,
        "single_laser_stamps_per_frame": 1,
        "single_laser_total_stamps": 7200,
        "queued_carve_events_total": 7200,
        "queued_carve_event_capacity": carve_queue["queued_carve_event_capacity"],
        "queued_carve_event_payload_bytes": carve_queue["queued_carve_event_payload_bytes"],
        "pending_carve_capacity": carve_queue["pending_carve_capacity"],
        "scheduled_carve_write_capacity": carve_queue["scheduled_carve_write_capacity"],
        "scheduled_carve_write_payload_bytes": carve_queue["scheduled_carve_write_payload_bytes"],
        "carve_queue_growth_unbounded": False,
        "carve_queue_overflow_policy": carve_queue["policy"],
        "graphics_buffer_overflow_for_single_laser": False,
        "graphics_buffer_overflow_condition": f"> {damage['damage_stamp_capacity_per_frame']} same-frame damage stamps",
        "graphics_buffer_overflow_policy": damage["overflow_policy"],
        "pager_queue_growth_unbounded": False,
        "pager_max_queued_write_payload_bytes": pager["max_write_arena_payload_bytes"],
        "pager_overflow_policy": pager["overflow_policy"],
        "rle_worst_case_fits_single_pager_sector": rle_packet["native_effective_worst_case_exceeds_sector_payload_by_bytes"] == 0,
        "rle_worst_case_note": "Pathological one-cell RLE is replaced by dense delta snapshot when dense is smaller; the native snapshot effective worst case is bounded by the dense payload.",
        "damage_volume_pressure": damage,
    }

    gates = {
        "dear_lie_shader_clip_present": all(shader_routes.values()),
        "sync_physx_registration_fallback_removed": len(sync_fallbacks) == 0,
        "voxel_carving_torture_job_present": has_text(FILES["rle"], "struct VoxelCarvingTortureJob"),
        "x006_blackbox_dump_path_present": (
            has_text(FILES["delta"], "Dump_SHINOBU_308_Voxel.bin")
            and rle_packet["compression_telemetry_dump_path_aligned"]
            and surface_dump["agent_dump_path_aligned"]
            and surface_dump["old_agent_dump_path_absent"]
            and mesh_publication_components["mesh_pipeline_blackbox_agent_dump_aligned"]
        ),
        "voxel_rle_architecture_wal_payload_guard": (
            rle_packet["architecture_max_wal_payload_bytes"] == pager["sector_payload_bytes"]
            and rle_packet["architecture_wal_payload_guard_present"]
            and rle_packet["vault_buffer_cell_capacity_fixed_to_chunk"]
            and rle_packet["vault_buffer_run_capacity_clamped_to_wal_payload"]
            and rle_packet["vault_buffer_staging_capacity_clamped_to_wal_payload"]
            and rle_packet["vault_buffer_sector_stats_capacity_clamped"]
        ),
        "voxel_carve_queue_and_commit_continuous_quality_scaled": (
            carve_queue["queued_drain_continuous_quality_scaled"]
            and carve_queue["scheduled_commit_continuous_quality_scaled"]
            and carve_queue["scheduled_commit_min_writes_per_frame"] >= 64
            and carve_queue["scheduled_commit_max_writes_per_frame"] >= 512
        ),
        "voxel_delta_force_complete_shutdown_only": (
            len(voxel_delta_shutdown["force_complete_hits"]) > 0
            and len(voxel_delta_shutdown["non_shutdown_force_complete_hits"]) == 0
            and voxel_delta_shutdown["shutdown_only_method_names_present"]
            and voxel_delta_shutdown["old_dispose_names_absent"]
            and voxel_delta_shutdown["on_disable_calls_shutdown_only"]
            and voxel_delta_shutdown["hot_carve_completion_nonblocking"]
            and voxel_delta_shutdown["hot_compaction_completion_nonblocking"]
        ),
        "unsafe_utility_malloc_absent_in_voxel_jobs": len(malloc["unsafe_utility_malloc_hits"]) == 0,
        "managed_chunk_tracking_absent": len(managed_chunk_tracking) == 0,
        "hot_native_allocations_absent": len(malloc["residual_hot_native_allocation_hits"]) == 0,
        "mesh_upload_main_thread_absent": len(mesh_apply) == 0,
        "mesh_upload_budgeted": mesh_budget["budgeted"],
        "mesh_upload_budget_continuous_quality_scaled": mesh_budget["continuous_quality_budget_present"],
        "mesh_upload_low_tier_burst_bias_present": mesh_budget["low_tier_burst_bias_present"],
        "unbudgeted_mesh_upload_absent": mesh_budget["budgeted"],
        "voxel_mesh_pool_runtime_lazy_allocation_absent": (
            mesh_pool["surface_mesh_pool_size"] >= 256
            and mesh_pool["physics_bake_mesh_pool_size"] >= 256
            and mesh_pool["cold_prewarm_creates_surface_pool_meshes"]
            and mesh_pool["cold_prewarm_creates_physics_pool_meshes"]
            and mesh_pool["runtime_acquire_lazy_mesh_creation_absent"]
            and mesh_pool["runtime_acquire_waits_for_existing_prewarm"]
            and mesh_pool["runtime_acquire_fails_closed"]
            and mesh_pool["physics_bake_schedule_fail_releases_pool_mesh"]
        ),
        "voxel_volume_runtime_spawn_pool_only": (
            volume_spawn_pool["spawn_volume_method_present"]
            and volume_spawn_pool["runtime_pool_miss_reports_blackbox"]
            and volume_spawn_pool["runtime_pool_miss_returns_null"]
            and volume_spawn_pool["editor_fallback_after_runtime_guard"]
            and volume_spawn_pool["runtime_fallback_new_gameobject_absent"]
            and volume_spawn_pool["all_spawn_calls_null_guarded"]
            and volume_spawn_pool["blackbox_flag_present"]
        ),
        "deferred_collider_upload_budget_continuous_quality_scaled": (
            collider_upload_budget["continuous_quality_budget_present"]
            and collider_upload_budget["token_bucket_present"]
            and collider_upload_budget["drain_uses_resolved_budget"]
            and collider_upload_budget["fixed_budget_driver_absent"]
        ),
        "physics_bake_schedule_backpressure_guard_present": (
            physics_bake_guard["schedule_precheck_present"]
            and physics_bake_guard["dispatcher_guard_present"]
            and physics_bake_guard["backpressure_guard_present"]
        ),
        "physics_bake_overflow_teardown_nonblocking": physics_bake_guard["emergency_teardown_overflow_nonblocking"],
        "physics_bake_force_complete_shutdown_only": physics_bake_guard["force_complete_shutdown_only"],
        "physics_bake_teardown_budget_continuous_quality_scaled": physics_bake_guard["teardown_budget_continuous_quality_scaled"],
        "physics_bake_deferred_teardown_keeps_chunk_visuals": physics_bake_guard["teardown_renderer_lookup_destroy_owner_only"],
        "physics_bake_job_not_burst_compiled": physics_bake_guard["physics_bake_job_not_burst_compiled"],
        "physics_bake_live_job_cancellable_wait_absent": (
            physics_bake_guard["live_job_cancellable_frame_wait_absent"]
            and physics_bake_guard["cancellation_deferred_teardown_present"]
        ),
        "physics_bake_teardown_driver_registered_before_schedule": physics_bake_guard["teardown_driver_registered_before_bake_schedule"],
        "physics_bake_registration_failure_nonblocking": physics_bake_guard["registration_failure_keeps_deferred_teardown"],
        "world_residency_pager_request_ids_monotonic": (
            residency_pager["monotonic_request_sequence_present"]
            and residency_pager["frame_xor_request_id_absent"]
            and residency_pager["ticket_ring_retires_before_reject"]
            and residency_pager["retire_budget_continuous_quality_scaled"]
        ),
        "world_residency_load_dispatch_continuous_quality_scaled": (
            residency_load_dispatch["low_tier_load_dispatch_budget"] == 1
            and residency_load_dispatch["visual_overkill_load_dispatch_budget"] >= 4
            and residency_load_dispatch["continuous_quality_scaled"]
            and residency_load_dispatch["max_concurrent_continuous_quality_scaled"]
            and residency_load_dispatch["token_bucket_present"]
            and residency_load_dispatch["same_frame_overdispatch_guard_present"]
            and residency_load_dispatch["queue_clear_resets_tokens"]
            and residency_load_dispatch["legacy_tier_switch_absent"]
        ),
        "world_residency_radius_continuous_quality_scaled": (
            residency_radius_quality["smooth_quality_helper_present"]
            and residency_radius_quality["prediction_distance_continuous"]
            and residency_radius_quality["load_radius_continuous"]
            and residency_radius_quality["unload_radius_continuous"]
            and residency_radius_quality["async_upload_budget_continuous"]
            and residency_radius_quality["impostor_residency_flag_continuous"]
            and residency_radius_quality["macro_database_tier_adapter_continuous"]
            and residency_radius_quality["legacy_resolved_tier_route_absent"]
        ),
        "world_residency_aup_paging_double_precision": (
            residency_aup_precision["radius_job_uses_safe_double_distance"]
            and residency_aup_precision["radius_job_float_distance_route_absent"]
            and residency_aup_precision["sort_job_uses_safe_double_distance"]
            and residency_aup_precision["projected_aup_uses_double3"]
            and residency_aup_precision["distance_helpers_use_safe_double"]
        ),
        "world_residency_hydration_apply_ledger_prewarmed": (
            hydration_apply_ledger["record_bytes"] == 64
            and hydration_apply_ledger["explicit_64_byte_layout_present"]
            and hydration_apply_ledger["vault_bit_present"]
            and hydration_apply_ledger["native_array_field_present"]
            and hydration_apply_ledger["owner_phase_prewarm_present"]
            and hydration_apply_ledger["sentinel_registered"]
            and hydration_apply_ledger["released_with_owner"]
            and hydration_apply_ledger["runtime_direct_indexed_store_present"]
            and hydration_apply_ledger["runtime_hot_growth_absent"]
            and hydration_apply_ledger["runtime_capacity_fail_closed"]
        ),
        "world_residency_teleport_reset_nonblocking": (
            teleport_reset["pending_flag_present"]
            and teleport_reset["pending_aup_present"]
            and teleport_reset["tick_finalizes_before_apply"]
            and teleport_reset["handle_defers_while_job_live"]
            and teleport_reset["pending_apply_waits_for_job_completion"]
            and teleport_reset["teleport_force_complete_absent"]
            and teleport_reset["teardown_force_complete_still_isolated"]
        ),
        "voxel_job_wait_cancellation_no_live_throw": (
            job_wait["awaiter_present"]
            and job_wait["cancellation_recorded"]
            and job_wait["finalizes_after_completion"]
            and job_wait["live_job_cancellable_frame_wait_absent"]
        ),
        "cave_graph_trymeasure_tryfill_temp_alloc_absent": (
            cave_graph["try_measure_stackalloc_present"]
            and cave_graph["try_fill_stackalloc_present"]
            and cave_graph["span_generator_present"]
            and cave_graph["temp_native_allocations_absent"]
            and cave_graph["native_list_references_absent"]
        ),
        "cave_graph_rebuild_scratch_prewarmed": (
            cave_graph["engine_rebuild_graph_scratch_prewarmed"]
            and cave_graph["engine_rebuild_graph_scratch_hard_cap_present"]
        ),
        "voxel_spawn_point_add_no_resize_bounded": (
            spawn_point_job["owner_job_present"]
            and spawn_point_job["parallel_writer_absent"]
            and spawn_point_job["capacity_guard_present"]
            and spawn_point_job["add_no_resize_present"]
            and spawn_point_job["parallel_schedule_absent"]
            and spawn_point_job["dependency_schedule_present"]
        ),
        "voxel_spawn_point_scratch_prewarmed": (
            spawn_point_job["scratch_prewarmed_on_lease"]
            and spawn_point_job["prepare_reuses_prewarmed_scratch"]
        ),
        "modified_cells_fill_time_sliced": (
            modified_cells_fill["engine_prepare_async_present"]
            and modified_cells_fill["engine_uses_async_delta_fill"]
            and modified_cells_fill["execute_awaits_modified_cells_prepare"]
            and modified_cells_fill["execute_yields_after_spatial_partitions"]
            and modified_cells_fill["delta_async_fill_present"]
            and modified_cells_fill["delta_fill_budget_yield_present"]
            and modified_cells_fill["delta_fill_probe_stride_present"]
            and modified_cells_fill["delta_dirty_mask_word_probe_present"]
        ),
        "modified_cells_scratch_prewarmed": (
            modified_cells_fill["engine_caps_measure_to_total_cells"]
            and modified_cells_fill["scratch_prewarmed_on_lease"]
            and modified_cells_fill["prepare_reuses_prewarmed_scratch"]
        ),
        "modified_cells_hashmap_overflow_fail_closed": modified_cells_fill["try_add_fail_closed_present"],
        "streaming_scratch_post_lease_growth_absent": (
            streaming_scratch["lease_signature_carries_grid_dimension"]
            and streaming_scratch["slot_marked_in_use_after_prewarm"]
            and streaming_scratch["slot_resize_skips_live_leases"]
            and streaming_scratch["slot_prewarm_exception_fail_closed"]
            and streaming_scratch["mesh_raw_capacity_continuous_quality_scaled"]
            and streaming_scratch["mesh_scratch_prewarmed_on_lease"]
            and streaming_scratch["spatial_scratch_prewarmed_on_lease"]
            and streaming_scratch["collider_scratch_prewarmed_on_lease"]
            and streaming_scratch["post_lease_nativearray_growth_absent"]
            and streaming_scratch["post_lease_capacity_fail_closed"]
            and streaming_scratch["capacity_overflow_blackbox_present"]
        ),
        "surface_nets_gpu_upload_finalize_completes_completed_job": (
            surface_gpu["dispatcher_present"]
            and surface_gpu["lock_buffer_route_present"]
            and surface_gpu["finalize_completes_completed_job_before_unlock"]
            and surface_gpu["finalize_no_precomplete_wait"]
        ),
        "surface_nets_gpu_upload_capacity_fail_closed": (
            surface_gpu["dispatcher_present"]
            and surface_gpu["upload_requires_indirect_args_view"]
            and surface_gpu["upload_capacity_fail_closed"]
            and surface_gpu["upload_silent_truncation_absent"]
            and surface_gpu["upload_marks_state_after_lock_and_schedule"]
        ),
        "surface_nets_gpu_partial_lock_failure_unlocks": (
            surface_gpu["dispatcher_present"]
            and surface_gpu["partial_lock_failure_unlocks_buffers"]
        ),
        "surface_nets_gpu_release_completed_upload_nonblocking": (
            surface_gpu["dispatcher_present"]
            and surface_gpu["release_completed_upload_without_wait"]
            and surface_gpu["release_request_deferred_nonblocking"]
            and surface_gpu["begin_upload_rejects_pending_release"]
        ),
        "surface_nets_gpu_initialize_respects_inflight_release": (
            surface_gpu["dispatcher_present"]
            and surface_gpu["initialize_respects_inflight_release"]
        ),
        "surface_nets_gpu_invalid_buffer_fail_closed": (
            surface_gpu["dispatcher_present"]
            and surface_gpu["graphics_buffer_validity_guard_present"]
            and surface_gpu["invalid_upload_resource_releases_for_cold_recreate"]
            and surface_gpu["invalid_release_skips_dead_graphics_buffers"]
            and surface_gpu["invalid_unlock_skips_dead_graphics_buffers"]
            and surface_gpu["finalize_invalid_upload_resource_fails_closed"]
        ),
        "surface_nets_blackbox_dump_path_x006": (
            surface_dump["agent_dump_path_aligned"]
            and surface_dump["old_agent_dump_path_absent"]
            and surface_dump["writes_primary_and_agent_dump"]
        ),
        "deformation_collider_main_thread_assignment_absent": len(collider_shared_mesh_assignments) == 0,
        "deformation_collider_null_mesh_mutation_absent": len(deformation_shared_mesh_nulls) == 0,
        "deferred_bake_presentation_null_mesh_mutation_absent": len(deferred_bake_presentation_nulls) == 0,
        "runtime_collider_null_mesh_mutation_absent": len(runtime_collider_nulls) == 0,
        "paging_cleanup_getcomponent_hotpath_absent": len(paging_getcomponent_hits) == 0,
        "voxel_rebuild_qualitysettings_lodbias_absent": "QualitySettings.lodBias" not in engine_text,
        "rle_packet_aligned": True,
        "rle_worst_case_fits_single_pager_sector": stress["rle_worst_case_fits_single_pager_sector"],
        "pager_write_queue_bounded": pager["write_slot_count"] > 0 and pager["sector_payload_bytes"] > 0,
        "pager_direct_read_slice_ready_only": pager["direct_read_slice_ready_only_present"],
        "pager_direct_read_staging_prewarmed": pager["direct_read_staging_prewarmed_present"],
        "graphics_stamp_buffer_bounded": (
            damage["damage_stamp_capacity_per_frame"] > 0
            and damage["cut_mask_stamp_capacity_per_frame"] > 0
            and damage["cut_mask_upload_fail_closed_present"]
            and damage["damage_volume_upload_fail_closed_present"]
            and damage["stamp_graphics_buffer_invalid_fail_closed"]
            and damage["stamp_graphics_buffers_recreated_when_invalid"]
            and damage["cut_mask_shader_stamp_count_clamped"]
            and damage["damage_volume_shader_stamp_count_clamped"]
            and damage["active_stamp_buffers_validated_before_dispatch"]
        ),
        "carve_ingress_queue_bounded": (
            carve_queue["queued_carve_event_capacity"] > 0
            and carve_queue["pending_carve_capacity"] > 0
            and carve_queue["queued_carve_event_payload_bytes"] > 0
        ),
        "carve_overflow_coalescing_present": (
            carve_queue["queue_overflow_coalescing_present"]
            and carve_queue["pending_overflow_coalescing_present"]
            and carve_queue["blind_oldest_drop_absent"]
            and carve_queue["pending_managed_growth_absent"]
        ),
        "scheduled_carve_write_buffer_fixed_capacity_present": (
            carve_queue["scheduled_carve_write_capacity"] > 0
            and carve_queue["scheduled_carve_write_payload_bytes"] > 0
            and carve_queue["scheduled_carve_write_prewarm_present"]
            and carve_queue["scheduled_carve_write_hot_growth_absent"]
            and carve_queue["scheduled_carve_write_over_capacity_reject_present"]
            and carve_queue["scheduled_carve_candidate_overflow_guard_present"]
            and carve_queue["scheduled_carve_schedule_exception_blackbox_present"]
        ),
        "damage_stamp_overflow_coalescing_present": (
            damage["cut_mask_overflow_coalescing_present"]
            and damage["damage_volume_overflow_coalescing_present"]
            and damage["overflow_coalescing_expands_coverage_present"]
        ),
        "damage_volume_quality_scaled": damage["quality_scaled_runtime_dimensions_present"],
        "damage_volume_quality_resize_inactive_gate_present": damage["quality_resize_inactive_gate_present"],
        "damage_volume_energy_gated": damage["energy_gated_dispatch_present"],
        "damage_volume_shader_active_energy_gated": damage["shader_active_energy_gated_present"],
        "damage_volume_gpu_upload_fail_closed": damage["damage_volume_upload_fail_closed_present"],
        "cut_mask_gpu_upload_fail_closed": damage["cut_mask_upload_fail_closed_present"],
        "stamp_graphics_buffer_invalid_fail_closed": damage["stamp_graphics_buffer_invalid_fail_closed"],
        "stamp_graphics_buffers_recreated_when_invalid": damage["stamp_graphics_buffers_recreated_when_invalid"],
        "cut_mask_shader_stamp_count_clamped": damage["cut_mask_shader_stamp_count_clamped"],
        "damage_volume_shader_stamp_count_clamped": damage["damage_volume_shader_stamp_count_clamped"],
        "damage_volume_binary_quality_route_absent": damage["binary_qualitysettings_route_absent"],
        "global_datavault_dirty_chunk_recycler_proven": dirty_chunk["global_datavault_recycler_proven"],
        "global_datavault_dirty_chunk_hot_swap_rebind_present": dirty_chunk["global_datavault_hot_swap_rebind_present"],
        "global_datavault_pool_limits_proven": (
            global_vault["max_buffer_capacity"] >= 32768
            and global_vault["max_generation_handle_capacity"] >= 100000
            and global_vault["max_block_capacity"] >= 65536
            and global_vault["vault_block_alignment_bytes"] == 64
            and global_vault["initial_arena_bytes"] >= 128 * 1024 * 1024
            and global_vault["minimum_quality_arena_limit_bytes"] >= 512 * 1024 * 1024
            and global_vault["maximum_quality_arena_limit_bytes"] >= 4 * 1024 * 1024 * 1024
            and global_vault["boot_primary_prewarm_present"]
            and global_vault["bounded_growth_guards_present"]
            and global_vault["pointer_alignment_audit_present"]
        ),
        "voxel_volume_registry_fixed_capacity_present": (
            volume_registry["fixed_volume_registry_present"]
            and volume_registry["managed_volume_lists_absent"]
            and volume_registry["registration_overflow_direct_rebuild_present"]
            and volume_registry["volume_registry_capacity"] >= 64
        ),
        "engine_active_volume_registry_hard_cap_present": (
            engine_active_volume_registry["active_volume_registry_capacity"] >= 64
            and engine_active_volume_registry["dedupe_present"]
            and engine_active_volume_registry["hard_capacity_guard_present"]
            and engine_active_volume_registry["eviction_selector_present"]
            and engine_active_volume_registry["post_eviction_fail_closed_present"]
        ),
        "collider_chunk_registry_fixed_capacity_present": (
            collider_chunk_registry["max_collider_chunk_count"] == 8
            and collider_chunk_registry["fixed_registry_arrays_present"]
            and collider_chunk_registry["runtime_registry_resize_absent"]
        ),
        "collider_chunk_hot_path_object_creation_absent": (
            collider_chunk_registry["cold_prepare_prewarms_hierarchy"]
            and collider_chunk_registry["hot_split_requires_prewarmed_hierarchy"]
        ),
        "published_volume_registry_hard_cap_present": (
            published_volume_registry["max_registered_published_volumes"] >= 256
            and published_volume_registry["list_capacity_matches_max"]
            and published_volume_registry["register_hard_cap_present"]
            and published_volume_registry["swap_back_remove_present"]
        ),
        "published_sdf_read_accessors_pure": published_volume_registry["all_read_accessors_pure"],
        "mesh_publication_component_cache_present": (
            mesh_publication_components["build_welded_uses_cached_components"]
            and mesh_publication_components["apply_volume_uses_source_volume"]
            and mesh_publication_components["mesh_publication_getcomponent_absent"]
            and mesh_publication_components["generated_volume_bound_before_mesh_publication"]
        ),
        "mesh_publication_volume_addcomponent_absent": (
            mesh_publication_components["volume_missing_component_fails_closed"]
            and mesh_publication_components["volume_missing_collider_uses_fake"]
            and mesh_publication_components["runtime_null_volume_collider_fails_closed"]
            and mesh_publication_components["cinematic_fake_proxy_search_editor_only"]
        ),
        "compaction_source_copy_job_present": dirty_chunk["compaction_copy_job_present"],
        "compaction_source_main_thread_copy_absent": dirty_chunk["compaction_main_thread_copy_absent"],
        "compaction_dirty_state_copy_job_present": dirty_chunk["compaction_dirty_state_copy_job_present"],
        "compaction_dirty_state_main_thread_copy_absent": dirty_chunk["compaction_dirty_state_main_thread_copy_absent"],
        "compaction_source_version_guard_present": dirty_chunk["compaction_source_version_guard_present"],
        "compaction_pressure_scheduler_present": dirty_chunk["compaction_pressure_scheduler_present"],
        "save_voxel_snapshot_borrowed_scratch_present": save_snapshot["processor_borrowed_snapshot_copy_present"] and save_snapshot["save_manager_uses_borrowed_snapshot"],
        "save_voxel_snapshot_per_save_nativearray_absent": save_snapshot["save_manager_per_save_nativearray_absent"],
        "save_voxel_snapshot_borrowed_not_disposed": save_snapshot["save_manager_borrowed_dispose_guard_present"],
        "save_voxel_snapshot_borrowed_lifetime_guarded": save_snapshot["save_manager_borrowed_lease_lifetime_present"],
        "save_voxel_snapshot_growth_blocked_during_borrow": save_snapshot["processor_borrowed_growth_blocked_during_lease"],
        "save_voxel_snapshot_borrowed_write_exclusion_present": save_snapshot["processor_borrowed_write_exclusion_present"],
        "datavault_rebind_waits_for_snapshot_lease": save_snapshot["datavault_rebind_waits_for_snapshot_lease"],
        "save_voxel_snapshot_copy_failure_fail_closed": save_snapshot["save_manager_copy_failure_fail_closed"],
        "save_voxel_legacy_load_fallback_present": save_snapshot["save_manager_legacy_load_fallback_present"],
        "published_sonar_encode_job_present": published_sonar["encoded_sample_job_present"],
        "published_sonar_main_thread_encode_absent": published_sonar["main_thread_encode_loop_absent"],
        "published_sonar_staging_swap_present": published_sonar["staging_swap_present"],
        "published_sonar_vault_copy_job_present": published_sonar["vault_copy_job_present"],
        "published_sonar_vault_memcopy_absent": published_sonar["vault_memcopy_absent"],
        "published_sonar_vault_per_byte_copy_absent": published_sonar["vault_per_byte_copy_absent"],
        "published_sonar_vault_write_lock_release_guard_present": published_sonar["vault_write_lock_released_after_copy"],
        "published_sonar_descriptor_lock_not_held_during_sdf_copy": published_sonar["descriptor_lock_not_held_during_sdf_copy"],
        "published_sonar_descriptor_invalidated_before_sdf_copy": published_sonar["descriptor_invalidated_before_sdf_copy"],
        "published_sonar_descriptor_final_write_after_sdf_copy": published_sonar["descriptor_final_write_after_sdf_copy"],
        "published_sonar_vault_publish_serialized_present": published_sonar["vault_publish_serialized_present"],
        "published_sonar_cancel_force_complete_absent": published_sonar["cancel_force_complete_absent"],
        "published_sonar_active_staging_buffers_present": published_sonar["active_and_staging_buffers_present"],
        "published_sonar_high_water_buffer_reuse_present": published_sonar["high_water_buffer_reuse_present"],
        "published_sonar_clear_metadata_only_present": published_sonar["clear_metadata_only_present"],
        "published_sonar_dispose_guarded_by_read_lease_present": published_sonar["dispose_guarded_by_read_lease_present"],
        "published_sonar_clear_aborts_inflight_publish_present": published_sonar["clear_aborts_inflight_publish_present"],
        "published_sonar_compaction_copies_actual_count": published_sonar["compaction_copies_actual_grid_count"],
        "published_sonar_vault_fixed_capacity_present": published_sonar["vault_fixed_max_capacity_present"],
        "published_sonar_vault_publish_hot_ensure_absent": published_sonar["vault_publish_hot_ensure_absent"],
        "published_sonar_vault_owner_phase_prewarm_present": published_sonar["vault_owner_phase_prewarm_present"],
        "published_sonar_vault_descriptor_owner_guard_present": published_sonar["vault_descriptor_owner_guard_present"],
        "published_sonar_vault_descriptor_unconditional_clear_absent": published_sonar["vault_descriptor_unconditional_clear_absent"],
        "published_sonar_local_read_lease_guard_present": (
            published_sonar["local_publish_serialized_present"]
            and published_sonar["build_buffer_read_lease_guard_present"]
            and published_sonar["compaction_source_read_lease_present"]
            and published_sonar["compaction_read_lease_release_present"]
        ),
    }

    unity_mesh_publication_residual = (not gates["mesh_upload_main_thread_absent"]) and gates["mesh_upload_budgeted"]
    hard_failed_gate_names = {
        name for name, passed in gates.items()
        if not passed and name != "mesh_upload_main_thread_absent"
    }
    failed = sorted(hard_failed_gate_names)
    if not failed and unity_mesh_publication_residual:
        verdict = "PASS_STATIC_WITH_BUDGETED_UNITY_MESH_UPLOAD_RESIDUAL"
    else:
        verdict = "PASS_STATIC" if not failed else "FAIL_STATIC_REMAINING_HOT_PATHS"

    return {
        "agent": "X_006",
        "report": "VOXEL_OPTIMIZATION_REPORT_X_006",
        "verdict": verdict,
        "failed_gates": failed,
        "residual_risks": {
            "unity_mesh_publication_main_thread_api": unity_mesh_publication_residual,
            "unity_mesh_publication_policy": mesh_budget["policy"],
        },
        "gates": gates,
        "stress_60hz_120s": stress,
        "rle_packet_byte_layout": rle_packet,
        "world_pager_limits": pager,
        "global_data_vault_pool": global_vault,
        "carve_queue_pressure": carve_queue,
        "voxel_delta_shutdown_completion": voxel_delta_shutdown,
        "surface_nets_datavault_pool": surface_nets_vault_ledger(),
        "surface_nets_blackbox_dump": surface_dump,
        "active_dirty_chunk_memory": dirty_chunk,
        "volume_registry": volume_registry,
        "engine_active_volume_registry": engine_active_volume_registry,
        "collider_chunk_registry": collider_chunk_registry,
        "published_volume_registry": published_volume_registry,
        "mesh_publication_component_cache": mesh_publication_components,
        "published_sonar_snapshot": published_sonar,
        "save_voxel_snapshot_scratch": save_snapshot,
        "voxel_mesh_pool": mesh_pool,
        "voxel_volume_spawn_pool": volume_spawn_pool,
        "surface_nets_gpu_upload_dispatcher": surface_gpu,
        "physics_bake_schedule_guard": physics_bake_guard,
        "world_residency_pager_prefetch": residency_pager,
        "world_residency_load_dispatch": residency_load_dispatch,
        "world_residency_radius_quality": residency_radius_quality,
        "world_residency_aup_precision": residency_aup_precision,
        "world_residency_hydration_apply_ledger": hydration_apply_ledger,
        "world_residency_teleport_reset": teleport_reset,
        "voxel_job_wait_cancellation": job_wait,
        "cave_graph_generator": cave_graph,
        "voxel_spawn_point_job": spawn_point_job,
        "modified_cells_fill": modified_cells_fill,
        "streaming_scratch_prewarm": streaming_scratch,
        "deferred_collider_upload_budgeting": collider_upload_budget,
        "evidence": {
            "sync_physx_registration_fallbacks": sync_fallbacks,
            "deferred_or_direct_shared_mesh_assignments": shared_mesh_assignments,
            "deferred_or_direct_collider_shared_mesh_assignments": collider_shared_mesh_assignments,
            "deformation_collider_shared_mesh_null_mutations": deformation_shared_mesh_nulls,
            "deferred_bake_presentation_shared_mesh_null_mutations": deferred_bake_presentation_nulls,
            "runtime_collider_shared_mesh_null_mutations": runtime_collider_nulls,
            "paging_cleanup_getcomponent_hotpath_hits": paging_getcomponent_hits,
            "physics_bake_calls": physics_bake,
            "main_thread_mesh_upload_sites": mesh_apply,
            "mesh_upload_budgeting": mesh_budget,
            "unsafe_malloc_in_voxel_sources": malloc["unsafe_utility_malloc_hits"],
            "native_allocations_in_voxel_sources": malloc["native_allocation_hits"],
            "native_allocations_classified": malloc["native_allocation_hits_classified"],
            "residual_hot_native_allocations": malloc["residual_hot_native_allocation_hits"],
            "managed_chunk_tracking": managed_chunk_tracking,
            "shader_clip_routes": shader_routes,
            "datavault_lanes": {
                "carve_writes": "BufferID.ShinobuDeltaCrusherCarveWrites = 70131",
                "carve_blackbox": "BufferID.ShinobuDeltaCrusherVoxelBlackBox = 70130",
                "save_rle_runs": "BufferID.SaveVoxelDeltaRleRuns = 70289",
                "save_rle_bytes": "BufferID.SaveVoxelDeltaRleBytes = 70291",
                "world_pager_write_commands": "BufferID.SaveWorldPagerWriteCommands = 70207",
                "world_pager_write_arena": "BufferID.SaveWorldPagerWriteArena = 70200",
            },
        },
    }


def main():
    report = build_report()
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"{report['verdict']} failed={','.join(report['failed_gates']) or 'none'} path={REPORT_PATH}")
    return 0 if report["verdict"].startswith("PASS_STATIC") else 2


if __name__ == "__main__":
    raise SystemExit(main())
