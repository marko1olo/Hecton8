# Handoff Report — Explorer Reconnaissance & Root-Cause Analysis (M8-M10)

**Authority used**: AGENTS.md; PROJECT_BIBLES.md; voxels.md; terrain.md.  
**Working Directory**: `C:\hades\Hecton8\.agents\explorer_m8_m10`  
**Target Audience**: Worker / Implementer Agent  
**Date**: 2026-07-27  

---

## 1. Observation

Direct observations from codebase inspection:

### R1. Voxel Physics Bake Signal & Kinematic Spawner Integration
- `Assets/_Project/Scripts/HectonVoxelVolume.cs`:
  - Lines 1998–2014: `CommitDeferredColliderChunkUpload(int index)` checks `collider.enabled = _bakeState == VoxelBakeState.Complete;`. Because `_bakeState` is `VoxelBakeState.Baking` during volume rebuilds, `collider.enabled` is set to `false`, causing `if (collider.enabled)` to evaluate to `false` and skipping `WorldChunkPhysicsBakedEvents.TryPublish(in signal)`.
  - Lines 4072–4108: `RefreshBakePresentation()` sets `collider.enabled = true` when `SetBakeState(VoxelBakeState.Complete)` is later called, but `RefreshBakePresentation()` **never calls `WorldChunkPhysicsBakedEvents.TryPublish`**. Thus, `WorldChunkPhysicsBakedSignal` is never published for completed `HectonVoxelVolume` chunks.
  - Line 2011: `TerrainSize = new float3(100f, 100f, 100f)` and `ChunkX = (int)math.floor(pos.x / 100f)` are hardcoded instead of dynamically using `_gridDimension * _voxelSize`.
- `Assets/_Project/Scripts/HectonPlayerSpawner.cs`:
  - Line 399: `if (TryResolveGroundHit(out _hitInfo) && IsSpawnPointPhysicsReady(searchOrigin.x, searchOrigin.y))` passes `searchOrigin.y` (Y height) as the `worldZ` parameter to `IsSpawnPointPhysicsReady`.
  - Line 434: `SpawnSearchResult centerResult = EvaluatePoint(searchOrigin.x, searchOrigin.y);` passes `searchOrigin.y` as `worldZ`.

### R2. Voxel Vertex Color Channel & Shader Blending Audit
- `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs`:
  - Lines 658–667: `PackColor` generates pseudo-random noise hashes for R, G, B channels (`r = hash01 * 255`, `g = blend * 255`, `b = quality * 255`, `a = 255`) and stores it into `ColorPacked` on `VoxelVertexDTO`.
  - Lines 199–202: `float3 normal = CalculateTetraNormal(vertexLocal, voxelSize, quality);` is calculated, but ignored by `PackColor`.
  - Violates URP Cave Shader spec: Red must be Floor weight (`ResolveFloorWeight(normal)`), Green must be Wall weight (`255 - floorByte`), Blue must be 0, Alpha must be Ambient Occlusion (`AO`).
- `Assets/_Project/Scripts/HectonVoxelEngine.cs`:
  - Lines 4185–4188: `ResolveFloorWeight(float3 normal)` accesses `normal.y` without finite checks or normalizing unnormalized/zero vectors.

### R3. Terrain Boundary Guard & Erosion Stability Audit
- `Assets/_Project/Scripts/World/HydraulicErosionJob.cs`:
  - Line 847: `int centerZ = math.clamp((int)math.floor(position.y), writeMinZ + 1, writeMaxX - 2);` uses `writeMaxX - 2` instead of `writeMaxZ - 2`.
  - Lines 894 & 937: `if (!IsInsideWriteWindow(position, writeMinX, writeMinZ, writeMaxX, writeMaxZ)) return 0f;` discards all carried sediment when droplets exit the write window, violating mass conservation and causing un-eroded ridge lines on 32px sub-grid boundaries.
- `Assets/_Project/Scripts/World/WorldProceduralTerrainThermalWeatheringJobs.cs`:
  - Lines 28–32 & 48–55: Outer border cells `[x == 0, z == 0, x == Width - 1, z == Height - 1]` are frozen and `x - 1 > 0` prevents edge exchanges, creating 1-pixel boundary cliffs between streaming terrain tiles.

---

## 2. Logic Chain

