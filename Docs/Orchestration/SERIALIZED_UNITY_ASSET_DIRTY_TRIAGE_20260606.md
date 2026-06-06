# Serialized Unity Asset Dirty Triage - 2026-06-06

Status: `STATIC_SERIALIZED_DIFF_TRIAGE / OWNER_DECISION_REQUIRED / TERMINALOS_ASMDEF_FIX_PRESENT / H8_1914_AUTORUN_DISABLED / LATEST_COMPILE_ONLY_PASS_AFTER_HOOK_FIX / ACCEPTANCE_PROOF_NOT_RUN`.
Evidence class: `GIT_DIFF_YAML_STATIC + PROCESS_GATE_READ`.

## Scope

This report covers only the current serialized Unity/MapMagic dirty files:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- `Assets/MapMagic/Map_Graph/New Gen/ACTUAL TERRAIN.asset`
- related diagnostic metadata in `Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.txt`

No Unity editor action, import, Play Mode, profiler, scene save, graph save, material save, prefab save, commit, revert, or YAML mutation was performed by this controller pass. Only this report was written.

## Commands Run

| Command | Exit | Result |
| --- | ---: | --- |
| `git diff -- "Assets/MapMagic/Map_Graph/New Gen/ACTUAL TERRAIN.asset"` | 0 | Serialized MapMagic graph diff read. |
| `git diff -- Assets/_Project/Scenes/02_HECTON_WORLD.unity` | 0 | Serialized scene diff read. |
| `git diff -- Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.txt Docs/Orchestration/MAPMAGIC_HYDRAULIC_EROSION_JOB_SAFETY_STATIC_REVIEW_20260606.md` | 0 | Diagnostic metadata and terrain-safety report diff read. |
| `Get-Process | Where-Object { $_.ProcessName -match 'Unity|dotnet|csc|ILPP|ShaderCompiler' } | Select-Object ProcessName,Id,StartTime,MainWindowTitle` | 0 | No matching processes at final read; process gate was green at this instant. |
| `git diff --check -- Docs/Orchestration/SERIALIZED_UNITY_ASSET_DIRTY_TRIAGE_20260606.md` | 0 | No whitespace errors. |

## Scene Diff

`Assets/_Project/Scenes/02_HECTON_WORLD.unity` adds a new root GameObject:

- name: `[MUSIC_SYSTEM]`
- root transform fileID: `884200002`
- MonoBehaviour fileID: `884200001`
- script: `Hecton8.Audio.HectonMusicDirectorAnchor`
- config reference: guid `3fe2e07be4fdac24cb6b2f12b438dcc3`
- added to `SceneRoots`

Static classification: `INTENT_PLAUSIBLE / UNITY_ACCEPTANCE_ABSENT`.

Risk:

- root-scene mutation is serialized state and cannot be accepted from text alone;
- anchor script/config compile/import status is not proven here;
- if this was created by an automated proof or transient run, it may be accidental scene dirtiness;
- if intentional, it needs scene owner acceptance and a clean Unity import/readback packet.

## MapMagic Graph Diff

`Assets/MapMagic/Map_Graph/New Gen/ACTUAL TERRAIN.asset` contains serialized graph/version/link churn:

- multiple generator version values changed;
- repeated serialized key blocks added for `enabled`, `id`, `version`, `guiPreview`, `guiAdvanced`, `guiDebug`;
- list/link references changed and at least two old references are removed;
- one link target changed to `4294967295`, which is a sentinel/unlinked-style value in this serialized context.

The diagnostic metadata diff adds the higher-level readable interpretation:

- graph summary changed from `linkCount=155` to `linkCount=152`;
- height output input changed from `HectonHydraulicErosionMapMagicNode` to `HectonBiomeMatrixMapMagicPostProcessNode`;
- erosion node changed from `enabled=True version=2` to `enabled=False version=4`;
- splat height input changed from erosion to biome post-process;
- splat sediment input changed to `UNLINKED`;
- anomaly node changed from `enabled=True version=2` to `enabled=False version=4`;
- anomaly height input changed to `UNLINKED`;
- generated terrain state changed from draft/in-progress to main ready, with terrain size height from `0.00` to `250.00`.

Static classification: `HIGH_RISK_GRAPH_MUTATION / NOT_PRODUCT_PROOF`.

Risk:

- this may quarantine broken erosion/anomaly generation, but it also bypasses the route that needs proof;
- static diff cannot prove MapMagic graph integrity, generator execution, or visual correctness;
- graph dirty state may have been produced by h8_1914 diagnostic/probe work and must not be promoted as acceptance evidence;
- if intentional, it needs a named terrain owner and clean Unity import/generation proof.

## h8_1914 Boundary

The same metadata shows the h8_1914 diagnostic route reached a main-ready terrain state after disabling/unlinking erosion and anomaly routes. That is not proof of the product route. It is rejection evidence for the proof route because it changes the system under observation.

`h8_1914_surface_crest_recovery_probe.*` remains diagnostic-only and must not enter acceptance packets.

## Findings

### Finding 1 - High - Scene Root Was Added

The `[MUSIC_SYSTEM]` root object may be required for audio route bootstrapping, but it is a serialized scene mutation. Accept only after scene owner readback verifies the object, script GUID, config GUID, playmode boot behavior, and no unrelated scene churn.

