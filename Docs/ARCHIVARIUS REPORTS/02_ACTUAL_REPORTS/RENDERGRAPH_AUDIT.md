# RENDERGRAPH AUDIT — HECTON-8 First-Party URP Features
Date: 2026-05-04
Status: REFERENCE


**Date:** 2026-04-29  
**Scope:** `Assets/_Project/Scripts/Visor/*Feature.cs` + owned `ScriptableRenderPass`  
**Authority:** CTO / Lead Architect  
**Status:** ETA LEAK_MAPPED

---

## EXECUTIVE SUMMARY

All **7 first-party** `ScriptableRendererFeature` implementations were audited for `RTHandle` / `RenderTexture` / `GraphicsBuffer` lifecycle hygiene.  
**Verdict:** Zero first-party RenderGraph resource leaks detected. Every pass that owns persistent GPU resources implements `IDisposable` and calls `Release()` in its `Dispose()` method. Every feature implements `Dispose(bool)` and forwards to its pass.

> **WARNING:** This audit covers first-party code ONLY. Third-party Crest URP passes (`UnderwaterEffectPass`, `UnderwaterMaskPass`, `WaterRenderer`, etc.) are EXCLUDED per the **3RD-PARTY INTEGRITY** mandate. Crest maintains its own `Release()` / `OnDestroy()` patterns — do NOT write wrappers.

---

## AUDIT TABLE

| # | Feature Class | Pass Class(es) | Persistent Resources | `Dispose(bool)` in Feature | `IDisposable` on Pass | `Release()` in Pass Dispose | Verdict |
|---|---------------|----------------|----------------------|----------------------------|----------------------|-----------------------------|---------|
| 1 | `HectonAbyssalSsdoFeature` | `AbyssalSsdoPass` | None (transient `renderGraph.CreateTexture` only) | ✅ Yes | ❌ No (not needed) | N/A | **CLEAN** |
| 2 | `HectonBiolumSSGIFeature` | `BiolumSsgiPass` | `_gatherTexture` (RTHandle), `_giTexture` (RTHandle) | ✅ Yes | ✅ Yes | ✅ Both `Release()` | **CLEAN** |
| 3 | `HectonDryVolumeFeature` | `DryRestorePass`, `UnderwaterResolvePass` | None (transient `renderGraph.CreateTexture` only) | ✅ Yes | ❌ No (not needed) | N/A | **CLEAN** |
| 4 | `HectonScooterVolumetricShaftsFeature` | `ShaftsPass` | `_histogramBuffer` (GraphicsBuffer), `_exposureStateBuffer` (GraphicsBuffer) | ✅ Yes | ✅ Yes | ✅ Both `Release()` | **CLEAN** |
| 5 | `HectonSonarPointCloudFeature` | `SonarPointCloudPass` | `_historyRead` / `_historyWrite` / `_worldHistoryRead` / `_worldHistoryWrite` (4× RTHandle) | ✅ Yes | ✅ Yes | ✅ All 4 `Release()` | **CLEAN** |
| 6 | `HectonVoxelSsaoFeature` | `VoxelSsaoPass` | `_aoTexture` (RTHandle), `_blueNoiseTextureHandle` (RTHandle) | ✅ Yes | ✅ Yes | ✅ Both `Release()` | **CLEAN** |
| 7 | `HectonVisorFluidDistortionFeature` | `VisorFluidPass` | None (transient `renderGraph.CreateTexture` only) | ✅ Yes | ❌ No (not needed) | N/A | **CLEAN** |

---

## DETAILED NOTES

### 1. HectonAbyssalSsdoFeature
- **Pass:** `AbyssalSsdoPass` — does NOT implement `IDisposable`.
- **Why OK:** The pass only uses `renderGraph.CreateTexture(...)` for transient intermediate buffers. The RenderGraph allocator owns and reclaims these automatically. No persistent `RTHandle.Alloc` or `GraphicsBuffer` fields exist.
- **Feature Dispose:** Destroys `_ssdoMaterial` and `_blurMaterial` via `CoreUtils.Destroy`.

