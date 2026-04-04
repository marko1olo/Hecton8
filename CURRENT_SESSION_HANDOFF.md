# CURRENT SESSION HANDOFF

Status: active runtime/gameplay handoff for the next dialog.  
Date: 2026-04-02

## Read First

This file keeps only the parts that are still operationally relevant.

Use it to understand:
- what is actually verified in Unity right now
- which recent fixes changed the current baseline
- what is still honestly open
- what the next best tasks are

Companion files for the next dialog:

- `RUNTIME_SYMPTOMS_AND_DIAGNOSTICS_2026-04-02.md`
- `NEW_DIALOG_START_PROMPT_2026-04-02.md`

---

## Communication Rules

- Answer in simple Russian.
- Explain directly, without empty terminology.
- Prefer the structure:
  - `What was done`
  - `What this means in simple terms`
  - `What this gives`
  - `What was verified`
  - `What remains open`
- Do not claim Unity verification unless it really happened.
- Work proactively, but do not hide risk.

---

## Current Verified State

- Unity currently compiles without `Error`.
- Editor reaches idle state after the latest passes.
- The old scan/runtime compile blockers are closed.
- The old input teardown spam is closed.
- The old persistent native leak on clean play start is closed.
- The `GasGiantRotationDriver` null `MaterialPropertyBlock` crash is closed.

### Recently fixed and already part of the baseline

- `ProximityColliderSystem` / `HectonRockManager`
  - safe runtime cleanup and reinit path
- `FlashlightTool`
  - dead `hit` references removed
- `ScanEvents` / `ScannerTool` / scan consumers
  - explicit `Unity.Mathematics.float3` contract restored
- `FlowFieldVisualizer`
  - job/global-current/local-volume path hardened
  - preview cleanup improved
  - editor tests were restored
- `ZeroGCStringCache`
  - now behaves like a real cache for repeated uppercase labels
- `HectonFluidEngine`
  - persistent native arrays now release safely on disable
- `InputManager`
  - stale `InputActionMap` teardown is now guarded
- `GasGiantRotationDriver`
  - renderer/property-block resources are re-established safely before use
- `HectonDevToolsMenu`
  - play-mode smoke menu no longer forces selection/ping on runtime helper objects
- `PDABarterTab` / `PDAConstructionTab` / `PDAShellChrome`
  - native `Update()` polling replaced with dynamic `ITickable` registration
  - PDA UI tabs now tick only while actually active/open
- `BuilderStatusOverlay`
  - native `Update()` replaced with dynamic `ITickable` registration
  - overlay now stays out of tick path while Builder Tool is inactive
- `SuitHUDV4CanvasOverlay` / `SuitHUDScreenCompositor`
  - play-mode refresh moved onto `ITickable`
  - editor preview paths preserved through editor-only `Update()`
- `VisorHUDController`
  - runtime HUD refresh moved off native `Update()` onto `ITickable`
  - edit preview preserved
  - runtime RT release path is now safe in both play and edit mode
- `SuitHUDPresentationController`
  - play-mode orchestration no longer lives in native `LateUpdate()`
  - dynamic `ITickable` registration added
  - overlay/canvas lookups are now more selective and cached where useful
- `HectonSuitHUDExtensions`
  - runtime `LateUpdate()` replaced with `ITickable`
  - flashlight overheat/flicker reset coroutines replaced with timer state
  - compile plus clean play-stop smoke confirmed
- legacy `HectonSuitHUD`
  - retired from the active player HUD stack
  - removed from first-party prefabs and code paths
- `HudNumericStringCache`
  - inherited the old prebuilt integer string cache used by scan markers
- HUD stack truth
  - current active path is `SuitHUDV4CanvasOverlay` + `SuitHUDPresentationController` + `VisorHUDController`
  - compile plus clean play-stop smoke confirmed after legacy HUD retirement
- `SuitHUDScreenCompositor`
  - no longer keeps ticking every frame after its setup is already stable
  - wakes back up only when a real refresh is needed
  - live compile plus clean play-stop smoke confirmed
- `WorldGenerativeGeologyBinding` / `WorldGenerativeGeologyIntegrationDirector`
  - active bindings now use a registry instead of repeated whole-scene scans
  - `includeInactiveBindings` now actually controls whether the slow full scan path is used
  - geology smoke lookup also uses the fast registry path first
  - compile plus clean play-stop smoke confirmed
- bootstrap/world runtime reference path
  - `SceneBootstrap` now publishes a fast current-player reference for runtime systems
  - duplicated player auto-resolve across world directors now uses a shared bootstrap-aware helper
  - several world directors now throttle auto-resolve instead of retrying too aggressively during startup
  - compile/import plus clean play-stop smoke confirmed
- `WorldContentSocket`
  - now caches its parent `WorldZoneAnchor` instead of re-running `GetComponentInParent` through multiple world directors
  - `WorldContentDirector`, `WorldPopulationDirector`, and `WorldProceduralFillDirector` now reuse that cached zone link
  - clean play-stop smoke confirmed
- world zone/content hot paths
  - nearest socket / zone selection now uses squared-distance comparisons where exact distance was unnecessary
  - `WorldZoneDirector` now evaluates zone activation/hold state through a single shared anchor pass instead of repeating the same work several times
  - console stayed free of new first-party errors after the pass
