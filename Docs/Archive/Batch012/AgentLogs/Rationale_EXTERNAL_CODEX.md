# Rationale_EXTERNAL_CODEX

## 2026-05-23 External Identity Reset
Problem: Previous context carried SHINOBU_361 texture-production identity, while user stated this agent is external and demanded concrete fixes.
Solution: Created independent EXTERNAL_CODEX task/rationale files and narrowed work to active core code.
Rejected Alternatives: Continuing texture queue audit would produce reports without fixing runtime code.
Scalability potential: Independent core fixes can improve all quality tiers without coupling to texture production.
Hardware Impact: No runtime cost; reduces integration drift.

## 2026-05-23 Compile-Wall Triage
Problem: Earlier compile failure referenced `Hecton8.Habitat` from construction files.
Solution: Verified source namespace and asmdef contract exist; evidence points to generated `.csproj/.slnx` graph missing the contract project, not absent gameplay code.
Rejected Alternatives: Rewriting construction or habitat namespaces would create source churn and likely break Unity asmdef ownership.
Scalability potential: Build graph hygiene affects all devices equally; no runtime path.
Hardware Impact: No frame cost; prevents wasted compile cycles.

## 2026-05-23 Generated Project Reference Pruner Fix
Problem: `HectonGeneratedProjectReferencePruner` removed every missing `Library/ScriptAssemblies` reference. That can erase valid local asmdef references before Unity has produced their DLLs, including `Hecton8.Habitat.Deformation.Contracts`, causing generated project builds to report missing namespaces while source and asmdef contracts are present.
Solution: Limited pruning to known stale refs and missing `Library/PackageCache` paths. Added an EditMode regression test that preserves a missing local `Library/ScriptAssemblies/Hecton8.Habitat.Deformation.Contracts.dll` reference while still removing stale `Unity.Entities` package-cache reference.
Rejected Alternatives: Editing generated `.csproj` files would be overwritten; moving contract DTOs into Core would violate domain ownership; changing construction code to avoid the contract would hide the broken generator filter.
Scalability potential: Low/Middle/High/Ultra all benefit through deterministic project generation; no quality-tier split, no runtime behavior change.
Hardware Impact: Runtime impact 0 us. Editor regeneration keeps valid local asmdef references, avoiding repeated failed compiles on low-end i3/MX350-class machines.

## 2026-05-23 Shader Compass Hot-Swap Binding Fix
Problem: `ShaderCompassRibbon` cached `GlobalRegistry.InertialNavigation` only in `OnEnable`/`Start`. If the UI enabled before `DiegeticGyroCompassRuntime` registered the service, the ribbon stayed hidden until component restart.
Solution: Implemented `IGlobalRegistryHotSwapListener`; navigation cache updates only on `InertialNavigationRuntime` replacement, and dispatcher replacement retries LateFrame registration.
Rejected Alternatives: Per-frame `GlobalRegistry.InertialNavigation` polling would violate cached-interface consumer discipline and add hot-path global reads; scene search was rejected outright.
Scalability potential: Low/Middle/High/Ultra all keep the compass responsive to service boot order; no quality binary switch.
Hardware Impact: Runtime hot path remains 0 allocation. Saved work is indirect: no repeated user/UI recovery path; listener callback cost only on registry rebind, estimated <5 us per rebind.

## 2026-05-23 Compile Verification Wall
Problem: `dotnet build Hecton8.Editor.csproj --no-restore` first stopped on missing Habitat contract namespace from stale generated `Hecton8.Core.csproj`.
Solution: Kept tracked source fix in `HectonGeneratedProjectReferencePruner`; additionally patched the ignored local generated `Hecton8.Core.csproj` to include Habitat deformation contract sources so the verifier could move past that wall before Unity regenerates projects.
Rejected Alternatives: Changing construction source to avoid Habitat contracts would hide the generated-graph fault; editing tracked `Hecton8.slnx` without a generated contract project would not fix `Hecton8.Core.csproj`.
Scalability potential: Build-only; no runtime tier behavior.
Hardware Impact: Runtime impact 0 us. Editor compile progressed past the Habitat error; the next 290 errors are from unrelated incomplete partials owned by other tasks.

