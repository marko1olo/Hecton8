# MapMagic Hydraulic Erosion Job Safety Static Review - 2026-06-06

Status: `STATIC_SOURCE_LOG_REVIEW / SOURCE_REPAIR_APPLIED / TERMINALOS_ASMDEF_FIX_PRESENT / H8_1914_AUTORUN_DISABLED / LATEST_COMPILE_ONLY_PASS_AFTER_HOOK_FIX / TERRAIN_PROOF_ABSENT`.
Evidence class: `STATIC_SOURCE + STATIC_LOG + MANDATE_REVIEW`.

No Unity readback, import, build, Play Mode, profiler, scene save, prefab save, material save, Addressables mutation, project-setting mutation, or raw YAML edit was performed. Runtime source was edited after a green process sample, then build/proof was blocked by a later red CPU/Unity gate.

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

## ProbeJ / ProbeK Progression And Source Repair

- `Docs/Logs/UnityCaptureSurfaceCrestActualTerrainProbeJ_20260606_012945.log` records zero `HydraulicErosionDeltaApplyJob` hits, zero `TempJob` warnings, and two thread failures from `HydraulicErosionJob.HeightDeltaBudget` being unassigned during `ScheduleFourPhaseSliced(...)`.
- ProbeJ failure moved the blocker from old queued delta-apply writer-fence cleanup to direct erosion job validation of an unused optional NativeArray field.
- `Assets/_Project/Scripts/World/HydraulicErosionJob.cs` now marks `HeightDeltaBudget` with `NativeDisableContainerSafetyRestriction` and `NativeDisableParallelForRestriction`.
- The source patch includes three local `SAFETY_JUSTIFICATION_PARAGRAPH_*` comments. The invariant is that `HeightDeltaBudget` is read only inside `TryEnqueueHeightDeltaBounded`, that method checks `IsCreated` and `Length`, and direct MapMagic erosion keeps `QueueHeightDeltas == 0`.
- `Docs/Logs/UnityCaptureSurfaceCrestActualTerrainProbeK_20260606_014642.log` records zero `HydraulicErosionDeltaApplyJob`, zero `HeightDeltaBudget`, zero `TempJob`, and two thread failures from `UnityEditor.EditorApplication.isUpdating` being called off the main thread in `HectonAnomalyEngine.ScheduleClosedBasinDetection(...)`.
- `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs` now captures the editor main-thread id via `UnityEditor.InitializeOnLoadMethod` and returns `false` before touching `EditorApplication` when called from a MapMagic worker thread.
- CPU/process gate turned red after the patch (`CPU=100`, active `Unity` and `UnityPackageManager`), so no compile, Unity import, Play Mode, capture, profiler, or screenshot proof was run.

## ProbeL Diagnostic Result And Compile Wall Repair

