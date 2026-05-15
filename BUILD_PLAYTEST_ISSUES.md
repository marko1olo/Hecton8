# HECTON-8 — BUILD / PLAYTEST ISSUES LEDGER

Status: `PENDING VERIFICATION`
Ledger Start Date: `2026-04-05`

## Purpose

This file tracks confirmed build and playtest observations.

2026-05-15 current-state boundary:

- Read `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md` before using this ledger for current project truth. May 11 reports remain historical where the May 13 DOC_AUDIT override conflicts.
- This ledger records build/playtest observations and coding follow-ups; it is not a global runtime certification report.
- Items marked `[c]` are code-closed only until build/user proof confirms them.
- If this file disagrees with current source, console, profiler, or fresh user evidence, the newer evidence wins.
- 2026-05-13 DOC_AUDIT filesystem check did not find `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt` or `.log`. Treat the May 11 compile-success line as stale report text until the artifact is restored or replaced. It is not current build proof and not player-build, Play Mode, Unity Console, profiler, GCMonitor, scene-wiring, visual-quality, or user-playtest proof.
- 2026-05-15 current-disk proof: latest observed `Docs/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_224641_CurrentDisk53.log` reports `Hecton8.Core.csproj` CLI compile `EXIT=0`, `Build succeeded`, `0 Warning(s)`, `0 Error(s)`; latest observed `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_224426_CurrentDiskBudgetGate22.json` reports H-Phi static budget `EXIT=0` with `GlobalRegistrySurface=5060/5060`. These are not player-build, live run, Play Mode, profiler, user-playtest, or visual acceptance evidence.
- Current visual-realism doctrine is visual fake first; do not log simulation work as accepted without gameplay-correctness need and profiler/GC/memory proof.

Rules:

- Only log real observations from builds, live runs, or manual playtests
- Do not log abstract ideas here
- Do not mark anything fully solved without new evidence
- Every item remains `PENDING VERIFICATION` until a new build or user check confirms the fix
- Player build is the main arbiter, not editor feel
- Use `[c]` for code-fixed issues that are closed for current coding work but still await build or user confirmation
- Do not reopen `[c]` issues for new coding work unless new logs, build evidence, or user verification proves the fix is incomplete or regressed
- Use `[x]` only after new proof from build, live run, or explicit user confirmation

## Entry Template

```md
## Build Entry — YYYY-MM-DD — Build Name / Version
- Build Size:
- Scene:
- Hardware:
- General Feel:
- Main Irritant:
- Main Visual Flaw:
- Main UX Flaw:
- Main Content Gap:
- New Blocker: yes / no

### [ ] Issue Name
- Status: [ ] / [~] / [c] / [x] / [!] / [?]
- Need User Check: yes / no
- Need Build Check: yes / no
- Need In-World Swim Check: yes / no
- Why:
- Evidence:
- Problems:
- Short Comment:
- Next Step:

- Did:
- Result:
- Failed:
- Broke:
- Remaining:
```

## Build Entry — 2026-04-05 — User Build Report
- Build Size: `~500 MB`
- Scene: `02_HECTON_WORLD`
- Hardware: `MX350 target context`
- General Feel: smoother than editor; underwater base feel already promising even before real content fill
- Main Irritant: surfacing hitch and broken oxygen refill
- Main Visual Flaw: gas giant depth illusion and blurry terrain/rocks in close-up
- Main UX Flaw: pause cursor missing; pause buttons not yet fully audited
- Main Content Gap: underwater world still lacks full life, caves, ruins, and density layers
- New Blocker: `yes`

### [c] Surface Transition Hitch
- Status: [c]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: this is an immediate feel-breaker during normal play
- Evidence: user report says the game can hitch when moving from underwater to above water while turning the camera
- Problems: editor is not reliable as final truth because build is smoother overall
- Short Comment: code fix accepted; closed for current coding work, waiting for build proof
- Next Step: build swim verification while rotating camera across the surface

- Did: diagnosed live runtime mismatch at the waterline (`Atmosphere=UNDERWATER` while `Visuals=false`, `Movement=false`, `Survival depth≈0`) and replaced it with one shared hysteresis contract based on `HectonPlayerMovement.CurrentDepth` for atmosphere, underwater visuals, and survival.
- Result: editor runtime no longer splits state at the surface boundary; current readback keeps `Atmosphere=surface`, `Visuals=false`, `Survival depth≈0.0049` on the same near-surface frame instead of contradictory surface/underwater states.
- Failed: build verification not run yet; could not force a scripted underwater transition sweep because Unity MCP runtime code execution fails on this machine (`mono.exe: filename or extension is too long`).
- Broke: no compile errors from the patch; console still shows unrelated warnings from `Dynamic Decals` and one generic `Leak Detected : Persistent allocates 8 individual allocations` warning after recompilation.
- Remaining: real swim test in player build while rotating the camera across the surface; confirm hitch is gone under build timing, not just editor runtime. Closed for coding unless new evidence reopens it.

### [c] Surface Oxygen Refill Missing
- Status: [c]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: survival trust collapses if surface safety does not work
- Evidence: user report says oxygen does not refill correctly when surfacing
- Problems: likely tied to surface-state truth and crossing logic
- Short Comment: code fix accepted; closed for current coding work, waiting for build proof
- Next Step: build swim verification with depleted oxygen and natural surfacing

- Did: survival oxygen flow now uses the same shared surface hysteresis contract and explicit surface refill path instead of unconditional underwater-style drain.
- Result: refill logic is now present in gameplay code and bound to the same surface truth used by atmosphere and visuals; near-surface runtime readback holds the player in surface state instead of flickering underwater.
- Failed: direct refill proof is still missing because automated oxygen field manipulation could not be executed through Unity MCP on this machine.
- Broke: no compile errors observed from the survival change.
- Remaining: lower oxygen during live play, surface naturally, and confirm refill resumes immediately in build and during in-world swim. Closed for coding unless new evidence reopens it.

### [c] Pause Cursor Missing / Pause Button Audit Needed
- Status: [c]
- Need User Check: yes
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: broken pause flow makes the game feel unfinished immediately
- Evidence: user report says `Esc` pause opens without a visible cursor and all buttons need checking
- Problems: input restore and menu state may still be fragile
- Short Comment: product shell issue, not optional polish
- Next Step: verify cursor, input map switching, `Esc` flow, and all button actions in build

- Did: traced the pause ownership conflict and added one shared pause truth in `PauseMenuController`, then switched `HectonPlayerMovement`, `PlayerInteraction`, and `PlayerFlashlight` to block gameplay/cursor reclaim while pause is open instead of only checking PDA/fabricator state.
- Result: the direct code-level conflict is removed; pause now has an explicit fail-safe gameplay block even if UI action-map switching degrades, and Unity recompilation completed with no new errors from the patch.
- Did Addendum: traced the remaining button-audit gap to deterministic focus, not only cursor visibility. `PauseMenuController` was opening sections with no explicit `EventSystem` selection target, so keyboard/gamepad audit could start from `null` selection and behave inconsistently by section. The controller now caches the default button for each pause section, selects it on `Open()` / section switch, and clears stale pause selection on close instead of leaving `EventSystem.currentSelectedGameObject` pointing into hidden pause UI.
- Result Addendum: pause section navigation now has a concrete selection anchor for `Main`, `Saves`, `Help`, and `Settings` instead of relying on implicit `EventSystem` state. Unity recompilation after this pass completed with no new `CS` errors; console still reports only the pre-existing `Dynamic Decals` warnings.
- Did Addendum: traced one more shell-state leak outside cursor/focus itself. `PauseMenuController.ExitToMainMenu()` was loading `01_MAIN_MENU` directly without clearing `GameStartContextHolder`, so the menu shell could inherit stale `LoadGame/Resume` handoff from the active world session. The exit path now calls `GameStartContextHolder.Reset()` before loading the main menu scene.
- Result Addendum: leaving gameplay through pause now clears both in-memory and cold-persisted start-session handoff state before returning to shell, so menu/bootstrap no longer see old save-slot context just because the previous session exited through pause.
- Failed: live cursor-state proof is still incomplete on this machine because Unity MCP `execute_code` still fails with `mono.exe: filename or extension is too long`, and the existing `UIRuntimeSmokeTester` stalled after `PASS PDA open Inventory` before producing a pause result.
- Broke: no new compile errors; console now shows only the old `CS0414` warnings in `SceneBootstrap`, `StateRecoveryVerifier`, `SceneTransitionVerifier`, plus transient MCP serializer warnings from the abandoned smoke attempt.
- Failed Addendum: attempted short MCP play-mode readback again after the focus patch, but `execute_code` still hard-fails on this machine with the same `mono.exe: filename or extension is too long`, so direct editor-side proof of the selected pause button is still blocked.
- Remaining: real runtime check of `Cursor.visible`, `Cursor.lockState`, `Esc` open/close, initial pause-button focus, every pause button action in build, and explicit proof that `Exit to Main Menu` now returns to a clean shell without stale session context; current status remains `PENDING VERIFICATION`.

### [~] Surface Audio Contract Not Yet Proven
- Status: [~]
- Need User Check: yes
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: shoreline and surfacing feel fake immediately if audio truth diverges from atmosphere/survival/visuals
- Evidence: code audit showed `AcousticZoneController` was reading `BuoyancyObject.IsInAir`, which conflates dry interiors with grounded shoreline; `MasterMixer.mixer` currently still contains only one default snapshot
- Problems: shoreline could be misread as `interior`, surface/open-air had no explicit zone path, and mixer-authored runtime truth is still unproven
- Short Comment: code contract improved; runtime truth still not established
- Next Step: wire real snapshot assets and live scene usage, then verify `underwater -> surface -> interior` transitions in build

- Did: added additive read-only `BuoyancyObject.IsInDryZone` so audio can distinguish unflooded module interiors from grounded terrain without changing fluid-engine `IsInAir` semantics. Reworked `AcousticZoneController` to resolve three acoustic zones (`interior / surface / underwater`), prefer `HectonAtmosphereManager.CurrentState`, fallback to the same `SurfaceStateUtility` hysteresis on player depth if atmosphere is unavailable, auto-bootstrap itself at runtime if no authored instance exists, and lazily bind the player's existing 2D looping underwater ambient source so surfacing/interior states can mute it from the same zone truth.
- Result: the audio code path is no longer forced to treat grounded shoreline as a dry base interior, it now has a surface/open-air branch instead of collapsing every non-interior state into `underwater`, and it no longer depends on a scene-placed controller just to exist at runtime.
- Failed: this remains `PENDING VERIFICATION` because `MasterMixer.mixer` still exposes only one default snapshot asset, automated play-mode proof is still blocked by the flaky MCP runtime path on this machine, and the player-loop heuristic has not yet been proven in build on the real traversal route.
- Broke: Unity recompilation completed with no new console errors or warnings after the patch.
- Remaining: assign real `surface / underwater / interior` snapshots, verify that the runtime bootstrap spawns exactly one controller, and run shoreline, surfacing, and dry-module transitions in build to prove the correct player ambient source is being muted/unmuted.

### [c] Game Start Context Handoff Ownership Raised, In-Game Verification Still Pending
- Status: [c]
- Need User Check: yes
- Need Build Check: yes
- Need Menu Flow Check: yes
- Why: start flow still had two sources of truth; `GameStartContext` existed, but `MainMenuController` and `SceneBootstrap` were still depending on legacy `TargetSaveSlot + PlayerPrefs` fallback
- Evidence: `MainMenuController.StartGame()` was writing both `GameStartContextHolder.Current` and `PlayerPrefs["TargetSaveSlot"]`, while `SceneBootstrap.LoadOrNewGameAsync()` rebuilt context from that single slot key when the holder was empty
- Problems: stale slot state could leak across unrelated loads, and the declared owner (`GameStartContext`) was not the real source of truth during menu -> world handoff
- Short Comment: ownership fixed; menu/build proof still missing

- Did: moved cold scene-handoff persistence into `GameStartContextHolder` itself. Added `SetCurrent(...)`, `TryGetCurrentOrRestore(...)`, and `ClearPersistedHandoff()` so the holder now owns both in-memory transfer and cold recovery for domain-reload-style menu -> world transit. `MainMenuController.StartGame()` now writes through `GameStartContextHolder.SetCurrent(...)` instead of writing its own `PlayerPrefs` key, and `SceneBootstrap.LoadOrNewGameAsync()` now restores through `TryGetCurrentOrRestore(...)`, clears the persisted handoff immediately after consuming it, and republishes the resolved runtime context back into `GameStartContextHolder.Current` for the rest of the session. Follow-up pass removed deprecated `MainMenuController.TargetSaveSlot`, so there is no second menu-side start-slot mirror left.
- Did Addendum: `MainMenuController.StartGame()` now also fail-closes repeated menu-confirm / button-spam calls with a dedicated `_isSceneLoadInFlight` guard, and resets that guard only if `SceneManager.LoadSceneAsync(...)` fails immediately. This closes the remaining open-gate where one menu transition could start multiple `LoadSceneRoutine()` coroutines.
- Did Addendum: `MainMenuController.OpenSaveLoadMenu()` no longer destroys and reinstantiates the whole save-slot panel on every open. The menu now builds one fixed `SaveSlotUI[]` shell once, validates the prefab once, and only refreshes slot data on later opens. This removes repeated `Destroy/Instantiate` churn from the production shell path.
- Did Addendum: `SaveSlotUI.Init(...)` no longer calls `RemoveAllListeners()/AddListener()` on every slot refresh. Listener binding now happens once in `Awake()`, and refresh updates only cached slot data plus button `interactable` state.
- Did Addendum: shell verifiers now honor their own inspector toggles. `StateRecoveryVerifier._enableVerification` and `SceneTransitionVerifier._verifyTransitions` are used as real gates before launching verification coroutines, so these fields are no longer dead config noise.
- Result: the start-flow owner is no longer split between `GameStartContext` and a hidden single-slot `PlayerPrefs` backdoor. Bootstrap now reads one owner path, stale persisted slot state is explicitly cleared after handoff, and menu/bootstrap no longer each invent their own fallback contract.
- Result Addendum: menu -> world shell transition is no longer open to repeated `StartGame()` launches during modal/button spam, repeated save/load panel opens no longer churn UI objects, and slot refresh no longer rebinds button listeners each visit.
- Result Addendum: start-session ownership on the menu side is now single-owner only (`GameStartContextHolder`), without deprecated static slot mirrors.
- Did Addendum: audited shell truth instead of trusting stale docs. `ProjectSettings/EditorBuildSettings.asset` and Unity MCP both confirm the production build order is already `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`. The real break was different: live `01_MAIN_MENU` had `MainMenuController` but no `SceneGuard`, so direct-entry protection depended on scene authoring. Added `BootstrapRouteEnforcer` and wired it into `MainMenuController.Awake()` and `SceneBootstrap.Awake()` so direct runtime entry into `01_MAIN_MENU` or `02_HECTON_WORLD` now fail-closes back to `00_BOOTSTRAP` even if the scene component is absent.
- Result Addendum: Build Settings are already correct, and bootstrap route protection no longer relies only on authored scene content.
- Failed: this remains `PENDING VERIFICATION` because I do not have a fresh menu -> new game -> load game -> return-to-menu runtime route on this machine after the pass.
- Failed Addendum: compile ready-state verification for the mirror-removal pass is still unstable (`refresh_unity(wait_for_ready=true)` times out), but console readback recovered and currently shows no new compile `CS` errors for this edit.
- Broke: no new break is known from static code audit; runtime/compile oracle is blocked until Unity session recovers.
- Remaining: verify `New Game`, `Load Game`, and `Resume` still enter `02_HECTON_WORLD` with the correct `GameStartContext`, verify direct `01_MAIN_MENU` / `02_HECTON_WORLD` misuse still routes through bootstrap correctly, and confirm no hidden dependency still expects removed `MainMenuController.TargetSaveSlot`.

