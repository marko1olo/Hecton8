# WORLD RUNTIME CODE AUDIT - 2026-04-03

Purpose:
- inspect active runtime scripts for zero-GC violations and runtime search/rebuild churn
- separate true hot-path problems from one-shot/editor-only code
- record what was changed and why

## High priority - confirmed active runtime risk

### `WorldProceduralScatterDirector.cs`
- Why it matters:
  - last validated traces directly tied giant startup/runtime GC spikes to scatter pool warmup
  - this is not theory; the trace showed `family.coral.low` warmup immediately before the `~4.8MB` window
- What was already done before this audit:
  - reduced runtime warmup burst size
  - added runtime cooldown
  - reduced runtime reserve top-up per rebuild
  - removed hot runtime escalation to large per-prefab warmup caps
- What was changed in the current pass:
  - runtime warmup reserve was removed from the scatter path
  - runtime warmup now prewarms exact rebuild demand only, not extra headroom
  - startup reserve now scales only from real direct demand instead of fixed family reserve
  - startup initial pass now has an explicit create/warmup batch limit through `maxInitialScatterCreatesPerRebuild`
  - the startup batch limit is applied to both warmup collection and actual instance creation, so the system no longer warms a larger batch than it is allowed to create in the same reconcile
- Why it is still important:
  - this remains the main confirmed source of the largest spikes from live traces
- Current status:
  - still the top runtime target
  - needs next live verification after exact-demand runtime warmup and startup batching changes

### `WorldGenerativeGeologyService.cs`
- Why it mattered:
  - active rebuild path was creating new `Renderer[]` and new `LOD[]` data every rebuild
  - old code also used `ToArray()` on the LOD list
  - this is managed heap churn on active world rebuilds
- What was changed:
  - moved to cached `LOD[]` per generated runtime
  - moved to cached `Renderer[]` per LOD slot
  - kept primitive/root reuse path already introduced earlier
- Why this is better:
  - repeated rebuilds with the same topology now reuse managed arrays instead of allocating them every time
- Honest caveat:
  - if topology count changes, cache can still resize once
  - this is much better than per-rebuild allocation, but not absolute zero allocation for every possible topology mutation

## Remaining non-editor debt after this pass

### bootstrap/load-only search paths
- `SceneBootstrap.cs`
- `HectonPlayerSpawner.cs`
- `MapMagicBridge.cs`
- `WorldStateManager.cs`
- Why they were not the next target:
  - these are startup/load-time search paths, not steady-state gameplay loops
  - they can still hurt startup smoothness, but they do not explain the validated runtime warmup spikes seen in live traces

### diagnostic/editor-style runtime helpers still present in non-editor assemblies
- `RuntimePerformanceProfiler.cs`
- `HectonFabricatorUI.cs` fallback font discovery
- `SkySystemFollowCamera.cs` full camera recovery scan
- Why they were not treated as the main villain:
  - profiler search is diagnostic by design
  - fabricator font search is one-shot startup fallback
  - sky follow helper now caches its resolved camera and only scans if no valid camera exists

### still-open primary runtime target
- `WorldProceduralScatterDirector.cs`
- Honest status:
  - even after the wider code cleanup, the last validated live traces still point to scatter warmup/rebuild as the main remaining runtime cost center
  - project-wide cleanup reduces background garbage and repeated search debt, but it does not replace the need to keep pushing directly on scatter cost

## Reviewed and intentionally not changed in this pass

### `HectonRockManager.cs`
- What I found:
  - `FindAnyObjectByType<GPUInstancerPrefabManager>()`
  - `FindAnyObjectByType<ProximityColliderSystem>()`
- Why I did not change it now:
  - both resolves happen in startup/init, not inside the slow tick body
  - this is not the source of the validated runtime megabyte spikes
  - changing GPU Instancer integration blindly is higher risk than the gain from this one-shot search

### `HectonPlayerSpawner.cs`
- What I found:
  - `GameObject.FindWithTag("Player")` in `Awake`
