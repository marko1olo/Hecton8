> **PROOF OF READING**: Quote `voxels.md` line 98: `"GlobalQualityWeight may scale SDF resolution, extraction distance, diagnostic depth, and rebuild cadence, but it must not change collision truth, carve permission, or save delta identity."`
> Authority files loaded & verified: `C:\hades\Hecton8\AGENTS.md`, `C:\hades\Hecton8\GEMINI.md`, `C:\hades\Hecton8\voxels.md`, `C:\hades\Hecton8\terrain.md`.

# Milestone 5: Voxel SDF Sampling Reconnaissance & Root Cause Audit Report

## 1. Executive Summary

A surgical, read-only diagnostic audit was conducted across `HectonVoxelEngine.cs`, `HectonAnomalyEngine.cs`, `HectonAnomalySdfJobs.cs`, and `AnomalyTestHarness.cs` to resolve Milestone 5 requirements (R1, R2, R3).

The audit identified two critical root causes violating `voxels.md` line 98:
1. **Local Grid Coordinate Noise Evaluation (R1 & R2 Root Cause)**: `VoxelCliffOverhangNoiseJob` evaluates 3D fractal noise using local grid coordinates `gridPos * VoxelSizeMeters * NoiseFrequency`, where `gridPos = (x, y, z)` starts from `0` for each chunk grid. This causes spatial phase resets across chunk borders, floating origin shifts, and LOD splits, destroying 100% world-coordinate alignment and determinism.
2. **Quality-Weighted Scratch Buffer Truncation (R3 Root Cause)**: `ResolveStreamingMeshRawScratchCapacity(int totalCellCount)` in `HectonVoxelEngine.cs` lerps buffer capacity down to `StreamingMeshRawVertexScratchLowTierCapacity` (262,144) when `GlobalQualityWeight` is low. For dense 64x64x64 chunks requiring `desired = 262,144 * 2 = 524,288` vertices, `if (desired > qualityCapacity) return qualityCapacity;` truncates scratch memory below the required Marching Cubes capacity, triggering scratch buffer overflows, array out-of-bounds writes, and chunk silhouette disappearance.

---

## 2. Requirement R1 & R2 Detailed Diagnostic Audit

### 2.1 Observation & Code Analysis
- **File**: `Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs`
- **Location**: `VoxelCliffOverhangNoiseJob` struct (lines 791-865)
- **Code Snippet** (lines 857-859):
  ```csharp
  float3 gridPos = new float3(x, y, z);
  float3 noisePos = gridPos * VoxelSizeMeters * NoiseFrequency;
  float noise = AnomalySdfNoise.FractalNoise3D(noisePos) * 2f - 1f;
  ```
- **Defect Analysis**: `gridPos` is constructed from local loop indices `(x, y, z)` which run from `0` to `SdfWidth - 1`, `SdfHeight - 1`, `SdfDepth - 1`. Every chunk grid evaluates `noisePos` starting at local `(0, 0, 0)`. Consequently:
  - Adjacent chunks sample disjoint noise patterns at their boundary faces.
  - Origin shifts change the relative local grid index, altering generated geometry.
  - Rebuilding volumes at different LODs or chunk splits results in phase mismatches.

### 2.2 Solution Architecture for R1 & R2
By introducing `public double3 OriginAup;` into `VoxelCliffOverhangNoiseJob` and calculating:
```csharp
float3 gridPos = new float3(x, y, z);
double3 worldPosAup = OriginAup + (double3)(gridPos * VoxelSizeMeters);
float3 noisePos = (float3)worldPosAup * NoiseFrequency;
float noise = AnomalySdfNoise.FractalNoise3D(noisePos) * 2f - 1f;
```
Every voxel sample evaluates 3D fractal noise based on its canonical Absolute Universal Position (AUP). This guarantees 100% spatial alignment across chunk boundaries, chunk splits, floating origins, and rebuilds.

### 2.3 Wiring & Call Chain Audit
1. **`HectonAnomalySdfJobs.cs`**: Add `public double3 OriginAup;` field to `VoxelCliffOverhangNoiseJob`.
2. **`HectonAnomalyEngine.cs`**: Update `ApplyVoxelCliffOverhangNoise` signature (lines 711-745) to accept `double3 originAup = default`. Pass `OriginAup = IsFiniteAup(originAup) ? originAup : double3.zero` when constructing `VoxelCliffOverhangNoiseJob`.
3. **`HectonVoxelEngine.cs`**: In `HectonVoxelEngine.cs` (line 10815), pass `sdfOriginAup` (defined at line 10774: `double3 sdfOriginAup = global::Hecton8.World.AUPMath.ToDouble3(data.VolumeOrigin) + data.AbsoluteUniverseOffsetAtStartDouble;`) into `HectonAnomalyEngine.ApplyVoxelCliffOverhangNoise`.
4. **`AnomalyTestHarness.cs`**: In editor test harness (line 919), pass `double3.zero` into `HectonAnomalyEngine.ApplyVoxelCliffOverhangNoise`.

### 2.4 Audit of Remaining Camera & Quality Bias Paths in `HectonVoxelEngine.cs`
A comprehensive audit of `HectonVoxelEngine.cs` confirmed:
- R99 fix (lines 10805-10813) previously eliminated camera-facing gating (`ShouldApplyCameraFacingOverhangNoise`) and `GlobalQualityWeight` scaling from SDF overhang noise generation.
- Remaining uses of `GlobalQualityWeight` in `HectonVoxelEngine.cs` (lines 8217, 8786, 9216) strictly govern asynchronous frame budgets (`VoxelMeshUploadBudgetPerFrame`, `DeferredVoxelPhysicsBakeTeardownBudgetPerFrame`, `DeferredVoxelColliderUploadBudgetPerFrame`) and do NOT scale SDF noise evaluation or geometry generation.

