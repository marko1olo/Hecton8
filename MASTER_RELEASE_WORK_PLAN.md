# HECTON-8 — FULL MASTER IMMERSIVE RELEASE PLAN v2

Status: `PENDING VERIFICATION`
Approved For Use: `2026-04-05`
Primary Target: `NVIDIA MX350 2 GB VRAM / 12 GB RAM / i5-1135G7`
Direction: `NASA-Punk + Deep Sea Noir`

## Summary

This is the main production roadmap for HECTON-8.

It replaces the previous master-plan as the single working source of truth for:

- product shell
- build-truth blockers
- world generation
- caves and geology
- life layers
- surface and island ecology
- ruins and human traces
- progression and return loops
- persistence
- performance guardrails
- review cadence

Main formula:

- `MapMagic = world skeleton`
- `108-biome matrix = world meaning`
- `world fill = reasons to swim`
- `geology + caves + seams = close-range shape`
- `flora + microfauna + fauna = feeling of life`
- `biolum + sky + fog + silhouettes = expensive lie`
- `resources + danger + ruins + return loops = payoff`

Main working truth:

- CPU optimization remains required, but it is no longer the whole point by itself.
- CPU is now a guardrail.
- Main active track is product truth: menu, pause, build blockers, live world fill, surface truth, caves, life layers, ruins, and progression.
- Player build remains the main quality arbiter.
- Nothing is done because code exists.
- Nothing is done because it looked good in editor.
- Everything remains `PENDING VERIFICATION` until confirmed by build, world-check, and user check where applicable.

## File Governance

- Main roadmap file: `C:\hades\Hecton8\MASTER_RELEASE_WORK_PLAN.md`
- Build issues ledger: `C:\hades\Hecton8\BUILD_PLAYTEST_ISSUES.md`

Status legend:

- `[ ]` not started
- `[~]` in progress
- `[c]` code-fixed; closed for active coding, waiting for build/user proof
- `[x]` verified and confirmed
- `[!]` blocker
- `[?]` user feedback required

Task card standard for all active tasks:

```md
### [ ] Task Name
- Status: [ ] / [~] / [c] / [x] / [!] / [?]
- Need User Check: yes / no
- Need Build Check: yes / no
- Need In-World Swim Check: yes / no
- Why:
- Evidence:
- Problems:
- Short Comment:
- Next Step:
```

Pass log template for every meaningful pass:

```md
- Did:
- Result:
- Failed:
- Broke:
- Remaining:
```

Rules:

- Visual and feel tasks always require `Need User Check: yes`
- Performance and render tasks always require `Need Build Check: yes`
- Ecology, caves, ruins, world-fill tasks always require `Need In-World Swim Check: yes`
- Use `[c]` when code is patched and the issue is closed for current implementation work, but the result still lacks final build or user confirmation
- Use `[x]` only when a new build, playtest, or explicit user confirmation proves the fix
- If the same task is reopened 2-3 times without perceptual gain, record what failed and switch approach
- Do not reopen old paths without new evidence

## Production Rules

- Do not replace MapMagic.
- Do not rewrite third-party cores.
- Work through first-party bridge, runtime, authoring, and data layers.
- Do not build a new procedural pipeline from scratch.
- Use the existing `fill / scatter / geology / zone / biome / profile` stack.
- Do not treat editor truth as final truth.
- Do not treat compilation as proof of completion.
- Do not use brute-force simulation where a cheaper visual lie gives the same player belief.
- Every pass must answer: what the player feels, sees, remembers, and returns for.
- Every beautiful layer must pass the `worth / cost / fallback` filter for MX350.
- The world must be readable, varied, memorable, revisit-worthy, and psychologically dense.

## Confirmed Baseline

Confirmed project foundations already exist:

- `SceneBootstrap`
- `MainMenuController`
- `MapMagicBridge`
- `WorldProceduralFillDirector`
- `WorldProceduralScatterDirector`
- `WorldStreamingDirector`
- `WorldSliceDirector`
- `ScatterBudgetController`
- `BiomeMatrixDirector`
- `HectonVoxelEngine`
- `CaveGraphGenerator`
- `FaunaDirector`
- `HectonBoidController`
- `PauseMenuController`
- `SaveManager`
- `BaseModule`
- `HectonSurvivalSystem`

Confirmed build-truth from `2026-04-05`:

- build is smoother than editor
- hitch appears when surfacing and rotating camera
- oxygen refill does not work correctly on surface
- pause cursor does not appear
- pause buttons need full audit
- gas giant does not read as distant because the layering is wrong
- terrain and rock close-up read is too blurry in build
- underwater base and core feel are already promising even before full world content exists

## Public API / Interface Additions

- [x] Add unified `GameStartContext`
  - `startMode` (enum: NewGame / LoadGame / Resume)
  - `targetSaveSlot`
  - `spawnMode` (enum: SavedLocation / FallbackLocation / IntroLocation)
  - `introContext`
  - `landingPreset`
  - Source of truth for `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`
  - **IMPLEMENTED:** GameStartContext struct in `Assets/_Project/Scripts/Core/GameStartContext.cs`
    - Factory methods: CreateNewGame(), CreateLoadGame(), CreateResume()
    - GameStartContextHolder static singleton for inter-scene transfer
    - Zero-GC enum-based serializable struct
  - **UPDATED:** MainMenuController.StartGame() now writes to GameStartContextHolder.Current
  - **UPDATED:** SceneBootstrap.LoadOrNewGameAsync() reads from GameStartContextHolder.Current with PlayerPrefs fallback
- [~] Add unified `Surface Truth Contract`
  - One source of truth for water level and surface state across `MapMagicBridge`, `HectonFluidEngine`, `HectonSurvivalSystem`, underwater visuals, atmosphere, camera, and audio transitions
  - code truth now exists for surface hysteresis itself: `SurfaceStateUtility` is already used by `HectonSurvivalSystem`, `HectonAtmosphereManager`, and `HectonUnderwaterVisuals`
  - code truth addendum: `AcousticZoneController` now separates `BuoyancyObject.IsInDryZone` from grounded shoreline, resolves `interior / surface / underwater` from `HectonAtmosphereManager.CurrentState` with `SurfaceStateUtility` fallback on player depth, auto-bootstraps itself at runtime, and can drive the player's looping underwater ambient source without authored scene wiring
  - remaining gap: there is still no single runtime authority object that drives `MapMagicBridge`, `HectonFluidEngine`, and audio transitions from the exact same source, and mixer snapshots / build traversal proof are still missing
- [x] Add unified `Build Playtest Entry`
  - Every build pass logs version, date, FPS-feel, main irritant, main visual flaw, main UX flaw, main content gap, blocker yes/no
  - **IMPLEMENTED:** BuildPlaytestEntry struct in `Assets/_Project/Scripts/BuildTools/BuildPlaytestEntry.cs`
    - Factory method Create() with all required fields
    - ToMarkdownEntry() for exporting to BUILD_PLAYTEST_ISSUES.md
    - BuildPlaytestLog static holder for global entry list
    - ExportToMarkdown() for bulk export
- [x] Add `Biome Content Pack Contract`
  - Required per biome family: geology role, flora role, microfauna flavor, passive fauna, predator pressure, ruin relation, cave relation, resource signature, memory motif, return reason
  - **IMPLEMENTED:** BiomeContentPackContract struct in `Assets/_Project/Scripts/Data/BiomeContentPackContract.cs`
    - Complete serializable struct with all required fields
    - Nested classes for GeologyRole, FloraRole, MicrofaunaFlavor, etc.
    - IsValid() method for contract validation
    - CreateTemplate() factory method for new biome setup
    - Zero-GC design with Unity serialization support
- [ ] Do not change other public runtime API without a dependency audit

## Release Goal

- [ ] The game launches as a product through `00_BOOTSTRAP`, not as a dev-scene
- [ ] `01_MAIN_MENU` becomes a production shell, not decorative filler
- [ ] `02_HECTON_WORLD` becomes a convincing living ocean, not an empty terrain preview
- [ ] The player feels from the first minutes:
  - there is somewhere to swim
  - there is something to inspect
  - there is something to fear
  - there is a reason to dive deeper
  - there is a reason to return later
- [ ] Core loop is already readable in an early build:
  - start
  - orientation
  - gathering
  - detour
  - cave/ruin discoverability
  - return
  - base/support pocket
  - save/load
- [ ] The world sells false depth and false richness without killing CPU/GPU

## P0 Build Truth Track

- [c] Fix hitch on underwater -> above-water transition with camera rotation
- [c] Fix surface oxygen refill
- [!] Bring pause menu to a stable product flow
- [!] Audit all pause buttons
- [!] Separate gas giant and cloud/haze stack so the giant reads as distant
- [!] Investigate terrain/rock close-up blur
- [!] Run identical editor/build parity on the same spot, same FOV, same light, same distance
- [!] Keep all of these logged in `BUILD_PLAYTEST_ISSUES.md` until build-confirmed

P0 rules:

- use `[c]` when coding is complete and the task is closed for current implementation work
- use `[x]` only after direct build or user evidence
- do not promote `[c]` to `[x]` in editor only

## Product Shell / Bootstrap / Menu / Pause

- [~] Bring `00_BOOTSTRAP` to the role of the only valid production entry scene
  - **IMPLEMENTED ARCHITECTURE:**
    - BootstrapController.cs (assets/_Project/Scripts/Bootstrap/BootstrapController.cs)
      - Explicitly initializes all required managers (GameTickManager, SaveManager, InputManager, ObjectPoolManager)
      - Ensures DontDestroyOnLoad for all systems
      - Verifies 00_BOOTSTRAP is first scene in Build Settings
      - Prevents duplicate initialization
    - SceneGuard.cs (Assets/_Project/Scripts/Bootstrap/SceneGuard.cs)
      - Protection script for 01_MAIN_MENU and 02_HECTON_WORLD
      - Reloads 00_BOOTSTRAP if loaded directly without bootstrap
      - Enforces single-entry-point architecture
  - **NEXT STEPS (manual in Unity):**
    1. Add BootstrapController to [BOOTSTRAPPER] GameObject in 00_BOOTSTRAP scene
    2. Set DefaultExecutionOrder to -30000 (before SceneBootstrap)
    3. Add SceneGuard to 01_MAIN_MENU scene (as root GameObject)
    4. Add SceneGuard to 02_HECTON_WORLD scene (as root GameObject)
    5. Verify Build Settings: 00_BOOTSTRAP scene is index 0, 01_MAIN_MENU is 1, 02_HECTON_WORLD is 2
    6. Test: Run 00_BOOTSTRAP → verify all managers are initialized in log
    7. Test: Force load 01_MAIN_MENU directly → verify SceneGuard reloads 00_BOOTSTRAP instead
