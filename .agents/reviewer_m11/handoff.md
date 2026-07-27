# Handoff Report — Code Review M11

**Authority used**: AGENTS.md; PROJECT_BIBLES.md; voxels.md; terrain.md.

## 1. Observation

Direct code observations from inspected target files:

1. **`Assets/_Project/Scripts/HectonVoxelVolume.cs` (lines 2003–2016 & 4126–4141)**:
   ```csharp
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
   ```
   `pos` is `transform.position` (`generationPosition = worldCenter`). `TerrainPosition` is assigned `pos`.

2. **`Assets/_Project/Scripts/World/Contracts/WorldChunkPhysicsBakedSignal.cs` (lines 45, 67–73)**:
   ```csharp
   /// World-space minimum corner of the chunk footprint (Y is the terrain base height).
   [FieldOffset(16)] public float3 TerrainPosition;

   public static bool ContainsWorldXZ(in WorldChunkPhysicsBakedSignal signal, float worldX, float worldZ)
   {
       float minX = signal.TerrainPosition.x;
       float minZ = signal.TerrainPosition.z;
       return worldX >= minX &&
           worldZ >= minZ &&
           worldX <= minX + signal.TerrainSize.x &&
           worldZ <= minZ + signal.TerrainSize.z;
   }
   ```

3. **`Assets/_Project/Scripts/World/HydraulicErosionJob.cs` (lines 894–900 & 945–950)**:
   ```csharp
   if (!IsInsideWriteWindow(position, writeMinX, writeMinZ, writeMaxX, writeMaxZ))
   {
       position = math.clamp(
           position,
           new float2(writeMinX + 2, writeMinZ + 2),
           new float2(math.max(writeMinX + 2, writeMaxX - 3), math.max(writeMinZ + 2, writeMaxZ - 3)));
   }
   ```
   Lines 890–893 inline comment states: *"A droplet outside the window must simply carry its sediment out; the neighbouring region owns that ground in its own pass (correct apron behaviour). Exiting early makes that explicit"*.

4. **`Assets/_Project/Scripts/World/WorldProceduralTerrainThermalWeatheringJobs.cs` (lines 24–32)**:
   ```csharp
   int x = index % Width;
   int z = index / Width;
   float center = math.saturate(InputHeights01[index]);

   if (x < 0 || z < 0 || x >= Width || z >= Height)
   {
       OutputHeights01[index] = center;
       return;
   }
   ```
   `index` is in `[0, Width * Height - 1]`.

5. **`Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs` (lines 658–670)**:
   ```csharp
   private static uint PackColorFromNormal(float3 normal, float ao)
   {
       float3 safeNormal = math.select(new float3(0f, 1f, 0f), math.normalize(normal), math.lengthsq(normal) > 1e-6f && math.all(math.isfinite(normal)));
       float t = math.saturate((safeNormal.y - 0.375f) * (1f / 0.45f));
       float floorWeight = t * t * (3f - (2f * t));
       ...
   }
   ```
   Duplicated from `VoxelSurfaceColorEncoding` in `HectonVoxelEngine.cs`.

---

## 2. Logic Chain

1. **Footprint Mismatch**:
   - `ContainsWorldXZ` evaluates whether `worldX` is in `[TerrainPosition.x, TerrainPosition.x + TerrainSize.x]`.
   - `HectonVoxelVolume.cs` passes `transform.position` (the volume center) into `TerrainPosition`.
   - Therefore, the bounding box tested by `ContainsWorldXZ` is shifted by `+0.5 * chunkSize` along X and Z.
   - Any query in the range `[center - 0.5*chunkSize, center]` (the lower-left half of the volume) fails `ContainsWorldXZ` and is reported as unbaked.
   - Any query in `[center + 0.5*chunkSize, center + chunkSize]` (outside the volume) incorrectly succeeds.
   - `IsSpawnPointPhysicsReady` in `HectonPlayerSpawner.cs` relies on `ContainsWorldXZ` to verify chunk readiness, so valid spawn points in negative voxel volume offsets are rejected.