- `Docs/Logs/UnityIntegratePlanetaryCanvasGraph_DisableAnomaly_20260606_015935.log` records `CS0121` at `Assets/_Project/Scripts/Editor/PlanetaryCanvasMapMagicGraphIntegrator.cs:90`: `Graph.Link(...)` was ambiguous between `(IOutlet<object>, IInlet<object>)` and `(IInlet<object>, IOutlet<object>)`.
- `Assets/_Project/Scripts/Editor/PlanetaryCanvasMapMagicGraphIntegrator.cs` now restores production-intent wiring: erosion output drives height output, splat height, anomaly height, and splat sediment; anomaly brine mask drives mud layers; erosion/anomaly nodes are enabled by recovery defaults.
- When present earlier, `Docs/Logs/UnityCaptureSurfaceCrestActualTerrainProbeL_20260606_022301.log` recorded zero `HydraulicErosionDeltaApplyJob`, zero `HeightDeltaBudget`, zero `get_isUpdating`, zero `TempJob`, zero `Thread failed`, zero `InvalidOperationException`, and zero `error CS` hits.
- That ProbeL log also recorded one Unity `MemoryLeaks` payload and wrote the rejected h8_1914 diagnostic screenshot/metadata. The log is no longer present in current `Docs/Logs`, so it is not live proof.
- ProbeL graph metadata shows `HectonHydraulicErosionMapMagicNode enabled=False` and `HectonAnomalyMapMagicNode enabled=False`; this proves a bypassed diagnostic route, not production terrain generation acceptance.
- ProbeL screenshot remains visually rejected: hard rectangular split across lower water/terrain, black undercut shoreline chunks, flat brown terrain plate, and non-production composition. It cannot be used as h8_1475, visual acceptance, or surface-route approval.
- `Tools/ValidateTerrainProbeEvidence.py` now classifies the current ProbeL path and metadata as `TERRAIN_PROBE_EVIDENCE_REJECTED blockers=10`: missing log, non-production `captureTruth`, `editor_only_unsaved`, `h8_1914`, erosion disabled, anomaly disabled, anomaly height unlinked, splat sediment unlinked, height output not eroded, and splat height not eroded.
- Latest unsuffixed probe refresh `UnityCaptureSurfaceCrestActualTerrainProbe_20260606_054748.log` recorded `Tundra build success`, wrote fresh h8_1914 PNG/TXT, then rejected: `python Tools\ValidateTerrainProbeEvidence.py --log Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbe_20260606_054748.log --metadata Docs\Screenshots\MCP\h8_1914_surface_crest_recovery_probe.txt --require-production` returns `TERRAIN_PROBE_EVIDENCE_REJECTED blockers=9` because the log contains a Unity `MemoryLeaks` payload and the h8_1914 metadata remains diagnostic: erosion/anomaly are disabled, anomaly/sediment links are absent, and height/splat are not sourced from hydraulic erosion.
- Later unsuffixed probe refresh `UnityCaptureSurfaceCrestActualTerrainProbe_20260606_060418.log` failed before capture on TerminalOS editor compile errors: `DecryptionPuzzleDTO` / `DecryptionKnobInputDTO` were referenced without a direct `Hecton8.Core.Contracts` assembly reference. `Assets/_Project/Scripts/UI/TerminalOS/Editor/Hecton8.UI.TerminalOS.Editor.asmdef`, `TerminalOsLayoutValidator.cs`, and `OscilloscopeDecryptionTunerWindow.cs` now reference/import `Hecton8.Core.Contracts`. Static Python gates pass after this source-side fix, but Unity compile/import proof is still pending.
- Follow-up unsuffixed probe refresh `UnityCaptureSurfaceCrestActualTerrainProbe_20260606_061409.log` provides external Unity compile evidence for that TerminalOS fix: Tundra build success, PNG/TXT written at 06:18, and no TerminalOS compile errors. Current hard-gate rerun rejects with `TERRAIN_PROBE_EVIDENCE_REJECTED blockers=10` because Unity emits `MemoryLeaks` and the metadata remains non-production `editor_only_unsaved` h8_1914 with erosion/anomaly disabled, anomaly/sediment unlinked, and non-eroded height/splat links. The later 06:24 PNG is visually rejected: rectangular water/terrain bands, hard horizon strip, dark detached shoreline chunks, and weak Aegir atmosphere integration.
- `UnityCaptureSurfaceCrestActualTerrainProbe_EditorGPU_20260606_062050.log` identifies the active memory leak source in the proof route: Crest `QueryBase` allocates `NativeArray<Vector3>` and `ComputeBuffer`, while `OceanRenderer` allocates another `ComputeBuffer`, all through `H8VisualProofCapture1912.InvokeCrestRunUpdate`. `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs` now forces Crest recovery probe cleanup before `EditorApplication.Exit`: `_debug._destroyResourcesInOnDisable=true`, disables the `OceanRenderer` after metadata write, pumps the editor loop, and destroys the temporary `HideAndDontSave` ocean material. This is static/source repair only until a fresh post-patch capture proves no Unity `MemoryLeaks` payload.
- `UnityCaptureSurfaceNoTerrainShell_EditorGPU_20260606_062938.log`, `UnityCaptureSurfaceNoTerrainShell_EditorGPU_20260606_063306.log`, and `UnityCaptureSurfaceNoTerrainShell_AutorunEditorGPU_20260606_064605.log` provide post-cleanup compile/import-only evidence with Tundra success and no scoped `error CS` rows, but no PNG/TXT capture output. `_064605` rejects with `TERRAIN_PROBE_EVIDENCE_REJECTED blockers=9` because capture output is missing and the h8_1914 metadata remains diagnostic; its already-disposed `CancellationTokenSource` row is editor/import noise unless a narrower owner route proves otherwise.
- `UnityMapMagicGraphIntegrator_20260606_063611.log` attempted the Unity API graph integration route and aborted because another Unity instance had the project open. No graph integration proof or serialized graph mutation was produced.
- `UnityMapMagicGraphIntegrator_20260606_073258.log` attempted the Unity API graph integration route again and aborted for the same reason: another Unity instance was running `CaptureSurfaceCrestCleanTerrainProbeAndExit`. No graph integration proof or serialized graph mutation was produced.
- `UnityCaptureSurfaceCrestCleanTerrainProbe_EditorGPU_20260606_073231.log` reached Tundra success, wrote `h8_1916_surface_crest_clean_terrain_probe.png/.txt`, and then emitted Unity `MemoryLeaks` from Crest `RunUpdate` / `OceanRenderer` / `QueryBase`. `Tools\ValidateTerrainProbeEvidence.py --require-production` now rejects it with `TERRAIN_PROBE_EVIDENCE_REJECTED blockers=9`: non-production `captureTruth`, Unity `MemoryLeaks`, `editor_only_unsaved`, erosion disabled, anomaly disabled, anomaly height unlinked, splat sediment unlinked, height output not eroded, and splat height not eroded. Visual inspection rejects h8_1916 for flat green water, hard horizon band, detached black shoreline chunks, and crude terrain silhouette.
- `UnityCaptureSurfaceCrestDaylightProbe_EditorGPU_20260606_074642.log` reached Tundra success, wrote `h8_1917_surface_crest_daylight_probe.png/.txt`, and then rejected with `TERRAIN_PROBE_EVIDENCE_REJECTED blockers=10`: non-production `captureTruth`, Unity `MemoryLeaks`, `compile-input-mutated`, `editor_only_unsaved`, erosion disabled, anomaly disabled, anomaly height unlinked, splat sediment unlinked, height output not eroded, and splat height not eroded. Visual inspection rejects h8_1917 for overbearing Aegir, black hard horizon strip, cyan slab water, dark clipped shoreline masses, weak/no shoreline contact foam, and crude terrain silhouette.
- `UnityCaptureSurfaceCrestCoastHorizonProbe_EditorGPU_20260606_080213.log` reached Tundra success, wrote `h8_1918_surface_crest_coast_horizon_probe.png/.txt`, and then rejected with `TERRAIN_PROBE_EVIDENCE_REJECTED blockers=9`: non-production `captureTruth`, Unity `MemoryLeaks`, `editor_only_unsaved`, erosion disabled, anomaly disabled, anomaly height unlinked, splat sediment unlinked, height output not eroded, and splat height not eroded. Visual inspection rejects h8_1918 for overbearing Aegir, black hard horizon strip, cyan slab water, black clipped shoreline masses, weak/no shoreline contact foam, and noisy gold/speckled terrain.
- `PlanetaryCanvasMapMagicGraphIntegrator.cs` now routes all graph rewiring through `LinkGraph(Graph, IOutlet<object>, IInlet<object>)`, a typed wrapper that prevents the previous ambiguous `Graph.Link(...)` overload shape from reappearing. Its upstream height-source resolver also rejects tectonic/erosion/splat/anomaly nodes as reusable sources, preventing an anomaly feedback route if serialized graph history changes. `Tools/ValidateMapMagicErosionSourceRoute.py` now rejects any direct `graph.Link(...)` call outside that wrapper and missing anomaly upstream exclusion.
- `Assets/_Project/Scripts/Plugins/MapMagic/HectonHydraulicErosionMapMagicNode.cs` no longer carries queued delta queue/budget constants, locals, registration helpers, disposal helpers, or apply-budget helpers; static grep shows the node calls only `ScheduleFourPhaseSliced(...)`.
- `HectonHydraulicErosionMapMagicNode` now stores the five `RegisterNativeArray(...)` ids returned by `NativeMemorySentinel` and unregisters temp buffers by `NativeMemorySentinel.Unregister(id)` in `finally`, avoiding pointer reads during cleanup.
- `Assets/_Project/Scripts/Plugins/MapMagic/HectonAnomalyMapMagicNode.cs` now stores the ten TempJob `RegisterNativeArray(...)` ids returned by `NativeMemorySentinel` and unregisters by stable id before disposal, removing the same pointer-based cleanup hazard from the anomaly node.
- `Assets/_Project/Scripts/Plugins/MapMagic/HectonTerrainSplatmapMapMagicNode.cs` and `HectonBiomeMatrixMapMagicPostProcessNode.cs` now also unregister TempJob buffers by stable Sentinel ids, removing pointer-based cleanup from the current terrain graph support nodes.
- `Assets/_Project/Scripts/Editor/ErosionTestHarness.cs` still owns the queued `ScheduleFourPhaseSlicedWithDeltaApply(...)` proof route, but its NativeArray cleanup now also unregisters by stable Sentinel ids; the queue remains pointerless owner/label unregister.
- `ErosionTestHarness` now marks `ScheduleFourPhaseSlicedWithDeltaApply(...)` with `QUEUED_DELTA_APPLY_QUARANTINED`, explicitly stating that production MapMagic must stay on direct `ScheduleFourPhaseSliced(...)` until queue writer/budget/apply lifecycle has fresh Unity proof.
- `Tools/ValidateMapMagicErosionSourceRoute.py` now guards the static source route: direct-only production MapMagic scheduling, queued delta flags, id-based NativeArray unregister in erosion/anomaly/splat/biome nodes, queued harness quarantine marker, no hidden queued-delta callers outside scheduler/harness, anomaly dependency-completion and worker-thread guard ordering, HeightDeltaBudget safety comments, production graph wiring through the typed `LinkGraph` wrapper, anomaly exclusion from upstream height source resolution, and erosion/anomaly recovery defaults enabled.