## 2026-05-23 Source Compile Repair Sweep
Problem: After the generated graph wall was bypassed, the build exposed concrete C# faults across runtime/editor files: readonly NativeArray write/read misuse, illegal unsafe field address operations, missing imports/constants, mutable vault buffers passed as `in`, ambiguous Burst `math.select`, nested job visibility, definite assignment, and one editor namespace collision.
Solution: Applied narrow source fixes in the owning files: copied NativeArray values before `in` calls, used `UnsafeUtility.AddressOf(ref value)` helpers for atomic pointer paths, passed mutable buffers by value only where a write is required, added the required narrative namespace import, referenced the existing metabolism fatigue flag, exposed the mock seismic job as `internal`, initialized camera-juice arrays before acquisition chains, and explicitly used `System.Environment.TickCount`.
Rejected Alternatives: Broad API redesign, public DTO layout changes, moving generated/other-agent partials into different assemblies, or replacing unsafe atomic routes with managed wrappers were rejected because they would alter runtime contracts and risk GC/hot-path churn.
Scalability potential: Low devices gain from having the existing Burst/native routes compile instead of falling back to managed/editor-only workarounds; middle/high/ultra tiers keep the same data-local runtime lanes and can spend frame budget on visuals rather than integration failure recovery.
Hardware Impact: Intended runtime delta 0 us for most fixes. The Airlock unsafe-address repair preserves native atomic writes instead of managed proxy allocation; estimated avoided hot-path allocation pressure is 0.5-2.0 us per contested flush on i3/MX350-class hardware. CameraJuice definite-assignment repair has 0 us runtime effect.

## 2026-05-23 Final Build Verification
Problem: Previous status was blocked by dependency because the build stopped before all downstream errors were visible.
Solution: Ran guarded iterative builds, fixed each newly exposed compile wall, and stopped after `Hecton8.Editor.csproj` built locally with 0 errors. Final log: `Docs/AgentLogs/Build_EXTERNAL_CODEX_after_patch5.log`.
Rejected Alternatives: Stopping at the first downstream wall, claiming external blockage after errors were visible and fixable, or launching concurrent builds against active compiler servers.
Scalability potential: Build verification is quality-tier neutral; it protects all runtime tiers by keeping source contracts compilable.
Hardware Impact: Runtime impact 0 us. Build-server shutdown prevents compiler-server contention after the pass; estimated editor-machine saving is 500,000+ us on subsequent low-end build attempts.

## 2026-05-23 Warning Cleanup And Death Event Repair
Problem: The first green build still emitted warnings: obsolete `BallastLiftN` editor writes, dead flashlight flicker locals, declared `OnDeath` events that were never raised, and duplicate source includes in the local generated Core project.
Solution: Removed the obsolete ballast slider/write from `SubmarineDynoTunerWindow`, deleted dead flashlight flicker state, raised `HectonPlayerHealth.OnDeath` before respawn reconciliation, raised `HectonSurvivalSystem.OnDeath` on lethal survival failure, preserved the latest survival death record across respawn, and removed duplicate local generated `Hecton8.Core.csproj` includes.
Rejected Alternatives: Warning suppression, keeping the obsolete ballast UI as a harmless editor knob, or clearing death telemetry during respawn were rejected because they hide defects from runtime observers and tools.
Scalability potential: Low/Middle/High/Ultra all receive the same event route. No gameplay truth changes with quality weight; UI/editor cleanup has no runtime tier split.
Hardware Impact: Death event publication is one nullable delegate call on death only; runtime frame impact 0 us. Removing dead flashlight state saves 0 us measurable hot-path time but removes misleading code. Duplicate include cleanup saves build-time only, estimated 200,000-600,000 us of warning triage per local build.