2. **Sediment Rim Artifacts**:
   - In `HydraulicErosionJob.cs`, `DepositSedimentaryFlat` and `DepositFlatSediment` check `!IsInsideWriteWindow(position, ...)`.
   - When true, instead of returning `0f` (as documented in comments), `position` is clamped to interior border coordinates (`writeMinX + 2`, `writeMinZ + 2`, etc.) and mass is deposited onto those boundary cells.
   - Droplets leaving the sub-grid deposit 100% of their remaining sediment on the boundary cells inside the chunk, creating artificial elevated sediment ridges along chunk borders and violating mass conservation between adjacent chunks.

3. **Dead Guard in Thermal Weathering**:
   - In `WorldProceduralTerrainThermalWeatheringJobs.cs`, `x = index % Width` is guaranteed to be in `[0, Width - 1]` and `z = index / Width` is guaranteed to be in `[0, Height - 1]`.
   - `if (x < 0 || z < 0 || x >= Width || z >= Height)` is dead code and never evaluates to `true`.
   - Border cells (`x == 0`, `z == 0`, `x == Width - 1`, `z == Height - 1`) participate in thermal weathering using single-sided interior neighbors without neighbor chunk data.
   - This creates asymmetrical height transfers at chunk boundaries, causing height steps and seam tearing between adjacent terrain chunks.

4. **Duplicated Surface Color Formula**:
   - `HectonVoxelEngine.cs` line 4171 requires all cave color encoding to route through `VoxelSurfaceColorEncoding`.
   - `VoxelSurfaceNetsJobs.cs` duplicates the math independently, creating maintenance drift risk across meshing implementations.

---

## 3. Caveats

- Runtime GPU rendering execution (Play Mode) was not executed in this environment. Findings are based on strict static code audit and mathematical logic tracing against `AGENTS.md`, `voxels.md`, and `terrain.md`.
- No alterations were made to implementation code, as this agent is restricted to review-only mode.

---

## 4. Conclusion

**Verdict**: VETO / REQUEST_CHANGES

The implementation fails critical correctness and safety checks in `HectonVoxelVolume.cs`, `HydraulicErosionJob.cs`, and `WorldProceduralTerrainThermalWeatheringJobs.cs`. The implementation MUST NOT be merged until the three Critical findings (signal spatial footprint mismatch, sediment boundary dumping, and dead thermal weathering guard) and the Major finding (color encoding duplication) are fixed.

---

## 5. Verification Method

To independently verify these findings:

1. **Signal Footprint**:
   - Inspect `HectonVoxelVolume.cs:2007` & `4131` vs `WorldChunkPhysicsBakedSignal.cs:67-73`.
   - Trace `ContainsWorldXZ(signal, center - chunkSize * 0.25f, center - chunkSize * 0.25f)`: `center - 0.25*chunkSize >= center` evaluates to `false`.

2. **Erosion Border Dumping**:
   - Inspect `HydraulicErosionJob.cs:894-900`.
   - Pass a position `position = new float2(writeMinX - 5, writeMinZ - 5)`.
   - Note that `!IsInsideWriteWindow` is true, position is clamped to `writeMinX + 2`, and sediment is added to `Heightmap` at index `(writeMinZ + 2) * Width + (writeMinX + 2)`.

3. **Thermal Weathering Guard**:
   - Inspect `WorldProceduralTerrainThermalWeatheringJobs.cs:28`.
   - Substitute `index = 0` (where `x = 0`, `z = 0`). `0 < 0 || 0 < 0 || 0 >= Width || 0 >= Height` is `false`. The return statement is never reached.

4. **Color Encoding Duplication**:
   - Compare `VoxelSurfaceNetsJobs.cs:660-662` with `HectonVoxelEngine.cs:4186-4188`.
