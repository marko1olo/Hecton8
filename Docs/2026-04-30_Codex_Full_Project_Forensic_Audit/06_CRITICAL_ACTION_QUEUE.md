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

5. Audit the spatial-hash handle contract.
Reason: `HectonSpatialHash` currently uses monotonic `_nextHandle++` without visible allocator-boundary guard or reuse. Even if it is not the proven root of `SetResource` spam yet, it is too weak to ignore.

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

## What Not To Do

- Do not claim DOTS is active production architecture.
- Do not keep adding managers into already overloaded roots.
- Do not treat large files as harmless if they still absorb more responsibility.
- Do not use docs as truth without code/editor cross-check.
- Do not call the project “close” while verification truth, test truth, and authority truth are still fragmented.

## 2026-05-01 Queue Delta

This section supersedes queue ordering where the May 1 audits produced sharper evidence.

## Priority 0 Additions

1. Remove presentation-owned gameplay transitions.
Reason: `FaunaBrain.UpdateBioluminescentHypnosis()` changes gameplay behavior based on `runtimeContext.PlayerCamera`. `StorageCrate.OpenCrate()` can dead-state if an Animator event does not fire. These are headless-simulation failures, not cosmetic issues.

2. Move frame-lane job barriers behind dispatcher-owned completion windows.
Reason: `ProximityColliderSystem.Tick`, `SaveManager.Tick`, and `HectonFluidEngine.PostFixedTick` call `.Complete()` in cadence-sensitive lanes. `IsCompleted` checks reduce stall probability but do not satisfy the project mandate requiring completion only in defined swap/end windows.

3. Replace broad physics masks with named layer masks.
Reason: the latest flaw report found query surfaces using `~0`, `DefaultRaycastLayers`, or serialized all-layer defaults. These are collision-matrix holes and make gameplay queries depend on unrelated visual/trigger layers.

4. Keep Core asmdef isolation as blocked, not done.
Reason: `OMEGA_CORE_ENFORCEMENT_2026-05-01.md` explicitly rejected blind removal. `Hecton8.Core.asmdef` still references UI/third-party packages, and safe isolation requires staged bridge assemblies first.

## Priority 1 Additions

1. Convert the remaining coroutine smoke/verifier harnesses one by one.
Reason: current strict grep still finds 15 `StartCoroutine(` call sites in `FieldToolRuntimeSmokeTester`, `ToolRuntimeSmokeTester`, `ToolTrialRangeRuntimeSmokeTester`, `Dev/ShellVerificationRuntimeSmokeTester`, and `Tools/StateRecoveryVerifier`.

2. Add AUP-safe route ownership to fauna navigation.
Reason: `FaunaBrain` caches raw `Vector3` route waypoints and target positions without implementing origin-shift listener behavior. After floating-origin rebases, cached routes can drift by the shift vector.

3. Add a hard event generation/depth guard.
Reason: `SystemDispatcher` has a late-frame dispatch budget, but `HectonEventBus` depth tracking has no hard cap. A recursive publish chain can still produce unbounded same-frame work unless generation splitting or max-depth rejection is enforced.

4. Verify BRG temporary allocation ownership before touching rendering code.
Reason: `HectonBatchRendererGroupUtility` allocates direct-draw `TempJob` memory. It may be Unity-owned after submission, but the project-owned free path is not proven in source.

## Current Do-Not-Claim List

- Do not claim Play Mode deadlock is fixed. Play Mode was intentionally not launched.
- Do not claim GC is zero. GCMonitor/profiler proof is absent.
- Do not claim MCP VERIFIED as a global runtime state. Previous May 1 evidence was `0` console errors for editor/script state only; this delta pass could not refresh the console because MCP returned `no_unity_session`.
- Do not claim the docs are fully current. This queue now points to current deltas, but older dated documents still contain historical and stale claims.

STATUS: PENDING VERIFICATION