### [c] Geology Terrain Seam Runtime GC Hardening Not Yet Proven
- Status: [c]
- Need User Check: yes
- Need Build Check: yes
- Need Profiler Check: yes
- Why: cave/geology seam code is live runtime architecture now; guaranteed managed churn inside `SlowTick` is unacceptable before any world-truth pass
- Evidence: `WorldGenerativeGeologyTerrainSeamApplier` was creating new terrain plan lists and new `float[,]` baseline patches during repeated seam reconcile / restore work
- Problems: even before visual proof, terrain seam runtime was structurally guaranteed to allocate while active seam plans were being maintained
- Short Comment: runtime seam path hardened; world proof still missing

- Did: rewired `WorldGenerativeGeologyTerrainSeamApplier` to reuse per-terrain plan buckets instead of recreating `List<WorldGenerativeGeologySeamPlan>` every reconcile, replaced per-apply and per-restore `new float[,]` patch creation with reusable per-terrain patch buffers, removed dictionary `foreach` from the active path in favour of indexed terrain-id iteration, routed terrain lookup through `MapMagicBridge` tile truth before the fallback `Terrain.activeTerrains` scan, refreshed the full terrain baseline snapshot whenever the bound `Terrain` / `TerrainData` owner or heightmap resolution changes, made stale restore paths drop and refresh state fail-safe if streamed `TerrainData` changes while the terrain is no longer touched, and dropped the live `terrain.name` diagnostic string from `ReconcileTerrainSeams()` in favour of primitive terrain-id tracking.
- Result: terrain seam application no longer guarantees managed list/patch churn on every `SlowTick`, seam lookup is aligned with the same MapMagic tile-backed terrain truth used by biome/height sampling, and stale baseline restore state is dropped fail-safe when terrain streaming swaps the underlying `TerrainData`.
- Failed: this remains `PENDING VERIFICATION` because no GC numbers were captured, no play/build seam traversal pass was run, and Unity still reported unrelated old `Dynamic Decals` obsolete warnings plus one generic `Persistent allocates 5 individual allocations` leak warning without a stack trace.
- Broke: no compile errors were introduced by the seam hardening pass.
- Remaining: profile active cave/geology traversal with live seam plans, verify whether seam footprint resizing still causes objectionable alloc spikes, and prove terrain -> geology -> voxel entrance continuity in build.

### [c] Cave Spawn Lifecycle Hardening Not Yet Proven
- Status: [c]
- Need User Check: yes
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: `WorldCaveDirector` sits on the live cave pipeline; duplicate or stale cave spawn state turns cave generation into nondeterministic clutter and teardown leaks before any readability pass even starts
- Evidence: code audit showed `WorldCaveDirector` was launching cave generation through `async void TrySpawnCaveAt(...)` and only adding the cave key to the active registry after `await voxelEngine.GenerateVolumeAsync(...)` returned
- Problems: the same cave key could be launched multiple times across repeated `SlowTick` passes while generation was still in flight, disable/reenable could leave stale pending ownership behind, and destroyed cave volumes could leave dead keys in the runtime registry
- Short Comment: cave spawn ownership hardened; world proof still missing

- Did: replaced the fire-and-forget `async void` cave spawn path with explicit pending-spawn ownership inside `WorldCaveDirector`, added per-key pending registry plus lifetime cancellation, cancel-on-disable teardown, stale cave-instance cleanup for missing voxel volumes, and a null-safe `TryGetCaveAt` fail-safe. Also moved the cave generation logs behind development/editor-only conditional methods so this gameplay path does not force string-building in release builds.
- Result: the live cave path no longer depends on “not active until the await finishes” semantics. Duplicate cave launches for one runtime key are blocked while generation is pending, teardown can cancel in-flight requests instead of leaving orphaned ownership behind, and dead cave-volume references stop poisoning the active-cave registry.
- Failed: this remains `PENDING VERIFICATION` because no runtime cave traversal or build pass was executed, no duplicate-spawn reproduction was captured before/after, and the machine-side MCP runtime path is still not proving actual in-world cave generation behavior.
- Broke: Unity recompilation completed with no new console errors; console still shows only the unrelated `Dynamic Decals` obsolete warnings.
- Remaining: verify in build that one cave cell produces one live cave volume under repeated `SlowTick` passes, confirm disable/reenable or scene reload does not leave blocked pending keys, and check that entrance cues/dressing are not duplicated on revisit.

 - Addendum: `CleanupUnsupportedCaves()` now actually cancels pending cave spawns and tears down tracked cave volumes when the current biome no longer supports caves, stale-lifecycle cleanup uses non-despawning registration removal so dead pooled owners do not trigger accidental teardown, and pending-cave cancellation now runs through a buffered key list instead of mutating the dictionary during enumeration.

### [c] Voxel Bridge Hot-Path Iteration Compliance Not Yet Proven
- Status: [c]
- Need User Check: no
- Need Build Check: yes
- Need Profiler Check: yes
- Why: `WorldGenerativeGeologyVoxelBridgeDirector` is live runtime glue between seam execution and voxel generation; its reconcile path already sits on `SlowTick`, so hot-path rule violations there are systemic debt, not style
- Evidence: code audit showed dictionary/hashset `foreach` still present in `ReconcileVoxelRequests()`, `CancelStalePendingRequests()`, and `ClearAllVolumes()`
- Problems: even if current runtime GC impact is small or zero on this CLR, the bridge was still violating the project iteration rule in the exact path that owns active voxel retention and pending-request cancellation
- Short Comment: hot-path compliance tightened; runtime proof still missing

- Did: replaced live `foreach` scans in `WorldGenerativeGeologyVoxelBridgeDirector` with explicit generic enumerator loops for active-volume retention, pending-runtime retention, active-volume removal selection, stale pending cancellation, full pending cancellation, and clear-all volume teardown. Follow-up pass: added an owner cache for active `WorldGenerativeGeologyVoxelRuntime` instances keyed by `runtimeKey`, so `ResolveRequestBuildSettings()` no longer performs per-request `TryGetComponent` during reconcile; the same pass also removed the `"None"` sentinel from `_debugTopVolume` so the live owner no longer depends on string-sentinel checks for top-volume diagnostics. Latest addendum: runtime-diagnostics trace formatting is now isolated behind development/editor-only helper methods, so `ReconcileVoxelRequests()`, launch flushing, and request completion/cancel/fault bodies no longer build trace strings directly in the release hot path. New ownership addendum: reconcile now trims stale `_activeVolumes/_activeRuntimes/_activeSignatures` entries before retention logic, `ShouldRetainActiveVolume()` and signature short-circuiting only trust live owners whose active GameObject, cached runtime component, `RuntimeKey`, and `RequestSignature` still match, and stale registration cleanup now forgets dead/reused pooled owners without blindly despawning a volume that may already belong to a new runtime key.
- Result: the voxel bridge no longer relies on banned dictionary/hashset `foreach` in its `SlowTick` reconcile/cleanup path, active request-resolution no longer needs a component lookup just to read previous detail-band / collider hysteresis state, release/runtime hot paths no longer carry diagnostics string interpolation debt just because trace support exists, and stale pooled/reused voxel volumes are less likely to poison retention logic or block a required respawn because an old key still looks “already tracked”.
- Failed: this remains `PENDING VERIFICATION` because no profiler capture was taken on a live seam/voxel traversal route, and no build run has yet proven that the bridge still behaves correctly under real cave/geology request churn.
- Broke: after this pass and a separate compile-hygiene cleanup in stale verifier/editor helpers, Unity recompilation is back to warning-only state; console currently reports only the old `Dynamic Decals` obsolete editor warnings.
- Remaining: capture profiler on live seam request churn, verify pending-request cancellation still clears correctly when requests fall out of range, and confirm no retention/removal regressions in build.

### [c] Seam Planning / Execution Hot-Path Hygiene Not Yet Proven
- Status: [c]
- Need User Check: no
- Need Build Check: yes
- Need Profiler Check: yes
- Why: geology seam planning and seam execution both run as live runtime owners; `SlowTick` diagnostics and trim passes cannot keep string formatting or banned dictionary iteration debt
- Evidence: `WorldGenerativeGeologyIntegrationDirector.RebuildIntegrationPlans()` was formatting `_debugTopPlan` with string interpolation, `TrimPlanDictionaries()` still used dictionary `foreach`, and `WorldGenerativeGeologySeamExecutionDirector.ReconcileExecutedSeams()` / `TryApplyRuntimeKey()` were formatting `_debugTopExecuted` the same way
- Problems: even when the gameplay result looked correct, these directors were still violating the project hot-path rules in the exact planning/execution path that decides seam retention and active execution
- Short Comment: planning/execution hygiene tightened; runtime proof still missing

- Did: replaced `TrimPlanDictionaries()` dictionary `foreach` scans with explicit generic enumerators, converted both directors' `top plan` diagnostics away from interpolated strings to direct field capture (`familyId` reference + archetype enum), moved `WorldProceduralProxyInstance` ownership lookup out of `WorldGenerativeGeologyIntegrationDirector.TryBuildPlan()` into a cached reference on `WorldGenerativeGeologyBinding`, and then hardened seam execution itself: `WorldGenerativeGeologySeamExecutionDirector` now caches `runtimeKey -> WorldGenerativeGeologySeamRuntime` ownership instead of doing `GetComponent` on the seam root during active reconcile, while `TerrainSkirt_*`, `VoxelCollar_*`, and `Debris_*` names now come from cold cached string arrays instead of new interpolated names on each seam rebuild. Latest addendum: seam-runtime cache reuse now rejects stale aliases whose cached runtime has already been reconfigured to a different `RuntimeKey`, stale cache trim now removes those alias entries instead of only clearing literal `null` references, `WorldGenerativeGeologyBinding` now trims stale disabled/null entries out of its static active-binding registry while refreshing proxy cache during `Configure(...)` so planner-side scans do not keep walking dead bindings or stale proxy references, and `WorldGenerativeGeologySeamRuntime` now trims stale disabled/null entries out of its static active-runtime list before seam-cleanup scans so execution-side retention does not keep paying for dead runtime registrations.
- Result: the seam planning/execution stack now has one less steady-state allocation source, one less banned iteration pattern, one less hot-path component lookup, and one less active rebuild string churn source in the runtime path. Diagnostics remain readable in the inspector without paying formatted-string churn every reconcile, proxy metadata now comes from binding-side cache instead of planner-side searches, seam runtime reuse no longer depends on repeated seam-root `GetComponent` probes, runtime-key cache truth is less likely to drift after pooled/reused host reconfiguration, and both integration-side binding scans plus execution-side runtime cleanup are less likely to waste work on dead registry entries.
- Failed: this remains `PENDING VERIFICATION` because no profiler capture was taken on active seam planning/execution churn, and no build run has yet proven that retain/trim/apply behavior stays clean under real cave/geology traversal.
- Broke: after removing two unrelated compile blockers (`BootstrapArchitectureValidator` dead `UnityEditor.SceneHierarchy` import and `BootstrapController` missing `using Hecton8.Input;`), Unity recompilation now completes with no errors; console currently shows only one local `SceneBootstrap.saveSlot` unused-field warning and the old `Dynamic Decals` obsolete editor warnings.
- Remaining: profile a live seam route, confirm no alloc spikes remain in integration/execution reconcile, and verify in build that retained seam execution still behaves correctly across entering/leaving geology zones.

### [c] Cave Sediment Shelf Runtime Layer Not Yet Proven
- Status: [c]
- Need User Check: yes
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: cave interior detail was still partially fake; `sediment shelves` existed in design/config data but the live runtime owner had a literal placeholder instead of a shelf system
- Evidence: `WorldCaveDirector.SpawnSedimentShelves(...)` was empty, while `CaveDressingConfig.GetConfigForContext()` was also allocating a fresh config object graph on every cave spawn despite claiming zero-runtime-allocation intent
- Problems: caves could never realize configured sediment shelves at runtime, and cave dressing configs were paying avoidable managed allocations every time a cave initialized its dressing layer
- Short Comment: shelf layer is now real runtime code; visual proof still missing

- Did: added `CaveSedimentShelfRuntimeBuilder` as the focused runtime owner for cheap sediment-shelf geometry, using deterministic shelf placement inside voxel-volume bounds, shared primitive meshes through `WorldGeneratedPrimitiveFactory`, and per-renderer property blocks for tint/opacity instead of material cloning. Rewired `WorldCaveDirector` to call that builder, removed the old `shelfPrefab != null` gate so the shelf layer can exist without authored prefab dependence, and now writes `caveKey / generationPosition / preset` into `HectonVoxelVolume` for downstream runtime consumers. Also changed `CaveDressingConfig.GetConfigForContext()` to return shared cached templates instead of constructing a new config graph for every cave spawn. Follow-up pass: added shared `CaveRuntimeBoundsUtility` so both shelves and deep-fungi use the same local volume-bounds truth, reworked deep-fungi emission to derive center, size, particle count, and emission rate from live cave bounds plus `DeepFungiConfig.verticalBias` instead of the old hardcoded `10x10x10` placeholder cloud, and wired `wall growth` into live runtime through `CaveWallGrowthRuntimeBuilder`. The wall-growth layer now creates deterministic cheap capsule growths on walls/ceiling from cave bounds and config color/sway/pulse data instead of leaving `WallGrowthConfig` as dead data. Also fixed cave-dressing root ownership so repeated dressing init reuses one `_CaveDressing` root per volume instead of stacking duplicates. Final addendum in this pass: `WorldCaveDirector` now serves biome cave presets from shared read-only templates, so repeated cave spawns stop rebuilding identical `CavePreset` objects and identical `allowedStructureTypes` arrays every time the same biome archetype is requested. Next addendum: introduced real config/runtime owners for `glowing tissue` and `service remnants`, and then closed the pooled-volume hygiene hole beneath all of this by making `HectonVoxelVolume` reset cave-owned runtime roots on reuse while entrance markers / entrance quality reuse named roots instead of spawning stale duplicate child graphs on pooled cave volumes.
- Result: `sediment shelves` are no longer a dead checklist bullet, deep-fungi emission no longer floats as a fixed generic box unrelated to cave size, `wall growth` is no longer config-only fiction, `glowing tissue` and `service remnants` now exist as live cave-detail layers, biome cave preset selection no longer pays repeated template-construction churn, and pooled cave volumes are less likely to leak old readability/detail state into the next spawn. The cave dressing/runtime setup path now has less fake data, less avoidable allocation churn, and less stale pooled state than before.
- Failed: this remains `PENDING VERIFICATION` because no build/swim pass has proven actual shelf readability or placement quality inside generated caves, and no profiler numbers were captured on repeated cave initialization after the config-cache change.
- Broke: Unity recompilation completed with no new compile errors; console still shows only the unrelated `Dynamic Decals` obsolete warnings.
- Remaining: verify that shelves, wall growth, glowing tissue, fungi, and service remnants all read correctly in live cave geometry, confirm pooled cave volumes do not resurrect stale entrance/detail children on reuse, and prove that the full cave-detail stack still looks intentional rather than noisy in build.

