# Rationale_SHINOBU_61

Date: 2026-05-18
Domain: ECHELON 2 WORLD GENERATION & TERRAIN / Voxel Surface Nets Meshing
Evidence status: IN PROGRESS; active prompt rebound from duplicate SHINOBU_61 collision.

## [ANALYSIS] Runtime Shape Before Code

Problem: Laser cave drilling stutters when procedural SDF terrain crosses managed mesh APIs or rebuilds geometry on the main thread. Surface extraction must consume DataVault density buffers, produce local-space GPU-ready vertices, and hand off the result without `new Mesh()`, managed `List<T>`, or sibling domain calls.

Solution: Add an isolated `Hecton8.World.VoxelSurfaceNets` runtime assembly. It owns aligned DTO contracts, Burst Surface Nets extraction, tetrahedral SDF normal packing, quality-driven coarse sampling/decimation, dirty-signal remesh flags, AUP-local AABB shift jobs, fixed telemetry, cold CSV/bootstrap/dump helpers, and a `GraphicsBuffer.LockBufferForWrite` upload bridge. Persistent native memory is requested from `GlobalDataVault` through local-cast `BufferID`s; GPU buffers are cold-created and reused.

Rejected Alternatives: Standard Unity `new Mesh()`, `mesh.SetVertices()`, `mesh.RecalculateNormals()`, `MeshCollider` hot baking, `Physics.Raycast` terrain queries, direct Agent 05/54/76 references, managed event lists, and binary `IsLowEnd` switches were rejected because they add GC, compile-wall coupling, or frame spikes during cave carving.

Scalability potential: Low uses coarse SDF sampling and center-biased surface vertices; Middle increases sample density and normal fidelity; High uses full 32x32x32 extraction with tighter smoothing; Ultra spends saved CPU budget on richer vertex material scalars and raw debug/telemetry without changing the authority contract.

Hardware Impact: Expected i3/MX350/Quest-class gain is avoiding managed mesh rebuilds and full-resolution extraction under thermal pressure. Conservative estimate: 300-900 us saved during laser remesh bursts versus `Mesh.SetVertices`/recalculate/validation paths, pending Unity profiler proof.

## Decision 00 - Active Prompt Collision Hygiene

Problem: `CURRENT_BATCH.md` contains two `SHINOBU_61` XML blocks. The pre-existing status/rationale files belonged to the older Apex predator prompt, not the current voxel surface nets assignment.

Solution: Use CLI extraction with `role="VOXEL_SURFACE_NETS_ARCHITECT"` as the binding predicate. Archive the stale Apex files instead of overwriting them, then start fresh Surface Nets status/rationale/log artifacts.

Rejected Alternatives: Mixing Apex and Surface Nets evidence in the same `Status_SHINOBU_61.md` was rejected as audit contamination. Deleting the old work was rejected because it is user/agent history.

Scalability potential: No runtime cost; it prevents wrong-domain decisions from leaking into the meshing architecture.

Hardware Impact: 0 us frame impact. Prevents compile-wall sabotage by ensuring this pass edits only World/VoxelSurfaceNets files.

## Byte Layout Audit Targets

- `VoxelVertexDTO`: 32 bytes. `float3 Position` 0..11, `uint NormalPacked` 12..15, `uint TangentPacked` 16..19, `uint ColorPacked` 20..23, `float2 UV` 24..31.
- `ChunkMeshingStateDTO`: 64 bytes. One cache line for chunk state/counters; public fields only.
- `VoxelMeshingTelemetryEntry`: 64 bytes. One cache line for black-box ring entries.
- Atomic/concurrent counters: if introduced, must use explicit 64-byte padding. Current first pass avoids shared atomics by running one chunk extraction job per workspace.

## Toaster / Middle / High / Ultra Policy

Low: 25% density resolution, center-biased vertices, max 5-12 Hz remesh cadence owned by caller, two chunks/frame hard cap.
Middle: 50-75% density resolution, tetra normals, planar UVs, material blend baked into vertex channels.
High: full cell resolution, smoother intersection centroid, raw debug capture.
Ultra: same deterministic topology contract but richer shader-fed packed data and diagnostics; no heavier CPU physics simulation.