- Why I did not change it now:
  - this is bootstrap/spawn-time logic
  - replacing it with bootstrap state could create circular dependency timing issues during player creation itself

### `MapMagicBridge.cs`
- What I found:
  - `GameObject.FindWithTag("Player")` in startup resolve
  - `Resources.FindObjectsOfTypeAll<MapMagicObject>()` to locate the MapMagic runtime object
- Why I did not change it now:
  - both paths are bootstrap/integration-time, not active per-frame gameplay loops
  - the `MapMagicObject` resolve is special-case third-party integration and should not be rewritten casually

### `RuntimePerformanceProfiler.cs`
- What I found:
  - `FindObjectsByType<Renderer>()` ownership audit path
- Why I did not change it now:
  - this is explicit diagnostic work gated behind profiler conditions
  - it can absolutely add cost when enabled, but it is not the same class of gameplay-runtime bug as the validated scatter warmup spikes

### `BeaconNetworkSystem.cs`
- What I found:
  - `FindFirstObjectByType<BeaconNetworkSystem>()` inside `GetOrCreate()`
- Why I did not change it now:
  - this is singleton bootstrap/creation logic, not steady-state gameplay work
  - current behavior is correct and low-risk compared to touching active beacon update logic

### `WorldProceduralFieldSampler.cs`
- Why it mattered:
  - runtime anchor refresh used `Resources.FindObjectsOfTypeAll<WorldZoneAnchor>()`
  - sampler is in the world selection path, so even infrequent scene scans are bad here
- What was changed:
  - sampler now copies active anchors from `WorldZoneAnchor` registry
- Why this is better:
  - removes scene-wide object search from normal runtime anchor resolution

### `WorldZoneDirector.cs`
- Why it mattered:
  - `RefreshAnchors()` used `Resources.FindObjectsOfTypeAll<WorldZoneAnchor>()`
  - this director runs as `ISlowTickable`, so the search path lived in an active system
- What was changed:
  - replaced scan with registry copy
- Why this is better:
  - no scene array build for normal zone refreshes

### `WorldInterestDirector.cs`
- Why it mattered:
  - same problem as zone director, but for `WorldInterestAnchor`
- What was changed:
  - replaced runtime scan with registry copy

### `WorldSliceDirector.cs`
- Why it mattered:
  - same problem again, for `WorldSliceAnchor`
- What was changed:
  - replaced runtime scan with registry copy

### `WorldContentDirector.cs`
- Why it mattered:
  - used `Resources.FindObjectsOfTypeAll<WorldContentSocket>()` in an active world director
  - content sockets are part of the same world evaluation stack as zone/population/procedural context
- What was changed:
  - added `WorldContentSocket` registry
  - director now copies active sockets instead of scanning the whole scene

### `WorldRuntimeReferenceUtility.cs`
- Why it matters:
  - many active world systems now resolve player and service references through this one helper
  - if fallback logic here is expensive, the same cost gets smeared across multiple directors
- What was changed:
  - added successful-resolution caches for player transform, generic scene objects, `MapMagicBridge`, and `ScavengePopulator`
  - bootstrap player transform remains the primary source of truth; cache is only a fallback accelerator
- Why this is better:
  - cuts repeated `FindAnyObjectByType` / `GameObject.Find*` retries across the world stack after the first successful bind
  - keeps world systems aligned on one cheap resolve path instead of many slightly different ones

### `FieldTargetDescriptor.cs` + `FieldTargetSemantics.cs`
- Why it matters:
  - `FieldTargetSemantics.TryFindNearestRouteMarker(...)` used `FindObjectsByType<FieldTargetDescriptor>()`
  - that helper is used by active tools and authored target assessment, not just editor/debug code
- What was changed:
  - added active descriptor registry on `FieldTargetDescriptor`
  - nearest-route lookup now iterates the registry instead of rebuilding a scene array
- Why this is better:
  - removes scene-wide descriptor search from live authored target reads
  - keeps tool-side semantic lookups cheap even as authored target count grows