## 2026-05-23 Final Zero-Warning Build Verification
Problem: The post-fix build initially needed `project.assets.json` restored and then showed residual `CS2002` duplicate-source warnings.
Solution: Ran `dotnet restore Hecton8.Editor.csproj`, shut down compiler servers, rebuilt under the guard, removed duplicate generated project includes, and rebuilt again. Final log: `Docs/AgentLogs/Build_EXTERNAL_CODEX_after_duplicate_include_cleanup.log`.
Rejected Alternatives: Building without restore after `project.assets.json` disappeared, building while Unity Roslyn compiler server was active, or adding `CS2002` to `NoWarn`.
Scalability potential: Build-only. Runtime behavior remains deterministic across quality tiers.
Hardware Impact: Runtime impact 0 us. Build machine impact: final clean build avoids three repeated csc duplicate-source warnings per pass.

## 2026-05-23 Runtime Scene Search Cleanup
Problem: Runtime files still contained scene scans outside editor-only tooling. `DynamicMusicGranularSynthesizer` searched for an existing component before creating its own host, and `DcsAscentProfileOverlay` searched the scene for `ShinobuPhysiologyRuntime`.
Solution: Removed the dynamic music search; active scene instances already set `_activeInstance` in `OnEnable`, and the fallback creates a deterministic audio host. Added `ShinobuPhysiologyRuntime.TryGetActive()` and made the dev overlay resolve the owner through that cold published pointer.
Rejected Alternatives: Leaving `FindObjectOfType` in runtime paths, adding a broad GlobalRegistry slot for a development overlay, or replacing the vocal-bank `AudioListener` bootstrap without proving audio filter graph behavior.
Scalability potential: Low/Middle/High/Ultra all avoid scene scans on these routes; quality remains continuous and does not change gameplay truth.
Hardware Impact: Dynamic music bootstrap saves one scene scan on startup, estimated 100-800 us depending on scene object count. Physiology dev overlay avoids one scan on enable and any future rebind path, estimated 50-400 us in development builds.

## 2026-05-23 Water Optics Camera Binding Cleanup
Problem: `WaterOpticsRuntime` used `Camera.main` during `Awake`/`OnEnable` as its camera fallback. Even when cold, this is a scene-tag lookup and can bind the wrong camera if player context arrives later.
Solution: Cached `IPlayerRuntimeContext` from `GlobalRegistry.Player`, listened for `GlobalRegistryServiceSlot.Player` hot swaps, and resolved the water-optics camera from the player context. Inspector-assigned cameras remain authoritative; runtime-resolved cameras are cleared on shutdown.
Rejected Alternatives: Keeping `Camera.main` as a fallback, adding per-frame GlobalRegistry polling, or adding a new registry slot for a single visual consumer. If no player camera exists, surface offset now resolves to deterministic 0 instead of searching the scene.
Scalability potential: Low/Middle/High/Ultra all use the same ownership route; quality weight can still scale water optics fidelity without changing camera truth ownership.
Hardware Impact: Avoids one or two cold scene-tag lookups per runtime enable, estimated 20-150 us depending on scene tag/object state. Per-frame cost remains one cached interface property read only while water optics needs camera-relative surface Y.

## 2026-05-23 Vocal Bank Listener Bootstrap Cleanup
Problem: `VocalBankPlaybackRuntime.EnsureRuntimeInstanceAfterSceneLoad()` scanned the scene for both an existing runtime and any `AudioListener`. This violated the registry/cached-owner route even though the listener mix mode itself is intentional.
Solution: Removed scene scans. The bootstrap now uses `_activeInstance` for already-enabled runtimes and resolves the listener through `GlobalRegistry.Player.PlayerCamera` or `GlobalRegistry.PlayerSensory.PlayerCamera`, then uses local `TryGetComponent` on that camera object before adding the runtime.
Rejected Alternatives: A new AudioSource driver host was rejected because SHINOBU_260 documentation says listener fallback protects the project mix and avoids a dedicated driver object; a raw scene listener scan was rejected because player/sensory context already owns the camera route.
Scalability potential: Low/Middle/High/Ultra all keep the same vocal ownership path. Quality remains controlled by continuous vocal quality scalar and does not alter cue identity or payload layout.
Hardware Impact: Avoids up to two scene object scans during vocal bootstrap, estimated 150-1000 us on content-heavy scenes. Audio callback cost is unchanged; no allocation was added to `OnAudioFilterRead`.