### [c] Cave Biome Runtime Classification Hygiene Not Yet Proven
- Status: [c]
- Need User Check: no
- Need Build Check: yes
- Need Profiler Check: yes
- Why: `WorldCaveDirector` is a live `SlowTick` owner and was repeatedly reparsing the same biome string contract across cave-support gating, preset selection, candidate generation seed, cave-key generation, and diagnostics
- Evidence: `EvaluateCaveSpawns()` called `EvaluateBiomeCaveSupport(biomeFamily)`, `GenerateCaveCandidates()` recomputed `biomeFamily.familyId.GetHashCode()`, `GetCavePresetForBiome()` reparsed the same biome string into `CaveBiomePresetKind`, `GenerateCaveKey()` hashed the family again, and diagnostics also re-read current biome labels every evaluation pass
- Problems: this was not a catastrophic alloc source, but it was still repeated string-driven classification debt in the live cave owner instead of one cached runtime contract
- Short Comment: cave biome classification centralized; world proof still missing

- Did: added a cached biome runtime context inside `WorldCaveDirector` that stores the active family reference, `familyId`, label, deterministic hash, `supports caves`, and resolved `CaveBiomePresetKind`. `EvaluateCaveSpawns()`, `GenerateCaveCandidates()`, `GetCavePresetForBiome()`, `GenerateCaveKey()`, and diagnostics now read that cached context instead of reparsing `familyId` independently each pass.
- Result: the live cave owner now has one biome-truth path instead of five partial ones. Cave support, preset selection, candidate seeding, key generation, and diagnostics are driven from the same cached classification state, which reduces repeated string work and makes future cave-biome behavior easier to harden without chasing duplicate logic.
- Failed: this remains `PENDING VERIFICATION` because no profiler capture or build traversal has proven any measurable gain, and no in-world cave route has yet verified that biome transitions still spawn the correct cave archetypes under real streaming conditions.
- Broke: Unity recompilation completed with no new errors after the pass; console currently reports only the old `Dynamic Decals` obsolete warnings plus local non-blocking unused-field warnings in tool/bootstrap scripts.
- Remaining: verify cave behavior when crossing between cliff/canyon/abyss family boundaries in build, confirm cave keys stay stable under biome transitions, and profile whether `WorldCaveDirector` still shows any meaningful managed churn on active exploration routes.

### [c] Cave Runtime Ownership Cleanup Not Yet Proven
- Status: [c]
- Need User Check: yes
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: `WorldCaveDirector` owns live cave registry truth, but its lifecycle cleanup only removed caves when `instance.volume == null`
- Evidence: `HectonVoxelVolume.PrepareForReuse()` resets `caveKey` to `0` and pooled voxel volumes can be despawned/reused while staying non-null references; `RefreshCaveLifecycleState()` and `TryGetCaveAt()` were still treating any non-null volume as alive
- Problems: pooled or reassigned cave volumes could leave stale `_caveInstances` / `_activeCaveKeys` entries behind, and `TryGetCaveAt()` could return a dead cave registration until the next cleanup tick
- Short Comment: cave ownership truth tightened; world proof still missing

- Did: added explicit tracked-volume validity checks in `WorldCaveDirector`. `RefreshCaveLifecycleState()` now removes caves when the tracked `HectonVoxelVolume` is null, inactive in hierarchy, or no longer owned by the same `caveKey`, and `TryGetCaveAt()` now validates the tracked volume at read time instead of trusting registry state blindly. Shared removal logic is centralized through a dedicated `RemoveTrackedCave(...)` helper.
- Result: cave registry truth no longer depends on a `null` check alone. Pooled/reused cave volumes that have already been reset or deactivated are less likely to poison active cave state, and point queries stop returning stale cave entries just because `SlowTick` has not cleaned them yet.
- Failed: this remains `PENDING VERIFICATION` because the Unity MCP session disconnected during verification after a refresh timeout, so this pass is code-reviewed only and has no fresh compile/runtime oracle from the editor.
- Broke: no new issue is known from the code audit itself, but verification is externally blocked until Unity reconnects.
- Remaining: reconnect Unity, rerun compile/read-console, then verify in build that cave revisit / cave despawn / pooled voxel reuse does not leave ghost cave registrations or dead `TryGetCaveAt()` hits.

### [!] Gas Giant Does Not Read As Distant
- Status: [!]
- Need User Check: partial success confirmed
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: sky scale is a major immersion pillar
- Evidence: user report says the gas giant looks too near because it sits incorrectly against the cloud layer
- Problems: hard giant silhouette needed atmospheric depth, but any camera-centered overlay sphere created visible parallax mismatch and read as a screen-space patch
- Short Comment: perceptual blocker
- Next Step: keep the giant huge, keep the new giant-anchored veil, verify it in build and across day/night states, and decide whether horizon compression should become a weather-state dial instead of one static default

- Did: traced the actual render order and found the root cause in the celestial camera path instead of size alone: `SpaceCamera` renders the `Celestial` layer as the base pass, and that same layer contains both `Sky_System/Sphere` and `GasGiant_Aegir`, so the existing sky dome clouds never draw any atmospheric veil over the gas giant. The first pass used a cloud/haze overlay shell, but the user rejected it, and then correctly identified the deeper bug: a camera-centered overlay sphere will always drift against the giant under lateral camera motion. Reworked the solution into the correct place: `SG_GasGiant_Master.shader` now applies the distance veil directly on the giant using the fragment view ray, sky-linked color, and `NightBlend`; `HectonCelestialEngine` now feeds the giant the same live sky colors as the rest of the atmosphere. After that, the remaining defect was no longer giant scale but horizon balance, so the next pass was moved to the actual source of truth: `HectonCelestialEngine` now compresses horizon luminance before pushing colors into both the sky and the gas giant, and `Mat_GasGiant` now carries a stronger horizon extinction curve so the lower arc loses contrast into the same atmospheric band instead of sitting on top of it. This pass adds the missing architectural layer for the future cloud problem: a separate soft `celestial occlusion` field in `SG_GasGiant_Master.shader`, sampled from the shared sky cloud atlas at low frequency and used only as optical transmittance/detail loss, not as the visible cloud layer itself. The legacy `Sphere_CloudOverlay` object is disabled in the live scene.
- Result: user reported the giant now looks much better. The atmospheric softening is now anchored to the giant instead of a fake disc in front of the camera, so the left/right edge inconsistency from the overlay patch is gone in the current live scene. The follow-up pass reduces the chalk-white horizon and makes the giant dissolve harder at the waterline, so the lower arc reads less like a clean sticker edge. The new soft occlusion field avoids the old giant-vs-cloud contradiction: celestial objects can now lose transmittance from atmospheric structure without having the ugly visible cloud shapes stamped directly on them. Scene-view and `SpaceCamera` readback after switching the occlusion field to spherical UV no longer show the earlier vertical-streak artifact from horizon-projected UVs.
- Result Addendum: the next objective readback showed a second issue after the first haze wins: the lower half of the giant was flattening into an overly uniform lavender plate, while the left horizon band was still too white. The latest pass narrows the giant extinction into a true horizon band instead of a broad half-disc wash, restores more structure in the middle of the planet, and cools the sky horizon material directly so the background no longer blows out into a near-white wall.
- Result Addendum: the follow-up pass split the problem one step further into `horizon band` and `bottom arc`. That preserves the upper and middle structure while letting the very lowest edge merge harder into the horizon. Current scene-view and `SpaceCamera` readback now match the intended shape more closely: top remains readable, middle is moderated, and the bottom arc is the part that gets eaten first.
- Result Addendum: the next pass tightened the `bottom arc` itself with a steeper response curve and slightly stronger veil/desaturation values. This keeps the extra extinction concentrated at the very edge instead of bleeding back into the middle of the disc. Current readback shows the lowest edge merging harder while the upper and middle zones remain readable.
- Result Addendum: the next objective gameplay screenshot exposed the last remaining shape error more clearly: the very bottom center was improving, but the lower side silhouette still read as a clean circular arc. The giant shader now welds not only the bottom center but the lower horizon-facing limb as a soft crescent, suppressing rim light and pushing that narrow silhouette strip into the same haze color as the horizon band. This is the correct physical shape for long atmospheric path length: not a flat strip, but a lower edge crescent.
- Result Addendum: a further coefficient pass pushed that lower horizon-facing limb crescent harder by reducing local detail/contrast and increasing haze tint along the side silhouette, not just at the bottom-center strip. `SpaceCamera` readback now shows the lower-left arc less clean than before, although the weld is still not absolute.
- Result Addendum: after the lower edge was pushed hard enough, a second regression appeared above the horizon: the bottom weld held, but the zone just above it became too clean again and the atmosphere stopped reading except at the waterline. The giant shader now has a separate `air-mass shoulder` between the narrow horizon weld and the cleaner upper disc. This shoulder reduces detail, saturation, and contrast in the lower-mid band without collapsing the top third into a flat wash.
- Result Addendum: the first `air-mass shoulder` pass fixed the missing middle haze but introduced a new energy bug: it was mathematically separate from the horizon band and too close to `_SkyHazeColor`, so the image broke into `white strip -> cleaner disc` and the giant became too bright and too blue above the horizon. The current pass replaces that stepped shoulder with a continuous broad air-mass curve above the horizon, keeps the narrow horizon band only as the extra lower-edge boost, and adds a separate air-mass darken term so the giant reads as behind haze instead of simply being painted with brighter milk.
- Result Addendum: after the user locked in a good lower horizon band manually, the remaining issue was isolated to the middle and upper thirds. The shader now has a dedicated `upper haze` lobe on top of the broad air-mass curve, aimed only at the upper/mid disc and modulated by the existing low-frequency celestial occlusion field. This keeps the current lower merge intact while letting the upper giant sit behind more atmosphere without stamping visible cloud shapes onto it.
- Failed: build verification is still missing, and automated day/night sweep is still blocked by MCP `execute_code` failing on this machine with `mono.exe: filename or extension is too long`. `02_HECTON_WORLD` remains dirty and unsaved. The old compile blocker from `WorldCaveDirector.cs` is now cleared, so the new `HectonCelestialEngine` feed is no longer blocked at compile time, but it still lacks build/runtime proof.
- Broke: the intermediate overlay-sphere path was a false solution and has been retired from the live runtime path.
- Remaining: verify horizon behavior and night darkening in build, then decide whether to delete the retired overlay assets entirely or keep them only as dead experiments outside the runtime path.
- Lesson: atmospheric depth cues must be attached either to the rendered object itself or to the same world-space ray logic as the rest of the sky. Camera-centered proxy geometry is not “cheap atmosphere”; it is guaranteed parallax debt.

- Lesson Addendum: when the horizon looks wrong, fix the shared sky-response first and only then tune object-specific extinction. If the sky and the giant are not driven by the same atmospheric color logic, the eye reads the giant as pasted in front immediately.
- Lesson Addendum: visible clouds and celestial occlusion are not the same system. The visible cloud layer can stay art-driven and high-character, while celestial objects should read a separate low-frequency transmittance field that only controls extinction, softness, and detail loss.
- Lesson Addendum: the separation is now implemented in both directions. `SG_GasGiant_Master.shader` reads a soft low-frequency occlusion field for the giant, and `Hecton_AlienSky_Master.shader` now has a separate celestial transmittance field that can dim stars, sun scatter, and halo without reusing the visible cloud silhouettes as direct masking.
- Lesson Addendum: broad full-disc fading is the wrong shape. Atmospheric loss has to be concentrated into a narrow horizon band; otherwise the giant stops feeling distant and starts feeling like a uniformly fogged matte sphere.
- Lesson Addendum: even `horizon band` alone is too coarse. The most believable extinction shape is two-stage: a moderate horizon band for the lower third, then a much tighter bottom arc that almost welds the final edge into the horizon band without killing the middle of the disc.
- Lesson Addendum: the bottom arc needs a steeper response curve than the broader horizon band. If both use the same softness, the extra extinction leaks upward and flattens the middle of the giant.
- Lesson Addendum: even a tight `bottom arc` is still incomplete if it only attacks the lowest center pixels. The last giveaway is usually the lower side silhouette. The physically useful shape is a `horizon-facing limb crescent`: horizon attenuation plus grazing-angle attenuation, so the lower side edge dissolves first without fogging the whole disc.
- Lesson Addendum: a believable distant planet needs three stacked distance zones, not one: a broad `air-mass shoulder` for the lower-mid band, a stronger `horizon band` for the lower third, and only then the tight `bottom arc / limb weld` at the final edge. If the shoulder is missing, the horizon looks fixed but the disc above it snaps back to a clean poster.
- Lesson Addendum: the broad upper haze must be continuous with the horizon haze. If it is computed as a separate leftover band or tinted too directly toward bright haze color, the eye sees an abrupt transition: a white strip at the horizon and then an unnaturally clean blue planet above it. The broad air mass should mainly reduce contrast, saturation, and brightness, while the true white milk belongs near the horizon itself.
- Lesson Addendum: once the lower band is artist-approved, stop touching it. Add any remaining distance cue as a separate upper haze layer. That preserves the hand-tuned horizon while giving the middle and upper thirds their own atmospheric coverage. The safest modulation source is the existing low-frequency celestial occlusion field, not the visible cloud layer.
- Failed Addendum: previewing night via direct `_NightBlend` material override is still not a trustworthy final oracle on this project. The current `SpaceCamera` capture path shows only weak visual response even after strengthening the giant's night branch, so night verification remains pending until a cleaner runtime/view path is available.
- Failed Addendum: `game_view` screenshot capture is still not a trustworthy oracle for this scene on this machine; the latest MCP capture from `Main Camera` returned a black frame, so scene-view remains the only usable visual readback during editor-side tuning.

### [!] Terrain / Rock Close-Up Blur
- Status: [!]
- Need User Check: yes
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: close-up terrain blur weakens world credibility
- Evidence: user report says rocks and terrain look blurrier in game than expected from editor
- Problems: could be material tiling, runtime terrain settings, streaming, or build-only LOD behavior
- Short Comment: must be solved by comparison, not guesswork
- Next Step: run an identical editor/build terrain parity pass and separate material vs streaming causes

- Did: issue recorded from build report
- Result: root cause audit is no longer blind. `VoxelChunk` is not the current visible terrain path; active close-up rock landmarks in `__PROCEDURAL_SCATTER_WORLD` are currently `proxyOnly` instances such as `SCATTER_family.rock.arch.large_*`, built from cube proxy prefabs. Their materials were also objectively empty on the active path: `MAT_family_rock_arch_large.mat`, `MAT_family_rock_cluster_medium.mat`, and `MAT_family_rock_small_floor.mat` were `URP/Lit` materials with no albedo or normal textures assigned.
- Did Addendum: patched `WorldProceduralScatterDirector` so final-variant eligibility is no longer frozen only by stale cached `placement.SupportsFinalVariant`; reconcile/signature/rebuild logic now re-resolves support from the current family asset. This is aimed at letting families that now have `finalReady && !proxyOnly` variants stop sticking to old proxy-only placements.
- Did Addendum: patched the active rock proxy materials and the `MAT_family_rock_arch_large_Placeholder` material with real albedo/normal/AO texture references, so even before runtime final-variant proof the live proxy path is no longer rendering empty flat-tint materials.
- Did Addendum: cleared the unrelated compile blocker that was freezing this verification path. `WorldCaveDirector` was still calling removed `MapMagicBridge.SampleHeight`; it now uses `TryGetHeight` with fail-safe fallback, `caveSpawnProbability` is finally wired back in as the intended biome-evaluation gate, and duplicate `using` noise was removed from `HectonVoxelEngine`.
- Failed: the runtime part is still `PENDING VERIFICATION` because close-range rock families have not yet been proven to rebuild into their `final` or `final.placeholder` variants during live runtime after the compile unblock. The active rock arch proxy geometry is also still cube-based placeholder form, so the material pass improves surface detail but cannot by itself turn placeholder blocks into final rock silhouettes.
- Broke: no new compile errors were introduced by the scatter or cave compile-fix passes; console now reports only the unrelated `Dynamic Decals` obsolete warnings and one earlier MCP warning about unsupported `_Smoothness` conversion during a material tool call.
- Remaining: verify that close-range rock families now rebuild into their `final` or `final.placeholder` variants in runtime, then run editor/build parity at the same spot and only after that decide whether terrain texture density itself still needs retuning.

