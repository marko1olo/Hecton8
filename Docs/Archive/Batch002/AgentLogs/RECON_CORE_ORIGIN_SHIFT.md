# RECON_CORE_ORIGIN_SHIFT

Status: PENDING VERIFICATION

Scan scope: `Assets/_Project/Scripts/**/*.cs`
Commands:
- `rg -n "Vector3\s+_[A-Za-z0-9_]*(Pos|Position)[A-Za-z0-9_]*|_[A-Za-z0-9_]*(Pos|Position)[A-Za-z0-9_]*\s*=\s*[^;]*\.position" Assets/_Project/Scripts -g "*.cs"`
- `rg -n "(last|cached|previous|prev|old|runtime).*position|position.*cache|Transform\.position|\.position" Assets/_Project/Scripts -g "*.cs"`

## High-Risk Runtime Position Caches

- `Assets/_Project/Scripts/CurrentVolume.cs:98,394` caches `_cachedPosition = cachedTransform.position`. Needs AUP rebase or recompute on origin shift.
- `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:225,1181` caches `_lastListenerPosition = _listenerTransform.position`. Needs listener cache reset on `AupShiftSignal`.
- `Assets/_Project/Scripts/Construction/LogisticsPipeNode.cs:110-113,485-486` caches source/destination world positions. Needs rebase if pipe visuals persist through shift.
- `Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs:234,1931` caches `_previousPlatformPosition` from platform transform. Needs shift-frame reset or rider delta can span the epoch jump.
- `Assets/_Project/Scripts/Gameplay/DeployableFlare.cs:136,478` caches `_lastSpatialPosition = _transform.position`. Needs AUP cache or origin-shift listener.
- `Assets/_Project/Scripts/Items/PickupItem.cs:69,74,302,324,624` caches `_lastSpatialPosition` and `_worldStateAnchorPosition` from `transform.position`. Needs rebase or AUP conversion.
- `Assets/_Project/Scripts/Interaction/VRValveWheelHandle.cs:36,253` caches `_cachedPivotWorldPosition = _cachedTransform.position`. Needs rebase or local-space pivot cache.
- `Assets/_Project/Scripts/HectonBoidController.cs:315,1036` caches `_targetPosition = _playerTransform.position`. Needs AUP-safe refresh on shift.
- `Assets/_Project/Scripts/WorldGenerativeGeologyIntegrationDirector.cs:57,310` caches `_lastPlanRefreshPosition = playerTransform.position`. Needs AUP distance or reset after shift.
- `Assets/_Project/Scripts/World/FloraInteractionManager.cs:822,824,825,1349` caches player/scooter/submarine runtime positions. Needs origin-shift rebase for wake smoothing.
- `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:315,318` caches camera positions for motion/culling. Needs reset on shift to avoid motion-vector smear.
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs:1588-1596` caches view/player motion positions. Needs reset on shift.
- `Assets/_Project/Scripts/World/WreckMaterialRegistry.cs:838` caches `_lastFrustumCameraPosition`. Needs reset on shift if frustum delta gates uploads.

## Lower-Risk Or Local-Space Caches

- Local position fields such as `_restLocalPosition`, `_snapStartLocalPosition`, `_baseLocalPosition`, and authored open/closed positions are not AUP-critical because local space survives root shifts.
- Editor-only fields such as `_editorLastEvaluationPosition`, `_editorLastObserverPosition`, and debug capture positions are lower runtime risk but still noisy in scans.
- `HectonFloatingOrigin`, `GlobalPhysicsStateManager`, `VehicleMotor`, and `HectonPlayerCameraRig` already contain explicit origin-shift handling in this pass; runtime proof is still missing.

## Required Follow-Up

Add `IOriginShiftListener` or AUP-authoritative storage to the high-risk owners in their domain batches. Do not patch all of them from CORE_ORIGIN_SHIFT without ownership; that would create cross-domain coupling.

## Follow-Up Applied - 2026-05-12 CORE_ORIGIN_SHIFT R&D

- `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` patched by CORE_ORIGIN_SHIFT because it directly feeds vegetation visual culling and motion vectors. It now listens for origin shifts, rebases cached cull/motion camera positions and explicit world bounds, invalidates the far-cull snapshot, and forces the next cull cadence to start clean.
- `Assets/_Project/Scripts/Items/PickupItem.cs` left unpatched in this pass after inspection: `WorldSpatialHashGrid.UpdateGridPosition(int, old, new)` refreshes from the registered transform and no longer uses the old/new parameters for native removal. It remains a domain cleanup candidate if item persistence or fauna bait logic later proves an old-epoch dependency.
