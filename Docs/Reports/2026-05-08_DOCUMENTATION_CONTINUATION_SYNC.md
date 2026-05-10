# 2026-05-08 Documentation Continuation Sync
Date: 2026-05-09
Status: PENDING FINAL UNITY PROOF (R186 DOTNET BUILD PASSED / UNITY MCP BLOCKED)
Scope: broad documentation authority refresh after local midnight source churn and R31-R186 compile gates

Mandates followed:

- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `AGENTS.md`

## Current Truth Boundary

This report is the latest documentation synchronization layer as of local workspace time `2026-05-09 19:39 +04:00`.

It supersedes May 7 source-count and build-blocker statements where they conflict.
It does not certify Play Mode, profiler, GCMonitor, scene/prefab wiring, player build, or memory retention.

R186 is the latest completed local compile gate captured by this pass. `CodexArtifacts/2026-05-09_R186_CORE_FULLGRAPH_SERIAL_NORESTORE_BUILD.log` ended with `DOTNET_EXIT_CODE=0`, `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, `CS_WRITES_AFTER_START=0`, and `CS_WRITES_AFTER_END=0`. R186 supersedes R171-R185 after R171/R174/R175/R177/R182/R183 source-write invalidation, R179 transient current-source compile blockers, R181 invalid root-only dependency mode, R184 `IAudioService` contract drift, and R185 `PredatorCognitionDomain` static/instance vortex-steering drift. This is not Unity Console, Play Mode, profiler, GCMonitor, player-build, scene wiring, runtime zero-GC, frame-time, or memory-retention proof.

## Filesystem Snapshot

Current documentation scan:

| Surface | Count |
|---|---:|
| `Docs/**/*.md` | `444` |
| active markdown excluding archive/deprecated/report-deprecated/obsolete | `231` |
| active non-report markdown | `163` |
| direct `Docs/Reports/*.md` | `68` |
| root `.md` files | `5` |
| root `.txt` files | `0` |
| root `.log` files | `0` |

Root markdown files observed:

- `AGENTS.md`
- `BROKEN_PREFABS.md`
- `BUILD_PLAYTEST_ISSUES.md`
- `MASTER_RELEASE_WORK_PLAN.md`
- `TERRAIN_AND_BIOME_REALITY_MAP.md`

Only `AGENTS.md`, `BUILD_PLAYTEST_ISSUES.md`, and `MASTER_RELEASE_WORK_PLAN.md` are active root documentation anchors.
`BROKEN_PREFABS.md` is a generated snapshot.
`TERRAIN_AND_BIOME_REALITY_MAP.md` remains a compatibility mirror; canonical authority is under `Docs/Reports/`.

## Source Snapshot

Latest completed `System.IO.StreamReader.ReadLine()` source-count pass during the 2026-05-08 manifest generation:

| Surface | C# files | Physical lines |
|---|---:|---:|
| `Assets/_Project` | `1292` | `759122` |
| `Assets/_Project/Scripts` | `1248` | `742892` |

Additional orientation data:

- direct scripts under `Assets/_Project/Scripts`: `342`
- `GlobalRegistryContracts.cs` direct public interfaces: `40`
- latest authoritative line-count method: `System.IO.StreamReader.ReadLine()` over every matched `.cs` file

May 7 comparison against `2026-05-07_MAIN_DOCUMENTATION_CURRENT_STATE_REFRESH.md`:

- previous main refresh: `1233` project C# files, `1192` script C# files, `683064` project physical lines, `667771` script physical lines, `39` direct public registry interfaces
- current manifest delta: `+59` project C# files, `+56` script C# files, `+76058` project physical lines, `+75121` script physical lines, `+1` direct public registry interface

Earlier observed source writes during this pass included:

- `Assets/_Project/Scripts/Audio/DeepPsychosisController.cs`
- `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs`
- `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs`
- `Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs`
- documentation authority files under `Docs/Reports/` and `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/`

Line counts are orientation data. At the R186 validation boundary, no `Assets/_Project/**/*.cs` writes were observed after the build start or end timestamp; treat this as current local `dotnet` compile evidence, not Unity/runtime proof.

## MCP Boundary

Unity MCP resource state in the current session: `list_mcp_resources` returned an empty resource list. The previous successful readback below is historical only and is not current Unity Console proof:

- `mcpforunity://project/info`: success; project root `C:/hades/Hecton8`, Unity `6000.4.1f1`, platform `StandaloneWindows64`
- `mcpforunity://editor/state`: success; active scene `Assets/_Project/Scenes/00_BOOTSTRAP.unity`, Play Mode off, compiling false, domain reload pending false, ready for tools true
- `read_console(error,warning)`: success; retrieved `0` log entries
- `mcpforunity://rendering/stats`: success; render textures `37`, render texture bytes `56320492`, draw calls/batches/set-pass reported `0` in editor snapshot
- `mcpforunity://pipeline/renderer-features`: success; active renderer `PC_Renderer`, `10` features

