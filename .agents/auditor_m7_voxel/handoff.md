# Handoff Report — Milestone 7 Forensic Integrity Audit

## 1. Observation
- **Authority intake quote (`voxels.md:98`)**: `"GlobalQualityWeight may scale SDF resolution, extraction distance, diagnostic depth, and rebuild cadence, but it must not change collision truth, carve permission, or save delta identity."`
- **Target files inspected**:
  - `Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs`
  - `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs`
  - `Assets/_Project/Scripts/HectonVoxelEngine.cs`
  - `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs`
- **Observations on `VoxelCliffOverhangNoiseJob`**:
  - `HectonAnomalySdfJobs.cs:824`: `public double3 OriginAup;`
  - `HectonAnomalySdfJobs.cs:860-862`:
    ```csharp
    float3 gridPos = new float3(x, y, z);
    double3 worldPosAup = OriginAup + (double3)(gridPos * VoxelSizeMeters);
    float3 noisePos = (float3)worldPosAup * NoiseFrequency;
    ```
- **Observations on `ApplyVoxelCliffOverhangNoise`**:
  - `HectonAnomalyEngine.cs:740`: Sets `OriginAup = IsFiniteAup(originAup) ? originAup : double3.zero` on job.
  - `HectonVoxelEngine.cs:10826`: Passes `sdfOriginAup` directly into `HectonAnomalyEngine.ApplyVoxelCliffOverhangNoise`.
- **Observations on `ResolveStreamingMeshRawScratchCapacity`**:
  - `HectonVoxelEngine.cs:12253-12260`:
    ```csharp
    static int ResolveStreamingMeshRawScratchCapacity(int totalCellCount)
    {
        long desired = (long)math.max(1, totalCellCount) * MC_BUFFER_MULTIPLIER;
        int qualityCapacity = ResolveStreamingMeshRawScratchQualityCapacity();
        long capacity = math.max(desired, (long)qualityCapacity);
        capacity = math.min(capacity, (long)StreamingMeshRawVertexScratchVisualOverkillCapacity);
        return capacity < 1L ? 1 : (int)capacity;
    }
    ```
- **Observations on `AnomalyTestHarness.cs`**:
  - Contains 13 assertion suites testing setting sanitization, Chebyshev bowl detection, open-edge rejection, brine pool bounds integrity, toxic mud grid, time-sliced flood fills, stamp overflow, cliff overhang noise, ridge features, terrain-SDF seam locks, and SDF injection.

## 2. Logic Chain
1. **Zero Mocks & Genuine Implementation**:
   - Examination of `HectonAnomalySdfJobs.cs`, `HectonAnomalyEngine.cs`, `HectonVoxelEngine.cs`, and `AnomalyTestHarness.cs` shows zero hardcoded dummy returns, mock facades, or test bypasses.
   - All job logic performs real mathematical computations (Chebyshev distance, trilinear sampling, FastHashNoise3D, FractalNoise3D, gradient-based slope calculations).
2. **Absolute AUP Noise Evaluation**:
   - `VoxelCliffOverhangNoiseJob` explicitly adds `OriginAup` to local voxel grid coordinates (`OriginAup + (double3)(gridPos * VoxelSizeMeters)`), yielding true absolute AUP position `worldPosAup` before scaling by `NoiseFrequency`.
   - `HectonAnomalyEngine.ApplyVoxelCliffOverhangNoise` accepts `double3 originAup = default` and populates `job.OriginAup`. `HectonVoxelEngine.cs` passes chunk AUP `sdfOriginAup` into `ApplyVoxelCliffOverhangNoise`.
3. **Scratch Memory Guarantee**:
   - `ResolveStreamingMeshRawScratchCapacity` evaluates `capacity = math.max(desired, (long)qualityCapacity)`. Because `math.max` is used, even if `qualityCapacity` shrinks under a low `GlobalQualityWeight`, `desired` capacity (cellCount * MC_BUFFER_MULTIPLIER) is preserved. The result is then capped at `StreamingMeshRawVertexScratchVisualOverkillCapacity`.
4. **Codebase Consistency & Asmdef Integrity**:
   - All files adhere to standard Hecton-8 Burst contracts (`CompileSynchronously = true`, `FloatMode = FloatMode.Deterministic`, `FloatPrecision = FloatPrecision.Standard`), explicit struct layout byte sizes, safety justification comments, and proper asmdef placement.

## 3. Caveats
- Static source audit only. Live runtime execution in batchmode / Unity Editor Play Mode requires process gates.
- No caveats regarding code logic or forensic integrity checks.

## 4. Conclusion
- Milestone 7 source changes in Hecton-8 Voxel/Anomaly engine pass all forensic integrity checks.
- Explicit Verdict: **CLEAN**.

## 5. Verification Method
- **File Inspection**:
  - Inspect `HectonAnomalySdfJobs.cs:860-862` to verify `worldPosAup` calculation.
  - Inspect `HectonAnomalyEngine.cs:740` & `HectonVoxelEngine.cs:10826` to verify `sdfOriginAup` parameter propagation.
  - Inspect `HectonVoxelEngine.cs:12253-12260` to verify `math.max(desired, (long)qualityCapacity)` logic.
  - Inspect `AnomalyTestHarness.cs:73-89` to verify harness entry point `Run()`.
- **Invalidation Conditions**:
  - Any future commit that changes `math.max` to `math.min` in `ResolveStreamingMeshRawScratchCapacity`.
  - Any regression removing `OriginAup` from `VoxelCliffOverhangNoiseJob` noise evaluation.
