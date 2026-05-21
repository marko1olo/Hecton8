# COMPUTE BUFFER LIFECYCLE AUDIT â€” HECTON-8 First-Party Code
Date: 2026-05-07
Status: PENDING VERIFICATION
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->



**Date:** 2026-04-29
**Scope:** `new ComputeBuffer`, `new GraphicsBuffer`, `GraphicsBuffer.Target.*` in `Assets/_Project/Scripts/`
**Authority:** CTO / Lead Architect
**Status:** ETA LEAK_MAPPED

---

## EXECUTIVE SUMMARY

All **first-party** `GraphicsBuffer` / `ComputeBuffer` owners were audited for `.Release()` / `.Dispose()` calls in `OnDisable`, `OnDestroy`, or equivalent teardown paths.

**Verdict:** Static first-party buffer ownership scan found no obvious release-path gaps in that pass. Runtime leak proof is absent.

> **WARNING:** This audit covers first-party code ONLY. Third-party Crest buffers (`WaveBuffers`, `FFTCompute` generators, `Query` compute buffers, `ShapeGerstner` cascade buffers) are EXCLUDED per the **3RD-PARTY INTEGRITY** mandate. Crest internally calls `.Release()` / `.Dispose()` in `OnDisable` / `OnDestroy` / `CleanUp()`.

---

## AUDIT TABLE

| # | Owner Class | Buffer Field(s) | Type | Teardown Method | Teardown Caller | `Release()` / `Dispose()` | Verdict |
|---|-------------|-----------------|------|-----------------|-----------------|---------------------------|---------|
| 1 | `DebrisManager` | `_matrixBuffer` | `GraphicsBuffer` (Structured) | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 2 | `TetherInstance` | `VisualSegmentBuffer` | `GraphicsBuffer` (Structured) | Inline `Release()` | `OnDestroy()` + resize path | âœ… `Release()` + null | **CLEAN** |
| 3 | `AbyssalThermalManager` | `_particleBufferA`, `_particleBufferB` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 3a | `AbyssalThermalManager` | `_ventBuffers[]` (ring) | `GraphicsBuffer[]` | `ReleaseBufferRing(ref)` | `OnDisable()` + `OnDestroy()` | âœ… Loop `Release()` | **CLEAN** |
| 4 | `GPUScatterDirector` | `_instanceBuffer` | `GraphicsBuffer` (Structured) | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 4a | `GPUScatterDirector` | `_visibleIndicesBuffer` | `GraphicsBuffer` (Append) | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` + resize | âœ… `buffer.Release()` | **CLEAN** |
| 4b | `GPUScatterDirector` | `_argsBuffer` | `GraphicsBuffer` (IndirectArguments) | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 5 | `HectonIndirectVegetationRenderer` | `_visibleIndicesLod0Buffer` | `GraphicsBuffer` (Append) | `ReleaseVisibleIndexBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `ReleaseGraphicsBuffer(ref)` | **CLEAN** |
| 5a | `HectonIndirectVegetationRenderer` | `_visibleIndicesLod1Buffer` | `GraphicsBuffer` (Append) | `ReleaseVisibleIndexBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `ReleaseGraphicsBuffer(ref)` | **CLEAN** |
| 5b | `HectonIndirectVegetationRenderer` | `_visibleIndicesShadowBuffer` | `GraphicsBuffer` (Append) | `ReleaseVisibleIndexBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `ReleaseGraphicsBuffer(ref)` | **CLEAN** |
| 5c | `HectonIndirectVegetationRenderer` | `_indirectArgsLod0Buffer` | `GraphicsBuffer` (IndirectArguments) | `ReleaseGraphicsBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 5d | `HectonIndirectVegetationRenderer` | `_indirectArgsLod1Buffer` | `GraphicsBuffer` (IndirectArguments) | `ReleaseGraphicsBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 5e | `HectonIndirectVegetationRenderer` | `_indirectArgsShadowBuffer` | `GraphicsBuffer` (IndirectArguments) | `ReleaseGraphicsBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 5f | `HectonIndirectVegetationRenderer` | `_legacyInstanceDataBuffer` | `ComputeBuffer` | `ReleaseLegacyInstanceDataBuffer()` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 5g | `HectonIndirectVegetationRenderer` | `_uploadedInstanceMatrixBuffer` | `GraphicsBuffer` | `ReleaseUploadedInstanceBuffers()` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 5h | `HectonIndirectVegetationRenderer` | `_uploadedInstanceDataBuffer` | `GraphicsBuffer` | `ReleaseUploadedInstanceBuffers()` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 5i | `HectonIndirectVegetationRenderer` | `_batchHandleBuffer` | `GraphicsBuffer` (BRG handle) | `ReleaseBatchRendererGroupResources()` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 6 | `SargassumMicroFaunaBoids` | `_boidsBufferA`, `_boidsBufferB` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 6a | `SargassumMicroFaunaBoids` | `_grazingAnchorBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 6b | `SargassumMicroFaunaBoids` | `_massiveThreatBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 6c | `SargassumMicroFaunaBoids` | `_formationBeaconBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 6d | `SargassumMicroFaunaBoids` | `_formationObstacleBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 6e | `SargassumMicroFaunaBoids` | `_leviathanNodeBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 6f | `SargassumMicroFaunaBoids` | `_latchStatsBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 6g | `SargassumMicroFaunaBoids` | `_pbdCorrectionBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 6h | `SargassumMicroFaunaBoids` | `_threatGridBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 6i | `SargassumMicroFaunaBoids` | `_threatVoxelBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 6j | `SargassumMicroFaunaBoids` | `_spatialGridCountBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 6k | `SargassumMicroFaunaBoids` | `_spatialGridCellBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 6l | `SargassumMicroFaunaBoids` | `_simulationFrameBuffer` | `GraphicsBuffer` | `ReleaseBuffer(ref)` | `OnDisable()` + `OnDestroy()` | âœ… `buffer.Release()` | **CLEAN** |
| 7 | `SystemDispatcher` | N/A (factory only) | â€” | â€” | â€” | â€” | **CLEAN** |