### `SkySystemFollowCamera.cs`
- Why it matters:
  - this script runs in `LateUpdate`
  - old target resolution could fall back to `Camera.main` and then `FindObjectsByType<Camera>()` repeatedly if no direct reference was assigned
- What was changed:
  - added cached resolved camera reuse
  - full camera scan now happens only when there is no valid cached or assigned target
- Why this is better:
  - removes avoidable per-frame camera search churn from an always-on follow helper

### `Fabricator.cs` + `Gameplay/PDAExchangeSystem.cs` + `UI/PDADataLogTab.cs` + `UI/PDABarterTab.cs`
- Why they matter:
  - these are active gameplay/UI systems, not editor tools
  - they still had service discovery via `FindFirstObjectByType<...>()` for scan/exchange/discovery systems
- What was changed:
  - swapped service auto-resolve to existing singleton `Instance` paths
  - kept bootstrap player hierarchy resolve as the first option where player-linked references are needed
- Why this is better:
  - removes unnecessary scene searches from PDA/fabrication flows
  - keeps runtime service binding aligned with the project's existing singleton architecture

### `WorldFaunaSpawnRegistry.cs` + `WorldGenerativeGeologyVoxelBridgeDirector.cs` + `WorldGenerativeGeologyTerrainSeamApplier.cs` + `WorldProceduralScatterDirector.cs`
- Why they matter:
  - these are active world/geology systems that previously still bypassed the shared runtime helper and called `FindAnyObjectByType` directly
- What was changed:
  - routed reference resolution through `WorldRuntimeReferenceUtility.TryResolveSceneObject(...)`
  - these systems now benefit from the shared successful-resolution cache
- Why this is better:
  - removes duplicated ad-hoc search code
  - makes repeated reference binding in the world stack cheaper and more predictable

### `WorldPopulationDirector.cs`
- Why it mattered:
  - rule selection was building blended diagnostic strings inside the candidate loop
  - this director runs in active runtime and evaluates many sockets against many rules
  - the old version could build strings for losing candidates, then throw them away
- What was changed:
  - selection loop now tracks only the best candidate metadata
  - blended/diagnostic strings are built once, after the winner is known
- Why this is better:
  - removes unnecessary string work from the inner rule-selection loop
- keeps gameplay result the same while reducing avoidable diagnostic churn

### `HectonBaseAI.cs`
- Why it mattered:
  - base AI refreshes player stimulus every tick
  - if the player flashlight was not found immediately, the old path could keep retrying `GetComponentInChildren<PlayerFlashlight>()` from live AI ticks
  - that is hierarchy traversal inside an always-on gameplay brain, which is the wrong place to keep probing
- What was changed:
  - added a cooldown retry for player stimulus source resolve
  - immediate resolve is still allowed when the player is first found
  - repeated retries while the flashlight/rigidbody are still unavailable are now throttled instead of happening from every tick
- Why this is better:
  - keeps AI light/noise reactions intact
  - removes pointless repeated hierarchy work from the active AI path while late-bound player components are still coming online

### `WorldProceduralScatterDirector.cs` diagnostics path
- Why it mattered:
  - scatter rebuild profiling was constructing large formatted report strings before calling `RuntimeDiagnosticsTrace.WriteEvent(...)`
  - pool warmup diagnostics inside the hot warmup loop did the same
  - `RuntimeDiagnosticsTrace` drops the write when no session is active, but the interpolated string had already been allocated
- What was changed:
  - scatter rebuild report is now built only when it will actually be used for a trace write or a spike log
  - pool warmup trace lines are now gated by `RuntimeDiagnosticsTrace.IsActive` before building the message
- Why this is better:
  - removes pointless string allocations from the confirmed scatter hot path when diagnostics tracing is off
  - keeps the trace useful when a session is active, without paying for it all the time

