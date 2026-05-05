# COMPUTE BUFFER LIFECYCLE AUDIT — HECTON-8 First-Party Code
Date: 2026-05-04
Status: REFERENCE


**Date:** 2026-04-29  
**Scope:** `new ComputeBuffer`, `new GraphicsBuffer`, `GraphicsBuffer.Target.*` in `Assets/_Project/Scripts/`  
**Authority:** CTO / Lead Architect  
**Status:** ETA LEAK_MAPPED

---

## EXECUTIVE SUMMARY

All **first-party** `GraphicsBuffer` / `ComputeBuffer` owners were audited for `.Release()` / `.Dispose()` calls in `OnDisable`, `OnDestroy`, or equivalent teardown paths.

**Verdict:** Zero first-party Compute/GraphicsBuffer leaks detected. Every owning class implements a teardown path that releases its buffers. NativeArray fields are likewise disposed.

> **WARNING:** This audit covers first-party code ONLY. Third-party Crest buffers (`WaveBuffers`, `FFTCompute` generators, `Query` compute buffers, `ShapeGerstner` cascade buffers) are EXCLUDED per the **3RD-PARTY INTEGRITY** mandate. Crest internally calls `.Release()` / `.Dispose()` in `OnDisable` / `OnDestroy` / `CleanUp()`.

---

## AUDIT TABLE