## Decision 01 - DataVault Workspace Instead of NativeList Ownership

Problem: The XML mentions preallocated `NativeList` buffers, but the project mandate says persistent native memory must come from `GlobalDataVault`. Private persistent `NativeList` ownership would fragment memory and violate Vault Law. Actual `rg --files` archaeology found no active `surface_nets_lut.h8bin` or `marching_cubes_edge_tables.bin`.

Solution: Reserve local-cast `BufferID`s `70780-70797` and allocate density, vertices, indices, cell map, states, telemetry, lookup masks, AABBs, dirty signals, indirect args, mock config, physics bake requests, and HZB tiles through `IDataVault`. Runtime code resets counters/length fields; it does not allocate per chunk. `GenerateEmergencyMockTables()` hydrates 256 edge masks when archaeology has no binary payload.

Rejected Alternatives: Private `NativeList<float3>`/`NativeList<int>` fields, managed `List<T>`, and edits to the shared `BufferID` enum were rejected. Enum edits would widen the compile wall; private lists would violate DataVault sovereignty.

Scalability potential: Low/Middle/High/Ultra all use the same fixed workspace. Quality changes mutate sampling density and packed vertex data, not allocation topology.

Hardware Impact: i3/MX350/Quest-class gain is avoiding allocation churn during laser carving bursts; estimated 100-500 us saved per remesh wave plus zero GC.

## Decision 02 - Surface Nets as a Dear Lie Mesh, Not Physics Truth

Problem: Dynamic cave drilling needs visible holes quickly. Full mesh truth, CPU triplanar UVs, interior rock faces, and MeshCollider baking would spend frame time where the player cannot see it.

Solution: `SurfaceNetExtractionJob` emits one vertex only for sign-crossing cells and skips fully solid/void cells. UVs are local planar only. Biome blend and quality are packed into vertex color so UberNoir can fake material continuity on the GPU.

Rejected Alternatives: Marching Cubes triangle-table spam, CPU triplanar projection, and interior face generation were rejected because their cost scales with hidden volume, not visible surface.

Scalability potential: Low samples at 25% resolution and biases vertices to cell centers; Middle/High increase resolution; Ultra keeps full topology and richer shader scalars.

Hardware Impact: Expected low-end gain is 300-900 us versus managed mesh generation plus normal recalculation during carve bursts.

## Decision 03 - Continuous Quality Sampling

Problem: Hardware thermal response cannot be a binary low/high tier. The mesh must degrade continuously as `GlobalQualityWeight` falls.

Solution: Quality is saturated and smoothed; sample ratio lerps from 0.25 to 1.0 and resolves to stride 4..1, with weight 0.2 anchored to exact 25% sampling. Decimation aggression biases Surface Nets vertices toward cell centers at low quality, reducing high-frequency topology without popping a separate code path. Non-urgent extraction cadence also breathes from 5 Hz to 60 Hz; laser-dirty chunks bypass cadence so the carve appears within the 3-frame target.

Rejected Alternatives: `if (IsLowEnd)` branches and disabling chunks were rejected because they cause visible pops and violate the scalability pillar.

Scalability potential: Low uses quarter-resolution SDF; Middle uses half/three-quarter resolution; High/Ultra sample full 32^3 density and preserve richer normal detail.

Hardware Impact: At low quality, active cell visits and vertex output can drop by roughly 60-75%, saving both CPU extraction and GPU vertex bandwidth.

## Decision 04 - GraphicsBuffer Upload Boundary

Problem: Standard Unity Mesh uploads validate and copy through managed/C++ boundaries, causing the exact stutter this agent is assigned to remove.

Solution: `VoxelSurfaceNetsGpuUploadDispatcher` cold-creates double-buffered `GraphicsBuffer` objects during explicit boot/prewarm and uploads vault vertices/indices with `LockBufferForWrite` plus `UnsafeUtility.MemCpy`. `SurfaceNetExtractionJob` writes the indirect draw args into vault memory; upload only copies those args to the GPU.