## 2026-05-23 Parasite Swarm Player Hot-Swap And Job Fence Cleanup
Problem: `ParasiteSwarmGpuRuntime` cached `GlobalRegistry.Player` only in `OnEnable`. If player context/camera registered later, the visual owner could stay blind. Its completed target-selection job also used a direct `Complete()` in the late-frame path.
Solution: Added `IGlobalRegistryHotSwapListener` handling for `GlobalRegistryServiceSlot.Player`, cached player context only on cold/hot-swap events, preserved inspector camera overrides, and routed completed/teardown job finalization through `DispatcherJobFence`.
Rejected Alternatives: Polling `GlobalRegistry.Player` inside `LateFrameTick`, changing the GPU swarm target selection jobs, or force-completing unfinished target extraction in the frame path.
Scalability potential: Low/Middle/High/Ultra keep the same continuous particle budget and quality-weight math; this only fixes ownership and completion hygiene.
Hardware Impact: Avoids a broken late-bind visual path without adding per-frame registry reads. Hot-frame cost remains existing cached interface reads. Dispatcher fence change has 0 us steady-state impact and centralizes completion proof.

## 2026-05-23 Surface Weather Player Context Rebind Cleanup
Problem: `HectonSurfaceWeatherDirector` re-read `GlobalRegistry.Player` inside its dependency resolution path, which runs from slow tick and lifecycle recovery. Late player registration was handled by polling instead of the first-party hot-swap route.
Solution: Implemented `IGlobalRegistryHotSwapListener`, cached `IPlayerRuntimeContext` in lifecycle/hot-swap callbacks, and made player movement, buoyancy, visor, and flashlight resolution consume the cached context. The existing bootstrap transform fallback remains for degraded startup.
Rejected Alternatives: Polling `GlobalRegistry.Player` in every slow dependency pass, removing bootstrap transform fallback, or forcing player sensory dependencies through new registry slots.
Scalability potential: Low/Middle/High/Ultra keep the same weather profile and local-rain exposure math; this only stabilizes ownership and late-bind behavior.
Hardware Impact: Removes a slow-tick player registry read and branch chain, estimated 1-5 us per dependency recovery pass. Frame hot path remains unchanged.

## 2026-05-23 Atmosphere Manager Player And Celestial Hot-Swap Cleanup
Problem: `HectonAtmosphereManager` read `GlobalRegistry.CelestialEngine` during giant-abyss light publication and read `GlobalRegistry.Player` while caching player camera state. Those owners already have registry service slots.
Solution: Implemented `IGlobalRegistryHotSwapListener`, cached player and celestial runtime references from lifecycle/hot-swap callbacks, routed giant-abyss light through the cached celestial owner, and kept explicit `_playerTransform` overrides intact before falling back to player-context camera.
Rejected Alternatives: Per-slow-tick registry polling, forcing authored player transform overrides to be replaced by player runtime context, or deleting the Aegir ring cookie fallback.
Scalability potential: Low/Middle/High/Ultra keep the same atmosphere, underwater detection, and giant-abyss light equations; quality truth and DTO layout are unchanged.
Hardware Impact: Removes one visual publication registry lookup for celestial state and one player camera registry lookup during cache refresh; estimated 1-8 us per affected slow-cycle pass. Frame hot path remains unchanged.

