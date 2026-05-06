# Critical Action Queue

Date: 2026-05-07
Status: PENDING VERIFICATION

## Priority 0

1. Restore trustworthy editor-state observability.
Reason: Right now console truth, log truth, MCP readiness, and editor responsiveness do not form one reliable verification surface.

2. Remove remaining forced job completion from runtime streaming retirement paths.
Reason: `27_PLAYMODE_DEADLOCK_STATIC_AUDIT.md` fixed pending chunk cancellation, but `HectonWorldGenerator` can still force-complete active chunk PhysX bakes before mesh destruction. This remains a concrete Play Mode stall candidate.

3. Freeze authority drift.
Reason: Choose the runtime sovereign path between registry/dispatcher/bootstrap versus legacy singleton residue. Stop adding more mixed ownership.

4. Clean `02_HECTON_WORLD` as a truth surface.
Reason: Separate production runtime roots from debug, preview, trial, and temporary residue.

5. Runtime-test the spatial-hash handle contract.
Reason: the earlier source-level claim that `HectonSpatialHash` only used monotonic `_nextHandle++` is stale. Current source has slot/generation handles, queued-free duplicate guard, and current-handle validation. The remaining P0 work is runtime churn proof under register/unregister pressure, not another blind source rewrite.

## Priority 1

1. Audit every `.Complete()` in hot or cadence-sensitive owners.
Reason: The project uses Jobs/Burst for real work, then repeatedly pays back the benefit with barriers.

2. Add watchdog diagnostics to scene and async job waits.
Reason: `SceneRuntimeService`, `GameBootstrapper`, `HectonVoxelEngine`, and `ProceduralWreckGenerator` yield instead of spin, but several waits have no failure deadline. This can look like Play Mode deadlock even when CPU is low.

3. Profile synchronous `.Run()` jobs before converting them.
Reason: `CraftingSystem`, `PlayerInventory`, `TetherInstance`, and `FloraInteractionManager` use synchronous jobs. This is not a deadlock root by itself, but it can be a frame-time stall source.

4. Decompose the worst monoliths.
Primary suspects:
- `HectonMapMagicVegetationBridge`
- `HectonPlayerMovement`
- `WorldProceduralScatterDirector`
- `FaunaDirector`
- `HectonVoxelEngine`

5. Produce a hard migration map for `Instance` and `DontDestroyOnLoad` owners.
Reason: `GlobalRegistry` cannot become authoritative while parallel sovereignty remains normal.

## Priority 2

1. Reclassify every major subsystem as:
- production
- transitional
- experimental
- dead seam

Current state: partially addressed in `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md`.
Remaining work: turn the conceptual classification into owner-by-owner tickets and live verification gates.

2. Mark DOTS honestly.
Reason: It is currently a seam, not a strength.

3. Build a real first-party regression harness.
Minimum targets:
- bootstrap
- save/load
- HUD
- player inventory
- world scatter
- scene transitions

## Priority 3

1. Reverify major docs against current code and editor truth.
Reason: documentation volume is high, but drift has already happened.

2. Add explicit â€œreality statusâ€ headers to key docs.
Suggested values:
- production
- partial
- stale
- target architecture only

3. Establish one audit scoreboard that is maintained from code/editor evidence, not aspiration.

4. Keep `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` synchronized with major source-level architecture changes.
Reason: this file is now the conceptual entry point. If it drifts, future agents will again read stale system ownership from older reports.

## What Not To Do

- Do not claim DOTS is active production architecture.
- Do not keep adding managers into already overloaded roots.
- Do not treat large files as harmless if they still absorb more responsibility.
- Do not use docs as truth without code/editor cross-check.
- Do not call the project â€œcloseâ€ while verification truth, test truth, and authority truth are still fragmented.

## 2026-05-01 Queue Delta

This section supersedes queue ordering where the May 1 audits produced sharper evidence.

## 2026-05-01 Editor.log Delta

Local `Editor.log` evidence after the console-stabilization pass reports:

- `error CS`: `0`
- `warning CS`: `0`
- `Exception`: `0`
- `Resource ID out of range in SetResource`: `0`
- mixed shader line-ending warnings: `0`

Evidence file: `Docs/Reports/2026-05-01_EDITOR_LOG_CONSOLE_STABILIZATION.md`.

This changes Priority 0 item 1 from "no trustworthy local log surface" to "local Editor.log surface currently clean after compile/import."
It does not solve MCP availability, Play Mode verification, profiler proof, or long-run memory retention.

## 2026-05-01 Event-Bus / Spatial-Hash Compile Delta

Follow-up evidence file: `Docs/Reports/2026-05-01_EVENT_BUS_SPATIAL_HASH_COMPILE_DELTA.md`.

Local `Editor.log` evidence after the event-bus/spatial-hash repair reports:

- latest `Tundra build success`: line `14575`
- latest `Mono: successfully reloaded assembly`: line `14663`
- strict post-success signals (`error CS`, `warning CS`, `Burst error`, `Exception`, `Resource ID out of range`): `0`
- MCP `read_console`: `0` error/warning entries after the compile refresh

Queue corrections from current source recheck:

- `HectonSpatialHash` no longer matches the older "monotonic `_nextHandle++` without visible allocator-boundary guard" finding. Current source has slot/generation handles, queued-free duplicate guard, and current-handle validation. Runtime churn remains unprofiled.
- `AutonomousExtractorSystem`, `WorldCaveDirector`, and `WorldProceduralFieldSampler` no longer show the specific `~0` query masks cited by the earlier flaw report. Current source uses named project masks at those query sites. Scene-layer validation remains pending before claiming physics filtering is complete.

## 2026-05-01 Compile Stabilization Continuation

Follow-up evidence file: `Docs/Reports/2026-05-01_COMPILE_STABILIZATION_CONTINUATION.md`.

Local `Editor.log` evidence after restoring `Assets/_Project/Scripts/World/VegetationJobRecovery.cs.meta` and waiting through Bee/backend recovery reports:

- latest `Tundra build success`: line `103944`
- latest `Begin MonoManager ReloadAssembly`: line `103987`
- latest `Mono: successfully reloaded assembly`: line `104086`
- strict post-success signals (`error CS`, `warning CS`, `Burst error`, `Exception`, `Resource ID out of range`, `Tundra build failed`): `0`

MCP console returned `0` error/warning entries after the final reload.
Treat the current state as editor/script compile-clean, not Play Mode or profiler proof.

## Priority 0 Additions

1. Remove presentation-owned gameplay transitions.
Reason: the original `FaunaBrain.UpdateBioluminescentHypnosis()` `runtimeContext.PlayerCamera` dependency is stale by current source recheck; it now consumes `PlayerRuntimeContext.LookState`. Remaining fauna headless risk is perception/LOD logic still using player Transform/Rigidbody paths plus absent no-camera Play Mode proof. `StorageCrate.OpenCrate()` was source-patched so gameplay opens immediately and the Animator event is idempotent, but Play Mode proof is absent.

2. Move frame-lane job barriers behind dispatcher-owned completion windows.
Reason: the older literal call-site claim naming `ProximityColliderSystem.Tick`, `SaveManager.Tick`, and `HectonFluidEngine.PostFixedTick` is stale by current strict source grep. Current `.Complete(` hits under `Assets/_Project/Scripts` are dispatcher completion callbacks in `ItemCatalog.cs` / `AssetLifecycleGovernor.cs` and one explicit `JobHandle.Complete()` in `World/DispatcherJobSwap.cs`. The remaining risk is dispatcher-owned completion-window proof and runtime stall profiling, not those stale call sites.

