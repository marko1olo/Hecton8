# STARTUP LOG SPAM AUDIT
Date: 2026-04-29

Status: `PENDING VERIFICATION`

## Scope

This document records the startup log-spam findings raised on `2026-04-16` and separates:

- confirmed code-level facts
- strong but still unverified runtime hypotheses
- actions already started in this recovery pass

It is not a claim that every pasted inference from the external summary is already proven inside this repo.

## Confirmed Facts

### 0. Original log files were checked directly

Original sources used in this pass:

- `C:\Users\danat\Documents\part1_log yuniti.txt`
- `C:\Users\danat\Documents\part2_log yuniti.txt`

Confirmed from those files:

- `part1` contains `1214` occurrences of `TLS Allocator ALLOC_TEMP_TLS`
- `part2` contains `1226` occurrences of `TLS Allocator ALLOC_TEMP_TLS`

This is not “a few warnings”. It is sustained allocator flood.

### 1. `Starter_ReefField` is a real production-world identifier

Confirmed in authoring code:

- `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs`
  - `private const string StarterReefFieldName = "Starter_ReefField";`
  - `private const string StarterReefFieldPath = "--- WORLD ---/" + StarterReefFieldName;`

That means the temp allocator spam string is not random garbage. It matches a real world-space hierarchy name used by this project.

Confirmed from the original logs:

- `part1` lines `107-119` show the UTF-16 dump for `Starter_ReefField`
- `part2` starts with the same repeating UTF-16 dump

The flood also alternates between the two exact addresses reported in the external summary:

- `00000269E0600050`
- `00000269E0600010`

### 2. A runtime hot path was reading hierarchy names after the shipping-filter patch

Confirmed in:

- `Assets/_Project/Scripts/World/WorldShippingContentFilter.cs`
- `Assets/_Project/Scripts/WorldZoneAnchor.cs`
- `Assets/_Project/Scripts/WorldContentSocket.cs`
- `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs`
- `Assets/_Project/Scripts/WorldZoneDirector.cs`
- `Assets/_Project/Scripts/WorldContentDirector.cs`

Before the current fix pass, the shipping-content filter was doing:

- ancestor traversal via `Transform current = target; while (current != null) ...`
- `current.name` checks inside runtime suppression evaluation

That suppression path is used by:

- `WorldZoneAnchor.CopyActiveAnchorsTo(...)`
- `WorldContentSocket.CopyActiveSocketsTo(...)`
- `WorldProceduralFieldSampler.RefreshActiveAnchorsSnapshot()`
- `WorldZoneDirector.RefreshAnchors()`
- `WorldContentDirector.RefreshSockets()`

This is a real architecture violation against `AGENTS.md` hot-path string rules.

### 3. `WorldProceduralFieldSampler` participates in repeated runtime anchor refresh

Confirmed in:

- `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs`

`PrepareBurstData()` checks `WorldZoneAnchor.ActiveAnchorVersion`, and when dirty calls:

- `RefreshActiveAnchorsSnapshot()`
- `WorldZoneAnchor.CopyActiveAnchorsTo(_anchors)`

So the shipping filter is not only an editor-time concern. It sits in a scatter-adjacent runtime path.

### 4. Acoustic snapshot warnings are real authoring gaps, but already one-shot guarded

Confirmed in:

- `Assets/_Project/Scripts/AcousticZoneController.cs`

Missing authored snapshots still produce warnings for:

- `UnderwaterSnapshot`
- `SurfaceSnapshot`
- `SurfaceRainSnapshot`
- `SurfaceStormSnapshot`
- `BaseInteriorSnapshot`

But those warnings already go through `LogSnapshotFallbackWarningOnce(...)`. This is not likely the source of the massive repeating spam.

### 5. `CameraJuiceSystem` has two distinct warning classes

Confirmed in:

- `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs`

One-shot startup warnings:

- missing `Vignette`
- missing `ChromaticAberration`
- missing `DepthOfField`

Recurring throttled warning:

- `Frame time exceeded budget`

This recurring warning is throttled by `_nextLogTime` to once per 5 seconds in editor/development. It can still pollute logs, but it is not the same class of spam as the allocator flood.

### 6. `WorldLODSceneBootstrap` really can report zero authored `LODGroup`s