## Hilbert Recheck - Anomaly Thread Safety

Hilbert's static review confirms the exact ProbeK root:

- `HectonAnomalyMapMagicNode.Generate(...)` calls `HectonAnomalyEngine.ScheduleClosedBasinDetection(...)`.
- `ScheduleClosedBasinDetection(...)` calls `ShouldUseEditorDirectExecution(...)`.
- `HectonAnomalyEngine.ScheduleRidgeFeatureDetection(...)` also calls `ShouldUseEditorDirectExecution(...)` at the same editor-worker-thread risk point.
- `ShouldUseEditorDirectExecution(...)` reads `UnityEditor.EditorApplication.isCompiling` and `.isUpdating`.
- MapMagic main and draft generation are executed through `Den.Tools.Tasks.ThreadManager` worker threads.

The direct execution of pure job `Execute()` code is not the illegal part. The illegal part is reading UnityEditor main-thread-only properties from the worker before the scheduling decision.

Current source patch classification:

- `HectonAnomalyEngine` thread-id guard is `STATIC_REPAIR_PRESENT / UNITY_PROOF_ABSENT`.
- A cached managed thread id from `[InitializeOnLoadMethod]` is a plausible guard, but not strong proof by itself.
- If the guard fails or remains ambiguous, the lowest-risk fallback is to remove editor direct execution from MapMagic worker scheduling entirely and always use the job path there.