Renderer feature snapshot:

- inactive: `ScreenSpaceAmbientOcclusion`, `ShapesRenderFeature`, `DecalRendererFeature`, `ScreenSpaceShadows`
- active: `HectonScooterVolumetricShaftsFeature`, `HectonAbyssalSsdoFeature`, `HectonVisorFluidDistortionFeature`, `SaveThumbnailCaptureFeature`, `HectonRetinaDistortionFeature`, `HectonVRBrownoutFeature`

Build-master hallucination check on 2026-05-08 supersedes any MCP-clean status claim in this file. Treat MCP proof as blocked until a fresh console read is captured. This is not Play Mode, profiler, GCMonitor, player-build, scene gameplay, or memory-retention proof.

## Build Evidence Boundary

Latest completed full Core dependency build after the current autonomous compile gate:

- command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`
- artifact: `CodexArtifacts/2026-05-09_R186_CORE_FULLGRAPH_SERIAL_NORESTORE_BUILD.log`
- result: `Build succeeded`
- summary: `0 Warning(s)`, `0 Error(s)`
- timestamp gate: `CS_WRITES_AFTER_START=0`, `CS_WRITES_AFTER_END=0`
- first-party compile errors: `0` in the R186 log

Warning boundary:

- latest serial `dotnet build ./Hecton8.Core.csproj --no-restore /m:1 /nr:false /p:UseSharedCompilation=false` succeeded in R186 with `0` warnings and `0` errors. This is compile-only evidence, not runtime allocation/frame-time proof.

The previous MCP-clean note is no longer accepted as current proof. Treat R186 as the latest local `dotnet` evidence only, not Play Mode, Unity Console, profiler, player-build, or runtime proof.

Post-build source churn check after `CodexArtifacts/2026-05-09_R186_CORE_FULLGRAPH_SERIAL_NORESTORE_BUILD.log`:

- R186 artifact start/end timestamps: `2026-05-09 19:39:29 +04:00` / `2026-05-09 19:39:36 +04:00`
- later `Assets/_Project/**/*.cs` writes after artifact start: `0`
- later `Assets/_Project/**/*.cs` writes after artifact end: `0`
- current-source local `dotnet` compile gate is proven for the sampled filesystem state; Unity proof is still absent

R130-R186 compile-surface repairs and blockers captured by this continuation:

- R130/R132/R136/R138/R141/R151/R154/R155/R160: clean `0 Warning(s)` / `0 Error(s)` snapshots that were invalidated by later source writes, in-build source writes, or timestamp proof failure.
- R133-R144: live-churn blockers included `HUDQuickBar` tool-manager subscription drift, `NoiseSystem` property-by-`in` misuse, `CrashTelemetryBuffer` missing callbacks/thread state, `UITooltip` unsupported `AsSpan`, `SpatialAudioManager` AUP helper/signature drift, `InputManager` invalid Input-to-Core `GlobalRegistry` references, `PlayerActionController` missing progress helper, `OxygenBubble` drift helper churn, and `SoundscapeSystem` stale native-memory overflow API calls.
- R145-R150: current pass repaired or revalidated BLACKBOX export callback/state drift, player action progress math, emergency relay deferred listener mutation, `WorldProceduralFieldSampler` 3-arg `math.max` drift, and `PlayerSwimPresentationController` obstacle-probe drift. R149/R150 exposed missing/double `_handObstacleHits`; current R164 source no longer uses `_handObstacleHits` or `SphereCastNonAlloc` and instead resolves recent wall-contact presentation state.
- R152-R153: stale live-write snapshots reproduced missing BLACKBOX export thread and Input `GlobalRegistry` regressions; current source no longer contains those compile blockers.
- R156-R162: R155 was invalidated by a later `InputManager.cs` write; R158 exposed missing local `_instance`; R160 passed but was invalidated by `ScannerTool.cs` and `FakeRadarBlipController.cs` writes during the build plus `InputManager.cs` after the build; R161 exposed reintroduced `GlobalRegistry` references in the Input assembly; R162 targeted Input build passed after local ownership repair.
- R163-R164: R163 passed cleanly but was invalidated by later `PhysicsApplySystem.cs` and `NativeMemorySentinel.cs` writes; R164 stable control build passed with `0 Warning(s)`, `0 Error(s)`, `DOTNET_EXIT_CODE=0`, and `0` `.cs` writes after build start/end at that snapshot.
- R165-R178: R165 passed cleanly but was superseded by post-build guarded diagnostic-log preprocessor edits in `LocalizationManager.cs` and `HectonBiolumManager.cs`; R166 passed with `0 Warning(s)`, `0 Error(s)`, `DOTNET_EXIT_CODE=0`, and `0` `.cs` writes after build start/end. R167/R168 were invalidated by source-write contamination. R169 reproduced current-source `CaveBioRootsGenerator.cs` missing-field/helper blockers after external churn. R170 ended with `DOTNET_EXIT_CODE=-1`, no build summary, and no compiler error lines. R171 passed but remained timestamp-contaminated by in-build and post-build C# writes. R173 exposed a transient `RandomEventSystem.cs` missing `TryResolvePlayerMeteorFrame` compile blocker while that file was being rewritten. R174 passed but had two C# writes after build start. R175 passed but had five C# writes after build start and retained two obsolete `GetInstanceID()` warnings. R176 passed cleanly but was invalidated by a later `HabitatIntegrityManager.cs` write. R177 passed with five `HectonFluidEngine.cs` CS0414 warnings and two in-build writes. R178 passed with `0 Warning(s)`, `0 Error(s)`, `DOTNET_EXIT_CODE=0`, `CS_WRITES_AFTER_START=0`, and `CS_WRITES_AFTER_END=0`.
- R179-R186: R179 failed on a stale live-write snapshot containing missing `ZeroGCFormatter`, `HectonAnalyticalFlowField`, `HectonGerstnerWater`, and partial `BuoyancyJob` fields. Current source already contained some of those fixes, so no false ownership was claimed. R180 aborted with `DOTNET_EXIT_CODE=-1` while `SteamManager.cs` was written. R181 root-only `--no-dependencies` mode was rejected as invalid for this Unity dependency graph. R182 passed but had duplicate `CrestBridge.cs`/`IOceanKinematics.cs` includes plus source-write contamination. R183 passed with `0 Warning(s)`/`0 Error(s)` but was invalidated by a new `Dev/BotController.cs`. R184 exposed `IAudioService.QueueAudioEvent(in AudioEvent)` drift in `SpatialAudioManager`/`NoOpAudioService`. R185 exposed `PredatorCognitionDomain` static/instance vortex-steering drift and `BotController` obsolete lookup warning. R186 passed with `0 Warning(s)`, `0 Error(s)`, `DOTNET_EXIT_CODE=0`, `CS_WRITES_AFTER_START=0`, and `CS_WRITES_AFTER_END=0`.

R53-R129 compile-surface repairs and blockers captured by this continuation:

- R57-R63: source churn exposed stale callback/helper gaps in `CrashTelemetryBuffer`, `SalvageSamplerTool`, `SargassumMicroFaunaBoids`, `CorporateOrderSystem`, `SpectrumSystem`, and `HectonBiolumController`; later clean gates superseded the failed snapshots.
- R67: compile passed but became stale after later source writes.
- R68: failed on `UIAudioFeedback` service callback contract drift.
- R69: failed on `UITooltip` missing `IServiceShutdown` contract.
- R70: failed on `ToolDurabilitySystem` bounded command lane type drift.
- R71: failed with live-churn compile blockers that were subsequently repaired or externally restored.
- R72: passed with `0 Warning(s)`, `0 Error(s)`, but was invalidated by later `Assets/_Project/Scripts/**/*.cs` writes.
- R73: passed with `0 Warning(s)`, `0 Error(s)`, but was invalidated by a later `HectonVoxelEngine.cs` write.
- R74: passed with `0 Warning(s)`, `0 Error(s)`, but was invalidated by later live source writes during documentation update.
- R75: failed with `48 Warning(s)`, `3 Error(s)` from `RelayHUDElement.cs` `char[].AsSpan()`, `CrashTelemetryBuffer.cs` stale `AwaitableDebtMonitor.ConsumePeakNextFrameContinuations`, and `LODSystemManager.cs` `List<T>` indexer passed by `in`.
- R76: passed with `0 Warning(s)`, `0 Error(s)`, but was invalidated by later live source writes.
- R77: failed with `0 Warning(s)`, `2 Error(s)` on transient `SargassumMicroFaunaBoids.cs` `_BoidGroupBoundsId` reads; current source no longer contains that symbol, and a later `RuntimeWatchdog.cs` write invalidated the snapshot.
- R78: passed with `0 Warning(s)`, `0 Error(s)`, but was invalidated by later live source writes.
- R79: failed with `0 Warning(s)`, `3 Error(s)` from `SargassumMicroFaunaBoids.cs` shadowed `spawnUploadCount` plus ambiguous `Debug` references in `RuntimeWatchdog.cs`.
- R80: passed with `0 Warning(s)`, `0 Error(s)`, but was invalidated by a post-build `SpectrumSystem.cs` write.
- R81: passed with `0 Warning(s)`, `0 Error(s)`, but was invalidated by a post-build `SpatialAudioManager.cs` write.
- R82: passed with `0 Warning(s)`, `0 Error(s)`, but was invalidated by later `KinematicGhostDebugger.cs` source churn.
- R83-R86: exposed recurrent live-churn blockers in `CrashTelemetryBuffer`, `ScavengePopulator`, `PDAMarkerHUDElement`, and `BeaconHUDElement`.
- R87: passed with `0 Warning(s)`, `0 Error(s)`, then later source writes invalidated it.
- R88-R109: repeated live-churn passes; blockers included `SpatialAudioManager`, `RepairTool`, `MissionMarkerSystem`, `PauseMenuController`, `CrashTelemetryBuffer`, `FaunaDirector`, `MantaScooter`, `MigrationDirector`, and transient `DOTNET_EXIT_CODE=-1` aborts.
- R110: passed with `0 Warning(s)`, `0 Error(s)`, then later source writes invalidated it.
- R111-R120: exposed stale/live-churn blockers in `QueryCacheContext`, `CrashTelemetryBuffer`, `GlobalPhysicsStateManager`, `FaunaSpatialHashRegistry`, `ToolTrialRangeRuntimeSmokeTester`, `SuitAdvisoryController`, `SpatialAudioManager`, `FaunaGeneticsManager`, `PlayerBuilder`, `SargassumMicroFaunaBoids`, `NarrativeEvents`, and `QueryCacheContext` trigger-mode callers; R119 was a compiler file-lock, not a source error.
- R121: passed with `0 Warning(s)`, `0 Error(s)`, then later script writes invalidated it.
- R122-R124: exposed new live-churn blockers in `SubmarineAtmosphereSystem`, `LoadingTipsDisplay`, `PropulsionTool`, `CrashTelemetryBuffer`, `NoiseSystem`, and `FaunaSensorSuite`; R124 passed, then a later `PhysicalBatteryCompartment.cs` write invalidated it.
- R125-R126: passed with `0 Warning(s)`, `0 Error(s)`, then later script writes invalidated each snapshot.
- R127: passed with `0 Warning(s)`, `0 Error(s)`, then later `SaveThumbnailSystem.cs` and `Editor/PersistenceUxSmokeTester.cs` writes invalidated the snapshot.
- R128: passed with `0 Warning(s)`, `0 Error(s)`, and no later script C# writes were observed after the artifact timestamp.
- R129: passed with `0 Warning(s)`, `0 Error(s)`, and no later script C# writes were observed after the artifact timestamp.

R31-R52 compile-surface repairs and blockers captured by this continuation:

- `LaserCutter.cs` / `HectonSurvivalSystem.cs`: mutable `FixedCharBuffer` fields were no longer kept `readonly`, removing illegal `ref` access to struct state.
- `Input/UserOptionsPersistence.cs`: stale `Hecton8.Core` dependency was removed from the Input assembly surface; bootstrap/Core remains the external registry owner.
- `FlashlightTool.cs`: `string.AsSpan()` usage was replaced with the project `FixedCharBuffer.Append(string)` path for target-framework compatibility.
- `HarpoonLauncherTool.cs`: missing HUD publish helpers were restored through a reusable `FixedCharBuffer` and non-alloc warning/info publish paths.
- `HectonVoxelEngine.cs`: predictive voxel proxy AUP bounds now match the `double3` out-parameter API.
- `FaunaBrain.cs`: transient `float3` to `Vector3` helper churn was caught in the gate chain; the current source contains the helper required by the compiled call sites.
- `InputManager.cs` / `UserOptionsPersistence.cs`: active external/source churn repeatedly rewrites these files into an invalid Input-to-Core dependency shape. They were locally restored again after R48, and R52 compiled that repaired state with `0 Error(s)`. No unrelated C# writes were observed after R52 at the post-build check.

Previous completed builds in this pass:

- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R30.log`
- result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`
- superseded by R31-R44 after later source churn and compile-gate repair

Previous completed build in this pass:

- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R29.log`
- result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`
- later C# writes occurred after this artifact (`MountablePlayerTransport.cs`, `PlayerSwimPresentationController.cs`); `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes it as latest completed-build evidence
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R28.log`
- result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`
- later C# writes occurred after this artifact (`AcousticZoneController.cs`); `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes it as latest completed-build evidence

- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R27.log`
- result: `Build FAILED`, `0 Warning(s)`, `2 Error(s)`
- first blockers: `Assets/_Project/Scripts/Input/UserOptionsPersistence.cs(1,15): Hecton8.Core namespace reference outside the Input assembly surface`; follow-on `MSB4181` on the Core aggregate build
- later source/project state changed; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R26.log`
- result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`
- later C# writes occurred after this artifact (`WorldStateManager.cs`, `HectonPlayerMovement.cs`, `UserOptionsPersistence.cs`, `SettingsManager.cs`, `PhysicalPanelButton.cs`, `PlayerSwimPresentationController.cs`, `MissionMarkerSystem.cs`, `SargassumGlobalDragManager.cs`); `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes it as latest completed-build evidence

- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R25.log`
- result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`
- later C# writes occurred after this artifact (`ARWaypointOverlay.cs`, `MissionMarkerSystem.cs`, `MountablePlayerTransport.cs`, `PlayerInventory.cs`, `MantaScooter.cs`, `AcousticZoneController.cs`, `CreatureDamageManager.cs`, `PlayerSwimPresentationController.cs`, `ProceduralLeviathanSpineIK.cs`, `HectonDirectorAI.cs` and two additional script writes); `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes it as latest completed-build evidence

- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_DOC_GOVERNANCE_FINAL_BUILD_R2.log`
- result: `Build FAILED`, `0 Warning(s)`, `1 Error(s)`
- first blocker: `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs(291,17): SnapSDFToTerrainJob.SnapHysteresisMeters missing during compile read`
- later source/project state changed; `AUTONOMOUS_DOC_GOVERNANCE_FINAL_BUILD_R3.log` supersedes this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_DOC_GOVERNANCE_FINAL_BUILD_R3.log`
- result: `Build succeeded`, `20 Warning(s)`, `0 Error(s)`
- later source/project state changed; `AUTONOMOUS_CONTINUATION_COMPILE_GATE_4.log` supersedes this as latest completed-build evidence
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_CONTINUATION_COMPILE_GATE_4.log`
- result: `Build succeeded`, `5 Warning(s)`, `0 Error(s)`
- later source/project state changed; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes this as latest completed-build evidence
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD.log`
- result: `Build FAILED`, `1 Warning(s)`, `52 Error(s)`
- first blockers: `Assets/_Project/Scripts/HectonVoxelEngine.cs` missing predictive voxel proxy cinematic state members after source churn
- later source/project state changed; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R3.log`
- result: `Build FAILED`, `0 Warning(s)`, `4 Error(s)`
- first blockers: `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs` stale voxel proxy slide helper references
- later source/project state changed; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R4.log`
- result: `Build FAILED`, `0 Warning(s)`, `9 Error(s)`
- first blockers: `Assets/_Project/Scripts/Visor/HectonAtmosphereSootFeature.cs` absent from the generated Core project surface; transient stale `FirstHourDirector` quest-hash type errors were not present in the current source before R6
- later source/project state changed; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R5.log`
- result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`
- later C# writes occurred after this artifact; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes it as latest completed-build evidence
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R7.log`
- result: `Build FAILED`, `2 Warning(s)`, `2 Error(s)`
- first blockers: `Assets/_Project/Scripts/Gameplay/VehicleMotor.cs` stale private `HectonPlayerMotor.SafeNormal(...)` calls; `PauseMenuController.cs` dead cached string warnings
- later source/project state changed; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R8.log`
- result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`
- later C# writes occurred after this artifact; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes it as latest completed-build evidence
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R9.log`
- result: `Build FAILED`, `0 Warning(s)`, `1 Error(s)`
- first blocker: `CS2012` file lock on `Temp/obj/Hecton8.Core/Hecton8.Core.dll`; no source compiler error was proven by this gate
- later serial/no-shared-compiler gates supersede this file-lock result
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R10.log`
- result: `Build FAILED`, `0 Warning(s)`, `3 Error(s)`
- first blocker: `Assets/_Project/Scripts/WorldGenerativeGeologyIntegrationDirector.cs` stale `playerDistance` after squared-distance replacement
- later source/project state changed; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R11.log`
- result: `Build FAILED`, `0 Warning(s)`, `2 Error(s)`
- first blockers: `CrashTelemetryBuffer.cs` missing background export callback field; `SaveSlotHoverPreview.cs` stale preview detail builder read
- later source/project state changed; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R12.log`
- result: `Build FAILED`, `0 Warning(s)`, `38 Error(s)`
- first blocker: `Assets/_Project/Scripts/World/EmergencyServiceRelay.cs` orphaned self-heal block after hash/AUP route refactor
- later source/project state changed; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R13.log`
- result: `Build FAILED`, `0 Warning(s)`, `6 Error(s)`
- first blockers: `CrashTelemetryBuffer.cs` stale `ExecuteBackgroundExport` callback reference; `EmergencyServiceRelayDirector.cs` stale `_instance` singleton residue
- later source/project state changed; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R14.log`
- result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`
- later C# writes occurred after this artifact; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes it as latest completed-build evidence
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R15.log`
- result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`
- later C# writes occurred after this artifact; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes it as latest completed-build evidence
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R16.log`
- result: `Build FAILED`, `0 Warning(s)`, `36 Error(s)`
- first blocker: `Assets/_Project/Scripts/World/EmergencyServiceRelayDirector.cs` orphaned self-heal block after live source churn
- later source/project state changed; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R17.log`
- result: `Build FAILED`, `0 Warning(s)`, `36 Error(s)`
- first blocker: same transient `EmergencyServiceRelayDirector.cs` orphaned self-heal block while the file was still changing
- later source/project state changed; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R18.log`
- result: `Build FAILED`, `0 Warning(s)`, `1 Error(s)`
- first blocker: `Assets/_Project/Scripts/Quest/QuestManager.cs` stale `out questData` definite-assignment read during live churn
- later source/project state changed; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R19.log`
- result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`
- later C# writes occurred after this artifact (`FaunaSteeringEngine.cs`, `HectonFlashlightVoxelShadowProvider.cs`); `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes it as latest completed-build evidence
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_STATUS_SANITY_BUILD_R20.log`
- result: `Build FAILED`, `0 Warning(s)`, `2 Error(s)`
- first blocker: `Assets/_Project/Scripts/Input/UserOptionsPersistence.cs(1,15): Hecton8.Core namespace reference outside the Input assembly surface`
- later source/project state changed; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R21.log`
- result: `Build FAILED`, `0 Warning(s)`, `1 Error(s)`
- first blocker: same `UserOptionsPersistence.cs` stale `using Hecton8.Core` read while the file was still changing
- later source/project state changed; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R22.log`
- result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`
- later C# writes occurred after this artifact (`PredatorCognitionDomain.cs`, `WorldSpatialHashGrid.cs`); `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes it as latest completed-build evidence
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R23.log`
- result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`
- later C# writes occurred after this artifact (`UserOptionsPersistence.cs`); `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes it as latest completed-build evidence
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R24.log`
- result: `Build FAILED`, `0 Warning(s)`, `2 Error(s)`
- first blocker: `Assets/_Project/Scripts/Input/UserOptionsPersistence.cs` was rewritten back to the invalid Input-to-Core dependency
- later source/project state changed; `AUTONOMOUS_ARCHIVARIUS_CORE_BUILD_R44.log` supersedes this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_DOC_GOVERNANCE_FINAL_BUILD.log`
- result: `Build FAILED`, `1 Warning(s)`, `7 Error(s)`
- first blockers: `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs` missing `NextRandomRange`, `NextRandom01`, and `NextRandomRangeInt`; `HectonAnomalyEngine.cs` stale terrain snap field read
- later source/project state changed; `AUTONOMOUS_DOC_GOVERNANCE_FINAL_BUILD_R2.log` superseded this failed gate
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_CONTINUATION_COMPILE_GATE_3.log`
- result: `Build FAILED`, `35 Warning(s)`, `1 Error(s)`
- first blocker: `Assets/_Project/Scripts/Gameplay/DebrisManager.cs(1345,64): EstimateMagnitudeNoSqrt missing from OrganicDebrisProfile scope`
- later source/project state changed; `AUTONOMOUS_CONTINUATION_COMPILE_GATE_4.log` supersedes this as latest completed-build evidence
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_CONTINUATION_COMPILE_GATE_2.log`
- result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`
- superseded by `AUTONOMOUS_CONTINUATION_COMPILE_GATE_3.log` after blueprint-preview job barrier cleanup exposed the debris compile blocker
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_CONTINUATION_COMPILE_GATE_1.log`
- result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`
- superseded by `AUTONOMOUS_CONTINUATION_COMPILE_GATE_2.log` after corpse-sink job barrier cleanup
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_CONTINUATION_BASELINE_BUILD.log`
- result: `Build FAILED`, `0 Warning(s)`, `2 Error(s)`
- first blocker: `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`: `SplineDescriptor` less accessible than `IConnectionSplineBatchRendererService` methods
- later source/project state changed; `AUTONOMOUS_CONTINUATION_COMPILE_GATE_1.log` superseded this first error
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_VOXEL_BARRIER_GATE_2.log`
- result: `Build FAILED`, `0 Warning(s)`, `1 Error(s)`
- first blocker: `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs(1201,37): ResolveVoxelProxySlideVelocity missing`
- later source/project state changed; `AUTONOMOUS_VOXEL_BARRIER_GATE_3.log` supersedes this as latest completed-build evidence
- artifact: `CodexArtifacts/2026-05-08_AUTONOMOUS_VOXEL_BARRIER_GATE_1.log`
- result: `Build FAILED`, `0 Warning(s)`, `1 Error(s)`
- first blocker: `Assets/_Project/Scripts/Interaction/PhysicalInteractionHandler.cs(178,17): VRCableDragPlug missing from Core compile surface`
- later source/project state changed; `AUTONOMOUS_VOXEL_BARRIER_GATE_2.log` superseded this first error
- artifact: `CodexArtifacts/2026-05-08_DOC_SYNC_CORE_BUILD2.log`
- result: `Build FAILED`, `48 Warning(s)`, `1 Error(s)`
- first blocker: `Assets/_Project/Scripts/UI/PDAAtlasSignalTab.cs(95,17): SignalBeaconTelemetry missing`
- later source/project state changed; `DOC_SYNC_CORE_BUILD3.log` supersedes this as latest completed-build evidence
- artifact: `CodexArtifacts/2026-05-08_DOC_SYNC_CORE_BUILD.log`
- result: `Build FAILED`, `48 Warning(s)`, `1 Error(s)`
- first blocker: `Assets/_Project/Scripts/VoxelDeltaProcessor.cs(134,26): LaserCutRadianceDecal missing`
- later source edits changed `VoxelDeltaProcessor.cs`; the second and third builds supersede this first error

## R129 Micro-Audit

Fresh untracked/runtime-surface files sampled after R129:

- `Assets/_Project/Scripts/Gameplay/LifePodTactilePrologueController.cs`
- `Assets/_Project/Scripts/Gameplay/LifePodFireExtinguisherNozzle.cs`
- `Assets/_Project/Scripts/Gameplay/VRSomaticRuntimeBootstrap.cs`
- `Assets/_Project/Scripts/Visor/HectonAtmosphereSootFeature.cs`
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`
- `Assets/_Project/Scripts/Editor/LifePodTactilePrologueSmokeTester.cs`
- `Assets/_Project/Scripts/Editor/SignalCryptographySmokeTester.cs`

