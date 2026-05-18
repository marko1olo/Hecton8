# Status_SHINOBU_61

Date: 2026-05-18
Agent: SHINOBU_61
Domain: ECHELON 2 WORLD GENERATION & TERRAIN / Voxel Surface Nets Meshing
Evidence status: IN PROGRESS; ACTIVE PROMPT IS `VOXEL_SURFACE_NETS_ARCHITECT`; prior duplicate-ID apex logs archived.

## Mandates Read Before Coding

- VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt
- VOX_Voxel_World_Logic_Carving_Persistence.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- REND_GPU_Sovereignty.txt
- GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## Batch Prompt Boundary

Extracted from `Docs/Tasks/CURRENT_BATCH.md` using CLI regex for `id="SHINOBU_61"` and `role="VOXEL_SURFACE_NETS_ARCHITECT"`.
Task count: 21 prompt task references; canonical XML task matrix is Task 01 through Task 20 plus the mandatory self-audit block.

## State Machine Checklist

- [x] Task 01 BINARY_GRAVEYARD_RECONNAISSANCE | DOD practice: `TryBootstrapLookupTables()` probes `Docs/Archive` and `Assets/StreamingAssets`, then `GenerateEmergencyMockTables()` fills 256 unmanaged edge masks | Alternative rejected: hard dependency on missing OSHINO LUT files | Estimate: 4-12 us cold boot fallback, 0 us hot path
- [x] Task 02 MANAGED_MESH_ERADICATION_PASS | DOD practice: new module contains no `new Mesh()`, `SetVertices`, `RecalculateNormals`, or CPU `SetData`; upload uses `GraphicsBuffer.LockBufferForWrite` and `UnsafeUtility.MemCpy` | Alternative rejected: standard Unity Mesh validation path | Estimate: 300-900 us saved per laser remesh burst, pending profiler
- [x] Task 03 CS1612_ENCAPSULATION_PURGE | DOD practice: hot DTOs use public fields; vault exposes `GetStateAsRef()` and `GetStateAsReadOnlyRef()` | Alternative rejected: `{ get; private set; }` state wrappers | Estimate: 2-6 us saved per 256 chunk-state pass
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION | DOD practice: `VoxelVertexDTO` is 32B, state/telemetry/AABB are 64B, no `Pack=1` | Alternative rejected: packed 52B/unaligned DTOs | Estimate: 8-30 us saved on Quest-class cache-line fetches
- [x] Task 05 BLIND_DEPENDENCY_MOCKING | DOD practice: `partial struct MockVoxelDensityArray` plus Burst hollow-sphere density generator | Alternative rejected: direct dependency on Agent 05 density compressor | Estimate: 20-60 us saved in isolated test harness setup
- [x] Task 06 BURST_SURFACE_EXTRACTION_KERNEL | DOD practice: `SurfaceNetExtractionJob` scans SDF cells, emits one centroid vertex per crossing cell, and connects coarse cell vertices | Alternative rejected: main-thread Marching Cubes lists | Estimate: target <1.0 ms for full 32^3 chunk, profiler pending
- [x] Task 07 GRADIENT_NORMAL_SMOOTHING_JOB | DOD practice: tetrahedral SDF sampling at generated vertex positions; normals/tangents packed RGB10_A2 | Alternative rejected: triangle-normal averaging and `mesh.RecalculateNormals()` | Estimate: 120-300 us saved and 8-16B/vertex bandwidth saved
- [x] Task 08 ASYNC_GPU_UPLOAD_DISPATCHER | DOD practice: boot-prewarmed double-buffered `GraphicsBuffer` upload state via `LockBufferForWrite`/`MemCpy`; Burst extraction writes indirect args into vault before upload | Alternative rejected: `Mesh.SetVertexBufferData` validation, CPU mesh ownership, and upload-time buffer allocation | Estimate: 200-600 us saved during upload bursts
- [x] Task 09 SEAMLESS_CHUNK_STITCHING | DOD practice: density workspace is `32+2` per axis and sampling is offset through a one-voxel ghost border | Alternative rejected: independent boundary-only chunk sampling | Estimate: avoids seam repair pass; 40-120 us saved per boundary-heavy chunk
- [x] Task 10 THE_DEAR_LIE_FRUSTUM_PRIORITIZATION | DOD practice: `VoxelSurfacePriorityJob` scores AUP-local chunk centers against camera/frustum, optional `VoxelSurfaceHzbCullJob` suppresses CPU-downloaded HZB-occluded chunks, tuning clamps max chunks/frame to 1-2 | Alternative rejected: FIFO remesh of off-camera/occluded chunks | Estimate: 0.3-2.0 ms saved in turn/laser stress frames
- [x] Task 11 CONTINUOUS_SCALABILITY_MESH_DECIMATION | DOD practice: `GlobalQualityWeight` drives exact 25%-100% sample ratio anchored at weight 0.2, stride 4..1, centroid-to-center bias, scheduler cadence 5..60 Hz for non-urgent chunks, and telemetry decimation ratio | Alternative rejected: binary low/high LOD switches | Estimate: up to 60% vertex/index reduction at low quality
- [x] Task 12 MATERIAL_TRANSITION_BAKING | DOD practice: biome/material blend is packed into `ColorPacked.g`, quality into `ColorPacked.b`, planar UVs only | Alternative rejected: CPU triplanar projection/3D material lookup | Estimate: 20-80 us CPU saved; shader receives cheap scalar data
- [x] Task 13 DYNAMIC_DESTRUCTION_RE-MESHING | DOD practice: `VoxelSurfaceDirtySignalJob` consumes unmanaged dirty signals and forces priority 1 dirty state for laser-carved chunks | Alternative rejected: direct Agent 05 event dependency | Estimate: remesh request visible under 3 frame budget if dispatcher schedules immediately
- [x] Task 14 PHYSICS_BAKING_BYPASS | DOD practice: `VoxelSurfacePhysicsBakeRequestDTO` and Burst request job stage mesh IDs for a physics-owned worker bridge; no main-thread collider bake in this domain | Alternative rejected: `MeshCollider`/`Physics.BakeMesh` inside Surface Nets runtime, because this domain owns no managed Mesh and must not reference Physics domain | Estimate: avoids known 50 ms main-thread bake spike when bridge is used
- [x] Task 15 AUP_ORIGIN_SHIFT_MIGRATION | DOD practice: `VoxelSurfaceAabbShiftJob` adjusts BRG AABB centers by shift delta; vertices remain chunk-local `float3` | Alternative rejected: regenerating meshes on origin shift | Estimate: 0.4-2.0 ms saved per visible chunk set
- [x] Task 16 ZERO_INIT_OVERHEAD_BYPASS | DOD practice: all persistent workspace buffers are DataVault handles; clear is length/counter reset plus first-hydration `MemClear` | Alternative rejected: per-chunk `NativeList` or managed `List<T>` allocation | Estimate: 100-500 us saved per remesh burst and 0 GC
- [x] Task 17 TELEMETRY_MESHING_RECORDER | DOD practice: 300-frame `VoxelMeshingTelemetryEntry` ring plus endian-marked `Dump_MESH_SURGEON.bin` and `Dump_SHINOBU_61.bin` | Alternative rejected: managed string logging in extraction path | Estimate: 30-100 us saved by avoiding logs; dump is cold only
- [x] Task 18 MESHING_TUNER_EDITOR_WINDOW | DOD practice: `Voxel Mesh Tuner` EditorWindow reads/writes unmanaged tuning in Play Mode | Alternative rejected: recompilation/ScriptableObject-only tuning | Estimate: designer iteration saved, 0 us gameplay hot path
- [x] Task 19 CSV_OVERRIDE_INGESTOR | DOD practice: zero-GC byte parser plus timestamp-gated polling for `meshing_profiles.csv` updates tuning and force-remesh version | Alternative rejected: JSON/managed CSV allocations in runtime path | Estimate: cold 0.1-0.8 ms load, 0 us extraction hot path
- [x] Task 20 GIZMO_WIREFRAME_DEBUGGER | DOD practice: SceneView hook draws yellow wire triangles from raw `float3` workspace when `Show Raw Extraction` is enabled | Alternative rejected: runtime debug GameObjects | Estimate: 0 us player hot path