### Finding 2 - High - MapMagic Erosion/Anomaly Route Was Disabled Or Unlinked

Readable metadata indicates erosion and anomaly nodes are now disabled or unlinked and height output bypasses hydraulic erosion. This may make generation complete, but it is not a proof that the erosion/anomaly route is fixed. It is a route change that requires owner decision.

### Finding 3 - High - Diagnostic Route Can Hide Product Gaps

The h8_1914 path now shows ready terrain after graph mutation. Because h8_1914 is already rejected as a diagnostic/probe route, this output cannot be used as product visual proof.

## Required Next Gates

1. Terrain owner decides whether the MapMagic graph mutation is intentional repair, temporary quarantine, or accidental dirty state.
2. Scene owner decides whether `[MUSIC_SYSTEM]` belongs in `02_HECTON_WORLD.unity`.
3. If either mutation is intentional, run Unity import/readback only after the dirty set is frozen.
4. Require clean logs for compile/import and exact route readback before accepting.
5. Keep h8_1914 probe output classified as diagnostic rejection evidence.

## Follow-Up Passive Unity Batch Observation

An external Unity batchmode process later ran `Hecton8.Editor.H8VisualProofCapture1912.CaptureSurfaceCrestRecoveryProbeAndExit` and wrote:

- `Docs/Logs/UnityCaptureSurfaceCrestActualTerrainProbeO_20260606_030119.log`
- `Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.png`
- `Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.txt`

The metadata is useful readback but not acceptance. It reports `captureTruth=surface_actual_terrain_crest_recovery_probe_editor_only_unsaved`, MapMagic tiles ready, height output from biome post-process, erosion disabled, anomaly disabled, `splat.sedimentIn=UNLINKED`, and `anomaly.heightIn=UNLINKED`.

The screenshot shows a visible Aegir, sky/clouds, island silhouette, terrain material, and shoreline elements, but also a broad flat rectangular band and incomplete product composition. It remains h8_1914 diagnostic evidence only and cannot substitute for canonical h8_1475 acceptance.

The same Unity log also contains a prior `CS0103` compile failure in `SeamGapDitherRenderer.cs`. The source blocker was fixed by the controller afterward. Later ProbeP/ProbeQ/ProbeS attempts repeated the same external h8_1914 capture route and stalled before useful evidence; the controller stopped the repeated probe loop. A first controlled Unity compile attempt exited before compilation due to Unity licensing/access-token initialization failure. A later controlled compile log, `Docs/Logs/UnityCompileAfterProofPatch_20260606_033000.log`, reached Tundra successfully twice and emitted no scoped C# error markers, but Unity/ILPP required forced cleanup after log growth stopped.

Later compile refreshes changed the current boundary. `UnityCompileClean_20260606_0446_import_fix.log` no longer shows the previous scoped source errors, but temporarily failed at `Unity.ILPP.Trigger.exe` `ExitCode -1`. That was superseded for the source state at that time by `UnityCompileClean_20260606_051745_stable_import.log`, which reached Tundra success three times and ended with Unity return code 0. Primary copy: `C:\hades\.codex_ops\logs\UnityCompileClean_20260606_051745_stable_import.log`; secondary copy: `Docs\Logs\UnityCompileClean_20260606_051745_stable_import.log`. A later h8_1914 run, `UnityCaptureSurfaceCrestActualTerrainProbe_20260606_060418.log`, exposed a TerminalOS editor compile wall because `Hecton8.UI.TerminalOS.Editor.asmdef` did not reference `Hecton8.Core.Contracts`; the asmdef source fix is now present. Follow-up diagnostic logs `UnityCaptureSurfaceCrestActualTerrainProbe_20260606_061409.log`, `UnityCaptureSurfaceCrestActualTerrainProbe_EditorGPU_20260606_062050.log`, `UnityCaptureSurfaceNoTerrainShell_EditorGPU_20260606_062938.log`, and `UnityCaptureSurfaceNoTerrainShell_EditorGPU_20260606_063306.log` reached Tundra success after that fix, but remain rejected h8_1914 evidence. `_061409` and `EditorGPU_062050` reject with Unity `MemoryLeaks` plus diagnostic disabled/unlinked graph metadata; the NoTerrainShell logs reject with `capture-output-missing` plus the same diagnostic metadata. `H8VisualProofCapture1912.cs` then briefly gained an autorun hook/marker path that caused `UnityCaptureSurfaceNoTerrainShell_AutorunEditorGPU_20260606_063601.log`; the controller removed the hook and marker, and `UnityCompileAutorunHook_20260606_064114.log` reached Tundra success plus final Unity return code 0. This still does not validate the serialized scene or MapMagic graph mutations.

## Final Status

`SERIALIZED_ASSET_OWNER_DECISION_REQUIRED / H8_1914_DIAGNOSTIC_REJECTION_ONLY / EXTERNAL_PROBE_LOOP_STOPPED / TERMINALOS_ASMDEF_FIX_PRESENT / H8_1914_AUTORUN_DISABLED / LATEST_COMPILE_ONLY_PASS_AFTER_HOOK_FIX / NO_ACCEPTANCE_PROOF / NO_ASSET_EDIT_BY_CONTROLLER`