### `Visor/SuitHUDPresentationController.cs`
- Why it mattered:
  - this is active HUD orchestration, not editor-only fluff
  - old auto-resolve path scanned live scene objects with `FindObjectsByType<SuitHUDV4CanvasOverlay>()`, `FindFirstObjectByType<SuitHUDScreenCompositor>()`, and `FindObjectsByType<Canvas>()`
  - because it retries while references are missing, that search debt could keep waking up in play mode
- What was changed:
  - switched overlay/compositor discovery to active registries
  - resolution now prefers the same transform root instead of global scene scans
  - shared projection texture now reuses `VisorHUDController.SharedRenderTexture` instead of searching textures globally
- Why this is better:
  - removes scene-wide HUD recovery scans from the live visor presentation path

### `Visor/SuitHUDScreenCompositor.cs`
- Why it mattered:
  - old fallback path used `Resources.FindObjectsOfTypeAll<Canvas>()` and `Resources.FindObjectsOfTypeAll<RenderTexture>()`
  - this component is `ExecuteAlways` and also ticks in play mode when dirty/missing references
- What was changed:
  - added active compositor registry
  - canvas resolution now comes from active `SuitHUDV4CanvasOverlay` registry
  - visor controller resolution now prefers hierarchy/root-local controllers and then controller registry
  - render texture now reuses `VisorHUDController.SharedRenderTexture`
- Why this is better:
  - removes the worst global search paths from runtime HUD compositing

### `UI/SuitHUDV4CanvasOverlay.cs`
- Why it mattered:
  - old projection-camera resolve used `FindObjectsByType<Camera>(Include, ...)`
  - this overlay also retried runtime auto-resolve when key references were missing
- What was changed:
  - added active overlay registry
  - projection camera now resolves through active `VisorHUDController` registry, preferring same-root controller
  - player-side systems now try `SceneBootstrap.TryGetCurrentPlayerTransform(...)` before any scene-wide fallback
- Why this is better:
  - removes global camera scans from active HUD canvas recovery
  - makes player-system binding cheaper and more deterministic during runtime bring-up

### `Visor/VisorHUDController.cs`
- Why it mattered:
  - other visor systems had no cheap way to reuse the already-resolved controller and its shared RT/hud camera
- What was changed:
  - added active controller registry
  - exposed read-only accessors for resolved HUD camera and shared render texture
- Why this is better:
  - downstream visor components can now reuse controller state instead of scanning the scene again

### `HectonSuitHUD_v4.cs`
- Why it mattered:
  - core legacy HUD still did lazy `FindFirstObjectByType<...>()` lookups for survival, movement, flashlight and underwater visuals
  - this is active HUD code, not a dead utility
- What was changed:
  - added active HUD registry
  - player-linked systems now resolve through `SceneBootstrap` player transform first, then only fall back if the hierarchy does not provide them
- Why this is better:
  - reduces scene-wide fallback searches in a live HUD path

### `HectonSuitHUDExtensions.cs`
- Why it mattered:
  - companion HUD layer still looked up primary HUD, canvas overlay, flashlight and tool manager through global search
- What was changed:
  - primary HUD now resolves from active HUD registry
  - canvas overlay now resolves from active overlay registry
  - flashlight/tool manager now try player hierarchy via `SceneBootstrap` first
- Why this is better:
  - keeps the whole visor/HUD stack on the same registry/bootstrap model instead of mixing in extra scene scans

## Medium priority - runtime risk only in special mode

### `WorldGenerativeGeologyIntegrationDirector.cs`
- Why it mattered:
  - `includeInactiveBindings` path used `FindObjectsByType<WorldGenerativeGeologyBinding>(Include, ...)`
  - this allocates and scans the scene
- Why this was not as bad as scatter:
  - normal runtime path already used `WorldGenerativeGeologyBinding.CopyActiveBindingsTo(...)`
  - the scene-scan only happened in the opt-in inactive-binding mode
- What was changed:
  - inactive-binding scan is now restricted to non-playing/editor-style usage
  - runtime always uses active binding registry