Static scan result:

- runtime sampled files: `0` `Update`, `LateUpdate`, `FixedUpdate`, `StartCoroutine`, `yield return`, LINQ `.Where/.Select/.ToList/.Any/.FirstOrDefault`, `Vector3.Distance`, `Mathf.Pow`, or `Mathf.Lerp` hits.
- editor-only smoke files contain `Debug.Log*` and dictionary/string contract tokens; these are under `Assets/_Project/Scripts/Editor` and are not runtime hot-path proof.
- `LifePodTactilePrologueController` uses a cold pre-sized latch list and `SetCharArray`/span-style BIOS text path; no sampled per-frame text assignment was found.
- `Hecton_ScooterVolumetricShafts.shader` static read found existing squared-distance/`rsqrt` paths, fake raymarch cutoff, fixed small loops, and approximate sonar pulse distance; no safe shader edit was made without Unity shader import proof.

## Non-Claims

- Do not claim project-wide player build or Unity import/console cleanliness from the Core build alone.
- Do not claim Unity console cleanliness from the Core dependency graph; R110 `dotnet` build result was `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, and no later script C# writes were observed after the artifact timestamp. Unity import/console proof is still absent.
- Do not claim Play Mode, profiler, GCMonitor, player-build, or memory-retention proof from stale or absent editor-console readback.
- Do not claim runtime zero-GC.
- Do not claim the May 7 `GlobalRegistry.PlayerRigidbody` / `GlobalRegistry.PlayerMovement`, earlier May 8 `LaserCutRadianceDecal`, or earlier May 8 `SignalBeaconTelemetry` failures as current blockers unless a newer build reproduces them.
- Do not claim documentation is permanently synchronized while source files keep changing.

## Regression Model

CPU: documentation edits plus compile-repair edits in predictive voxel proxy compile surface, player-motor/vehicle-motor drift, atmosphere soot renderer feature inclusion, route-distance math, telemetry export callback ownership, and emergency-relay registry residue. No new `Tick`, `FixedTick`, `Update`, or per-frame UI allocation site was introduced by this sync.

GC: no managed allocation was added to the changed hot-path logic. Measured hot-path `0 B/frame` proof absent.

Memory: no scenes, prefabs, textures, Addressables groups, native containers, render textures, project settings, or packages were changed by this report.

Cadence: this continuation did not add new waits, coroutines, or gameplay-loop awaits. No bootstrap, scene transition, asset loading, physics, UI, or rendering cadence was intentionally changed by this report.

Correctness: active documentation now points at the current 2026-05-09 local evidence boundary and records that source churn invalidated May 7/May 8 source-count/build-blocker statements plus R129-R185 snapshots. The latest completed compile gate is `2026-05-09_R186_CORE_FULLGRAPH_SERIAL_NORESTORE_BUILD.log`, which passed with `0 Warning(s)`, `0 Error(s)`, `DOTNET_EXIT_CODE=0`, `CS_WRITES_AFTER_START=0`, and `CS_WRITES_AFTER_END=0`. Unity console and Play Mode proof remain absent.

Status: PENDING FINAL UNITY PROOF (R186 DOTNET BUILD PASSED / UNITY MCP BLOCKED)