- `WorldProceduralFieldSampler`
  - zone resolution now reuses the shared `WorldZoneAnchor` evaluation path instead of duplicating distance and activation work
  - console stayed free of new first-party errors after the pass
- dev runtime profiler hook
  - disabled `__DEV_RuntimePerformanceProfiler` object added to the active world scene under `--- SYSTEMS ---`
  - scene saved cleanly and console stayed empty after the addition
- `WorldProceduralScatterDirector`
  - false bootstrap waiting is removed; scatter now defers only behind a real active `SceneBootstrap`
  - scatter instance reuse now goes through `ObjectPoolManager` without `Pool exhausted` warning spam
  - repeated pattern/context budget lookups inside the sampling loop were reduced
  - long play profiling showed startup scatter improving from roughly `407ms / sample 224ms` to roughly `311ms / sample 108ms`
  - incremental movement rebuilds stayed around `70-122ms` instead of startup-scale spikes

### Documents that already describe these passes

- [COMPILE_HARDENING_2026-04-02.md](C:/hades/Hecton8/COMPILE_HARDENING_2026-04-02.md)
- [WORLD_RUNTIME_HARDENING_2026-04-02.md](C:/hades/Hecton8/WORLD_RUNTIME_HARDENING_2026-04-02.md)
- [FLOW_FIELD_VISUALIZER_HARDENING_2026-04-02.md](C:/hades/Hecton8/FLOW_FIELD_VISUALIZER_HARDENING_2026-04-02.md)
- [RUNTIME_SMOKE_HARDENING_2026-04-02.md](C:/hades/Hecton8/RUNTIME_SMOKE_HARDENING_2026-04-02.md)
- [INPUT_RUNTIME_HARDENING_2026-04-02.md](C:/hades/Hecton8/INPUT_RUNTIME_HARDENING_2026-04-02.md)
- [PDA_UI_TICK_HARDENING_2026-04-02.md](C:/hades/Hecton8/PDA_UI_TICK_HARDENING_2026-04-02.md)
- [BUILDER_OVERLAY_TICK_HARDENING_2026-04-02.md](C:/hades/Hecton8/BUILDER_OVERLAY_TICK_HARDENING_2026-04-02.md)
- [HUD_RUNTIME_TICK_HARDENING_2026-04-02.md](C:/hades/Hecton8/HUD_RUNTIME_TICK_HARDENING_2026-04-02.md)
- [VISOR_HUD_CONTROLLER_HARDENING_2026-04-02.md](C:/hades/Hecton8/VISOR_HUD_CONTROLLER_HARDENING_2026-04-02.md)
- [SUIT_HUD_PRESENTATION_HARDENING_2026-04-02.md](C:/hades/Hecton8/SUIT_HUD_PRESENTATION_HARDENING_2026-04-02.md)
- [SUIT_HUD_EXTENSIONS_RUNTIME_HARDENING_2026-04-02.md](C:/hades/Hecton8/SUIT_HUD_EXTENSIONS_RUNTIME_HARDENING_2026-04-02.md)
- [HECTON_SUIT_HUD_GLITCH_ZERO_GC_2026-04-02.md](C:/hades/Hecton8/HECTON_SUIT_HUD_GLITCH_ZERO_GC_2026-04-02.md)
- [SUIT_HUD_SCREEN_COMPOSITOR_HARDENING_2026-04-02.md](C:/hades/Hecton8/SUIT_HUD_SCREEN_COMPOSITOR_HARDENING_2026-04-02.md)
- [WORLD_GEOLOGY_BINDING_REGISTRY_HARDENING_2026-04-02.md](C:/hades/Hecton8/WORLD_GEOLOGY_BINDING_REGISTRY_HARDENING_2026-04-02.md)
- [WORLD_RUNTIME_REFERENCE_HARDENING_2026-04-02.md](C:/hades/Hecton8/WORLD_RUNTIME_REFERENCE_HARDENING_2026-04-02.md)
- [WORLD_CONTENT_SOCKET_ZONE_CACHE_HARDENING_2026-04-02.md](C:/hades/Hecton8/WORLD_CONTENT_SOCKET_ZONE_CACHE_HARDENING_2026-04-02.md)
- [WORLD_ZONE_HOTPATH_HARDENING_2026-04-02.md](C:/hades/Hecton8/WORLD_ZONE_HOTPATH_HARDENING_2026-04-02.md)
- [DEV_RUNTIME_PROFILER_SCENE_HOOK_2026-04-02.md](C:/hades/Hecton8/DEV_RUNTIME_PROFILER_SCENE_HOOK_2026-04-02.md)
- [WORLD_SCATTER_RUNTIME_HARDENING_2026-04-02.md](C:/hades/Hecton8/WORLD_SCATTER_RUNTIME_HARDENING_2026-04-02.md)

---

## What Is Still Not Closed

These items should not be called “done” yet:

- `ToolRuntimeSmokeTester` is not yet a deterministic PASS path through MCP-driven play sessions.
- `WorldGenerativeGeologyRuntimeSmokeTester` is not yet a deterministic PASS path through MCP-driven play sessions.
- `FieldToolRuntimeSmokeTester` still has an automation tail around salvage/equip flow.
- `ScanRuntimeSmokeTester` still wants a deterministic smoke pass, not just compile/runtime correctness.
- Real authored play verification is still missing for several improved tools:
  - flashlight guidance
  - scanner mode usefulness
  - analyzer readouts
  - repair/cutter service-lane feedback
  - weaker non-core tools as a whole
- Big-world streaming/content integration is still not complete for all layers:
  - flora
  - debris
  - broader construction/service roots
  - hybrid near/mid/far density slices
- The first startup scatter burst is still too heavy even after the latest pass.
- Scene/prefab `missing script` warnings are now visible during longer sessions and should be audited separately from scatter profiling.

---

## Important Clarification About Smoke

The current high-signal remaining issue is no longer input teardown or compile hygiene.

The real tail is this:
- dev smoke menu items do start
- but MCP observation of play-mode smoke remains unreliable and often sits in `playmode_transition`

This should currently be treated as:
- partly a tooling/observation problem
- and partly an unfinished deterministic smoke-trigger path

It should not automatically be interpreted as proof that the gameplay systems themselves are broken.

Additional current limitation:
- the recent PDA / builder / HUD / visor presentation passes are now live-verified through compile plus clean play-stop smoke
- MCP observation during active play can still briefly lose ping responsiveness
- so mid-play MCP telemetry is still less trustworthy than post-play idle verification

---

## Best Next Tasks

The best next sequence is:

1. Stabilize dev smoke triggering and observation
   - `ToolRuntimeSmokeTester`
   - `WorldGenerativeGeologyRuntimeSmokeTester`
   - `FieldToolRuntimeSmokeTester`
   - `ScanRuntimeSmokeTester`

2. Run authored real-play verification for the upgraded tool stack
   - flashlight
   - scanner
   - analyzer
   - repair
   - cutter
   - non-core tools

3. Continue the large-world runtime track
   - enable and use the new scene profiler hook to collect actual numbers
   - continue reducing the first `WorldProceduralScatterDirector` startup burst
   - audit scene/prefab `missing script` warnings so runtime profiling stays clean
   - hybrid-density world slices
   - broader streamed roots
   - flora/debris/construction propagation onto the shared world profile

---

## What To Read Next

Start with:
- [Live Fix Plan](C:/hades/Hecton8/Что_и_как_исправляем_—_живой_план.md)
- [NEXT_SPRINT_TASKS.md](C:/hades/Hecton8/NEXT_SPRINT_TASKS.md)
- [TOOLS_ENTERPRISE_SPRINT.md](C:/hades/Hecton8/TOOLS_ENTERPRISE_SPRINT.md)

Then read as needed:
- [WORLD_CHUNK_STREAMING_ENTERPRISE_PLAN.md](C:/hades/Hecton8/WORLD_CHUNK_STREAMING_ENTERPRISE_PLAN.md)
- [AI_FAUNA_CHUNK_STREAMING_PLAN.md](C:/hades/Hecton8/AI_FAUNA_CHUNK_STREAMING_PLAN.md)
- [PROCEDURAL_WORLD_FILL_ENTERPRISE_PLAN.md](C:/hades/Hecton8/PROCEDURAL_WORLD_FILL_ENTERPRISE_PLAN.md)

---

## 2026-04-03 scatter movement checkpoint

### Fresh validated facts

- A new heavy-world run was inspected through Unity MCP console + profiler counters.
- The top repeated runtime offender remained `WorldProceduralScatterDirector`.
- Fresh observed slow-tick spikes included:
  - `221.25ms`
  - `160.24ms`
  - `128.09ms`
  - `108.52ms`
  - `102.56ms`
  - `93.28ms`
  - `85.32ms`
  - `76.56ms`
- This means the earlier pending-reconcile pass was not enough on its own.

### Current scene/runtime state seen during inspection

- `WorldProceduralScatterDirector` reported:
  - `_debugEvaluatedCells = 225`
  - `_debugDesiredPlacements = 82`
  - `_debugActivePlacements = 82`
  - `_debugFallbackSamples = 225`
- `WorldProceduralFieldSampler` reported fallback-driven sampling.
- `MapMagicBridge.IsAvailable = false` in the inspected scene state.
- Important consequence:
  - even without a healthy terrain provider path, scatter rebuilds were still expensive
  - the current runtime problem cannot be blamed only on Crest/terrain/water

### Memory snapshot caveat

- The latest snapshot was taken in Editor after the run, not from a clean player build.
- Still, the numbers are too large to ignore:
  - `Total Used Memory = 8.56 GB`
  - `Texture Memory = 1.92 GB`
  - `Gfx Used Memory = 2.59 GB`
  - `GC Reserved Memory = 3.14 GB`
  - `GC Used Memory = 1.56 GB`
  - `Profiler Used Memory = 1.87 GB`
- Treat these as a red flag for the MX350 target, not as final build truth.

### Changes applied in this package

1. Existing same-cell pending reconcile continuation remains active in
   `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`.

