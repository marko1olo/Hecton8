# Critical Action Queue

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

2. Add explicit “reality status” headers to key docs.
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
- Do not call the project “close” while verification truth, test truth, and authority truth are still fragmented.

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

Local `Editor.log` evidence after restoring `Assets/_Project/Scripts/World/VegetationJobRecovery.cs.meta` reports:

- latest `Tundra build success`: line `91784`
- latest `Begin MonoManager ReloadAssembly`: line `91826`
- latest `Mono: successfully reloaded assembly`: line `91935`
- strict post-success signals (`error CS`, `warning CS`, `Burst error`, `Exception`, `Resource ID out of range`, `Tundra build failed`): `0`

MCP console is not globally clean: after Console UI clear it still reports one stale internal build-system exception about missing `BuildFinishedMessage`.
Treat the current state as local compile-clean by `Editor.log`, not `MCP VERIFIED`.

## Priority 0 Additions

1. Remove presentation-owned gameplay transitions.
Reason: the original `FaunaBrain.UpdateBioluminescentHypnosis()` `runtimeContext.PlayerCamera` dependency is stale by current source recheck; it now consumes `PlayerRuntimeContext.LookState`. Remaining fauna headless risk is perception/LOD logic still using player Transform/Rigidbody paths plus absent no-camera Play Mode proof. `StorageCrate.OpenCrate()` was source-patched so gameplay opens immediately and the Animator event is idempotent, but Play Mode proof is absent.

2. Move frame-lane job barriers behind dispatcher-owned completion windows.
Reason: `ProximityColliderSystem.Tick`, `SaveManager.Tick`, and `HectonFluidEngine.PostFixedTick` call `.Complete()` in cadence-sensitive lanes. `IsCompleted` checks reduce stall probability but do not satisfy the project mandate requiring completion only in defined swap/end windows.

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

- Do not claim Play Mode deadlock is fixed. Play Mode was intentionally not launched.
- Do not claim GC is zero. GCMonitor/profiler proof is absent.
- Do not claim MCP VERIFIED as a global runtime state. Current May 1 evidence is local `Editor.log` compile/reload success, while MCP console still exposes one stale internal build-system exception. Play Mode, GCMonitor, profiler, and long-run memory proof are absent.
- Do not claim the docs are fully current. This queue now points to current deltas, but older dated documents still contain historical and stale claims.
- Do not treat `2026-05-01_CURRENT_PROJECT_STATE.md` as runtime verification. It is a conceptual source-backed snapshot only.

STATUS: PENDING VERIFICATION