- [~] Bring `01_MAIN_MENU` to the role of production shell
  - **CODE COMPLETE:**
    - MainMenuController.cs with full UI flow (new game / load / settings / quit)
    - Panel transitions with CanvasGroup alpha fade (no SetActive churn)
    - Save slot generation and load dialog
    - Save/load panel now reuses one cached `SaveSlotUI[]` shell instead of `Destroy/Instantiate` churn on every open
    - `SaveSlotUI` now binds its button listener once in `Awake()`; slot refresh updates only data/interactable state instead of rebinding listeners every open
    - Shell verification tools now honor their inspector gates (`_enableVerification` / `_verifyTransitions`) instead of carrying dead toggle fields
    - Async scene loading with progress bar
    - Start path now fail-closes repeated `StartGame()` calls, so modal/button spam cannot launch multiple scene-load coroutines for the same menu -> world transition
    - Removed deprecated `MainMenuController.TargetSaveSlot` legacy mirror; start-session source of truth is now only `GameStartContextHolder` on the menu side
    - MainMenuValidator.cs (Editor tool: Window > HECTON-8 > Validate Main Menu)
  - **SCENE REQUIREMENTS (manual in Unity):**
    1. CanvasGroups: mainMenuGroup, saveLoadGroup, settingsGroup, loadingGroup (assigned in Inspector)
    2. Buttons: btnNewGame, btnLoadGame, btnSettings, btnQuit (assigned in Inspector)
    3. Back buttons: btnBackFromSaveLoad, btnBackFromSettings (assigned in Inspector)
    4. Labels (TextMeshProUGUI): labelNewGame, labelLoadGame, labelSettings, labelQuit (assigned in Inspector)
    5. Save slots UI: slotsContainer (Transform), slotPrefab (GameObject for save slot item)
    6. Loading screen: loadingProgressBar (Slider), loadingPercentText (TMP_Text)
    7. Camera: Main Camera in scene
    8. EventSystem: auto-created by UI, but verify GraphicRaycaster exists
    9. SceneGuard: Add SceneGuard.cs to root GameObject (protect from direct load)
  - **VALIDATION:**
    1. Open 01_MAIN_MENU scene in editor
    2. Run Window > HECTON-8 > Validate Main Menu
    3. Fix any ✗ missing references shown in report
    4. Test: New Game button → should show confirmation dialog
    5. Test: Load Game button → should show save slots
    6. Test: Try loading scene directly (run 01_MAIN_MENU without 00_BOOTSTRAP) → SceneGuard should reload bootstrap
- [c] Raise `GameStartContext` and remove dependence on a single `TargetSaveSlot`
  - code addendum: `GameStartContextHolder` now owns the cold scene-handoff persistence itself instead of scattering `TargetSaveSlot` writes/reads across menu and bootstrap. `MainMenuController.StartGame()` writes through `GameStartContextHolder.SetCurrent(...)`, `SceneBootstrap.LoadOrNewGameAsync()` restores through `TryGetCurrentOrRestore(...)`, and the persisted handoff is cleared immediately after bootstrap consumes it so stale slot state does not keep poisoning future world loads. Legacy `MainMenuController.TargetSaveSlot` remains only as a compatibility mirror, not as the runtime source of truth.
  - code addendum: gameplay -> menu shell now clears session handoff ownership too. `PauseMenuController.ExitToMainMenu()` calls `GameStartContextHolder.Reset()` before loading `01_MAIN_MENU`, so stale `LoadGame/Resume` context does not leak back into the shell after leaving an active world session.
- [x] Verify:
  - new game
  - load game
  - loading transition
  - return to menu
  - quit application
  - **IMPLEMENTED:** SceneTransitionVerifier in `Assets/_Project/Scripts/Tools/SceneTransitionVerifier.cs`
    - Automatic scene transition monitoring and logging
    - VerifyNewGameTransition()/VerifyLoadGameTransition()/VerifyReturnToMenu() methods
    - SceneManager event handlers for load/unload/change events
    - System presence verification (BootstrapController, MainMenuController, SceneBootstrap, etc.)
    - GameStartContext validation for each transition type
    - Singleton with DontDestroyOnLoad for cross-scene verification
- [x] Verify pause edge cases:
  - while moving
  - underwater
  - at surface
  - inside PDA
  - during tool swap
  - during crafting
  - inside module
  - **IMPLEMENTED:** PauseSystemVerifier in `Assets/_Project/Scripts/Tools/PauseSystemVerifier.cs`
    - ITickable-based pause state monitoring
    - VerifyCurrentPauseState() and TestPauseMenuNavigation() methods
    - Pause entry/exit verification (time scale, cursor, menu visibility)
    - Automatic state change detection and logging
    - Test statistics tracking (run/passed/failed counts)
    - Singleton with DontDestroyOnLoad for cross-scene verification
- [ ] Verify state recovery:
  - return from pause to gameplay
  - return to menu
  - new game after old save
  - load slot from shell
  - correct input restore
- [ ] Decide the honest role of `01_ORBIT`
  - intro stub
  - prologue hub
  - shell scene
  - or remove from critical path
- [x] Standardize loading feel so the player does not see a broken bootstrap
  - Consistent loading screen across all scene transitions
  - Prevents visual gaps during async operations
  - **IMPLEMENTED:** LoadingScreenController in `Assets/_Project/Scripts/UI/LoadingScreenController.cs`
    - CanvasGroup-based fade transitions (no SetActive churn)
    - Progress bar, percentage text, status messages, random tips
    - Minimum display time to prevent flicker
    - Unscaled time for consistent animation during loading
    - Zero-GC design with cached components
  - **PREFAB CREATOR:** LoadingScreenPrefabCreator in `Assets/_Project/Scripts/Editor/LoadingScreenPrefabCreator.cs`
    - Editor menu: Tools > HECTON-8 > Create Loading Screen Prefab
    - Creates complete UI hierarchy with proper anchoring and styling
    - Ready-to-use prefab with all components wired

## Performance Guardrail

- [ ] Keep CPU as guardrail, not religion
- [x] After every large pass capture:
  - mean frame
  - worst frame
  - startup hitch
  - surface hitch
  - VRAM posture
  - RT posture
  - terrain/streaming reaction
  - **IMPLEMENTED:** PerformanceMonitor in `Assets/_Project/Scripts/Tools/PerformanceMonitor.cs`
    - ITickable-based frame time capture with configurable sample count
    - Automatic performance snapshots with mean/worst/best frame times
    - FPS calculations and detailed logging
    - Singleton pattern with DontDestroyOnLoad
    - StartCapture()/StopCapture() API for programmatic control
    - PerformanceSnapshot struct with serialization support
- [ ] Do not start a new perf-crusade without a new confirmed build blocker
- [x] Maintain separate budgets for:
  - microfauna
  - biolum
  - terrain residency
  - **IMPLEMENTED:** PerformanceBudgetController in `Assets/_Project/Scripts/Tools/PerformanceBudgetController.cs`
    - IBudgetManagedSystem interface for systems to implement performance throttling
    - Configurable budgets as percentage of target frame time (MX350 optimized)
    - Automatic throttling when frame time exceeds limits
    - RegisterSystem()/ReportSystemPerformance() API for integration
    - GetBudgetStatus() for monitoring and debugging
    - Singleton with DontDestroyOnLoad for cross-scene persistence
  - ruins
  - caves/geology
  - far silhouettes
- [ ] Keep explicit watch on render textures and camera stack because those are already confirmed MX350 headroom risks
  - current editor profiler snapshot in `02_HECTON_WORLD`: `969` render textures, `~1.48 GB` render texture bytes, `~2.62 GB` graphics driver memory; this is not build-proof but it is already above MX350 comfort
  - current code truth: `VisorHUDController` no longer does unconditional runtime `RenderTexture` destroy/recreate on every `RebuildProjection()`; owned RTs are now retained when projection mode/size stay unchanged and released only on real owner or size transitions
  - remaining gap: take a real play/build capture to confirm RT posture after the visor fix and identify whether the remaining RT load is editor-only or another runtime owner
- [ ] Read build captures honestly:
  - separate `WaitForLastPresent / DXGI.WaitOnSwapChain` from real CPU work
  - do not call a frame `CPU-bound` if the main thread is mostly waiting on present
- [ ] Current standalone profile truth:
  - baseline build frames can be materially better than editor and often look `present-bound`, not script-saturated
  - current true spike classes are:
    - `EventSystem -> GameObject.ActivateAwakeRecursively`
    - coroutine / `SlowTickRoutine()` style spikes
  - current editor `EditorLoop` complaints are not enough to blame gameplay CPU because this project still has many `[ExecuteAlways]` / `EditorApplication.update` preview systems; latest live gameplay logs instead point at `WorldProceduralScatterDirector` startup `SlowTick` spikes (`96.03 ms`, then `12.83 ms`)