Rejected Alternatives: `new Mesh()`, `mesh.SetVertices()`, `mesh.SetData()`, `mesh.RecalculateNormals()`, and direct GameObject/MeshFilter assignment were rejected.

Scalability potential: Low uploads fewer vertices from coarser extraction; Ultra uploads dense packed DTOs without changing the bridge.

Hardware Impact: Expected 200-600 us saved during upload bursts, plus reduced driver validation pressure and no upload-time buffer allocation spike.

## Decision 05 - Physics Bake as a Request, Not a Domain Violation

Problem: The XML asks for `Physics.BakeMesh(meshId, false)` in a background Burst job, but this Surface Nets domain deliberately owns no managed `Mesh` and must not reference a sibling Physics runtime. Calling UnityEngine.Physics from Burst would also be AOT-hostile.

Solution: Add `VoxelSurfacePhysicsBakeRequestDTO` and a Burst request job that stages mesh IDs/chunk versions for a physics-owned bridge. This keeps the main thread free of MeshCollider baking and preserves compile-wall isolation.

Rejected Alternatives: Main-thread `MeshCollider` assignment and direct `Physics.BakeMesh` calls from this runtime were rejected. If the physics bridge later consumes the DTO, it can execute the allowed worker bake at its boundary.

Scalability potential: Low can ignore colliders and query SDF math; Middle/High/Ultra can request legacy bake only for visible/static chunks.

Hardware Impact: Avoids the known 50 ms collider bake spike in this domain. The residual cost is shifted to an explicit physics bridge instead of hidden in meshing.

## Decision 06 - Black Box and Endianness

Problem: A slow extraction or NaN needs forensic data. Managed logs are too late and too expensive.

Solution: Write fixed 64-byte telemetry rows into a 300-frame vault ring. Cold dump writes `Dump_MESH_SURGEON.bin` and `Dump_SHINOBU_61.bin` with magic and little-endian marker `0x01020304`, then copies telemetry bytes directly.

Rejected Alternatives: `Debug.Log`, string telemetry, and unmarked binary blobs were rejected because they are GC-heavy or ambiguous under cross-platform hydration.

Scalability potential: All quality levels write the same fixed telemetry; high-end simply produces larger vertex counts and richer debug capture.

Hardware Impact: Hot path pays fixed NativeArray stores only; estimated 30-100 us saved versus managed logging during endurance faults.

## Decision 07 - Editor Control Without Runtime Coupling

Problem: Designers need to tune meshing without recompiling, but runtime cannot reference `GlobalRegistry` or editor assemblies.

Solution: Runtime assembly depends only on Core.Contracts/Core.Memory and Unity job packages. Editor assembly references Core and exposes `Voxel Mesh Tuner`, CSV load, dump trigger, and SceneView raw wireframe draw.

Rejected Alternatives: ScriptableObject-only tuning, runtime UI, and direct Core/GlobalRegistry runtime references were rejected.

Scalability potential: Human tuning can adjust quality, iso threshold, chunk budget, decimation, and debug capture while preserving one runtime DTO.

Hardware Impact: 0 us player hot path; editor-only draw cost is outside player runtime.

## Decision 08 - Verification Boundary

Problem: The code needs compiler proof, but AGENTS explicitly forbids launching a build/compiler while CPU is under work or another compiler is running.

Solution: Static scans were executed immediately. Compile guard checks repeatedly reported CPU above 50%; the latest sample was 100% CPU with external `dotnet` processes active, so no `dotnet build` or targeted compiler was launched.

Rejected Alternatives: Launching a second compiler to fake proof was rejected because it violates the user's direct instruction and project hardware-protection rule.

Scalability potential: No runtime impact.

Hardware Impact: Protects the developer machine from additional compile contention. Compiler proof remains pending until CPU/process guard clears.

## Decision 09 - HZB and Indirect Args Hardening

Problem: Frustum priority alone still sends work for chunks hidden behind nearer terrain, and the previous upload bridge computed indirect args on the CPU side after extraction.

