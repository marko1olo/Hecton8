# LOG_SHINOBU_61

Date: 2026-05-18

## Session Start

What was wrong: Active `SHINOBU_61` files were contaminated by an older duplicate-ID Apex predator assignment while the current user request is voxel Surface Nets meshing.

What was done: Archived stale Apex files and started fresh Surface Nets status/rationale/log files. No runtime code was modified before active prompt, domain, mandates, and binary-ledger context were re-read.

Cinematic Cheats used: Planned Dear Lie is to skip unseen solid-interior geometry, process at most two chunks/frame by frustum priority, use planar UVs and packed biome scalars while UberNoir shaders fake seamless 3D material continuity.

Exact Microseconds saved: 0 us implemented at this point; expected target is 300-900 us saved per dynamic remesh burst by bypassing managed Mesh rebuild paths.

## Surface Nets Implementation Pass

What was wrong: No isolated Surface Nets module existed for the active SHINOBU_61 voxel prompt. The available status history belonged to a duplicate-ID Apex prompt, and the terrain remesh path risked standard Unity Mesh API stutter during laser drilling.

What was done: Added `Hecton8.World.VoxelSurfaceNets` runtime assembly with aligned DTOs, DataVault workspace handles, emergency lookup masks, mock SDF density generation, Burst Surface Nets extraction, tetrahedral normal packing, dirty chunk handling, AUP AABB shift, telemetry/dumps, CSV tuning, and a direct `GraphicsBuffer` upload dispatcher. Added `Voxel Mesh Tuner` EditorWindow with unmanaged sliders and raw extraction wireframe draw.

Cinematic Cheats used: Fully solid/void cells are not geometry. CPU UVs are planar only; UberNoir shader world-space sampling owns the expensive material lie. Frustum scoring limits urgent meshing to the chunks the player can plausibly see. Low quality reduces sampling density and biases vertices to centers instead of simulating expensive topology preservation.

Exact Microseconds saved: Estimated 300-900 us per laser remesh burst by deleting managed Mesh rebuild/recalculate paths; 200-600 us on upload by using `GraphicsBuffer.LockBufferForWrite` plus `UnsafeUtility.MemCpy`; 120-300 us by replacing triangle-normal averaging with tetra SDF gradient packing; up to 50 ms main-thread spike avoided by staging collider bake requests instead of baking in this domain. Profiler proof is pending because compile/run was blocked by CPU/process guard.

## Ultra-Think Hardening Pass

What was wrong: The first implementation still let `TryUpload` allocate `GraphicsBuffer`s if boot prewarm was missed, computed indirect args in upload code, lacked HZB occlusion, treated `GlobalQualityWeight=0.2` too softly for the required 25% sampling collapse, and only loaded CSV on command.

What was done: `TryUpload` now requires preinitialized buffers. `SurfaceNetExtractionJob` writes indirect args. `VoxelSurfaceHzbCullJob` consumes vault HZB tiles and marks occluded AABBs before draw dispatch. Non-urgent extraction cadence now scales 5..60 Hz while laser-dirty chunks bypass cadence. CSV has timestamp-gated polling through unmanaged tuning state.

Cinematic Cheats used: Geometry behind the camera or behind HZB depth is deprioritized/culled; hidden rock still emits no vertices; low-quality mesh detail melts through sampling stride and center bias, not through a visible binary tier flip.

Exact Microseconds saved: Added expected 100-500 us scene-dependent savings during occluded cave turnaround frames; removed possible upload-time allocation spike; preserved 0 us/player hot path for CSV polling.

## Upload Ref Safety Pass

What was wrong: `TryUpload` accepted the vault buffer view by `in` while mutating `States`, creating a CS1612/readonly-copy risk.

What was done: Removed `in` from the upload dispatcher path only, preserved `in` on pure read/schedule helpers, and corrected `BufferSet` telemetry to report the uploaded buffer.

Cinematic Cheats used: None; this was correctness hardening.