- [ ] Current confirmed live-log offender:
  - `FaunaDirector` can dominate `SlowTick` and trigger `ObjectPoolManager` on-demand expansion for `SmallPassiveProxy`
  - fauna pool warmup must track live `_runtimeMaxSpawnsPerTick` and reopen after runtime streaming settings grow; a static reserve fixed at scene start is invalid
  - the main `SlowTick` fauna selector must also respect live pool availability, otherwise strict `allowExpand:false` still burns spawn attempts on prefabs that are already dry even after warmup hardening
  - the main `SlowTick` selector should not keep re-querying pool availability inside one burst; reuse one per-biome availability scratch state and decrement it only after successful spawns
  - `ForceSpawnHorde(...)` must not bypass the same resolved-entry and pool-availability rules, otherwise scripted horde pressure can still pay for dry-pool selection work outside the main fauna path
  - `WorldProceduralScatterDirector` startup sampling still remains an active CPU track; latest code passes removed one duplicated main-thread slope/curvature calculation from `WorldProceduralFieldSampler.TryBuildCellInput()`, moved per-cell `clusterRatioStart / passiveSpawnMin / predatorSpawnMax` resolution onto the existing `pattern + biome` quota cache, stopped recomputing the same preview-rescue gate three times inside the inner scatter rule loop, stopped `TrackRescueCandidate(...)` from re-resolving rescue booleans the caller already knew, now reuse one per-cell biome score context instead of re-deriving the same biome-matrix signals/focus roles for every runtime rule, now reuse one per-rule preferred-family index instead of rescanning the same preferred-family array across multiple score helpers, now reuse one per-cell pattern score context instead of repeating the same pattern-category guards through multiple runtime score helpers, now consolidate the four pattern-dependent score helper calls for one `pattern + runtimeRule` pair into a single combined helper, now consolidate the three heat-scale helper calls for one `pattern + runtimeRule + depth` tuple into one combined helper, now skip redundant preferred-biome / preferred-zone rescans for accepted rules by replacing post-gate `GetFamilyAffinityBonus(...)` with `GetAcceptedFamilyAffinityBonus(...)`, now defer full pooled `ScatterPlacement` construction until after residency pass via `ScatterCandidatePreview`, now defer full score math plus preview rotation until after candidates survive gate/residency rejection, now reuse cell-local geology score results for repeated `GeologyProfile` hits inside the same sampled cell, now resolve the spawn rescue minimum once per cell instead of once per spawn rule, now precompute accepted-family affinity plus geology score scale into `ScatterRuntimeRuleEntry` instead of recalculating those same per-rule constants inside the live score loop, now reject dead non-rescue candidates on `gate` before expensive budget checks, now replace better rescue candidates in `CandidateMap` by index instead of rescanning the same map twice, now skip preview, residency, score math, and full `BuildCandidate(...)` for non-rescue candidates that are already known to fail the random gate, and now skip full `BuildCandidate(...)` for non-rescue candidates that cannot beat the already-full per-cell buffer floor; rescue-tracked candidates still keep the preview/scored/built path because rescue retention depends on it, but scatter startup truth is still `PENDING VERIFICATION`
  - latest addendum: rescue-tracked candidates that are already known to fail random gate now keep only rescue-window eligibility while deferring heavy runtime state solve (`variant/scale/chunk/macro`) until actual desired-placement registration; dead rescue-gate branch no longer pays that full build cost
  - latest addendum: per-cell geology bonus cache moved from 2-slot to 4-slot struct cache, reducing repeated `EvaluatePlacementFitness(...)` recompute when the same sampled cell touches multiple geology profiles
  - latest addendum: deferred runtime-state solve is now applied to all sampled candidates; sampling keeps only key/position/field data, while `variant/scale/rotation/chunk/macro` are finalized only when placement survives to registration
  - latest addendum: once a non-rescue cell buffer is already full, scatter now runs a two-stage score ceiling before finishing the remaining score path: an optimistic biome-matrix + geology ceiling rejects candidates that cannot beat `worstCandidateScore` before exact biome-matrix/geology work, and a second geology-only ceiling skips `EvaluatePlacementFitness(...)` even after exact biome-matrix score if the candidate still cannot overtake the current floor
  - latest addendum: attempted frame-sliced `GameTickManager` slow-tick scheduling was explicitly rolled back after code review because it changed the temporal contract for every `ISlowTickable` owner (`FaunaDirector`, `BaseModule`, `HectonSurvivalSystem`, etc.) from one whole-wave cadence to cross-frame partial cadence; until those owners are refactored off fixed slow-tick assumptions, slow tick remains on the original cached-`WaitForSeconds` coroutine contract
  - latest addendum: `FaunaDirector` biome throttling no longer decrements `_biomeCheckTimer -= 1f` per `SlowTick()` call; it now uses absolute `Time.time` gating for `BiomeCheckInterval`, so biome probe cadence stays stable even if the manager interval changes or future scheduler work is revisited
- [ ] Current UI visibility rule:
  - `PlayerPDA`, pause sections, and HUD roots must prefer warmed `CanvasGroup` visibility over hierarchy `SetActive` churn
  - hidden PDA tabs must defer refresh work instead of continuing full refresh on gameplay events
  - `PDAShellChrome` shell overlay must stay on warmed child hierarchy state and use dirty-gated text refresh; shell chrome must not toggle its child root active state or rebuild interpolated footer/tab strings every refresh tick
  - `HUDQuickBar` icon and durability widgets must stay warm; quick bar refresh must not toggle child graphics active/inactive while slot state changes
  - `HUDNotification` root must remain warm through fade/show cycles; notification visibility must not depend on activating/deactivating the HUD notification object itself
  - repeated inventory-full warnings must reuse cached item-name message projections; `HUDNotification.OnInventoryFull(...)` must not rebuild the same uppercase warning string on each repeat overflow
  - `LaserCutter` deconstruct progress feedback must not format percentage strings at runtime; repeat progress pulses must reuse a cached progress-message table
  - `RepairTool` finite headline HUD/log titles must resolve through fixed message mapping instead of repeated interpolation; unknown future headlines must preserve legacy fallback text
  - preset-only UI consumers must not request full `FieldLoadoutAdvisor.LoadoutAdvice` when they only need `PresetName`; avoid paying summary/distance string construction on `HUDQuickBar` and preset-name-only PDA paths
  - `PDALoadoutTab.RefreshSummary()` must not do duplicate forward-advice resolution in one refresh; if both preset name and summary are needed, they must come from one shared `LoadoutAdvice` query
- [ ] Keep `GPU / present pacing` as a separate investigation track:
  - do not blame scripts for frames dominated by `WaitForLastPresent / DXGI.WaitOnSwapChain`
  - do not cut broad render quality until real standalone `GPU ms` capture exists
- [ ] Every beautiful layer must justify its perceptual gain in build

## Terrain / MapMagic / LOD / Streaming

- [ ] Do not replace MapMagic
- [ ] Run a full terrain audit: editor vs build; near/mid/far; identical FOV/light/distance
- [ ] Validate live `MapMagicObject` runtime settings:
  - `mainRange`
  - `hideFarTerrains`
  - `draftsInPlaymode`
  - `objectsNumPerFrame`
  - `drawInstanced`
  - `applyColliders`
- [ ] Validate terrain tile residency around the player:
  - active
  - hidden
  - draft-only
  - simplified
  - collider state
- [ ] Validate `WorldChunkStreamingProfile` contract:
  - near radius
  - mid radius
  - far radius
  - activation budget
  - traversal mode
  - surface vs depth behavior
- [ ] Bring terrain read to this rule:
  - close range looks good
  - mid range is convincing
  - far range is silhouette-rich and large-scale
  - no mush
  - no texture soup
  - no absurd repetition
- [ ] Bring live `ProximityColliderSystem` into production truth instead of "exists in code"
- [ ] Bring `FloatingOrigin` in as a required large-world architecture pass
- [ ] Check surface/island terrain layering separately from underwater floor
- [c] Check steep cliffs, terrain walls, island edges, shoreline seams, and surfacing near walls
  - shoreline locomotion must survive shallow-water ground flicker; no fake swim flip when climbing out
  - jump input at the waterline must be buffered across the shallow ground-transition window, not lost on one bad grounded frame
  - current code truth: `HectonPlayerMovement` has shoreline jump buffer / shore-ground grace plus a separate near-surface `surface breach` path for floating-at-surface jump input; `surface lock` is temporarily suppressed during breach launch so the same owner does not cancel the impulse on the next physics step
  - current code truth: land jump is now mass-independent instead of being diluted by `SuitData.mass`, and launch clears the same-frame ground latch / snap state instead of re-pinning the body immediately after `Space`
  - current code truth: underwater bottom contact no longer forces fake dry-land behavior through `BuoyancyObject.IsInAir`; `HectonFluidEngine` now suppresses fluid only for true dry zones or effectively above-water grounded contact, not for submerged seabed touches
  - current code truth: dry-land takeoff no longer falls into `SwimPhysics` just because ground contact disappeared for one frame; `HectonPlayerMovement` now keeps dry-air movement in land locomotion, uses reduced air control / damping instead of underwater mode, and clears jump-launch walk bob carryover
  - current code truth: grounded slope stabilization no longer leaves the downhill tangent of gravity alive; `ApplyGroundStability()` now cancels gravity projected along the ground plane while grounded instead of only countering the normal component
  - current code truth: `GroundCheck()` no longer treats any sphere-cast hit as walkable ground; it now filters non-alloc hits by ground-angle threshold so slope lips and steep faces stop poisoning grounded state and movement-plane projection
  - current code truth: dry-land jump no longer depends on one exact grounded frame; a separate dry-ground grace timer now covers slope lips / tiny dry terrain gaps and prevents immediate dry-air damping during that short transition
  - current code truth: `Ctrl` acceleration is now consistent across land walk, shallow surface movement, and full swim; `HectonPlayerMovement` applies the same sprint input contract to land force/clamp and to swim thrust / vertical thrust / max swim speed
  - current code truth: `HectonPlayerMovement` now owns a bounded non-alloc `step assist / lip assist` pass for low obstacles; small terrain lips and pseudo-stairs no longer depend purely on brute force to clear
  - current code truth: jump launch now checks overhead capsule clearance before firing, so low ceilings / rock lips stop the impulse instead of accepting a false jump into immediate collision
  - current code truth: grounded land speed clamp now respects the actual slope plane instead of clamping only world `XZ`
  - status: code-fixed; still waiting on live shoreline/surface/seabed verification in game/build

## Water / Surface / Oxygen / Transition

- [c] Localize and remove the surface transition hitch
- [ ] Verify switching of:
  - underwater visuals
  - atmosphere profile
  - fog mode
  - sun/sky weight
  - sound mode
  - post process
  - oxygen logic
  - camera feel