## 2026-05-23 Hostile Flora And Celestial Service Rebind Cleanup
Problem: `HostileFlora` resolved `GlobalRegistry.Player` in every slow tick and resolved audio through `GlobalRegistry.Audio` on each shot. `HectonCelestialEngine` also cached DataVault, biome, weather, GI, underwater, random-event, dynamic-resolution, world-seed, and player owners only during lifecycle, so late service registration or replacement could leave stale/null cached runtime state.
Solution: Added hot-swap listeners to both systems. Hostile flora now uses cold cached player/audio services plus Player/Audio/Dispatcher rebinding. Celestial runtime now updates each cached owner through the matching `GlobalRegistryServiceSlot`; DataVault replacement refreshes generation handles and marks atmosphere gradient samples dirty.
Rejected Alternatives: Slow-tick registry polling, adding new singleton routes, changing celestial snapshot DTO layout, changing orbit job scheduling, or using scene search to recover missing player targets were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical ballistic, celestial, atmosphere, and quality-weight behavior; this only repairs ownership routes and late-bind stability.
Hardware Impact: Hostile flora saves one player registry read per active plant slow tick and one audio registry read per shot, estimated 1-4 us per dense flora slow pass. Celestial service replacement cost moves to rare registry events; steady-frame impact remains 0 us.

## 2026-05-23 Drone Fleet Static Owner Rebind Cleanup
Problem: `DroneFleetManager` initialized static construction/player/submarine/fluid caches once, then the headless simulation consumed those cached owners for task scoring, player position, formation anchor, and flow sampling. Service replacement after initialization could leave the drone fleet reading stale owners.
Solution: Made the existing headless driver implement `IGlobalRegistryHotSwapListener`, registered it with the registry alongside the updatable/late/render lanes, and updated only the four existing static owner caches from service-slot callbacks.
Rejected Alternatives: Re-reading `GlobalRegistry` inside `ScheduleHeadlessSimulation`, adding direct dependencies to construction/player objects, or clearing all native drone state on every service replacement were rejected.
Scalability potential: Low/Middle/High/Ultra keep the same drone capacity, pathfinding, render distance, and continuous quality tuning; this only repairs owner identity.
Hardware Impact: Avoids per-tick registry refresh in the drone simulation. Steady-state cost is 0 us; hot-swap callback cost is rare and estimated <10 us.

## 2026-05-23 Ocean Surface Player And Vault Rebind Cleanup
Problem: `ShinobuOceanSurfaceAtmosphereRuntime` computed wave LOD/phase through `ResolveCameraAupDouble()`, which read `GlobalRegistry.Player` during runtime publishing/readback paths. Its slow tick could also retry `GlobalRegistry.DataVault` when the vault was missing during enable.
Solution: Implemented `IGlobalRegistryHotSwapListener`, cached player context and DataVault during enable and service replacement, converted camera AUP resolution to consume the cached player context, and reset wave/vault handles only on DataVault replacement.
Rejected Alternatives: Leaving registry retry fallback in slow tick, adding scene camera fallback, changing wave DTO ownership, or forcing a rebuild of ocean wave buffers every frame were rejected.
Scalability potential: Low/Middle/High/Ultra keep the same wave count, readback budget, weather, and continuous quality-weight cadence; this only fixes owner routing.
Hardware Impact: Removes player registry lookup from wave shader publish/readback paths and removes slow-tick DataVault polling while unavailable; estimated 2-6 us per ocean slow/update pass on low-end CPUs. Hot-swap cost is rare.

## 2026-05-23 Voxel Player Runtime Context Route Cleanup
Problem: `HectonVoxelEngine` still read `GlobalRegistry.Player` inside static predictive voxel proxy and player-distance collider logic, while the same file already uses `PlayerRuntimeContextService.TryGetActiveRuntimeContext` as the pure read-only active player route.
Solution: Replaced both direct registry player reads with the active runtime context route and kept existing bootstrap fallback only in the player AUP path.
Rejected Alternatives: Adding a new static player cache, adding hot-swap listener state to static voxel helpers, or using scene search were rejected.
Scalability potential: Low/Middle/High/Ultra keep the same predictive proxy/collider math and voxel LOD behavior; only the context lookup route changed.
Hardware Impact: Removes two direct registry reads from voxel runtime helper paths, estimated 1-3 us when those helpers run. First compile attempt exposed a local leftover variable and was fixed before accepting the loop.

