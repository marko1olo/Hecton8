# AUP DRIFT WARNINGS — HECTON-8 Static Audit
Date: 2026-05-04
Status: DEPRECATED


**Generated:** 2026-04-27 | **Auditor:** Static Compliance Officer  
**Mandate:** MATH_Coordinate_Precision_AUP_FloatingOrigin.txt

---

## I. Classes Implementing `IOriginShiftListener`

| # | File | Class | OnOriginShift Present? |
|---|---|---|---|
| 1 | `HectonSurfaceWeatherDirector.cs` | `HectonSurfaceWeatherDirector` | ✅ Yes |
| 2 | `BiomeSamplerCache.cs` | `BiomeSamplerCache` | ✅ Yes |
| 3 | `GlobalPhysicsStateManager.cs` | `GlobalPhysicsStateManager` | ✅ Yes |
| 4 | `ContextualPhysicalIkRig.cs` | `ContextualPhysicalIkRig` | ✅ Yes |
| 5 | `ContextualPhysicalIkRuntime.cs` | `ContextualPhysicalIkRuntime` | ✅ Yes |
| 6 | `DebrisManager.cs` | `DebrisManager` | ✅ Yes |
| 7 | `HectonPlayerMovement.cs` | `HectonPlayerMovement` | ✅ Yes |
| 8 | `MountablePlayerTransport.cs` | `MountablePlayerTransport` | ✅ Yes |
| 9 | `SubmarineFluidDynamics.cs` | `SubmarineFluidDynamics` | ✅ Yes |
| 10 | `TetherManager.cs` | `TetherManager` | ✅ Yes |
| 11 | `WorldGenerativeGeologyIntegrationDirector.cs` | `WorldGenerativeGeologyIntegrationDirector` | ✅ Yes |
| 12 | `AbyssalFluidDecalManager.cs` | `AbyssalFluidDecalManager` | ✅ Yes |
| 13 | `AbyssalThermalManager.cs` | `AbyssalThermalManager` | ✅ Yes |
| 14 | `HectonMarineSnowRenderer.cs` | `HectonMarineSnowRenderer` | ✅ Yes |
| 15 | `FloraInteractionManager.cs` | `FloraInteractionManager` | ✅ Yes |
| 16 | `HectonDistantLandmarkRenderer.cs` | `HectonDistantLandmarkRenderer` | ✅ Yes |
| 17 | `HectonHLODRenderer.cs` | `HectonHLODRenderer` | ✅ Yes |
| 18 | `HectonMapMagicVegetationBridge.cs` | `HectonMapMagicVegetationBridge` | ✅ Yes |
| 19 | `SargassumCrestDampingController.cs` | `SargassumCrestDampingController` | ✅ Yes |
| 20 | `SargassumGlobalDragManager.cs` | `SargassumGlobalDragManager` | ✅ Yes |
| 21 | `SargassumMicroFaunaBoids.cs` | `SargassumMicroFaunaBoids` | ✅ Yes |
| 22 | `SuitHUDV4CanvasOverlay.cs` | `SuitHUDV4CanvasOverlay` | ✅ Yes |
| 23 | `HectonUIScaler.cs` | `HectonUIScaler` | ✅ Yes |

**All 23 classes implement `OnOriginShift`.** The question is: do they correctly rebase ALL their `Vector3` world-space fields?

---

## II. Untracked Vector3 Fields (Static Cross-Check)

**Method:** For each class, grep for `private.*Vector3` fields, then check if they appear in the `OnOriginShift` method body with `+= shiftOffset` or `-= shiftOffset`.

| File | Untracked Vector3 Field | Severity | Notes |
|---|---|---|---|
| `HectonPlayerMovement.cs` | `_exosuitGrappleAnchorCurrentWS` | 🔴 **CRITICAL** | World-space anchor for grapple. Must be rebased. (Historical reports confirm this was fixed, but static grep cannot verify runtime state.) |
| `HectonPlayerMovement.cs` | `_lastTransportPlatformPosition` | 🟡 **HIGH** | Used for platform-relative tracking. Must be rebased. |
| `AbyssalThermalManager.cs` | `_ventStates[].PositionWS` (struct field) | 🔴 **CRITICAL** | Thermal vent positions stored as raw Vector3 in struct array. `OnOriginShift` iterates and rebases — **verified in code**. |
| `AbyssalThermalManager.cs` | `_empNestStates[].PositionWS` (struct field) | 🟡 **HIGH** | EMP nest positions. Same pattern — **verified rebased in OnOriginShift**. |
| `SargassumGlobalDragManager.cs` | `_nestedAttachmentStates[].SampleSpaceAnchorWS` | 🟡 **HIGH** | Nested object world-space anchors. `OnOriginShift` iterates — **verified**. |
| `HectonMapMagicVegetationBridge.cs` | `_accumulatedFloatingOriginOffset` | ✅ **Tracked** | Accumulated offset, correctly updated. |
| `HectonMapMagicVegetationBridge.cs` | `_abyssalAnchorPositions[]` | ⚠️ **MEDIUM** | `Vector3[]` — rebased in `OnOriginShift` via loop. Verified. |
| `SargassumMicroFaunaBoids.cs` | `_computeShaderOriginOffset` | ✅ **Tracked** | Uploaded to GPU each frame. |
| `FloraInteractionManager.cs` | Various flora anchor positions | ⚠️ **MEDIUM** | Rebased in `OnOriginShift` — verified. |
| `DebrisManager.cs` | `_lastValidPositions[]` | ✅ **Tracked** | Rebased in `OnOriginShift`. |
| `TetherManager.cs` | `_anchorPositions[]` | ✅ **Tracked** | Rebased in `OnOriginShift`. |

---

## III. Top 3 Files with Most Untracked Vector3 Risk

| Rank | File | Risk Count | Highest Severity |
|---|---|---|---|
| 1 | `HectonPlayerMovement.cs` | 2 unverified fields | 🔴 CRITICAL (`_exosuitGrappleAnchorCurrentWS`) |
| 2 | `AbyssalThermalManager.cs` | 2 struct-array fields (verified rebased) | 🟡 HIGH (struct array traversal correctness) |
| 3 | `SargassumGlobalDragManager.cs` | 1 struct-array field (verified rebased) | 🟡 HIGH (nested attachment anchors) |

---

## IV. Historical Issues (Resolved per Code Review)

Per `NAYDENNYE PROBLEMY.txt`, the following were previously flagged as missing `IOriginShiftListener`:
- `AbyssalThermalManager.cs` — **Now implements IOriginShiftListener** ✅
- `FloraInteractionManager.cs` — **Now implements IOriginShiftListener** ✅
- `SargassumGlobalDragManager.cs` — **Now implements IOriginShiftListener** ✅

These were fixed in a previous sprint. The current audit confirms the interface is present and `OnOriginShift` methods exist.

---

**STATUS:** PENDING VERIFICATION — full field-level rebase audit requires Unity Editor runtime test (shift → check field values).