Confirmed in:

- `Assets/_Project/Scripts/World/WorldLODSceneBootstrap.cs`

It scans the scene once at startup with `Object.FindObjectsByType<LODGroup>(...)`, filters by scene, filters through `WorldShippingContentFilter`, then logs the resulting count. If the count is `0`, that is likely authored scene truth, not random logger corruption.

### 7. Scatter cost is already documented elsewhere in this repo

Confirmed in:

- `RUNTIME_PROBLEM_REPORT_AND_FILES.txt`

The repo already contains prior captured `[WorldScatterProfiler]` spikes and rebuild timings. So the external summary's high-level performance concern is directionally consistent with evidence already present in the project.

### 8. The original startup logs confirm the exact startup warning set

Confirmed from `part1_log yuniti.txt`:

- line `19`: `[CameraJuiceSystem] DepthOfField override not found in Volume profile.`
- line `23`: runtime lore recovery warning from `HectonLoreSystemsRoot`
- line `28`: `[AcousticZoneController] Surface snapshot set missing and no fallback snapshot exists. Surface acoustic transitions will keep the previous mixer state.`
- line `50`: `[WorldLODSceneBootstrap] Registered 0 LODGroup components for scene '02_HECTON_WORLD'.`
- line `55`: `[CameraJuiceSystem] Frame time exceeded budget: 1.54ms`
- lines `60` and `89`: MCP-for-Unity parallel batch warning

Confirmed later in `part1`:

- line `8752`: `[AcousticZoneController] UnderwaterSnapshot missing. Falling back to surface/interior snapshot coverage.`
- lines `8762`, `31038`, `36784`, `39528`: underwater/surface transition diagnostics

Confirmed in `part2`:

- line `41825`: `[AcousticZoneController] MasterMixer is assigned but no authored acoustic snapshots were resolved by name. Expected names include Underwater/UnderwaterSnapshot, BaseInterior/BaseInteriorSnapshot, Surface/SurfaceSnapshot, SurfaceRain/SurfaceRainSnapshot, SurfaceStorm/SurfaceStormSnapshot.`

This means the audio snapshot problem is not hypothetical and not limited to one warning branch.

### 9. The original logs confirm the exact scatter timing progression

Confirmed from `part1_log yuniti.txt`:

- line `78`: `rebuild=45840.10ms` `wait=45489.56ms` `reason=None`
- line `487`: `GameTickManager first-slow-tick registered=39`
- line `493`: `WorldScatterRuntime first-slow-tick bootstrapReady=False defer=False invalidation=None`
- line `500`: `rebuild=75.33ms` `wait=64.47ms` `reason=pending-complete`
- line `513`: `TickProfiler SlowTick spike total=80.33ms`
- line `1394`: `rebuild=262.91ms` `wait=258.66ms` `reason=pending-complete`
- line `2621`: `rebuild=742.41ms` `wait=736.97ms` `reason=startup-settle`

Confirmed from `part2_log yuniti.txt`:

- line `4742`: `rebuild=510.49ms` `wait=504.88ms` `reason=cell-changed`

Repeated invariants in those same log lines:

- `floraGpuiActive=0`
- `floraGpuiPrototypes=0`
- `floraGpuiReady=True`
- `zone=Synthetic:Navigation`
- `biome=Littoral Karst`
- `pattern=ReefNavigation`
- `topFamily=Cave Entrance Marker`

### 10. The asset-side warning sources were checked directly

Confirmed in `Assets/_Project/MasterMixer.mixer`:

- `m_Snapshots` contains only one snapshot
- that snapshot is named `Snapshot`
- there are no authored snapshots named:
  - `Underwater` / `UnderwaterSnapshot`
  - `BaseInterior` / `BaseInteriorSnapshot`
  - `Surface` / `SurfaceSnapshot`
  - `SurfaceRain` / `SurfaceRainSnapshot`
  - `SurfaceStorm` / `SurfaceStormSnapshot`

Confirmed in `Assets/_Project/Scenes/02_HECTON_WORLD/Main Camera Profile.asset` before the current patch:

- the world camera profile contained only:
  - `Vignette`
  - `ChromaticAberration`
- it did not contain `DepthOfField`

That means both warning families were backed by real asset debt, not false logger noise.

