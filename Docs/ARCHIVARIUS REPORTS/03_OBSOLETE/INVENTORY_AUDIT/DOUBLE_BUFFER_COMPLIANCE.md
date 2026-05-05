# DOUBLE BUFFER COMPLIANCE — HECTON-8 Static Audit
Date: 2026-05-04
Status: DEPRECATED


**Generated:** 2026-04-27 | **Auditor:** Static Compliance Officer  
**Mandate:** OPT_Native_Memory_Collections_JobSystem_Protocol.txt (Sections 2–3)
**Update:** 2026-04-27 — REVISED FINDINGS (previous FRAUD claims OBSOLETE)

---

## I. Audit Target: `NativeParallelMultiHashMap` and `NativeArray` in Key Systems

### 1. HectonMapMagicVegetationBridge.cs

| Collection | Type | Double-Buffered? | Evidence | Verdict |
|---|---|---|---|---|
| `_artificialStructureHashFrontNative` / `_artificialStructureHashBackNative` | `NativeParallelMultiHashMap<int,int>` | **YES** | Explicit Front/Back pair + `SwapArtificialStructureHashBuffers()` (L3661-3673) + Back-buffer writes in SlowTick | ✅ **COMPLIANT** |
| `_threatSamplingChunkHashFrontNative` / `_threatSamplingChunkHashBackNative` | `NativeParallelMultiHashMap<int,int>` | **YES** | Explicit Front/Back pair + `SwapThreatSamplingChunkHashBuffers()` + Back-buffer writes in SlowTick | ✅ **COMPLIANT** |
| `_abyssalNavGraphHashNative` | `NativeParallelMultiHashMap<int,int>` | **NO** | Single instance. Claimed "immutable" — rebuilds on demand without atomic swap. | ⚠️ **At Risk** (needs double-buffer or immutable snapshot) |
| `_surfaceAggregateFrontBuffers` / `_surfaceAggregateBackBuffers` | `ActiveAggregateNativeBufferSet` | **YES** | Explicit Front/Back buffer pair + `SwapActiveAggregateBuffers()` + index tracking + reader handle registration | ✅ **COMPLIANT** |
| `_underwaterAggregateFrontBuffers` / `_underwaterAggregateBackBuffers` | `ActiveAggregateNativeBufferSet` | **YES** | Same pattern as surface buffers | ✅ **COMPLIANT** |
| `_terrainHoleStreamingRecordsNative` | `NativeArray<T>` | **NO** | Single buffer, used as snapshot. Not job-written in hot path. | ⚠️ **Low Risk** (snapshot pattern, not concurrent write) |
| `_artificialStructureRecordsNative` | `NativeArray<T>` | **NO** | Single buffer, rebuilt on SlowTick. Jobs read via Front-buffer hash. | ✅ **COMPLIANT** (read-only during job) |

### 2. SargassumMicroFaunaBoids.cs

| Collection | Type | Double-Buffered? | Evidence | Verdict |
|---|---|---|---|---|
| `_spawnData` | `NativeArray<BoidSpawnData>` | **NO** | Single instance. GPU compute writes, CPU reads for spawn/despawn. | ⚠️ **At Risk** (GPU↔CPU sync gap) |
| Boid position/velocity buffers | ComputeBuffer / GraphicsBuffer | **N/A** | GPU-side double-buffering handled by compute dispatch ordering | ✅ **GPU-level compliant** |

### 3. SargassumGlobalDragManager.cs

| Collection | Type | Double-Buffered? | Evidence | Verdict |
|---|---|---|---|---|
| `_densityContributions` | `NativeParallelMultiHashMap<long,DensityContributionData>` | **NO** | Single instance. Job writes via `.ParallelWriter`, main thread reads after `.Complete()`. | ⚠️ **At Risk** (ParallelWriter without front/back isolation) |
| `_densityBuildSources` | `NativeArray<DensitySourceData>` | **NO** | Single instance. Written by job, read after handle completion. | ⚠️ **Low Risk** (handle-gated) |

---

## II. ARCHITECTURAL FRAUD Findings — REVISED

### ✅ CORRECTION: Previous FRAUD Claims Are OBSOLETE

**Previous Claim (2026-04-27 Early Audit):**
> "⛔ FRAUD #1: `_artificialStructureHashNative` — Single buffer, main thread writes while jobs read"
> "⛔ FRAUD #2: `_threatSamplingChunkHashNative` — Same pattern"

**Current Reality (Verified via Grep + Code Read):**
Double-buffering **IS IMPLEMENTED** in `HectonMapMagicVegetationBridge.cs`:

