> **PROOF OF READING**: Quote `voxels.md` line 98: `"GlobalQualityWeight may scale SDF resolution, extraction distance, diagnostic depth, and rebuild cadence, but it must not change collision truth, carve permission, or save delta identity."`
> Authority files loaded & verified: `C:\hades\Hecton8\AGENTS.md`, `C:\hades\Hecton8\GEMINI.md`, `C:\hades\Hecton8\voxels.md`, `C:\hades\Hecton8\terrain.md`.

# Handoff Report — Milestone 5 Voxel SDF Sampling Reconnaissance & Root Cause Audit

## 1. Observation

### 1.1 Source Observations
1. **`Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs` (lines 857-859)**:
   ```csharp
   float3 gridPos = new float3(x, y, z);
   float3 noisePos = gridPos * VoxelSizeMeters * NoiseFrequency;
   float noise = AnomalySdfNoise.FractalNoise3D(noisePos) * 2f - 1f;
   ```
   `gridPos` is local array index coordinates `(x, y, z)` starting at `0` for every chunk. Evaluating `noisePos` from local coordinates resets noise phase across chunk boundaries, floating origin shifts, and LOD splits.

2. **`Assets/_Project/Scripts/World/HectonAnomalyEngine.cs` (lines 711-745)**:
   `ApplyVoxelCliffOverhangNoise` schedules `VoxelCliffOverhangNoiseJob` but lacks an `originAup` parameter.

3. **`Assets/_Project/Scripts/HectonVoxelEngine.cs` (lines 10815-10826)**:
   `ApplyVoxelCliffOverhangNoise` is called without passing `sdfOriginAup` (which is already defined at line 10774: `double3 sdfOriginAup = global::Hecton8.World.AUPMath.ToDouble3(data.VolumeOrigin) + data.AbsoluteUniverseOffsetAtStartDouble;`).

4. **`Assets/_Project/Scripts/HectonVoxelEngine.cs` (lines 12252-12260)**:
   ```csharp
   static int ResolveStreamingMeshRawScratchCapacity(int totalCellCount)
   {
       long desired = (long)math.max(1, totalCellCount) * MC_BUFFER_MULTIPLIER;
       int qualityCapacity = ResolveStreamingMeshRawScratchQualityCapacity();
       if (desired > qualityCapacity)
           return qualityCapacity;

       return desired < 1L ? 1 : (int)desired;
   }
   ```
   For a dense 64x64x64 chunk (`totalCellCount = 262,144`), `desired = 524,288`. When `GlobalQualityWeight` is low (0), `qualityCapacity = 262,144`. `if (desired > qualityCapacity) return qualityCapacity;` truncates scratch memory allocation to 262,144, causing Marching Cubes buffer overflow, array out-of-bounds writes, and missing chunk silhouettes.

5. **`Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs` (lines 919-929)**:
   Editor harness calls `HectonAnomalyEngine.ApplyVoxelCliffOverhangNoise(...)` without `originAup`.

---

## 2. Logic Chain

1. **R1 & R2 Noise Alignment & Determinism**:
   - *Observation*: `gridPos` is local to chunk grid `(x, y, z)` starting at 0.
   - *Deduction*: Two neighboring chunks at world coordinates `(100, 0, 0)` and `(164, 0, 0)` evaluate noise from `0` to `64` relative to their own origins. The fractal noise is discontinuous across the chunk boundary seam.
   - *Deduction*: Floating origin shifts alter local origin alignment, generating different terrain geometry for the same world location.
   - *Conclusion*: Passing canonical `double3 OriginAup` into `VoxelCliffOverhangNoiseJob` and calculating `worldPosAup = OriginAup + (double3)(gridPos * VoxelSizeMeters)` ensures `noisePos = (float3)worldPosAup * NoiseFrequency` is evaluated in absolute world coordinates, restoring 100% spatial alignment and determinism across chunk splits and origins.

2. **R3 Capacity Overflow Prevention**:
   - *Observation*: `ResolveStreamingMeshRawScratchQualityCapacity()` lerps to 262,144 on low `GlobalQualityWeight`. `ResolveStreamingMeshRawScratchCapacity` returns `qualityCapacity` if `desired > qualityCapacity`.
   - *Deduction*: Marching Cubes on a 64x64x64 dense chunk generates up to `524,288` vertices. Truncating the allocation to 262,144 starves Marching Cubes scratch memory.
   - *Deduction*: This violates `voxels.md` line 98: `"GlobalQualityWeight ... must not change collision truth, carve permission, or save delta identity."`
   - *Conclusion*: `ResolveStreamingMeshRawScratchCapacity` must ensure `capacity = math.max(desired, qualityCapacity)` capped at `StreamingMeshRawVertexScratchVisualOverkillCapacity`, so `desired` is NEVER truncated below `desired` for dense chunks regardless of quality tier.

---

## 3. Caveats

- **Read-Only Scope**: This report is read-only; no C# source files were edited during this audit phase.
- **Worker Execution Requirement**: Worker must apply the exact diffs provided in Section 4 across all 4 affected files (`HectonAnomalySdfJobs.cs`, `HectonAnomalyEngine.cs`, `HectonVoxelEngine.cs`, `AnomalyTestHarness.cs`).
- **Editor Harness Compatibility**: `AnomalyTestHarness.cs` must pass `double3.zero` (or test origin) when calling `ApplyVoxelCliffOverhangNoise`.

---

## 4. Conclusion & Proposed Code Diffs for Implementation

### Step 1: Update `Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs`

Add `public double3 OriginAup;` to `VoxelCliffOverhangNoiseJob` and compute `noisePos` from world position.

