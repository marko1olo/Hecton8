# Code Review Report — Hecton8 R1, R2, R3 Requirements

**Authority used**: AGENTS.md; PROJECT_BIBLES.md; voxels.md; terrain.md.
**Verdict**: VETO / REQUEST_CHANGES

---

## Executive Summary

An adversarial code review of the 6 target file regions (`HectonVoxelVolume.cs`, `HectonPlayerSpawner.cs`, `VoxelSurfaceNetsJobs.cs`, `HectonVoxelEngine.cs`, `HydraulicErosionJob.cs`, `WorldProceduralTerrainThermalWeatheringJobs.cs`) was conducted.

While signal flag integration (`WorldChunkPhysicsBakedSignal`) and vertex color packing specs (R: Floor weight, G: Wall weight, B: 0, A: AO byte) were correctly structured in theory, **three (3) Critical findings** and **one (1) Major finding** were uncovered that violate spatial signal accuracy, boundary mass conservation, and project single-source-of-truth rules:

1. **[CRITICAL] Spatial Footprint Mismatch in `WorldChunkPhysicsBakedSignal` (`HectonVoxelVolume.cs`)**: `TerrainPosition` is assigned `transform.position` (the volume center), but the signal contract and `ContainsWorldXZ` treat `TerrainPosition` as the **minimum corner**. This shifts the physics readiness footprint by half a chunk along X and Z, causing spawner readiness queries to fail for the lower-left half of voxel volumes and falsely pass outside the volume.
2. **[CRITICAL] Mass Conservation Violation & Border Rim Dumping (`HydraulicErosionJob.cs`)**: In `DepositSedimentaryFlat` and `DepositFlatSediment`, when a droplet moves outside the sub-grid write window (`!IsInsideWriteWindow(...)`), `position` is clamped to interior border cells and sediment is dumped on the boundary. This directly contradicts inline documentation (*"A droplet outside the window must simply carry its sediment out"*) and produces artificial sediment wall/rim artifacts along chunk borders.
3. **[CRITICAL] Dead Boundary Guard & Unbounded Border Weathering (`WorldProceduralTerrainThermalWeatheringJobs.cs`)**: The boundary check `if (x < 0 || z < 0 || x >= Width || z >= Height)` is dead code (since `x = index % Width` and `z = index / Width` are strictly non-negative and less than dimensions). Outer boundary cells undergo single-sided thermal transfer without neighbor protection or apron alignment, leading to mass non-conservation and seam tearing at chunk borders.
4. **[MAJOR] Duplicated Color Encoding Logic (`VoxelSurfaceNetsJobs.cs`)**: `PackColorFromNormal` duplicates floor/wall weight math (`0.375f`, `1f / 0.45f`) instead of calling `VoxelSurfaceColorEncoding` in `HectonVoxelEngine.cs`, violating the canonical single-source-of-truth rule.

---

## Detailed Findings

### [Critical] Finding 1: Signal Spatial Footprint Mismatch (`HectonVoxelVolume.cs`)
- **Location**: `Assets/_Project/Scripts/HectonVoxelVolume.cs` (lines 2007–2016 & 4131–4140)
- **Issue**: `WorldChunkPhysicsBakedSignal` is published with `TerrainPosition = pos` where `pos` is `transform.position` (the volume center `generationPosition = worldCenter`).
- **Impact**: 
  - `WorldChunkPhysicsBakedSignal.cs` defines `TerrainPosition` as *"World-space minimum corner of the chunk footprint"*.
  - `WorldChunkPhysicsBakedSignal.ContainsWorldXZ` tests `worldX >= minX && worldX <= minX + size.x`.
  - Setting `TerrainPosition` to the center shifts the query box by `+0.5 * chunkSize` along X and Z.
  - Queries for points in `[-chunkSize/2, 0]` return `false` (unbaked), while points in `[chunkSize/2, chunkSize]` outside the volume return `true`.
  - `HectonPlayerSpawner.cs` line 399/437 (`IsSpawnPointPhysicsReady`) rejects valid spawn points in the lower-left half of voxel volumes.
- **Required Fix**: Calculate minimum corner `float3 minCorner = pos - new float3(chunkSizeX * 0.5f, 0f, chunkSizeZ * 0.5f)` before setting `TerrainPosition`.

---

### [Critical] Finding 2: Mass Conservation & Boundary Dumping (`HydraulicErosionJob.cs`)
- **Location**: `Assets/_Project/Scripts/World/HydraulicErosionJob.cs` (lines 893–899, 937–943)
- **Issue**: In `DepositSedimentaryFlat` (lines 894-900) and `DepositFlatSediment` (lines 945-950), if `!IsInsideWriteWindow(position, ...)` is true:
  ```csharp
  if (!IsInsideWriteWindow(position, writeMinX, writeMinZ, writeMaxX, writeMaxZ))
  {
      position = math.clamp(position, ...);
  }
  ```
  The position is clamped to the interior border cells and execution proceeds to deposit `amount` onto those border cells.