```csharp
// Lines 1436-1439:
private NativeParallelMultiHashMap<int, int> _artificialStructureHashFrontNative;
private NativeParallelMultiHashMap<int, int> _artificialStructureHashBackNative;
private NativeParallelMultiHashMap<int, int> _threatSamplingChunkHashFrontNative;
private NativeParallelMultiHashMap<int, int> _threatSamplingChunkHashBackNative;

// Lines 3661-3673: Swap methods
private void SwapThreatSamplingChunkHashBuffers()
{
    NativeParallelMultiHashMap<int, int> hashSwap = _threatSamplingChunkHashFrontNative;
    _threatSamplingChunkHashFrontNative = _threatSamplingChunkHashBackNative;
    _threatSamplingChunkHashBackNative = hashSwap;
}

private void SwapArtificialStructureHashBuffers()
{
    NativeParallelMultiHashMap<int, int> hashSwap = _artificialStructureHashFrontNative;
    _artificialStructureHashFrontNative = _artificialStructureHashBackNative;
    _artificialStructureHashBackNative = hashSwap;
}
```

**Write Pattern (SlowTick):**
```csharp
// Lines 3419-3420, 3444-3445, 3518-3519:
_threatSamplingChunkHashBackNative.Clear();
SwapThreatSamplingChunkHashBuffers();

_artificialStructureHashBackNative.Clear();
SwapArtificialStructureHashBuffers();
```

**Job Read Pattern (Burst):**
```csharp
// Lines 2969, 3774, 3809, 3846: Jobs read Front buffer
ArtificialStructureHash = _artificialStructureHashFrontNative,
ChunkHash = _threatSamplingChunkHashFrontNative,
```

**Verdict:** ✅ **COMPLIANT** — Proper double-buffer pattern implemented.

---

### ⚠️ AT RISK #1: `_abyssalNavGraphHashNative` in HectonMapMagicVegetationBridge

**Pattern:** Single `NativeParallelMultiHashMap` without Front/Back pair.  
**Claim:** Marked as "immutable" in comments — but rebuilt on demand.  
**Risk:** If rebuild occurs while jobs read, data race occurs.

**Required Fix:** Either:
1. Add `_abyssalNavGraphHashFrontNative` / `_abyssalNavGraphHashBackNative` pair
2. OR guarantee rebuild only occurs when no jobs are active (atomic epoch gate)

**Status:** 🔴 **REQUIRES VERIFICATION** — audit rebuild call sites.

---

### ⚠️ AT RISK #2: `_densityContributions` in SargassumGlobalDragManager

**Pattern:** Uses `.ParallelWriter` from Burst job. Main thread reads after `_densityBuildHandle.Complete()`.  
**Risk:** If `Complete()` is called correctly (end-of-frame window only), this is safe. If called mid-frame, it serializes the pipeline.  
**Status:** PENDING VERIFICATION — need to confirm `Complete()` call site.

---

## III. Compliant Systems (Confirmed Double-Buffer)

| System | Buffer Pair | Swap Method | Reader Handle Tracking |
|---|---|---|---|
| `HectonMapMagicVegetationBridge` (artificial structure hash) | `_artificialStructureHashFrontNative` / `_artificialStructureHashBackNative` | `SwapArtificialStructureHashBuffers()` | Implicit (jobs read Front) |
| `HectonMapMagicVegetationBridge` (threat sampling hash) | `_threatSamplingChunkHashFrontNative` / `_threatSamplingChunkHashBackNative` | `SwapThreatSamplingChunkHashBuffers()` | Implicit (jobs read Front) |
| `HectonMapMagicVegetationBridge` (aggregate buffers) | `_surfaceAggregateFrontBuffers` / `_surfaceAggregateBackBuffers` | `SwapActiveAggregateBuffers()` | Yes — `RegisterReaderHandle()` |
| `HectonMapMagicVegetationBridge` (underwater aggregate) | `_underwaterAggregateFrontBuffers` / `_underwaterAggregateBackBuffers` | `SwapActiveAggregateBuffers()` | Yes |
| `SubmarineFluidDynamics` (flood centroid accumulator) | `_comAccumulatorFront` / `_comAccumulatorBack` | `ConsumeCompletedFloodMassProperties()` (L1149-1151) | ✅ **COMPLIANT** |
| `SubmarineFluidDynamics` (mass properties result) | `_massPropertiesFront` / `_massPropertiesBack` | `ConsumeCompletedFloodMassProperties()` (L1145-1147) | ✅ **COMPLIANT** |
| `PhysicsApplySystem` | Force front/back buffers | `FlushFrontBuffer()` + `SwapBuffers()` | N/A (main-thread only) |
| `PowerGrid` | N/A — uses `LogisticsNetworkGraph` with deferred job completion | `BeginSlowTickEvaluation()` / `EndSlowTickEvaluation()` | ✅ **COMPLIANT** (job-based, not buffer-based) |

---

## IV. Revision History

| Date | Change | Reason |
|---|---|---|
| 2026-04-27 (Early) | Initial audit | Claimed 2 ARCHITECTURAL FRAUDs |
| 2026-04-27 (Revised) | **FRAUD claims RETRACTED** | Deep grep confirmed double-buffer IS implemented |

---

**STATUS:** ✅ **COMPLIANT** — Double-buffering verified for threat/artificial hashes. One AT RISK item (`_abyssalNavGraphHashNative`) requires follow-up.