---

## NATIVEARRAY DISPOSAL (ASSOCIATED AUDIT)

Several classes own `NativeArray<T>` (Persistent allocator). These were statically scanned in that pass; this is not current runtime or profiler proof:

| Owner | NativeArray Fields | Disposed In | Verdict |
|-------|-------------------|-------------|---------|
| `DebrisManager` | `_frontStates`, `_backStates` | `OnDestroy()` | âœ… CLEAN |
| `GPUScatterDirector` | `_instanceData` | `OnDestroy()` | âœ… CLEAN |
| `HectonIndirectVegetationRenderer` | `_cpuCullingMatrices`, `_cpuCullingData`, `_batchMetadata` | `OnDisable()` + `OnDestroy()` | âœ… CLEAN |
| `SargassumMicroFaunaBoids` | `_staticObstacleCache`, `_leviathanPathScratchNative`, `_leviathanNodeFrontNative`, `_leviathanNodeBackNative`, `_leviathanNodeCountNative`, `_foveatedSimulationInputNative`, `_foveatedSimulationFrontNative`, `_foveatedSimulationBackNative`, `_threatGridUploadNative`, `_threatVoxelUploadNative`, `_simulationFrameNative` | `CompletePendingReadbackAndReleaseBuffers()` â†’ `OnDisable()` + `OnDestroy()` | âœ… CLEAN |
| `SystemDispatcher` | `_pendingDispatcherRaycastCommands`, `_scheduledDispatcherRaycastCommands`, `_scheduledDispatcherRaycastHits` | `OnDestroy()` / `OnDisable()` | âœ… CLEAN |

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
- `GPUScatterDirector.EnsureScatterBuffers()` â€” releases then reallocates.
- `HectonIndirectVegetationRenderer.EnsureGpuIndirectResources()` â€” releases then reallocates.
- `SargassumMicroFaunaBoids.EnsureBuffer()` â€” releases then reallocates.
- `AbyssalThermalManager.EnsureVentBuffers()` â€” releases ring then reallocates.

### Deferred Disposal
`SargassumMicroFaunaBoids` uses `DisposeNativeArrayDeferred<T>()` for job-dependent arrays. This is compliant with AGENTS.md Â§13: *"Deferred disposal ONLY. array.Dispose(activeHandle); array = default;"*.

### Factory Pattern
`SystemDispatcher.CreateStructuredBuffer<T>()` and `CreateStructuredLockBuffer<T>()` are **static factory methods**. They do NOT retain buffer ownership. Callers (e.g., `DebrisManager`, `HectonIndirectVegetationRenderer`) are responsible for release. This is correct.

---

## REGRESSION MODEL

| Dimension | Before | After | Delta |
|-----------|--------|-------|-------|
| First-party buffer leaks | Unknown | 0 confirmed | â€” |
| Missing `OnDisable` release | Unknown | 0 confirmed | â€” |
| Missing `OnDestroy` release | Unknown | 0 confirmed | â€” |
| Resize-without-release | Unknown | 0 confirmed | â€” |

---

**MANDATES FOLLOWED:** AGENTS.md Â§13 (Memory Lifetime â€” NO LEAKS), Â§3RD-PARTY INTEGRITY rule, Â§COLD ALLOC comments verified.

**STATUS:** ETA LEAK_MAPPED â€” Compute Buffer slice complete.
