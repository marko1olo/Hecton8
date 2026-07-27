# Comprehensive Reconnaissance & Technical Analysis Report: M8-M10 Requirements (R1, R2, R3)

**Authority used**: AGENTS.md; PROJECT_BIBLES.md; voxels.md; terrain.md.  
**Working Directory**: `C:\hades\Hecton8\.agents\explorer_m8_m10`  
**Date**: 2026-07-27  
**Author**: Explorer Subagent (`teamwork_preview_explorer`)  

---

## Executive Summary

This investigation performed deep codebase reconnaissance for requirements R1 (Voxel Physics Bake Signal & Kinematic Spawner Integration), R2 (Voxel Vertex Color Channel & Shader Blending Audit), and R3 (Terrain Boundary Guard & Erosion Stability Audit). Multiple critical defects were uncovered, including silent physics signal publication drop race conditions, coordinate argument misassignments in the player spawner, corrupted vertex color packing in Surface Nets Burst jobs, and mass conservation violations / typo bugs in terrain erosion jobs.

---

## R1. Voxel Physics Bake Signal & Kinematic Spawner Integration

### 1.1 Codebase Reconnaissance & Tracing
- **Files Inspected**:
  - `Assets/_Project/Scripts/HectonVoxelVolume.cs` (lines 1960–2018, 4060–4160)
  - `Assets/_Project/Scripts/HectonVoxelEngine.cs` (lines 14030–14175)
  - `Assets/_Project/Scripts/HectonPlayerSpawner.cs` (lines 390–445, 1090–1160)
  - `Assets/_Project/Scripts/MapMagicBridge.cs` (lines 610–660)
  - `Assets/_Project/Scripts/World/Contracts/WorldChunkPhysicsBakedSignal.cs`
  - `Assets/_Project/Scripts/World/WorldChunkPhysicsBakedEvents.cs`

### 1.2 Root Cause Analysis

#### Defect R1.1: Silent Physics Signal Drop Race Condition (`HectonVoxelVolume.cs`)
- **Observation**:
  In `CommitDeferredColliderChunkUpload(int index)` (lines 1998–2016):
  ```csharp
  collider.enabled = _bakeState == VoxelBakeState.Complete;
  DisableColliderChunkBakeProxy(index);

  if (collider.enabled)
  {
      var pos = transform.position;
      WorldChunkPhysicsBakedSignal signal = new WorldChunkPhysicsBakedSignal
      {
          ChunkX = (int)math.floor(pos.x / 100f),
          ChunkZ = (int)math.floor(pos.z / 100f),
          TerrainEntityHash = (uint)gameObject.GetInstanceID(),
          Frame = (uint)UnityEngine.Time.frameCount,
          TerrainPosition = pos,
          TerrainSize = new float3(100f, 100f, 100f),
          Flags = WorldChunkPhysicsBakedSignal.FlagColliderActive | WorldChunkPhysicsBakedSignal.FlagHeightmapSynced
      };
      WorldChunkPhysicsBakedEvents.TryPublish(in signal);
  }
  ```
- **Mechanism**:
  During `ProcessQueuedRebuildsAsync`, `_bakeState` is set to `VoxelBakeState.Baking`. When `CommitDeferredColliderChunkUpload` is invoked during the deferred collider upload drain, `_bakeState == VoxelBakeState.Complete` evaluates to `false`. Consequently, `collider.enabled` is set to `false`, `if (collider.enabled)` evaluates to `false`, and `WorldChunkPhysicsBakedEvents.TryPublish` is bypassed!
  Later, when `SetBakeState(VoxelBakeState.Complete)` is called at the end of the rebuild pass, `RefreshBakePresentation()` iterates over colliders and sets `collider.enabled = true`, but **never publishes `WorldChunkPhysicsBakedSignal`**.
- **Impact**: `WorldChunkPhysicsBakedSignal` is never published when voxel volume colliders complete baking, leaving spawner/kinematic gates hanging indefinitely or falling back to time-based timeouts.

#### Defect R1.2: Coordinate Argument Misassignment Bug (`HectonPlayerSpawner.cs`)
- **Observation**:
  In `HectonPlayerSpawner.cs`:
  - Line 399:
    ```csharp
    if (TryResolveGroundHit(out _hitInfo) && IsSpawnPointPhysicsReady(searchOrigin.x, searchOrigin.y))
    ```
  - Line 434:
    ```csharp
    SpawnSearchResult centerResult = EvaluatePoint(searchOrigin.x, searchOrigin.y);
    ```
- **Mechanism**: `searchOrigin` is a `Vector3`. Passing `searchOrigin.x, searchOrigin.y` passes `searchOrigin.y` (vertical Y position, e.g. -50f) as `worldZ`!
- **Impact**: The spawner queries physics readiness and evaluates terrain points at `(X, Y)` instead of `(X, Z)`. It checks a world position hundreds of meters away from the actual spawn location, resulting in persistent gate failures.