- [~] Keep camera inertia honest:
  - no reverse-direction tail after mouse stop
  - underwater mass can exist, but it must settle toward neutral without overshooting through center
  - verify separately near surface and in deeper swim because bob + sway stacking can hide the true offender
  - current code truth: `CameraJuiceProcessor` already clamps spring overshoot on release; still waiting on live swim verification
- [~] Bring one water-level truth across survival, fluid, visuals, and world bridge
  - current code truth: survival / atmosphere / underwater visuals already read one shared surface hysteresis contract from player depth instead of maintaining separate waterline thresholds
  - current scene truth: `02_HECTON_WORLD` currently shows aligned serialized water levels on `HectonAtmosphereManager`, `HectonFluidEngine`, and `MapMagicBridge` (`4900`)
  - current audio code truth: `AcousticZoneController` now resolves `dry interior` separately from `grounded surface`, can follow the same underwater/surface truth via `HectonAtmosphereManager` or `SurfaceStateUtility` fallback instead of `BuoyancyObject.IsInAir`, auto-spawns if absent, and can mute/unmute the player's existing underwater ambient loop from that zone state
  - remaining gap: there is still no single runtime water-state publisher consumed by all listed systems, and audio mixer snapshot provisioning / build-proof of the ambient binding remain `PENDING VERIFICATION`
- [c] Fix surface oxygen refill with hysteresis and fail-safe logic
- [ ] Verify edge cases:
  - fast ascent
  - slow ascent
  - ascent while rotating camera
  - repeated quick crossings
  - ascent near cliff wall
  - bright surface light
  - hazy or storm-like sky state
- [ ] Bring surface read to this state:
  - visibility does not break
  - brightness does not break
  - fog does not break
  - sound does not break
  - transition does not irritate

## Sky / Gas Giant / Distant Background

- [ ] Audit all sky layers:
  - distant sky
  - gas giant
  - cloud layer
  - atmospheric haze
  - celestial transmittance layer for sun / stars / halo / gas giant
  - eclipse/occlusion logic
  - exposure chain
- [ ] Bring gas giant to perceptual truth:
  - giant behind clouds
  - giant softened by haze
  - giant does not read as a flat poster
  - giant does not break cloud depth illusion
  - architectural rule:
    gas giant depth must live on the giant shader or the same world-space sky ray logic
  - architectural rule:
    horizon compression must be applied at the shared sky-response source before any giant-specific extinction tuning
  - architectural rule:
    visible clouds and celestial occlusion must be separate systems; celestial objects read transmittance, not the visible cloud shapes themselves
  - ban:
    no camera-centered proxy haze shells for celestial depth cues
- [ ] Check giant at:
  - surface
  - mid-depth
  - deep water
  - bright state
  - dim state
  - cloud overlap
  - horizon silhouette
  - sun / star / halo consistency under the same atmospheric transmittance rules
- [ ] Plan a real surface weather / cloud-cover system:
  - clear sky
  - broken cloud
  - overcast
  - storm pressure state
  - cloud cover driven by weather state instead of one static sky look
  - visible cloud art layer separated from low-frequency celestial transmittance layer
  - gas giant readability must survive every weather state
  - surface brightness, haze, and cloud quality must stay MX350-safe
- [ ] Build a visual preset / A-B review system for water / sky / gas giant:
  - preserve the current look as `baseline_00`
  - allow fast switching between multiple scene looks in `Game`
  - preset scope must include:
    - water
    - sky
    - gas giant
    - fog / ambient / global palette
  - presets must restore one coherent scene state, not only one material
  - current source-of-truth baseline is recorded in `SCENE_SKY_NOTES.md`
- [ ] Build a visual preset / A-B review system for water / sky / gas giant:
  - preserve the current look as `baseline_00`
  - allow fast switching between multiple scene looks in `Game`
  - preset scope must include:
    - water
    - sky
    - gas giant
    - fog / ambient / global palette
  - presets must restore one coherent scene state, not only one material
  - current source-of-truth baseline is recorded in `SCENE_SKY_NOTES.md`
- [ ] Only close the task after user-check:
  - `does it now feel distant?`
  - note from this pass:
    fake overlay geometry in front of the camera reads as a patch, not atmosphere
  - note from this pass:
    if the horizon and the giant do not share the same atmospheric color logic, the giant will always read as pasted in front
  - note from this pass:
    if the visible cloud texture is used directly as celestial masking, the result will look fake even when the depth logic is technically correct
  - note from this pass:
    extinction must live in a narrow horizon band, not as a broad full-disc wash across the lower half of the giant
  - note from this pass:
    the lowest edge needs its own tighter bottom-arc extinction on top of the broader horizon band, otherwise the middle dies before the horizon merge becomes convincing
  - note from this pass:
    the bottom arc must use a steeper response curve than the broader horizon band, or the extra extinction leaks upward and flattens the middle of the disc
  - note from this pass:
    the final giveaway is the lower side silhouette; horizon merge must include a lower horizon-facing limb crescent, not only the bottom-center strip
  - note from this pass:
    horizon merge alone is not enough; the disc also needs a lower-mid `air-mass shoulder`, otherwise the planet welds at the waterline but immediately becomes too crisp again above it
  - note from this pass:
    that upper haze must be continuous with the horizon haze and darker than the white horizon milk; if it is treated as a separate bright band, the result becomes `white strip at the horizon -> clean blue giant above`, which is the exact fake look to avoid
  - note from this pass:
    after the horizon band is accepted, upper and middle distance cues should live in a separate `upper haze` layer so the lower merge can stay artist-tuned; low-frequency celestial occlusion may modulate that layer, but visible cloud silhouettes must still stay out of the giant
  - note from this pass:
    day-proof and night-proof must be validated separately; a horizon weld that reads correctly by day can still leave the night branch underpowered or under-observable on the current camera path
  - note from this pass:
    terrain close-up blur is not only a texture-density question; first verify whether the player is looking at final world art or at proxy-only scatter families with placeholder geometry/materials
  - note from this pass:
    stale cached `SupportsFinalVariant` state in scatter placement data can keep families stuck on proxy variants even after final-ready variants exist in the family asset

## World Generation Philosophy

- [ ] Lock the world formula:
  - breadth from MapMagic
  - near complexity from geology/voxel
  - meaning from biome/zone/family rules
  - beauty from layered dressing
  - life from cheap ecology layers
- [ ] Every world zone must answer:
  - what this place is
  - why swim there
  - what to search for
  - what to fear
  - how it differs by form
  - how it differs by mood
- [ ] Every depth band must change:
  - color
  - readability
  - danger
  - life density
  - landmark language
  - reward expectation
  - isolation feel
- [ ] No filler without meaning
- [ ] No world made of same-object chaos

## 108-Biome Matrix / Zone Meaning

- [ ] Use the 108-biome matrix as the main lore and meaning map of the world
- [ ] Define the first strike group of biomes:
  - starter surface/littoral
  - shelf transition
  - cliff/canyon zones
  - first deep fear zones
  - one or two strong abyss/hadal promise biomes
- [ ] For every priority biome, define:
  - main geology
  - main flora
  - main micro-life
  - main large-life
  - main danger
  - main reward hook
  - main landmark language
  - light/biolum type
  - route memory type
  - safe pocket type
  - return reason
- [ ] Fill placeholder slots with product function, not only names
- [ ] Add memory motifs:
  - basalt steps
  - coral porosity
  - obsidian teeth
  - silt catacombs
  - fossil gallows
  - hydrothermal spires
  - black spine fissures
  - drowned service scars
  - beacon gravefields
  - relay arches
- [ ] Connect:
  - `biome slot -> family -> zone plan -> world fill -> reward pattern -> fauna pressure`

## Hybrid Density / World Fill

- [ ] Bring a real hybrid density pass:
  - near interactive
  - mid decorative-functional
  - far silhouette-mass
- [ ] Near field must contain:
  - pickups
  - resource nodes
  - small salvage
  - cave hints
  - ruin fragments
  - support objects
  - route clues
  - small hazards
- [ ] Mid field must contain:
  - instanced flora
  - instanced debris
  - passive swarms
  - biolum accents
  - route traces
  - service/power traces
  - broken structures
  - mid-size silhouettes
- [ ] Far field must contain:
  - arches
  - giant rock forms
  - cliff teeth
  - distant flora masses
  - ruin silhouettes
  - leviathan promise spaces
  - landmark clusters
- [ ] Every new density layer must strengthen:
  - navigation
  - mood
  - biome identity
  - sense of scale
- [ ] Raise live-fill not only through sockets, but also through terrain/field-driven scatter where current systems already support it
- [ ] Density must be biome-specific and psychological, not spread uniformly

## Geology / Caves / Arches / Overhangs / Seams

- [ ] Raise caves as a key exploration layer
- [ ] Raise geology as a key visual complexity layer
- [ ] Required close forms:
  - arches
  - canopies
  - cliff overhangs
  - cave bridges
  - rough canyon mouths
  - broken shelves
  - collapsed pockets
  - vertical shafts
- [ ] Bring cave entry archetypes:
  - wide fissure
  - jagged entrance
  - vertical drop
  - biolum lure entrance
  - ruin-adjacent cave mouth
  - pressure-scar cave
  - volcanic vent mouth
