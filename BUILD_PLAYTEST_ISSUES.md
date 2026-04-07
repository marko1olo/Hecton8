# HECTON-8 — BUILD / PLAYTEST ISSUES LEDGER

Status: `PENDING VERIFICATION`
Ledger Start Date: `2026-04-05`

## Purpose

This file tracks confirmed build and playtest observations.

Rules:

- Only log real observations from builds, live runs, or manual playtests
- Do not log abstract ideas here
- Do not mark anything fully solved without new evidence
- Every item remains `PENDING VERIFICATION` until a new build or user check confirms the fix
- Player build is the main arbiter, not editor feel
- Use `[c]` for code-fixed issues that are closed for current coding work but still await build or user confirmation
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

### [!] Pause Cursor Missing / Pause Button Audit Needed
- Status: [!]
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
- Failed: live cursor-state proof is still incomplete on this machine because Unity MCP `execute_code` still fails with `mono.exe: filename or extension is too long`, and the existing `UIRuntimeSmokeTester` stalled after `PASS PDA open Inventory` before producing a pause result.
- Broke: no new compile errors; console still shows only pre-existing `Dynamic Decals` warnings, plus transient MCP serializer warnings during the abandoned smoke attempt.
- Failed Addendum: attempted short MCP play-mode readback again after the focus patch, but `execute_code` still hard-fails on this machine with the same `mono.exe: filename or extension is too long`, so direct editor-side proof of the selected pause button is still blocked.
- Remaining: real runtime check of `Cursor.visible`, `Cursor.lockState`, `Esc` open/close, initial pause-button focus, and every pause button action in build; current status remains `PENDING VERIFICATION`.

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

### [~] Geology Terrain Seam Runtime GC Hardening Not Yet Proven
- Status: [~]
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

### [~] Cave Spawn Lifecycle Hardening Not Yet Proven
- Status: [~]
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

### [~] Voxel Bridge Hot-Path Iteration Compliance Not Yet Proven
- Status: [~]
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

### [~] Seam Planning / Execution Hot-Path Hygiene Not Yet Proven
- Status: [~]
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

### [~] Cave Sediment Shelf Runtime Layer Not Yet Proven
- Status: [~]
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

### [~] Cave Biome Runtime Classification Hygiene Not Yet Proven
- Status: [~]
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

### [~] Cave Runtime Ownership Cleanup Not Yet Proven
- Status: [~]
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

- Failed: GPU-side truth is still incomplete because the screenshots do not expose actual GPU frame time, and MCP currently reports profiler disabled; its fallback rendering snapshot is not trustworthy as a live player oracle here.
- Broke: nothing.
- Remaining: audit and reduce:
  - `EventSystem` -> `GameObject.Activate/ActivateAwakeRecursively` spikes
  - UI `SetActive` cascades in `PlayerPDA`, `PDAInventoryTab`, `PauseMenuController`, and HUD roots
  - `GameTickManager.SlowTickRoutine()` / coroutine spike ownership
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
- Result: the known `ActivateAwakeRecursively` path is now attacked at the actual sources instead of at profiler symptoms. The intended runtime effect is fewer UI activation spikes, less activation-adjacent GC on open/switch frames, and lower `EventSystem` cost when toggling PDA/pause/HUD visibility.
- Failed: standalone before/after capture for `open PDA`, `switch PDA tab`, `open pause`, and `resume gameplay` still has not been re-run, and Unity MCP `execute_code` remains blocked on this machine by `mono.exe: filename or extension is too long`.
- Broke: the unrelated compile contamination that previously blocked this verification path is now cleared. Current compile readback shows warnings and editor-inspector null spam, but no new `CS` errors from the UI pass.
- Remaining: rebuild after clearing unrelated compile blockers, then compare standalone profiler frames before/after for `PDA open`, `tab switch`, `pause open`, `pause close`, and `idle gameplay with HUD active`.

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
- Result: compile state remains clean of `CS` errors after the fauna/scatter pass. The runtime scatter blocker that previously aborted `WorldProceduralScatterDirector.Awake()` is code-fixed, and the fauna director no longer locks its warmup to a one-time static reserve disconnected from live activation limits.
- Result Addendum: fauna is now fail-soft under pool pressure. If reserve is insufficient, the director skips that spawn attempt instead of injecting pool expansion and allocation spikes into gameplay.
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