2. Scatter center-cell math was corrected in
   `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`:
   - previous code used `Mathf.RoundToInt(...)`
   - current code uses floor-based cell partition via `WorldToScatterCellIndex(...)`
   - reason: `RoundToInt` moved the player into the next scatter cell after roughly half a cell, which made rebuilds fire too early

3. Spike-only phase logging was added to
   `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`:
   - the next heavy run should print scatter phase timings even with `enableScatterDetailedDiagnostics = false`
   - target output is the built-in `[WorldScatterProfiler]` breakdown:
     - `sample`
     - `wait`
     - `post`
     - `rescue`
     - `restore`
     - `reconcile`
     - `cleanup`
     - `spawn`
     - `fauna`

4. A second first-party runtime pass was added in
   `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs`:
   - the seafloor height cache no longer clears at the start/end of every scatter sampling frame
   - this cache now survives across neighboring rebuilds
   - cache invalidation still happens when burst data is marked dirty
   - a hard cap now clears the cache before unbounded growth
   - reason:
     - adjacent scatter rebuilds reuse most of the same probe points
     - previous code threw away that reuse opportunity every time
     - this likely inflated repeated terrain/fallback sampling cost during movement

5. The next validated heavy run finally exposed the real remaining phase split:
   - movement rebuilds were roughly:
     - `rebuild = 59-73ms`
     - `sample = 54-68ms`
     - `wait = ~0.20-0.32ms`
     - `post = 52-65ms`
     - `reconcile = 3-5ms`
   - startup dirty rebuild was still much heavier:
     - `rebuild = 160.97ms`
     - `sample = 124.48ms`
     - `input = 25.24ms`
     - `post = 91.00ms`
     - `reconcile = 24.59ms`
   - meaning:
     - movement bottleneck is now confirmed as post-sampling work
     - not job wait
     - not reconcile/spawn

6. Another safe runtime gate was added in
   `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`:
   - `ShouldSkipScatterRefresh()` now has `cell-hysteresis`
   - entering the next scatter cell does not force an immediate rebuild unless the observer has moved at least one full runtime cell from the last refresh sample
   - reason:
     - the new logs still showed rebuild reason dominated by `cell-changed`
     - the system was still paying full 225-cell rebuild cost too eagerly while moving

7. The next post-sampling scoring pass was added in
   `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`:
   - `PrepareRuntimeRuleBuffer(...)` now precomputes `ScoreBaseBonus` once per runtime rule
   - the hot post-sampling loop no longer recomputes placement/scatter-layer bonuses for every candidate
   - candidate accumulation now uses ordered insertion into `_candidateBuffer`
   - reason:
     - the latest validated logs showed movement cost dominated by `post`
     - one obvious avoidable cost inside that phase was repeated bonus recomputation plus a full per-cell candidate sort
   - expected effect:
     - lower CPU cost inside post-sampling
     - no change to gameplay-facing scoring order

8. The next candidate-build reduction pass was added in
   `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`:
   - non-rescue candidates that already fail the random spawn gate now exit before `BuildCandidate(...)`
   - rescue candidates still preserve old behavior and remain eligible for fallback injection flows
   - runtime rule preparation now precomputes:
     - `StreamingLayer`
     - `GeologyProfile`
     - `HasMacroZone`
     - `SupportsFinalVariant`
   - per-cell biome-context label is resolved once and reused when placement data is initialized
   - reason:
     - `BuildCandidate(...)` was still doing repeated family-level resolve work inside the hottest post-sampling loop
     - that loop was also materializing pooled placements for candidates that were already dead on arrival in the normal non-rescue path
   - expected effect:
     - lower post-sampling CPU cost
     - lower pooled placement churn
     - no gameplay semantic change for rescue injection

### How to continue

- Do not waste another rerun on tiny edits.
- The current package now includes:
  - pending reconcile continuation
  - floor-based scatter cell indexing
  - spike-only scatter phase logging
  - persistent cross-rebuild seafloor height cache
  - cell-change hysteresis before full rebuild
  - cached per-rule `ScoreBaseBonus`
  - ordered candidate insertion to avoid full per-cell sort
  - early non-rescue gate reject before `BuildCandidate(...)`
  - cached family-level build data inside runtime rules
  - one-time per-cell biome-context label reuse
- The next checkpoint should be a heavy run after this package, not another blind micro-fix.
- For the next play pass:
  1. run the same heavy swim scenario
  2. capture new `TickProfiler` output
  3. capture new `[WorldScatterProfiler]` phase logs
  4. then decide whether the next strike is:
     - sampling/post-sampling
     - reconcile/spawn
     - or a separate memory/texture budget track

### Honest status

- Nothing in this package is declared fixed.
- This remains `PENDING VERIFICATION` until a new heavy movement run confirms lower scatter spike frequency and lower spike cost.

### Additional startup + memory pass completed

9. A startup-focused scatter cleanup pass was added in
   `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`:
   - `_faunaSnapshotDirty` now gates `PublishFaunaRegistrySnapshot()`
   - continuation ticks no longer republish fauna anchors when `_desiredPlacements` did not change
   - startup pending semantics were tightened so far placements do not keep `_hasPendingStartupPlacements = true` forever
   - observer position is now cached once per reconcile/warmup pass and reused by:
     - `PrepareScatterPoolWarmup(...)`
     - `GetResolvedPlacementVariant(...)`
     - `ShouldCreateDuringInitialWarmup(...)`
     - `ShouldUseFinalVariant(...)`
   - reason:
     - after movement cost dropped, the next verified hotspot moved into startup reconcile/spawn work