## Strong Hypotheses Not Yet Proven

### A. The `ALLOC_TEMP` / `Starter_ReefField` spam may be caused by hierarchy-name access, not by NativeArray disposal

Current strongest candidate:

- runtime `Transform.name` / `GameObject.name` access on objects under `Starter_ReefField`
- specifically in shipping-content suppression checks added during the recent scene-truth cleanup pass

Why this is plausible:

- the exact leaked UTF-16 string matches a real hierarchy name
- that hierarchy name is now reachable from runtime suppression logic
- Unity `Object.name` is a known allocation source and violates project hot-path rules

What is still missing:

- a fresh post-fix startup log proving the allocator spam is reduced or removed
- or a `-diag-temp-memory-leak-validation` callstack from the user/editor launch configuration

### B. Scatter/geology spikes are real, but root cause ranking is still unresolved

The external summary attributes major cost to:

- `WorldGenerativeGeologySeamExecutionDirector`
- `WorldGenerativeGeologyTerrainSeamApplier`
- `WorldProceduralScatterDirector`

That is plausible, but this audit has not yet re-run live profiler/console capture in a clean Unity-ready session during this pass.

### C. Thread-pool starvation / GC interaction is plausible but not yet proven

The external summary infers escalating `wait=` timings and possible starvation. That is a credible diagnosis pattern, but not something this document can mark as proven without fresh runtime traces.

## Action Started In This Pass

### 1. Shipping-filter hot path de-allocation fix

Started in:

- `Assets/_Project/Scripts/World/WorldShippingContentFilter.cs`

Change intent:

- remove repeated runtime hierarchy-name checks from suppression evaluation
- replace them with cold-path cached transform instance IDs per active runtime scene
- keep name-based detection only in cold-path cache priming / scene cleanup

Expected effect:

- `Starter_ReefField` string should stop being re-read in hot runtime suppression checks
- if the external allocator spam was caused by `Object.name` access in this path, startup spam should materially drop

Status:

- code change started
- runtime confirmation still absent

### 3. World camera profile `DepthOfField` authoring fix

Applied in:

- `Assets/_Project/Scenes/02_HECTON_WORLD/Main Camera Profile.asset`

Change:

- added a real `DepthOfField` override to the world camera profile
- left it default-inactive so `CameraJuiceSystem` can drive activation and focus distance without startup blur

Expected effect:

- remove the one-shot `CameraJuiceSystem` warning:
  - `DepthOfField override not found in Volume profile.`
- restore the intended interaction-focus path instead of leaving DoF unavailable at runtime

Status:

- asset patched
- Unity import/runtime confirmation still absent

### 4. Acoustic warning consolidation without hiding authoring debt

Applied in:

- `Assets/_Project/Scripts/AcousticZoneController.cs`

Change:

- when zero authored acoustic snapshots are resolved at all, keep the global summary warning
- suppress extra per-zone fallback warnings that only repeat the already-known root cause

This does **not** claim the audio problem is fixed. The mixer still lacks authored snapshot coverage. The change only improves log hygiene.

Expected effect:

- keep one honest summary warning for missing snapshot authoring
- reduce redundant follow-up warnings during surface/underwater transitions in the same session

Status:

- code patched
- runtime confirmation still absent

### 5. Shipping-filter compile warning cleanup

Applied in:

- `Assets/_Project/Scripts/World/WorldShippingContentFilter.cs`

Change:

- replaced deprecated `GetInstanceID()` usage with `GetEntityId()`
- replaced deprecated implicit `SceneHandle` conversions with `GetRawData()`

Why this mattered:

- the previous shipping-filter fix removed one suspected allocation path
- but it introduced new compile-time warnings in Unity 6000.4.1f1
- those warnings were visible in the editor console and would have become fresh log debt from this pass

Status:

- code patched
- editor compile re-check confirmed those warnings no longer appear in the console snapshot after refresh

## Latest Unity Verification Snapshot

This section reflects the most recent Unity-side checks during this pass.

Verified in Unity editor/console:

- `DepthOfField override not found in Volume profile.` did **not** reappear in the captured startup console after `Main Camera Profile.asset` was patched
- `TLS Allocator` filtered console query returned `0` entries in the captured startup window
- `Starter_ReefField` filtered console query returned `0` entries in the captured startup window
- `Surface snapshot set missing` filtered console query returned `0` entries
- `UnderwaterSnapshot missing` filtered console query returned `0` entries
- `WorldShippingContentFilter` deprecated API warnings disappeared after the `GetEntityId()` / `GetRawData()` cleanup

Still present in Unity:

- one acoustic summary warning remains valid:
  - `MasterMixer is assigned but no authored acoustic snapshots were resolved by name...`
- lore recovery warning remains valid because the production scene still lacks authored player-facing lore placement
- `WorldLODSceneBootstrap` still reports `Registered 0 LODGroup components for scene '02_HECTON_WORLD'`

Operational limitation:

- Unity MCP repeatedly drops the session on entering Play Mode
- because of that, deep play-mode capture beyond the early startup window is not reliable in this turn
- conclusions about the absence of allocator flood are therefore **promising but not final proof**

### 2. Bootstrap scatter prime de-synchronization

Started in:

- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`
- `Assets/_Project/Scripts/SceneBootstrap.cs`

Original problem:

- `RebuildScatterPreview()` used `TryRunScatterSamplingSynchronously()` while `_bootstrapRuntimeState.AllowPrimePass` was true
- `TryRunScatterSamplingSynchronously()` performs `_samplingJobHandle.Complete()` on the main thread
- `SceneBootstrap` explicitly called `TryPrimeBootstrapScatterPass()` before player activation

That is a direct architecture match for the startup stall seen in the original logs.

Change intent:

- keep bootstrap prime radius limiting
- remove synchronous prime behavior from runtime bootstrap prime path
- let bootstrap prime use the same async state-machine progression used by the normal runtime path
- expose “prime still in flight” state so `SceneBootstrap` does not falsely treat an in-flight async prime as complete

Expected effect:

- startup should stop blocking on `_samplingJobHandle.Complete()` inside the bootstrap prime call itself
- remaining prime work can continue across frames instead of freezing one frame for tens of seconds

Status:

- code change started
- runtime confirmation still absent

### 2.1. New log delta from `C:\Users\danat\Documents\novye logi.txt`

Confirmed from the new raw log:

- hard compile blocker:
  - `Assets\_Project\Scripts\UI\PDADataLogTab.cs(356,33): error CS0103: The name 'GetLocalizedCategoryLabel' does not exist in the current context`
- catastrophic startup scatter stall:
  - `rebuild=30037.07ms`
  - `sample=29982.09ms`
  - `wait=29815.33ms`
  - `reason=None`
- same allocator flood still present:
  - UTF-16 dump resolves to `Starter_ReefField`
  - repeated `TLS Allocator ALLOC_TEMP_TLS ... size 68`

The stack in that log pins the live stall seam to:

- `WorldProceduralScatterDirector.ProcessCompletedScatterSampling()`
- `WorldProceduralScatterDirector.HandleScatterStateMachine()`
- `WorldProceduralScatterDirector.RebuildScatterPreview()`
- `WorldProceduralScatterDirector.Tick(float dt)`
- `GameTickManager.Update()`

Current repo state after this pass:

- `PDADataLogTab.cs` now contains a real `GetLocalizedCategoryLabel(AudioLogCategory)` helper
- `WorldInterestDirector` no longer writes `bestAnchor.name` into runtime debug state
- `WorldInterestAnchor` now exposes a stable serialized `InterestLabel`

Important negative result:

- the `Starter_ReefField` allocator flood survived the `WorldInterest*` hot-path name fix
- so that previous fix was correct, but it was not the root cause

Additional evidence collected in the live scene before the MCP bridge degraded:

- total `WorldZoneAnchor` count = `3`
- total `WorldInterestAnchor` count = `3`

That weakens the theory that the 30-second stall is caused by huge zone-anchor cardinality inside the sampling job.

### 2.2. Explicit cold-path Burst/job prewarm

Applied in:

- `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs`
- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`
- `Assets/_Project/Scripts/SceneBootstrap.cs`

Change:

- added explicit cold-path prewarm for the scatter `CellSamplingJob`
- prewarm runs before bootstrap prime passes, while the player is still inactive
- prewarm schedules a one-cell sampling job and completes it immediately on the cold path

Why this exists:

- the catastrophic `reason=None` stall appears on the initial sampling pass
- later passes remain slow, but are materially smaller
- that pattern is consistent with first-use Burst/job compilation debt

This is containment, not proof of cure. It moves one-time compilation/setup debt into bootstrap instead of letting the first gameplay-adjacent scatter pass absorb it.

### 2.3. Latest live Play Mode capture after the prewarm patch

Latest live capture facts:

- after a short `8s` window the console stayed empty
- a longer live capture still reproduced the allocator flood:
  - `Allocation of 34 bytes ... Starter_ReefField`
  - `TLS Allocator ALLOC_TEMP_TLS ... size 68`
- scatter still produced a catastrophic stall, but the observed peak was lower than the earlier `30s` log:
  - `[WorldScatterProfiler] rebuild=11908.95ms`
  - `sample=11874.04ms`
  - `wait=11781.34ms`
  - `reason=same-cell`

Interpretation:

- the prewarm patch did **not** eliminate the world stall
- it may have reduced the first visible peak, but that is still only a tentative observation
- the allocator flood survived untouched

Additional runtime debt surfaced in the same capture:

- repeated input-system errors:
  - `Map must be contained in state`
  - `Map index on InputActionMap is out of range`
- one VFX budget warning:
  - `[CameraJuiceSystem] Frame time exceeded budget: 5.62ms`

Current honest state:

- compile blocker in `PDADataLogTab` appears locally repaired
- startup scatter stall remains a live blocker
- `Starter_ReefField` temp allocator spam remains a live blocker
- input action-map state corruption is a new confirmed runtime defect and should be handled as a separate repair slice

## Prioritized Next Checks

1. Re-run startup and inspect console after the shipping-filter fix.
2. If allocator spam remains, capture exact new spam signature and isolate the next repeated string source.
3. If available, launch/editor-run with `-diag-temp-memory-leak-validation` for callstack evidence.
4. Re-run startup and compare whether the 45.8s bootstrap scatter stall is gone or materially reduced.
5. Only after the allocator flood and bootstrap scatter stall are under control, continue down the warning list:
   - authored acoustic snapshots
   - real authored LOD presence in `02_HECTON_WORLD`

## Regression Model

CPU:
- expected improvement in repeated suppression checks

GC:
- expected reduction if the spam source was runtime hierarchy-name access

Memory:
- expected reduction only if the allocator spam originated from this path

Cadence:
- no intended gameplay cadence change

Correctness:
- suppression behavior should remain equivalent for known dev/temp roots

WARNING: Regression risk in additive-scene suppression if multiple gameplay scenes begin relying on independent shipping-filter caches simultaneously. Current project architecture is normative single-scene runtime handoff, so this risk is acceptable but still unverified.

## 3. Input + Same-Cell Repair Slice (2026-04-16)

Fresh evidence now being acted on directly:

- user-provided `C:\Users\danat\Documents\novye logi.txt` confirms the compile blocker:
  - `PDADataLogTab.cs(356,33): error CS0103: The name 'GetLocalizedCategoryLabel' does not exist`
- the same log confirms the catastrophic startup scatter path is still alive:
  - `rebuild=30037.07ms`
  - `wait=29815.33ms`
  - later `reason=startup-settle` pass still hits `509.13ms`
- separate live Play Mode capture previously confirmed runtime input corruption:
  - `Map must be contained in state`
  - `Map index on InputActionMap is out of range`

### 3.1. Confirmed input defect in code

`Assets/_Project/Scripts/Input/InputManager.cs` had two concrete problems:

- `OnEnable()` reinitialized the entire runtime `InputActionAsset` on every repeated enable after the first one
- `SafeEnableActionMap()` read `actionMap.enabled` before entering the `try/catch`

Why this is bad:

- repeated `OnEnable()` rebuilds invalidate existing map/action state and can leave external consumers holding stale `InputActionMap` / `InputAction` references
- reading `enabled` outside the guarded block still allows `Map must be contained in state` / `Map index on InputActionMap is out of range` to escape from the exact place the code claims is "safe"

Applied change:

- `OnEnable()` now calls `EnsureInputActionsInitialized()` instead of unconditional `ReinitializeInputActions()`
- `SafeEnableActionMap()` / `SafeDisableActionMap()` now guard the `enabled` check inside the exception handling block