Rejected fixes:

- catching `UnityException` around `EditorApplication.isUpdating`;
- disabling anomaly only to get a screenshot;
- treating ProbeK as fixed from source comments;
- any exception-suppression route that leaves thread ownership unclear.

## Carver Recheck - Erosion Job Repair Scope

Carver's static review adds one queued-path caveat and preserves the current repair classification:

- `HeightDeltaBudget` suppression is statically defensible only for the current MapMagic direct schedule, where `QueueHeightDeltas` stays zero and `ScheduleFourPhaseSliced(...)` does not enable queued deltas.
- The current `SAFETY_JUSTIFICATION_PARAGRAPH_*` comments are present and the required unsafe namespace/attribute route is statically compile-plausible.
- The suppression is interim. If queued deltas become production again, split direct and queued erosion jobs rather than broadening safety suppression.
- `TryEnqueueHeightDeltaBounded(...)` validates `HeightDeltaBudget`, but the queued writer route still depends on `HeightDeltaQueue` being configured by the scheduler. Current scheduler sets queue and budget together in `ScheduleFourPhaseSlicedWithDeltaApply(...)`, so this is not the MapMagic direct-path defect, but it is a latent public-job-struct misconfiguration risk.
- `HectonAnomalyEngine` thread-id guard is statically adequate against the obvious MapMagic worker-thread UnityEditor API call, but final architecture should make editor direct execution an explicit caller policy. MapMagic should pass a no-direct-editor-execution policy instead of relying on global `EditorApplication.isCompiling/isUpdating` state.

Additional Carver proof requirements:

- Jobs Debugger plus Collections checks must exercise the current direct MapMagic `ScheduleFourPhaseSliced(...)` route with no optional-container exception.
- `ErosionTestHarness` or equivalent queued path must prove no `HeightDeltaQueue`/`HeightDeltaBudget` safety exception and no TempJob lifetime leak before queued deltas can leave quarantine.
- Anomaly scheduling must be triggered from MapMagic/editor worker context and show zero UnityEditor off-main-thread calls.

## Dirac Recheck - Serialized Graph Diff

Dirac static diff confirms the current serialized `ACTUAL TERRAIN.asset` state remains a diagnostic bypass and no longer matches the restored production-intent integrator source:

- `HectonHydraulicErosionMapMagicNode` is still disabled in the graph metadata.
- `HectonAnomalyMapMagicNode` changed from enabled to disabled.
- `anomaly.heightIn` is cleared.
- mud/brine texture routing is cleared through a null sentinel.
- `splat.sedimentIn` is currently unlinked in latest h8_1914 metadata.

This is not product terrain proof. It is evidence that the serialized graph asset still needs Unity API readback/reintegration against the restored production-intent source route, then must pass Unity import/readback and `Tools/ValidateTerrainProbeEvidence.py --require-production`.