10. Scatter metadata setup in `Assets/_Project/Scripts/WorldProceduralProxyInstance.cs`
    no longer uses `Enum.ToString()`:
    - static label resolvers now map scatter layer / seafloor source / geology archetype directly to cached strings
    - reason:
      - remove managed string churn from the reconcile/spawn metadata path

11. Another narrow spawn-path cleanup was added in
    `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`:
    - `CreateScatterInstance(...)` no longer performs a second `pool.GetAvailableCount(prefab)` check after `TryReserveScatterCreate(...)` already reserved the allowance
    - reason:
      - remove redundant pool work from the confirmed startup spawn hotspot

12. A first-party RAM / VRAM importer pass was completed before the next validation run:
    - enabled `streamingMipmaps` on 42 large first-party world textures
    - removed `isReadable` from the large first-party sky textures:
      - `eb2.png`
      - `bo3.png`
      - `oblakajip.png`
    - restored default compression on `eb2.png`
    - reason:
      - let large world textures participate in the project's existing streaming budget
      - remove pointless CPU-readable copies from big sky textures

13. Compile/runtime status after this package:
    - Unity refresh + compile completed
    - no new first-party compile errors were introduced
    - console still showed only:
      - the known editor-side `LifecycleManagement` null-reference error
      - unrelated third-party `UDR0001` warnings

14. Latest validated heavy-run results after this package:
    - `[WorldScatterProfiler] rebuild=77.76ms sample=53.42ms input=15.23ms wait=2.42ms post=35.77ms rescue=10.12ms restore=0.30ms reconcile=13.59ms cleanup=1.93ms spawn=9.19ms fauna=2.47ms diag=0.33ms removed=0 rebuilt=0 created=9 reused=0 cells=225 desired=160 active=9 reason=dirty`
    - `[TickProfiler] SlowTick spike total=96.97ms ... WorldProceduralScatterDirector=89.79ms`
    - follow-up scatter-dominant slow ticks dropped to about:
      - `14.56ms`
      - `0.72ms`
      - `20.34ms`

15. Comparison against the previous validated startup dirty rebuild:
    - previous dirty rebuild:
      - `144.95ms total`
      - `sample=100.81ms`
      - `post=60.44ms`
      - `spawn=20.95ms`
    - latest dirty rebuild:
      - `77.76ms total`
      - `sample=53.42ms`
      - `post=35.77ms`
      - `spawn=9.19ms`
    - meaning:
      - startup scatter cost dropped again in a material way
      - scatter remains the main first-party CPU offender, but the dirty startup burst is no longer in the 145-161ms range seen earlier

16. Latest memory / profiler snapshot tied to this run:
    - `GC Allocated In Frame = 1325 B`
    - `GC Allocation In Frame Count = 20`
    - `Texture Memory = 2010269152 B`
    - `Gfx Used Memory = 2788252503 B`
    - `Profiler Used Memory = 1887579840 B`
    - `Total Used Memory = 8783733599 B`
    - `render_textures = 1101`
    - `render_textures_bytes = 1631522368 B`

### Current reading

- CPU: improved, but not finished.
- GC: low in the validated run, but not zero.
- VRAM / render textures: still far above target for MX350.
- Status remains `PENDING VERIFICATION`.

17. Another validated runtime checkpoint was added after the heavier terrain + water run:
    - `[WorldScatterProfiler] rebuild=77.76ms sample=53.42ms input=15.23ms wait=2.42ms post=35.77ms rescue=10.12ms restore=0.30ms reconcile=13.59ms cleanup=1.93ms spawn=9.19ms fauna=2.47ms diag=0.33ms removed=0 rebuilt=0 created=9 reused=0 cells=225 desired=160 active=9 reason=dirty`
    - `[TickProfiler] SlowTick spike total=96.97ms ... WorldProceduralScatterDirector=89.79ms`
    - later scatter-heavy slow ticks in the same run were much lower:
      - `14.56ms`
      - `0.72ms`
      - `20.34ms`

18. Profiler counter snapshot tied to that run:
    - `GC Allocated In Frame = 1325 B`
    - `GC Allocation In Frame Count = 20`
    - `Texture Memory = 2010269152 B`
    - `Gfx Used Memory = 2788252503 B`
    - `Profiler Used Memory = 1887579840 B`
    - `Total Used Memory = 8783733599 B`
    - `render_textures = 1101`
    - `render_textures_bytes = 1631522368 B`

19. Screenshot review summary:
    - selected profiler frames were not showing a giant gameplay-script stall inside the render tree
    - `RenderPlayModeViewCameras` and `UpdateScene` on the chosen frames were in low single-digit milliseconds
    - render breakdown showed:
      - `Ocean Mask` and `Underwater Effect` were present but not dominant CPU costs on those frames
      - `ExecuteRenderGraph` and `ScriptableRenderContext.Submit` were visible, but still only low-millisecond contributors
    - practical meaning:
      - current big first-party CPU offender is still scatter startup/rebuild, not Crest/water rendering
      - current major non-CPU pressure is VRAM / render-texture usage