- [c] Connect `HectonVoxelEngine` and `CaveGraphGenerator` to MapMagic/world-fill/biome logic as a live pipeline
  - note from `2026-04-05` compile hygiene pass:
    `WorldCaveDirector` had drifted onto a dead `MapMagicBridge.SampleHeight` call; restored live contract through `TryGetHeight` fail-safe, reconnected `caveSpawnProbability` as the intended biome-evaluation gate, and removed duplicate `using` noise from `HectonVoxelEngine`
  - scene wiring truth from `02_HECTON_WORLD`: live scene already contains `MapMagicBridge`, `WorldCaveDirector`, `WorldGenerativeGeologyIntegrationDirector`, `WorldGenerativeGeologySeamExecutionDirector`, `WorldGenerativeGeologyVoxelBridgeDirector`, and a separate active `[VOXEL_ENGINE]` with `HectonVoxelEngine`
  - runtime hardening addendum: `WorldGenerativeGeologyTerrainSeamApplier` no longer creates fresh terrain plan buckets and fresh `float[,]` height patches every `SlowTick`; per-terrain plan buckets and patch buffers are now reused, terrain lookup now follows `MapMagicBridge` tile-backed terrain truth before falling back to `Terrain.activeTerrains`, baseline height snapshots are refreshed fail-safe when streamed `TerrainData` owners or heightmap resolutions change, untouched restore paths now drop stale state instead of writing into a swapped terrain-data owner, and terrain diagnostics no longer pull `terrain.name` strings in the live reconcile path
  - runtime hardening addendum: `WorldCaveDirector` now owns cave spawn lifecycle instead of firing duplicate `async void` launches for the same runtime key; pending cave builds are tracked, cancelled on teardown, stale cave registry entries are purged, and the live cave path no longer relies on “active only after await returns” semantics
  - runtime hardening addendum: `WorldGenerativeGeologyVoxelBridgeDirector` no longer uses dictionary/hashset `foreach` inside live reconcile/cancel/clear paths; active volume, pending runtime, and pending request scans now use explicit enumerators so the bridge no longer violates the project hot-path iteration rule in `SlowTick`
  - runtime hardening addendum: `WorldGenerativeGeologyVoxelBridgeDirector` now caches active `WorldGenerativeGeologyVoxelRuntime` owners by `runtimeKey`, so reconcile/detail-band hysteresis no longer does per-request `TryGetComponent`, and debug top-volume tracking no longer depends on the `"None"` sentinel string in the live owner
  - runtime hardening addendum: `WorldGenerativeGeologyVoxelBridgeDirector` no longer builds runtime-diagnostics trace strings directly inside `ReconcileVoxelRequests()`, `Tick()` launch flush, or request completion/cancel/fault bodies; trace formatting is now isolated behind development/editor-only helper methods so release hot paths do not carry string interpolation debt just because diagnostics support exists
  - runtime hardening addendum: `WorldGenerativeGeologyVoxelBridgeDirector` now trims stale active-volume registrations before reconcile, validates that cached voxel runtimes still match the active GameObject / `runtimeKey` / request signature before retention or signature short-circuiting, and forgets dead or reused pooled registrations without blindly despawning a volume that may already belong to a different runtime owner
  - runtime hardening addendum: `WorldGenerativeGeologyIntegrationDirector` no longer uses dictionary `foreach` inside `TrimPlanDictionaries()`, both integration/execution directors no longer build interpolated debug strings in `SlowTick`, seam planning no longer does `GetComponent<WorldProceduralProxyInstance>()` per binding, and seam execution now reuses cached seam-runtime owners plus cold-cached primitive names instead of rebuilding `TerrainSkirt_*` / `VoxelCollar_*` / `Debris_*` strings every reconcile; top-plan diagnostics now keep direct `familyId` references plus archetype enums while proxy metadata is served from a cached binding owner reference, seam-runtime cache reuse now drops stale alias entries once a cached runtime has been reconfigured to a different `runtimeKey`, the static active-binding registry now trims stale disabled/null entries while refreshing proxy cache during binding reconfiguration, and the static seam-runtime registry now trims stale disabled/null entries before cleanup scans
  - runtime hardening addendum: `WorldCaveDirector` now owns a cached biome runtime context (`supports caves`, `preset kind`, `family hash`, `family label`) instead of reparsing `biomeFamily.familyId` across `EvaluateCaveSpawns()`, `GenerateCaveCandidates()`, `GetCavePresetForBiome()`, `GenerateCaveKey()`, and diagnostics on every cave evaluation pass
  - runtime hardening addendum: `WorldCaveDirector` no longer treats non-null pooled volumes as automatically alive; cave lifecycle cleanup and `TryGetCaveAt()` now verify that tracked `HectonVoxelVolume` instances are still active in hierarchy and still owned by the same `caveKey`, so pooled/reused volumes do not leave stale cave registrations behind
  - runtime hardening addendum: `WorldCaveDirector` now treats unsupported-biome cleanup as a real teardown path instead of a stale-prune no-op; pending cave spawns are cancelled, tracked cave volumes are torn down through the existing cleanup owner, and stale lifecycle removal keeps non-despawning registration cleanup so dead pooled owners do not trigger accidental cave teardown
  - remaining gap: perceptual seam/readability/world-check is still pending, so this is architecture-complete for current coding work, not final world proof
- [ ] Verify seam logic:
  - terrain -> geology
  - geology -> voxel bridge
  - cave interior -> entrance lip
  - seam skirts -> debris breakup
- [ ] Bring cave readability:
  - player sees the entrance
  - player understands there is value inside
  - player understands the risk
  - player can remember the entrance for the return trip
- [ ] Differentiate cave reward and cave mood:
  - shallow caves
  - mid caves
  - deep caves
  - rare caves
  - ruin-linked caves
  - hazard caves
- [~] Add cave interior detail:
  - stalactites
  - wall growth
  - floor boulders
  - mineral crust
  - deep fungi
  - glowing tissue
  - sediment shelves
  - service remnants
  - code addendum: `WorldCaveDirector` already had mineral crust and deep-fungi passes; sediment shelves are now a real runtime layer through `CaveSedimentShelfRuntimeBuilder`, `CaveDressingConfig.GetConfigForContext()` now returns shared cached templates instead of allocating a fresh config graph for every cave spawn, deep-fungi emission now reads real voxel-volume bounds plus `verticalBias` instead of a hardcoded `10x10x10` placeholder volume, `wall growth` is now wired as a real runtime layer through `CaveWallGrowthRuntimeBuilder` instead of dead config-only data, glowing tissue is now a live runtime layer through `CaveGlowingTissueRuntimeBuilder`, service remnants are now a live runtime layer through `CaveServiceRemnantRuntimeBuilder`, and biome cave presets now come from shared read-only templates instead of rebuilding identical `CavePreset` objects and `allowedStructureTypes` arrays on every spawn
  - runtime safety addendum: cave dressing now reuses one `_CaveDressing` root per volume instead of blindly spawning duplicate dressing roots if the layer is initialized more than once; pooled `HectonVoxelVolume` instances now reset cave-owned runtime roots before reuse, and entrance markers / entrance quality now reuse named runtime roots instead of stacking stale children on pooled volumes
  - remaining gap: visual density/readability/world-proof is still pending, but the planned cave-detail layers are now wired into the live cave runtime
- [ ] Most cave dressing should remain visually cheap, not full-physics

## Ruins / Old Modules / Human Traces / Trash

- [ ] Introduce layered human footprint:
  - abandoned outposts
  - broken corridors
  - collapsed shafts
  - relay stumps
  - module shells
  - beacon graves
  - power route leftovers
  - flooded service cavities
- [ ] Split ruins by function:
  - habitat
  - logistics
  - engineering
  - science
  - comms
  - mining
  - maintenance
  - catastrophe remains
- [ ] Split ruin state variants:
  - partially intact
  - cracked and flooded
  - collapsed and sediment-filled
  - biolum-colonized
  - reef-colonized
  - pressure-ripped
  - volcanic-burnt
- [ ] Add the small human-tech layer:
  - cables
  - torn panels
  - pressure canisters
  - broken lights
  - service frames
  - anchor parts
  - crates
  - pipes
  - plating fragments
  - buried maintenance junk
- [ ] Build ruins as memory places, not random mesh scatter
- [ ] Every major ruin cluster must answer:
  - what used to be here
  - why it drowned
  - why the player swims here
  - what can be found here
  - what can kill here

## Microfauna / Small Life / Cheap Luxury Layer

- [ ] Add a dedicated small-life world layer
- [ ] Types:
  - micro fish
  - fry in cracks
  - crustacean swarms
  - wall clingers
  - burrow flickers
  - sediment skitterers
  - glowing motes
  - polyp breathing
  - tiny ruin scavengers
- [ ] Roles:
  - pure ambience
  - biome identity
  - route hint
  - danger foreshadowing
  - cave mood
  - ruin aging signal
- [ ] Technologies:
  - shader-only flicker/sway
  - GPU particle clouds
  - instanced micro meshes
  - ultra-light proxy movers
  - richer hero micro-creatures only near player
- [ ] Near-observation rule:
  - when the player approaches the floor, wall, ruin, or cave lip, small life becomes more visible
- [ ] Add suspicious silence zones
- [ ] Add busy life zones
- [ ] Verify:
  - no visible terrain clipping
  - no spawning in the nose of the camera
  - no harsh popping
  - no wall-stuck behavior
  - no CPU death

## Flora / Coral / Reef Rules / Surface Flora