## ProbeN / ProbeO Refresh

- `UnityCaptureSurfaceCrestActualTerrainProbeN_20260606_025336.log` reached Tundra build success and overwrote `h8_1914_surface_crest_recovery_probe.*`, but the metadata remains `editor_only_unsaved`, h8_1914, temp-haze/shell terrain, erosion disabled, anomaly disabled, and unlinked anomaly/sediment inputs.
- `UnityCaptureSurfaceCrestActualTerrainProbeO_20260606_030119.log` overwrote `h8_1914_surface_crest_recovery_probe.*` again at `03:13`, emits Unity `MemoryLeaks`, and records a moving-worktree compile failure: `SeamGapDitherRenderer.cs` changed while Csc was running, then `CS0103` for `_registeredToDispatcher`, `Tundra build failed`, and `Editor compiler errors found. Will not reload assemblies`.
- Current disk source `Assets/_Project/Scripts/SeamGapDitherRenderer.cs` no longer contains `_registeredToDispatcher`; its diff removes `IUpdatable` and the old updatable registration lane, leaving late-frame registration. This statically clears that exact stale-field compile wall, but Unity compile/import proof is still absent.
- Historical ProbeO log evidence previously rejected with `TERRAIN_PROBE_EVIDENCE_REJECTED blockers=13` while the log was present: Unity `MemoryLeaks`, compile error, Tundra failure, editor compiler errors, mutated Csc input, `editor_only_unsaved`, h8_1914, erosion disabled, anomaly disabled, anomaly height unlinked, splat sediment unlinked, height output not eroded, and splat height not eroded.
- `Tools/ValidateTerrainProbeEvidence.py` now rejects compile-poisoned terrain proof logs through `compile-error`, `tundra-build-failed`, `editor-compiler-errors`, and `compile-input-mutated` blockers, incomplete capture-output logs, missing/empty production metadata under `--require-production`, missing `captureTruth`, missing required production link rows, and missing/ambiguous erosion/anomaly `enabled=True` generator rows.
- `python -m unittest Tools/test_validate_terrain_probe_evidence.py` ran 19 tests OK with 1 skipped historical-artifact test, including completed-capture rejection for Unity `MemoryLeaks` plus disabled/unlinked MapMagic graph metadata, generator-enabled production gates, and a source guard requiring Crest recovery probe cleanup before editor exit.
- Current file-state recheck: `python Tools\ValidateTerrainProbeEvidence.py --log Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbeO_20260606_030119.log --metadata Docs\Screenshots\MCP\h8_1914_surface_crest_recovery_probe.txt --require-production` returns `TERRAIN_PROBE_EVIDENCE_REJECTED blockers=10` because the ProbeO log is currently absent and the h8_1914 metadata remains non-production diagnostic state.
- Both logs remain diagnostic only.

## ProbeR / Hazard Compile Refresh

- `UnityCaptureSurfaceCrestActualTerrainProbeR_20260606_033941.log` records Csc input mutation and repeated `CS0136` at `HectonHazardManager.cs(87,39)`, then Tundra build failures.
- Current `HectonHazardManager.cs` line 87 uses `existingZoneManager`, so the exact compile error is likely a stale moving-worktree snapshot.
- `UnityCompileAfterHazardFix_20260606_034456.log` records Tundra build success, but active `dotnet` remained present during controller refresh, so this is not full Unity readiness.
- Historical ProbeR log evidence previously rejected as compile-poisoned while the log was present. Current file-state recheck returns `TERRAIN_PROBE_EVIDENCE_REJECTED blockers=10` because the ProbeR log is currently absent and the h8_1914 metadata remains non-production diagnostic state.

## ProbeS / ProbeT Process Refresh

