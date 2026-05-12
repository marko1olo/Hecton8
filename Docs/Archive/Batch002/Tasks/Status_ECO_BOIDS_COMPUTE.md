# Status_ECO_BOIDS_COMPUTE

Status: VERIFIED MASTER GRADE
Verification Boundary: ECO_BOIDS_COMPUTE code path verified; global project compile remains blocked by external non-boid errors.
Prompt ID: ECO_BOIDS_COMPUTE
Identity: SWARM_DIRECTOR
Domain: ECHELON 3: FLORA, FAUNA & BIOTA
Task Count: 15

## Mandates Loaded

- AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt
- GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- GPU_Compute_Warp_Sizing_Mobile.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt

## Checklist

- [x] Task 1. Compute Spatial Hash in HLSL.
  DOD: `BoidSimulation.compute` has `ClearSpatialGrid`, `BuildSpatialGrid`, negative-space-safe origin hashing, finite guards, bounded cell count, and 64-thread kernels.
  Rejected Alternative: CPU-side spatial hash and global/tiled O(N^2) neighbor scan.
  Microsecond Estimate: Pending profiler evidence; no fake metric recorded. Expected savings come from removing global neighbor scans.

- [x] Task 2. Bitonic Sort / Prefix Sum or atomic grouping.
  DOD: Implemented atomic-add prefix grouping through `RWByteAddressBuffer _SpatialGridCounts` and fixed-slot `RWStructuredBuffer<uint> _SpatialGridCells`.
  Rejected Alternative: Bitonic sort, because it adds multiple global passes before density data proves the atomic path insufficient.
  Microsecond Estimate: Pending profiler evidence; one build pass replaces a sort pipeline.

- [x] Task 3. Flocking kernels.
  DOD: `CSMain` reads 27 neighboring spatial cells through `groupshared uint sharedSpatialIndices[32]` staging and keeps separation/alignment/cohesion local to the hash grid.
  Rejected Alternative: Re-enable the full-tile O(N^2) neighbor scan.
  Microsecond Estimate: Pending profiler evidence. Expected win is reduced global buffer traffic per neighbor lookup.

- [x] Task 4. SDF obstacle avoidance.
  DOD: `HectonBoidController` binds the active `HectonCaveVoxelLightingVolume` GPU SDF payload or a 2x2x2 fallback `Texture3D`; `BoidSimulation.compute` samples signed distance and repels along a cheap finite-difference normal.
  Rejected Alternative: Per-boid physics raycasts or CPU obstacle queries.
  Microsecond Estimate: Pending profiler evidence. Cost is bounded texture samples in compute, not CPU queries.

- [x] Task 5. Flow field advection.
  DOD: Generic boids bind `HectonFluidEngine.TryGetGpuAbyssalFlowFieldBuffer` and sample the published abyssal flow buffer in compute. The implementation uses the project's existing buffer contract instead of inventing a parallel `Texture3D` asset path.
  Rejected Alternative: CPU current volumes or a fake new flow texture that would duplicate fluid ownership.
  Microsecond Estimate: Pending profiler evidence. Expected cost is one buffer sample for active cells.

- [x] Task 6. Predator evasion.
  DOD: Controller exposes a fixed 16-slot predator AUP upload lane; compute resolves predator escape, panic falloff, and velocity override without per-predator GameObject scans.
  Rejected Alternative: Scene searches and per-predator MonoBehaviour callbacks.
  Microsecond Estimate: Pending profiler evidence. Upper bound is 16 cheap vector checks per boid.

- [x] Task 7. Batch Renderer Group / indirect draw.
  DOD: Generic boids now render through `Graphics.RenderMeshIndirect` with a persistent raw `GraphicsBuffer.IndirectDrawIndexedArgs` buffer; mesh topology fields are uploaded CPU-side only on mesh change and instance count is GPU-owned.
  Rejected Alternative: Keeping `Graphics.RenderMeshPrimitives` and CPU instance count submission.
  Microsecond Estimate: Pending profiler evidence. Expected CPU saving is draw submission decoupled from CPU-visible boid count.

- [x] Task 8. VAT animation speed.
  DOD: `BoidData` now carries `panic/stateFlags` matching `BoidFishInstanced.shader`, which already remaps speed into VAT playback and tail frequency. The compute lane now feeds the shader-compatible metadata instead of padding.
  Rejected Alternative: CPU animation speed updates.
  Microsecond Estimate: 0 CPU animation updates by design; GPU shader cost pending profiler evidence.