Exact Microseconds saved: 0 us. Prevents invalid state mutation semantics and avoids a potential upload-state stall.

## Explicit Layout and Conservative HZB Pass

What was wrong: Hot DTOs used `LayoutKind.Sequential` with manual `Size`, which is not a byte-offset proof. `VoxelSurfacePhysicsBakeRequestDTO` had a risky byte/ushort/ulong order under a requested 32B size. HZB occlusion used only the AABB center, which is too aggressive for large terrain chunks.

What was done: Converted Surface Nets DTOs to `LayoutKind.Explicit` with `FieldOffset` on every field. Fixed the physics bake request to a provable 32B ARM64-safe layout. HZB culling now projects all 8 AABB corners, builds a conservative screen rect, samples corner/center HZB tiles, and fails open when clip projection is unsafe. Tetra normals use pre-normalized constants; mock SDF distance uses guarded `rsqrt`; vault clear now requires unmanaged element types.

Cinematic Cheats used: HZB remains a bounded five-sample Dear Lie instead of an unbounded per-tile visibility solve. It is intentionally conservative: questionable chunks stay visible rather than causing terrain holes. The shader still owns material richness; CPU keeps planar UVs and packed scalars.

Exact Microseconds saved: Removing four normalizations per emitted vertex reduces ALU in the dense extraction path. HZB savings remain scene-dependent at the previously estimated 100-500 us during occluded turn frames. Compile proof was not run because the CPU guard reported 91% load with external `csc.exe`/`dotnet` active.

## Log Hygiene Pass

What was wrong: A duplicate-ID Apex continuation pointer appeared in the active voxel Surface Nets log, contaminating the strict parsing boundary.

What was done: Moved that pointer into `LOG_SHINOBU_61_APEX_LEVIATHAN_ARCHIVE_20260518.md` and kept the active log scoped to Surface Nets.

Cinematic Cheats used: None.

Exact Microseconds saved: 0 us. Prevents wrong-domain audit contamination.

## 2026-05-19 - Mapped GraphicsBuffer Burst Copy Pass

What was wrong: The upload evidence needed to match the current source: mapped `GraphicsBuffer` views must not be copied synchronously on the main thread. Active `SHINOBU_61` files are also contested by a duplicate Apex prompt, so this Surface Nets trail is preserved in the Surface Nets archive.

What was done: Verified the current two-phase upload path. `TryBeginUpload(...)` locks prewarmed vertex/index/indirect buffers and schedules `VoxelSurfaceGpuUploadCopyJob`; `TryFinalizeUpload(...)` unlocks only after the caller-owned dependency is completed. Static scans show no managed Mesh API, no `GraphicsBuffer.SetData`, no arbitrary `JobHandle.Complete`, and 8/8 mandated Burst job attributes in the Surface Nets runtime.

Cinematic Cheats used: Upload remains a pure data blast. Hidden geometry was already rejected earlier by sign-crossing cell extraction and HZB/frustum priority; the shader still owns material richness through planar UV plus packed vertex scalars.

Exact Microseconds saved: Expected 200-600 us per upload burst versus managed Mesh/update validation remains the working estimate. No profiler number is claimed because compiler/Unity proof is blocked by CPU guard; no `dotnet build` was launched.

## 2026-05-19 - Edge-Gated Quad Emission Pass

What was wrong: The Surface Nets second pass emitted quads from four adjacent generated cell vertices without rechecking that the shared grid-edge crossed the iso-surface. That can create false internal triangles after dense laser carving and can corrupt raw wireframe evidence.

What was done: Added exact X/Y/Z sign-changing edge gates before quad emission. Added sign-based winding and made raw debug triangle capture mirror the final index order.

Cinematic Cheats used: None added. This removes false geometry from the existing Dear Lie: only visible sign-crossing surface cells become triangles; solid interior rock remains non-geometry.

Exact Microseconds saved: No measured profiler number. Expected savings are scene-dependent index/overdraw reduction in carved chunks; static scans remained clean and no compiler was launched because CPU sampled at 99.23%.
