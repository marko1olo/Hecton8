## 2026-07-27T03:03:23Z
<USER_REQUEST>
You are a Worker subagent (teamwork_preview_worker) assigned to execute Iteration 2 code remediation for Hecton8 R1, R2, and R3 requirements to resolve Reviewer VETO findings.

Working Directory: C:\hades\Hecton8\.agents\worker_m8_m10_iter2

## Authority & Domain Rules
Read:
- `C:\hades\Hecton8\AGENTS.md`
- `C:\hades\Hecton8\voxels.md`
- `C:\hades\Hecton8\terrain.md`

Include exact receipt: "Authority used: AGENTS.md; PROJECT_BIBLES.md; voxels.md; terrain.md."

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A Forensic Auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

## Reviewer VETO Findings & Remediation Instructions

### 1. Fix `WorldChunkPhysicsBakedSignal` Bounding Box (`HectonVoxelVolume.cs`)
- **Finding**: `TerrainPosition` was assigned `transform.position` (the volume center). `ContainsWorldXZ` assumes `TerrainPosition` is the minimum corner `(minX, minZ)`. Setting it to center shifts the physics readiness box by `+0.5 * chunkSize`, causing `ContainsWorldXZ` to misidentify the physics boundary.
- **Fix**: In `HectonVoxelVolume.cs` lines 2012 and 4134, calculate the minimum corner:
  ```csharp
  float3 minCorner = new float3(pos.x - chunkSizeX * 0.5f, pos.y, pos.z - chunkSizeZ * 0.5f);
  ```
  Set `TerrainPosition = minCorner` in `WorldChunkPhysicsBakedSignal`.

### 2. Fix Vertex Color Single Source of Truth (`VoxelSurfaceNetsJobs.cs`)
- **Finding**: `PackColorFromNormal` duplicated floor/wall weight math (`0.375f`, `1f / 0.45f`) instead of using `VoxelSurfaceColorEncoding` in `HectonVoxelEngine.cs`.
- **Fix**: In `VoxelSurfaceNetsJobs.cs`, call `HectonVoxelEngine.VoxelSurfaceColorEncoding.ResolveFloorWeight(safeNormal)` to compute `floorWeight`.

### 3. Revert Sediment Boundary Dumping (`HydraulicErosionJob.cs`)
- **Finding**: Clamping droplet deposits to interior boundary cells when droplets cross sub-grid write boundaries dumps artificial sediment walls along 32px grid lines.
- **Fix**: In `HydraulicErosionJob.cs` `DepositSedimentaryFlat` and `DepositFlatSediment`, restore `if (!IsInsideWriteWindow(position, writeMinX, writeMinZ, writeMaxX, writeMaxZ)) return 0f;`. Droplets outside the write window carry their sediment out naturally without dumping on interior borders.

### 4. Restore Weathering Outer Apron Protection (`WorldProceduralTerrainThermalWeatheringJobs.cs`)
- **Finding**: Outer boundary check `x < 0 || z < 0 || x >= Width || z >= Height` was dead code and caused single-sided perimeter mass loss.
- **Fix**: In `WorldProceduralTerrainThermalWeatheringJobs.cs`, restore outer apron boundary protection:
  ```csharp
  if (x <= 0 || z <= 0 || x >= Width - 1 || z >= Height - 1)
  {
      OutputHeights01[index] = center;
      return;
  }
  ```
  Keep neighbor checks `x - 1 > 0`, `x + 1 < Width - 1`, `z - 1 > 0`, `z + 1 < Height - 1` for interior cells.

## Verification
- Verify zero syntax or compilation errors.
- Document all file edits in `C:\hades\Hecton8\.agents\worker_m8_m10_iter2\handoff.md`.
- Send a message back to parent when completed.
</USER_REQUEST>