1. **R1 Logic Chain**:
   - `HectonVoxelVolume.cs` defers collider uploads during meshing.
   - During meshing, `_bakeState` is `Baking`. `CommitDeferredColliderChunkUpload` tests `_bakeState == Complete`, which is `false`. Colliders are disabled and signals are skipped.
   - When meshing completes and `_bakeState` becomes `Complete`, `RefreshBakePresentation()` enables colliders but omits signal publication.
   - Therefore `WorldChunkPhysicsBakedEvents` receives no signals for voxel volumes.
   - Simultaneously, `HectonPlayerSpawner.cs` passes `(searchOrigin.x, searchOrigin.y)` to `IsSpawnPointPhysicsReady`, querying a point off by hundreds of meters in Z.
   - Together, these two bugs prevent player spawning and kinematic arrest gates from ever resolving correctly.

2. **R2 Logic Chain**:
   - `VoxelSurfaceNetsJobs.cs` extracts Surface Nets vertices and calculates surface normals.
   - However, `PackColor` generates pseudo-random hash colors for R, G, B instead of calling `VoxelSurfaceColorEncoding`.
   - When the GPU uploads these Surface Nets meshes, the URP Cave Shader reads random values in vertex color R (floor weight) and G (wall weight).
   - This causes URP cave shaders to display inverted or corrupted material blending across all Surface Nets voxel terrain.

3. **R3 Logic Chain**:
   - On line 847 of `HydraulicErosionJob.cs`, `centerZ` is clamped against `writeMaxX - 2`.
   - On rectangular heightmaps/sub-grids where `writeMaxX != writeMaxZ`, Z coordinate clamping is wrong, causing out-of-bounds array reads or truncated erosion.
   - Furthermore, droplets crossing sub-grid write boundaries drop their sediment to `0f`, creating artificial ridge lines along sub-grid borders.

---

## 3. Caveats

- Investigation was performed via static codebase analysis and structural tracing (read-only mode per subagent mandate).
- Playmode profiler validation and batchmode render captures remain `PENDING VERIFICATION` until the Worker agent implements the fixes and executes test runs.
- MapMagic graph nodes wrap `HydraulicErosionJob` and `WorldProceduralTerrainThermalWeatheringJob`; fixes in the underlying Burst jobs automatically fix MapMagic generation output.

---

## 4. Conclusion

The root causes for R1, R2, and R3 are precisely identified and localized to `HectonVoxelVolume.cs`, `HectonPlayerSpawner.cs`, `VoxelSurfaceNetsJobs.cs`, `HectonVoxelEngine.cs`, `HydraulicErosionJob.cs`, and `WorldProceduralTerrainThermalWeatheringJobs.cs`.

Detailed actionable code modifications are specified in `analysis.md`. Implementing these modifications will restore reliable physics bake signal publication, correct player spawner coordinate targeting, enforce URP Cave Shader vertex color blending spec, and guarantee mass conservation across terrain erosion boundaries.

---

## 5. Verification Method

To verify the implementations:
1. **Compilation Check**: Run `dotnet build` or Unity batchmode compilation to verify zero C# syntax errors.
2. **R1 Verification**:
   - Inspect log output of `WorldChunkPhysicsBakedEvents` during voxel volume generation. Verify `IsLaneActive == true` and `IsWorldPointPhysicsBaked(x, z)` returns `true` for voxel chunk footprints.
   - Verify `HectonPlayerSpawner` queries `(searchOrigin.x, searchOrigin.z)` and resolves spawn readiness without timing out.
3. **R2 Verification**:
   - Inspect `VoxelVertexDTO.ColorPacked` generated by `VoxelSurfaceNetsJobs`. Verify R channel equals Floor weight (`ResolveFloorWeight(normal)`), G channel equals Wall weight (`255 - floorByte`), B channel equals `0`, A channel equals `255`.
   - Verify URP cave shader renders correct floor/wall material transitions.
4. **R3 Verification**:
   - Verify line 847 of `HydraulicErosionJob.cs` uses `writeMaxZ - 2`.
   - Run `HydraulicErosionSmokeTester.cs` / `ErosionTestHarness.cs` and verify no seam artifacts or 32px grid ridges appear on heightmap X-Ray cards.

---
*Handoff report authored by Explorer Subagent (`teamwork_preview_explorer`).*