- [x] Task 9. Compute frustum culling.
  DOD: Added `ClearVisibleIndirectArgs` and `CullVisibleBoids` compute kernels. Culling compacts visible boid IDs into `_VisibleBoidIndices` and atomically increments the indirect draw instance count before render.
  Rejected Alternative: CPU AABB frustum test as the final visibility path or per-boid CPU culling.
  Microsecond Estimate: Pending profiler evidence. Expected cost is one 64-thread dispatch over boids plus six plane tests per boid.

- [x] Task 10. Math LOD.
  DOD: Controller uploads `_BoidMathLodMode` from `DistanceMath.ResolveMathLodMode(GlobalRegistry.ScalabilityTier)`; compute disables social alignment/cohesion on low tier while retaining cheap separation.
  Rejected Alternative: Same full flocking math on all scalability tiers.
  Microsecond Estimate: Pending profiler evidence. Low tier skips two social accumulators per neighbor.

- [x] Task 11. Bubble / scatter integration.
  DOD: Compute tracks `panic` and writes bit 0 of `stateFlags` when acceleration/predator/ping panic is active, giving VFX/render lanes a GPU-owned scatter flag.
  Rejected Alternative: CPU event spawn per fish.
  Microsecond Estimate: 0 CPU fan-out by design; exact GPU cost pending profiler evidence.

- [x] Task 12. No CPU readback.
  DOD: Edited boid lane uses persistent `GraphicsBuffer` dispatch/render binding only. Recon grep found no `GetData`, `AsyncGPUReadback`, or readback request in the edited generic boid files.
  Rejected Alternative: CPU inspection of boid buffers.
  Microsecond Estimate: 0 us readback by design; profiler confirmation still pending runtime.

- [x] Task 13. Ping dispersion.
  DOD: `HectonBoidController` registers as `IAcousticPingEventListener` with `PhysicsEventBus`; compute applies a radial ping kick and panic scalar via fixed uniforms.
  Rejected Alternative: CPU signal fan-out to boid objects.
  Microsecond Estimate: Pending profiler evidence. CPU cost is one listener event, not per-boid work.

- [x] Task 14. Reconnaissance protocol.
  DOD: `Assets/_Project/Scripts` scanned for real `void Update()` declarations in flock/boid/school/swarm files. No legacy Update-based generic flocking file was selected for rewrite.
  Rejected Alternative: Assumption without scan.
  Microsecond Estimate: Static scan only; runtime cost 0 us.

- [x] Task 15. Omega compile check. [BLOCKED BY DEPENDENCY]
  DOD: `HectonBoidController.cs` validates with 0 errors after Omega polish. Scoped scans found no `RenderMeshPrimitives`, dead CPU `CheckFrustumVisibility`, `GetData`, `AsyncGPUReadback`, or readback references in the edited boid files. Unity console reports no `HectonBoidController.cs`, `BoidSimulation.compute`, or `BoidFishInstanced.shader` errors; current global compile is blocked by unrelated `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs(2534,13)` missing `WritePowerBlackBoxSample`.
  Rejected Alternative: Claim clean compile while console contains hard errors outside this prompt.
  Microsecond Estimate: Not applicable.

## Loop Notes

- Loop 1: Prompt extracted, domain loaded, mandates read, generic boid lane selected.
- Loop 2: Implemented HLSL spatial grid clear/build and 27-cell neighborhood traversal.
- Loop 3: Re-extracted prompt after tasks 1-3; bound persistent C# grid buffers and dispatch order.
- Loop 4: Import surfaced a BoidSimulation zero-iteration loop warning; dead legacy tile loop was preprocessor-disabled.
- Loop 5: Recon scan completed; Unity compile remained blocked by unrelated global errors.
- Loop 6: Added shared-memory spatial index staging, cave SDF, abyssal flow, predator AUP slots, ping panic, panic flag, and Math LOD.
- Loop 7: Fixed own compile errors (`Texture3D.GetRawTextureData` and `HectonFluidEngine` namespace reference), recompiled, and verified only external compile blockers remain.
- Loop 8: Added GPU visible-index culling and `Graphics.RenderMeshIndirect`; verified `HectonBoidController.cs` with 0 errors and current Unity console has only external `VoxelDeltaProcessor.cs` blockers.
- Loop 9: Executed Omega polish. Removed stale primitive-draw documentation and dead CPU AABB culling method; repeated zero-readback/dead-path scans; Unity validation remains 0 errors for `HectonBoidController.cs`. Current global blocker is external power-domain missing method `WritePowerBlackBoxSample`.