20. Updated comparison against the previous validated scatter startup checkpoint:
    - previous dirty rebuild:
      - `144.95ms total`
      - `sample=100.81ms`
      - `post=60.44ms`
      - `spawn=20.95ms`
    - latest dirty rebuild:
      - `77.76ms total`
      - `sample=53.42ms`
      - `post=35.77ms`
      - `spawn=9.19ms`
    - meaning:
      - recent scatter work reduced startup cost materially again
      - scatter remains the top first-party CPU offender, but no longer at the earlier 145-161ms startup level

21. Current direction after this checkpoint:
    - continue honest scatter work on the remaining startup/reconcile tail
    - begin a focused first-party RT / VRAM audit because `render_textures_bytes ~ 1.63 GB` is too large for the MX350 target
    - status remains `PENDING VERIFICATION`

22. A focused first-party RT / VRAM audit was completed:
    - only one explicit first-party `RenderTexture` asset exists:
      - `Assets/_Project/Art/TEXTURES/RT_HUD_Display.renderTexture`
    - it is a `1920x1080` shared visor HUD projection RT
    - the player prefab uses the visor in shared RT mode (`_projectionMode: 1`)
    - meaning:
      - the very large `render_textures_bytes ~ 1.63 GB` number is not caused by a large set of first-party custom RT assets
      - it is mostly internal render-pipeline / editor / camera footprint, with the visor RT path still being the one explicit first-party RT path worth tightening

23. A narrow first-party visor-camera optimization was applied in
    `Assets/_Project/Prefabs/Player.prefab`:
    - the dedicated `HUD_Render_Camera` had expensive settings enabled for a HUD-only projection camera:
      - HDR
      - MSAA
      - occlusion culling
      - URP shadows
      - URP depth texture option via pipeline settings
      - URP opaque texture option via pipeline settings
      - XR rendering allowance
      - HDR output allowance
    - these were turned off for that camera
    - reason:
      - this camera renders only the internal HUD layer into the visor RT
      - those features add bandwidth / RT overhead without meaningful value on the MX350 target

24. Verification status after the visor-camera prefab pass:
    - Unity asset refresh completed
    - no new first-party import or compile errors were introduced
    - console came back clean after refresh
    - runtime benefit is still `PENDING VERIFICATION`

25. Another autonomous first-party block was completed after the latest validated run:
    - `WorldProceduralScatterDirector.ReconcileInstances(...)` now resolves `WorldGenerativeGeologyService` once per reconcile pass and passes it through to `ApplyGeneratedGeology(...)`
    - the same path now reuses the already-cached observer position for `ShouldApplyGeneratedGeology(...)`
    - reason:
      - reduce repeated service resolution and repeated transform reads inside the startup reconcile/spawn path

26. The dedicated visor HUD camera in `Assets/_Project/Prefabs/Player.prefab` was tightened further:
    - disabled on `HUD_Render_Camera`:
      - HDR
      - MSAA
      - occlusion culling
    - disabled on its URP camera data:
      - render shadows
      - depth texture requirement
      - opaque texture requirement
      - XR rendering
      - HDR output
    - reason:
      - this is a HUD-only projection camera rendering into the visor RT
      - those features spend bandwidth / RT budget without meaningful value for the MX350 target

27. Verification status after this autonomous block:
    - Unity refresh + compile completed
    - no new first-party compile/import errors were introduced
    - console stayed clean after refresh
    - runtime effect remains `PENDING VERIFICATION`