#### Defect R1.3: Hardcoded Chunk Footprint and Unhandled Bake Failures
- **Observation**:
  `HectonVoxelVolume.cs` line 2011 hardcodes `TerrainSize = new float3(100f, 100f, 100f)` and `ChunkX = (int)math.floor(pos.x / 100f)` instead of using actual volume bounds (`_gridDimension * _voxelSize`).
  Additionally, when `RebuildVolumeAsync` fails or volume is disabled, no terminal `FlagBakeFailed` signal is published.

---

### 1.3 Exact Proposed Code Modifications for R1

#### Modification R1.1: Publish Physics Baked Signal upon Bake Completion in `HectonVoxelVolume.cs`
In `Assets/_Project/Scripts/HectonVoxelVolume.cs`:
1. In `RefreshBakePresentation()` (or inside `SetBakeState(VoxelBakeState.Complete)`), when `_bakeState == VoxelBakeState.Complete`, publish `WorldChunkPhysicsBakedSignal` for all active collider chunks:
```csharp
private void PublishPhysicsBakedSignalsOnComplete()
{
    if (_bakeState != VoxelBakeState.Complete)
        return;

    var pos = transform.position;
    float chunkSizeX = _gridDimension > 0 && _voxelSize > 0f ? _gridDimension * _voxelSize : 100f;
    float chunkSizeZ = chunkSizeX;
    float3 size = new float3(chunkSizeX, chunkSizeX, chunkSizeZ);

    WorldChunkPhysicsBakedSignal signal = new WorldChunkPhysicsBakedSignal
    {
        ChunkX = (int)math.floor(pos.x / chunkSizeX),
        ChunkZ = (int)math.floor(pos.z / chunkSizeZ),
        TerrainEntityHash = (uint)gameObject.GetInstanceID(),
        Frame = (uint)UnityEngine.Time.frameCount,
        TerrainPosition = pos,
        TerrainSize = size,
        Flags = WorldChunkPhysicsBakedSignal.FlagColliderActive | WorldChunkPhysicsBakedSignal.FlagHeightmapSynced
    };
    WorldChunkPhysicsBakedEvents.TryPublish(in signal);
}
```
2. Call `PublishPhysicsBakedSignalsOnComplete()` when `SetBakeState(VoxelBakeState.Complete)` is set.

#### Modification R1.2: Fix Coordinate Arguments in `HectonPlayerSpawner.cs`
In `Assets/_Project/Scripts/HectonPlayerSpawner.cs`:
- Line 399: Change `searchOrigin.y` to `searchOrigin.z`:
  ```csharp
  if (TryResolveGroundHit(out _hitInfo) && IsSpawnPointPhysicsReady(searchOrigin.x, searchOrigin.z))
  ```
- Line 434: Change `searchOrigin.y` to `searchOrigin.z`:
  ```csharp
  SpawnSearchResult centerResult = EvaluatePoint(searchOrigin.x, searchOrigin.z);
  ```

---

## R2. Voxel Vertex Color Channel & Shader Blending Audit

### 2.1 Codebase Reconnaissance & Tracing
- **Files Inspected**:
  - `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs` (lines 180–215, 658–667)
  - `Assets/_Project/Scripts/HectonVoxelEngine.cs` (lines 4175–4196, 4290–4300, 4530–4540)
  - `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsContracts.cs`

### 2.2 Root Cause Analysis

#### Defect R2.1: Surface Nets Burst Job Vertex Color Corruption (`VoxelSurfaceNetsJobs.cs`)
- **Observation**:
  In `VoxelSurfaceNetsJobs.cs` lines 658–667:
  ```csharp
  private static uint PackColor(float3 positionLocal, float quality, float biomeBlendScale, uint chunkHash)
  {
      uint h = math.hash(new uint4((uint)math.abs((int)positionLocal.x), (uint)math.abs((int)positionLocal.y), (uint)math.abs((int)positionLocal.z), chunkHash));
      float hash01 = (h & 1023u) * (1f / 1023f);
      float blend = math.saturate((positionLocal.y * math.max(biomeBlendScale, 0.001f) * 0.01f) + (hash01 * 0.35f));
      uint r = (uint)math.clamp((int)math.round(hash01 * 255f), 0, 255);
      uint g = (uint)math.clamp((int)math.round(blend * 255f), 0, 255);
      uint b = (uint)math.clamp((int)math.round(math.saturate(quality) * 255f), 0, 255);
      return r | (g << 8) | (b << 16) | (255u << 24);
  }
  ```
