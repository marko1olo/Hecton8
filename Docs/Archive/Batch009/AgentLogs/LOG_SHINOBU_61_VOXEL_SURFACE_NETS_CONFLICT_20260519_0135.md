# LOG_SHINOBU_61

Date: 2026-05-19
Domain: VOXEL_SURFACE_NETS_ARCHITECT

## 2026-05-18 - Surface Nets Implementation Pass

What was wrong:
- Active `SHINOBU_61` files had been contaminated by a duplicate Apex prompt while the current assignment is voxel Surface Nets meshing.
- No isolated Surface Nets module existed to convert SDF data into GPU-ready geometry without managed Mesh churn during laser drilling.

What was done:
- Added `Hecton8.World.VoxelSurfaceNets` runtime assembly with explicit DTOs, DataVault buffer handles, emergency edge masks, mock SDF density generation, Burst extraction, tetrahedral normal packing, dirty signal handling, AUP AABB shift, telemetry/dumps, CSV tuning, and a direct GraphicsBuffer dispatcher.
- Added `Voxel Mesh Tuner` EditorWindow with unmanaged sliders and raw extraction wireframe draw.

Cinematic Cheats used:
- Fully solid/void cells emit no geometry.
- CPU UVs are planar; UberNoir shader owns world-space material continuity.
- Frustum priority processes visible chunks first and caps work to two chunks/frame.

Exact Microseconds saved:
- Estimated 300-900 us per laser remesh burst by deleting managed Mesh rebuild/recalculate paths.
- Estimated 120-300 us by replacing triangle-normal averaging with tetra SDF gradient packing.
- Up to 50 ms main-thread collider bake avoided by staging physics bake requests instead of baking in this domain.

## 2026-05-18 - Explicit Layout and Conservative HZB Pass

What was wrong:
- Sequential DTOs with manual `Size` were not byte-offset proof.
- HZB occlusion tested only AABB center, unsafe for large chunks.

What was done:
- Converted Surface Nets hot DTOs to `LayoutKind.Explicit` with `FieldOffset`.
- Fixed `VoxelSurfacePhysicsBakeRequestDTO` to provable 32B ARM64-safe layout.
- HZB culling now projects all 8 AABB corners, samples four screen-rect corners plus center, and fails open on invalid projection.

Cinematic Cheats used:
- HZB is a bounded five-sample visibility lie, conservative enough to avoid terrain holes.

Exact Microseconds saved:
- Removing four normalizations per emitted vertex cuts dense extraction ALU.
- HZB savings remain scene-dependent at an estimated 100-500 us during occluded cave turns.

## 2026-05-19 - Mapped GraphicsBuffer Burst Copy

What was wrong:
- The dispatcher no longer used managed Mesh APIs, but still copied vertex/index data into locked `GraphicsBuffer` memory synchronously on the caller thread.
- The one-shot `TryUpload` shape encouraged hidden stalls or locked-buffer lifetime mistakes.

What was done:
- Added `VoxelSurfaceGpuUploadCopyJob`, a Burst `IJob` that copies vault vertices, indices, and indirect args directly into mapped `GraphicsBuffer` NativeArray views.
- Added two-phase upload API: `TryBeginUpload` locks prewarmed buffers and returns a `JobHandle`; `TryFinalizeUpload` unlocks/publishes only after that dependency is already completed.
- Disabled legacy `TryUpload` by making it side-effect-free and returning false; callers must use the two-phase path.
- Re-ran static forbidden API scans: no managed Mesh API, `Pack=1`, `LayoutKind.Sequential`, hot DTO get/set properties, `JobHandle.Complete`, LINQ/`foreach`, Physics casts, runtime private native collection allocation, binary hardware switches, or sibling runtime domain references.
- Burst scan found 8/8 jobs with mandated flags. `git diff --check` passed; only LF-to-CRLF warnings were reported.

Cinematic Cheats used:
- No new physical simulation. The upload bridge preserves the existing lie: only visible sign-crossing surface data reaches the GPU, while hidden rock and CPU material projection never exist.

Exact Microseconds saved:
- Main-thread upload copy risk reduced by an estimated 200-600 us during laser drilling remesh bursts, pending profiler proof.
- No `JobHandle.Complete()` was introduced; caller retains dependency ownership.
- Compiler proof remains blocked: latest CPU sample was 100%, so no `dotnet build` was launched.