Solution: Add `VoxelSurfaceHzbTileDTO`, a vault-owned HZB tile buffer, and `VoxelSurfaceHzbCullJob` that projects AABB centers in camera-local space and penalizes/marks occluded chunks before draw dispatch. Move indirect args creation into `SurfaceNetExtractionJob` so the Burst extraction result owns index/instance counts.

Rejected Alternatives: CPU-side visibility lists and post-extraction managed draw argument construction were rejected because they leak rendering work back to the main thread and hide occlusion cost in upload code.

Scalability potential: Low quality uses frustum/HZB to avoid spending meshing and vertex bandwidth behind terrain. Middle/High/Ultra can spend saved cycles on richer shader material data and denser visible surfaces.

Hardware Impact: Expected low-end gain is scene-dependent; conservative target is 100-500 us saved during cave turnarounds where occluded chunks would otherwise upload or draw.

## Decision 10 - CSV Polling Without Runtime Watchers

Problem: The first CSV path loaded on demand, but the prompt requires designer edits to be picked up continuously without recompilation.

Solution: Add timestamp-gated `TryPollCsvOverrides()` using vault `LastCsvWriteTicks`. The Editor facade calls it once per second; changed files are parsed into the unmanaged tuning DTO and visible chunks are marked dirty through `ForceRemeshVersion`.

Rejected Alternatives: `FileSystemWatcher`, JSON, managed row models, and per-frame file reads were rejected as GC-heavy or editor/OS fragile.

Scalability potential: Same tuning DTO controls Low/Middle/High/Ultra; no separate tier assets or binary switches are introduced.

Hardware Impact: 0 us extraction hot path; editor/cold poll is a bounded file timestamp check and parser call only when changed.

## Decision 11 - Upload Dispatcher Cannot Mutate Through `in`

Problem: `VoxelSurfaceNetsGpuUploadDispatcher.TryUpload` must update chunk stage flags in the vault state view. Passing `VoxelSurfaceNetsVaultBuffers` as `in` risks C# readonly-copy/CS1612 behavior around mutable `NativeArray` views, the same failure pattern already observed in prior SHINOBU work.

Solution: Remove `in` from `TryUpload` while keeping read-only `in` on pure scheduling/dump helpers. The dispatcher now mutates the explicit transient view and reports the buffer set actually used for upload.

Rejected Alternatives: Keeping `in` and hoping the NativeArray indexer setter survives compiler semantics was rejected because this is exactly the defensive-copy ambiguity the mandate forbids.

Scalability potential: No change to Low/Middle/High/Ultra math. It protects state correctness for all quality levels.

Hardware Impact: 0 us direct frame gain; avoids a compile/runtime state write fault that would strand chunks in `ReadyForUpload` or misreport upload buffers.

## Decision 12 - Explicit DTO Layout and Conservative HZB AABB

Problem: Sequential structs with manual `Size` still leave byte offsets to runtime layout rules. The previous `VoxelSurfacePhysicsBakeRequestDTO` mixed byte flags with a trailing `ulong`, making the requested 32B footprint suspect on ARM64. HZB culling also tested only the AABB center, which can falsely reject a large chunk whose center is behind terrain while its edge is visible.

Solution: Convert all hot Surface Nets DTOs to `LayoutKind.Explicit` with `FieldOffset` on every field. `VoxelVertexDTO` is 32B, `ChunkMeshingStateDTO`, `VoxelMeshingTelemetryEntry`, `VoxelSurfaceAabbDTO`, and `VoxelSurfaceModifiedSignal` are 64B. Physics bake request is fixed to 32B with 8-byte padding at offset 24. `VoxelSurfaceHzbCullJob` now projects all 8 AABB corners in camera-local space, builds a screen rect, samples HZB corner/center tiles, and fails open on invalid/near-plane projection. Tetra normals use pre-normalized constants instead of four runtime normalizations per vertex.

Rejected Alternatives: Keeping `Sequential` plus comments was rejected because it is not proof. Center-only HZB was rejected because it is cheap but can create visible chunk holes. Full per-tile rect iteration was rejected for now because it can turn a visibility job into a variable-cost screen-space loop; five fixed HZB samples are a bounded Dear Lie.