- **Mechanism**:
  `PackColor` generates pseudo-random noise bytes for R, G, B channels (`r = hash01 * 255`, `g = blend * 255`, `b = quality * 255`, `a = 255`).
  This **directly violates the URP Cave Shader Texture Blending Specification**:
  - **Red**: Floor weight (`0 = wall/ceiling, 1 = floor` based on surface normal up-vector `normal.y`)
  - **Green**: Wall weight (`1 - floor weight`)
  - **Blue**: `0` (or reserved)
  - **Alpha**: Ambient Occlusion (`AO` byte in `[0, 255]`)
- **Impact**: All Surface Nets mesh extractions pack random noise into `ColorPacked` on `VoxelVertexDTO`. When uploaded to GPU, URP cave shaders render completely broken material blending (floor textures on vertical walls, wall textures on flat ground, visual noise artifacts).

#### Defect R2.2: Missing Normal Normalization and Non-Finite Protection (`HectonVoxelEngine.cs`)
- **Observation**:
  In `VoxelSurfaceColorEncoding`:
  ```csharp
  public static float ResolveFloorWeight(float3 normal)
  {
      float t = math.saturate((normal.y - FloorTransitionMin) * (1f / FloorTransitionRange));
      return t * t * (3f - 2f * t);
  }
  ```
  `ResolveFloorWeight` directly accesses `normal.y` without verifying if `normal` is finite, non-zero, or unit-length. Unnormalized vectors (e.g. `(0, 5, 0)`) or zero vectors `(0, 0, 0)` cause incorrect floor weight calculation or division-by-zero artifacts.

---

### 2.3 Exact Proposed Code Modifications for R2

#### Modification R2.1: Fix Surface Nets Color Packing in `VoxelSurfaceNetsJobs.cs`
Replace `PackColor` in `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs` with URP Cave Shader spec-compliant encoding:

```csharp
private static uint PackColorFromNormal(float3 normal, float ao)
{
    float3 safeNormal = math.select(new float3(0f, 1f, 0f), math.normalize(normal), math.lengthsq(normal) > 1e-6f && math.all(math.isfinite(normal)));
    float t = math.saturate((safeNormal.y - 0.375f) * (1f / 0.45f));
    float floorWeight = t * t * (3f - 2f * t);

    uint floorByte = (uint)math.clamp((int)math.round(floorWeight * 255f), 0, 255);
    uint wallByte = 255u - floorByte;
    uint blueByte = 0u;
    uint aoByte = (uint)math.clamp((int)math.round(math.saturate(ao) * 255f), 0, 255);

    return floorByte | (wallByte << 8) | (blueByte << 16) | (aoByte << 24);
}
```
In `VoxelSurfaceNetsJobs.Execute` line 202, update:
```csharp
uint colorPacked = PackColorFromNormal(normal, 1.0f);
```

#### Modification R2.2: Add Normal Safeguards to `VoxelSurfaceColorEncoding`
In `Assets/_Project/Scripts/HectonVoxelEngine.cs`:
```csharp
public static float ResolveFloorWeight(float3 normal)
{
    float3 safeNormal = math.select(new float3(0f, 1f, 0f), math.normalize(normal), math.lengthsq(normal) > 1e-6f && math.all(math.isfinite(normal)));
    float t = math.saturate((safeNormal.y - FloorTransitionMin) * (1f / FloorTransitionRange));
    return t * t * (3f - 2f * t);
}
```

---

## R3. Terrain Boundary Guard & Erosion Stability Audit

### 3.1 Codebase Reconnaissance & Tracing
- **Files Inspected**:
  - `Assets/_Project/Scripts/World/HydraulicErosionJob.cs` (lines 450–515, 560–675, 840–885)
  - `Assets/_Project/Scripts/World/WorldProceduralTerrainThermalWeatheringJobs.cs` (lines 20–68)

### 3.2 Root Cause Analysis

#### Defect R3.1: Typo Bug in `centerZ` Clamp Bound (`HydraulicErosionJob.cs`)
- **Observation**:
  In `HydraulicErosionJob.cs` line 847:
  ```csharp
  int centerX = math.clamp((int)math.floor(position.x), writeMinX + 1, writeMaxX - 2);
  int centerZ = math.clamp((int)math.floor(position.y), writeMinZ + 1, writeMaxX - 2);
  ```
- **Mechanism**:
  Line 847 uses `writeMaxX - 2` instead of `writeMaxZ - 2`!
- **Impact**:
  For non-square sub-grids or core chunks where `writeMaxX != writeMaxZ`:
  - If `writeMaxX < writeMaxZ`, `centerZ` is clamped prematurely, cutting off erosion before the true Z boundary.
  - If `writeMaxX > writeMaxZ`, `centerZ` can exceed `writeMaxZ - 2`, causing `z = centerZ + oz` to sample out of bounds or cross sub-grid write boundaries.