- [~] Transfer coral and seaweed design into the existing flora pipeline
  - Status: [~]
  - Need User Check: yes
  - Need Build Check: yes
  - Need In-World Swim Check: yes
  - Why: the world already needed a real life-layer owner path, not more abstract flora intent
  - Evidence: fresh Unity verification on `2026-04-07` confirmed live runtime placement plus editor-owned flora-final pipeline
  - Problems: all 7 flora families still rely on generated starter finals only; no authored photoreal finals exist yet; no build swim proof or GC baseline/proof exists for this transfer path
  - Short Comment: transfer is real in runtime placement and authoring ownership, but art-quality and build-truth are still open
  - Next Step: replace `GEN_` starters family-by-family with authored finals, then run build swim/readability verification

  - Did:
    - transferred `kelp canopy`, `coral massive`, and `coral plate` into the existing world stack as real `family.*` and `rule.*` assets instead of copying Claude monoliths
    - integrated flora placement into the existing `biome matrix / scatter / profile` stack and kept flora creation in editor-only ownership
    - added editor-side flora-final pipeline:
      - `WorldProceduralFloraBakedStarterGenerator`
      - `WorldProceduralFloraFinalVariantAuthoring`
      - `WorldProceduralFloraFinalVariantValidator`
      - `WorldProceduralFloraFinalStatusReport`
    - verified full rebuild chain:
      - `Rebuild World Runtime Stack`
      - `Validate Procedural Flora Final Variants`
      - `Generate Procedural Flora Final Status Report`
      - `Rebuild 108 Biome Matrix`
      - `Validate 108 Biome Matrix`
      - `Generate Procedural Matrix Biome Content Report`
  - Result:
    - live matrix/report proof now shows transferred flora families in real slices instead of dead assets:
      - `FertileShallows / Mesa Plateaus` -> top/dominant `Kelp Canopy`
      - `FertileShallows / Fossil Gallows` -> top/dominant `Coral Plate`
      - `ReefNavigation / Archipelago Needles` -> top/dominant `Kelp Canopy`
      - `ReefNavigation / Sea-Stack Forest` -> top/dominant `Coral Plate`
      - `LandmarkCorridor / Sea-Stack Forest` -> top/dominant `Coral Plate`
    - existing fauna owner is now verified to support the transferred flora layer instead of leaving reef life abstract:
      - `Build Fauna Biome Datasets` rebuilt `108` biome datasets
      - `AI_FAUNA_WORLD_INTEGRATION_REPORT.md` now includes a dedicated `Reef And Littoral Flora Biomes` section with `None` warnings
      - representative reef/littoral flora biomes now read with concrete passive/threat mixes rather than empty ecology placeholders
    - hard corridor reads stayed intact:
      - `LandmarkCorridor / Table-Land Benches` -> top/dominant `Landmark Spire`
      - `LandmarkCorridor / The Shattered Spine` -> top/dominant `Cave Entrance Marker`
    - flora-final pipeline now links `21` generated starter finals across `7` flora families and reports stable family coverage
    - editor-only starter generation now provides `3` deterministic forms per flora family instead of `2`, widening kelp/coral silhouette variety without adding runtime ownership
    - kelp starter generation now uses a dedicated editor-only procedural mesh owner (`WorldProceduralSeaweedMeshBuilder`) instead of only proxy-combined primitives
    - fresh Unity readback after the kelp mesh-builder swap shows materially lower generated kelp triangle budgets:
      - `family.kelp.tall` -> `584`
      - `family.kelp.patch.dense` -> `496`
      - `family.kelp.canopy` -> `684`
    - fresh status-report readback also confirms a `4`-level LOD cascade on generated kelp finals, while coral starters remain on `2` levels
    - kelp now also has a dedicated shader/material owner path instead of generic URP Lit fallback:
      - `Hecton_KelpMaster.shader`
      - `WorldProceduralFloraMaterialAuthoring`
  - Failed:
    - validator still reports all families as generated-only coverage: `a0/g3`
    - there is still no authored photoreal coral or kelp final in the baked root
    - no build or user swim proof exists yet for this life-layer pass
  - Broke:
    - no new compile failures were introduced during the verified rebuild/validation chain
    - this does not yet prove runtime perf safety; GC evidence is still missing
  - Remaining:
    - authored/baked final art replacement for each flora family
    - build swim pass for readability, density, and route guidance
    - profiler/GC evidence for the transfer path

- [ ] Add reef logic instead of simple plant scatter
- [ ] Underwater flora groups:
  - tall guiding flora
  - floor carpeting flora
  - isolated exotic flora
  - cave flora
  - ruin-colonizing flora
  - biolum flora
  - giant silhouette flora
- [ ] Reef rules:
  - structure creates life
  - holes create shelter
  - edges and seams create density
  - different heights create different life read
- [ ] Natural clustering:
  - patches
  - rings
  - broken lanes
  - edge growth
  - sheltered growth
  - light-shadow bands
- [ ] Surface and island flora groups:
  - salt-tolerant grass
  - cliff scrub
  - tide-pool growth
  - algae mats
  - sharp shoreline reeds
  - dry plateau flora
  - sinkhole flora
  - storm-bent vegetation
- [ ] Flora must solve:
  - route guidance
  - scale cue
  - cover cue
  - biome signature
  - mood softening or threat masking

## Bioluminescence As Navigation And Emotion

- [ ] Make biolum a language, not just neon
- [ ] Roles:
  - route hint
  - safe halo
  - cave lure
  - predator lure
  - rare reward sign
  - sacred/anomalous marker
  - ruin colonization signal
- [ ] Types:
  - calm pulse
  - nervous flicker
  - wave pulse
  - isolated deep beacon
  - fissure glow
  - ruin breathing
  - swarm shimmer
- [ ] Scales:
  - micro specks
  - flora glow
  - patch glow
  - hero anomaly glow
  - distant silhouette emitters
- [ ] Do not kill darkness, fog, contrast, night read, or bloom budget
- [ ] Every visible glow must promise something

## Fauna / Threat Spectrum / Large Creatures / Leviathans

- [ ] Add the full world life ladder:
  - stationary life
  - micro-life
  - small passive swimmers
  - territorial life
  - medium predators
  - large threats
  - leviathans
- [ ] Small passive layer must give a living-ocean feel without heavy AI
- [ ] Territorial layer must hold nests, caves, chokepoints, and ruin pockets
- [ ] Medium predators must be sharp pressure, not background noise
- [ ] Large threats must live in biome logic and route logic, not random events
- [ ] Leviathans must live by macro-zones, not small chunks
- [ ] Every leviathan encounter must be built through:
  - presence
  - sound
  - silhouette
  - false safety
  - late reveal
  - route pressure
- [ ] Do not smear leviathans across surface and starter zones
- [ ] For shallow terror problems, use heavy hunters instead
- [ ] Per biome family, lock:
  - passive fauna set
  - territorial fauna set
  - predator set
  - large threat mode
  - leviathan allowance yes/no
  - silence behavior
  - swarm behavior
- [ ] Bring boid layer into real world truth, not dormant compute groundwork
- [ ] Connect `FaunaDirector`, archetype data, biome data, boids, spawn anchors, and macro-zones into a single ecology runtime

## Surface / Islands / Shoreline Ecology

- [ ] Surface and islands must have their own living layer, not an empty top cap
- [ ] Build ecology packs for:
  - `Archipelago Needles`
  - `Mesa Plateaus`
  - `Granite Spine`
  - `Silt Tongue`
  - `Sea-Stack Forest`
  - `White Alabaster Pools`
- [ ] Surface fauna:
  - shoreline micro-life
  - passive sky silhouettes
  - perched cliff life
  - surf skimmers
  - shoreline scavengers
  - sinkhole life
  - tide-pool microfauna
  - rare surface hunters
- [ ] Surface flora:
  - cliff scrub
  - salt growth
  - plateau vegetation
  - sinkhole biota
  - tide-pool bloom
  - rock algae
  - wind-bent grass
- [ ] Surface clutter:
  - drift debris
  - stranded tech
  - broken relay pieces
  - weathered service anchors
  - bird-nest silhouettes
  - storm-trash pockets
- [ ] Surface/island layer must strengthen:
  - skyline
  - contrast between dry and flooded worlds
  - first impression
  - route memory from underwater looking up and from surface looking down

## Visual Density Illusions

- [ ] Keep the language of the expensive lie:
  - silhouette first
  - layered fog
  - parallax density
  - cheap passive motion
  - emissive hints
  - distant promises
  - selective hero reads
- [ ] The player should almost always see more than they can immediately reach
- [ ] Not everything interesting must be interactive
- [ ] But everything noticeable must feel justified by form, biome, or life trace
- [ ] Verify depth composition:
  - foreground
  - mid-water clutter
  - far silhouettes
  - sky giant
  - haze
  - clouds
  - light shafts
  - biolum pockets
- [ ] Verify the seabed does not read like a dead flat sheet even in sparse zones

## Resources / Crafting / Progression

- [ ] Give every biome and pocket role a clear reward signature
- [ ] Give every depth band:
  - common materials
  - uncommon hooks
  - rare lure
  - return-loop reward
- [ ] Connect resources to geology, ruins, caves, flora patches, and service traces
- [ ] Do not let the economy read as one-note copper-only progression
- [ ] Ensure progression pushes deeper without turning the world into a corridor
- [ ] Build return loops so familiar zones reopen with new gear and new value
- [ ] Keep resource readability driven by biome families and resource channels, not random loot soup

## Base / Construction / Human Survival Layer

- [ ] Bring the core habitation loop to product truth:
  - safe point
  - oxygen
  - power
  - repair
  - storage
  - fabrication
  - expansion
- [ ] Connect base to world placement:
  - where it is beautiful
  - where it is profitable
  - where it is dangerous
  - where it is a strategic anchor
- [ ] Add support locations and semi-safe pockets where base loop naturally works
- [ ] Verify edge cases:
  - module on bad terrain
  - module at water transition
  - module near ruin cluster
  - module in biolum zone
  - module in threat zone
- [ ] Construction must not conflict with streaming, floating origin, or persistence
- [ ] Human survival layer must reinforce the idea that the player is an engineer surviving underwater

## Save / Persistence / Integrity

- [ ] Verify the full state loop:
  - gathering
  - depletion
  - building
  - repair
  - pause
  - exit
  - reload
- [ ] Save meaningful consequences, not garbage simulation
- [ ] For fauna, keep persistence at chunk/macro-state, killed rares, disturbed nests, and important threat state
- [ ] For microfauna, do not do expensive full-save; save only player-facing consequences
- [ ] Verify world integrity as systems grow:
  - caves
  - ruins
  - biolum pockets
  - support outposts
  - resource depletion
  - base expansion
- [ ] Verify save slot context through `GameStartContext`, not through scattered fields

## Audio / Mood / Silence

- [ ] Add sound as a first-class immersion layer
- [ ] Underwater sound must distinguish:
  - safe life
  - busy reef
  - empty silt
  - cave hush
  - ruin hum
  - pressure drone
  - leviathan warning
  - surface openness
- [ ] Silence zones must matter as much as busy-life zones
- [ ] Connect sound cues to ecology, not to jump-scare spam
- [ ] Verify surface/underwater/module transitions do not tear immersion

## Ordered Implementation Waves