This does not prove the input defect is gone. It removes one direct stale-map churn source and one unsafe read site.

### 3.2. Confirmed same-cell startup rebuild defect in code

`Assets/_Project/Scripts/WorldProceduralScatterDirector.cs` had a real startup cadence flaw:

- `Tick()` ignored `ShouldSkipScatterRefresh()` completely
- while `_startupRuntimeState.StabilizationPending` was true, tick-driven scatter could still re-enter `RebuildScatterPreview()` every `0.25s`

Why this matters:

- the state machine already knows how to skip `same-cell`, `cell-hysteresis`, `cell-drift-buffer`, and to trigger exactly one `startup-settle` refresh
- `Tick()` bypassed that logic and could force same-state rebuild attempts during the stabilization window
- that matches the pattern seen in the logs: startup completes a heavy pass, then additional same-state / startup-settle work still lands immediately afterward

Applied change:

- `Tick()` now runs the same `ShouldSkipScatterRefresh()` gate that `SlowTick()` already uses
- if the state should be skipped, tick only continues pending reconcile work and returns
- rebuild now only happens when the skip logic explicitly says the state is dirty enough to justify it

This is a real logic correction, not a cosmetic guard. It removes one confirmed path that could schedule redundant same-cell rebuilds during startup.

### 3.3. Honest remaining state after this slice

- compile blocker in `PDADataLogTab` was already repaired earlier
- input stale-map root cause is not fully proven dead until a new runtime log capture stays clean
- scatter still has a deeper job-duration / wait problem even after same-state gating; this slice only removes one redundant rebuild source
- `Starter_ReefField` temp allocator flood remains unresolved

## 4. New evidence: the `Starter_ReefField` temp leak may be MCP/editor-induced, not gameplay-induced

New raw log context from `C:\Users\danat\Documents\part1_log yuniti.txt`:

- immediately before the first visible `Starter_ReefField` temp-allocation dump, the log contains an MCPForUnity editor stack:
  - `MCPForUnity.Editor.Tools.BatchExecute/<HandleCommand>d__3:MoveNext`
  - `MCPForUnity.Editor.Tools.CommandRegistry:ExecuteCommand`
  - `MCPForUnity.Editor.Services.Transport.TransportCommandDispatcher:ProcessQueue`
  - `UnityEngine.UnitySynchronizationContext:ExecuteTasks`
- only then does the allocator dump begin:
  - `Allocation of 34 bytes ... Starter_ReefField`
  - `TLS Allocator ALLOC_TEMP_TLS ... size 68`

This does **not** yet prove the game is innocent. But it materially changes the hypothesis ranking:

1. MCP/editor transport or object-enumeration path may be reading `Starter_ReefField` in a leaking temporary-native path.
2. A pure gameplay/runtime system may still also do it, but the evidence base is now weaker than before.

Implication:

- do **not** continue blindly rewriting gameplay systems around `Starter_ReefField` just because the dumped string matches a world root name
- the next valid proof step is callstack evidence with `-diag-temp-memory-leak-validation`, ideally reproduced once with MCP activity and once without it

## 5. Latest editor-state verification after the input/scatter slice

After a clean script refresh and console clear:

- the earlier `HectonPlayerMovement.cs(1040): canSurfaceBreach` error did **not** reproduce on the next clean compile
- current console no longer shows the `_hasEnabledOnce` warning introduced during the input fix
- current console still shows:
  - `The referenced script (Unknown) on this Behaviour is missing!` x4
  - `Leak Detected : Persistent allocates 8 individual allocations`

After a valid unpaused Play Mode capture:

- filtered console queries returned `0` entries for:
  - `WorldScatterProfiler`
  - `Map must be contained in state`
  - `Map index on InputActionMap is out of range`
- the capture was instead dominated by the `Starter_ReefField` allocator flood

Interpretation:

- the current input fix removed one confirmed stale-map churn source and the runtime input spam was not reproduced in this capture window
- the current tick-path scatter fix removed at least one redundant startup same-state rebuild path, but longer capture is still required before claiming the catastrophic stall is gone
- the allocator issue is now the primary live blocker in the observed session

## 6. New verified fix: `VRAMMonitor` was reporting CPU memory as VRAM