### [ ] Build Smoother Than Editor
- Status: [ ]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: this is not a bug; it is a production rule reminder about truth source
- Evidence: user explicitly reports build feels smoother than editor
- Problems: editor-heavy debugging can still waste time if treated as final truth
- Short Comment: use build as arbiter
- Next Step: keep player-build-first discipline for P0 blockers and perceptual quality

- Did: observation recorded
- Result: promoted into workflow rule. Standalone profiler screenshots from `2026-04-06` reinforce the same conclusion: the player build baseline is materially better than editor play mode, so editor-only spikes must not be treated as final truth without build capture.
- Failed: nothing
- Broke: nothing
- Remaining: maintain this discipline on all future passes

### [~] Standalone Player Profiling Snapshot
- Status: [~]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: performance work now has real standalone evidence instead of editor noise
- Evidence: attached standalone player profiler screenshots from `Shinobu - Submerge`
- Problems: the build console warned that the player was built with uncompiled code changes, so the captured player may lag behind the latest source edits; GPU timings were not available in the screenshots (`GPU --ms`), and current MCP profiler attachment is not active
- Short Comment: baseline build performance is not a blanket CPU disaster; the real blockers are intermittent spike classes
- Next Step: re-capture the same build with named scenarios (`idle swim`, `surface crossing`, `PDA/pause open`, `dense world route`) and correlate each spike to a concrete action

- Did: extracted the standalone screenshots into one frame table:

| Frame | CPU Frame | Primary Marker | Read |
| --- | ---: | --- | --- |
| `3327` | `14.72 ms` | `WaitForLastPresent ≈ 9.06 ms` | Healthy baseline frame. Real gameplay + render work is much lower than total frame time; a large part is present/frame-pacing wait. |
| `3676` | `42.18 ms` | `WaitForLastPresent / DXGI.WaitOnSwapChain ≈ 36.14 ms` | Present-bound miss. Main thread total looks scary, but the frame is dominated by waiting, not by script saturation. |
| `2483` | `22.19 ms` | `Coroutine: MoveNext ≈ 10.01 ms` | Real intermittent CPU hitch. Matches the project pattern where `GameTickManager` still runs a global `SlowTickRoutine()` coroutine. |
| `3826` | `53.55 ms` | `EventSystem.Update() ≈ 42.89 ms` -> `GameObject.ActivateAwakeRecursively ≈ 23.54 ms` | Real CPU spike from UI activation cascade. `Collect ≈ 2.27 ms` is visible on the same frame. |

- Result: the screenshots separate the frame into two different problems instead of one fake general slowdown:
  1. Baseline standalone frames are often `present-bound`, not logic-bound.
  2. The real CPU hitches are intermittent and currently fall into two buckets:
     - UI activation storms
     - coroutine / slow-tick spikes
  3. The current geometry load does not read as the main blocker from these screenshots alone: visible counters sit roughly around `73-117` batches, `~101k-346k` triangles, `~181k-346k` vertices, `~0.73 GB` total memory, `~366 MB` texture memory, `239` materials, `~16.1k-16.6k` objects, and `~82-85 MB` GC used memory.
  4. The `3676` render-thread screenshot supports the same interpretation: it spends `~40.2 ms` mostly in `Semaphore.WaitForSignal / WaitForGfxCommandsFromMainThread`, which is consistent with a present-bound frame rather than a render-thread work explosion.
  5. The current editor complaint about `EditorLoop` is still not runtime proof. Code audit shows many edit-time preview owners (`HectonAtmosphereManager`, `HectonUnderwaterVisuals`, visor/HUD preview stack, sky helpers) subscribed through `[ExecuteAlways]` and `EditorApplication.update`, while the latest live gameplay log points to a different concrete offender: startup `SlowTick` spikes with `WorldProceduralScatterDirector=96.03ms` and a later `12.83ms` steady spike. Until a player capture shows otherwise, runtime CPU work should be attributed to those live offender logs, not to `EditorLoop`.
  6. New scatter CPU addendum: `WorldProceduralFieldSampler.TryBuildCellInput()` was redundantly computing `slopeDegrees` and `curvature` on the main thread inside `TryGetLocalTerrainContext()`, even though `CellSamplingJob` recalculates both from the same height probes for every sampled cell. The sampler now splits cheap `cell height context` from full `local terrain context`, so the startup scatter path no longer pays that duplicate math in the pre-job stage.
  7. New scatter CPU addendum: `WorldProceduralScatterDirector` was still re-resolving `clusterRatioStart`, `passiveSpawnMin`, and `predatorSpawnMax` per sampled cell right after `PopulatePatternQuotaCache(pattern, biomeProfile)` had already identified the same `pattern + biome` key. Those values now ride the same cached quota payload instead of forcing extra profile lookups for the same cell context.
  8. New scatter CPU addendum: the inner scatter rule loop was recomputing `NeedsPreviewRescue(sample, family)` three times for the same rule path: once through `ResolveEffectiveMinHeat`, once through `ResolveEffectiveDensityScale`, and once again for rescue tracking. That gate is now resolved once per rule and reused through the rest of the branch.
  9. New scatter CPU addendum: the same hot loop already knew `needsPreviewRescue` and `needsSpawnRescue`, but `TrackRescueCandidate(...)` recalculated both from the same `sample + family` before touching the rescue maps. The loop now passes those existing booleans through directly, so rescue tracking no longer pays duplicate gate resolution for every retained rescue candidate.
  10. New scatter CPU addendum: the score branch was still rebuilding the same biome-matrix signals and focus-role derivations for every runtime rule inside one sampled cell. `WorldProceduralScatterDirector` now builds one stack-only `ScatterBiomeScoreContext` per cell and reuses those cached `resource / salvage / landmark / pressure / survival` signals plus preferred/focus roles across heat/score helpers instead of re-deriving them for every rule.
  11. New scatter CPU addendum: even after the biome-score context pass, one runtime rule could still linearly scan the same `preferred*Families` array multiple times through `GetPreferredContentScoreBonus`, `GetBiomeSignatureScoreBonus`, `GetPatternSpecificPreferredCategoryScoreBonus`, and structure-only soft-water bonus helpers. The loop now resolves one `layerPreferredFamilyIndex` per rule and reuses that index across those score helpers instead of rescanning the same preferred-family array 2-4 times for the same rule.
  12. New scatter CPU addendum: one sampled cell also kept re-evaluating the same pattern-category gates (`soft water`, `service-like`, `landmark corridor`, `industrial signature`, `sediment resources`) across multiple score helpers for every runtime rule. `WorldProceduralScatterDirector` now builds one stack-only `ScatterPatternScoreContext` per cell and reuses those booleans through the runtime score/heat helpers instead of repeating the same pattern guards for each rule.
  13. New scatter CPU addendum: one runtime rule was still issuing four separate pattern-dependent score helper calls on the same `resolvedPattern` (`GetPatternAffinityBonus`, `GetClusterAccentPatternBonus`, `GetSpawnFamilyPatternBonus`, `GetPatternContextBonus`). The score branch now consolidates that into one `GetCombinedPatternScoreBonus(...)` call so the same pattern/runtimeRule pair is evaluated once instead of through four separate helper dispatches.
  14. New scatter CPU addendum: the heat branch was still multiplying three separate scale helpers for the same `pattern + runtimeRule + depth` tuple (`GetPatternHeatScale`, `GetLandmarkSoftWaterHeatScale`, `GetDepthDomainScale`). That path now goes through one `GetCombinedHeatScale(...)` helper so the same tuple is resolved once instead of via three separate helper dispatches.
  15. New scatter CPU addendum: after `MatchesScatter(...)` had already proven preferred-biome and preferred-zone acceptance, the score branch still rescanned `PreferredBiomeFamilies` and `PreferredZoneKinds` in `GetFamilyAffinityBonus(fieldSample, runtimeRule)`. The post-gate score path now uses `GetAcceptedFamilyAffinityBonus(...)`, which reuses that already-proven truth and removes the duplicate array scans from accepted rules.
  16. New scatter CPU addendum: `BuildCandidate(...)` was still doing full variant / scale / chunk / macro / pooled-placement work before residency reject. The startup scatter loop now builds a lightweight `ScatterCandidatePreview` first, checks residency from preview `position + streamingLayer`, and only constructs the full pooled `ScatterPlacement` after the candidate is already inside the residency envelope.
  17. New scatter CPU addendum: even after that residency split, the loop was still computing full score math before `gate` and `residency` rejection, and preview build still spent `Quaternion.Euler(...)` on candidates that died before placement. The score branch now runs only after residency pass, and preview rotation is deferred to full `BuildCandidate(...)`, so geology/pattern/biome score work and rotation math no longer run for dead candidates.
  18. New scatter CPU addendum: within one sampled cell, the score branch was still re-running `WorldGenerativeGeologyProfile.EvaluatePlacementFitness(...)` for every accepted rule that shared the same `GeologyProfile`. The loop now uses a small cell-local profile cache, so repeated geology profiles on the same sampled cell reuse the same computed bonus instead of paying the same geology fitness solve again.
  19. New scatter CPU addendum: `ShouldTrackRuntimeSpawnRescue(...)` was recalculating `ResolveMinimumSpawnPlacements(pattern, biomeProfile)` for every spawn-family rule in the same sampled cell. That spawn minimum is now resolved once per cell and reused through the rule loop instead of re-deriving the same `pattern + biomeProfile` spawn floor for every spawn rule.
  20. New scatter CPU addendum: two score constants in the inner loop were still static per runtime rule: accepted family affinity and geology composition scale. Those are now precomputed once in `PrepareRuntimeRuleBuffer()` and stored on `ScatterRuntimeRuleEntry`, so accepted rules no longer redo the same preferred-family bonus derivation and geology weight clamp on every sampled-cell evaluation.
  21. New scatter CPU addendum: `gate` and `needsRescueTracking` were still being evaluated after `HasPatternLayerGlobalBudget(...)`, `HasLayerBudget(...)`, and `CanAcceptPatternAccentBudget(...)`. The rule loop now rejects dead non-rescue candidates on `gate` before those budget checks, so cells no longer pay that budget work for candidates that cannot survive the random gate anyway.
  22. New scatter CPU addendum: `TrackWindowCandidate(..., ref CandidateMap)` was scanning the same `CandidateMap` twice on replace (`TryGetValue` then `TryAdd`). The hot rescue-map path now uses an index lookup and replaces in-place, removing the duplicate linear scan for better-candidate replacement.
  23. New scatter CPU addendum: after residency passed, non-rescue candidates were still paying full `BuildCandidate(...)` before dying on the already-known `gate > spawnProbability` random reject. The loop now short-circuits that gate before `BuildCandidate(...)` for non-rescue candidates, while rescue-tracked candidates keep the old path because rescue retention still needs a built candidate.
  24. New scatter CPU addendum: that same non-rescue dead branch was still paying full score math before the same known `gate > spawnProbability` reject. The loop now short-circuits the gate before both score calculation and `BuildCandidate(...)` for non-rescue candidates, while rescue-tracked candidates still keep the scored path because rescue ranking depends on it.
  25. New scatter CPU addendum: the same non-rescue dead branch was still paying `BuildCandidatePreview(...)` plus residency checks before the already-known `gate > spawnProbability` reject. The loop now short-circuits that gate before preview, residency, score, and full build for non-rescue candidates; rescue-tracked candidates still keep the old path because rescue capture depends on preview/build state.
  26. New scatter CPU addendum: even after those gate cuts, non-rescue candidates could still pay full `BuildCandidate(...)` after score calculation when the per-cell candidate buffer was already full and their score could not beat the current worst retained candidate. The loop now checks `worstCandidateScore` before building and skips those dead-on-arrival non-rescue candidates entirely.
  27. New scatter CPU addendum: rescue-tracked candidates that were already known to fail `gate > spawnProbability` still paid full runtime state assembly (`ResolveRuntimeVariant`, `ResolveScaleMultiplier`, chunk/macro coordinate solve) before being discarded after rescue-window tracking. `BuildCandidate(...)` now supports deferred runtime state for this exact branch, and runtime state is resolved only when that placement is actually registered into desired placements (including rescue injection), preserving rescue semantics while cutting dead branch work.
  28. New scatter CPU addendum: cell-local geology bonus cache previously held only two profiles, so cells with 3+ active geology profiles could thrash and re-run `EvaluatePlacementFitness(...)` for the same profile repeatedly inside one sample pass. The cache is now a reusable 4-slot struct (`GeologyBonusCache`) and still zero-alloc, reducing geology-fitness recomputation without changing score semantics.
  29. New scatter CPU addendum: deferred runtime-state assembly is now applied to all sampled candidates, not only rescue-gate rejects. `BuildCandidate(...)` no longer resolves `variant/scale/rotation/chunk/macro` in the sampling loop; that state is finalized only at placement registration (`TryRegisterDesiredPlacement(...)` / retained-placement restore), so candidates rejected later by budget/spacing/candidate-prune gates no longer pay that heavy work.
  30. New scatter CPU addendum: once a non-rescue cell buffer is already full, the score path now does a two-stage reject before expensive remaining work. First it computes `scoreBeforeBiomeMatrixAndGeology` and compares it against a strict `ResolveBiomeMatrixScoreUpperBound(...) + GeologyScoreScale` ceiling; candidates that still cannot beat `worstCandidateScore` are rejected before biome-matrix and geology work. Then, after exact biome-matrix score is known, the path runs one more geology-only ceiling check so `EvaluatePlacementFitness(...)` is skipped for candidates that still cannot overtake the current floor. Rescue-tracked candidates keep the full path unchanged.
  31. Scheduler review addendum: the attempted frame-sliced `GameTickManager` slow-tick dispatcher was rolled back before verification. Code review found a real temporal-regression risk: many existing owners still assume one full `SlowTick` wave per interval and/or hardcode fixed dt semantics (`FaunaDirector`, `BaseModule`, `HectonSurvivalSystem`). Keeping the sliced scheduler would have changed runtime behaviour before any live proof, so the manager was restored to the original cached-`WaitForSeconds` coroutine contract.
  32. Fauna cadence addendum: `FaunaDirector` had an owner-local cadence bug independent of the scheduler work. Biome throttling was implemented as `_biomeCheckTimer -= 1f` inside `SlowTick()`, which silently assumed one call per second and was already wrong against the manager's `0.5s` default. This now uses absolute `Time.time` gating (`_nextBiomeCheckTime`) for `BiomeCheckInterval`, preserving throttle semantics without depending on call count.