- 2026-04-04 scene/prefab consistency guard: confirmed live scene drift on `--- GAMEPLAY ---/Player/HUD_Render_Camera` vs `Assets/_Project/Prefabs/Player.prefab`. Scene instance had stale heavy camera flags (`allowHDR=true`, `allowMSAA=true`, `renderShadows=true`, `requiresDepthOption=2`, `requiresColorOption=2`, `allowXRRendering=true`, `allowHDROutput=true`) while prefab asset already had optimized values. Live scene instance synced back to prefab-equivalent values via Unity MCP. Scene remains dirty/unsaved; prefab asset is still source of truth. Do not use blanket `Apply All` on Player/HUD/visor prefab instances without re-verifying perf-critical camera/RT properties.
- 2026-04-04 AGENTS verification: root `AGENTS.md` now contains `### [RULE] PREFAB / SCENE CONSISTENCY GUARD` at line 313. Confirmed camera subtree audit for `Player` prefab instance. `Main Camera`, `SpaceCamera`, and `Suit_Visor` matched expected critical values. Confirmed actual drift on `HUD_Render_Camera`: prefab asset still had heavy camera flags while live scene had optimized values. Normalized `Assets/_Project/Prefabs/Player.prefab` `HUD_Render_Camera` camera block to `m_HDR=0`, `m_AllowMSAA=0`, `m_OcclusionCulling=0`; URP block already normalized (`m_RenderShadows=0`, `m_RequiresDepthTextureOption=0`, `m_RequiresOpaqueTextureOption=0`, `m_AllowXRRendering=0`, `m_AllowHDROutput=0`). Re-imported assets, verified live scene readback matches, and saved `Assets/_Project/Scenes/02_HECTON_WORLD.unity` so scene is no longer dirty.
- 2026-04-04 broad scene consistency pass continued. Active HUD path confirmed as `--- UI ---/Suit_HUD_Canvas` + `--- UI ---/Suit_HUD_ProjectionSource`; `HUD_Internal` is inactive and not the primary path. `Player` camera subtree re-verified. `HUD_Render_Camera` live scene kept synced to lightweight values and saved earlier. Normalized `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab` to the live scene on stable HUD tuning fields: `CanvasScaler.m_ReferenceResolution=1600x900`, `SuitHUDV4CanvasOverlay.overallScale=0.98`, `chromeAlpha=0.14`. Materialized missing stable compositor fields in `Assets/_Project/Prefabs/HUD_Internal.prefab`: `showAsInsetPreview=1`, `insetSize=340x340`, `insetMargin=18x18` without pushing scene refs. During the same pass found and corrected a confirmed asset bug: `Assets/_Project/Prefabs/Item_Titanium.prefab` had `PickupItem.itemData` pointing to `Data_Copper.asset`; fixed to `Assets/_Project/Data/Items/Resources/Raw/Data_TitaniumScrap.asset` and live scene readback now matches. `Global Volume` matches prefab; `Directional Light` scene differs from prefab on light intensity, but was left untouched because it is likely scene/runtime-authored rather than a safe prefab sync candidate.
- 2026-04-04 continued broad scene consistency pass. Remaining exact-match prefab-backed roots were classified: `Global Volume` matches prefab; `VoxelChunk` matches prefab; `Objects` matches prefab; `Sky_System` appears runtime/scene-authored (follow-camera root position differs, script defaults match); `GasGiant_Aegir` root transform differs from prefab and is scene-placement data, not a safe blind sync; `Ocean_Crest` contains runtime camera/light refs and third-party state, so no blind prefab push was made. Nested `Tool_Staging` world-item prefabs were validated in bulk: all `Item_Tool_*_World.prefab` assets point to the correct matching `Item_Tool_*.asset` itemData and did not show the titanium-style mismatch.
28. Resumed the real optimization plan after the prefab/scene detour:
    - CPU priority remains `WorldProceduralScatterDirector` startup/reconcile/spawn
    - RAM/VRAM remains secondary and limited to first-party RT/texture footprint until CPU spikes are pushed lower

29. Added a pass-local reconcile-plan cache in
    `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`:
    - new field: `_reconcilePlanVersion`
    - both `PrepareScatterPoolWarmup(...)` and `ReconcileInstances(...)` now reuse one cached per-placement decision for:
      - resolved runtime variant
      - final-variant state
      - active instance reference
      - whether the placement requires spawn/rebuild
    - reason:
      - the startup `dirty` rebuild was still paying for the same resolve/rebuild logic twice: once in warmup, once again in reconcile

30. Added another low-risk hot-path cleanup in
    `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`:
    - `ApplyPlacement(...)` now uses `SetPositionAndRotation(...)`
    - `ScatterPlacement` now caches biome/profile/pattern label strings at initialize time instead of resolving them every time metadata is applied
    - reason:
      - reduce repeated scalar work in the remaining reconcile/apply path without changing behavior or adding allocations

31. Verification status after this autonomous block:
    - Unity script refresh/compile completed
    - no new first-party compile errors were introduced
    - console still shows only pre-existing third-party `UDR0001` warnings
    - runtime effect remains `PENDING VERIFICATION` until the next heavy run
32. Added a small first-party VRAM guard in
    `Assets/_Project/Scripts/Visor/VisorHUDController.cs`:
    - runtime RT defaults now match the already-optimized shared visor RT path
    - default width/height changed from `1920x1080` to `1280x720`
    - reason:
      - if any instance falls back to runtime RT mode, it no longer silently allocates a larger full-HD target by default

33. Verification after the visor runtime RT default pass:
    - Unity script refresh/compile completed
    - no new first-party compile errors were introduced
    - console still shows only existing third-party `UDR0001` warnings

34. Completed a substantial active-HUD runtime pass in
    `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`:
    - heading/status labels no longer build strings every tick
    - suit/heading/status text now use dirty-gated updates
    - gauge labels, values, fill amounts, and colors now use cached writes instead of blind per-tick assignment
    - static chrome/reticle/telemetry colors and root scale moved behind `ApplyStaticStyleIfNeeded(...)`
    - canvas/scaler/render-mode setup moved behind `_canvasStateApplied` so `NormalizeCanvas()` stops rewriting the same state every tick
    - added `InvalidateVisualCaches()` on layout/hierarchy invalidation to keep rebuilt UI state correct
    - reason:
      - this is the actual active HUD path, and the old code was rewriting static UI state and building strings inside `Tick`

35. Verification after the active HUD pass:
    - no new first-party compile errors surfaced in console
    - console still shows only pre-existing third-party `UDR0001` warnings
    - runtime effect remains `PENDING VERIFICATION` until the next heavy run