## Iteration Log

### Loop 0 - Active Prompt Rebind

- Detected duplicate `SHINOBU_61` prompt collision in `CURRENT_BATCH.md`.
- Archived stale Apex/Leviathan `Status`, `Rationale`, `LOG`, and `SELF_AUDIT` files under `_APEX_LEVIATHAN_ARCHIVE_20260518`.
- Rebound active work to `VOXEL_SURFACE_NETS_ARCHITECT`.
- Runtime code not touched yet.
- Compile verification pending; no `dotnet build` launched.

### Loop 1 - Tasks 01-05

- Added runtime asmdef and aligned contracts.
- Actual `rg --files` scan over `Docs/Archive` and `Assets/StreamingAssets` found no `surface_nets_lut.h8bin` or `marching_cubes_edge_tables.bin`; fallback edge masks are required.
- Added 256-case emergency edge mask generation and DataVault lookup handle.
- Added `MockVoxelDensityArray` and Burst hollow-sphere density generator.
- Static scan passed for `new Mesh`, Mesh setters, `Pack=1`, DTO properties, `UnityEngine.Random`, `foreach`, and sibling domain references.
- Compile verification blocked by guard: CPU sample was 100% and external `csc.exe`/`dotnet` processes were active.

### Loop 2 - Tasks 06-10

- Added Surface Nets extraction job, lookup mask routing, one-voxel ghost sampling, frustum priority job, and two-chunks/frame tuning clamp.
- Re-read active prompt before this loop.
- Compile verification still blocked by CPU/process guard; no `dotnet build` launched.

### Loop 3 - Tasks 11-15