- Why this is better:
  - production play mode no longer falls back to full binding scene scans

## Low priority - real search, but not hot-path worthy right now

### `MapMagicBridge.cs`
- Code:
  - `Resources.FindObjectsOfTypeAll<MapMagicObject>()`
  - `GameObject.FindWithTag("Player")`
- Why I am not treating this as the current fire:
  - both happen during bootstrap-style reference resolution in `Awake`
  - they are not in a per-frame or per-slow-tick hot path
- Decision:
  - keep for now
  - not a zero-GC hot-path blocker compared to scatter/geology runtime work

### `WorldStateManager.cs`
- Code:
  - `FindObjectsByType<ResourceNode>(FindObjectsInactive.Include, ...)`
- Why I am not treating this as the current fire:
  - method comment and usage show it is a one-shot save/load scene apply step
  - not part of the steady gameplay hot loop
- Decision:
  - keep for now
  - acceptable as load-time work unless a trace later shows otherwise

### `ScavengePopulator.cs`
- Code:
  - `Dictionary`, `Queue`, `StringBuilder`, `List` allocations happen in `Awake`
  - chunk containers allocate on first use per chunk
  - unique id generation still ends with `StringBuilder.ToString()` on actual spawn
- Why I am not treating this as the current fire:
  - current structure is mostly preallocated and reused
  - remaining string allocation is tied to real spawned node ids, not a per-frame loop
  - this is a valid optimization target, but not the strongest confirmed source from recent traces
- Decision:
  - keep under observation
  - not promoted above confirmed scatter/geology problems yet

### `FaunaDirector.cs`
- Code:
  - runtime state dictionaries and arrays are lazy-initialized in `EnsureRuntimeStateInitialized()`
  - per-biome type arrays are created once from dataset size
- Why I am not treating this as the current fire:
  - this looks like startup/first-use setup, not steady-state repeated GC
  - main loop itself is mostly list/dictionary reuse with index-based iteration
- Decision:
  - acceptable for now as first-use setup
- worth profiling later if a trace shows first-biome-entry spikes tied to fauna startup

### `AmbientWaterMotionManager.cs`
- Code:
  - observer fallback previously used `GameObject.FindGameObjectWithTag("Player")`
- Why I touched it:
  - this manager is active `ITickable` runtime code and retries observer resolution with cooldown
  - even though it was not the biggest offender, it is easy cheap debt to remove
- What was changed:
  - now prefers `SceneBootstrap.TryGetCurrentPlayerTransform(...)`
- Decision:
  - improved as part of the broader runtime reference cleanup

### `AcousticZoneController.cs`
- Code:
  - player buoyancy fallback previously used `GameObject.FindWithTag("Player")`
- Why I touched it:
  - this controller ticks every frame and retries player buoyancy resolve until ready
  - again, not the biggest trace villain, but active runtime debt
- What was changed:
  - now resolves player transform via `SceneBootstrap` and then `TryGetComponent` on that transform
- Decision:
  - improved as part of the same player-reference cleanup

### `FaunaDirector.cs`
- Code:
  - player fallback used `GameObject.FindWithTag("Player")`
  - spawn/procedural registries used `FindAnyObjectByType`
- Why I touched it:
  - this is a real active fauna runtime system, not editor scaffolding
  - even if the searches are lazy, they live in a gameplay director that may retry until references appear
- What was changed:
  - player resolve now goes through `WorldRuntimeReferenceUtility`
  - fauna registries now use shared scene-object helper instead of raw direct searches
- Decision:
  - improved as part of active runtime reference normalization

### `HectonDirectorAI.cs`
- Code:
  - player lookup used `GameObject.FindWithTag("Player")`
  - director dependencies used direct `FindAnyObjectByType`
- Why I touched it:
  - this is the session pacing director; even “one-time” lazy resolution here belongs on the shared runtime path, not ad-hoc scene scans
- What was changed:
  - player resolve now uses `WorldRuntimeReferenceUtility`
  - fauna/scavenge dependencies now resolve through shared helpers