- Historical ProbeS/ProbeT log evidence did not contain completed production capture output markers while the logs were present.
- Current file-state recheck: `Docs\Logs` no longer contains the ProbeS or ProbeT log files.
- `python Tools\ValidateTerrainProbeEvidence.py --log Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbeS_20260606_034800.log --metadata Docs\Screenshots\MCP\h8_1914_surface_crest_recovery_probe.txt --require-production` returned `TERRAIN_PROBE_EVIDENCE_REJECTED blockers=9`.
- `python Tools\ValidateTerrainProbeEvidence.py --log Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbeT_20260606_034946.log --metadata Docs\Screenshots\MCP\h8_1914_surface_crest_recovery_probe.txt --require-production` returned `TERRAIN_PROBE_EVIDENCE_REJECTED blockers=9`.
- Later external h8_1914 relaunches at `Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbe_20260606_052914.log`, `_053214.log`, `_053625.log`, `_054049.log`, `_054423.log`, `_054748.log`, `_060418.log`, `_061409.log`, `UnityCaptureSurfaceCrestActualTerrainProbe_EditorGPU_20260606_062050.log`, `UnityCaptureSurfaceNoTerrainShell_EditorGPU_20260606_062938.log`, `UnityCaptureSurfaceNoTerrainShell_EditorGPU_20260606_063306.log`, and `UnityCaptureSurfaceNoTerrainShell_AutorunEditorGPU_20260606_064605.log` were stopped or rejected by the controller. `Tools\ValidateTerrainProbeEvidence.py --require-production` rejected `_052914`, `_053214`, `_053625`, and `_054423` with `blockers=9`; `_054049` was rejected with `blockers=11` after stale/moving source snapshot `CS0246` rows; post-patch `_054748` had `Tundra build success` and rejected with `blockers=9` on Unity `MemoryLeaks` plus diagnostic disabled/unlinked graph metadata; `_060418` rejected with `blockers=11` on TerminalOS editor `CS0246`/`CS0012`, `Tundra build failed`, missing capture output, and the same diagnostic metadata; `_061409` compiled and captured but rejected with `blockers=9` on `MemoryLeaks` plus diagnostic graph metadata; `EditorGPU_062050` compiled and captured but rejected with `blockers=9` on `MemoryLeaks` plus diagnostic graph metadata; `NoTerrainShell_062938`, `NoTerrainShell_063306`, and `NoTerrainShell_AutorunEditorGPU_064605` reached Tundra success but did not produce complete capture output and reject with `blockers=9` including `capture-output-missing` plus the same diagnostic disabled/unlinked graph metadata. `UnityMapMagicGraphIntegrator_20260606_063611.log` aborted because another Unity instance had the project open, so it produced no graph integration proof. These logs are process-cleanup/rejection evidence only, not terrain proof.

## Compile Refresh / ILPP Blocker

- `UnityCompileClean_20260606_042058.log` cleared the earlier save/seam blockers but failed on moving-worktree hazard telemetry method visibility; current disk source contains `TryEnsureHazardTelemetryBuffers`, `ReleaseHazardTelemetryBuffers`, and `RecordHazardBlackBoxTelemetry`.
- `UnityCompileClean_20260606_042751.log` then failed on a stale/moving source snapshot missing decryption DTO and binary-layout imports.
- Current `AcousticEchoLocationRuntime.cs` no longer depends on `Hecton8.UI`; current `HazardZoneManager.cs` already imports `Hecton8.Core.Memory.Layout`.
- `UnityCompileClean_20260606_0446_import_fix.log` contains no scoped `error CS` rows for the earlier blockers, but the batch fails at `Unity.ILPP.Trigger.exe` `ExitCode -1` after a system-error dialog.
- `UnityCompileClean_20260606_051745_stable_import.log` supersedes the ILPP-blocked run for the source state at that time: Tundra success at lines 1240, 2174, and 2187, final Unity return code 0 at line 2521, and no scoped old source blocker markers. Primary copy: `C:\hades\.codex_ops\logs\UnityCompileClean_20260606_051745_stable_import.log`; secondary copy: `Docs\Logs\UnityCompileClean_20260606_051745_stable_import.log`.
- The later TerminalOS editor asmdef fix is source-present and externally supported by `_061409` h8_1914 diagnostic compile/capture.
- `H8VisualProofCapture1912.cs` briefly gained an autorun hook/marker route that caused `UnityCaptureSurfaceNoTerrainShell_AutorunEditorGPU_20260606_063601.log`; the controller removed only that autorun hook and deleted the marker.
- `UnityCompileAutorunHook_20260606_064114.log` now provides compile-only proof for the latest source state after the TerminalOS asmdef fix and autorun-hook removal: Tundra success at line 1211 and final Unity return code 0 at line 1640, with no scoped compile-error markers. Terrain proof remains absent because no production terrain generation/readback route was run.

## Static Classification

The ProbeF log is real failure evidence, but it is not proof that the current disk source still schedules `HydraulicErosionDeltaApplyJob` through the MapMagic node.

Current source classification:

- `HectonHydraulicErosionMapMagicNode`: `DIRECT_PRODUCTION_SOURCE_ROUTE_PRESENT / PENDING_UNITY_REIMPORT_OR_COMPILE_PROOF`.
- `PlanetaryCanvasMapMagicGraphIntegrator`: `PRODUCTION_INTENT_GRAPH_WIRING_RESTORED / UNITY_GRAPH_ASSET_READBACK_REQUIRED`.
- `HectonAnomalyMapMagicNode`: `ID_BASED_NATIVEARRAY_UNREGISTER_STATIC_REPAIR_PRESENT / UNITY_COMPILE_PROOF_ABSENT`.
- `HectonTerrainSplatmapMapMagicNode` and `HectonBiomeMatrixMapMagicPostProcessNode`: `ID_BASED_NATIVEARRAY_UNREGISTER_STATIC_REPAIR_PRESENT / UNITY_COMPILE_PROOF_ABSENT`.
- `HectonSpaceEngine098MapMagicNodes` and `HectonSandboxAbyssalShelfMapMagicNode`: `ID_BASED_NATIVEARRAY_UNREGISTER_STATIC_REPAIR_PRESENT / UNITY_COMPILE_PROOF_ABSENT`.
- `HectonHydraulicErosionMapMagicNode` queued leftovers: `STATIC_CLEANED_FROM_PRODUCTION_NODE / UNITY_COMPILE_PROOF_ABSENT`.
- `HectonHydraulicErosionMapMagicNode` Sentinel cleanup: `ID_BASED_UNREGISTER_STATIC_REPAIR_PRESENT / UNITY_COMPILE_PROOF_ABSENT`.
- `ErosionTestHarness` queued route: `QUARANTINED_EDITOR_PROOF_TOOL / ID_BASED_NATIVEARRAY_UNREGISTER_STATIC_REPAIR_PRESENT / UNITY_COMPILE_PROOF_ABSENT`.
- Queued delta source comment: `QUARANTINE_MARKER_PRESENT / UNITY_COMPILE_PROOF_ABSENT`.
- Source-route validator: `MAPMAGIC_EROSION_SOURCE_ROUTE_OK / STATIC_GUARD_PRESENT / HIDDEN_QUEUED_CALLERS_GUARDED / MAPMAGIC_PLUGIN_ID_UNREGISTER_GUARDED / ANOMALY_DEPENDENCY_AND_THREAD_GUARD_GUARDED / GRAPH_LINK_OVERLOAD_GUARDED / ANOMALY_UPSTREAM_SOURCE_GUARDED`.
- ProbeL/batchmode bypass route: `HISTORICAL_NAMED_JOB_FAILURES_CLEARED_WHEN_LOG_PRESENT / LATEST_UNSUFFIXED_LOG_REJECTED / VISUAL_ROUTE_REJECTED / PRODUCTION_GENERATION_NOT_PROVEN`.
- `ScheduleFourPhaseSlicedWithDeltaApply`: `STILL_EXISTS / SAFETY_SENSITIVE / DO_NOT_ROUTE_MAPMAGIC_THROUGH_IT_WITHOUT_NEW_PROOF`.
- `TryEnqueueHeightDeltaBounded`: `BUDGET_GUARDED / QUEUE_WRITER_CONFIGURATION_STILL_CALLER_OWNED / QUEUED_PATH_REMAINS_QUARANTINED`.
- `NativeMemorySentinel.UnregisterNativeArray(array)`: `STILL_SAFETY_SENSITIVE_IF_USED_WITH_LIVE_WRITER_HANDLE / FIRST_PARTY_MAPMAGIC_PLUGIN_CLEANUP_MOVED_TO_ID_UNREGISTER`.
- ProbeF log: `CURRENT_BLOCKER_UNTIL_CURRENT_SOURCE_IS_PROVEN_IN_UNITY`.
- ProbeN/ProbeO: `DIAGNOSTIC_OR_ACTIVE_COMPILE_LANE / NOT_ACCEPTANCE_PROOF`.
- ProbeR: `COMPILE_POISONED_DIAGNOSTIC_LANE / NOT_ACCEPTANCE_PROOF`.
- Current serialized graph asset: `ANOMALY_DISABLED_DIAGNOSTIC_BYPASS / SOURCE_RESTORED_BUT_UNITY_GRAPH_READBACK_REQUIRED`.
- Terrain probe validator: `COMPILE_POISON_BLOCKERS_PRESENT / TERMINALOS_CONTRACT_REFERENCE_REPAIRED_AND_EXTERNALLY_COMPILE_PROVEN / CREST_PROOF_ROUTE_CLEANUP_PATCHED_STATIC_ONLY / MEMORY_LEAK_AND_DIAGNOSTIC_GRAPH_BLOCKERS_PRESENT / GENERATOR_ENABLED_ROWS_REQUIRED / UNIT_TEST_GREEN`.

## Rejections

