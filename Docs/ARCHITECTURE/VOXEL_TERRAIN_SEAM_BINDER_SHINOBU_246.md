# Voxel Terrain Seam Binder SHINOBU_246

Scope: offline/editor seam baking between heightmap terrain meshes and voxel cave meshes.

Runtime authority: none. Runtime loads baked mesh assets and does not run spatial hashing, vertex snapping, normal blending, texture alpha baking, CSV parsing, preview drawing, or scanners.

Compile wall:

- `Hecton8.World.VoxelTerrainSeamBinder.asmdef` owns only raw DTOs/math and references `Unity.Mathematics`.

- `Hecton8.World.VoxelTerrainSeamBinder.Editor.asmdef` owns Forge window, Burst jobs, scanner, preview, and AssetDatabase writer.
- It references owned seam assembly, Unity Burst/Collections/Jobs/Mathematics, and explicit Roslyn DLLs for AST scanning only.

- No sibling runtime domain reference is introduced. Terrain, voxel cave, streaming, rendering, and rollback domains are touched only through offline mesh assets and reports.

Data route:

- Input: terrain/voxel `Mesh` pairs for LOD0, LOD1, LOD2 plus `double3` terrain and voxel AUP roots.

- Work buffers: `NativeArray<SeamBindVertex32>`, `NativeArray<int>`, `NativeParallelMultiHashMap<long, SeamBoundaryVertex64>`, `NativeArray<SeamSnapResult64>`.

- Output: stitched mesh assets under `Assets/_Project/BakedGeometry/Stitched/`, rollback exclusion sidecar `.bytes`, and stitch JSON reports.

- Rollback fence sidecar: fixed 32 bytes, eight little-endian uint lanes: terrain hash, voxel hash, stitched hash, rollback-excluded flag, `VTSF` magic, version, endian marker `0x01020304`, reserved.

- Black box: `SeamBindTelemetryEntry[300]` records stage, frame cursor, vertex counts, snapped count, max error, warning flags, and root AUP. On failure it dumps to `Docs/AgentLogs/Dump_SHINOBU_246.bin`.

- Preview route: `PreviewLod0` uses copied LOD0 native buffers.
- It uploads terrain-local thick SceneView pull ribbons into hidden editor Mesh from short-lived Temp native buffers.
- It does not create or overwrite mesh assets.
- Forge control changes queue one debounced preview refresh through `EditorApplication.delayCall`.

Layout contract:

- Stitched mesh vertex stride is 32 bytes: float3 position, float3 normal, UNorm8x4 color, UNorm16x2 uv0.

- Layout validation is editor-time and throws if explicit DTO sizes or offsets diverge.

Quality contract:

- `GlobalQualityWeight` is continuous. It affects LOD seam tolerance and visual blend curves only.

- It does not change gameplay truth ownership, save identity, StateRingBuffer routing, or DTO layout.

- Below `0.3`, editor bake expands lower-LOD seam tolerance/cell size and collapses visual smoothing toward cheaper linear ramps.
- Mid-tier weights preserve smoothstep-like blending.
- High/Ultra tighten positional tolerance and spend saved runtime budget in shader interpretation of baked vertex alpha and normals.

Rollback/networking:

- Static stitched geometry is presentation/environment data.

- Mesh bytes are excluded from StateRingBuffer and Merkle hashing.

- Runtime synchronization remains limited to existing gameplay entity authority routes.

Scanner proof route:

- `Dynamic_Vertex_Scanner` uses Roslyn AST for non-Editor source files and reports seam-context runtime calls to `.mesh.vertices`, `sharedMesh.vertices`, `GetVertices`, `SetVertices`, and `RecalculateNormals`.

- Text scanning remains only as a parser-failure fallback and reports `parserFailures` into `Docs/Reports/WORLD_OPTIMIZATION_REPORT.json`.

Verification boundary:

- Static source and filesystem proof exist.

- Loop 10 static scan:
  - No private array declarations, `List`, `DropdownField`, lambdas, `foreach`, LINQ, DTO properties, `MemClear`, Persistent allocator, `Time.deltaTime`, or `UnityEngine.Random`.
  - Scope: owned source domain.
  - CSV parsing: short-lived native scratch, not large stackalloc.
  - Rollback fence bytes `16..31`: magic/version/endian/reserved proof fields.
  - Removed state: inert padding in that byte range.

- Brace/preprocessor balance scan over 7 owned code files passed.
- Three `.Complete()` calls remain only at offline editor boundaries: MeshData lifetime, AssetDatabase/report readback, mock benchmark handoff.

- Pending proof: Unity import, C# compile, Burst Inspector, mock benchmark execution.
- Also pending: generated mesh diff, SceneView preview capture, profiler, GCMonitor.
- Reason: local CPU/build gate has not permitted a build run.
