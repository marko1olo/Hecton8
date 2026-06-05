# MapMagic Hydraulic Erosion Job Safety Static Review - 2026-06-06

Status: `STATIC_SOURCE_LOG_REVIEW / SOURCE_BYPASS_PRESENT / UNITY_PROOF_ABSENT`.
Evidence class: `STATIC_SOURCE + STATIC_LOG + MANDATE_REVIEW`.

No Unity readback, import, build, Play Mode, profiler, scene save, prefab save, material save, Addressables mutation, project-setting mutation, runtime source mutation, or raw YAML edit was performed.

## Mandates Followed

- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`

## Facts

- `Docs/Logs/UnityCaptureSurfaceCrestActualTerrainProbeF_20260606_003256.log` records two `HydraulicErosionDeltaApplyJob` safety exceptions at lines `638` and `667`.
- The exception path is `HydraulicErosionDeltaApplyJob.Heightmap` writer safety -> `NativeMemorySentinel.UnregisterNativeArray<T>` -> `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr` -> `HectonHydraulicErosionMapMagicNode.DisposeTracked<T>`.
- The same log records `TempJob` leak evidence at lines `750`, `754`, `756`, and a Unity memory-leak payload at line `758`.
- Current disk source `Assets/_Project/Scripts/Plugins/MapMagic/HectonHydraulicErosionMapMagicNode.cs` calls `HydraulicErosionScheduler.ScheduleFourPhaseSliced(...)` at line `338`.
- Current disk source has no `ScheduleFourPhaseSlicedWithDeltaApply(...)` call in the MapMagic node.
- Current disk source lines `334-337` explicitly state that the queued delta-apply path exposed a safety handle that could outlive the returned dependency in editor generation.
- `Assets/_Project/Scripts/World/HydraulicErosionJob.cs` still keeps `ScheduleFourPhaseSlicedWithDeltaApply(...)` at lines `137-198`; that path schedules `HydraulicErosionDeltaApplyJob` at lines `180-188`.
- `Assets/_Project/Scripts/Core/NativeMemorySentinel.cs` line `890` reads the NativeArray pointer during unregister. If a live writer safety handle still owns the array, unregister itself can throw before disposal runs.

## Static Classification

The ProbeF log is real failure evidence, but it is not proof that the current disk source still schedules `HydraulicErosionDeltaApplyJob` through the MapMagic node.

Current source classification:

- `HectonHydraulicErosionMapMagicNode`: `SOURCE_BYPASS_PRESENT / PENDING_UNITY_REIMPORT_OR_COMPILE_PROOF`.
- `ScheduleFourPhaseSlicedWithDeltaApply`: `STILL_EXISTS / SAFETY_SENSITIVE / DO_NOT_ROUTE_MAPMAGIC_THROUGH_IT_WITHOUT_NEW_PROOF`.
- `DisposeTracked<T>`: `FAILS_HARD_IF_CALLED_WITH_LIVE_WRITER_HANDLE`.
- ProbeF log: `CURRENT_BLOCKER_UNTIL_CURRENT_SOURCE_IS_PROVEN_IN_UNITY`.

## Rejections

- Do not claim the surface route is fixed from this static review.
- Do not claim the TempJob leak is gone until a fresh Unity log proves no `TempJob` leak after current source import.
- Do not suppress the exception with `NativeDisableContainerSafetyRestriction`.
- Do not re-enable the queued delta-apply MapMagic path until a completion/sentinel contract is proven.
- Do not hide terrain failure with haze, rocks, flora, fog, bloom, or screenshots from the rejected h8_1914 probe route.

## Required Next Proof

When process gate is green:

1. Confirm Unity imported and compiled the current `HectonHydraulicErosionMapMagicNode.cs`.
2. Run the no-mutation surface/terrain readback or ProbeF equivalent with a fresh log path.
3. Require zero occurrences of `HydraulicErosionDeltaApplyJob`, `previously scheduled job`, `TempJob allocates`, and `remaining Allocations on the JobTempAlloc`.
4. Confirm terrain generation can complete without dirtying unrelated scene/material assets.
5. If the same exception recurs on current source, inspect for hidden callers of `ScheduleFourPhaseSlicedWithDeltaApply(...)` and add a source-level fix that either completes the exact producer handle before Sentinel unregister or unregisters by owner/label without reading a guarded pointer.

## Regression Model

- CPU: current source still uses a cold synchronous publish barrier in MapMagic generation; acceptable only as editor/offline terrain generation until profiler proof says otherwise.
- GC: static review only; no runtime GC measurement.
- Memory: ProbeF log proves TempJob leak risk; memory state is `PENDING VERIFICATION`.
- Cadence: no gameplay cadence changed.
- Correctness: queued delta-apply safety is a blocker for terrain route proof until current source is re-run in Unity.

## Low / Middle / High / Ultra

- Low: terrain generation must not leak TempJob allocations; visual floor still requires readable terrain material, coastline, and waterline.
- Middle: same safety contract; better terrain breakup can only be promoted after no-leak generation proof.
- High: richer terrain erosion/detail is allowed only if job completion and Sentinel lifecycle stay clean.
- Ultra: capture-grade terrain detail does not override NativeArray ownership or proof requirements.

Final status: `BLOCKER_CLASSIFIED / CURRENT_SOURCE_REPAIR_PENDING_UNITY_PROOF`.