- Failed: GPU-side truth is still incomplete because the screenshots do not expose actual GPU frame time, and MCP currently reports profiler disabled; its fallback rendering snapshot is not trustworthy as a live player oracle here.
- Failed Addendum: compile ready-state verification for addendum 27 is still unstable (`refresh_unity(wait_for_ready=true)` times out), but live console readback is available and currently shows no new compile `CS` errors from this pass.
- Failed Addendum: compile ready-state verification for addendum 28 is still unstable (`refresh_unity(wait_for_ready=true)` timeout remains), and immediate post-refresh console readback returned no entries, so this pass remains `PENDING VERIFICATION` until the next stable compile oracle/profiler pass.
- Failed Addendum: full compile verification for addendum 29 is currently blocked by unstable Unity readiness (`Unity session not ready / ping timeout`). The previously reported `WorldProceduralSeaweedMeshBuilder.cs` (`CS0103`, `CS0136`) compile blockers were patched in code during this pass, but clean compile readback is still pending until the Unity session recovers.
- Failed Addendum: compile oracle for addendum 30 is still degraded. `refresh_unity(mode='force', scope='scripts', compile='request', wait_for_ready=true)` timed out after `60s`, but immediate `read_console(types=['error'])` readback returned `0` entries. That is better than the previous session blackout, but still not equivalent to a clean ready-state compile proof, so the pass remains `PENDING VERIFICATION`.
- Failed Addendum: compile/runtime oracle for addenda 31-32 is still unavailable. `refresh_unity(mode='force', scope='scripts', compile='request', wait_for_ready=true)` was previously timing out and the last `read_console` retry hit `Unity session not ready`. So the rollback/hardening pass is still `PENDING VERIFICATION` until the Unity session becomes stable again.
- Broke: nothing.
- Remaining: audit and reduce:
  - `EventSystem` -> `GameObject.Activate/ActivateAwakeRecursively` spikes
  - UI `SetActive` cascades in `PlayerPDA`, `PDAInventoryTab`, `PauseMenuController`, and HUD roots
  - `GameTickManager.SlowTickRoutine()` / coroutine spike ownership without breaking whole-wave slow-tick contract
  - only after that decide whether a broader render reduction pass is even justified

### [c] UI Activation Cascade In PDA / Pause / HUD Roots
- Status: [c]
- Need User Check: yes
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: standalone frame `3826` already showed a real CPU spike from `EventSystem.Update() -> GameObject.ActivateAwakeRecursively`
- Evidence: standalone profiler screenshots from `2026-04-06`
- Problems: build still has unrelated compile blockers in other files, so a clean end-to-end compile oracle is currently contaminated
- Short Comment: code pass applied; closed for current implementation work until new build evidence
- Next Step: capture new standalone profile around `open PDA`, `switch PDA tabs`, `open pause`, and `resume gameplay`

- Did: replaced UI root visibility churn with cached visibility gates in the confirmed hot stack. `PlayerPDA` shell and tabs now use warmed `CanvasGroup` visibility instead of repeated hierarchy wake/sleep; `PDAInventoryTab` now hides item blocks, detail widgets, selection/hover markers, and action roots without `SetActive`; `PDALoadoutTab` action buttons, preset cards, and suggested-action root now use cached `CanvasGroup` visibility; `PDADataLogTab` now defers hidden-tab refresh work instead of refreshing in the background; `PauseMenuController` section switching now uses per-panel `CanvasGroup` visibility; `SuitHUDV4CanvasOverlay` root hide/show no longer toggles the overlay root active state.
- Did Addendum: `PDAShellChrome` still had a smaller but real local churn path on top of that broader UI pass. Its shell child root no longer toggles active state during refresh; it now stays warm behind a cached `CanvasGroup`, and the tab/footer strings are dirty-gated with constant format strings instead of runtime string interpolation every `refreshInterval`.
- Did Addendum: `HUDQuickBar` still had owner-local `SetActive` churn on icon and durability widgets during its periodic refresh. Those child graphics now stay warm; visibility is driven by cached image state and width/alpha updates instead of `gameObject.SetActive(...)`, and TMP summary/directive assignment is dirty-gated so unchanged text no longer forces rebuild on every refresh.
- Did Addendum: `HUDNotification` still used root `SetActive(true/false)` during show/fade and even disabled its own `gameObject` in `EnsureBuilt()`. That owner now keeps the notification object warm for the whole scene lifetime; fade/show state is tracked through cached `_isShowing + _currentAlpha` instead of hierarchy activation.
- Did Addendum: preset-only UI consumers were still paying full `FieldLoadoutAdvisor.LoadoutAdvice` construction, including `Summary` string formatting, just to read `PresetName`. `FieldLoadoutAdvisor` now exposes a preset-only path, and `HUDQuickBar` plus `PDALoadoutTab.GetRecommendedPresetName()` use it instead of the full advice builder. Full `Summary` generation remains only on callers that actually render or validate `advice.Summary`.
- Did Addendum: `PDALoadoutTab.RefreshSummary()` was still resolving forward advice twice in one refresh cycle: once for `SUGGESTED` preset and once again for `FIELD` directive. That path now resolves `LoadoutAdvice` once per refresh and reuses both `PresetName` and `Summary` from the same query.
- Result: the known `ActivateAwakeRecursively` path is now attacked at the actual sources instead of at profiler symptoms. The intended runtime effect is fewer UI activation spikes, less activation-adjacent GC on open/switch frames, and lower `EventSystem` cost when toggling PDA/pause/HUD visibility.
- Failed: standalone before/after capture for `open PDA`, `switch PDA tab`, `open pause`, and `resume gameplay` still has not been re-run, and Unity MCP `execute_code` remains blocked on this machine by `mono.exe: filename or extension is too long`.
- Broke: the unrelated compile contamination that previously blocked this verification path is now cleared. Current compile readback shows warnings and editor-inspector null spam, but no new `CS` errors from the UI pass.
- Did Addendum: `HUDNotification.OnInventoryFull(...)` was still rebuilding the same uppercase warning string every time inventory overflow repeated for the same item. That producer now uses a small cached message path keyed by item name, so repeat overflows reuse the full HUD warning string instead of paying `ToUpperInvariant()` and full message assembly each time.
- Did Addendum: `LaserCutter` recovery-mode progress feedback was still formatting a fresh percentage string on every timed progress pulse during deconstruction. That owner now uses a prebuilt `0..100%` message table and indexes it by clamped rounded progress, preserving the same cadence/text semantics without repeated runtime formatting.
- Did Addendum: `RepairTool` was still rebuilding two finite headline strings through interpolation on every active-service start and every service-diagnosis title emit (`"REPAIR TOOL - {headline}"`, `"SERVICE DIAG - {headline}"`). Those owner-local finite headline paths now resolve through fixed message mapping, while unknown future headlines still fall back to the old concatenated behaviour to avoid semantic drift.
- Did Addendum: `SalvageSamplerTool` still rebuilt recovered-item HUD strings and diagnosis headline emits on the secondary recovery/diagnostic path. Recovered item messages now reuse a small cache keyed by item name, and finite diagnosis headline emits/log titles now resolve through fixed mapping while dynamic node-percentage headlines keep the old fallback text.
- Remaining: rebuild after clearing unrelated compile blockers, then compare standalone profiler frames before/after for `PDA open`, `tab switch`, `pause open`, `pause close`, and `idle gameplay with HUD active`. Separate perf tracks still remain for string-producing tool/status producers: `ToolHitUtility` callers, `PlayerTool.GetOperationalSummary/GetOperationalDirective()`, and the full `FieldLoadoutAdvisor` summary path on callers that still genuinely need `advice.Summary`.

### [~] GPU / Present Pacing Track Still Separate
- Status: [~]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: some scary CPU totals are actually `present wait`, not script overload
- Evidence: standalone frames `3327` and `3676` are dominated by `WaitForLastPresent / DXGI.WaitOnSwapChain`
- Problems: current screenshots do not include trustworthy GPU frame times (`GPU --ms`), and MCP profiler attachment is not live
- Short Comment: do not mix this track with UI or slow-tick CPU hitches
- Next Step: recapture standalone with real `GPU` timings enabled and scenario labels

- Did: separated the render/present track in the ledger and master plan so future passes do not falsely blame script systems for present-bound frames.
- Result: the project now has an explicit rule: `WaitForLastPresent / DXGI.WaitOnSwapChain` must be treated as a separate render/pacing investigation, not as proof that gameplay CPU is overloaded.
- Failed: no new GPU timing evidence yet.
- Broke: nothing.
- Remaining: collect player build captures with actual GPU milliseconds before attempting any broad render cuts.

### [~] Fauna SlowTick Spike / `SmallPassiveProxy` Pool Exhaustion
- Status: [~]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: live runtime log now points to a concrete world offender instead of a vague `SlowTick` bucket
- Evidence: user console log from `2026-04-06` shows `[TickProfiler] SlowTick spike total=19.64ms ... FaunaDirector=12.05ms` and repeated `[ObjectPoolManager] 'SmallPassiveProxy': Pool exhausted, expanding by 4`
- Problems: the old fauna pool warmup used one static reserve of `8`, while live runtime streaming settings can increase `_runtimeMaxSpawnsPerTick` far above that after scene start
- Short Comment: active runtime offender; this is not editor noise
- Next Step: run the same swim route again and confirm whether `SmallPassiveProxy` expansion warnings stop and whether `FaunaDirector` drops out of the top `SlowTick` offender slot

- Did: cleared the hard runtime crash in `WorldProceduralScatterDirector` by removing the invalid `NativeArray<ScatterCandidate>` use from `CandidateMap`; `ScatterCandidate` contains managed references and cannot live in `NativeArray<T>`. The cache now uses managed arrays in cold/runtime cache space instead of invalid job memory. Then patched `FaunaDirector` pool warmup so reserve targets are derived from live runtime streaming limits instead of a dead constant `8`, and so a later runtime settings refresh can reopen warmup when those limits grow. `SmallPassiveProxy` now gets a stronger reserve target than ordinary fauna prefabs because it is the prefab named in the live expansion warnings.
- Did Addendum: patched both gameplay spawn sites in `FaunaDirector` to call `ObjectPoolManager.Spawn(..., allowExpand:false)` instead of the default expanding path. This closes the remaining zero-GC hole where `SlowTick` could still trigger runtime `Instantiate` via pool expansion when reserve was temporarily exhausted.
- Did Addendum: the main `SlowTick` selector in `FaunaDirector` still ignored pool availability and could keep choosing prefabs that had already run dry, wasting spawn attempts on strict `allowExpand:false` paths. `TrySelectResolvedEntry(...)` now receives `ObjectPoolManager`, filters out entries with `GetAvailableCount(prefab) <= 0`, and falls back only among entries that are both under `maxAlive` and actually spawnable from the current pool state.
- Did Addendum: the main `SlowTick` selector was still re-querying `ObjectPoolManager.GetAvailableCount(...)` across multiple passes inside one spawn burst. `FaunaDirector` now fills one reusable per-biome `availablePoolCounts` scratch array once per `TrySpawnCreatures(...)`, reuses it through selection, and decrements it only after successful spawns so the selector no longer keeps hammering pool lookups inside the same burst.
- Did Addendum: `ForceSpawnHorde(...)` still bypassed the resolved-entry cache, bypassed pool-availability truth, and paid a separate prefab/type selection path that could target dry pools. Horde spawning now uses the same cached `ResolvedFaunaEntry[]`, current per-type counts, and reusable `availablePoolCounts` scratch array, and selects only non-large-threat entries that are both under `maxAlive` and actually available in the pool.
- Did Addendum: `WorldFaunaSpawnRegistry` ordinary-anchor and large-threat-zone queries were still scanning the full anchor dictionaries on every spawn attempt. The registry now builds cold bucket caches keyed by `WorldChunkCoordinate` / `WorldMacroZoneCoordinate` and limits live lookup to the bounded observer neighborhood instead of rescanning all anchors each time `FaunaDirector` asks for a spawn point.
- Did Addendum: `WorldProceduralStateRegistry.IsFaunaAnchorAvailable(...)` was still running a full expired-fauna-state cleanup on every anchor availability query. The registry now batch-cleans fauna cooldown state on a bounded play-time interval and handles queried-key expiry directly, so fauna anchor checks no longer pay a full dictionary sweep for every spawn-point candidate.
- Did Addendum: successful anchor use was still expensive even after lookup cleanup because `WorldProceduralStateRegistry.MarkFaunaAnchorUsed(...)` called `UpdateDiagnostics()` after every spawn, and that diagnostics path rescanned the full fauna-state dictionary each time. Fauna-state diagnostics are now marked dirty and refreshed on a bounded interval instead of forcing a full dictionary sweep on every anchor consume/block/restore event.
- Did Addendum: the remaining live fauna-state scans in `WorldProceduralStateRegistry` cleanup/diagnostics were still written as dictionary `foreach`. Those hot-ish scans now use explicit `Dictionary<long, FaunaSpawnState>.Enumerator` loops instead of forbidden dictionary `foreach` in the active fauna path.
- Result: compile state remains clean of `CS` errors after the fauna/scatter pass. The runtime scatter blocker that previously aborted `WorldProceduralScatterDirector.Awake()` is code-fixed, and the fauna director no longer locks its warmup to a one-time static reserve disconnected from live activation limits.
- Result Addendum: fauna is now fail-soft under pool pressure. If reserve is insufficient, the director skips that spawn attempt instead of injecting pool expansion and allocation spikes into gameplay.
- Result Addendum: both regular fauna spawning and forced horde spawning now fail closed against live pool availability instead of spending work on prefabs that cannot spawn from the current reserve state.
- Result Addendum: fauna anchor lookup is now proportional to nearby chunk / macro-zone buckets instead of total registered anchors, so `FaunaDirector` no longer pays full-registry scan cost during ordinary and large-threat spawn resolution.
- Result Addendum: fauna availability truth now stays correct without forcing `WorldProceduralStateRegistry` to rescan the whole cooldown table on every anchor check; expired unblocked anchors reopen immediately, but full cleanup cost is now throttled instead of living inside each spawn query.
- Result Addendum: anchor cooldown accounting no longer drags a full diagnostics rescan behind each successful spawn. Runtime state truth is unchanged, but inspector/debug fauna counts are now deferred to bounded refreshes instead of sitting directly on the spawn hot path.
- Result Addendum: active fauna cooldown cleanup and diagnostics scans are now iteration-compliant as well; the remaining throttled scans no longer rely on banned dictionary `foreach` in runtime fauna state ownership.
- Failed: no new in-world proof yet that `SmallPassiveProxy` warnings are gone, because the user has not supplied the next live swim/build log after this patch and `execute_code` remains unusable on this machine.
- Broke: no new compile errors from `FaunaDirector` or `WorldProceduralScatterDirector`. The remaining console noise is editor selection null spam (`GameObjectInspector` / `SerializedObjectNotCreatableException`) plus unrelated warnings.
- Remaining: re-run the same underwater route in live game/build, capture the next `TickProfiler` line, and confirm:
  - `FaunaDirector` no longer dominates the top offender list at the same magnitude
  - `SmallPassiveProxy` no longer expands on-demand
  - close-up fish density still looks acceptable after the stronger prewarm

### [~] Camera Turn Overshoot / Reverse Lean After Mouse Stop
- Status: [~]
- Need User Check: yes
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: camera feel breaks trust immediately if horizontal look gives a reverse tail after release
- Evidence: user report from `2026-04-06` says horizontal mouse turns can accumulate and then lean/shift back in the opposite direction after the mouse stops
- Problems: the live camera juice stack applies spring-driven `swim roll` and `turn sway`, so release-phase overshoot can read like false head inertia instead of believable underwater mass
- Short Comment: active feel blocker
- Next Step: user/build verification while doing sharp left-right mouse turns at surface swim and in deeper water

- Did: patched `CameraJuiceProcessor` so the horizontal `swim roll` and `turn sway` tracks cannot spring past their target on release. The old spring behaviour could cross zero and create a visible opposite-direction tail after mouse stop. The new helper clamps to the target and zeroes the spring velocity as soon as an overshoot is detected, instead of letting the effect rebound through the center.
- Result: the code path now specifically attacks the reported symptom without deleting the whole camera-mass layer. The intended runtime effect is: the camera can still lean and sway during the turn, but when input stops it should settle to neutral instead of kicking to the opposite side.
- Failed: no user/build proof yet. I have only compile confirmation and code-path inspection, not a new live swim check.
- Broke: no new compile errors; console remains limited to editor selection null spam.
- Remaining: verify in live game/build with:
  - steady left-right mouse sweeps underwater
  - fast flick then release
  - same test near the surface where bob + sway stack together