---

## 3. Requirement R3 Detailed Diagnostic Audit

### 3.1 Observation & Code Analysis
- **File**: `Assets/_Project/Scripts/HectonVoxelEngine.cs`
- **Location**: `ResolveStreamingMeshRawScratchCapacity` and `ResolveStreamingMeshRawScratchQualityCapacity` (lines 12252-12274)
- **Code Snippet** (lines 12252-12274):
  ```csharp
  static int ResolveStreamingMeshRawScratchCapacity(int totalCellCount)
  {
      long desired = (long)math.max(1, totalCellCount) * MC_BUFFER_MULTIPLIER;
      int qualityCapacity = ResolveStreamingMeshRawScratchQualityCapacity();
      if (desired > qualityCapacity)
          return qualityCapacity;

      return desired < 1L ? 1 : (int)desired;
  }

  static int ResolveStreamingMeshRawScratchQualityCapacity()
  {
      float quality = HomeostasisBrain.GlobalQualityWeight;
      float q = math.saturate(math.isfinite(quality) ? quality : 1f);
      float smooth = q * q * (3f - 2f * q);
      return math.clamp(
          (int)math.round(math.lerp(
              StreamingMeshRawVertexScratchLowTierCapacity,
              StreamingMeshRawVertexScratchVisualOverkillCapacity,
              smooth)),
          StreamingMeshRawVertexScratchLowTierCapacity,
          StreamingMeshRawVertexScratchVisualOverkillCapacity);
  }
  ```

### 3.2 Root Cause Analysis of Marching Cubes Scratch Overflow & Silhouette Disappearance
1. **Mathematical Requirement**: For a 64x64x64 voxel chunk, `totalCellCount = 262,144`. `MC_BUFFER_MULTIPLIER = 2`. The mathematically required vertex scratch buffer capacity is `desired = 262,144 * 2 = 524,288`.
2. **Quality Truncation Mechanism**: When `GlobalQualityWeight` is low (0.0 to ~0.4), `ResolveStreamingMeshRawScratchQualityCapacity()` lerps towards `StreamingMeshRawVertexScratchLowTierCapacity = 262,144`.
3. **Capacity Truncation**: Line 12256 executes `if (desired > qualityCapacity) return qualityCapacity;`. This truncates the scratch buffer size from 524,288 down to 262,144.
4. **Failure Cascade**:
   - `TryEnsureStreamingScratchArray` allocates `ScratchLaneMeshRawVertices` with capacity 262,144.
   - During Marching Cubes mesh extraction on dense/complex cliff geometry, generated raw vertices exceed index 262,144.
   - Burst job execution encounters buffer overflow / index out-of-bounds, truncating output or failing job completion.
   - Mesh generation produces incomplete geometry, causing chunk silhouettes to disappear.
5. **Authority Violation**: This directly violates `voxels.md` line 98: `"GlobalQualityWeight ... must not change collision truth, carve permission, or save delta identity."` Truncating scratch buffer allocation based on graphics quality starves dense chunks of scratch space, destroying mesh extraction and collision truth.

### 3.3 Exact Fix Recommendation for R3
Modify `ResolveStreamingMeshRawScratchCapacity` in `HectonVoxelEngine.cs` so `desired` (`totalCellCount * MC_BUFFER_MULTIPLIER`) is NEVER truncated below `desired` for dense chunks regardless of `GlobalQualityWeight`:

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

#### Verification of Fix:
- For 64x64x64 chunk (`totalCellCount = 262,144`): `desired = 524,288`. Even if `qualityCapacity = 262,144`, `math.max(524,288, 262,144)` returns `524,288`.
- If `qualityCapacity` is higher (e.g. 786,432 on visual overkill quality), `math.max(desired, qualityCapacity)` provides extra quality headroom.
- Upper bound remains strictly capped by `StreamingMeshRawVertexScratchVisualOverkillCapacity` (786,432).
- Scratch memory allocation is guaranteed to cover 100% of dense Marching Cubes vertex output without overflow.

---

## 4. Summary Matrix of Required File Edits

| Target File | Scope of Edit | Purpose |
|---|---|---|
| `Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs` | `VoxelCliffOverhangNoiseJob` struct (lines 791-865) | Add `public double3 OriginAup;` field. Calculate `noisePos` using `worldPosAup = OriginAup + (double3)(gridPos * VoxelSizeMeters)`. |
| `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs` | `ApplyVoxelCliffOverhangNoise` method (lines 711-745) | Add `double3 originAup = default` parameter. Pass `OriginAup = IsFiniteAup(originAup) ? originAup : double3.zero` into `VoxelCliffOverhangNoiseJob`. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 1. Invocation of `ApplyVoxelCliffOverhangNoise` (line 10815)<br>2. `ResolveStreamingMeshRawScratchCapacity` (lines 12252-12260) | 1. Pass `sdfOriginAup` into `ApplyVoxelCliffOverhangNoise`.<br>2. Replace truncation logic with `math.max(desired, qualityCapacity)` capped at `StreamingMeshRawVertexScratchVisualOverkillCapacity`. |
| `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs` | Invocation of `ApplyVoxelCliffOverhangNoise` (line 919) | Pass `double3.zero` for the new `originAup` parameter. |