- Do not claim the surface route is fixed from this static review.
- Do not claim the TempJob leak is gone until a fresh Unity log proves no `TempJob` leak after current source import.
- Do not suppress live writer-fence, sentinel cleanup, or TempJob leak exceptions with `NativeDisableContainerSafetyRestriction`.
- Optional unused container validation suppression is accepted only with source-local safety justification and fresh Unity proof; proof is still absent.
- Do not re-enable the queued delta-apply MapMagic path until a completion/sentinel contract is proven.
- Do not hide terrain failure with haze, rocks, flora, fog, bloom, or screenshots from the rejected h8_1914 probe route.
- Do not treat ProbeL as production terrain proof because both erosion and anomaly nodes are disabled in the graph metadata.
- Do not accept the ProbeL screenshot visually; it still violates surface/coast/water floor.
- Do not bypass `Tools/ValidateTerrainProbeEvidence.py --require-production` for future terrain proof acceptance.

## Required Next Proof

When process gate is green:

1. Confirm Unity imported and compiled the current `HectonHydraulicErosionMapMagicNode.cs`.
2. Confirm Unity imported and compiled the current `HydraulicErosionJob.cs` and `HectonAnomalyEngine.cs`.
3. Run the no-mutation surface/terrain readback or ProbeK equivalent with a fresh log path.
4. Require zero occurrences of `HydraulicErosionDeltaApplyJob`, `HeightDeltaBudget has not been assigned`, `get_isUpdating can only be called from the main thread`, `Thread failed`, `previously scheduled job`, `TempJob allocates`, and `remaining Allocations on the JobTempAlloc`.
5. Confirm both MapMagic draft and main terrain tasks complete or produce a new exact named blocker.
6. Confirm both anomaly scheduling entry points are covered: `ScheduleClosedBasinDetection(...)` and `ScheduleRidgeFeatureDetection(...)`, or record which one did not execute in the proof route.
7. Confirm terrain generation can complete without dirtying unrelated scene/material assets.
8. If the same exception recurs on current source, inspect for hidden callers of `ScheduleFourPhaseSlicedWithDeltaApply(...)` and add a source-level fix that either completes the exact producer handle before Sentinel unregister or extends id-based unregister to that exact caller.
9. If `HeightDeltaBudget` still recurs on direct scheduling, split direct and queued erosion into separate job structs instead of adding more safety suppression.
10. If queued-path proof is requested, prove `HeightDeltaQueue` and `HeightDeltaBudget` are configured together and remain valid through all producer/consumer handles; otherwise keep queued delta apply quarantined.
11. If `get_isUpdating` still recurs, remove editor direct execution from MapMagic worker scheduling or add explicit caller policy with MapMagic passing `false`, rather than adding exception handling.
12. Run a production-intent terrain proof where erosion/anomaly are either enabled and clean, or explicitly replaced by an approved non-throwing production route; a disabled-node diagnostic bypass is insufficient.
13. Require no Unity `MemoryLeaks` payload in the proof log before accepting terrain generation memory hygiene.
14. Run `python Tools\ValidateTerrainProbeEvidence.py --log <fresh-log> --metadata <fresh-metadata> --require-production`; accepted proof must return `TERRAIN_PROBE_EVIDENCE_ACCEPTED blockers=0`.
15. Run `python Tools\ValidateMapMagicErosionSourceRoute.py`; accepted static source route must return `MAPMAGIC_EROSION_SOURCE_ROUTE_OK`.

## Regression Model

- CPU: current source still uses a cold synchronous publish barrier in MapMagic generation; acceptable only as editor/offline terrain generation until profiler proof says otherwise.
- GC: static review only; no runtime GC measurement.
- Memory: ProbeF log proves historical TempJob leak risk; ProbeJ/K did not repeat TempJob warnings; ProbeL did not repeat TempJob warnings but still emitted a Unity `MemoryLeaks` payload. Memory state remains `PENDING VERIFICATION`.
- Cadence: no gameplay cadence changed.
- Correctness: queued delta-apply safety is a blocker for terrain route proof until current source is re-run in Unity.

## Low / Middle / High / Ultra

- Low: terrain generation must not leak TempJob allocations; visual floor still requires readable terrain material, coastline, and waterline.
- Middle: same safety contract; better terrain breakup can only be promoted after no-leak generation proof.
- High: richer terrain erosion/detail is allowed only if job completion and Sentinel lifecycle stay clean.
- Ultra: capture-grade terrain detail does not override NativeArray ownership or proof requirements.

Final status: `PARTIAL_DIAGNOSTIC_PROOF / CURRENT_PROBEL_LOG_MISSING / TERMINALOS_ASMDEF_FIX_PRESENT / H8_1914_AUTORUN_DISABLED / LATEST_COMPILE_ONLY_PASS_AFTER_HOOK_FIX / TERRAIN_PROOF_ABSENT / MEMORY_AND_VISUAL_ROUTE_REJECTED`.