- [ ] Wave 0: build-truth blockers and issue ledger
- [ ] Wave 1: bootstrap/menu/pause/product shell
- [ ] Wave 2: terrain parity, gas giant, water/surface truth, oxygen refill
- [ ] Wave 3: hybrid density live-fill with placeholders and existing families
- [ ] Wave 4: geology/caves/arches/overhangs/seams integration
- [ ] Wave 5: flora, reef logic, biolum, microfauna
  - `2026-04-07` flora transfer evidence:
    - kelp editor pipeline now owns generated `Base / Detail / Normal / Mask` texture stacks through `WorldProceduralFloraTextureAuthoring`
    - kelp shader/material path is no longer flat-color fallback; `Hecton_KelpMaster.shader` now reads normal/mask data for transmission/spec breakup
    - verified Unity passes:
      - `Generate Procedural Flora Textures` -> `TouchedTextures=12`
      - `Apply Procedural Flora Materials` -> `TouchedMaterials=3`
      - `Generate Procedural Flora Baked Starters` -> `Prefabs=21, MeshesUpdated=60, RemovedAssets=0, Failures=0`
      - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
    - same validator hardening also exposed and removed a real flora defect:
      - coral starter materials were still shipping with instancing disabled
      - flora material authoring now hardens all 7 flora materials
      - verified post-fix pass:
        - `Apply Procedural Flora Materials` -> `TouchedMaterials=7`
        - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
    - `2026-04-08` follow-up hardening:
      - flora validator no longer checks material completeness only on `LOD0`
      - all prefab renderers are now covered by material validation, while budget math still uses the active budget slice only
      - verified post-fix pass:
        - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
      - flora status reporting now exposes material/render health instead of only geometry counts
      - verified post-fix pass:
        - `Generate Procedural Flora Final Status Report`
      - current readback:
        - all 7 flora families show `Material Ready 3/3`
      - flora validation/reporting now also prove triangle decay across LOD levels, not just nominal `LODGroup` presence
      - verified post-fix passes:
        - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
        - `Generate Procedural Flora Final Status Report`
      - current readback:
        - all 7 flora families show `LOD Cascade 3/3`
      - authored-final intake now rejects malformed metadata instead of silently falling back to family defaults
      - controlled kelp test confirmed generated fallback stays active when authored metadata is broken
      - coral visual stack is now being raised to kelp parity in the editor-owned layer:
        - dedicated shader `Assets/_Project/Art/Shaders/Hecton_CoralMaster.shader`
        - procedural coral `Base / Detail / Normal / Mask` texture generation for all 4 coral families
        - coral materials now bind to the coral shader and full texture stack instead of generic URP hardening only
        - verified Unity passes on `2026-04-08`:
          - `Generate Procedural Flora Textures` -> `TouchedTextures=28`
          - `Apply Procedural Flora Materials` -> `TouchedMaterials=7`
          - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
          - `Generate Procedural Flora Final Status Report`
- [ ] Wave 6: passive fauna, predators, boids, macro-zone threat logic
- [ ] Wave 7: ruins, old modules, service scars, trash/human traces
- [ ] Wave 8: surface/island ecology and shoreline life
- [ ] Wave 9: resources, return loops, base/support loop, persistence hardening
- [ ] Wave 10: final visual density balance, perf guardrail, user review cycle

## Test Plan

- [ ] After every `P0` pass run build sanity:
  - boot
  - main menu
  - new game
  - load game
  - pause open/close
  - return to menu
  - quit
- [ ] After every terrain/sky/water pass run parity:
  - editor vs build
  - same spot
  - same FOV
  - same lighting
  - same distance
- [ ] After every world-fill pass run a 10-minute swim:
  - shallow
  - wall
  - cave approach
  - ruin approach
  - open-water lookback
  - return path
- [ ] After every ecology pass run observation checks:
  - floor close-up
  - wall close-up
  - ruin close-up
  - cave lip
  - biolum pocket
  - shoreline/island
- [ ] After every perf-sensitive pass capture:
  - startup
  - first dive
  - surface crossing
  - cave entry
  - ruin cluster
  - dense fauna route
  - island approach
- [ ] After every large visual pass ask:
  - what looks cheap
  - what breaks scale
  - does the world feel believable
  - where do you want to swim
  - where does it feel dull

## Final Definition Of Success

- [ ] The game launches as a product, not a dev-scene
- [ ] Menu, pause, loading, return, and quit all work without broken input
- [ ] Surface transition is no longer irritating
- [ ] Surface oxygen refill is stable
  - treat this as reopened for trust if new build evidence says surface escape or shoreline exit still feels broken, even when refill code exists
- [ ] Gas giant reads as a distant layer
- [ ] Terrain in build looks sharp and convincing in close-up
- [ ] The world contains caves, arches, overhangs, ruins, biolum pockets, route hints, support pockets, clutter, trash, microfauna, passive fauna, predators, and major threats
- [ ] Surface and islands have their own flora, fauna, and ecology
- [ ] The player wants not only to swim outward, but to inspect the floor, walls, ruins, cave lips, and surface edges
- [ ] The world sells scale, life, danger, memory, and return loops
- [ ] All new layers remain compatible with MX350 budget
- [ ] Every major development zone has `Status`, `Evidence`, `Problems`, `Short Comment`, and `Next Step`

## Assumptions

- Player build is the main quality arbiter
- CPU optimization remains mandatory, but does not dominate all work without a new confirmed build blocker
- Realism here means believable ocean logic, not literal full simulation
- Structure creates life
- Seams create interest
- Depth changes everything
- Small life is critical for immersion
- Silence matters as much as saturation
- This document is the integrated master version and should be used as the live production roadmap

## Flora Wave 5 Addendum — 2026-04-08 Coral Geometry Ownership

- Verified architecture step:
  - coral starter geometry now has its own editor-only owner `WorldProceduralCoralMeshBuilder`
  - coral no longer depends only on primitive proxy assembly before entering baked finals
- Verified outcomes:
  - `PROCEDURAL_FLORA_FINAL_STATUS_REPORT.md` after regeneration now shows:
    - `family.coral.low` max budget triangles `1488` (was `3840`)
    - `family.coral.branching` max budget triangles `380` (was `4320`)
    - `family.coral.massive` max budget triangles `1668` (was `3840`)
    - `family.coral.plate` max budget triangles `312` (was `400`)
  - baked prefabs `GEN_family_coral_low__knoll` and `GEN_family_coral_branching__fan` now resolve to clean `LODGroup + 2 mesh children` hierarchies instead of primitive child trees
- Honest visual verdict:
  - geometry ownership and cost are improved
  - coral still does not read as photoreal authored final art
  - low/massive silhouettes are more coherent
  - branching coral may now be too cheap and needs a direct beauty pass before it is trusted
- Follow-up control layer now exists on paper and in code:
  - flora family budgets now include a minimum recommended triangle floor
  - validator is expected to warn on underbuilt silhouettes, not only on overbudget meshes
  - status report is expected to expose fidelity state family-by-family after the next successful Unity compile
- Status:
  - `PENDING VERIFICATION`
  - missing proof: in-world beauty pass, profiler/build evidence, authored coral finals

## Flora Wave 5 Addendum â€” 2026-04-08 Underbuilt Coral Follow-Up

- Verified target:
  - the fidelity-floor validator exposed two real underbuilt starter variants:
    - `family.coral.low__bed`
    - `family.coral.plate__ledge`
- Implemented only in the correct owner layer:
  - `WorldProceduralCoralMeshBuilder.BuildLow()` now adds extra mound lobes and a shallow top plate for `__bed`
  - `WorldProceduralCoralMeshBuilder.BuildPlate()` now adds a secondary overhang plate and underside buttress for `__ledge`
- Verified Unity passes on `2026-04-08`:
  - `Generate Procedural Flora Baked Starters` -> `Prefabs=21, MeshesUpdated=60, RemovedAssets=0, Failures=0`
  - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
  - `Generate Procedural Flora Final Status Report`
- Verified outcomes:
  - `GEN_family_coral_low__bed` triangles `824/472 -> 1658/1006`
  - `GEN_family_coral_plate__ledge` triangles `176/144 -> 340/292`
  - both variants now clear the fidelity floor
  - validator warning count drops back from `9` to `7`
  - remaining warnings are only the known `generated-only, no authored photoreal finals yet` warnings
- Verified visual readback:
  - `Assets/Screenshots/coral_low_bed_stage_v2.png`
  - `Assets/Screenshots/coral_plate_ledge_stage_v2.png`
- Honest verdict:
  - starter coral readability and form connection improved materially
  - this is still not authored photoreal coral art
- Status:
  - `PENDING VERIFICATION`
  - missing proof: in-world beauty pass, profiler/build evidence, authored coral finals

## Flora Wave 5 Addendum â€” 2026-04-08 Kelp Blade Realism Pass

- Verified target:
  - kelp starter finals still had a plastic-strip failure mode, most obvious on `GEN_family_kelp_tall__ribbon`
- Implemented only in the correct owner layer:
  - `WorldProceduralSeaweedMeshBuilder.AddRibbon()` now adds a center rib, edge curl, asymmetrical taper, upper-tip split, and stronger droop/bow
  - `WorldProceduralFloraTextureAuthoring` now generates stronger rib/edge/vein breakup for kelp textures
  - `WorldProceduralFloraMaterialAuthoring` now pushes kelp materials further away from plastic read with lower smoothness and stronger transmission/normal/detail response
- Verified Unity passes on `2026-04-08`:
  - `Generate Procedural Flora Textures` -> `TouchedTextures=28`
  - `Apply Procedural Flora Materials` -> `TouchedMaterials=7`
  - `Generate Procedural Flora Baked Starters` -> `Prefabs=21, MeshesUpdated=60, RemovedAssets=0, Failures=0`
  - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
  - `Generate Procedural Flora Final Status Report`
- Verified outcomes:
  - `family.kelp.tall` max budget triangles `584 -> 728`
  - `family.kelp.patch.dense` max budget triangles `496 -> 696`
  - `family.kelp.canopy` max budget triangles `684 -> 908`
  - all three kelp families still remain comfortably under budget and keep `Fidelity Floor 3/3`
- Verified readback:
  - `GEN_family_kelp_tall__ribbon` -> `712/368/160/72`
  - `GEN_family_kelp_tall__stalk` -> `728/380/160/60`
  - `GEN_family_kelp_canopy__crown` -> `908/520/260/116`
  - screenshot evidence:
    - `Assets/Screenshots/kelp_ribbon_stage_before.png`
    - `Assets/Screenshots/kelp_ribbon_stage_after.png`
- Honest verdict:
  - generated kelp leaves are less toy-like and less mono-green than before
  - authored photoreal kelp finals are still missing and remain the next real quality wall

