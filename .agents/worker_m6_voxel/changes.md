# Changes Summary — Milestone 6: SDF Sampling Determinism & Capacity Protection Implementation

## Modified Files

1. **`Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs`**:
   - Added `public double3 OriginAup;` field to `VoxelCliffOverhangNoiseJob`.
   - Updated `Execute(int index)` to calculate `double3 worldPosAup = OriginAup + (double3)(gridPos * VoxelSizeMeters);` and derive `float3 noisePos = (float3)worldPosAup * NoiseFrequency;`.
   - **Rationale**: Replaces local grid chunk coordinates `(x, y, z)` with absolute universe coordinates (`OriginAup`), eliminating 3D noise phase discontinuities across chunk seams and floating origin shifts.

2. **`Assets/_Project/Scripts/World/HectonAnomalyEngine.cs`**:
   - Added `double3 originAup = default` parameter to `ApplyVoxelCliffOverhangNoise(...)`.
   - Initialized `OriginAup = IsFiniteAup(originAup) ? originAup : double3.zero` when instantiating `VoxelCliffOverhangNoiseJob`.
   - **Rationale**: Exposes `originAup` parameter on the static engine API with finite validation fallback.

3. **`Assets/_Project/Scripts/HectonVoxelEngine.cs`**:
   - Updated `ApplyVoxelCliffOverhangNoise` call at line 10823 to pass `sdfOriginAup`.
   - Refactored `ResolveStreamingMeshRawScratchCapacity(int totalCellCount)` (line 12252) to compute:
     ```csharp
     long desired = (long)math.max(1, totalCellCount) * MC_BUFFER_MULTIPLIER;
     int qualityCapacity = ResolveStreamingMeshRawScratchQualityCapacity();
     long capacity = math.max(desired, (long)qualityCapacity);
     capacity = math.min(capacity, (long)StreamingMeshRawVertexScratchVisualOverkillCapacity);
     return capacity < 1L ? 1 : (int)capacity;
     ```
   - **Rationale**: Eliminates capacity truncation where `qualityCapacity` forced scratch memory below `desired` cell count, preventing Marching Cubes array overflow and missing chunk geometry on low `GlobalQualityWeight`.

4. **`Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs`**:
   - Updated call to `HectonAnomalyEngine.ApplyVoxelCliffOverhangNoise(...)` at line 926 to pass `double3.zero`.
   - **Rationale**: Updates editor test harness signature to maintain compatibility with updated API.