#### Defect R3.2: Mass Conservation Loss Across Sub-Grid Write Windows (`HydraulicErosionJob.cs`)
- **Observation**:
  In `DepositFlatSediment` (line 937) and `DepositSedimentaryFlat` (line 894):
  ```csharp
  if (!IsInsideWriteWindow(position, writeMinX, writeMinZ, writeMaxX, writeMaxZ))
      return 0f;
  ```
- **Mechanism**:
  When a droplet traverses past `writeMax` into an adjacent sub-grid's motion domain, `IsInsideWriteWindow` returns `false`, causing the deposit functions to return `0f`. The droplet's accumulated sediment vanishes from the simulation, destroying mass conservation.
- **Impact**: Un-deposited sediment creates hard artificial ridge lines and un-eroded seams along sub-grid boundaries (the 32-pixel checkerboard) and chunk borders.

#### Defect R3.3: Boundary Ringing and Frozen Edge Seams (`WorldProceduralTerrainThermalWeatheringJobs.cs`)
- **Observation**:
  Lines 28–32:
  ```csharp
  if (x <= 0 || z <= 0 || x >= Width - 1 || z >= Height - 1)
  {
      OutputHeights01[index] = center;
      return;
  }
  ```
  And lines 48–55:
  ```csharp
  if (x - 1 > 0) delta += ...;
  if (x + 1 < Width - 1) delta += ...;
  if (z - 1 > 0) delta += ...;
  if (z + 1 < Height - 1) delta += ...;
  ```
- **Mechanism**: Outer edge cells `[x == 0, z == 0, x == Width - 1, z == Height - 1]` are un-eroded. Cell `x = 1` exchanges mass with interior cells `x = 2`, but `x - 1 > 0` prevents mass transfer with `x = 0`.
- **Impact**: When terrain chunks stream side-by-side, the shared boundary line remains frozen while interior cells slump down, creating 1-pixel height cliffs and seams across chunk borders.

---

### 3.3 Exact Proposed Code Modifications for R3

#### Modification R3.1: Fix `centerZ` Clamp Typo in `HydraulicErosionJob.cs`
In `Assets/_Project/Scripts/World/HydraulicErosionJob.cs` line 847:
```csharp
// FIX: Change writeMaxX - 2 to writeMaxZ - 2
int centerZ = math.clamp((int)math.floor(position.y), writeMinZ + 1, writeMaxZ - 2);
```

#### Modification R3.2: Fix Boundary Mass Conservation in Deposit Methods
In `Assets/_Project/Scripts/World/HydraulicErosionJob.cs`:
In `DepositFlatSediment` and `DepositSedimentaryFlat`, if a droplet exits the write window carrying sediment, deposit the sediment at the clamped interior border cell of the write window (`math.clamp(x, writeMinX, writeMaxX - 1)`) instead of discarding it to 0f.

#### Modification R3.3: Fix Border Transfers in `WorldProceduralTerrainThermalWeatheringJobs.cs`
In `Assets/_Project/Scripts/World/WorldProceduralTerrainThermalWeatheringJobs.cs`:
Allow boundary transfer guards to include `x - 1 >= 0`, `x + 1 < Width`, `z - 1 >= 0`, `z + 1 < Height` when neighbor heightmap overlaps are provided, ensuring continuous slope slumping across tile borders.

---

## Conclusion & Actionable Guidance for Worker Agent

1. **R1 Action Plan**:
   - Update `HectonVoxelVolume.cs` to publish `WorldChunkPhysicsBakedSignal` upon bake completion in `SetBakeState(VoxelBakeState.Complete)` / `RefreshBakePresentation()`.
   - Update `HectonPlayerSpawner.cs` lines 399 and 434 to pass `searchOrigin.z` instead of `searchOrigin.y`.
2. **R2 Action Plan**:
   - Rewrite `PackColor` in `VoxelSurfaceNetsJobs.cs` to encode Red = Floor weight, Green = Wall weight, Blue = 0, Alpha = AO byte according to URP Cave Shader spec.
   - Add finite and normalization guards to `VoxelSurfaceColorEncoding.ResolveFloorWeight` in `HectonVoxelEngine.cs`.
3. **R3 Action Plan**:
   - Fix typo on line 847 of `HydraulicErosionJob.cs` (`writeMaxX - 2` -> `writeMaxZ - 2`).
   - Fix sediment dropping boundary loss in `HydraulicErosionJob.cs`.
   - Update border guards in `WorldProceduralTerrainThermalWeatheringJobs.cs`.

---
*Report compiled and verified by Explorer Subagent (`teamwork_preview_explorer`).*