Fresh code audit on `Assets/_Project/Scripts/Optimization/VRAMMonitor.cs` found a real source-of-truth bug:

- `TotalVRAMBytes` was populated from `Profiler.GetTotalAllocatedMemoryLong()`
- that API reports total allocated managed/native memory, not GPU graphics-driver memory
- the monitor also hard-coded a single recorder name for texture and RenderTexture counters:
  - `Texture Memory`
  - `RenderTexture Memory`

Why this matters:

- the fresh user log showed:
  - `[VRAMMonitor] BUDGET EXCEEDED: Texture=0.0MB RT=0.0MB Total=2074.6MB`
- that exact shape is consistent with:
  - texture / RT recorders failing to resolve
  - total value coming from system memory instead of VRAM

Applied change:

- `VRAMMonitor` now resolves memory counters from candidate names at startup instead of assuming one counter name
- texture candidates:
  - `Texture Memory`
  - `Texture Used Memory`
- RenderTexture candidates:
  - `RenderTexture Memory`
  - `Render Textures Bytes`
  - `Render Textures Memory`
- total GPU-side memory now comes from `Profiler.GetAllocatedMemoryForGraphicsDriver()`
- if the RenderTexture profiler counter is unavailable or returns zero, the monitor falls back to `RenderTextureLifecycleTracker.TrackedRenderTextureMemoryBytes`
- pressure state now includes texture-budget utilization as a first-class signal instead of only RT + total utilization

Unity-verified:

- first compile failed once with:
  - `VRAMMonitor.cs(56,31): error CS0246: ProfilerRecorderHandle`
- the missing namespace import was fixed
- the next clean script compile returned `0` console errors

This does **not** prove the VRAM leak itself is gone. It proves the monitor is no longer obviously using the wrong top-level memory source.

## 7. New verified runtime state after the VRAMMonitor slice

Fresh clean cycle:

1. `clear console`
2. clean script compile
3. Play Mode runtime capture
4. targeted console filters

Observed facts:

- compile after the final `VRAMMonitor` patch returned `0` console entries
- a fresh 10-second Play Mode window returned `0` general console entries
- targeted filtered console queries returned `0` entries for:
  - `Starter_ReefField`
  - `TLS Allocator`
  - `Map must be contained in state`
  - `Map index on InputActionMap is out of range`
  - `WorldScatterProfiler`
- live rendering stats in Play Mode reported:
  - `render_textures = 371`
  - `render_textures_bytes = 83522206` (~79.7 MiB)
  - `used_textures_bytes = 0`

Interpretation:

- this is the strongest clean runtime window so far: the previously dominant input spam, scatter profiler spam, and `Starter_ReefField` allocator spam did not reproduce in this capture
- `render_textures_bytes` being non-zero proves the runtime now has at least one GPU-memory source that is not lying as total zero
- `used_textures_bytes = 0` means the broader texture-memory telemetry story is still not fully trustworthy across all counters; more runtime evidence is still required

WARNING: Regression risk in the MCP tooling containment patch. A temporary editor-only change was made in `Library/PackageCache/com.coplaydev.unity-mcp.../GameObjectLookup.cs` so hierarchy churn no longer clears cached names on every change. That helped isolate allocator spam under controlled MCP queries, but it lives in an immutable package cache and is not a durable product-side fix.

## 8. Missing-script status after direct owner passes

Two direct owner passes were executed:

- `Tools/Hecton/Dev/Scene/Remove Missing Scripts In Loaded Scenes`
- `Tools/Hecton/Dev/Scene/Remove Missing Scripts In _Project Prefabs`

Verified results:

- loaded scenes owner reported:
  - `[Hecton Dev] No missing scripts found in loaded scenes.`
- prefab owner pass did **not** report removed prefab entries
- generic console spam still reproduced during prefab-side scanning:
  - `The referenced script (Unknown) on this Behaviour is missing!` repeated many times

Interpretation:

- the current missing-script issue is **not** in the loaded `02_HECTON_WORLD` scene graph
- it is likely asset-side and may involve prefab dependencies outside the `_Project` root or script assets whose GUID still resolves but whose `MonoScript` type no longer resolves cleanly in Unity
- static YAML scans for broken `m_Script` GUIDs did not find an obvious first-party deleted-script case

This remains unresolved.