### [c] Surface Jump / Shoreline Climb Reliability

- Status: [c]
- Need User Check: yes
- Need Build Check: yes
- Need In-World Shore Check: yes
- Why: if the player cannot reliably jump or climb out of shallow shoreline geometry, surface trust collapses even if oxygen/surface-state code is technically correct
- Evidence: user report from `2026-04-06 15:56` says jumping on the surface does not work and climbing slopes/shoreline edges feels blocked
- Problems: `HectonPlayerMovement` only accepted jump on the exact `_isWalking && _isGrounded` frame, while shoreline mode could drop out of `walking` on shallow-water/slope transitions and let `surface lock` fight the same movement window
- Short Comment: code-fixed; remaining work is live in-game / build verification
- Next Step: user/build verification on shoreline and shallow incline routes

- Did: added a short `jump buffer` and `shore ground grace` in `HectonPlayerMovement` so shallow shoreline movement no longer depends on a single exact grounded frame. The jump request now survives briefly until the next valid shore-support frame, shallow-water walk mode can hold through tiny ground-check gaps, and `surface lock` is suppressed during that shallow grace window instead of pushing against the same movement.
- Result: the code path now targets the reported symptom directly. Intended runtime effect: pressing jump near the waterline should still fire when the player is in a valid shallow-ground transition, and shoreline climbing should stop dropping into false swim/surface-lock behaviour on tiny contact losses.
- Addendum: the shoreline pass did not cover floating-at-surface breach. `HectonPlayerMovement` still had no non-grounded surface jump path, so a player bobbing at the waterline without shore support could press jump and get nothing while `ApplySurfaceLock()` kept ownership of vertical motion. Code truth now splits `shore jump` from `surface breach`: grounded shallow support still uses buffered land impulse, while a separate near-surface swimming gate can consume jump input, fire an upward breach impulse, and temporarily suppress `surface lock` so the same owner does not cancel the launch on the next physics step.
- Addendum: dry-land jump had a second hard physics defect. `SuitData.jumpImpulse` was still being applied through `ForceMode.Impulse` while suit masses are `80` and `400`, so the real velocity change was effectively near-zero. `HectonPlayerMovement` now applies jump as mass-independent vertical velocity change and clears the ground latch / snap state on launch, so `Space` on land or shoreline is no longer immediately re-pinned by the same frame's ground-stability path.
- Addendum: the underwater floor "magnet" was not just movement feel. `BuoyancyObject.IsInAir` treated any ground touch as dry-air state, and `HectonFluidEngine` used that flag to zero all buoyancy/drag/current. The fluid engine now suppresses water forces only for true dry zones or for grounded objects that are effectively above the waterline; underwater bottom contact keeps fluid forces alive instead of flipping into fake dry-land sliding.
- Addendum: dry-land movement still had a deeper owner bug after those passes. `HectonPlayerMovement` was using `_isWalking` as both `land locomotion` and `currently grounded`, so the first frame after takeoff on dry land could fall into `SwimPhysics` and underwater camera feel. Code truth now keeps dry-air movement in land locomotion, applies reduced land-air control / damping instead of water mode, and clears walk-bob carryover on jump launch so the takeoff wobble no longer inherits the previous footstep phase.
- Addendum: live readback exposed the deeper grounding fault behind the remaining dead-jump / weak-walk reports. `Rigidbody.position.y` on `Player` is the capsule center, not the feet (`CapsuleCollider.center.y = 0.9`, `height = 1.8`), but `GroundCheck()`, `ComputeImmersionRatio()`, `ComputeDepth()`, and surface-lock math were still reading it as foot level. Code truth now resolves body bottom/top/eye from the actual capsule bounds before doing grounding or waterline tests, so dry land no longer looks like permanent airborne state just because the root pivot sits at body center.
- Addendum: slope locomotion had its own force bug after grounding improved. `ApplyGroundStability()` was pushing a world-down snap force while grounded; on an incline that injects a downhill tangent and helps the player slide down while resisting uphill motion. Ground support and snap are now applied along the smoothed ground normal instead of world-down, so slope hold no longer comes with an artificial downhill shove.
- Addendum: uphill motion still had a second slope owner bug even after the snap-axis fix. `HectonPlayerMovement` was still applying full gravity every frame, but `ApplyGroundStability()` only cancelled the normal component, leaving the downhill tangent from gravity intact. That means uphill walk input had to fight a constant slope-slide force. Code truth now cancels the gravity component projected along the ground plane while grounded, so walkable slopes no longer get a built-in downhill drag from the same system that is supposed to stabilize contact.
- Addendum: grounding was still fragile on slope lips and wall-adjacent terrain because `GroundCheck()` accepted the first sphere-cast hit as valid ground. On inclines that can be a steep face, not the floor, which corrupts `_smoothedGroundNormal`, projects walk input into the wrong plane, and kills uphill motion or jump eligibility. Code truth now uses `SphereCastNonAlloc` with a reused hit buffer and only accepts contacts whose normal is within a walkable ground-angle threshold.
- Addendum: dry-land jump still had a one-frame dependency even after shoreline fixes. Only shallow-water support had grace time, so a dry slope lip or tiny terrain gap could still drop grounded for one physics step and eat `Space` or immediately switch to dry-air damping. Code truth now keeps a separate dry-ground grace timer for truly dry land, and the land jump gate / dry-air damping path respect that timer instead of requiring one perfect grounded frame.
- Addendum: sprint was inconsistent across movement modes. `Ctrl` only armed `_isSprinting` while already in walk mode, and the multiplier was only applied to grounded walk force / walk clamp. That meant underwater swim, surface-swim, and shallow locomotion all ignored the same input contract. Code truth now treats `Ctrl` as a movement-wide acceleration signal: land sprint respects dry/shore grace support, and swim sprint scales thrust, vertical thrust, and swim max speed with the same `SuitData.sprintMultiplier`.
- Addendum: locomotion still had no actual step solver. Even with better ground normals, small terrain lips and pseudo-stairs could still hard-stop the body because `HectonPlayerMovement` only had downward grounding, not forward step resolution. Code truth now adds a non-alloc local `step assist / lip assist` pass: detect a low forward obstacle, verify raised clearance, find walkable landing, and perform a bounded step-up correction with cooldown instead of relying on raw force alone.
- Addendum: jump was still missing ceiling safety. `HectonPlayerMovement` launched upward blindly, so under cave lips and overhangs the same input could slam into overhead geometry and produce ugly contact jitter. Code truth now performs a non-alloc capsule clearance check before accepting the jump launch; blocked headroom keeps the request buffered until the window expires instead of firing a bogus impulse.
- Addendum: walk clamp was still not slope-consistent. Move force was projected onto the ground plane, but land speed was clamped in world `XZ`, so incline tangential speed could still feel wrong even after slope-force fixes. Code truth now clamps grounded land velocity on the actual slope plane and preserves the normal component separately.
- Failed: no live build proof yet. I have compile/console confirmation only, not a new shoreline traversal test.
- Broke: no new compile errors detected; console remains limited to editor inspector null spam.
- Remaining: verify in live game/build with:
  - dry ground jump from full stop and while running
  - jump spam while partially submerged at the shoreline
  - jump while floating at the surface with no ground contact
  - underwater swim with repeated bottom touches: no dry-land pinning, no rail-slide along the seabed
  - walking up shallow wet slopes and rock lips
  - surfacing against an incline and then trying to climb out without losing control

## Next Build Question

After each new build, ask one short question:

`What breaks belief in the world the most right now?`

### [c] Build Compile Oracle Contaminated By `WorldProceduralScatterDirector`
- Status: [c]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: new flora verification is currently contaminated by a giant scatter-file compile blocker, so old compiled domains can still execute editor menus and fake a partial pass
- Evidence:
  - console compile errors on `2026-04-08`:
    - `Assets\\_Project\\Scripts\\WorldProceduralScatterDirector.cs(949,57): error CS0103: The name 'GetPreferredFamilyIndexForLayer' does not exist in the current context`
    - `Assets\\_Project\\Scripts\\WorldProceduralScatterDirector.cs(960,31): error CS1501: No overload for method 'GetBiomeMatrixBonus' takes 5 arguments`
    - follow-up argument mismatch errors on lines `961-963`
  - `validate_script` on `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs` reports widespread duplicate method signatures, indicating a corrupted edit history in that giant owner
  - coral parity pass symptom:
    - `Generate Procedural Flora Textures` still logs `TouchedTextures=12`, which is the old kelp-only count and proves the new coral texture branch did not enter the active compiled domain
- Problems:
  - this was a temporary stale compile-truth incident during the coral parity pass
- Short Comment: not reproduced on the next forced compile; no longer the active blocker for flora verification
- Next Step: only reopen if the same `WorldProceduralScatterDirector` compile errors recur on a fresh forced compile

### [c] Compile Oracle Blocked Again By Parallel Scatter Work
- Status: [c]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: flora branching beauty regeneration was temporarily blocked by a fresh compile failure in the giant scatter owner, but that blocker did not reproduce on the next clean refresh
- Evidence:
  - transient console on `2026-04-08`:
    - `Assets\\_Project\\Scripts\\WorldProceduralScatterDirector.cs(2233,17): error CS0246: The type or namespace name 'ScatterCandidatePreview' could not be found`
    - `Assets\\_Project\\Scripts\\WorldProceduralScatterDirector.cs(2254,16): error CS0246: The type or namespace name 'ScatterCandidatePreview' could not be found`
  - follow-up clean-domain verification:
    - `Generate Procedural Flora Baked Starters` -> `Prefabs=21, MeshesUpdated=60, RemovedAssets=0, Failures=0`
    - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
    - `Generate Procedural Flora Final Status Report`
- Problems:
  - no active flora-side compile blocker remains from this incident
  - the remaining blockers are now content quality and missing in-world/build evidence, not compile truth
- Short Comment: resolved as stale/parallel compile contamination; do not treat this as the current flora blocker unless it reproduces again on a fresh refresh
- Next Step: reopen only if the same scatter compile failure reproduces on a clean compile

### [x] Gas Giant Tick No Longer Resolves Renderer Resources In Hot Path
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `GasGiantRotationDriver.Tick()` was paying a per-tick `EnsureRendererResources()` guard that could still hit `GetComponent<Renderer>()` in runtime fallback paths
- Evidence:
  - `Assets/_Project/Scripts/GasGiantRotationDriver.cs`
  - renderer/resource resolution now lives in `Awake`, `OnEnable`, and editor-only `OnValidate`
  - `Tick()` now only early-outs on missing `_planetRenderer` and updates the MPB rotation value
- Problems:
  - no fresh Unity compile oracle yet
- Short Comment: owner-local hot-path smell removed without changing rotation semantics
- Next Step: verify on next live compile/readback

### [x] Celestial Sky Colors Resolved Once Per Tick
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `HectonCelestialEngine` was resolving the same sky colors in both sky and Aegir material paths inside one tick
- Evidence:
  - `Assets/_Project/Scripts/HectonCelestialEngine.cs`
  - `_resolvedSkyZenith/_resolvedSkyHorizon/_resolvedSkyNadir` now update once before `UpdateSkyMaterial()` and `UpdateAegirMaterial()`
  - sky material blend gate remains intact; only the duplicate color solve was removed
- Problems:
  - no fresh Unity compile oracle yet
- Short Comment: reduced duplicate per-tick color work without changing blend math or material write order
- Next Step: verify on next live compile/readback

### [x] Propulsion Lock Name Uppercase Cached At Lock Time
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `PropulsionTool` was rebuilding `_lockedName.ToUpperInvariant()` on the locked summary path and repeated lock-status emits
- Evidence:
  - `Assets/_Project/Scripts/PropulsionTool.cs`
  - `_lockedNameUpper` now resolves once during `TryAcquireLock()`
  - summary / hold / launch feedback reuse the cached uppercase string
  - release path clears both `_lockedName` and `_lockedNameUpper`
- Problems:
  - no fresh Unity compile oracle yet
- Short Comment: owner-local string work reduced on a frequently polled lock state without touching the shared `PlayerTool` contract
- Next Step: verify on next live compile/readback

### [x] Propulsion No-Lock Assessment Shared Per Frame
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `PropulsionTool` could raycast and build the same no-lock assessment twice in one frame when summary and directive were both requested
- Evidence:
  - `Assets/_Project/Scripts/PropulsionTool.cs`
  - `TryGetAssessmentCached()` now shares one current-frame assessment result
  - lock acquire/release paths explicitly invalidate the cached assessment
- Problems:
  - no fresh Unity compile oracle yet
  - frame-local cache can still be stale if external world state changes between two UI reads in the same frame
- Short Comment: duplicate same-frame propulsion assessment work removed without changing inter-frame tool behavior
- Next Step: verify on next live compile/readback

### [x] Harpoon Tether Name Uppercase Cached At Tether Registration
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `HarpoonLauncherTool` was re-running uppercase conversion for tether headline/summary feedback on a tether state that can be polled repeatedly
- Evidence:
  - `Assets/_Project/Scripts/HarpoonLauncherTool.cs`
  - `_tetheredNameUpper` now resolves once during `TryRegisterTether()`
  - tether lock summary and tether reel feedback reuse the cached uppercase string
  - `ClearTether()` clears both tether name fields
- Problems:
  - no fresh Unity compile oracle yet
  - dead local helper remains in file as non-runtime cleanup debt because the file tail has encoding noise; runtime call sites no longer use it
- Short Comment: repeated tether-name string work removed from live harpoon state without changing tether cadence or reel behavior
- Next Step: verify on next live compile/readback

### [x] Harpoon No-Tether Assessment Shared Per Frame
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `HarpoonLauncherTool` could raycast and build the same no-tether assessment twice in one frame when summary and directive were both requested
- Evidence:
  - `Assets/_Project/Scripts/HarpoonLauncherTool.cs`
  - `TryGetAssessmentCached()` now shares one current-frame no-tether assessment result
  - tether register/clear paths invalidate the cached assessment explicitly
- Problems:
  - no fresh Unity compile oracle yet
  - frame-local cache can still be stale if external world state changes between two UI reads in the same frame
- Short Comment: duplicate same-frame harpoon assessment work removed without changing inter-frame tether behavior
- Next Step: verify on next live compile/readback

### [x] Scanner Mode Strings Cached As Finite Mode State
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `ScannerTool` was repeatedly rebuilding the same mode label / mode summary / mode feedback strings from three deterministic modes
- Evidence:
  - `Assets/_Project/Scripts/ScannerTool.cs`
  - `_currentModeLabel/_currentModeSummary/_currentModeHudMessage/_currentModeOperationTitle` now refresh only on `Awake` and mode switch
  - summary/directive/mode-change feedback reuse those cached strings
- Problems:
  - no fresh Unity compile oracle yet
- Short Comment: finite scanner mode text moved to cached owner state without touching scan result semantics or pulse logic
- Next Step: verify on next live compile/readback

### [x] Analyzer Target Assessment Shared Per Frame
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `EnvironmentalAnalyzerTool` could raycast and rebuild the same target assessment twice in one frame when summary and directive were both requested
- Evidence:
  - `Assets/_Project/Scripts/EnvironmentalAnalyzerTool.cs`
  - `TryGetTargetAssessmentCached()` now shares one current-frame target assessment result
  - primary and secondary analyzer actions explicitly invalidate the cached assessment after their own emits