```diff
--- a/Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs
+++ b/Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs
@@ -820,6 +820,9 @@ public struct VoxelCliffOverhangNoiseJob : Unity.Jobs.IJobParallelFor
         /// <summary>Blend strength from original SDF to displaced SDF.</summary>
         public float Strength;
 
+        /// <summary>SDF chunk origin in Absolute Universal Position (AUP).</summary>
+        public double3 OriginAup;
+
         /// <inheritdoc />
         public void Execute(int index)
         {
@@ -855,8 +858,9 @@ public struct VoxelCliffOverhangNoiseJob : Unity.Jobs.IJobParallelFor
                 return;
             }
 
             float3 gridPos = new float3(x, y, z);
-            float3 noisePos = gridPos * VoxelSizeMeters * NoiseFrequency;
+            double3 worldPosAup = OriginAup + (double3)(gridPos * VoxelSizeMeters);
+            float3 noisePos = (float3)worldPosAup * NoiseFrequency;
             float noise = AnomalySdfNoise.FractalNoise3D(noisePos) * 2f - 1f;
             float lateralSq = gx * gx + gz * gz;
             float invLateral = math.rsqrt(math.max(lateralSq, 0.0000001f));
```

### Step 2: Update `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs`

Accept `double3 originAup = default` in `ApplyVoxelCliffOverhangNoise` and pass `OriginAup` to `VoxelCliffOverhangNoiseJob`.

```diff
--- a/Assets/_Project/Scripts/World/HectonAnomalyEngine.cs
+++ b/Assets/_Project/Scripts/World/HectonAnomalyEngine.cs
@@ -719,6 +719,7 @@ public static class HectonAnomalyEngine
             float lateralAmplitudeMeters,
             float noiseFrequency,
             float strength,
+            double3 originAup = default,
             JobHandle dependency = default)
         {
             ValidateSdfBuffer(inputSdf, sdfWidth, sdfHeight, sdfDepth);
@@ -735,7 +736,8 @@ public static class HectonAnomalyEngine
                 SlopeThreshold = ResolveNonNegativeFinite(slopeThreshold, 0f),
                 LateralAmplitudeMeters = ResolveNonNegativeFinite(lateralAmplitudeMeters, 0f),
                 NoiseFrequency = ResolvePositiveFinite(noiseFrequency, 0.000001f),
-                Strength = math.isfinite(strength) ? math.saturate(strength) : 0f
+                Strength = math.isfinite(strength) ? math.saturate(strength) : 0f,
+                OriginAup = IsFiniteAup(originAup) ? originAup : double3.zero
             };
 
             int safeSdfWidth = math.max(1, sdfWidth);
```

### Step 3: Update `Assets/_Project/Scripts/HectonVoxelEngine.cs`

Pass `sdfOriginAup` in `ApplyVoxelCliffOverhangNoise` and fix `ResolveStreamingMeshRawScratchCapacity`.

```diff
--- a/Assets/_Project/Scripts/HectonVoxelEngine.cs
+++ b/Assets/_Project/Scripts/HectonVoxelEngine.cs
@@ -10823,6 +10823,7 @@ public class HectonVoxelEngine : MonoBehaviour
                 CliffOverhangLateralAmplitudeMeters,
                 CliffOverhangNoiseFrequency,
                 CliffOverhangBlendStrength,
+                sdfOriginAup,
                 densityHandle);
             densityField = overhangDensityField;
         }
@@ -12252,9 +12253,9 @@ public class HectonVoxelEngine : MonoBehaviour
     static int ResolveStreamingMeshRawScratchCapacity(int totalCellCount)
     {
         long desired = (long)math.max(1, totalCellCount) * MC_BUFFER_MULTIPLIER;
         int qualityCapacity = ResolveStreamingMeshRawScratchQualityCapacity();
-        if (desired > qualityCapacity)
-            return qualityCapacity;
-
-        return desired < 1L ? 1 : (int)desired;
+        long capacity = math.max(desired, (long)qualityCapacity);
+        capacity = math.min(capacity, (long)StreamingMeshRawVertexScratchVisualOverkillCapacity);
+        return capacity < 1L ? 1 : (int)capacity;
     }
```

### Step 4: Update `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs`

Pass `double3.zero` for `originAup` parameter.

```diff
--- a/Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs
+++ b/Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs
@@ -926,7 +926,8 @@ public class AnomalyTestHarness
                     0.5f,
                     0.73f,
                     1f,
-                    1f);
+                    1f,
+                    double3.zero);
 
                 // COLD SYNC JOB: Editor test harness must inspect deterministic SDF output immediately.
                 handle.Complete();
```

---

## 5. Verification Method

1. **Static / Structural Verification**:
   - Verify that `VoxelCliffOverhangNoiseJob` compiles cleanly with `BurstCompile`.
   - Verify `HectonVoxelEngine.cs`, `HectonAnomalyEngine.cs`, `HectonAnomalySdfJobs.cs`, and `AnomalyTestHarness.cs` compile with zero errors/warnings.
2. **Deterministic SDF Noise Verification**:
   - Run `AnomalyTestHarness.cs` or voxel engine tests. Verify that evaluating SDF overhang noise across chunk boundaries and floating origin shifts yields identical 3D noise values for identical world positions.
3. **Scratch Buffer Capacity Verification**:
   - Set `GlobalQualityWeight` to `0.0`. Request voxel mesh generation for a dense 64x64x64 chunk (`totalCellCount = 262,144`).
   - Confirm that `ResolveStreamingMeshRawScratchCapacity(262144)` returns `524,288` (not truncated to `262,144`).
   - Confirm that Marching Cubes completes without scratch buffer overflow errors or disappearing chunk silhouettes.