Scalability potential: Low devices keep the bounded five-sample HZB test and avoid hidden chunk upload/draw. Middle/High/Ultra get safer visible-edge retention while still culling chunks that are fully behind terrain. DTO layout is identical across all tiers.

Hardware Impact: ARM64 layout proof prevents unaligned read traps and cache-line drift; expected gain is defensive correctness rather than a new frame-time claim. Removing tetra normalizations saves roughly 4 normalize operations per emitted vertex; on dense chunks this is a measurable ALU reduction pending profiler proof. Conservative HZB can avoid 100-500 us of wasted upload/draw in occluded cave turns while refusing unsafe false culls.

Verification Guard: Post-patch static scans passed, but compiler proof was not launched because the latest guard sample reported 91% CPU load with external `csc.exe` and `dotnet` processes active.

## Decision 13 - Mapped GraphicsBuffer Copy Must Be a Job Boundary

Problem: `GraphicsBuffer.LockBufferForWrite` gives mapped buffer views that can tempt a main-thread `UnsafeUtility.MemCpy`, creating a CPU copy spike exactly where laser drilling is sensitive. The active `SHINOBU_61` audit trail is contested by a duplicate Apex prompt, so voxel evidence is preserved in the Surface Nets archive rather than mixed into Apex files.

Solution: Keep upload as a two-phase state machine. `TryBeginUpload(...)` locks prewarmed vertex/index/indirect `GraphicsBuffer` objects and schedules `VoxelSurfaceGpuUploadCopyJob`; `TryFinalizeUpload(...)` only unlocks after the caller dependency is already completed. Source/destination NativeArray fields in the copy job use `[NoAlias]`, and the caller owns the returned `JobHandle`.

Rejected Alternatives: A synchronous dispatcher-side `MemCpy`, `JobHandle.Complete()`, `Mesh.SetVertexBufferData`, `GraphicsBuffer.SetData`, and a one-shot `TryUpload` that hides the dependency were rejected because they either block the caller thread, reintroduce Unity validation/copy paths, or make dependency graph ownership opaque.

Scalability potential: Low quality uploads fewer vertices because extraction stride and decimation reduce the vertex/index counts before the mapped copy. Middle/High/Ultra use the same upload path and spend higher quality on denser visible surfaces, not a different API.

Hardware Impact: Expected upload burst saving remains 200-600 us versus managed Mesh/update validation. The exact profiler number is pending; static proof shows 8/8 Burst jobs and no forbidden Mesh/SetData/Complete path in the Surface Nets runtime.

## Decision 14 - Surface Nets Quads Must Be Edge-Gated

Problem: The first topology pass emitted a quad whenever the four surrounding cells had generated Surface Nets vertices. That is not strict enough. Surface Nets topology is edge-owned: a quad exists around a grid edge only when that specific edge crosses the iso-surface. Without the edge recheck, dense cave cuts can emit extra interior quads around adjacent active cells, inflating index count and creating potential backface/overdraw artifacts during laser drilling.

Solution: Add `TryResolveEdgeWinding(...)` to sample the exact X/Y/Z grid-edge before emitting a quad. Each quad now requires a sign change on the matching edge. The same edge signs drive winding reversal, and raw extraction debug vertices are copied from the final index order so the Editor wireframe shows the actual triangles uploaded to the GPU.

Rejected Alternatives: Keeping four-cell adjacency alone was rejected because it is a convenience heuristic, not the Surface Nets contract. A post-process triangle cleanup pass was rejected because it adds another O(indices) walk and hides the topology error after the fact. Double-sided duplicate triangles were rejected because they would mask winding at the cost of index bandwidth.

Scalability potential: Low quality benefits most because coarse stride cells no longer produce unnecessary quads from adjacent active cells. Middle/High/Ultra keep denser surfaces but only around real sign-changing edges; richer shader material data remains GPU-side.

Hardware Impact: Expected impact is fewer false indices and less overdraw in high-frequency carved caves. No measured profiler claim is made. Static scans stayed clean, Burst flag count remains 8/8, and compiler proof is still blocked by the CPU guard.
