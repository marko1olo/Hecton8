# 2502 Bootstrap Route Readiness Hang Audit

Status: COMPLETE
Agent: 2502
Date: 2026-06-04
Scope: report-only audit. No Unity run, no build, no scene/material/code edits.

## Authority And Evidence

Loaded authority:
- `AGENTS.md`
- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `.agents-skills/CORE_Global_State_Reset_NonReload_Transitions.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `PROJECT_BIBLES.md`
- `bootstrap.md`
- `systems.md`
- `Docs/QUALITY_GATES.md`

Evidence classes used:
- SOURCE: static source inspection.
- SCENE_YAML: serialized Unity scene YAML inspection.
- UNITY_CONSOLE: existing Unity editor log inspection.

Latest route-bearing log used:
- `Docs/AgentLogs/UnityEditor_visual_audit_restart_1474b.log`

`Docs/AgentLogs/UnityEditor_visual_audit_restart_1474.log` is newer by filename family but contains only launch/licensing lines and no usable route sequence.

## Top Findings

1. The first current route blocker is a readiness watchdog timeout after `[GameBootstrapper] Step 8: Runtime World Prime` and before `[GameBootstrapper] Step 8.5: Cold Cleanup + Memory Snapshot`.
   - Evidence: UNITY_CONSOLE `1474b` lines 2208, 2241, 2255, 2307, 2527, 2556.
   - Code owner: `GameBootstrapper.ExecuteSceneReadinessGatesAsync` shared `cts.CancelAfter(TimeSpan.FromSeconds(bootstrapTimeout))` at `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:6715`, Step 8 at `:6759`, `PrimeRuntimeWorldAsync` at `:7159`, scatter calls at `:7168` and `:7174`, timeout fail at `:6800`.
   - Classification: readiness route timeout, not async scene activation.

2. The same latest log later contains a successful route through `[GameBootstrapper] Complete`.
   - Evidence: UNITY_CONSOLE `1474b` lines 3351, 3379, 3393, 3445, 3665, 3694, 3723, 3752, 3781, 3810.
   - It reaches Step 8.5, Step 8.75, Step 8.9, Step 8.95, then Complete.
   - Classification: route can complete under current source and scene data; the first failure is not a permanent scene route impossibility.

3. The return to `00_BOOTSTRAP` after the successful route is correlated with Unity editor asset refresh/domain reload, not normal bootstrap route logic.
   - Evidence: UNITY_CONSOLE `1474b` line 3810 Complete, line 3839 temp backup scene, line 3947 `Reloading assemblies for play mode.`, line 4189 `Asset Pipeline Refresh ... ForceDomainReload`, line 4310 `Loaded scene 'Assets/_Project/Scenes/00_BOOTSTRAP.unity'`.
   - Source route back to bootstrap is only menu start handoff: `MainMenuController.TryRouteStartThroughBootstrap` logs at `Assets/_Project/Scripts/MainMenuController.cs:1397`.
   - Classification: editor reload / asset pipeline side effect, not completed bootstrap handoff owner.

4. Current latest log does not show Aegir null spam.
   - Evidence: UNITY_CONSOLE `1474b` has HectonUnderwaterVisuals manual-add failures after timeout, but no `UpdateAegirMaterial` null exception.
   - Historical evidence: `UnityEditor_visual_audit_restart_1468.log` lines 5603-5608 and repeats show older `ArgumentNullException` from `HectonCelestialEngine.UpdateAegirMaterial`.
   - Classification: historical blocker, not current latest route blocker.

5. `HectonUnderwaterVisuals` ready-lock rejection in `1474b` is post-timeout MCP/manual injection, not a serialized scene activation dependency.
   - Evidence: UNITY_CONSOLE `1474b` lines 2592-2607 include `UnityEngine.GameObject:AddComponent<Hecton8.Environment.HectonUnderwaterVisuals> ()`.
   - SCENE_YAML confirms `02_HECTON_WORLD` already has `HectonUnderwaterVisuals` at `Assets/_Project/Scenes/02_HECTON_WORLD.unity:4625`.
   - Classification: test harness/manual injection issue after route failure, not the first timeout owner.

## Route Gate Matrix

| Gate / Owner Candidate | Code Path | Latest Evidence | Classification |
|---|---|---|---|
| Menu start handoff | `MainMenuController.StartGameWithScene` -> `GameStartContextHolder.SetCurrent` -> `TryRouteStartThroughBootstrap`; log at `Assets/_Project/Scripts/MainMenuController.cs:1397` | `1474b:2208` and `1474b:3351`: `[MainMenuController] Routing start through 00_BOOTSTRAP with pending target scene.` | PASS. Menu routes to bootstrap. |
| Bootstrap consumes pending target | `GameBootstrapper.TryStartCompletedBootstrapHandoff`; log at `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2086` | `1474b:2241` and `1474b:3379`: `[GameBootstrapper] Completed bootstrap handoff loading pending target scene '02_HECTON_WORLD'.` | PASS. Pending target is consumed. |
| Async gameplay scene load | `LoadGameplaySceneFromBootstrapHandoffAsync`; Step 0 at `GameBootstrapper.cs:2981`, Step 0.5 at `:3009` | `1474b:2255` Step 0, `1474b:2307` Step 0.5. Second route repeats at `1474b:3393`, `1474b:3445`. | PASS. Not the current hang owner. |
| Main-menu scene activation watchdog | `LoadMainMenuAsync`; watchdog labels at `GameBootstrapper.cs:2923` and `:2945` | No current `main-menu load` or `main-menu activation` watchdog line in latest route sequence. | NOT EVIDENCED. |
| Critical singleton gate | `VerifySingletons` inside `ExecuteSceneReadinessGatesAsync` | Logs advance past Step 1 in both attempts. | PASS. |
| World-ready queue | `WaitForWorldReadyAsync`; stall warning at `GameBootstrapper.cs:7002` | No current `[GameBootstrapper] World-ready queue stalled. Continuing bootstrap.` before first timeout. Logs advance past Step 5. | PASS or non-owner. |
| Ground-ready | `WaitForGroundReadyAsync`; timeout warning at `GameBootstrapper.cs:7034` | No current `[GameBootstrapper] Ground-ready timed out...` before first timeout. Logs advance past Step 6. | PASS or non-owner. |
| Runtime world prime | `PrimeRuntimeWorldAsync`; Step 8 at `GameBootstrapper.cs:6759`; scatter calls at `:7168`, `:7174` | First route: `1474b:2527` Step 8, then `1474b:2556` timeout. No Step 8.5 between them. | CURRENT FIRST FAILURE OWNER. |
| Cold cleanup and memory snapshot | `RunColdCleanupAndCaptureMemorySnapshotAsync`; Step 8.5 at `GameBootstrapper.cs:6764` | First route never reaches it. Second route reaches `1474b:3694`. | Not first failure. |
| Resident prefab pools | `WaitForResidentWorldPrefabPoolsReadyAsync`; Step 8.75 at `GameBootstrapper.cs:6768`; missing-registry fail at `:7233` | First route never reaches it. Second route reaches `1474b:3723`. | Not first failure. |
| SceneInstantiationGate | `WaitForSceneInstantiationGateAsync`; Step 8.9 at `GameBootstrapper.cs:6773`; failure reasons in `SceneInstantiationGate.cs:134-159` | First route never reaches it. Second route reaches `1474b:3752`. | Not first failure. |
| Scene graph guard | Step 8.95 at `GameBootstrapper.cs` route after gate wait | Second route reaches `1474b:3781`. | PASS in successful attempt. |
| Complete/game ready | `SetSceneActivationStep("Complete")`, `BootstrapState.PublishGameReady(true)`, `PublishBootstrapPresence(false)` | `1474b:3810`: `[GameBootstrapper] Complete` | PASS in successful attempt. |

## Serialized Route Fields

`00_BOOTSTRAP`
- SCENE_YAML: `Assets/_Project/Scenes/00_BOOTSTRAP.unity:5121` contains `BootstrapController` script GUID `37290befeffd3d94796e62b9097c7db9`.
- SCENE_YAML: `:5124`, `:5126`, `:5142` contain shader warmup/catalog fields.
- No serialized `GameBootstrapper` component was found in route-relevant YAML. Runtime bootstrapper defaults are therefore source-owned unless another runtime path injects them.

`01_MAIN_MENU`
- SCENE_YAML: `Assets/_Project/Scenes/01_MAIN_MENU.unity:394` contains `MainMenuController` script GUID `759f3087469a99f40ab0dc8c4a3b6fb3`.
- SCENE_YAML: `:418` `targetSceneName: 02_HECTON_WORLD`.
- SCENE_YAML: `:419` `newGameTargetSceneName: 02_HECTON_WORLD`.

`02_HECTON_WORLD`
- SCENE_YAML: `Assets/_Project/Scenes/02_HECTON_WORLD.unity:4625` contains `HectonUnderwaterVisuals`.
- SCENE_YAML: `:46785` `bootstrapPrimeRadiusCells: 3`.
- SCENE_YAML: Aegir authored refs exist at `:90890` `aegirTransform`, `:90891` `aegirObserverRelativeBody`, `:90892` `aegirRenderer`.
- SCENE_YAML: `:90900` `aegirRingShadowCookie: {fileID: 0}` and `:91287` `_authoredAegirRingShadowCookie: {fileID: 0}`. This is a missing optional cookie reference unless code requires it.

## Boot State File Assessment

Source:
- `GameBootstrapper.cs:125` uses `BootStateFileName = "boot.bin"`.
- `GameBootstrapper.cs:7600` reads previous boot state from `HectonPersistentPathPolicy.CombineFile(BootStateFileName)`.
- If the marker is valid and not `BootStateMarker.Complete`, source requests safe mode via `_bootStateSafeModeRequested = true` and `GlobalRegistry.RequestSafeModeBoot()`.
- `GameBootstrapper.cs:7652` writes the same file path during marker updates.

Latest log classification:
- No latest route evidence proves stale `boot.bin` as the current hang owner.
- A stale marker could alter the next boot profile, but the observed first failure has a stronger direct owner: Step 8 runtime world prime exceeding the shared scene activation timeout.

Minimal distinction test:
- Before a Unity owner reruns the route, record the current `boot.bin` marker and path.
- If marker is not Complete and the next run enters safe mode before menu/world route, stale boot state is an owner.
- If marker is Complete or absent and the next run still stops at Step 8, stale boot state is not the owner.

## Leak Warning Assessment

Latest log:
- `1474b:3035` and `1474b:4012`: `Leak Detected : Persistent allocates 4 individual allocations.`
- Stack in the latest log points to `WeatherEvents.EnsureInitialized()` from `HectonCelestialEngine.OnEnable()`.
- These warnings occur around domain reload/assembly reload windows, not at the first Step 8 timeout stack.

Classification:
- Real defect, separate owner.
- Not proven as bootstrap fatal in the current route evidence.
- Do not mark it fixed by route completion; assign to celestial/weather native allocation ownership separately.

## Minimal Unity Owner Test Order

1. Start from a clean editor idle state: no asset import, no script compile, no forced domain reload in progress.
2. Enter Play, route from `01_MAIN_MENU` using the normal start button. Capture only console lines containing `[MainMenuController]`, `[GameBootstrapper]`, `SceneInstantiationGate`, `Loaded scene`, `Reloading assemblies`, `Asset Pipeline Refresh`, `Leak Detected`.
3. If the last bootstrap step before failure is `Step 8: Runtime World Prime`, inspect `WorldProceduralScatterDirector.TryPrewarmBootstrapSamplingPipeline()` and `TryPrimeBootstrapScatterPass()` cost/hang behavior. This is the current first-failure branch.
4. If the last step is `Step 8.75: Resident World Prefab Gate`, inspect `GlobalRegistry.PersistentWorldRegistry.AreResidentWorldPrefabPoolsReady()` and resident prefab pool contents. The source loop has no inner timeout except the outer bootstrap timeout.
5. If the last step is `Step 8.9: Scene Gate Verification`, read `SceneInstantiationGate.ActiveRuntime.LastFailureReason`. Valid code reasons are `WORLD_PRIME_PENDING`, `PLAYER_INSTANTIATION_PENDING`, `MEMORY_SNAPSHOT_PENDING`, `PRESSURE_SAMPLE_PENDING`, and `VRAM_GATE_REJECT`.
6. If `[GameBootstrapper] Complete` appears and the editor later loads `00_BOOTSTRAP`, check for `Reloading assemblies for play mode` or `Asset Pipeline Refresh ... ForceDomainReload` between Complete and the load. If present, classify as editor reload, not bootstrap logic.
7. If `main-menu load` or `main-menu activation` watchdog appears, switch investigation to `LoadMainMenuAsync`; this did not happen in the latest route-bearing log.
8. If Aegir null spam reappears in a fresh latest log, treat it as a current celestial visual blocker. In `1474b`, it is not current.

## Scalability Consequences

No runtime algorithm changed in this audit.

Low tier:
- Existing fixed `bootstrapTimeout` remains the governing readiness watchdog. If runtime world prime does not scale down internally, low tier will hit the Step 8 timeout first.

Middle tier:
- Expected to pass only if scatter bootstrap prime finishes inside the shared 30 second activation timeout. Current evidence shows one fail and one pass.

High tier:
- Successful route is possible, but editor import/domain reload can still reset the observed route after Complete during proof capture.

Ultra tier:
- Extra performance does not protect against forced editor domain reload or stale boot state. It only reduces Step 8 timing risk if scatter prime cost is CPU-bound.

## Final Classification

Current first hang owner:
- `GameBootstrapper` readiness timeout during Step 8 runtime world prime, probably inside scatter bootstrap prime path. Evidence is direct and current.

Current "returns to bootstrap after complete" owner:
- Unity editor asset refresh/domain reload after successful route. Evidence is direct and current.

Not current first owners:
- Async scene activation.
- Main-menu activation.
- Aegir null spam.
- Resident prefab pool gate.
- SceneInstantiationGate.
- Leak warning fatality.
- Serialized route target mismatch.
- Stale boot state, unless the next run proves a non-Complete `boot.bin` marker changes boot mode.