- Decision:
  - improved as part of the same cleanup

### `HectonFluidEngine.cs`
- Code:
  - one-time observer fallback used `GameObject.FindGameObjectWithTag("Player")`
- Why I touched it:
  - this is active physics infrastructure
  - the search was not the hottest problem, but it was still easy debt on a foundational runtime system
- What was changed:
  - observer fallback now uses bootstrap player transform
- Decision:
  - improved as low-risk runtime cleanup

### `HectonAtmosphereManager.cs`
- Code:
  - `FindObjectsByType<Light>(...)`
- Why I am not treating this as runtime:
  - current usage is under `#if UNITY_EDITOR` `OnValidate`
- Decision:
  - no runtime action needed

## Low/medium priority - runtime fallback path, but not core world bottleneck

### `Visor/SuitHUDScreenCompositor.cs`
- Code:
  - `Resources.FindObjectsOfTypeAll<Canvas>()`
  - `Resources.FindObjectsOfTypeAll<RenderTexture>()`
- Why it matters:
  - if references are missing, fallback auto-resolve can scan repeatedly on retry interval
- Why I did not patch it in this pass:
  - this is UI recovery logic, not the confirmed world-performance bottleneck
  - changing it safely may require project-specific assumptions about canvas uniqueness
- Decision:
  - keep as secondary cleanup target
  - not ignored, just behind scatter/world runtime work

## Registry changes introduced in this audit

These types now self-register and support zero-allocation copy into caller-owned lists:
- `WorldZoneAnchor`
- `WorldInterestAnchor`
- `WorldSliceAnchor`
- `WorldContentSocket`

Why this pattern was chosen:
- avoids scene-wide object enumeration in active directors
- keeps ownership simple and local to the component type
- caller reuses its own `List<T>` and only refreshes contents

## Honest summary

What this audit proved:
- the project was carrying more than one active runtime zero-GC violation
- the earlier large spikes were not caused by one trivial line
- the hottest confirmed problem is still scatter warmup
- beyond that, there was a second layer of garbage/search debt spread across active world directors and geology rebuild plumbing

What is now already improved by code:
- active world anchor/socket discovery is cleaner
- generated geology rebuild path is materially less allocation-heavy
- inactive-binding full-scene scans are no longer allowed in play mode
- visor/HUD runtime recovery no longer depends on full-scene or full-resource scans for its main references
- several active runtime systems now resolve the player through bootstrap instead of repeating tag/name searches
- fauna/director/physics runtime layers also moved closer to shared player/service resolution instead of isolated ad-hoc scene lookups
- pooled and gameplay-bound tools no longer grab the player via tag search on spawn/use paths:
  - `BuilderTool`
  - `LaserCutter`
- player-side shared systems now resolve survival through bootstrap instead of scene search:
  - `PlayerTool`
  - `PlayerFlashlight`
  - `PlayerPDA`
- active AI/runtime systems no longer use direct player tag lookup on their lazy resolve paths:
  - `HectonBaseAI`
  - `HectonBoidController`
  - `ScavengePopulator`
  - `ProximityColliderSystem`
- visor/HUD fallback paths were tightened further:
  - `HectonSuitHUD_v4`
  - `HectonSuitHUDExtensions`
  - `UI/SuitHUDV4CanvasOverlay`
  - bootstrap player hierarchy is now the only normal runtime recovery path for these links
- PDA/UI runtime tabs now resolve player-linked dependencies through bootstrap hierarchy instead of scene-wide searches:
  - `HUDQuickBar`
  - `ToolLoadoutProvisioner`
  - `PDAInventoryTab`
  - `UI/PDALoadoutTab`
  - `UI/PDAConstructionTab`
  - `UI/PDADataLogTab`
  - `UI/PDAShellChrome`