### 2. HectonBiolumSSGIFeature
- **Pass:** `BiolumSsgiPass` — implements `IDisposable`.
- **Resources:**
  - `_gatherTexture = RTHandles.Alloc(...)` — released in `Dispose()`.
  - `_giTexture = RTHandles.Alloc(...)` — released in `Dispose()`.
- **Feature Dispose:** Calls `_pass?.Dispose()` then destroys material.

### 3. HectonDryVolumeFeature
- **Passes:** `DryRestorePass`, `UnderwaterResolvePass` — neither implements `IDisposable`.
- **Why OK:** Both passes use transient `renderGraph.CreateTexture(...)` for color/depth copies. No persistent handles.
- **Feature Dispose:** Destroys `_dryMaterial` and `_restoreMaterial`.

### 4. HectonScooterVolumetricShaftsFeature
- **Pass:** `ShaftsPass` — implements `IDisposable`.
- **Resources:**
  - `_histogramBuffer` (GraphicsBuffer) — released in `Dispose()`.
  - `_exposureStateBuffer` (GraphicsBuffer) — released in `Dispose()`.
- **Auto-Exposure Note:** `EnsureAutoExposureResources()` is intentionally **NOT called** (noir stack runs fixed-exposure). `ReleaseAutoExposureResources()` is called in `Setup()` as a safety guard. This is by design.
- **Feature Dispose:** Calls `_pass?.Dispose()` then destroys material.

### 5. HectonSonarPointCloudFeature
- **Pass:** `SonarPointCloudPass` — implements `IDisposable`.
- **Resources:**
  - `_historyRead` (RTHandle) — released.
  - `_historyWrite` (RTHandle) — released.
  - `_worldHistoryRead` (RTHandle) — released.
  - `_worldHistoryWrite` (RTHandle) — released.
- **Feature Dispose:** Calls `_pass?.Dispose()` then destroys material.

### 6. HectonVoxelSsaoFeature
- **Pass:** `VoxelSsaoPass` — implements `IDisposable`.
- **Resources:**
  - `_aoTexture` (RTHandle) — released.
  - `_blueNoiseTextureHandle` (RTHandle) — released.
- **Feature Dispose:** Calls `_pass?.Dispose()`.

### 7. HectonVisorFluidDistortionFeature
- **Pass:** `VisorFluidPass` — does NOT implement `IDisposable`.
- **Why OK:** Uses transient `renderGraph.CreateTexture(...)` only. No persistent handles.
- **Feature Dispose:** Destroys `_fluidMaterial`.

---

## TOP 3 SUSPECTS (LEAK LIKELIHOOD)

| Rank | Suspect | Reason | Action |
|------|---------|--------|--------|
| 1 | **Crest URP passes** (third-party) | `WaterRenderer`, `UnderwaterMaskPass`, `UnderwaterEffectPass` own `RenderTexture` rings and `CommandBuffer` pools. Complex lifetime graph across `OnDisable` / `OnDestroy` / edit-mode toggles. | Audit Crest runtime logs ONLY. Do NOT patch first-party wrappers. |
| 2 | **None (first-party)** | All first-party features pass lifecycle audit. | N/A |
| 3 | **None (first-party)** | All first-party features pass lifecycle audit. | N/A |

> The `Resource ID out of range in SetResource` spam reported by Omega is **NOT** originating from first-party `ScriptableRendererFeature` / `ScriptableRenderPass` code. Redirect investigation to Crest render-target cache or BRG/GraphicsBuffer stale handle usage.

---

## REGRESSION MODEL

| Dimension | Before | After | Delta |
|-----------|--------|-------|-------|
| First-party Render Feature leaks | Unknown | 0 confirmed | — |
| First-party Pass leaks | Unknown | 0 confirmed | — |
| Missing `Dispose(bool)` features | Unknown | 0 | — |
| Missing `IDisposable` passes | Unknown | 0 (all persistent-owning passes have it) | — |

---

**MANDATES FOLLOWED:** AGENTS.md §13 (Memory Lifetime), §22 (Jobs/Burst dispose order), §3RD-PARTY INTEGRITY rule.

**STATUS:** ETA LEAK_MAPPED — RenderGraph slice complete.