- Added continuous quality stride/decimation, packed material blend, dirty-signal job, physics bake request DTO/job, and AUP AABB shift job.
- Physics bake is intentionally staged as a request because Surface Nets owns no managed Mesh and must not call Physics runtime directly.
- Static compile-wall scan found no direct AI/VFX/Caves/Gameplay/Audio/Flora/Fauna/Physics domain references.

### Loop 4 - Tasks 16-20

- Added DataVault workspace hydration, telemetry ring, dump writer, CSV parser, GPU upload dispatcher, EditorWindow, and raw extraction SceneView debugger.
- `git diff --check` over touched files passed.
- Static forbidden API scans remained clean.

### Loop 5 - Self-Audit Pass

- Verified `VoxelVertexDTO` byte layout: 32 bytes exactly.
- Verified all new jobs include `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.
- Verified runtime asmdef references only Core.Contracts, Core.Memory, and Unity Burst/Collections/Jobs/Mathematics.
- Fresh compiler proof is pending only because AGENTS guard forbids launching a new compiler while CPU >50% or `csc.exe` is running.

### Loop 6 - Ultra-Think Hardening Pass

- Re-read active XML prompt, `Rationale_SHINOBU_61.md`, and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` before editing.
- Corrected low-quality sampling anchor: `GlobalQualityWeight=0.2` now resolves to exact 25% sampling via stride 4; full quality remains stride 1.
- Added deterministic 5..60 Hz schedule cadence for non-urgent extraction; dirty/laser priority chunks bypass the cadence to preserve the under-3-frame remesh target.
- Moved indirect draw args generation into `SurfaceNetExtractionJob`; upload only copies the job-written args into the indirect `GraphicsBuffer`.
- Removed upload-time `GraphicsBuffer` allocation from `TryUpload`; buffers must be initialized during boot/prewarm.
- Added optional `VoxelSurfaceHzbCullJob` and vault HZB tile buffer for CPU-downloaded depth pyramid occlusion before BRG/procedural dispatch.
- Added timestamp-gated CSV polling so designer edits to `meshing_profiles.csv` can trigger unmanaged tuning reload without managed row allocations.
- Static forbidden API scans remained clean; jobs now count 7/7 with mandated Burst flags.
- Compile verification remained blocked by guard: CPU sample was 100% and external `csc.exe`/`dotnet` were active.

### Loop 7 - CS1612 Upload Ref Safety Pass

- Found the same class of risk previously seen in other SHINOBU work: `TryUpload(in VoxelSurfaceNetsVaultBuffers ...)` mutates `NativeArray` views through an `in` parameter.
- Removed `in` from the upload dispatcher parameter so state-stage writes mutate the intended vault view without readonly defensive-copy ambiguity.
- Corrected upload state reporting so `BufferSet` records the buffer actually uploaded, not the next write set.
- Static forbidden API scans and `git diff --check` remained clean after the patch.
- Fresh compile proof remains blocked: latest guard sample was 100% CPU with external `dotnet` processes active. No `dotnet build` launched.

### Loop 8 - Active Log Hygiene Pass

- Detected an Apex duplicate-ID continuation pointer in the active voxel log after the final readback.
- Preserved that block in `LOG_SHINOBU_61_APEX_LEVIATHAN_ARCHIVE_20260518.md`.
- Removed the cross-domain pointer from active `LOG_SHINOBU_61.md` and added a Surface Nets hygiene entry.
- No runtime code changed in this loop.

### Loop 9 - Explicit Layout and Conservative HZB Pass

- Re-read active `VOXEL_SURFACE_NETS_ARCHITECT` XML, active rationale, active status, and binary payload ledger before editing.
- Replaced runtime DTO layout declarations with `LayoutKind.Explicit` and `FieldOffset` attributes. `VoxelVertexDTO` is fixed at 32B; state, telemetry, AABB, and modified-signal DTOs are fixed at 64B.
- Corrected `VoxelSurfacePhysicsBakeRequestDTO` to a provable 32B layout: `MeshId` 0, `ChunkIndex` 4, `ChunkHash` 8, `Version` 12, flags 16..19, `_pad2` 20, `_pad1` 24. This removes the previous sequential-layout risk around 8-byte `ulong` alignment.
- Hardened `VoxelSurfaceHzbCullJob` from center-point depth to conservative projected AABB culling. It projects all 8 AABB corners, samples screen-rect HZB corner/center tiles, and fails open when projection is invalid or crosses near-plane.
- Removed four per-vertex tetra-vector normalizations by using pre-normalized tetra constants; mock SDF distance now uses guarded `rsqrt`; `ClearArray<T>` now requires `unmanaged`.
- Static evidence after patch: `git diff --check` passed; forbidden scan found no `Pack=1`, `Sequential`, DTO properties, managed Mesh API, `foreach`, or `UnityEngine.Random`; Burst directive scan still shows 7/7 jobs with the mandated flags.
- Compile verification remained blocked by guard: latest CPU sample was 91% and external `csc.exe`/`dotnet` processes were active. No `dotnet build` launched.