- `HUDNotification` now exposes an active-instance registry, and direct runtime callers were moved off global scene scans:
  - `EnvironmentalAnalyzerTool`
  - `FlashlightTool`
  - `Gameplay/PDAExchangeSystem`
  - `PlayerBuilder`
  - `ScanLogSystem`
  - `ToolHitUtility`
  - `UI/SuitAdvisoryController`
  - `Interaction/SaveStation`
- more UI runtime overlays/menus now resolve player-linked references through bootstrap instead of scene search:
  - `UI/PDABarterTab`
  - `UI/BuilderStatusOverlay`
  - `UI/PauseMenuController`
- manual runtime review also found and cleaned smaller but real hot-path waste in active always-on systems:
  - `AmbientWaterMotionManager`
    - LOD distance squares are now cached instead of being recomputed every tick
  - `HectonFluidEngine`
    - fluid LOD observer no longer risks staying unresolved forever after startup
    - it now retries on a cooldown, so the system can recover its LOD center after late player/camera spawn instead of simulating everything as near-detail forever
  - `AcousticZoneController`
    - player buoyancy lookup now has a retry cooldown instead of probing bootstrap every frame until the player arrives
  - `WorldPopulationDirector`
    - full blended diagnostic strings are no longer built for every socket in the world on each slow tick
    - full verbose reasoning is kept only for the active nearest socket, while background sockets keep the lightweight summary that gameplay actually needs

What still needs live proof:
- the latest scatter warmup throttling
- the practical effect of these registry/rebuild cleanups on real play traces

## Remaining non-editor search debt after this pass

This is the honest remainder from a repo-wide non-editor grep, grouped by priority.

### Still active gameplay/UI fallback debt
- `UI/PDABarterTab.cs`
- still uses scene search for `PDAExchangeSystem`
- player PDA resolve is now bootstrap-based
- `UI/PauseMenuController.cs`
- player PDA resolve is now bootstrap-based
- remaining search here is font discovery through `Resources.FindObjectsOfTypeAll<TMP_FontAsset>()`
- `UI/PDADataLogTab.cs`
  - global service dependencies still use scene search:
    `ScanLogSystem`, `FieldOperationLogSystem`, `PDAExchangeSystem`,
    `BeaconNetworkSystem`, `HectonDiscoveryManager`
  - this is safer than touching player hierarchy blindly, but it is still remaining runtime debt
- `Gameplay/PDAExchangeSystem.cs`
  - still uses scene search for `ScanLogSystem`
  - player inventory and HUD notification now use safer runtime paths
- `UI/BuilderStatusOverlay.cs`
  - player-linked references are now bootstrap-based
  - remaining singleton/service fallback is `ConstructionManager.Instance`, which is acceptable

### Mostly startup/bootstrap/service paths
- `SceneBootstrap.cs`
  - still owns some explicit player/tag searches
  - acceptable as bootstrap debt, not active gameplay debt
- `MapMagicBridge.cs`
  - still has player/tag search and `Resources.FindObjectsOfTypeAll<MapMagicObject>()`
  - important, but mostly startup/integration territory rather than steady-state hot path
- `HectonPlayerSpawner.cs`
  - still uses player tag lookup in spawn/bootstrap logic

### Runtime diagnostics or singleton bootstraps
- `RuntimePerformanceProfiler.cs`
  - still uses `FindObjectsByType<Renderer>(...)` for audit pass
  - not hidden anymore, but still intentionally expensive when enabled
- `BeaconNetworkSystem.cs`, `Input/InputManager.cs`, `Input/RebindingManager.cs`, `FlowFieldVisualizer.cs`
  - these are singleton/bootstrap style lookups, not primary steady-state GC offenders

## FaunaDirector throttling

`FaunaDirector` was doing two things every `SlowTick` that are safe to damp:
- recomputing streaming-derived runtime settings even when no inputs changed
- probing for a late-bound player every tick when the player was still spawning

Changes:
- runtime streaming settings are now refreshed on a short interval or when marked dirty
- player resolve retries are throttled by a short cooldown
- prefab-to-type index is cached per biome, removing repeated linear scans on every spawn