- Problems:
  - no fresh Unity compile oracle yet
  - frame-local cache can still be stale if external world state changes between two UI reads in the same frame
- Short Comment: duplicate same-frame analyzer assessment work removed with explicit post-use invalidation
- Next Step: verify on next live compile/readback

### [x] Beacon Nearest Assessment Shared Per Frame
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `BeaconDeployerTool` could resolve nearest beacon assessment twice in the same frame when HUD/PDA asked for both summary and directive
- Evidence:
  - `Assets/_Project/Scripts/BeaconDeployerTool.cs`
  - `TryGetNearestAssessmentCached()` now shares one nearest-beacon read/assessment per frame
  - deploy and retract paths explicitly invalidate the cached frame data
- Problems:
  - no fresh Unity compile oracle yet
  - frame-local cache can still be stale if some external system mutates the beacon grid between two UI reads in the same frame
- Short Comment: duplicate same-frame nearest-beacon work removed without changing inter-frame beacon behavior
- Next Step: verify on next live compile/readback

### [x] Beacon Operational Text Shared Per Frame
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: after nearest assessment sharing, `BeaconDeployerTool` could still format the same summary/directive strings multiple times in one frame
- Evidence:
  - `Assets/_Project/Scripts/BeaconDeployerTool.cs`
  - `RefreshOperationalTextCache()` now builds summary/directive once per frame
  - deploy/retract invalidation also clears the operational text cache
- Problems:
  - no fresh Unity compile oracle yet
  - frame-local text cache shares the same external-mutation edge case as the nearest-assessment cache
- Short Comment: duplicate same-frame beacon HUD text formatting removed without changing inter-frame semantics
- Next Step: verify on next live compile/readback

### [x] Stun Pistol Assessment Shared Per Frame
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `StunPistolTool` could resolve the same target assessment twice in one frame when HUD/PDA requested both summary and directive
- Evidence:
  - `Assets/_Project/Scripts/StunPistolTool.cs`
  - `TryGetAssessmentCached()` now shares one target read/assessment per frame
  - primary and secondary action paths explicitly invalidate the cached frame data after each probe outcome
- Problems:
  - no fresh Unity compile oracle yet
  - frame-local cache can still be stale if some external system mutates target state between two UI reads in the same frame
- Short Comment: duplicate same-frame stun target assessment work removed without changing inter-frame behavior
- Next Step: verify on next live compile/readback

### [x] Knife Contact Probe Shared Per Frame
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `KnifeTool` could run the same `SphereCastNonAlloc` contact probe twice in one frame when HUD/PDA requested both summary and directive
- Evidence:
  - `Assets/_Project/Scripts/KnifeTool.cs`
  - `TryGetBestHitCached()` now shares one best-hit probe per frame for summary/directive reads
  - primary swing and secondary tactical read still use their own direct probe path
- Problems:
  - no fresh Unity compile oracle yet
  - frame-local cache can still be stale if some external system mutates contact state between two UI reads in the same frame
- Short Comment: duplicate same-frame knife contact probing removed without changing blade action truth
- Next Step: verify on next live compile/readback

### [x] Repair Diagnosis Shared Per Frame
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `RepairTool` could resolve the same service diagnosis twice in one frame when HUD/PDA requested both summary and directive
- Evidence:
  - `Assets/_Project/Scripts/RepairTool.cs`
  - `TryGetServiceDiagnosisCached()` now shares one service diagnosis read per frame
  - primary and secondary service paths explicitly invalidate cached diagnosis data after each action outcome
- Problems:
  - no fresh Unity compile oracle yet
  - frame-local cache can still be stale if some external system mutates module state between two UI reads in the same frame
- Short Comment: duplicate same-frame repair diagnosis work removed without changing repair action semantics
- Next Step: verify on next live compile/readback

### [x] Flashlight Adapter Snapshot Shared Per Frame
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `FlashlightTool` could read `PlayerFlashlight` operational strings and forward context more than once in the same frame across summary/directive/assessment paths
- Evidence:
  - `Assets/_Project/Scripts/FlashlightTool.cs`
  - `TryGetOperationalSnapshot()` now shares one summary/recommendation snapshot per frame
  - `TryGetForwardContextDirectiveCached()` now shares one context directive result per frame
  - toggle and beam-mode mutation paths explicitly invalidate both caches
- Problems:
  - no fresh Unity compile oracle yet
  - frame-local cache can still be stale if some external system mutates flashlight/context state between two UI reads in the same frame
- Short Comment: duplicate same-frame flashlight adapter reads removed without moving ownership out of `PlayerFlashlight`
- Next Step: verify on next live compile/readback

### [x] Builder Bridge Operational Text Shared Per Frame
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `BuilderTool` could rebuild the same bridge summary/directive strings multiple times in one frame when HUD/PDA requested both values
- Evidence:
  - `Assets/_Project/Scripts/BuilderTool.cs`
  - `RefreshOperationalCache()` now builds one summary/directive pair per frame
  - spawn/despawn/equip/unequip and builder action paths explicitly invalidate that cache
- Problems:
  - no fresh Unity compile oracle yet
  - frame-local cache can still be stale if some external system mutates builder state between two UI reads in the same frame
- Short Comment: duplicate same-frame builder bridge text assembly removed without moving logic into `PlayerBuilder`
- Next Step: verify on next live compile/readback

### [x] Sampler Diagnosis Shared Per Frame
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `SalvageSamplerTool` could resolve the same salvage diagnosis twice in one frame when HUD/PDA requested both summary and directive
- Evidence:
  - `Assets/_Project/Scripts/SalvageSamplerTool.cs`
  - `TryGetDiagnosisCached()` now shares one sampler diagnosis read per frame
  - primary and secondary sampler action paths explicitly invalidate cached diagnosis data after each outcome
- Problems:
  - no fresh Unity compile oracle yet
  - frame-local cache can still be stale if some external system mutates target state between two UI reads in the same frame
- Short Comment: duplicate same-frame sampler diagnosis work removed without changing sampling action semantics
- Next Step: verify on next live compile/readback

### [x] Player Tool Base Name Cached
- Status: [x]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: base `PlayerTool.GetOperationalSummary()` recomputed uppercase tool name on every HUD read for tools that still use the base summary path
- Evidence:
  - `Assets/_Project/Scripts/PlayerTool.cs`
  - uppercase operational tool name is now cached on spawn with a lazy fallback getter
  - base summary path now reuses that cached name instead of calling `ToUpperInvariant()` every time
- Problems:
  - no fresh Unity compile oracle yet
  - this is a base-layer change, so compile/runtime truth matters more than code review alone
- Short Comment: removed repeated base summary name allocation without changing default naming fallback semantics
- Next Step: verify on next live compile/readback

### [x] Flora Automation Preview Work Staged Per Update
- Status: [x]
- Need User Check: yes
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `WorldProceduralFloraFinalStatusReport` could re-run heavy `PreviewRenderUtility` prefab capture for the same pending task on every editor update during automation preview generation
- Evidence:
  - `Assets/_Project/Scripts/Editor/WorldProceduralFloraFinalStatusReport.cs`
  - preview queue now processes a bounded number of tasks per editor update
  - direct prefab capture is attempted once per task, then fallback work uses `AssetPreview` polling instead of repeated full prefab preview rendering
  - completed tasks clear cached prefab asset references immediately
- Problems:
  - no fresh Unity compile oracle yet
  - if automation payload is extremely large, preview completion latency may increase because work is now intentionally staged
- Short Comment: cut editor RAM churn during flora preview automation without removing report/preview output
- Next Step: verify during a real flora automation session with RAM observation

### [x] Scene View Sky Enforcer Throttled And Dirtied
- Status: [x]
- Need User Check: yes
- Need Build Check: yes
- Need In-World Swim Check: no
- Why: `SceneViewSkyboxEnforcer` wrote scene-view defaults and re-resolved source sky objects on every editor update while Scene view stayed open
- Evidence:
  - `Assets/_Project/Scripts/Editor/SceneViewSkyboxEnforcer.cs`
  - editor default enforcement now runs on a bounded interval instead of every update
  - source sky sphere resolution is cached and invalidated on hierarchy changes
  - preview pose refresh now happens on scene-view camera rendering instead of unconditional editor-update work
- Problems:
  - no fresh Unity compile oracle yet
  - scene-view sky defaults may now restore with a short bounded delay instead of instantly on the same editor tick
- Short Comment: reduced always-on editor churn without removing scene-view sky preview ownership
- Next Step: verify long idle Scene-view session and watch RAM / responsiveness

### [x] Visor HUD Edit-Mode Helpers Self-Unsubscribe When Settled
- Status: [x]
- Need User Check: yes
- Need Build Check: no
- Need In-World Swim Check: no
- Why: the visor/HUD preview stack (`SuitHUDScreenCompositor`, `SuitHUDPresentationController`, `SuitHUDV4CanvasOverlay`, `HectonSuitHUDExtensions`) stayed subscribed to `EditorApplication.update` for the whole editor session even after their edit-mode preview work had settled
- Evidence:
  - `Assets/_Project/Scripts/Visor/SuitHUDScreenCompositor.cs`
  - `Assets/_Project/Scripts/Visor/SuitHUDPresentationController.cs`
  - `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`
  - `Assets/_Project/Scripts/HectonSuitHUDExtensions.cs`
  - each owner now evaluates whether edit-mode preview work is still needed, self-unsubscribes when settled, and re-arms on `OnValidate` or explicit refresh requests
- Problems:
  - long-lived edit-mode preview ownership still remains when these systems are intentionally active
  - preview re-arming now depends on owner invalidation paths staying complete
- Short Comment: cut always-on visor/HUD editor churn without stripping preview functionality
- Next Step: verify a long idle editor session with visor scene objects present and watch RAM slope

### [x] Save Thumbnail Caches Bounded
- Status: [x]
- Need User Check: yes
- Need Build Check: no
- Need In-World Swim Check: no
- Why: `SaveThumbnailSystem` and `SaveSlotManagerWindow` retained every loaded thumbnail sprite/texture until global cache clear or window close, which is direct RAM retention in long save-management/editor sessions
- Evidence:
  - `Assets/_Project/Scripts/SaveThumbnailSystem.cs`
  - `Assets/_Project/Editor/SaveSlotManagerWindow.cs`
  - runtime sprite cache is now bounded and evicts least-recently-used entries by destroying both sprite and texture
  - editor thumbnail texture cache is now bounded and evicts least-recently-used textures immediately
- Problems:
  - first access to an evicted thumbnail now reloads from disk, so very large slot lists may trade some IO for bounded RAM
  - compile truth is clean, but live memory slope still needs a real save-window session
- Short Comment: removed unbounded thumbnail retention without changing thumbnail load/delete ownership
- Next Step: open Save Slot Manager, scroll through many slots, then watch whether RAM plateaus instead of climbing

### [x] Visor HUD Controller Edit Tick Gated To Real Preview Need
- Status: [x]
- Need User Check: yes
- Need Build Check: no
- Need In-World Swim Check: no
- Why: `VisorHUDController` stayed on `EditorApplication.update` after `OnEnable()` even when edit-mode preview was already settled, keeping another `ExecuteAlways` owner hot for the full editor session
- Evidence:
  - `Assets/_Project/Scripts/Visor/VisorHUDController.cs`
  - `ShouldTickInEditMode()` now keeps editor ticking only for dirty material state, unresolved refs, or explicit edit-mode pose sync
  - `OnEnable()` / `OnValidate()` now evaluate whether edit tick should exist instead of subscribing unconditionally
  - fresh Unity compile/readback after this pass returned `0` console errors/warnings from touched files
- Problems:
  - if some future edit-mode preview invalidation path forgets to re-arm, preview can look stale until the next explicit validate/change
  - live memory improvement is still unproven without an hour-scale idle sample
- Short Comment: removed another always-on visor preview owner from idle editor sessions without touching runtime projection ownership
- Next Step: leave visor scene open in edit mode and observe whether RAM / editor activity plateaus