36. Follow-up block after that:
    - scatter startup path:
      - cached reconcile-plan now stores generated-geology decision, sync signature, and initial-warmup eligibility
      - `PrepareScatterPoolWarmup()` folded allowance build into the warmup pass instead of doing a second dictionary sweep
      - `CreateScatterInstance()` now returns resolved `WorldProceduralProxyInstance`, removing duplicate component lookups on create/rebuild
    - active HUD support:
      - `SuitHUDV4CanvasOverlay` caches `Canvas` / `CanvasScaler`
      - `HectonSuitHUDExtensions` resolves player root once per auto-resolve and reuses cached flashlight heat for diagnostics
    - verification:
      - Unity editor returned `ready_for_tools=true`
      - console still shows only pre-existing warnings
      - runtime remains `PENDING VERIFICATION` until the user runs the next heavy scenario
37. Latest autonomous block:
    - new run facts captured:
      - startup dirty rebuild `186.35ms` (`sample=131.68`, `post=76.63`, `reconcile=35.62`, `spawn=20.78`)
      - recurring movement `cell-changed` rebuild `43.54ms` (`sample=27.02`, `post=24.42`, `reconcile=11.95`, `spawn=3.91`)
      - worst capture frame is polluted by `Mono.JIT`, Burst JIT and profiler callstack; recurring gameplay hitch is still scatter, not GC
      - current counters: `GC Allocated In Frame=1260 B`, `Texture Memory=2018604839 B`, `Gfx Used Memory=2162094879 B`, `Total Used Memory=8128271343 B`
    - code changes:
      - `ScatterPlacement` caches effective spacing, fauna anchor flags, and fauna radius
      - `PublishFaunaRegistrySnapshot`, placement-grid registration, candidate spacing, and required-distance checks now use cached placement data
      - `GameTickManager` slow-tick diagnostics now rate-limit spike logs and use type-only labels, reducing profiler/log-induced allocations on repeated spike frames
      - `WorldProceduralScatterDirector.ShouldSkipScatterRefresh()` now applies a one-cell `cell-drift-buffer` for large runtime windows, deferring full rebuilds on every adjacent-cell crossing
    - verification:
      - Unity compile passed
      - console shows no new first-party compile errors
      - runtime status stays `PENDING VERIFICATION`
    - new runtime finding:
      - live MapMagic tiles near player had `ActiveTerrain=null` and only inactive `Draft Terrain` children with valid `TerrainData`
      - this made `MapMagicBridge.FindTerrainAt()` miss terrain completely because it relied on `Terrain.activeTerrains`
      - patched bridge now caches `TerrainTile[]` on hierarchy change and resolves `ActiveTerrain -> main -> draft`
      - `WorldProceduralFieldSampler` seafloor probe switched to `RaycastNonAlloc` and ignores player/self hits to stop false `terrain-source-upgraded` reprimes
38. Runtime regression fix-up:
    - latest pre-patch evidence:
      - scatter spike still active: `rebuild=96.67ms`, `sample=71.53ms`, `post=41.44ms`, `reconcile=18.09ms`, `spawn=8.69ms`, `reason=dirty`
      - secondary runtime offender: `FaunaDirector=38.13ms` with explicit pool creation/exhaustion warnings for `SmallPassiveProxy`, `HunterProxy`, `TerritorialProxy`
      - `HectonPlayerMovement.UpdateCrestWaterHeight()` was throwing `NullReferenceException` every `FixedTick`
      - live MCP showed `MapMagicBridge.IsAvailable=false`, `mapMagicObject=null`, `playerTransform=null`, and field sampler still on `FallbackSynthetic`
    - code changes:
      - `HectonPlayerMovement` now cold-allocates `Crest.SampleHeightHelper` in `Awake()` and null-guards the runtime sampling path
      - `MapMagicBridge` now performs throttled scene-binding recovery in `SlowTick()` and uses `WorldRuntimeReferenceUtility` for player rebinding
      - `FaunaDirector` now warms resolved creature prefabs up to `4` pooled instances before runtime spawn pressure hits
    - verification:
      - Unity compile passed
      - console shows no new first-party compile errors
      - runtime effect is still `PENDING VERIFICATION`
- Latest profiled runtime before current fixes: scatter still top offender (`rebuild=105.39ms`, `sample=77.57ms`, `post=44.25ms`, `reconcile=20.53ms`, `spawn=9.00ms`, `reason=dirty`), `TickProfiler` peak `162.19ms` with `WorldProceduralScatterDirector=130.81ms`, `FaunaDirector=17.21ms`.
- Concrete follow-up applied: scene wiring fix for `MapMagicBridge` (serialized `mapMagicObject`/`playerTransform` now present and scene saved) plus `FaunaDirector` warmup target increased to 8 due continued pool expansion warnings for `SmallPassiveProxy` and `HunterProxy`.
- Verification pending next gameplay run.
- Further objective pass: `WorldProceduralScatterDirector.SetFaunaSpawnRegistry(...)` now short-circuits same-registry assignments to avoid redundant fauna-registry invalidation; editor wiring now invokes live `MapMagicBridge` setters; `FaunaDirector.TryWarmupCreaturePools()` dedupes repeated prefabs across biome datasets before warmup. Compile clean. Runtime effect still pending.