## 2026-05-23 Player Expression Late Binding Cleanup
Problem: `PlayerExpressionManager.AutoResolveReferences()` read `GlobalRegistry.Player` when resolving tool and movement owners, and save registration only ran against the SaveRuntime present during `OnEnable`. Late Player or SaveRuntime registration could leave profile bindings incomplete.
Solution: Implemented `IGlobalRegistryHotSwapListener`, cached `IPlayerRuntimeContext`, routed tool/movement resolution through cached context plus existing bootstrap transform fallback, and registered/unregistered save ownership on Save service replacement.
Rejected Alternatives: Keeping registry reads in every resolve call, adding scene search, or changing player expression save DTO/state layout were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical expression profile, suit, loadout, HUD override, and save identity behavior; only owner binding is stabilized.
Hardware Impact: Removes repeated Player registry property-chain reads from expression reference resolution. Runtime frame impact is 0 us; late-bind recovery callback cost is rare and estimated <10 us.

## 2026-05-23 PDA Loadout Service Rebind Cleanup
Problem: `PDALoadoutTab` used direct registry fallbacks for player inventory/expression ownership during summary/action refreshes. If Player or PlayerExpression service was replaced after enable, cached UI references could remain stale and runtime paths could re-query registry on null.
Solution: Added `IGlobalRegistryHotSwapListener`, seeded player/inventory/expression caches only from cold lifecycle, cleared references that came from the previous player context, and converted expression summary/action helpers to consume the cached service only.
Rejected Alternatives: Polling `GlobalRegistry.PlayerExpression` in every summary helper, clearing all serialized UI references on every player replacement, or adding a new PDA-specific global route were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical PDA loadout, preset, and suit identity behavior; quality scaling is unchanged and no DTO/save identity route changed.
Hardware Impact: Removes repeated expression registry reads from PDA refresh/action paths and prevents stale player-owned references after service replacement. Estimated low-end saving is 1-4 us per loadout refresh; hot-swap callback cost is rare and <10 us.

## 2026-05-23 Culling Manager Camera Owner Rebind Cleanup
Problem: `CullingManager.ResolveMainCamera()` read PlayerSensory and Player from `GlobalRegistry` after enable, so slow culling recovery could poll registry and could miss runtime camera replacement until the cached camera was manually cleared.
Solution: Added `IGlobalRegistryHotSwapListener`, cached `IPlayerRuntimeContext` and `IPlayerSensoryService` during enable, invalidated only the camera/layer-cull binding on Player or PlayerSensory replacement, and retried slow-tick registration when Dispatcher appears.
Rejected Alternatives: Polling registry inside every slow culling pass, forcing a new camera service route, or altering culling distance/frustum math were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical culling thresholds and layer distances. Quality scaling can still adjust content budgets outside this manager; camera truth ownership is not tier-dependent.
Hardware Impact: Removes PlayerSensory/Player registry reads from culling camera recovery, estimated 1-3 us when slow tick resolves or re-resolves the camera on low-end CPUs. Hot-swap callback cost is rare and below 10 us.

## 2026-05-23 HectonItem Static Service Cache Rebind Cleanup
Problem: `HectonItem` used shared static caches for player context, inventory, physics, and object pool services. Those caches were seeded by lifecycle methods but did not update when a service was replaced, so pooled pickup interactions and AUP conversion could keep stale service pointers.
Solution: Added a single class-level `StaticRegistryHotSwapListener` that updates the shared static caches on Player, PlayerInventory, Physics, and ObjectPool service replacement. Lifecycle cold cache seeding remains for bootstrap.
Rejected Alternatives: Registering every pickup instance as a hot-swap listener, polling registry during interaction/preview, or replacing static caches with per-item copies were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical pickup, inventory, buoyancy, and pooling behavior; service identity is stable across all tiers.
Hardware Impact: Prevents stale service pointers without adding per-pickup listener cost. Avoids per-interaction registry polling; estimated saving is 1-2 us per pickup interaction/preview on low-end CPUs, with one rare listener callback per service replacement.

Residual Risk: The ignored local generated `Hecton8.Core.csproj` was patched only to verify the current checkout. Durable graph correctness still depends on Unity/project generation preserving local asmdef script-assembly references, which is covered by the tracked pruner fix and EditMode regression test.