### [x] Environment Preview Stops Burning Background Ticks
- Status: [x]
- Need User Check: yes
- Need Build Check: no
- Need In-World Swim Check: no
- Why: `HectonAtmosphereManager`, `HectonUnderwaterVisuals`, and `HectonCelestialEngine` kept running full edit-mode preview ticks even when the Unity editor window was inactive; `HectonUnderwaterVisuals` also kept probing editor cameras too aggressively
- Evidence:
  - `Assets/_Project/Scripts/HectonAtmosphereManager.cs`
  - `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
  - `Assets/_Project/Scripts/HectonCelestialEngine.cs`
  - all three editor tick paths now early-out when `InternalEditorUtility.isApplicationActive` is false
  - `HectonUnderwaterVisuals.ResolveEditorCamera()` now prefers cached SceneView/game cameras and retries `Camera.main` only on a bounded interval instead of every editor update
  - fresh Unity compile/readback after this pass returned `0` console errors/warnings from touched files
- Problems:
  - when Unity is unfocused, edit-mode atmosphere/underwater/celestial preview intentionally pauses until focus returns
  - live RAM slope improvement is still unproven without a long idle sample
- Short Comment: background editor environment preview now backs off instead of burning full update work while the editor is idle in the background
- Next Step: leave Unity unfocused for 20-60 minutes in the same scene and compare resident/texture memory slope against the old behavior

### [c] Fauna Spawn Registry Stops Allocating In Anchor Selection
- Status: [c]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: `WorldFaunaSpawnRegistry` is live runtime ownership for ordinary fauna anchors and large-threat macro-zone anchors, but both nearest-anchor queries were still iterating `Dictionary<long, Anchor>` through `foreach`
- Evidence:
  - `Assets/_Project/Scripts/WorldFaunaSpawnRegistry.cs`
  - `TryGetLargeThreatZone(...)` now uses explicit `Dictionary<long, Anchor>.Enumerator`
  - `TryGetNearestOrdinaryAnchor(...)` now uses explicit `Dictionary<long, Anchor>.Enumerator`
  - selection semantics, distance checks, and procedural-state availability gates were not changed
- Problems:
  - compile/build truth still needs a fresh Unity pass
  - this only removes hot-path iteration debt; it does not yet connect dormant GPU boid groundwork into the fauna runtime
- Short Comment: passive-fauna and macro-zone anchor selection no longer pays forbidden dictionary-foreach churn in the live spawn registry
- Next Step: recompile, then observe fauna spawn and large-threat zone behavior on a real swim route

### [c] Relax Phase No Longer Reintroduces New Predator Pressure Through Normal Spawn
- Status: [c]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: `SetPredatorPressure(false)` already pushed live creatures into `Wander`, but the ordinary fauna spawn loop could still pick new aggressive / hunter / leviathan entries because `TrySelectResolvedEntry(...)` ignored pressure state entirely
- Evidence:
  - `Assets/_Project/Scripts/FaunaDirector.cs`
  - resolved fauna cache now precomputes `blockedWhenPressureDisabled`
  - ordinary weighted selection now skips those entries while `_pressureEnabled == false`
  - horde spawn semantics stay unchanged because `ForceSpawnHorde(...)` was already blocked by relax pressure truth
- Problems:
  - compile/build truth still needs a fresh Unity pass
  - this still does not wire GPU boid schools into live ecology ownership; it only prevents relax-phase predator backfill through the normal pooled spawn loop
- Short Comment: relax phase now suppresses new aggressive predator re-entry through normal fauna spawning instead of only calming creatures that already existed
- Next Step: recompile, then verify on a relax-phase swim that passive fauna can still appear while new predator pressure stays suppressed

### [c] Cave Detail Builders Stop Reformatting Child Names On Every Runtime Build
- Status: [c]
- Need User Check: no
- Need Build Check: yes
- Need In-World Swim Check: yes
- Why: live cave-detail builders for service remnants, wall growth, and glowing tissue were still formatting `"Name_{index}"` strings inside their runtime build loops
- Evidence:
  - `Assets/_Project/Scripts/CaveServiceRemnantRuntimeBuilder.cs`
  - `Assets/_Project/Scripts/CaveWallGrowthRuntimeBuilder.cs`
  - `Assets/_Project/Scripts/CaveGlowingTissueRuntimeBuilder.cs`
  - all three builders now use bounded cold name caches sized to their own runtime count caps instead of interpolation inside the build loop
- Problems:
  - compile/build truth still needs a fresh Unity pass
  - this is a zero-GC cleanup only; it does not yet solve the higher-level `ruins as memory places` content gap
  - Wave 7 runtime mix was still too flat: `WorldProceduralScatterDirector` could satisfy service-water structure quotas with repeated generic tech families instead of preserving a readable mix of service scars, power routes, and ruin modules
- Short Comment: active cave readability layers keep the same child naming semantics without paying repeated runtime string churn
- Did Addendum: `WorldProceduralScatterDirector` now runs a bounded service-domain rescue pass during `IndustrialService` / `BrineToxic` structure injection. It uses the existing candidate pool and tries to keep at least one `ServiceScar`, one `PowerRoute`, and a supported `RuinModule` present when the structure budget allows, without changing public enums, asset contracts, or generic structure accent logic.
- Did Addendum: `WorldProceduralScatterDirector` still let solitary ruin fragments compete too evenly with ruin clusters/landmarks in strong route memory patterns. The runtime score now gives `RuinModule` families a bounded placement-mode bonus in `LandmarkCorridor`, `IndustrialService`, and `BrineToxic`, so ruin clusters and landmark-scale ruins win more often when landmark/salvage signal is high, while solitary fragments lose priority in those strongest reads.
- Did Addendum: `WorldProceduralScatterDirector` still allowed service-like water to satisfy trace readability without nearby payoff clusters. The runtime rescue path now also preserves at least one `DebrisField` and, when cluster budget allows, one `ResourcePocket` inside `IndustrialService` / `BrineToxic` windows by using the existing cluster rescue pool instead of new authoring data or new runtime systems.
- Did Addendum: `WorldProceduralScatterDirector` still treated those support/payoff clusters mostly as rescue exceptions instead of first-class quota pressure. Service-water cluster min/ratio logic now also pushes `DebrisField` and `ResourcePocket` upward while pushing unrelated fertile/shelter accents down inside `IndustrialService` / `BrineToxic`, so the rescue path is backed by the base quota contract.
- Did Addendum: `WorldProceduralScatterDirector` still left ruin state readability mostly to score drift. A bounded ruin-placement-mode rescue now preserves `RuinModule` `Cluster` variants and, when structure budget allows, `Landmark` variants inside `LandmarkCorridor`, `IndustrialService`, and `BrineToxic`, so those windows do not collapse into one generic ruin read.
- Did Addendum: `LandmarkCorridor` still relied too much on score drift for nearby payoff after the ruin-state pass. `WorldProceduralScatterDirector` now runs a bounded `ResourcePocket` cluster rescue there as well, so ruin/cave corridors keep at least one resource-side cluster when budget and live candidates support it.
- Perf Addendum: those recent service/ruin rescues were themselves doing unnecessary CPU work by re-sorting the same `rescueCandidates` buffer and re-scanning `_desiredPlacements` several times per window. `WorldProceduralScatterDirector` now computes the ordered rescue list and current ruin/service counts once per wrapper and reuses them across the sub-passes.
- Perf Addendum: preferred family rescue wrappers were still re-running `CountPlacedFamily(...)` for every preferred family, which meant another `_desiredPlacements` scan per family even after the wrapper had already built its ordered candidate list. `WorldProceduralScatterDirector` now builds one reusable preferred-family count map per wrapper and updates it only after successful injects, so cluster/structure/spawn preferred-family rescue no longer keeps rescanning the full desired-placement table inside the same window.
- Perf Addendum: service-domain rescue and ruin-placement-mode rescue were still doing three separate domain scans and two separate ruin-mode scans per window. `WorldProceduralScatterDirector` now counts service domains and ruin placement modes in single wrapper-local enumerator passes, so those rescue wrappers no longer pay multiple full `_desiredPlacements` walks for the same structure-layer state.
- Perf Addendum: cluster rescue still rebuilt `_occupiedCellBuffer` multiple times inside the same window, especially through `InjectPatternClusterAccentCandidates(...)` and the service/landmark cluster rescue passes. `WorldProceduralScatterDirector` now rebuilds cluster occupancy once at the wrapper level and reuses that live buffer across the sub-passes, relying on successful injects to keep the buffer current instead of recomputing it again for each accent role.
- Perf Addendum: the early cluster rescue chain was still paying three wrapper-level cluster occupancy rebuilds in the same window before preferred-family, service-accent, and landmark-corridor passes even started. `WorldProceduralScatterDirector` now rebuilds cluster occupancy once before that whole chain and lets those three wrappers reuse the same live `_occupiedCellBuffer`, which they already keep current after successful injects.
- Hardening Addendum: reconcile sync-signature was still seeded by a magic number and an implicit field list. `WorldProceduralScatterDirector` now uses an explicit `ScatterPlacementSyncSignatureVersion` seed, so future field-contract changes have a visible version bump point instead of hiding that dependency in `17`.
- Perf Addendum: per-window structure/spawn budget dictionaries were still relying only on their cold constructor capacity (`256`) and could resize when the sampled scatter radius/stride demanded more unique windows. `WorldProceduralScatterDirector` now estimates current window cardinality up front and calls `EnsureCapacity(...)` on `_structureWindowCounts` / `_spawnWindowCounts` before the rebuild loop starts.
- Diagnostics Addendum: `enableScatterDetailedDiagnostics` was still a runtime toggle in `WorldProceduralScatterDirector`, which meant release-player code paths could still keep detailed scatter diagnostics alive behind a bool. Detailed scatter diagnostics are now hard-disabled outside `UNITY_EDITOR || DEVELOPMENT_BUILD`, and release spike logging no longer depends on that toggle.
- Perf Addendum: `TryInjectCandidate(...)` was still doing duplicate per-window budget work for every structure/spawn inject attempt: choosing the same dictionary twice, composing the same window key twice, and paying separate read/write lookups around `RegisterWindowPlacement(...)`. It now resolves the target window-count dictionary once, composes the window key once, checks the current count once, and reuses that same key for the successful registration path.
- Perf Addendum: `CandidateMap` window tracking was still doing duplicate linear scans in the `ref CandidateMap` path: `TrackWindowCandidate(...)` first called `TryGetIndex(...)` and then paid a second full key scan through `TryAdd(...)` before retaining the placement. `WorldProceduralScatterDirector` now appends through a known-unique path after the existing index miss and only calls `RetainPlacement(...)` if the append actually succeeds, so candidate-window tracking no longer rescans the same key set or over-retains on capacity failure.
- Perf Addendum: structure/spawn rescue wrappers were still re-sorting the same `structureCandidates` / `spawnCandidates` pools across adjacent sub-passes in the same window. `WorldProceduralScatterDirector` now builds the ordered structure/spawn candidate view once at the wrapper level and passes that shared list into preferred-family, service-domain, ruin-mode, and preferred-spawn injectors instead of re-running `FillOrderedCandidateBuffer(...)` for each one.
- Diagnostics Addendum: `CandidateMap` near-capacity / overflow warnings were still living directly in the candidate tracking path with string-formatted `Debug.LogWarning(...)` calls. Those warnings are now compile-stripped to `UNITY_EDITOR || DEVELOPMENT_BUILD` and one-shot gated, so release players no longer keep that diagnostic string/log path alive if a candidate pool hits its limit.
- Architecture Addendum: vertical runtime ownership was still implicit even though `BiomeMatrixDirector` already resolved player depth tiers. It now publishes first-class `CurrentDepthTier` / `CurrentDepthMeters` state plus `OnDepthTierChanged`, so depth-band systems have a cheap owner signal instead of inferring depth meaning from scatter refresh or horizontal biome events.
- Runtime Addendum: `ScatterBudgetController` and `WorldStreamingDirector` were both recomputing depth from `MapMagicBridge.WaterSurfaceLevel - player.y`, even though `HectonPlayerMovement` already maintains current depth. Both controllers now prefer cached `HectonPlayerMovement.CurrentDepth` with bridge fallback, so deep/shallow budget switching no longer depends on duplicate water-surface math when the player movement owner is already live.
- Result Addendum: service-heavy water should read less like repeated anonymous tech fragments and more like layered human footprint built from the existing Wave 7 families.
- Result Addendum: ruin-heavy routes should bias more toward readable remembered places and less toward random small-module clutter, without requiring new authoring data or public API changes.
- Result Addendum: service traces should more often carry scavenging/support payoff nearby instead of resolving as readable traces with no cluster-layer follow-through.
- Result Addendum: landmark ruin/cave corridors should more often carry a nearby resource-side reason to stop and inspect, instead of depending only on scoring luck.
- Result Addendum: key ruin/service corridors should preserve a more legible mix of ruined states instead of relying on one placement mode winning by score accident.
- Result Addendum: this depth-state pass does not wake scatter on Y motion, so shallow-water horizontal scatter cadence stays intact while deeper runtime controllers get a cleaner vertical signal path.
- Runtime Addendum: `HectonUnderwaterVisuals` was still driven only by `MapMagicBridge.OnBiomeChanged`, so matrix/depth ownership could not actually steer underwater visuals. It now subscribes to `BiomeMatrixDirector.OnMatrixBiomeChanged` as an optional override source and only applies `runtimeVisualProfile` when the current matrix biome explicitly provides one; otherwise it stays on the existing palette/index path.
- Runtime Addendum: `HectonAtmosphereManager` still left `HectonBiomeFamilyProfile.atmosphereProfile` effectively dead, because surface atmosphere only knew about `MapMagicBridge` biome overrides and underwater always forced `_profileUnderwater`. It now also listens to `BiomeMatrixDirector.OnMatrixBiomeChanged` and applies an optional family-level atmosphere override for surface states only; underwater ownership is unchanged.
- Runtime Addendum: `AcousticZoneController` was still polling `HectonAtmosphereManager.CurrentState` every tick for the exterior surface/underwater branch even though the atmosphere owner already publishes `OnStateChanged`. It now caches the exterior acoustic zone from that event and keeps per-frame work only for the real edge case that still needs it: dry-zone / interior detection.
- Runtime Addendum: `SuitHUDV4CanvasOverlay` was still resolving depth inline every runtime tick even though the overlay already owns stable survival/player references and only needed a cheap vertical signal. It now caches depth from `HectonSurvivalSystem.OnDepthChanged` and only falls back to `HectonPlayerMovement.CurrentDepth` when survival is unavailable, so the overlay stops re-polling survival depth for its temperature/status path.
- Result Addendum: deep/vertical matrix-owned water can now push an explicit underwater visual profile without waking scatter on Y movement and without replacing shallow MapMagic biome behavior when no matrix override is authored.
- Result Addendum: matrix-owned families can now steer surface atmosphere through their own `atmosphereProfile` without replacing the cheap shallow `MapMagicBridge` fallback and without touching underwater atmosphere ownership.
- Result Addendum: the audio transition owner now follows vertical surface/underwater state through a cached event-fed path instead of a repeated atmosphere-state poll, while still preserving dry interior transitions and movement-depth fallback when atmosphere state is unavailable.
- Result Addendum: the canvas-overlay HUD now stays on the same cached vertical signal spine as the immediate-mode HUD, so depth/pressure/temperature overlay reads do not depend on repeated live survival depth polling when a depth event already exists.
- Result Addendum: early cluster rescue no longer pays repeated wrapper-level occupancy rebuilds before the same-window cluster sub-passes, so the main scatter offender sheds another narrow duplicate-work tail without changing cluster placement contract.
- Runtime Addendum: `HectonSuitHUD_v4` was still resolving temperature depth through `HectonUnderwaterVisuals.CurrentDepth` even though the HUD already owns cached `_depthMeters` via survival/player movement signals. It now stays on the cached HUD depth path, and when survival is absent it refreshes `_depthMeters` from `HectonPlayerMovement.CurrentDepth` before pressure/temperature evaluation instead of leaving stale depth in place.
- Result Addendum: the HUD vertical readout path no longer cross-polls underwater visuals for temperature estimation, so depth-driven visor telemetry stays on its own cheap owner signal and remains valid even when survival is unavailable.
- VRAM Addendum: sky / gas-giant texture imports were intentionally left untouched. `HectonCelestialEngine` now detaches its own celestial texture references only below `1000 m`, so deep-water runtime can drop sky-sphere, day/night skybox, blended skybox cubemap, and gas-giant material residency without changing authored texture sizes or shallow-water visuals.
- VRAM Addendum: current live `RT RED` snapshot is coming from idle unfocused editor, not play mode. `VisorHUDController` now suspends its edit-mode projection preview when the Unity editor loses focus: it unbinds the HUD camera target texture, disables the HUD preview camera, releases any owned runtime RT, and restores the projection only when focus returns. This is an editor-only sleep path; play mode and shallow runtime visor behavior are unchanged.
- VRAM Addendum: the next proven editor-side render owner is URP decals, not first-party TAA. Live scene inspection showed `0` `DecalProjector` objects and no active `DynamicDecals` components, while `PC_Renderer.asset` still keeps `DecalRendererFeature` active in `DBuffer` mode. The package feature now fail-closes when `m_DecalEntityManager.chunkCount == 0`, so empty-decals cameras stop enqueuing the `CopyDepth + DBuffer + ForwardEmissive` chain at all.
- Result Addendum: this is not a project-settings change and not a texture downgrade. It is an owner-local render-feature suppression path for the exact case where there are no registered decal entities to draw.
- VRAM Addendum: the next editor-side owner cut now hits unfocused non-game editor cameras directly. `ScreenSpaceAmbientOcclusion`, `ScreenSpaceShadows`, `ShapesRenderFeature`, and `DecalRendererFeature` now all fail-close for non-`Game` cameras when the Unity editor is unfocused, so SceneView / Preview / inspector-preview cameras stop enqueuing those feature passes while nobody is looking at the editor.
- Result Addendum: after the compile blocker was neutralized and the guards actually entered the live domain, the unfocused editor snapshot moved from the pre-pass high-water mark of `981` render textures / `~2051 MB` graphics driver memory to a repeated post-reload snapshot of `978` render textures / `~1687-1714 MB` graphics driver memory. This is only a partial reduction and `RT RED` remains open.
- Failed: no new swim/build proof yet that these domains now read correctly in-world; this is code truth only.
- Failed: `render_textures_bytes` is still about `1.49 GB` in idle unfocused editor and `RT RED` is still unresolved. The new guards reduced editor-side pressure, but they did not identify or clear the full retained RT owner set.
- Runtime Addendum: `HectonUnderwaterVisuals` was still leaving the player camera stack's `SpaceCamera` alive all the way into aphotic depths even though the vertical path already owns the underwater transition and `SpaceCamera` only exists to render the celestial layer. It now zeros `SpaceCamera.cullingMask` below `1000 m` while underwater and restores the original celestial mask on return, so deep water stops paying that sky-layer render path without touching shallow-water behavior or the camera stack contract.
- Result Addendum: the new deep-water celestial cut is narrow and fail-safe. It does not wake scatter on Y motion, does not change shallow/twilight rendering, and restores the original `SpaceCamera` culling mask on disable or ascent.
- Next Step: recompile, then verify cave detail layers still build and recycle cleanly in a live cave route
