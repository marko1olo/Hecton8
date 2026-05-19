# Rationale_SHINOBU_61

Date: 2026-05-19
Domain: ECHELON 2 WORLD GENERATION & TERRAIN / Voxel Surface Nets Meshing
Evidence status: LOOP 10 MAPPED GRAPHICSBUFFER BURST COPY APPLIED; STATIC RECHECK PASSED; ROSLYN/UNITY PENDING CPU GUARD

## [ANALYSIS] Runtime Shape Before Code

Problem: Laser cave drilling stutters when SDF remesh work crosses managed Unity Mesh APIs or performs large CPU validation/copy work on the main thread.

Solution: Keep Surface Nets extraction and mapped GPU upload in an isolated `Hecton8.World.VoxelSurfaceNets` assembly. Data comes from vault-owned native buffers, extraction writes 32B GPU-ready vertices and uint indices, and the GPU upload path writes into prewarmed `GraphicsBuffer.LockBufferForWrite` views through a Burst copy job.

Rejected Alternatives: `new Mesh()`, `mesh.SetVertices()`, `mesh.RecalculateNormals()`, `MeshCollider` hot baking, direct Agent 05/54/76 references, managed event lists, and binary `IsLowEnd` switches.

Scalability potential: Low uses stride-4 SDF sampling and center-biased vertices; Middle increases sampling and normal fidelity; High uses full `32^3` cells; Ultra spends saved CPU on richer shader-fed packed material scalars and debug/telemetry, not heavier physics truth.

Hardware Impact: Expected i3/MX350/Quest-class gain is 300-900 us during laser remesh bursts versus managed mesh rebuilds, plus 200-600 us upload-burst risk reduction after moving the big copy off the caller thread. Profiler proof is still pending.

## Decision 00 - Duplicate-ID Prompt Hygiene

Problem: `CURRENT_BATCH.md` contains duplicate `SHINOBU_61` prompts. Active user instruction is Voxel Surface Nets, while active files had been overwritten by Apex evidence.

Solution: Bind work to `role="VOXEL_SURFACE_NETS_ARCHITECT"` and restore active docs from the Surface Nets archive plus current source facts.

Rejected Alternatives: Mixing Apex and Surface Nets evidence in one active status/rationale was rejected as audit contamination.

Scalability potential: 0 runtime cost; protects domain routing.

Hardware Impact: 0 us frame impact.

## Decision 01 - DataVault Workspace

Problem: The XML mentions preallocated lists, but project law requires persistent native memory from `GlobalDataVault`.

Solution: Reserve local-cast buffer IDs `70780-70797` for density, vertices, indices, cell map, states, tuning, telemetry, edge masks, raw debug vertices, AABBs, dirty signals, priorities, indirect args, mock config, physics bake requests, and HZB tiles.

Rejected Alternatives: Private persistent `NativeList<T>` fields and edits to shared `BufferID` enum.

Scalability potential: All quality levels use the same fixed workspace; quality mutates math and counts only.

Hardware Impact: Avoids allocation churn during carve bursts; estimated 100-500 us saved per remesh wave plus 0 GC.

## Decision 02 - Surface Nets Dear Lie

Problem: Full MC triangle-table output, interior faces, CPU triplanar UVs, and MeshCollider baking would spend time where the player cannot see it.

Solution: Emit one centroid vertex per sign-crossing cell, skip fully solid/void cells, output planar UVs, and pack biome/quality scalars for UberNoir shader world-space material fakery.

Rejected Alternatives: Managed Marching Cubes lists, CPU triplanar projection, and hidden rock geometry.

Scalability potential: Low uses 25% density resolution and center bias; Ultra keeps full topology and richer packed data.

Hardware Impact: Expected 300-900 us saved versus managed mesh generation plus normal recalculation in carve bursts.

## Decision 03 - Continuous GlobalQualityWeight

Problem: Thermal response cannot be low/high branching.

Solution: Saturate and smooth `GlobalQualityWeight`; sample ratio lerps 0.25..1.0, stride resolves 4..1, non-urgent cadence breathes 5..60 Hz, and `DecimationAggression` biases vertices toward cell centers at low quality. Dirty laser chunks bypass cadence.

Rejected Alternatives: `if (IsLowEnd)` switches and disabling chunks.

Scalability potential: Low/Middle/High/Ultra form a continuum, not a dichotomy.

Hardware Impact: Low quality can reduce active cell visits and GPU vertex bandwidth by roughly 60-75%.

## Decision 04 - Explicit ARM64/GPU Layout

Problem: Sequential runtime DTOs with comments are not byte-offset proof.

