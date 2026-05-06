# HOT_PATH_VIOLATIONS.md
Date: 2026-05-07
Status: PENDING VERIFICATION


**Date:** 2026-04-29  
**Status:** PENDING VERIFICATION  
**Scope:** static source readback of current hot-path and near-hot-path risks under `Assets/_Project/Scripts/`

**Mandates Followed:** `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`

---

## Method

- Re-checked the concrete files named in the earlier report.
- Split findings into three buckets:
  - false prior claims
  - confirmed architecture or allocation risks
  - open items that still need runtime proof

---

## False Prior Claims

| Prior claim | Current result |
|---|---|
| `PhysicsApplySystem.cs` had no profiler markers | false; file now contains multiple `ProfilerMarker` fields and usage sites |
| `HectonFluidEngine.cs` had no profiler markers | false; file now contains multiple `ProfilerMarker` fields and usage sites |
| `HectonPlayerMovement.cs` had no profiler markers | false; file now contains tick and fixed-tick markers |
| `CullingManager.cs` performed `LayerMask.NameToLayer` lookups inside `ApplyLayerCullDistances()` | false; current method uses cached layer indices |
| `HectonSurfaceWeatherDirector.cs` performed unguarded runtime `AssetDatabase` loading | false in current source; the observed `AssetDatabase` usage is under `#if UNITY_EDITOR` |

---

## Confirmed Current Risks

| ID | File | Current issue | Severity |
|---|---|---|---|
| HP-01 | `World/HectonCrestOceanDepthCacheBootstrap.cs` | retains `using Crest;` direct dependency and caches both `"Terrain"` and `"Terrain "` layer names | HIGH |
| HP-02 | `World/AcousticOcclusionUtility.cs` | static constructor still resolves eight layer names through `LayerMask.NameToLayer(...)` | MEDIUM |
| HP-03 | `ModdingAPI/HectonEventBus.cs` | bus is list-backed managed dispatch, not the mandated zero-alloc `NativeQueue<T>` event surface | HIGH |
| HP-04 | `CraftingEvents.cs` and modding event payloads | `ItemData` references remain in event payload surfaces, keeping managed object traffic in event chains | HIGH |
| HP-05 | `HectonFluidEngine.cs` | file header still contains stale Russian/garbled performance claims and legacy singleton wording | MEDIUM |

---

## Important Clarifications

- `AcousticOcclusionUtility.cs` is not a per-frame `NameToLayer` offender in the old sense; the lookups currently sit in a static constructor, so this is startup hygiene debt, not repeated hot-loop churn.
- `HectonCrestOceanDepthCacheBootstrap.cs` likewise performs the layer lookups through a guarded cache-init method, not in a frame loop. The real issue is architecture debt and suspicious `"Terrain "` fallback handling.
- `BuoyancyObject.cs` resolves `"Water"` once in initialization. That is not evidence of a frame-allocation hotspot by itself.

---

## Open Items

- No live GCMonitor, profiler capture, or Burst timeline was collected in this pass.
- Static readback cannot prove whether managed event payloads materially breach frame budget on target hardware.
- Broader hot-path coverage remains incomplete; this rewrite only repairs the claims that were directly rechecked.

---

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only rewrite. |
| GC | None. Documentation-only rewrite. |
| Memory | None. Documentation-only rewrite. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improved by separating false accusations from still-live architectural debt. |

---

## Verdict

The earlier report overstated several hot-path violations that are no longer present in current source.  
Real debt remains around event architecture, direct Crest coupling, and managed payload surfaces.  
Runtime performance impact remains `PENDING VERIFICATION`.
