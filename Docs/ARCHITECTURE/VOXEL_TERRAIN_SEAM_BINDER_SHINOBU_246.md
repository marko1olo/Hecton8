# Voxel Terrain Seam Binder SHINOBU_246

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Scope: offline/editor seam baking between heightmap terrain meshes and voxel cave meshes.

Runtime authority: none. Runtime loads baked mesh assets and does not run spatial hashing, vertex snapping, normal blending, texture alpha baking, CSV parsing, preview drawing, or scanners.

Compile wall:
- `Hecton8.World.VoxelTerrainSeamBinder.asmdef` owns only raw DTOs/math and references `Unity.Mathematics`.
- `Hecton8.World.VoxelTerrainSeamBinder.Editor.asmdef` owns the Forge window, Burst jobs, scanner, preview, and AssetDatabase writer. It references the owned seam assembly plus Unity Burst/Collections/Jobs/Mathematics and explicit Roslyn precompiled DLLs for AST scanning only.
- No sibling runtime domain reference is introduced. Terrain, voxel cave, streaming, rendering, and rollback domains are touched only through offline mesh assets and reports.

Data route:
- Input: terrain/voxel `Mesh` pairs for LOD0, LOD1, LOD2 plus `double3` terrain and voxel AUP roots.
- Work buffers: `NativeArray<SeamBindVertex32>`, `NativeArray<int>`, `NativeParallelMultiHashMap<long, SeamBoundaryVertex64>`, `NativeArray<SeamSnapResult64>`.
- Output: stitched mesh assets under `Assets/_Project/BakedGeometry/Stitched/`, rollback exclusion sidecar `.bytes`, and stitch JSON reports.
- Rollback fence sidecar: fixed 32 bytes, eight little-endian uint lanes: terrain hash, voxel hash, stitched hash, rollback-excluded flag, `VTSF` magic, version, endian marker `0x01020304`, reserved.
- Black box: `SeamBindTelemetryEntry[300]` records stage, frame cursor, vertex counts, snapped count, max error, warning flags, and root AUP. On failure it dumps to `Docs/AgentLogs/Dump_SHINOBU_246.bin`.
- Preview route: `PreviewLod0` uses copied LOD0 native buffers and uploads terrain-local thick SceneView pull ribbons into a hidden editor Mesh from short-lived Temp native buffers without creating or overwriting mesh assets. Forge control changes queue one debounced preview refresh through `EditorApplication.delayCall`.

Layout contract:
- Stitched mesh vertex stride is 32 bytes: float3 position, float3 normal, UNorm8x4 color, UNorm16x2 uv0.
- Layout validation is editor-time and throws if explicit DTO sizes or offsets diverge.

Quality contract:
- `GlobalQualityWeight` is continuous. It affects LOD seam tolerance and visual blend curves only.
- It does not change gameplay truth ownership, save identity, StateRingBuffer routing, or DTO layout.
- Below 0.3 the editor bake expands lower-LOD seam tolerance/cell size and collapses visual smoothing toward cheaper linear ramps. Mid-tier weights preserve smoothstep-like blending. High/Ultra tighten positional tolerance and spend saved runtime budget in shader interpretation of baked vertex alpha and normals.

Rollback/networking:
- Static stitched geometry is presentation/environment data.
- Mesh bytes are excluded from StateRingBuffer and Merkle hashing.
- Runtime synchronization remains limited to existing gameplay entity authority routes.

Scanner proof route:
- `Dynamic_Vertex_Scanner` uses Roslyn AST for non-Editor source files and reports seam-context runtime calls to `.mesh.vertices`, `sharedMesh.vertices`, `GetVertices`, `SetVertices`, and `RecalculateNormals`.
- Text scanning remains only as a parser-failure fallback and reports `parserFailures` into `Docs/Reports/WORLD_OPTIMIZATION_REPORT.json`.

Verification boundary:
- Static source and filesystem proof exist.
- Loop 10 static scan found no private array declarations, no `List`, no `DropdownField`, no lambdas, no `foreach`, no LINQ, no DTO properties, no `MemClear`, no Persistent allocator, no `Time.deltaTime`, and no `UnityEngine.Random` in the owned source domain. CSV parsing uses short-lived native scratch instead of large stackalloc, and rollback fence bytes 16-31 now carry magic/version/endian/reserved proof fields instead of inert padding.
- Brace/preprocessor balance scan over 7 owned code files passed. Three `.Complete()` calls remain only at offline editor boundaries where MeshData lifetime, AssetDatabase/report readback, or mock benchmark handoff requires completed buffers.
- Unity import, C# compile, Burst Inspector, mock benchmark execution, generated mesh diff, SceneView preview capture, profiler, and GCMonitor proof are still pending because local CPU/build gate has not permitted a build run.
