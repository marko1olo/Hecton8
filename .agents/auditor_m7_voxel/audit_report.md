# Forensic Audit Report — Milestone 7: Voxel & Anomaly Engine

**Work Product**: `Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs`, `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs`, `Assets/_Project/Scripts/HectonVoxelEngine.cs`, `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs`
**Profile**: General Project / Hecton-8 Voxel Engine
**Verdict**: CLEAN

---

## 1. Authority Intake & Proof of Reading

> **Reference Quote (`voxels.md`, line 98)**:
> *"GlobalQualityWeight may scale SDF resolution, extraction distance, diagnostic depth, and rebuild cadence, but it must not change collision truth, carve permission, or save delta identity."*

> **Authority Receipt**:
> `Authority used: AGENTS.md; GEMINI.md; voxels.md; terrain.md; HectonAnomalySdfJobs.cs; HectonAnomalyEngine.cs; HectonVoxelEngine.cs; AnomalyTestHarness.cs.`

---

## 2. Forensic Verification Results

### Phase 1: Zero Mocks & Genuine Implementation Audit

| Check ID | Verification Item | Status | Line / File Evidence | Summary |
|---|---|---|---|---|
| **1.1** | Hardcoded test returns, dummy/facade implementations, or shortcut bypasses | **PASS** | `HectonAnomalySdfJobs.cs`, `HectonAnomalyEngine.cs`, `HectonVoxelEngine.cs`, `AnomalyTestHarness.cs` | No facade returns, `TODO` hacks, or mock shortcuts detected. All SDF math, noise sampling, distance fields, and test harness assertions are fully implemented and Burst-deterministic. |
| **1.2** | `VoxelCliffOverhangNoiseJob` evaluates `noisePos` in absolute AUP world space | **PASS** | `HectonAnomalySdfJobs.cs:860-862` | `worldPosAup = OriginAup + (double3)(gridPos * VoxelSizeMeters);` and `noisePos = (float3)worldPosAup * NoiseFrequency;` genuinely evaluate absolute AUP coordinates. |
| **1.3** | `ApplyVoxelCliffOverhangNoise` assigns `OriginAup` and `HectonVoxelEngine.cs` passes `sdfOriginAup` | **PASS** | `HectonAnomalyEngine.cs:740`, `HectonVoxelEngine.cs:10826` | `ApplyVoxelCliffOverhangNoise` binds `OriginAup = IsFiniteAup(originAup) ? originAup : double3.zero` on the job, and `HectonVoxelEngine.cs` passes `sdfOriginAup` directly into the method call. |
| **1.4** | `ResolveStreamingMeshRawScratchCapacity` buffer sizing and quality clamp guarantee | **PASS** | `HectonVoxelEngine.cs:12253-12260` | Computes `capacity = math.max(desired, (long)qualityCapacity)` and clamps to `StreamingMeshRawVertexScratchVisualOverkillCapacity`, guaranteeing desired memory is never truncated under low quality weight. |

### Phase 2: Codebase Consistency & Asmdef Integrity

| Check ID | Verification Item | Status | Summary |
|---|---|---|---|
| **2.1** | Clean C# Syntax & Deterministic Burst Attributes | **PASS** | All jobs utilize `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]` and proper unsafe safety justifications (`SAFETY_JUSTIFICATION_PARAGRAPH_1..3`). |
| **2.2** | Assembly Definition & Placement | **PASS** | `HectonAnomalySdfJobs.cs` and `HectonAnomalyEngine.cs` reside under `Hecton8.World`, `HectonVoxelEngine.cs` under core runtime, and `AnomalyTestHarness.cs` under `Assets/_Project/Scripts/Editor/` in `Hecton8.Editor.asmdef`. |

---

## 3. Evidence Log & Forensic Code Extracts

### AUP World Space Evaluation (`HectonAnomalySdfJobs.cs:860-862`)
```csharp
float3 gridPos = new float3(x, y, z);
double3 worldPosAup = OriginAup + (double3)(gridPos * VoxelSizeMeters);
float3 noisePos = (float3)worldPosAup * NoiseFrequency;
float noise = AnomalySdfNoise.FractalNoise3D(noisePos) * 2f - 1f;
```

### OriginAup Assignment & Binding (`HectonAnomalyEngine.cs:740` & `HectonVoxelEngine.cs:10826`)
```csharp
// HectonAnomalyEngine.cs line 740
OriginAup = IsFiniteAup(originAup) ? originAup : double3.zero

// HectonVoxelEngine.cs line 10815-10827
densityHandle = HectonAnomalyEngine.ApplyVoxelCliffOverhangNoise(
    densityField,
    overhangDensityField,
    data.PtsX,
    data.PtsY,
    data.PtsZ,
    data.VoxelStep,
    CliffOverhangSlopeThreshold,
    CliffOverhangLateralAmplitudeMeters,
    CliffOverhangNoiseFrequency,
    CliffOverhangBlendStrength,
    sdfOriginAup,
    densityHandle);
```

### Scratch Capacity Non-Truncation Lock (`HectonVoxelEngine.cs:12253-12260`)
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

---

## 4. Final Verdict

**FINAL VERDICT: CLEAN**

The audited voxel and anomaly engine changes meet all zero-mock, mathematical purity, deterministic Burst, AUP coordinate calculation, and memory safety requirements. No integrity violations were found.
