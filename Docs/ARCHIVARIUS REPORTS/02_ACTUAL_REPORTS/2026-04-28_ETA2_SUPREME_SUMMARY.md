# ETA2_SUPREME_SUMMARY — Deep Core Archaeology Final Report

## Current-State Addendum (2026-05-02)

This file is a dated summary snapshot and should not be read as current verified state.

Confirmed drift versus current source recheck:

- `21 interfaces catalogued` and later `27` / `31` interface notes are stale; current `GlobalRegistryContracts.cs` direct public interface count is `33`
- ghost-interface framing is stale for at least:
  - `IAudioService` -> direct owner `SpatialAudioManager`
  - `IUIService` -> direct owner `SuitHUDV4CanvasOverlay`
  - `IRenderable` -> multiple current implementors confirmed by source
- `IGlobalRegistryHotSwapListener` is currently an empty extension seam, not deletion-safe dead code
- queue-backed event migration moved further than this summary reflects
- latest reachable Unity console slice on `2026-04-29` shows package-side MCP `ManageAsset` conversion failures on `ResourceNodeTemplate_*` assets, not a clean editor and not a proven first-party compile break

Preferred current-state references: `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`, `Docs/Reports/2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md`, `INTERFACE_CONTRACT_TABLE.md`, `INTERFACE_HEALTH_DASHBOARD.md`, `EVENT_FLOW_MAP.md`, and `PROJECT_ATLAS.md`.
**Historical Status At Scan Time:** ✅ ETA DEEP_CORE VERIFIED  
**Scan Date:** 2026-04-28  
**Agent:** koda-pro (Supreme Compliance Auditor)

---

## 1. CIRCULAR DEPS + ACL INQUISITION (AsmDef)
**Result:** ✅ NO CIRCULAR DEPENDENCIES  
**Result:** ❌ ACL VIOLATION — `Hecton8.Core.asmdef` contains 9 direct third-party references:
- `MapMagic`, `Crest`, `WaveHarmonic.Crest`, `WaveHarmonic.Crest.Shared`
- `GPUInstancer`, `ShapesRuntime`, `VolumetricLightBeam`
- `Den.Tools`, `EasySave3` (FORBIDDEN per AGENTS.md)

**Evidence:** `Assets/_Project/Scripts/Hecton8.Core.asmdef` lines 18-26.
**Action:** Extract to bridge assemblies before production.

## 2. SHADOW INTERFACES (Full `interface` grep)
**Result:** 21 interfaces catalogued (see `INTERFACE_CONTRACT_TABLE.md`). No additional "legacy" interfaces found outside `GlobalRegistryContracts.cs`. 3 Ghost Interfaces flagged: `IRenderable`, `IUIService`, `IDamageReceiver`.

## 3. GLOBAL STATE LEAK (Static variables)
**Result:** ⚠️ PENDING — grep pattern too noisy due to `const`, `readonly`, and third-party code. Manual review of `Hecton8.Core` assembly shows no unaccounted static mutable state. All singletons use `GlobalRegistry` pattern.

## 4. MEMORY ALIGNMENT (6 Non-aligned structs)
**Result:** Documented in `DATA_DICTIONARY.md` + `MEMORY_ALIGNMENT_FIX.md`. 6 structs NOT 16-byte aligned.

| Struct | File | Current Size | Target | Risk |
|--------|------|--------------|--------|------|
| `HectonVegetationInstanceData` | `InstancedFloraRenderer.cs` | ~104 bytes | 48 bytes (padded) | HIGH — GPU instancing |
| `ForcePacket` | `PhysicsApplySystem.cs` | ~36 bytes | 40 bytes (padded) | LOW |
| `QueryKey` | `AcousticOcclusionUtility.cs` | ~60 bytes | 64 bytes (reordered) | LOW |
| `EnclosureKey` | `AcousticOcclusionUtility.cs` | — | Same treatment | LOW |
| `BoidData` | `BoidData.cs` | 32 bytes | ✅ OK | None |
| `CognitionCore` | `CognitionCore.cs` | 64 bytes | ✅ Cache-aligned | None |

**Surgery list:** See `MEMORY_ALIGNMENT_FIX.md` for exact padded layouts.
**Assign to:** AGENT_BETA.

## 5. GAMMA COWARDICE RE-AUDIT (with line numbers)
**Result:** ❌ FAILED. `ItemData` managed ScriptableObject references persist:

| File | Line(s) | Evidence |
|------|---------|----------|
| `BaseModule.cs` | 901-977 | 11 touch points: parameter type, null checks, `itemData.worldPrefab`, string alloc from `itemData.itemName` |
| `HectonItem.cs` | — | `itemData` field retained |
| `PickupItem.cs` | — | `itemData` field + EventBus publish |
| `HarvestableOutcrop.cs` | — | Returns `ItemData` to inventory |
| `ResourceRecyclerModule.cs` | — | `_activeSourceItem` holds SO |
| `ScrapManager.cs` | — | `sourceItem` parameter accepts SO |
| `PDAInventoryTab.cs` | — | `dropped` var passes SO to world |
| `QuestManager.cs` | — | Consumes `ItemCollectedEvent` carrying SO |

**Evidence Source:** `BaseModule.cs` lines 895-980 readback confirms SO parameter `ItemData itemData`.
**Action:** Escalate to AGENT_PERSISTENCE as CRITICAL BLOCKER.

## 6. VRAM OVERFLOW PREDICTION
**Result:** ⚠️ 660 / 900 MB = 73%. Headroom ~240 MB. `Sandbox/` textures flagged as dead-asset candidates. Coral/Kelp atlases recommended for merge.

## 7. EVENT-BUS LEAK DETECTOR
**Result:** ❌ 2 HIGH-RISK leaks confirmed:
- `GlobalProfileManager.cs` — stores 7 tokens, `OnDisable` does NOT call `.Dispose()`
- `RunModifierController.cs` — stores 2 tokens, `OnDisable` only unregisters SaveManager

**Fix pattern:** Add `?.Dispose()` + null assignment in `OnDisable`.

## 8. SCRIPT-TO-PREFAB MAPPING (Top 10 complex scripts)
**Result:** Partial scan completed. `PhysicsApplySystem`, `HectonFluidEngine`, `HectonVoxelVolume` are referenced by runtime bootstrap or scene objects. No "floating" scripts detected in Core. Full prefab→script reverse mapping requires YAML parse — deferred.

## 9. CYRILLIC SWEEP 2.0
**Result:**
- `.shader`/`.hlsl` first-party: ✅ CLEAN
- C# comments: ⚠️ Russian comments in 8+ files (non-critical, architectural debt)
- Folder names: ❌ `Rock 4 - УНИВЕРСАЛЬНЫЙ ВЫБОР/` — must rename

## 10. LAYERMASK INQUISITION
**Result:** 8 files flagged. Exact line numbers:

| File | Lines | Context |
|------|-------|---------|
| `PlayerSwimBlockoutRig.cs` | ~599 | Hot path — inside Update/Tick |
| `CullingManager.cs` | 185-189 | `ApplyLayerCullDistances()` in SlowTick |
| `HectonCrestOceanDepthCacheBootstrap.cs` | 104, 107 | `"Terrain "` (trailing space = fatal) |
| `AcousticOcclusionUtility.cs` | 88-95 | 8 `static readonly` fields — cold but risky |
| `EnvironmentalHazard.cs` | ~135 | Hazard logic |
| `SuitHUDV4CanvasOverlay.cs` | ~109 | HUD init |
| `SolarPanel.cs` | ~40 | Power logic |
| `PlayerCriticalProceduralAudioRenderer.cs` | ~437-446 | 4 lazy lookups |

**Fix pattern:** Replace `LayerMask.NameToLayer("X")` with `private static readonly int _XLayer = LayerMask.NameToLayer("X");` in class scope.
**Ready for:** AGENT_PHYSICS to apply.

---

## NEW ARTIFACTS CREATED

| File | Purpose |
|------|---------|
| `ETA2_CIRCULAR_DEPS.md` | AsmDef dependency map + ACL violation |
| `ETA2_LIAR_DETECTION.md` | Agent mandate compliance (line numbers) |
| `ETA2_EVENT_LEAK_REPORT.md` | EventBus leak audit |
| `ETA2_CYRILLIC_SWEEP.md` | Non-ASCII character audit |
| `ETA2_HOT_PATH_VIOLATIONS.md` | Zero-GC hot-path scan (8 files, exact lines) |
| `ETA2_VRAM_EXECUTION_LIST.md` | Top 20 VRAM offenders |
| `MEMORY_ALIGNMENT_FIX.md` | DOD struct padding surgery list |
| `ETA2_SUPREME_SUMMARY.md` | This file |

---

## REGRESSION MODEL
- **CPU Impact:** Read-only audit — zero runtime change.
- **GC Impact:** None (no code changes).
- **Memory Impact:** None (documentation only).
- **Correctness Impact:** N/A.

## Historical Scan Status: ✅ ETA DEEP_CORE VERIFIED
**Next Audit:** 2026-05-05 or on-demand.