- **Impact**: 
  - The inline code comments explicitly state: *"A droplet outside the window must simply carry its sediment out; the neighbouring region owns that ground in its own pass (correct apron behaviour)"* and *"Exiting early makes that explicit"*.
  - Instead of exiting early (returning 0f), clamping `position` dumps all external droplet sediment onto border cells, creating artificial sediment ridges/rims along chunk boundaries and violating boundary mass conservation.
- **Required Fix**: Immediately return `0f` (or discard external sediment) when `!IsInsideWriteWindow(...)` evaluates to true in deposit jobs.

---

### [Critical] Finding 3: Dead Boundary Guard & Seam Tearing (`WorldProceduralTerrainThermalWeatheringJobs.cs`)
- **Location**: `Assets/_Project/Scripts/World/WorldProceduralTerrainThermalWeatheringJobs.cs` (lines 28–32, 41–48)
- **Issue**: Line 28 checks `if (x < 0 || z < 0 || x >= Width || z >= Height)`. Because `x = index % Width` and `z = index / Width` are derived from `index` in `[0, Width * Height - 1]`, this condition is mathematically impossible to hit (`x` is always `[0, Width-1]` and `z` is always `[0, Height-1]`).
- **Impact**:
  - The boundary guard is completely dead code.
  - Border cells (`x == 0`, `z == 0`, `x == Width - 1`, `z == Height - 1`) participate in thermal weathering calculations with 1-sided interior neighbors.
  - Because border cells lack adjacent chunk neighbor data, height transfers at chunk boundaries are asymmetrical and non-conservative, creating height steps and seam tearing across chunk borders.
- **Required Fix**: Change line 28 to check for border cells `if (x == 0 || z == 0 || x == Width - 1 || z == Height - 1)` and preserve `center` height for unpadded chunk passes.

---

### [Major] Finding 4: Duplicated Color Encoding Logic (`VoxelSurfaceNetsJobs.cs`)
- **Location**: `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs` (lines 658–670)
- **Issue**: `PackColorFromNormal` duplicates the floor/wall transition curve (`0.375f`, `1f / 0.45f`, smoothstep) rather than calling `VoxelSurfaceColorEncoding` in `HectonVoxelEngine.cs`.
- **Impact**:
  - `HectonVoxelEngine.cs` line 4171 explicitly documents that all cave surface color calculations must route through `VoxelSurfaceColorEncoding` to prevent drift.
  - Hardcoding identical math in `VoxelSurfaceNetsJobs.cs` creates maintenance drift risk between Marching Cubes and Surface Nets mesh passes.
- **Required Fix**: Refactor `PackColorFromNormal` to invoke `VoxelSurfaceColorEncoding.ResolveFloorWeight` or `VoxelSurfaceColorEncoding.Resolve`.

---

## Verified Claims & Checks

1. **Signal Flags (`WorldChunkPhysicsBakedSignal`)**:
   - `FlagColliderActive` (1u << 0) and `FlagHeightmapSynced` (1u << 1) are correctly bitwise ORed in `HectonVoxelVolume.cs` (lines 2015 & 4139). **[PASS]**
2. **URP Cave Shader Channel Alignment**:
   - Color packing format: Byte 0 (R) = Floor weight, Byte 1 (G) = Wall weight, Byte 2 (B) = 0, Byte 3 (A) = AO byte. Confirmed compliant with shader spec. **[PASS]**
3. **NaN & Zero Division Safeguards**:
   - `VoxelSurfaceNetsJobs.cs` lines 193-197 and `VoxelSurfaceColorEncoding.cs` line 4186 guard against non-finite floats, zero length vectors, and zero division. **[PASS]**

---

## Summary Verdict Matrix

| Requirement | Target File | Status | Issue |
|---|---|---|---|
| R1 Signal Footprint | `HectonVoxelVolume.cs` | **FAIL** | `TerrainPosition` uses center instead of min corner |
| R1 Signal Readiness | `HectonPlayerSpawner.cs` | **PARTIAL** | Consumes signal, but affected by footprint shift |
| R2 Cave Shader Color | `VoxelSurfaceNetsJobs.cs` | **FAIL** | Channel spec matches, but duplicates `VoxelSurfaceColorEncoding` |
| R2 Color Encoding | `HectonVoxelEngine.cs` | **PASS** | `VoxelSurfaceColorEncoding` implementation clean |
| R3 Hydraulic Erosion | `HydraulicErosionJob.cs` | **FAIL** | Border clamping dumps sediment on chunk boundaries |
| R3 Thermal Weathering | `WorldProceduralTerrainThermalWeatheringJobs.cs` | **FAIL** | Dead guard condition `x < 0` allows unpadded boundary weathering |