Solution: Use `LayoutKind.Explicit` and `FieldOffset` for hot DTOs. `VoxelVertexDTO` is 32B exactly; state/telemetry/AABB/dirty signal DTOs are 64B; physics bake request is 32B with the 8-byte pad at offset 24.

Rejected Alternatives: `Pack=1`, implicit sequential layout, or "trust comments."

Scalability potential: Layout is tier-independent.

Hardware Impact: Prevents ARM64 unaligned trap/cache drift; correctness and predictable SIMD/GPU stride.

## Decision 05 - GraphicsBuffer Upload Boundary

Problem: The prior dispatcher removed Mesh APIs but still performed a large `UnsafeUtility.MemCpy` on the caller/main thread after locking the `GraphicsBuffer`.

Solution: Add `VoxelSurfaceGpuUploadCopyJob`. `TryBeginUpload` locks mapped vertex/index/indirect buffers and schedules the copy job. `TryFinalizeUpload` only unlocks and publishes if the caller-provided dependency is already completed. The dispatcher never calls `JobHandle.Complete()`.

Rejected Alternatives: Main-thread MemCpy, `Mesh.SetVertexBufferData`, synchronous one-shot upload, and upload-time buffer allocation. Legacy `TryUpload` now returns false without side effects to prevent a caller from accidentally leaving a GraphicsBuffer locked.

Scalability potential: Low uploads fewer vertices from coarse extraction; Ultra uploads dense packed DTOs through the same bridge.

Hardware Impact: Removes the biggest remaining caller-thread copy from laser drilling remesh bursts; expected 200-600 us risk reduction pending profiler proof.

## Decision 06 - HZB Dear Lie

Problem: Frustum priority alone can still upload/draw chunks hidden by nearer cave walls. Center-only HZB cull can create holes.

Solution: Project all 8 AABB corners, build a conservative screen rect, sample HZB at four corners plus center, and only cull when all samples agree. Unsafe projection fails open.

Rejected Alternatives: No occlusion, center-only HZB, and variable-cost full screen-rect iteration.

Scalability potential: Low avoids hidden upload/draw; High/Ultra spend saved cycles on visible material richness.

Hardware Impact: Scene-dependent 100-500 us saved in occluded cave turns, pending Frame Debugger/profiler.

## Decision 07 - Physics Bake as Request

Problem: XML asks for background bake, but this module owns no managed `Mesh` and must not call Unity Physics from Burst.

Solution: Emit `VoxelSurfacePhysicsBakeRequestDTO` rows for a physics-owned bridge. Surface Nets never bakes or assigns `MeshCollider`.

Rejected Alternatives: Direct `Physics.BakeMesh`, `MeshCollider.sharedMesh`, or sibling Physics runtime reference.

Scalability potential: Low can remain SDF-query-only; higher quality can request colliders for visible static chunks through the owner bridge.

Hardware Impact: Avoids known 50 ms main-thread collider spike in this domain.

## Decision 08 - Black Box and Endianness

Problem: Slow extraction or NaN needs fixed forensic evidence without managed logs.

Solution: 300-frame 64B telemetry ring and cold endian-marked dump files `Dump_MESH_SURGEON.bin`/`Dump_SHINOBU_61.bin` with marker `0x01020304`.

Rejected Alternatives: `Debug.Log`, string telemetry, ambiguous dump headers.

Scalability potential: Fixed memory at all qualities.

Hardware Impact: Hot path is fixed NativeArray writes only; avoids 30-100 us managed log risk during faults.

## Decision 09 - Human Control

Problem: Designers need tuning without recompiling.

Solution: Runtime exposes unmanaged tuning; editor assembly owns `Voxel Mesh Tuner`, timestamp-polled `meshing_profiles.csv`, dump trigger, and raw wireframe debug.

Rejected Alternatives: runtime UI, ScriptableObject-only tuning, and runtime direct `Hecton8.Core` reference.

Scalability potential: Same tuning DTO controls Low/Middle/High/Ultra.

Hardware Impact: 0 us player hot path.

## Decision 10 - Verification Boundary

Problem: Compiler/build proof is required eventually, but user and AGENTS forbid launching `dotnet build` while CPU is >50% or compiler processes are active.

Solution: Static scans passed after Loop 10; defer Roslyn/Unity compile until CPU/process guard clears.

Rejected Alternatives: Launching a second compiler to fake proof.

Scalability potential: No runtime impact.

Hardware Impact: Protects developer machine from compile contention. Latest guard sample reported CPU at 100%, so no `dotnet build` was launched.