| # | Owner Class | Buffer Field(s) | Type | Teardown Method | Teardown Caller | `Release()` / `Dispose()` | Verdict |
|---|-------------|-----------------|------|-----------------|-----------------|---------------------------|---------|
| 1 | `DebrisManager` | `_matrixBuffer` | `GraphicsBuffer` (Structured) | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 2 | `TetherInstance` | `VisualSegmentBuffer` | `GraphicsBuffer` (Structured) | Inline `Release()` | `OnDestroy()` + resize path | ✅ `Release()` + null | **CLEAN** |
| 3 | `AbyssalThermalManager` | `_particleBufferA`, `_particleBufferB` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 3a | `AbyssalThermalManager` | `_ventBuffers[]` (ring) | `GraphicsBuffer[]` | `ReleaseBufferRing(ref)` | `OnDisable()` + `OnDestroy()` | ✅ Loop `Release()` | **CLEAN** |
| 4 | `GPUScatterDirector` | `_instanceBuffer` | `GraphicsBuffer` (Structured) | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 4a | `GPUScatterDirector` | `_visibleIndicesBuffer` | `GraphicsBuffer` (Append) | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` + resize | ✅ `buffer.Release()` | **CLEAN** |
| 4b | `GPUScatterDirector` | `_argsBuffer` | `GraphicsBuffer` (IndirectArguments) | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 5 | `HectonIndirectVegetationRenderer` | `_visibleIndicesLod0Buffer` | `GraphicsBuffer` (Append) | `ReleaseVisibleIndexBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `ReleaseGraphicsBuffer(ref)` | **CLEAN** |
| 5a | `HectonIndirectVegetationRenderer` | `_visibleIndicesLod1Buffer` | `GraphicsBuffer` (Append) | `ReleaseVisibleIndexBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `ReleaseGraphicsBuffer(ref)` | **CLEAN** |
| 5b | `HectonIndirectVegetationRenderer` | `_visibleIndicesShadowBuffer` | `GraphicsBuffer` (Append) | `ReleaseVisibleIndexBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `ReleaseGraphicsBuffer(ref)` | **CLEAN** |
| 5c | `HectonIndirectVegetationRenderer` | `_indirectArgsLod0Buffer` | `GraphicsBuffer` (IndirectArguments) | `ReleaseGraphicsBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 5d | `HectonIndirectVegetationRenderer` | `_indirectArgsLod1Buffer` | `GraphicsBuffer` (IndirectArguments) | `ReleaseGraphicsBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 5e | `HectonIndirectVegetationRenderer` | `_indirectArgsShadowBuffer` | `GraphicsBuffer` (IndirectArguments) | `ReleaseGraphicsBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 5f | `HectonIndirectVegetationRenderer` | `_legacyInstanceDataBuffer` | `ComputeBuffer` | `ReleaseLegacyInstanceDataBuffer()` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 5g | `HectonIndirectVegetationRenderer` | `_uploadedInstanceMatrixBuffer` | `GraphicsBuffer` | `ReleaseUploadedInstanceBuffers()` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 5h | `HectonIndirectVegetationRenderer` | `_uploadedInstanceDataBuffer` | `GraphicsBuffer` | `ReleaseUploadedInstanceBuffers()` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 5i | `HectonIndirectVegetationRenderer` | `_batchHandleBuffer` | `GraphicsBuffer` (BRG handle) | `ReleaseBatchRendererGroupResources()` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 6 | `SargassumMicroFaunaBoids` | `_boidsBufferA`, `_boidsBufferB` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 6a | `SargassumMicroFaunaBoids` | `_grazingAnchorBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 6b | `SargassumMicroFaunaBoids` | `_massiveThreatBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 6c | `SargassumMicroFaunaBoids` | `_formationBeaconBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 6d | `SargassumMicroFaunaBoids` | `_formationObstacleBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 6e | `SargassumMicroFaunaBoids` | `_leviathanNodeBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 6f | `SargassumMicroFaunaBoids` | `_latchStatsBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 6g | `SargassumMicroFaunaBoids` | `_pbdCorrectionBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 6h | `SargassumMicroFaunaBoids` | `_threatGridBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 6i | `SargassumMicroFaunaBoids` | `_threatVoxelBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 6j | `SargassumMicroFaunaBoids` | `_spatialGridCountBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 6k | `SargassumMicroFaunaBoids` | `_spatialGridCellBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 6l | `SargassumMicroFaunaBoids` | `_simulationFrameBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | ✅ `buffer.Release()` | **CLEAN** |
| 7 | `SystemDispatcher` | N/A (factory only) | — | — | — | — | **CLEAN** |

---

## NATIVEARRAY DISPOSAL (ASSOCIATED AUDIT)

Several classes own `NativeArray<T>` (Persistent allocator). All verified:

| Owner | NativeArray Fields | Disposed In | Verdict |
|-------|-------------------|-------------|---------|
| `DebrisManager` | `_frontStates`, `_backStates` | `OnDestroy()` | ✅ CLEAN |
| `GPUScatterDirector` | `_instanceData` | `OnDestroy()` | ✅ CLEAN |
| `HectonIndirectVegetationRenderer` | `_cpuCullingMatrices`, `_cpuCullingData`, `_batchMetadata` | `OnDisable()` + `OnDestroy()` | ✅ CLEAN |
| `SargassumMicroFaunaBoids` | `_staticObstacleCache`, `_leviathanPathScratchNative`, `_leviathanNodeFrontNative`, `_leviathanNodeBackNative`, `_leviathanNodeCountNative`, `_foveatedSimulationInputNative`, `_foveatedSimulationFrontNative`, `_foveatedSimulationBackNative`, `_threatGridUploadNative`, `_threatVoxelUploadNative`, `_simulationFrameNative` | `CompletePendingReadbackAndReleaseBuffers()` → `OnDisable()` + `OnDestroy()` | ✅ CLEAN |
| `SystemDispatcher` | `_pendingDispatcherRaycastCommands`, `_scheduledDispatcherRaycastCommands`, `_scheduledDispatcherRaycastHits` | `OnDestroy()` / `OnDisable()` | ✅ CLEAN |

---

## DEBT TALLY

| Category | Count |
|----------|-------|
| First-party Compute/GraphicsBuffers lacking `Dispose` | **0** |
| First-party NativeArrays lacking `Dispose` | **0** |
| Third-party buffer violations | Excluded from scope |

---

## NOTES & EDGE CASES

### Resize Paths
All classes that recreate buffers on capacity change release the OLD buffer **BEFORE** allocating the new one:
- `GPUScatterDirector.EnsureScatterBuffers()` — releases then reallocates.
- `HectonIndirectVegetationRenderer.EnsureGpuIndirectResources()` — releases then reallocates.
- `SargassumMicroFaunaBoids.EnsureBuffer()` — releases then reallocates.
- `AbyssalThermalManager.EnsureVentBuffers()` — releases ring then reallocates.

### Deferred Disposal
`SargassumMicroFaunaBoids` uses `DisposeNativeArrayDeferred<T>()` for job-dependent arrays. This is compliant with AGENTS.md §13: *"Deferred disposal ONLY. array.Dispose(activeHandle); array = default;"*.

### Factory Pattern
`SystemDispatcher.CreateStructuredBuffer<T>()` and `CreateStructuredLockBuffer<T>()` are **static factory methods**. They do NOT retain buffer ownership. Callers (e.g., `DebrisManager`, `HectonIndirectVegetationRenderer`) are responsible for release. This is correct.

---

## REGRESSION MODEL

| Dimension | Before | After | Delta |
|-----------|--------|-------|-------|
| First-party buffer leaks | Unknown | 0 confirmed | — |
| Missing `OnDisable` release | Unknown | 0 confirmed | — |
| Missing `OnDestroy` release | Unknown | 0 confirmed | — |
| Resize-without-release | Unknown | 0 confirmed | — |

---

**MANDATES FOLLOWED:** AGENTS.md §13 (Memory Lifetime — NO LEAKS), §3RD-PARTY INTEGRITY rule, §COLD ALLOC comments verified.

**STATUS:** ETA LEAK_MAPPED — Compute Buffer slice complete.