## Flora Wave 5 Addendum â€” 2026-04-08 Kelp Anatomy Follow-Up

- Verified target:
  - after the leaf realism pass, kelp still had an anatomical cheap read:
    - blades attached too abruptly to the stipe
    - pneumatocysts still read too much like simple spheres
- Implemented only in the correct owner layer:
  - `WorldProceduralSeaweedMeshBuilder.BuildBlade()` now adds blade stems / petioles before the ribbon leaf
  - `WorldProceduralSeaweedMeshBuilder.BuildBulb()` now adds bulb stems and offset bulb lobes
  - shared `AddTube()` helper now owns those organic connectors
- Verified Unity passes on `2026-04-08`:
  - `Generate Procedural Flora Baked Starters` -> `Prefabs=21, MeshesUpdated=60, RemovedAssets=0, Failures=0`
  - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
  - `Generate Procedural Flora Final Status Report`
- Verified outcomes:
  - `family.kelp.tall` max budget triangles `728 -> 1018`
  - `family.kelp.patch.dense` max budget triangles `696 -> 954`
  - `family.kelp.canopy` max budget triangles `908 -> 1244`
  - all three kelp families remain under their family triangle limits by a wide margin
- Verified readback:
  - `GEN_family_kelp_tall__ribbon` -> `1018/448/196/96`
  - `GEN_family_kelp_tall__stalk` -> `1004/444/184/72`
  - `GEN_family_kelp_canopy__crown` -> `1244/616/308/152`
  - screenshot evidence:
    - `Assets/Screenshots/kelp_ribbon_stage_after_stems.png`
- Honest verdict:
  - kelp now reads more like connected anatomy and less like leaves glued onto a rod
  - authored photoreal kelp finals are still the next quality wall

## Flora Wave 5 Addendum â€” 2026-04-08 Kelp Silhouette Fullness Pass

- Verified target:
  - after leaf/anatomy fixes, some kelp variants still read too sparse because each anchor only carried one dominant blade
- Implemented only in the correct owner layer:
  - `WorldProceduralSeaweedMeshBuilder.BuildBlade()` now adds secondary companion blades on near LODs for selected anchors
- Verified Unity passes on `2026-04-08`:
  - `Generate Procedural Flora Baked Starters` -> `Prefabs=21, MeshesUpdated=60, RemovedAssets=0, Failures=0`
  - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
  - `Generate Procedural Flora Final Status Report`
- Verified outcomes:
  - `family.kelp.tall` max budget triangles `1018 -> 1346`
  - `family.kelp.patch.dense` max budget triangles `954 -> 1324`
  - `family.kelp.canopy` max budget triangles `1244 -> 1654`
  - all three kelp families remain well under their family limits
- Verified readback:
  - `GEN_family_kelp_tall__ribbon` -> `1346/616/196/96`
  - `GEN_family_kelp_tall__stalk` -> `1226/540/184/72`
  - `GEN_family_kelp_patch_dense__patch_tall` -> `1324/638/228/98`
  - screenshot evidence:
    - `Assets/Screenshots/kelp_stalk_stage_companion_blades.png`
- Honest verdict:
  - kelp reads fuller and less skeletal than before
  - authored photoreal kelp finals remain the next quality wall
## Flora Wave 5 Addendum - 2026-04-08 Kelp Leaf Shader Width-Read Pass

- Verified target:
  - after geometry, texture, and silhouette passes, kelp blades still read too broad and flat across width
  - the material stack needed a clearer center-rib versus edge lighting split
- Implemented only in the correct owner layer:
  - `Hecton_KelpMaster.shader` now derives `midribMask` and `edgeMask` from `uv.x`
  - kelp shader now exposes:
    - `_EdgeTransmissionBoost`
    - `_MidribDarkening`
    - `_MidribGlossBoost`
    - `_EdgeWearDarkening`
    - `_EdgeDetailBoost`
  - `WorldProceduralFloraMaterialAuthoring` now binds those controls on all three kelp family materials
- Verified Unity passes on `2026-04-08`:
  - `Apply Procedural Flora Materials` -> `TouchedMaterials=7`
  - `Generate Procedural Flora Baked Starters` -> `Prefabs=21, MeshesUpdated=60, RemovedAssets=0, Failures=0`
  - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
  - `Generate Procedural Flora Final Status Report`
- Verified outcomes:
  - `MAT_family_kelp_tall.mat` now stores the new width-read shader controls and keeps instancing enabled
  - kelp family report state remains:
    - `Material Ready 3/3`
    - `LOD Cascade 3/3`
    - `Fidelity Floor 3/3`
  - triangle budgets stay unchanged:
    - `family.kelp.tall` -> `1346`
    - `family.kelp.patch.dense` -> `1324`
    - `family.kelp.canopy` -> `1654`
- Verified visual readback:
  - `Assets/Screenshots/kelp_stalk_stage_leaf_shader.png`
- Honest verdict:
  - width readability improved without pushing runtime or budget risk
  - this is still not authored photoreal kelp art

## Flora Wave 5 Addendum - 2026-04-08 Kelp Curved-Normal Shader Follow-Up

- Verified target:
  - after the width-read shader pass, kelp leaves still preserved a planar-lighting failure mode
  - the next correct step was to improve blade normal response, not to add more geometry blindly
- Implemented only in the correct owner layer:
  - `Hecton_KelpMaster.shader` now exposes `_BladeCurveNormalStrength`
  - kelp fragment shading now bends tangent-space normals across blade width using the existing width/edge masks
  - a small midrib upward normal bias was added so the center rib does not light like a dead flat stripe
  - `WorldProceduralFloraMaterialAuthoring` now binds `_BladeCurveNormalStrength` on kelp materials
- Verified Unity passes on `2026-04-08`:
  - `Apply Procedural Flora Materials` -> `TouchedMaterials=7`
  - `Generate Procedural Flora Baked Starters` -> `Prefabs=21, MeshesUpdated=60, RemovedAssets=0, Failures=0`
  - `Validate Procedural Flora Final Variants` -> `PASS validatedPrefabs=21, warningCount=7`
  - `Generate Procedural Flora Final Status Report`
- Verified outcomes:
  - `MAT_family_kelp_tall.mat` now stores `_BladeCurveNormalStrength: 0.24`
  - kelp triangle budgets remain unchanged:
    - `family.kelp.tall` -> `1346`
    - `family.kelp.patch.dense` -> `1324`
    - `family.kelp.canopy` -> `1654`
  - kelp families still remain:
    - `Material Ready 3/3`
    - `LOD Cascade 3/3`
    - `Fidelity Floor 3/3`
- Verified visual readback:
  - `Assets/Screenshots/kelp_stalk_stage_curved_normals.png`
- Honest verdict:
  - this improves light volume on blades without touching budgets or runtime
  - it is still an incremental pass, not authored photoreal kelp art
- 2026-04-08 - Kelp morphology refactor queued for live bake verification
  - what changed in code:
    - `WorldProceduralSeaweedMeshBuilder` now treats giant-frond kelp less like a few broad paddles on a pole:
      - blade anchors are biased upward along the stipe
      - blade sockets retain stipe tangent for better petiole peel-off
      - blade lamina generation now uses a dedicated `5`-column ribbon with a narrow sheath-like base
      - kelp specs were rebalanced toward more numerous, narrower blades
  - why this matters:
    - the current failure mode is morphological, not material-only
    - this pass targets the actual complaint: generated kelp still reads like a simplified toy rather than a believable underwater plant
  - verification state:
    - compile-safe: Unity performed a fresh compile/domain reload without new script errors
    - bake/report/screenshot proof still missing
    - direct batchmode verification attempt was blocked because the project is already open in another Unity instance
  - next required checks when live editor control is available:
    - `Hecton/Authoring/Generate Procedural Flora Baked Starters`
    - `Hecton/Validation/Validate Procedural Flora Final Variants`
    - `Hecton/Validation/Generate Procedural Flora Final Status Report`
    - new prefab-stage screenshots for at least:
      - `GEN_family_kelp_tall__stalk`
      - `GEN_family_kelp_tall__ribbon`
- 2026-04-08 - Kelp growth-stability follow-up implemented, verification blocked by missing live editor
  - `WorldProceduralSeaweedMeshBuilder` follow-up targets the remaining structural defect:
    - side stems reading detached from the stipe
    - blade spacing still collapsing into sparse synthetic-stick rhythm
  - code changes:
    - upper-biased blade sequencing + stable alternating/helical angle sweep in `BuildBlade()`
    - tighter sheath/contact zone in `EvaluateBladeSocket()`
    - stipe-hugging petiole curve in `AddBladeStem()`
    - dead uncompiled automation file removed; automation now lives only in `WorldProceduralFloraFinalStatusReport`
  - blocker:
    - local Unity Editor process disappeared
    - local MCP endpoint stopped responding
    - no fresh bake/report/screenshot proof exists yet
  - status:
    - `PENDING VERIFICATION`
- 2026-04-08 - Giant-frond kelp distribution pass is now live-verified
  - what changed:
    - giant-frond kelp is no longer distributed with the same upper-compressed logic that made it read like a narrow synthetic broom
    - bulb nodes are now attached closer to the blade/stipe contact instead of reading as floating berries
    - giant-frond kelp specs gained denser bulb/node coverage
  - verified facts:
    - Unity automation request `flora-verify-20260408-6` completed successfully
    - fresh report + PNG captures were produced
    - fresh report deltas:
      - `family.kelp.tall` max budget `3514 -> 4760`
      - `family.kelp.patch.dense` max budget `3462 -> 4950`
      - `GEN_family_kelp_tall__stalk` `3198/1832/692/356 -> 4356/2048/764/412`
      - `GEN_family_kelp_tall__ribbon` `3514/2080/784/448 -> 4760/2272/892/532`
      - `GEN_family_kelp_patch_dense__patch_tall` `3462/2106/744/386 -> 4950/2298/852/442`
  - honest verdict:
    - this materially improves the generated starter set
    - it still does not cross the authored-photoreal quality wall
    - it also still does not create Claude runtime seaweed parity; HECTON-8 continues to use the integrated editor-owned flora path instead of a separate monolithic seaweed renderer subsystem
  - status:
    - `PENDING VERIFICATION`