3. Replace broad physics masks with named layer masks.
Reason: the latest flaw report found query surfaces using `~0`, `DefaultRaycastLayers`, or serialized all-layer defaults. Source-level partial patch applied: `GravityTetherTool`, `PhysicalInteractionHandler`, `PlayerInteraction`, `HectonMusicDirector`, `HectonVoxelVolume`, `ResourceNode`, `SubmarineFluidDynamics`, and `AbyssalThermalManager` now use named/fallback masks. Follow-up source recheck no longer finds the earlier cited `~0` masks in `AutonomousExtractorSystem`, `WorldCaveDirector`, or `WorldProceduralFieldSampler`; scene-layer validation remains pending before claiming physics filtering is complete.

4. Keep Core asmdef isolation as blocked, not done.
Reason: `OMEGA_CORE_ENFORCEMENT_2026-05-01.md` explicitly rejected blind removal. `Hecton8.Core.asmdef` still references UI/third-party packages, and safe isolation requires staged bridge assemblies first.

## Priority 1 Additions

1. Convert the remaining coroutine smoke/verifier harnesses one by one.
Reason: current strict grep now finds 0 `StartCoroutine(` call sites outside `Editor/**`. `Tools/StateRecoveryVerifier`, `ToolRuntimeSmokeTester`, `FieldToolRuntimeSmokeTester`, `ToolTrialRangeRuntimeSmokeTester`, and `Dev/ShellVerificationRuntimeSmokeTester` were migrated to `Awaitable` at source level; Play Mode proof is absent.

2. Add AUP-safe route ownership to fauna navigation.
Reason: `FaunaBrain` caches raw `Vector3` route waypoints and target positions without implementing origin-shift listener behavior. After floating-origin rebases, cached routes can drift by the shift vector.

3. Add NativeQueue generation split for event lanes.
Reason: source recheck found `HectonEventBus.MaxDispatchDepth = 4` and dispatcher late-frame budget enforcement already present. Remaining risk is same-frame reenqueue in NativeQueue-backed lanes until `MaxLateFrameEventsPerFrame` is exhausted. Evidence: `Docs/Reports/2026-05-01_EVENT_CASCADE_RECHECK.md`.

4. Verify BRG temporary allocation ownership before touching rendering code.
Reason: `HectonBatchRendererGroupUtility` allocates direct-draw `TempJob` memory. It may be Unity-owned after submission, but the project-owned free path is not proven in source.

## Current Do-Not-Claim List

- Do not claim Unity batchmode is globally clean from old logs alone. May 2 fresh dotnet build is clean for `Hecton8.Core.csproj`, but older Unity batch artifacts still include stale compile/path failures and must be re-run cleanly before Unity editor import truth is claimed.
- Do not claim Play Mode deadlock is fixed. Play Mode was intentionally not launched.
- Do not claim GC is zero. GCMonitor/profiler proof is absent.
- Do not claim MCP VERIFIED as a global runtime state. Current May 4 evidence is editor-only MCP readback: latest current recheck reports active scene `00_BOOTSTRAP`, Play Mode off, compiling false, ready for tools, and console error/warning entries `0`. Earlier May 4 documentation-sweep readback reported `01_MAIN_MENU` in Play Mode transition with `18` warnings. Bounded Play Mode gameplay, GCMonitor, profiler, and long-run memory proof are absent.
- Do not claim older dated reports are current. This queue points to current deltas, but historical report bodies retain scan-time claims.
- Do not treat `2026-05-01_CURRENT_PROJECT_STATE.md` as runtime verification. It is a conceptual source-backed snapshot only.

## 2026-05-02 Documentation / Compile Evidence Delta

Follow-up evidence file: `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md`.

Fresh local command:

- `dotnet build .\Hecton8.Core.csproj`
- result: exit code `0`, `136 Warning(s)`, `0 Error(s)`, elapsed `00:01:24.05`
- latest post-restore `dotnet build .\Hecton8.Core.csproj --no-restore` rerun: exit code `0`, `73 Warning(s)`, `0 Error(s)`, elapsed `00:00:23.95`

Queue corrections:

- Priority 0 item 1 remains open. Observability improved for dotnet compile only; Unity batchmode, MCP, Play Mode, GCMonitor, profiler, and memory retention are still not one reliable verification surface.
- Priority 3 item 1 remains open. This pass updated active indexes and current-state anchors, not every historical document body.
- The documentation authority path is now `Docs/README.md` -> `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` -> `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
- Current source inventory for this queue boundary is superseded by the May 4 sweep: `1118` first-party `.cs` files under `Assets/_Project`, `1078` under `Assets/_Project/Scripts`, `519952` static script lines, and `0` strict `StartCoroutine(` hits under `Assets/_Project/Scripts`.

STATUS: PENDING VERIFICATION

## 2026-05-04 Documentation / Guard Evidence Delta

Follow-up evidence files: `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` and `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`.

Fresh May 4 evidence:

- `Hecton8.Core.csproj --no-restore`: `0 Warning(s)`, `0 Error(s)`.
- `Hecton8.Editor.csproj --no-restore`: `0 Warning(s)`, `0 Error(s)`.
- `Hecton8.World.Dots.csproj --no-restore`: blocked by missing `Temp\obj\Hecton8.World.Dots\project.assets.json`; restore build then returned `1 Warning(s)`, `0 Error(s)`.
- `Hecton8.PlayModeTests.csproj --no-restore`: blocked by missing `Temp\obj\Hecton8.PlayModeTests\project.assets.json`; restore build then returned `0 Warning(s)`, `0 Error(s)`.
- Post-repair foundation guard scan exits `0`; `UnsafeUtility.MemCpy outside guard` is `0` and unauthorized Unity loop methods are `0`.
- MCP readback: earlier documentation-sweep retry saw active scene `01_MAIN_MENU`, editor in Play Mode transition, console errors `0`, console warnings `18`; latest current recheck saw active scene `00_BOOTSTRAP`, Play Mode off, compiling false, ready for tools, and console error/warning entries `0`.

Queue corrections:

- Priority 0 observability remains open. Compile, source-guard, and current editor-console evidence improved; bounded Play Mode gameplay, profiler, GCMonitor, and memory-retention proof are still absent.
- Priority 1 `.Complete()` audit remains open. Current strict text inventory is `.Complete(` hits `5`, with only `SystemDispatcher` counted as guarded dispatcher completion.
- Documentation authority path is now `Docs/README.md` -> `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` -> `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.

STATUS: PENDING VERIFICATION

## 2026-05-03 Input Controls Lifecycle Delta

Follow-up evidence file: `Docs/Reports/2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md`.

Queue correction:

- Priority 0 item 3 is reduced for pause/PDA controls event lifecycle ownership: `PDAControlsRebindUI` and `PauseControlsPanel` now unsubscribe from the exact `InputManager` and `IInputBindingService` owners captured during subscribe.
- `PauseControlsPanel` saves binding overrides before clearing its cached rebinding owner, avoiding save calls against a replaced registry slot during disable.
- `PDAControlsRebindUI` now fails closed when input is absent during action lookup instead of dereferencing `InputManager.Instance`.
- Controls selection indicator visibility now uses cached `CanvasGroup` references instead of resolving/adding `CanvasGroup` components during navigation-time selection refresh.
- `BeaconHUDElement.ApplyDisplayVisible()` no longer performs `GetComponent<CanvasGroup>()` / `AddComponent<CanvasGroup>()` from the `Tick()` call chain; malformed icon records now fail closed.
- `NotificationEvents` now guards duplicate listener registration, absent unregistration, and null listener dispatch slots.
- Central event lanes `SaveEvents`, `ScanEvents`, `CraftingEvents`, `InteractionEvents`, `InventoryEvents`, `QuestEvents`, and `NarrativeEvents` now skip null listener slots during dispatch where needed; save/scan/quest/narrative also guard duplicate register and absent unregister.
- Remaining direct raw-array listener invocations under `Assets/_Project/Scripts` were removed from bootstrap, weather, flashlight, ending, first-hour, storage-reservation commit, physics-impact, tool-effect, power telemetry, and pressure/implosion lanes. Dispatch now reads the slot to a local listener and skips null without allocation.

Evidence:

- Fresh local Core build after this patch: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`
- Warning cleanup: removed dead `UIAudioFeedback` pitch-variation inspector fields because `IAudioService.PlayStatic2D` has no pitch parameter.
- Follow-up source-slice build after selection-indicator caching while another build chain was contending for project references: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Follow-up result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`
- Follow-up source-slice build after beacon HUD hot-path cache hardening: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Follow-up result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`
- Follow-up source-slice build after notification listener hardening: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Follow-up result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`
- Follow-up source-slice build after save/scan/crafting/interaction event-lane hardening: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Follow-up result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`
- Follow-up source-slice build after inventory/quest/narrative event-lane hardening: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Follow-up result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`
- Source grep after raw listener dispatch purge: `rg -n "rawArray\\[i\\]\\.On" Assets/_Project/Scripts`
- Grep result: no matches.
- Follow-up source-slice build after raw listener dispatch purge: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Follow-up result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:50.77`

Still open:

- Broader `InputManager.Instance` authority drift remains. This patch did not change public input contracts or migrate binding-query API surface.
- No Unity Play Mode input-service replacement test.
- No MCP console proof.
- No GCMonitor proof.

STATUS: PENDING VERIFICATION

## 2026-05-03 Foundation Hardening Delta

Follow-up evidence file: `Docs/Reports/2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md`.

Fresh Unity batchmode evidence after the deferred PhysX-bake teardown and watchdog pass reports:

- latest `Tundra build success`: `51.07 seconds`, `33 items updated`, `1808 evaluated`
- `CompileScripts`: `52654.128ms`
- latest `Mono: successfully reloaded assembly`: present
- strict compiler failure scan (`error CS`, `warning CS`, `Compiler error`, `Scripts have compiler errors`, `Tundra build failed`, `Compilation failed`): `0`
- batchmode exit: `Exiting batchmode successfully now!`

Queue correction:

- Priority 0 item 2 is source-patched for `HectonWorldGenerator`: runtime chunk retirement now defers active PhysX-bake teardown instead of force-completing bake handles during cancellation/eviction.
- Priority 0 item 5 now has active Editor self-test coverage for `HectonSpatialHash`: stale handles are rejected, recycled handles advance generation, moved entries do not leave source-cell ghost occupancy, and AUP-scale queries pass at the tested range. Evidence: `Temp/CodexArtifacts/editmode-results-2026-05-03-spatialhash-selftest-after-beacon.xml`, result `Passed`, `3/3`.
- Priority 1 item 2 is partially addressed for `ProceduralWreckGenerator` and `HectonFloatingOrigin`: mesh-build yield waits now have a watchdog, and floating-origin shift stability now reports within `1200` frames instead of `50000`.

Still open:

- Play Mode eviction stress is absent.
- Spatial-hash live runtime churn under register/unregister pressure is absent.
- GCMonitor proof is absent.
- MCP runtime console is absent.
- Memory-retention soak is absent.

STATUS: PENDING VERIFICATION

## 2026-05-03 Registry Service/Renderable / Job Barrier Guard Delta

Follow-up evidence files:

- `Docs/Reports/2026-05-03_REGISTRY_RENDERABLE_AND_JOB_BARRIER_GUARD.md`
- `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`

Source corrections:

- `HectonUnderwaterVisuals`, `HectonSubmarineOS`, and `MissionMarkerSystem` now set `_registeredRenderable` from `GlobalRegistry.Renderables.Contains(this)` after `GlobalRegistry.Renderables.Register(this)`.
- `DebrisManager` and `PhysicsApplySystem` now set `_isInitialized` from authoritative `GlobalRegistry.Debris` / `GlobalRegistry.Physics` slot ownership after service registration, and their teardown unregister paths verify slot ownership before unregister.
- `HectonMapMagicVegetationBridge` now tracks TerrainTile static event subscription separately from `HectonFloatingOrigin` listener ownership, and reads `_originShiftListenerRegistered` from `HectonFloatingOrigin.IsListenerRegistered(this)` after registration.
- `Tools/ReloadAudit/Scan-FoundationGuards.ps1` now source-scans first-party scripts for broad `GlobalRegistry.Register*(...this...)`, renderable self-registration, `HectonFloatingOrigin.RegisterListener(this)` followed by blind `_registered* = true` / `_isInitialized = true` / listener state flags, and direct `rawArray[i].On*` listener dispatch.
- `RebindingManager` now uses the bootstrap-bound native input manager for rebind operations, binding override save/load/clear, and conflict detection instead of querying `InputManager.Instance` directly.

Fresh source guard results:

- `Global registry self-registration sites`: `493`
- `Blind registry flag drift`: `0`
- `Origin shift listener blind flag drift`: `0`
- Synchronous job `.Run(` sites: `0`
- Hot-path synchronous job `.Run(` review sites: `0`
- Completion `.Complete(` text hits: `1`
- Direct raw-array listener dispatch: `0`
- `GlobalRegistry.Input` nullable misuse: `0`
- Direct `InputManager.Instance` sites: `21`
- Hot-path direct `InputManager.Instance` review sites: `0`
- Optimization singleton residue: `0`
- Unauthorized Unity loop methods: `0`
- Legacy coroutine sites: `0`
- Forbidden runtime asset API sites: `0`
- Broad physics layer masks outside Editor: `0`
- Runtime Find API text hits outside Editor folder: `0`

Queue correction:

- Priority 0 item 3 is reduced for the scanned broad registry/renderable self-registration flag pattern. It is not closed because singleton/DDOL residue and service sovereignty conflicts still exist.
- Priority 1 item 1 remains open. `.Complete(` text hits are still dispatcher request callbacks plus `DispatcherJobSwap`; runtime stall proof is absent.
- Priority 1 item 3 remains open. `.Run(` sites are inventoried and classified in the report, but not migrated without profiler data.

Verification:

- `dotnet build Hecton8.Core.csproj -v:minimal -nr:false -m:1 -p:UseSharedCompilation=false`
- Result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`
- Warning cleanup: removed dead `UIAudioFeedback` pitch-variation inspector fields because `IAudioService.PlayStatic2D` has no pitch parameter.
- Latest source-slice rerun after raw listener-dispatch purge and guard regeneration: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Latest source-slice result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:10.39`
- Latest source-slice rerun after rebinding native-owner binding: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Latest source-slice result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:03.95`
- Log: `.codex-artifacts/dotnet-Hecton8.Core-2026-05-03-foundation-guard-physicsmask.log`

Still open:

- Unity Play Mode proof is absent.
- MCP runtime console proof is absent.
- GCMonitor/profiler proof is absent.
- Renderable bucket pressure and scene registration behavior are untested.
- Job `.Run(` sites need measured frame-time attribution before dispatcher-window migration.

STATUS: PENDING VERIFICATION

## 2026-05-03 Habitat Graph Anchor-State Delta

Follow-up evidence file: `Docs/Reports/2026-05-03_HABITAT_GRAPH_ANCHOR_STATE_HARDENING.md`.

Queue correction:

- `HabitatGraphManager` no longer uses `_anchorReachability` as generic BFS scratch in component power, flood center-of-mass, and fungal target traversal paths.
- Authoritative anchor/isolated truth is now separated from traversal scratch through a persistent `_traversalVisited` native buffer.
- This addresses a concrete source-level risk where later graph publish stages could read traversal visited state as anchored-state truth.

Evidence:

- Fresh full local Core build after this patch: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Result: `0 Error(s)`, `1 Warning(s)`
- Warning: `MSB3026` file-copy retry on `Temp\obj\Hecton8.Core\Hecton8.Core.dll`; not a C# compiler warning

Still open:

- No Play Mode base graph test.
- No scene/prefab readback for module anchor roles.
- No GCMonitor proof.
- No construction graph teardown soak.

STATUS: PENDING VERIFICATION
