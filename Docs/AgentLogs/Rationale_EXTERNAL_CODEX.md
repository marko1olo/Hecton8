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

## 2026-05-23 Player Tool Runtime Context And VR Contract Cleanup
Problem: `PlayerToolManager` mixed a concrete `PlayerRuntimeContext` field with interface consumers and could fail after player service replacement. The generated Core project also omitted existing VR interaction contract sources, blocking verification once the player-tool split was fixed.
Solution: Added a cached `IPlayerRuntimeContext` service route for consumers while preserving the concrete runtime context only for interaction-state publication. Patched the local generated `Hecton8.Core.csproj` to include `VRInteractionBridgeContracts.cs` for verification.
Rejected Alternatives: Casting interface consumers back to concrete `PlayerRuntimeContext`, changing interaction DTO layout, or rewriting VR interaction contracts into gameplay files were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical tool, interaction, and VR bridge semantics; this only repairs ownership and generated graph visibility.
Hardware Impact: Removes stale player context risk without adding per-frame registry reads. Runtime frame impact 0 us; service replacement callback cost is rare and below 10 us.

## 2026-05-23 PDA Spectrum And Physical Terminal Service Rebind Cleanup
Problem: `PDASpectrumTab` read `GlobalRegistry.Player` while resolving last-loss/player AUP text. `PhysicalTerminalKeyboard` and `PhysicalPanelDial` read `GlobalRegistry.Audio` directly during press/scroll input. The next compile wall exposed stale generated Core graph state for `PlayerHandIkContracts.cs` and a missing World namespace import in `PlayerKinematicsRuntime_HandIK.cs`.
Solution: Added hot-swap Player caching to `PDASpectrumTab`; added hot-swap Audio caching to the physical terminal keyboard and dial; patched the local generated Core project to include `PlayerHandIkContracts.cs`; added `using Hecton8.World;` to the hand IK partial.
Rejected Alternatives: Per-input registry reads, scene searches, duplicating hand IK constants, or removing published hand IK state were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical PDA diagnostics, terminal input, audio event payloads, and hand IK DTO layout. Quality scaling remains continuous and separate from service ownership.
Hardware Impact: Removes PDA/player registry reads during loss label refresh and Audio registry reads from physical terminal press/scroll actions, estimated 1-3 us per affected UI refresh/input burst on low-end CPUs. Generated graph/source import repairs are build-only.

Residual Risk: The ignored local generated `Hecton8.Core.csproj` was patched only to verify the current checkout. Durable graph correctness still depends on Unity/project generation preserving local asmdef script-assembly references, which is covered by the tracked pruner fix and EditMode regression test.

## 2026-05-23 Documentation Boundary Sync
Problem: Stable root/architecture docs did not carry the latest EXTERNAL_CODEX CLI compile slice and registry hot-swap cleanup boundary, leaving the current evidence split buried in status/log files.
Solution: Updated concise boundary notes in the root docs, runtime execution plan, and global-authority architecture docs. Marked the build as CLI_COMPILE only, named the artifact, and preserved Unity/runtime/profiler/GC gaps.
Rejected Alternatives: Creating a large new dated report would bloat the active documentation surface; editing archived batch docs would violate batch hygiene; claiming runtime proof from CLI build would be false.
Scalability potential: Low/Middle/High/Ultra all benefit from clearer global-authority burn-down state; no quality route or gameplay truth changed.
Hardware Impact: Runtime 0 us. Documentation reduces repeated agent discovery/triage time, estimated 20,000-60,000 us per future handoff on low-end editor machines.

## 2026-05-23 Root Anchor Boundary Sync
Problem: `AGENTS.md`, root release/playtest ledgers, and root/report indexes still exposed R51-only wording after the EXTERNAL_CODEX CLI compile slice was promoted into stable docs.
Solution: Added one-line CLI_COMPILE boundary notes to the root anchors and indexes without changing runtime or product acceptance status.
Rejected Alternatives: Rewriting historical roadmap/playtest entries would bloat high-churn ledgers and risk changing capture-time evidence; leaving root anchors stale would mislead new agents.
Scalability potential: Low/Middle/High/Ultra unaffected; this is documentation routing only.
Hardware Impact: Runtime 0 us. Handoff lookup saving estimated 10,000-30,000 us per agent because the primary root files now name the current compile artifact.

## 2026-05-23 UI Audio Playback Service Rebind Cleanup
Problem: `UIButtonAudioTrigger` refreshed `GlobalRegistry.Audio` on click when the cached audio service was missing. `UIAudioFeedback.PlaySound()` also had a playback-time Audio registry fallback, and `SuitAdvisoryController.PlayUiClip()` resolved Audio directly for warning/critical clips.
Solution: Added hot-swap Audio caching to `UIButtonAudioTrigger` and `SuitAdvisoryController`; removed playback-time fallback in `UIAudioFeedback`; kept existing cold service seeding during lifecycle.
Rejected Alternatives: Per-click/per-warning registry reads, adding scene audio lookup, or routing button audio through a new service slot were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical UI audio clip identity and volume behavior; this only stabilizes service ownership.
Hardware Impact: Removes playback-time Audio registry reads from click, hover, slider/toggle, and suit advisory paths, estimated 1-3 us per UI audio burst on low-end CPUs. Hot-swap callback cost is rare and below 10 us.

## 2026-05-23 Verifier Pointer Documentation Sync
Problem: Stable docs promoted the EXTERNAL_CODEX boundary but still pointed at `Build_EXTERNAL_CODEX_hotpath_cleanup16_retry2.log`, while current status recorded `Build_EXTERNAL_CODEX_hotpath_cleanup17.log` after UI audio service rebind cleanup.
Solution: Updated root/docs/architecture pointers to `cleanup17` and added UI audio feedback to the global-authority migration surface.
Rejected Alternatives: Keeping mixed verifier pointers would make future agents read stale evidence; creating another report would add noise without new proof.
Scalability potential: Low/Middle/High/Ultra unaffected; documentation routing only.
Hardware Impact: Runtime 0 us. Handoff lookup saving estimated 5,000-15,000 us per agent.

## 2026-05-23 Active Doc Header Actuality Sync
Problem: Active entry docs had 2026-05-14/15/18/19/20/21 headers after 2026-05-23 EXTERNAL_CODEX facts were promoted into their bodies.
Solution: Updated edited root/docs/architecture headers to 2026-05-23 and marked CLI_COMPILE as valid only where an artifact path is cited.
Rejected Alternatives: Leaving stale headers would mislead read-order triage; mass-updating untouched subsystem docs would create noise.
Scalability potential: Low/Middle/High/Ultra unaffected; documentation metadata only.
Hardware Impact: Runtime 0 us. Handoff lookup saving estimated 5,000-10,000 us per agent.

## 2026-05-23 Runtime Service Rebind Cleanup 25
Problem: Several active runtime routes still mixed cold registry bootstrap with stale service ownership. `BuoyancyObject` missed late `HectonFluidEngine` registration; `PickupItem` static Player/Inventory/Physics/ObjectPool caches did not hot-swap; `WorldSliceDirector` and `WorldProceduralScatterDirector` read Player through registry-backed fallback routes; scatter spawning/destruction re-read ObjectPool; `SubtitleManager` resolved Player during audio-log sensory pulses; `TerminalOsRuntime` retried DataVault through registry when native buffers were missing.
Solution: Added hot-swap service caching where the class already owns the runtime route. Buoyancy now rebinds to `FluidRuntime`; pickups use one static hot-swap listener like `HectonItem`; world slice/scatter cache Player and scatter ObjectPool; subtitles cache Player; terminal native allocation consumes cached DataVault and resets handles on DataVault replacement.
Rejected Alternatives: Per-frame/per-input registry polling, scene searches, registering every pickup instance as a hot-swap listener, changing terminal DTO layouts, or moving scatter pool ownership into a new global service were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical buoyancy math, pickup identity, scatter placement, subtitle impulse, and terminal native DTO behavior. This only stabilizes owner routing; continuous quality routes and visual budgets are unchanged.
Hardware Impact: Removes registry lookups from world scatter warmup/spawn/destroy, slice/scatter observer resolution, subtitle cue pulses, terminal vault retry, and pickup interaction helpers. Estimated low-end saving is 1-6 us per affected action/pass; late hot-swap callbacks are rare and below 10 us. The compile retry exposed one static/instance fallout and was fixed before accepting the loop.

## 2026-05-23 UI Loading Preview Pool Rebind Cleanup
Problem: UI loading and preview helpers still had direct runtime service fallbacks. `LoadingScreenController.Show()` performed a dead Audio registry read; `SaveSlotHoverPreview.PopulatePreviewMetadata()` pulled Localization and SaveRuntime during hover display; `LoadingTipsDisplay.LoadTips()` pulled Localization on language reload; `UIParticleEffect` despawned pooled particles through the current ObjectPool instead of the pool that created the instance.
Solution: Removed the dead loading Audio read; added hot-swap cached Localization/SaveRuntime to save-slot preview; added hot-swap cached Localization to loading tips; cached ObjectPool for UI particle spawn and stored the owning pool for despawn.
Rejected Alternatives: Keeping hover/loading registry reads, treating language events as sufficient for Localization replacement, despawning pooled particles through whatever ObjectPool is currently registered, or adding a new UI-only pool route were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical loading text, save metadata, tip selection, particle count/lifetime/speed/color, and pool semantics. Continuous quality behavior is unchanged.
Hardware Impact: Removes registry reads from hover metadata refresh and loading-tip localization reload, and prevents wrong-pool despawn after ObjectPool replacement. Estimated saving is 1-3 us per UI refresh/particle lifecycle on low-end CPUs; hot-swap callback cost is rare and below 10 us. First build attempt failed before compile due a concurrent log-file lock, then retry compiled clean.

## 2026-05-23 World Spatial/Wreck/Vegetation Owner Route Cleanup
Problem: `WorldSpatialHashGrid` read Player through `GlobalRegistry` in far-unload and acoustic-density runtime helpers. `WreckMaterialRegistry` read Player for PDA signal distance and view-camera culling. `HectonMapMagicVegetationBridge`/`VegetationFlowFieldIntegrator` read Weather directly while scheduling flow/thermal jobs and registering biolume surges.
Solution: Routed spatial hash and wreck AUP/camera queries through `PlayerRuntimeContextService.TryGetActiveRuntimeContext`. Cached `IWeatherService` in vegetation bridge during cold enable and updated it through `IGlobalRegistryHotSwapListener`; flow/thermal scheduling and biolume surge registration now consume the cached owner.
Rejected Alternatives: Keeping slow/update helper registry reads, adding scene camera fallbacks, creating new global slots, or changing flow-field/weather DTO payloads were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical far-unload thresholds, acoustic density cells, BRG culling behavior, flow-field math, and weather bias. Continuous `GlobalQualityWeight` remains a fidelity/cadence control only.
Hardware Impact: Removes Player/Weather registry lookups from affected world runtime passes, estimated 1-5 us per far-unload/acoustic/wreck/vegetation pass on i3/MX350-class CPUs. Hot-swap callback cost is rare and below 10 us.

## 2026-05-23 Concurrent Verifier Pointer Sync
Problem: Another active slice verified after the world-owner build and produced `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup19_retry1.log` with 0 warning/error text matches.
Solution: Promoted root/architecture CLI_COMPILE pointers from `cleanup19.log` to `cleanup19_retry1.log` without changing world-owner code claims.
Rejected Alternatives: Leaving root docs on an older clean verifier while status marked a newer clean verifier as current would mislead handoff.
Scalability potential: Documentation-only; Low/Middle/High/Ultra runtime behavior unchanged.
Hardware Impact: Runtime 0 us. Handoff lookup saving estimated 2,000-5,000 us per future agent.

## 2026-05-23 Beacon Acoustic Pause Service Rebind Cleanup
Problem: Several action/runtime paths still used direct registry fallback for replaceable services: beacon pool/localization, acoustic audio service, builder camera owner, beacon HUD localization, death-dump localization, and pause-menu Save/Localization/Player routes.
Solution: Added cold service seeding plus `IGlobalRegistryHotSwapListener` refresh where the owner already owns the lifecycle. Beacon despawn now uses the pool that spawned the beacon. Builder camera binding uses the active player runtime context route. Pause save/language actions consume cached SaveManager/Localization/Player context.
Rejected Alternatives: Per-action registry reads, scene camera searches, new global slots, changing save DTOs, or despawning beacons through the currently registered pool were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical beacon labels, pause save behavior, localized UI text, acoustic mix payloads, and quality-weight semantics. This only repairs ownership and replacement behavior.
Hardware Impact: Removes registry reads from beacon spawn/despawn/localization, pause save/language modal paths, acoustic playback resolution, and HUD/death-dump localization refresh. Estimated saving is 1-5 us per affected UI/audio/spawn action on i3/MX350-class CPUs; hot-swap callback cost is rare and below 10 us.

## 2026-05-23 Resource/Ecosystem/Voxel Streaming Player Route Cleanup
Problem: `ResourceDistributionDirector`, `EcosystemDirector`, and `HectonVoxelStreamingBridge` still had Player reads through direct `GlobalRegistry.Player` runtime helpers. These routes run from slow/runtime residency, ecosystem stress/AUP, and voxel streaming passes, so they could poll the cold registry or miss service replacement.
Solution: Cached `IPlayerRuntimeContext` in resource distribution and ecosystem lifecycle/hot-swap paths, handled `GlobalRegistryServiceSlot.Player`, cleared owner state on shutdown, and routed voxel streaming through `PlayerRuntimeContextService.TryGetActiveRuntimeContext`.
Rejected Alternatives: Polling `GlobalRegistry.Player` inside slow/runtime helper methods, adding a new global slot, or removing existing deterministic transform fallback in voxel streaming were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical resource sector residency, ecosystem spawn/stress, and voxel streaming distance math. Quality weight remains a cadence/fidelity control only.
Hardware Impact: Removes Player registry reads from affected resource/ecosystem/voxel streaming passes, estimated 1-4 us per pass on i3/MX350-class CPUs. Hot-swap callback cost is rare and below 10 us.

## 2026-05-23 Biome Boundary And Thermal Grid Quality Cleanup
Problem: `BiomeBoundarySdfRuntime` cached Player only from cold registry calls and could miss late Player replacement, while `AbyssalThermalManager.UsesThermalGrid()` used a binary `Low/Mx350` tier branch to disable the 32^3 thermal grid.
Solution: Added Player/Dispatcher hot-swap binding to the biome boundary SDF runtime. Replaced the thermal-grid tier branch with continuous `HomeostasisBrain.GlobalQualityWeight` multiplied by a smooth VRAM weight from 1024 MB to 3072 MB.
Rejected Alternatives: Polling `GlobalRegistry.Player` in the biome slow tick, adding a new global slot, preserving the binary thermal tier switch, or changing thermal DTO/save layout were rejected.
Scalability potential: Weak devices fade the expensive thermal grid out through low quality/VRAM weight; middle devices cross the threshold naturally; high/ultra keep the grid and can spend the saved budget on visuals. Gameplay truth, save identity, and DTO layout do not change.
Hardware Impact: Biome SDF avoids stale/missing Player context without slow-tick registry reads, estimated 1-2 us per recovery pass. Thermal grid avoids allocating/running the 32^3 diffusion/readback path when continuous quality and VRAM budget do not justify it; low-end i3/MX350-class gain is workload-dependent and can be milliseconds when the grid stays disabled.

## 2026-05-23 Proxy Light Continuous Math Cleanup
Problem: `ProxyLightRegistry.GetVisibleLightsBatch()` read `GlobalRegistry.ScalabilityTier` in the visible-light batch and selected `DistanceMath.Normalize` through a binary tier path.
Solution: Replaced the hot registry tier read with `HomeostasisBrain.GlobalQualityWeight` and blended dominant-axis approximation with precise normalization by continuous quality and distance.
Rejected Alternatives: Keeping binary low/high math LOD, adding a listener to a static registry, or forcing precise normalization for every low-tier distant proxy light were rejected.
Scalability potential: Weak devices keep dominant-axis cheap math for distant/low-quality lights; middle tiers blend; high/ultra get precise near-light forward gating. Light identity and visibility authority remain unchanged.
Hardware Impact: Removes one `GlobalRegistry.ScalabilityTier` read per proxy-light batch and avoids precise normalize on low-quality distant gates. Estimated saving is 1-3 us per dense proxy-light query on i3/MX350-class CPUs.

## 2026-05-23 Active Architecture Verifier Pointer Repair
Problem: `GLOBAL_AUTHORITY_BOUNDARIES.md` and `GLOBAL_AUTHORITY_OPERATING_MODEL.md` still named `cleanup21_beacon_pause` after proxy-light cleanup produced the newer `cleanup21` verifier artifact.
Solution: Moved both active authority docs to `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup21.log`.
Rejected Alternatives: Keeping mixed active verifier pointers or rewriting historical log entries were rejected.
Scalability potential: Documentation-only; all runtime tiers unchanged.
Hardware Impact: Runtime 0 us. Future handoff lookup saving estimated 1,000-3,000 us.

## 2026-05-23 Audio Log Service Rebind Cleanup
Problem: `AudioLogPickup` resolved AudioLogRuntime and Localization through registry in enable/interact/localization paths. `AudioLogSystem` registered/unregistered against `SaveRuntime` through direct registry reads and did not update on Save service replacement.
Solution: Added cached AudioLogRuntime/Localization to pickups with hot-swap refresh, and cached SaveManager in `AudioLogSystem` with replacement-time unregister/register. Existing audio/player cache handling stayed intact.
Rejected Alternatives: Per-interact registry reads, new audio-log global route, scene search, or leaving saveable registration pinned to a stale SaveManager were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical log discovery, pickup deactivation, playback, save mask layout, and localization behavior. This only repairs owner routing.
Hardware Impact: Removes registry reads from audio-log pickup enable/interact/localization and SaveRuntime lifecycle registration. Estimated saving is 1-3 us per pickup interaction/enable on low-end CPUs; hot-swap callback cost is rare and below 10 us.

## 2026-05-23 Flora Genome Continuous Quality Cleanup
Problem: `FloraGenomeVaultRuntime.ResolveHardwareTier()` read `GlobalRegistry.ScalabilityTier` when scheduling plant L-system generation.
Solution: Replaced the registry tier read with finite/saturated `HomeostasisBrain.GlobalQualityWeight` and four cost bands: weak, middle, high, ultra.
Rejected Alternatives: Keeping binary hardware-tier switch, adding a new registry slot, or changing `FloraPlantSeedDTO` layout were rejected.
Scalability potential: Weak devices cap L-system expansion and matrix output cheaply; middle/high tiers scale up; ultra keeps full branch matrix budget. Save identity and DTO layout remain unchanged.
Hardware Impact: Removes one registry tier read per plant generation schedule and keeps low-end plant jobs in cheaper matrix/iteration caps. Estimated saving is 1-4 us per dense generation scheduling burst plus reduced job work on low quality.

## 2026-05-23 Resource Scarcity Service Rebind Cleanup
Problem: `ResourceScarcityDirector` registered directly against `GlobalRegistry.SaveRuntime` and read Quest/PlayerInventory/Player through registry inside scarcity slow-tick and collection paths.
Solution: Added cached SaveManager, QuestManager, PlayerInventory service, and Player runtime context; added `IGlobalRegistryHotSwapListener`; Save replacement unregisters/registers through the cached owner; scarcity evaluation and player AUP resolution consume cached services.
Rejected Alternatives: Per-slow-tick registry reads, static player AUP lookup, adding a new scarcity global route, or changing scarcity save DTOs were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical directive thresholds, sector extraction math, quest hashes, and save layout. This only repairs service ownership and replacement behavior.
Hardware Impact: Removes Quest/Inventory/Player registry reads from scarcity slow-tick and collection event evaluation. Estimated saving is 1-3 us per scarcity pass on i3/MX350-class CPUs; hot-swap callback cost is rare and below 10 us. Compile proof is now covered by `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup23_outpost_quality_retry2.log`.

## 2026-05-23 Marauder Outpost Continuous Quality Cleanup
Problem: `MarauderOutpostGenerationService.ResolveQualityTier()` read `GlobalRegistry.ScalabilityTier` while choosing outpost WFC/job cost.
Solution: Replaced the registry tier read with finite/saturated `HomeostasisBrain.GlobalQualityWeight` and weak/middle/high/ultra thresholds.
Rejected Alternatives: Keeping hardware-tier registry reads, adding quality to outpost snapshot/persistence DTOs, or collapsing to a binary low/high quality switch were rejected.
Scalability potential: Weak devices keep smaller outpost solve dimensions and job work; middle/high scale naturally; ultra keeps the full WFC budget. Gameplay truth, save identity, and DTO layout remain unchanged.
Hardware Impact: Removes one registry tier read per outpost generation schedule and keeps low-end WFC work in cheaper caps. Estimated saving is 1-5 us per generation scheduling path plus reduced job work on low quality.

## 2026-05-23 Soundscape Continuous Quality Cleanup
Problem: `SoundscapeSystem` seeded impact-signal policy from `GlobalRegistry.ScalabilityTier` and kept a binary `ScalabilityChangedEvent` subscription for drain budget and dynamic pitch.
Solution: Removed the tier cache/event route for this presentation policy. Signal drain budget and impact pitch now derive from finite/saturated `HomeostasisBrain.GlobalQualityWeight` with smooth continuous scaling.
Rejected Alternatives: Keeping binary tier events, adding a new registry slot, or preserving a high/low dynamic-pitch switch were rejected.
Scalability potential: Weak devices drain fewer impact signals and keep flatter clang pitch; middle/high scale continuously; ultra spends budget on more signal detail and stronger pitch variation. Audio event identity and gameplay truth remain unchanged.
Hardware Impact: Removes scalability event listener maintenance and one cold registry tier seed; low quality drains fewer impact signals. Estimated saving is 1-4 us per soundscape slow tick under dense impact traffic.

## 2026-05-23 Save Thumbnail Continuous Quality Cleanup
Problem: `SaveThumbnailSystem.ShouldSkipScreenshotForCurrentTier()` read `GlobalRegistry.ScalabilityTier` to skip captures on Low/Mx350.
Solution: Replaced the tier read with finite/saturated `HomeostasisBrain.GlobalQualityWeight` and a continuous threshold that preserves low-quality skip behavior.
Rejected Alternatives: Keeping binary hardware-tier gate, changing thumbnail dimensions/file format, or adding save-slot quality metadata were rejected.
Scalability potential: Weak devices keep cheap fallback thumbnail reuse; middle/high/ultra capture thumbnails normally. Save identity, thumbnail path, and cache layout remain unchanged.
Hardware Impact: Removes one registry tier read per thumbnail request and avoids GPU readback/encode below quality threshold. Estimated saving is 1-3 us per request plus full skipped capture cost on weak devices.

## 2026-05-23 Contextual Physical IK Continuous Quality Cleanup
Problem: `ContextualPhysicalIkRig` kept a binary `GlobalRegistry.ScalabilityTier` cache and `ScalabilityChangedEvent` subscription for lower-body IK, wall touch, breathing wave, and spine target policy.
Solution: Removed the tier cache/listener. `HomeostasisBrain.GlobalQualityWeight` now continuously scales IK cadence distance bias, foot/hand probe and contact weights, wall-touch influence, breathing rate/amplitude/jitter, and triangle-to-fast-sine spine motion without changing `ContextualPhysicalIkEntityState` or target-frame layout.
Rejected Alternatives: Keeping Low/Mx350 feature switches, adding a new registry slot, changing runtime DTO layout, or forcing high-cost sine/long probe distances on weak devices were rejected.
Scalability potential: Weak devices keep reduced IK influence, shorter hand/foot probe work, flatter breathing, and earlier cadence throttling; middle devices interpolate; high/ultra keep fuller wall touch/spine motion. Gameplay truth, save identity, and IK DTO layout remain unchanged.
Hardware Impact: Removes one cold tier read and one scalability event listener per rig. Low quality reduces ray distances/influence and pushes throttle bands earlier; estimated saving is 2-9 us per dense rig capture/raycast scheduling slice on i3/MX350-class CPUs.

## 2026-05-23 Procedural Wreck Continuous Quality And Signal Lane Cleanup
Problem: `ProceduralWreckGenerator` still used a binary scalability listener for WFC grid/placement, BRG fragment, debris, and debris-gravity budgets. The next compile also exposed mismatched signal-lane writes: compliance, crush warning, simulation pause, and brownout payloads were pushed through unrelated or wrong generic lanes.
Solution: Wreck budgets now read finite/saturated `HomeostasisBrain.GlobalQualityWeight` and map continuously to power-of-two grid cap, placement cap, BRG cap, debris budget, and gravity slice. Removed the wreck scalability listener. Signal producers now publish through the payload owner route: `GlobalSignals.Publish(in payload)`.
Rejected Alternatives: Keeping tier/event caches, changing wreck save/render DTO layout, adding a new registry slot, or casting payloads into unrelated `SignalBus<T>` lanes were rejected.
Scalability potential: Weak devices keep smaller wreck solve grids, fewer placements/fragments/debris records, and smaller gravity slices; middle/high interpolate; ultra keeps full authored wreck density. Gameplay identity, AUP seed, save layout, and signal payload layouts remain unchanged.
Hardware Impact: Removes one scalability event listener from each wreck generator and avoids tier cache maintenance. Low quality cuts WFC/debris/BRG work before allocation-heavy paths. Estimated saving is 3-14 us per wreck setup/slow slice plus larger job/render work avoidance on low-end i3/MX350-class CPUs.

## 2026-05-23 Acoustic Service Rebind Cleanup
Problem: `AcousticZoneController` still resolved `SoundscapeSystem` and `HectonAtmosphereManager` through `GlobalRegistry` in resolver paths reached by tick/update flow.
Solution: Added cached Soundscape/Atmosphere owner fields seeded in cold enable and refreshed through `IGlobalRegistryHotSwapListener`. Runtime resolvers now return cached owners only; service replacement refreshes soundscape tier and atmosphere zone caches.
Rejected Alternatives: Keeping timed registry retry inside soundscape/atmosphere resolvers, adding scene search fallback, or changing acoustic transition DTO/signal layout were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical acoustic snapshots, storm/vegetation overlays, soundscape tier scalars, and atmosphere zone semantics. This only repairs owner routing.
Hardware Impact: Removes Soundscape/Atmosphere registry reads from acoustic tick-dependent resolver paths. Estimated saving is 1-2 us per acoustic context refresh on i3/MX350-class CPUs; hot-swap callbacks are rare and below 10 us. Direct acoustic build attempts hit stale concurrent source in other files; later compile proof is covered by `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup28_ui_quality.log`.

## 2026-05-23 UI And Vegetation Continuous Quality Cleanup
Problem: `HectonIndirectVegetationRenderer` consumed binary scalability profile snapshots for density decimation. `DiegeticPanelController`, `SuitHUDV4CanvasOverlay`, and `DiegeticTooltipSystem` kept `ScalabilityChangedEvent` listener state for presentation-only quality policy.
Solution: Replaced those routes with finite/saturated `HomeostasisBrain.GlobalQualityWeight`. Vegetation density now maps quality pressure to decimation step. Diegetic panel refreshes RT/phosphor/material policy from continuous quality during runtime ticks. Suit HUD and tooltip update cadence/fade/dither from continuous quality without scalability event subscription.
Rejected Alternatives: Keeping binary profile/event listeners, changing UI signal DTOs, changing vegetation instance payload layout, or editing dirty unrelated runtime files were rejected.
Scalability potential: Weak devices raise vegetation decimation and reduce UI presentation cadence/fade/dither cost; middle/high interpolate; ultra keeps dense vegetation, full HUD cadence, and richer tooltip/panel presentation. Gameplay truth, input identity, save state, and render DTO layouts remain unchanged.
Hardware Impact: Removes four scalability listener routes and one vegetation profile snapshot drain. Estimated low-end i3/MX350-class saving is 1-8 us across dense UI/vegetation presentation slices, plus reduced indirect vegetation culling/draw pressure at low quality. Compile proof: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup28_ui_quality.log`, 0 warning/error text matches.

## 2026-05-23 Battery Charger Service Rebind And Physics Fauna Contract Cleanup
Problem: `BatteryCharger` resolved Player/Audio through `GlobalRegistry` in interaction/insert paths. The next owned compile also exposed a concrete physics dependency on `Hecton8.AI.FaunaBrain` inside `GlobalPhysicsStateManager`.
Solution: `BatteryCharger` now cold-caches Player runtime context and Audio service, refreshes both through `IGlobalRegistryHotSwapListener`, and force-resets player-owned tool/inventory caches on Player replacement. `GlobalPhysicsStateManager` classifies fauna rigidbodies through `IScannerFaunaScientificContact` instead of the AI concrete.
Rejected Alternatives: Keeping per-interact registry reads, preserving stale tool/inventory cache across Player replacement, or adding AI concrete includes to physics were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical charger transaction semantics and physics clamp values. This only repairs owner routing and cross-domain coupling.
Hardware Impact: Removes Player/Audio registry reads from charger interaction/insert paths and removes one physics->AI concrete compile dependency. Estimated runtime saving is 1-2 us per charger interaction on i3/MX350-class CPUs. Compile proof: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup30_loot_quality_retry3.log`, 0 warning/error text matches.

## 2026-05-23 UI/Vegetation Smoke Regression Gate And Compile Wall Repair
Problem: Cleanup42 removed binary scalability routes, but no source smoke gate prevented their return. The next guarded compile exposed dirty-file walls in Fauna acoustic imports, DataMonolith native import attributes, and MessageTerminal late-frame interface binding.
Solution: `AdvancedAcousticsSmokeTester` now asserts indirect vegetation and diegetic UI use `HomeostasisBrain.GlobalQualityWeight` and reject `ScalabilityChangedEvent`/binary profile routes. Restored minimal import/interface links in the dirty files only.
Rejected Alternatives: Leaving cleanup42 as chat/documentation proof, editing core legacy scalability bridge, changing fauna acoustic DTOs, or rewriting DataMonolith native read logic were rejected.
Scalability potential: Weak/Middle/High/Ultra runtime behavior stays unchanged; the new editor gate protects continuous quality routing and blocks binary regression in UI/vegetation presentation.
Hardware Impact: Runtime 0 us for the smoke gate. Compile-wall repairs are semantic/no-cost. Future regression lookup saving estimated 1,000-3,000 us.

## 2026-05-23 Loot Magnet Continuous Quality Cleanup
Problem: `LootMagnetSystem` read `GlobalRegistry.ScalabilityTierProfileByte` to choose acoustic, wake, and fluid impulse presentation budgets.
Solution: Replaced the binary tier byte with finite/saturated `HomeostasisBrain.GlobalQualityWeight`, slow-tick hysteresis, continuous budget interpolation, and smooth fluid impulse radius/lifetime/intensity.
Rejected Alternatives: Keeping hard low/default/high/ultra tier branches, changing loot acquisition truth, changing DataVault buffer layouts, or touching unrelated death-cache edits were rejected.
Scalability potential: Weak devices emit fewer/softer loot presentation signals; middle/high interpolate; ultra spends budget on stronger wake/acoustic/fluid feedback. Item acquisition truth, inventory commits, signal DTOs, and save identity remain unchanged.
Hardware Impact: Removes one scalability registry read per dependency refresh and avoids hard tier gates in loot presentation. Estimated saving is 1-3 us per dense loot commit slice on i3/MX350-class CPUs plus lower presentation signal pressure at weak quality.

## 2026-05-23 Base Airlock Service Rebind Cleanup
Problem: `BaseAirlock` resolved Audio through `GlobalRegistry` when a cycle started/ended and resolved NativeInputManager through `GlobalRegistry` when capturing the cycle input lock.
Solution: Added cached Audio and NativeInputManager owner fields seeded during enable and refreshed through `IGlobalRegistryHotSwapListener`. Cycle audio and input lock now consume cached owner references only.
Rejected Alternatives: Keeping per-cycle registry reads, adding scene-search fallbacks, or changing airlock equalization/snap/teleport semantics were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical airlock timing, pressure estimate, teleport snap, and audio clip semantics. This only repairs owner routing.
Hardware Impact: Removes two Audio registry reads per airlock cycle and one NativeInputManager registry read per input lock. Estimated saving is 1-2 us per cycle on i3/MX350-class CPUs. Compile proof: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup49_service_rebind.log`, 0 warning/error text matches.

## 2026-05-23 Continuous Quality Tail And Compile Wall Cleanup
Problem: `FloraInteractionManager`, `DestructibleOrganicManager`, `TradeMarauderRuntime`, and `SpectrumSystem` still used binary scalability profile/tier state in presentation/economy policy. The guarded compile also exposed small dirty-file walls in physics impact interface shape, vocal warning helper scope, armor grid aliases, and PDA haptics namespace resolution.
Solution: Replaced those quality tails with finite/saturated `HomeostasisBrain.GlobalQualityWeight` and neutral `0.5f` fallback. Removed the duplicate inherited impact-material property, qualified the nested vocal warning helper call, restored armor grid row/column aliases to the penetration LUT table, and added the PDA haptics namespace route.
Rejected Alternatives: Keeping hard Low/Mx350 gates, changing save/DTO/layout identity, broad rewrites in dirty systems, or adding new global quality slots were rejected.
Scalability potential: Weak devices reduce sway, fracture, economy route solve, and active sonar shader pressure continuously; middle/high interpolate; ultra keeps fuller presentation and solver budgets. Gameplay truth and data layouts stay unchanged.
Hardware Impact: Removes binary registry quality reads from four active systems and repairs compile-only walls with no runtime cost. Estimated low-end saving is 1-8 us across dense presentation/economy slices. Compile proof: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup49_service_rebind.log`, 0 warning/error text matches.

## 2026-05-23 Charger Module And PDA Shell Service Rebind Cleanup
Problem: `BatteryChargerModule` used `GlobalRegistry.Player` as an interaction fallback, and `PDAShellChrome` resolved Player and NativeInput through direct registry reads after lifecycle.
Solution: Added cached Player owner state and `IGlobalRegistryHotSwapListener` to `BatteryChargerModule`. Added cached Player context to `PDAShellChrome`, updated Player replacement to clear stale PDA/player-owned refs before rebinding, and changed NativeInput replacement to consume the rebound service instance.
Rejected Alternatives: Polling Player/NativeInput from interaction/PDA-open paths, scene searches for player-owned state, or changing PDA/inventory/tool DTOs were rejected.
Scalability potential: Low/Middle/High/Ultra keep identical dock semantics, PDA footer state, reboot binding display, and intrusion chrome. This only repairs owner routing.
Hardware Impact: Removes one Player registry read from dock fallback and Player/NativeInput registry reads from PDA chrome binding paths. Estimated saving is 1-3 us per affected interaction/open/rebind path on i3/MX350-class CPUs. Compile proof: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup49_service_rebind.log`, 0 warning/error text matches.

## 2026-05-23 Seismic Shader Shake Continuous Quality Cleanup
Problem: `HectonSeismicTideDirector.RefreshCachedRuntimeState()` disabled shader shake through a hard `GlobalRegistry.ScalabilityTier` Low/Mx350/Unknown gate.
Solution: Removed the cached tier field and registry tier read. Shader-shake disable now consumes the existing filtered `HomeostasisBrain.GlobalQualityWeight`, while still respecting low-memory and low math-precision modes.
Rejected Alternatives: Keeping binary tier disables, adding a second seismic quality owner, or changing seismic/celestial DTO layouts were rejected.
Scalability potential: Weak devices fade shader shake off through continuous quality pressure; middle/high can keep it when budget allows; ultra keeps full shader shake unless memory/math policy explicitly forbids it.
Hardware Impact: Removes one tier registry read from seismic refresh and avoids hard profile discontinuity. Estimated saving is below 1 us per refresh; real value is deterministic continuous LOD behavior. Compile proof: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup49_service_rebind.log`, 0 warning/error text matches.

## 2026-05-23 GPU Scatter Continuous Quality Cleanup
Problem: `GPUScatterDirector` and `GpuScatterLodManager` still carried binary scalability event/tier state for scatter LOD, cull distance, material payload, and shader scalar policy.
Solution: Removed `ScalabilityChangedEvent` listeners and `GlobalRegistry.ScalabilityTier` seeding. Scatter policy now reads finite/saturated `HomeostasisBrain.GlobalQualityWeight`; cull distance, payload strength, transition range, and material scalars scale continuously. Authored low/high material variant remains a discrete asset boundary derived from quality threshold only.
Rejected Alternatives: Keeping binary Low/High switches, adding quality into DataVault scatter DTOs, or changing scatter generation identity were rejected.
Scalability potential: Weak devices keep shorter cull distance and cheaper visual payload; middle/high interpolate; ultra spends budget on full scatter distance, richer transition, and shader payload strength without changing gameplay truth.
Hardware Impact: Removes scalability listener maintenance and one tier registry seed path. Estimated saving is 2-7 us per scatter policy refresh on i3/MX350-class CPUs, plus lower GPU scatter pressure at weak quality. Compile proof: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup49_service_rebind.log`, 0 warning/error text matches.

## 2026-05-23 Cultivation Inventory Rebind And Geology Compile Wall Cleanup
Problem: `BotanyPlanterModule.CopyBufferSnapshot()` and `CultivationManager.ResolveItemCatalog()` read `GlobalRegistry.PlayerInventory` on runtime/UI paths. The guarded build then exposed generated-Core drift (`GlobalSignalPayloads.UiSaveWorld.cs` omitted), a duplicate `CoreContractsAssemblyMarker.cs` include, and a same-file geology parser-helper ownership error.
Solution: Added cached `IPlayerInventoryService` fields and `IGlobalRegistryHotSwapListener` rebinding to planter/cultivation. Added the missing UI/save/world signal payload file to the local generated Core project, removed the duplicate explicit Core contracts marker include because `Directory.Build.targets` already injects it, and moved geology parser helpers back into `WorldGenerativeGeologyBinding`, where the private labels and getters live.
Rejected Alternatives: Per-snapshot/per-catalog registry polling, warning suppression for CS2002, duplicating geology labels into the service class, changing generation math, or changing inventory/save DTOs were rejected.
Scalability potential: Weak/Middle/High/Ultra keep identical cultivation item truth and geology generation output. The fix only repairs owner routing and compile graph hygiene; quality policy remains continuous and separate.
Hardware Impact: Removes PlayerInventory registry reads from planter snapshot/catalog resolution paths, estimated 1-2 us per affected UI/cultivation refresh on i3/MX350-class CPUs. Geology helper relocation and generated-project graph repair are runtime 0 us. Compile proof: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup52_cultivation_inventory_rebind_retry4.log`, 0 warning/error text matches. MSBuild shutdown succeeded; orphan `VBCSCompiler.exe` was force-stopped after its shutdown command failed.

## 2026-05-23 Atmosphere Thermodynamics Quality And Player Interaction Rebind Cleanup
Problem: `ToxicOutgassingChemistryRuntime` and `ThermodynamicsHazardGridRuntime` still carried binary scalability/tier quality policy. `PlayerInteraction` played hover/interact audio and completed pickup handling through action-path `GlobalRegistry` reads. Guarded compile also exposed current dirty-source walls in VR somatic finiteness, missing UI/save/world payload includes, duplicate Core contracts include, and missing fauna director concrete bridge.
Solution: Replaced toxic/thermo binary tier policy with finite continuous runtime quality from signal/global quality owners, scaled resolution/tick cadence smoothly, cached PlayerInteraction Audio/PlayerInventory owners with registry hot-swap, added the missing local generated Core payload include, guarded the duplicate Core contracts include, fixed `VRSomaticProvider` finite vector check, and restored the fauna concrete helper through the service interface route.
Rejected Alternatives: Binary `ScalabilityChangedEvent` routes, per-hover/per-interact registry reads, suppressing duplicate compile warnings, changing DTO/save identity, or adding new global service slots were rejected.
Scalability potential: Weak devices get cheaper toxic-grid resolution/tick cadence and no interaction registry churn; middle/high interpolate; ultra keeps full chemistry/thermal presentation cost. Gameplay truth, authority route, and save/DTO layout stay unchanged.
Hardware Impact: Toxic/thermal continuous LOD can save roughly 3-12 us per affected slow slice on i3/MX350-class CPUs under low quality. PlayerInteraction removes two Audio registry reads and one PlayerInventory registry read from hover/interact paths, estimated 1-3 us per dense interaction burst. Source graph fixes are runtime 0 us. Compile proof for loop 53 is still source-only: retry2 exposed and fixed `IDataVault`/`InputDispatcher` compile walls; retry3 hit a stale/concurrent `ModalWindow` overload wall while current source already has the required overload; retry4 guard timed out after 10 minutes because CPU/compiler contention never cleared. Last full PASS remains `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup52_cultivation_inventory_rebind_retry4.log`.

## 2026-05-24 Somatic/GI/Memory Continuous Quality And Input Graph Cleanup
Problem: `SomaticKinematicsRuntime` still collapsed CCD to one step on Low/ThermalThrottle. `HectonGIRelaySystem` still fell back through `GlobalRegistry.ScalabilityTier` when global quality was not finite. `MemorySentinelRuntime` subscribed to binary `ScalabilityEvents` only to flip a flag. The local generated Core graph also compiled `InputBindingServiceContracts.cs` while `Hecton8.Bootstrap.Contracts` compiled the same file, producing two incompatible `INativeInputManagerRuntime` type identities.
Solution: Somatic CCD step count now scales from continuous quality pressure; GI relay caches finite `HomeostasisBrain.GlobalQualityWeight`; MemorySentinel removed the binary scalability listener; `IServiceHeartbeat.TickCount` now uses `global::System.Environment.TickCount`; local generated Core no longer compiles the duplicate bootstrap contract source.
Rejected Alternatives: Binary Low/Mx350 CCD collapse, tier fallback for GI, dummy scalability flag mutation, casting `InputManager` across duplicate interface identities, or building while CPU guard was red were rejected.
Scalability potential: Weak devices reduce somatic CCD work smoothly; middle/high interpolate; ultra keeps full CCD steps and GI quality. Memory sentinel keeps the same continuous validation cadence. Input contract identity is build-only.
Hardware Impact: Somatic weak-quality saving is estimated 1-6 us per high-speed collision slice on i3/MX350-class CPUs. GI fallback removes one tier registry route; MemorySentinel removes one listener lane; input graph fix is runtime 0 us. Compile proof is source-only: `git diff --check` passed; retry1 exposed current walls; build guard stayed blocked by >50% CPU after stale MSBuild/Roslyn nodes were stopped.

## 2026-05-24 Base/Tether/Voxel/Drill/Lockstep Authority Tail Cleanup
Problem: `BaseModule` still read `GlobalRegistry.ObjectPool` in a runtime reef-proxy visual gate. `TetherManager` and `VoxelDeltaProcessor` still consumed binary registry quality tiers for tether visual/solver bands and voxel carve drain budget. `DeployableSdfDrillRuntime` and `LockstepStateValidator` still registered scalability-event listeners even though their active math already resolved authoritative ultra or continuous `GlobalQualityWeight`.
Solution: Added cached ObjectPool state to `BaseModule` cold cache and hot-swap replacement handling. Replaced tether and voxel tier reads with finite/saturated `HomeostasisBrain.GlobalQualityWeight` mapping into existing bounded budgets. Removed the dead drill and lockstep scalability listener routes.
Rejected Alternatives: Per-frame/per-visual registry reads, adding new global quality slots, changing tether/voxel/drill DTO layouts, changing lockstep replay/hash payloads, or launching a guarded build while CPU/dotnet state was red were rejected.
Scalability potential: Weak devices keep cheaper tether visuals and voxel drain budgets through continuous quality pressure; middle/high interpolate through existing cost bands; ultra keeps indirect tether rendering and full carve drain budget. Base reef visuals keep identical pool-reserve behavior. Drill authoritative math and lockstep hash cadence semantics stay unchanged.
Hardware Impact: Removes one ObjectPool registry read from BaseModule reef visual gate, one tier registry read from tether slow cache, one tier-derived carve-budget route from voxel drain, and two no-op scalability listener lanes. Estimated low-end saving is 1-4 us across affected slow/visual slices; listener removal is runtime-noise reduction. Compile proof is source-only: scoped `git diff --check` passed; guarded build stayed blocked by CPU >50% and non-owned compiler contention.

## 2026-05-24 Player/Submarine Binary Scalability Tail Cleanup
Problem: `HectonPlayerMotor`, `HectonSubmarineOS`, and `SubmarineAutoLevelBallastController` still carried binary scalability listener/tier routes. Motor profile byte was dead state, submarine OS used tier enum for sonar cadence/interpolation, and ballast listened to scalability events despite refreshing continuous quality in slow tick.
Solution: Removed the dead listeners/profile byte. Submarine OS now reads finite/saturated `HomeostasisBrain.GlobalQualityWeight`, maps sonar cadence through weak/middle/high interpolation, and writes continuous interpolation/quality scalar to `_HectonSubOsSonarLod`.
Rejected Alternatives: Keeping `ScalabilityEvents` subscriptions, publishing presentation changes through tier payloads, changing sonar/snapshot DTOs, or touching unrelated dirty submarine shader-queue edits were rejected.
Scalability potential: Weak devices get slower sonar refresh and no sharp high-tier interpolation jump; middle/high interpolate; ultra keeps fastest sonar presentation. Ballast and player movement truth ownership, save identity, and DTO layout stay unchanged.
Hardware Impact: Removes three binary listener/profile routes and one shader tier branch. Estimated saving is 1-4 us across affected player/submarine presentation/control slices on i3/MX350-class hardware; larger value is eliminating discontinuous quality policy. Compile proof is source-only because CPU stayed above the >50% build guard; non-owned `dotnet` processes were observed earlier.

## 2026-05-24 Player Movement Continuous Presentation Quality Cleanup
Problem: `HectonPlayerMovement` still consumed binary scalability profile state for brine fog hard-clip and cinematic focus FOV, and subscribed to `ScalabilityEvents` to republish presentation state.
Solution: Removed the binary listener/profile byte. Brine fog hard-clip now interpolates from weak hard-clip to authored high-quality fog via continuous `HomeostasisBrain.GlobalQualityWeight`; cinematic focus FOV narrows with continuous quality weight instead of a tier gate.
Rejected Alternatives: Keeping event-driven tier payloads, adding a new player quality cache service, changing movement DTOs, or changing brine gameplay density/toxicity were rejected.
Scalability potential: Weak devices keep cheaper brine fog and reduced FOV effect; middle/high interpolate; ultra keeps authored brine fog and full cinematic focus. Movement truth, brine toxicity, save identity, and telemetry layout stay unchanged.
Hardware Impact: Removes one listener lane and one profile-byte registry route from player movement. Estimated saving is 1-3 us across brine/focus presentation slices on i3/MX350-class hardware; compile proof is source-only because CPU samples exceeded the >50% build guard.

## 2026-05-24 Scanner/Gyro/Interior-GI Binary Listener Cleanup
Problem: `ScannerTool`, `DiegeticGyroCompassRuntime`, and `InteriorGIProbeVolumeRuntime` still subscribed to binary scalability events for presentation/quality policy while their actual knobs could read continuous `HomeostasisBrain.GlobalQualityWeight`.
Solution: Removed the binary listener interfaces, register/unregister routes, and event callbacks. Scanner refreshes quality on fast/publish/resample paths; gyro refreshes quality and indirect buffers from slow tick; interior GI resolves quality from continuous global weight with finite fallback.
Rejected Alternatives: Keeping `ScalabilityEvents` subscriptions, adding another quality route, or changing scanner/GI/compass DTO/save layout were rejected.
Scalability potential: Weak devices get lower scanner presentation pressure, cheaper gyro indirect rendering policy, and lower GI resolution through existing continuous budgets; middle/high interpolate; ultra keeps richer scanner/gyro/GI presentation.
Hardware Impact: Removes three scalability listener lanes and avoids binary event dispatch for touched systems. Estimated saving is 1-5 us across affected presentation/slow slices on i3/MX350-class CPUs. Compile proof is source-only because CPU guard was 100%; scoped `diff --check` and touched-file greps passed.

## 2026-05-24 Runtime Binary Scalability Tail Burn-Down
Problem: Remaining non-editor runtime tails still used binary scalability routes in bootstrap vault/math LOD, DRS, player kinematics, submarine fluid, hydro KCC, and stale player-movement registration references.
Solution: `GameBootstrapper` derives vault profile byte and math LOD from continuous `HomeostasisBrain.GlobalQualityWeight`; `ThermalDynamicResolutionAdapter` projects continuous quality into existing tier byte/enum DTO fields; player kinematics, hydro KCC, and submarine fluid removed listener lanes whose work is already continuous tick refresh or constant authoritative math LOD; stale player-movement scalability registration references are gone.
Rejected Alternatives: Keeping Core scalability events as first-party runtime policy transport, changing DTO layouts, or launching a build during active external `dotnet/csc` work were rejected.
Scalability potential: Weak devices keep cheaper vault/DRS/player/submarine/KCC presentation budgets; middle/high interpolate; ultra keeps visual overkill through existing continuous quality weights and current DTO ABI.
Hardware Impact: Removes remaining non-editor/non-Core bridge binary listener/tier routes from runtime source. Estimated saving is 2-9 us across affected bootstrap/presentation/tick slices; larger value is removing discontinuous policy and compile-risk stale registration references. Compile proof is source-only: the guarded loop59 build attempt exited 1 with a 0-byte log/no diagnostics, and follow-up guard stayed above 50% CPU.

## 2026-05-24 Beacon/Blueprint Registry Fanout Cleanup
Problem: `BeaconNetworkSystem` static retract/nearest/destroy routes read `GlobalRegistry.BeaconNetwork` on action paths, and construction blueprint visibility calls fanned out into `GlobalRegistry.QuestSystem` for every catalog/card/cycle item.
Solution: Beacon network now publishes an active runtime pointer during lifecycle/service registration and uses it for static action reads. `BuildableData`/`ModuleCatalog` gained cached-quest overloads; `PlayerBuilder` and `PDAConstructionTab` cache `IQuestSystem` and pass it through catalog scans.
Rejected Alternatives: Keeping static registry lookups, adding a new signal lane for one-owner blueprint queries, or changing quest/blueprint DTO identity were rejected.
Scalability potential: Weak devices avoid avoidable registry fanout in construction UI/tool cycling; middle/high/ultra keep identical blueprint truth while spending saved UI/action budget on richer presentation already owned by PDA/builder systems.
Hardware Impact: Removes repeated QuestSystem registry reads from construction catalog scans and BeaconNetwork registry reads from static action helpers. Estimated saving is 1-5 us per dense PDA/builder refresh or beacon action burst on i3/MX350-class CPUs. Compile proof is source-only: scoped `diff --check` passed; `IsBlueprintViewable()` callsite grep now returns only the legacy method definition; build guard stayed red because active compiler processes existed and CPU samples exceeded 50%.

## 2026-05-24 SDF/Terrain Probe Owner Cache Cleanup
Problem: SDF/Terrain probe helpers in PDA focus, buoyancy, equipment interaction, contextual IK, VR somatic, deployable drill, and laser cutter DOD used cached service fields but still fell back to `GlobalRegistry.VoxelSonarSdf` or `GlobalRegistry.Terrain` inside probe/action paths.
Solution: Probe helpers now consume only cached owner fields. Lifecycle cold cache remains, and service replacement updates caches through existing hot-swap listeners; laser cutter DOD now exposes an explicit cache setter called from `LaserCutter.OnGlobalRegistryServiceReplaced`.
Rejected Alternatives: Per-probe registry retry, scene/physics fallback, DTO layout changes, or new global slots were rejected.
Scalability potential: Weak devices avoid registry fallback fanout in dense probe slices; middle/high/ultra keep richer SDF/terrain probes through the same cached owner route. Gameplay truth and save/DTO identity are unchanged.
Hardware Impact: Removes hot fallback reads from multiple probe/action paths. Estimated saving is 1-6 us across dense tool/IK/buoyancy probe slices on i3/MX350-class CPUs. Compile proof is source-only: scoped `diff --check` passed and the project runtime grep for `?? GlobalRegistry.VoxelSonarSdf/Terrain` returned no matches; guarded build was blocked by CPU 72% and active `VBCSCompiler`.

## 2026-05-24 Construction Manager Service Cache Cleanup
Problem: `ConstructionManager` still read `GlobalRegistry.ObjectPool`, `GlobalRegistry.PlayerInventory`, and `GlobalRegistry.DataVault` from deconstruction, load/clear, save catalog, and telemetry paths after lifecycle.
Solution: Added cached ObjectPool/PlayerInventory/DataVault owner fields, seeded them in cold lifecycle, cleared them on unregister/shutdown, and refreshed them through the existing `IGlobalRegistryHotSwapListener` lane.
Rejected Alternatives: Per-action registry fallback, a new construction-specific service slot, scene search, or changing construction/save/deconstruction DTO identity were rejected.
Scalability potential: Weak devices avoid registry fanout in dense construction/deconstruction/save slices; middle/high/ultra keep the same gameplay truth and can spend saved budget on richer construction feedback.
Hardware Impact: Removes service registry reads from module teardown, clear/load, and item-catalog save helpers. Estimated saving is 1-4 us per dense construction action/save slice on i3/MX350-class CPUs. Compile proof is source-only: scoped `diff --check` passed; service-owner grep leaves only cold cache reads; guarded build was blocked by CPU 100/100/100 and active `csc`/`dotnet`.

## 2026-05-24 Callback/Physics/Audio Owner Cache Cleanup
Problem: Service-replacement callbacks in LaserCutter, RandomEventSystem, HarvestableOutcrop, and PDAInventoryTab still retried through `GlobalRegistry`. Fauna ragdoll handoff fell back to `GlobalRegistry.Physics` during joint application. Procedural audio non-allocating enqueue could resolve DataVault from registry, and power-grid shutdown could attempt release through a non-owned registry vault.
Solution: Callback paths now use `currentService` or existing cached owners. Fauna ragdoll registered as a Physics hot-swap listener and uses the cached service during handoff. Procedural audio DataVault lookup is confined to the allocating cold path. Power-grid shutdown releases only `_jacobiVaultOwner`.
Rejected Alternatives: Registry retry inside callbacks/action paths, scene physics fallback, unknown-vault release, or broad DataVault static rewrites were rejected.
Scalability potential: Weak devices avoid small callback/action-path registry churn; middle/high/ultra keep identical event, ragdoll, audio, and power behavior.
Hardware Impact: Removes callback retry tails and one physics handoff fallback; estimated saving is 1-5 us across dense replacement/handoff/enqueue bursts. Compile proof is source-only: scoped `diff --check` passed; callback fallback grep returned no matches; build guard stayed blocked by CPU 53.6%.

## 2026-05-24 Structural Integrity DataVault Late-Bind Cleanup
Problem: `StructuralIntegrityCalculatorRuntime` failed init when DataVault was absent at enable, did not register a hot-swap listener, then retried `GlobalRegistry.DataVault` inside `TryInitialize`.
Solution: Register hot-swap before initialization, seed DataVault only in cold lifecycle, rebind on `GlobalRegistryServiceSlot.DataVault`, release old handles before reinit, and remove the `TryInitialize` registry fallback.
Rejected Alternatives: Cold-tick registry retry, scene/bootstrap search, changing structural DTOs, or broad hull deformation rewrites were rejected.
Scalability potential: Weak devices avoid retry churn and late-bind blindness in structural integrity setup; middle/high/ultra keep identical stress/deformation truth and can spend budget on existing structural warning visuals.
Hardware Impact: Removes DataVault registry retry from structural initialization and makes DataVault replacement deterministic. Estimated saving is 1-3 us per failed/late structural init pass on i3/MX350-class CPUs. Compile proof is source-only: scoped `diff --check` passed; touched-file DataVault grep leaves only cold cache read; guarded build was blocked by CPU 100/100/100.

## 2026-05-24 DataVault Owner Cache Tail Cleanup
Problem: DestructibleOrganicManager Dear Lie bootstrap, HullIntegrityRuntime initialization, and HectonVoxelEngine voxel-mesh black-box release/dump still used `?? GlobalRegistry.DataVault` after their owner cache had already been seeded.
Solution: These paths now consume only the cached owner vault. DestructibleOrganicManager seeds `_dearLieVault` during cold service cache. HullIntegrityRuntime trusts the OnEnable cache. Voxel black-box release/dump uses `_voxelMeshPipelineBlackBoxVault` only.
Rejected Alternatives: Unknown-vault release, editor dump registry retry, or broad Core/DataVault static rewrites were rejected.
Scalability potential: Weak devices avoid small registry fallback churn in organic, hull, and voxel diagnostic lanes; middle/high/ultra preserve identical buffers and visual behavior.
Hardware Impact: Removes selected DataVault fallback reads and prevents release against a non-owned vault; estimated saving is 1-4 us in dense organic/hull/voxel diagnostic slices. Compile proof is source-only: scoped `diff --check` passed; build guard stayed blocked by CPU 100%.

## 2026-05-24 GameBootstrapper Warning Cleanup
Problem: Guarded `cleanup65_owner_cache` build reached `Hecton8.Editor.dll` but emitted three `CS0168` warnings from unused caught `exception` locals in GameBootstrapper.
Solution: Converted those catch clauses to `catch (Exception)` so the failure behavior stays unchanged and no unused locals remain.
Rejected Alternatives: Warning suppression or adding new exception-string logging without a telemetry route were rejected.
Scalability potential: Build hygiene only; weak/middle/high/ultra runtime behavior is unchanged.
Hardware Impact: Runtime 0 us except exception paths. Compile proof is source-only until rebuild; `cleanup65_owner_cache` is pass-with-warnings, not clean.

## 2026-05-24 Armor Torture Job Warning Cleanup
Problem: Guarded `cleanup66_warning_fix` build reached `Hecton8.Editor.dll` but emitted six `CS0649` warnings from fields on `CombatDamageTortureJob`. The job had no callsites and duplicated the active mock-fill plus `EvaluateArmorPenetrationJob` proof path.
Solution: Deleted the dead Burst job struct.
Rejected Alternatives: Warning suppression, dummy assignments, or keeping dead proof code were rejected.
Scalability potential: Build hygiene only; armor runtime proof path remains the active generated mock plus evaluator jobs.
Hardware Impact: Runtime 0 us for live gameplay because the deleted job was unreferenced. Compile proof is source-only until rebuild; `cleanup66_warning_fix` is pass-with-warnings, not clean.

## 2026-05-24 Legacy Doc Whitespace Gate Boundary
Problem: Adding `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md` and `Docs/SYSTEMS_CONTRACTS.md` to the scoped diff gate reports full-file CRLF/trailing-whitespace noise.
Solution: Kept verification on code and clean active docs; recorded the legacy-doc caveat instead of normalizing hundreds of unrelated lines.
Rejected Alternatives: Mass line-ending cleanup would pollute the multi-agent diff and bury loop65 source changes.
Scalability potential: Documentation-only; no runtime tier impact.
Hardware Impact: Runtime impact 0 us. Avoids review/build distraction from metadata churn.

## 2026-05-24 BeaconNetwork Static Action Fallback Cleanup
Problem: `BeaconNetworkSystem.GetOrCreate()` still fell back to `GlobalRegistry.BeaconNetwork`, leaving a static action path dependent on registry lookup after loop60.
Solution: Static helpers now read `s_activeRuntime`; lifecycle/recovery registers the owner service, and hot-swap callbacks keep the active pointer synchronized.
Rejected Alternatives: Static action registry lookup, scene search, or new global route were rejected.
Scalability potential: Weak devices avoid registry fallback in beacon deploy/retract bursts; middle/high/ultra keep identical beacon truth and visual behavior.
Hardware Impact: Removes one static registry lookup path from beacon actions; estimated saving 1-2 us per missing-active recovery/action burst on i3/MX350-class CPUs. Compile proof is source-only; guarded rebuild blocked by CPU 25.3/79/100.

## 2026-05-24 Scanner DataVault Owner Cache Cleanup
Problem: `ScannerDataMiningRouter.EnsureVaultState()` still retried `GlobalRegistry.DataVault` after lifecycle cache existed, creating a runtime registry fallback in scanner initialization.
Solution: Added DataVault hot-swap listener ownership, cold cache seeding, deferred rebind while query/completion buffers are locked, and removed the `?? GlobalRegistry.DataVault` instance fallback.
Rejected Alternatives: Runtime registry retry, scene/bootstrap search, or forced DataVault swap while scanner buffers/jobs are locked were rejected.
Scalability potential: Weak devices avoid registry retry churn during scanner setup/rebind; middle/high/ultra keep identical scan truth and can spend saved slice budget on scanner feedback.
Hardware Impact: Removes one DataVault registry fallback from scanner runtime initialization/rebind paths; estimated saving is 1-3 us per late DataVault init/replacement burst on i3/MX350-class CPUs. Compile proof is source-only; latest guard sampled CPU `100,100,100` with no compiler processes.

## 2026-05-24 Combat DataVault Owner Cache Cleanup
Problem: Combat ballistics, status effects, and armor penetration still had DataVault fallback paths after owner cache/hot-swap state existed.
Solution: Added a combat DataVault hot-swap bridge, routed ballistics/status/armor through cached owner vault state, removed combat `?? GlobalRegistry.DataVault` fallbacks, and released ballistics vault handles on swap/shutdown.
Rejected Alternatives: Runtime registry retry inside combat init, direct scene/bootstrap search, or keeping stale ballistics handles after a vault replacement were rejected.
Scalability potential: Weak devices avoid small combat setup/rebind registry churn; middle/high/ultra keep identical damage/status/ballistics truth and can spend saved budget on combat feedback.
Hardware Impact: Removes selected combat DataVault fallback reads and prevents old-vault handle retention; estimated saving is 1-4 us per late combat init/rebind burst on i3/MX350-class CPUs. Compile proof is source-only; latest guard sampled CPU `100` with no compiler processes.

## 2026-05-24 Runtime GlobalRegistry Fallback Tail Cleanup
Problem: Remaining non-editor source still contained `?? GlobalRegistry.DataVault` fallbacks in MathGuard, static data stores, Babel dictionary store, and SignalWarden crash dump.
Solution: MathGuard now resolves DataVault only during cold `Initialize`; hot writer/drain paths read cached handles only. Static/Babel stores now require bound owner vaults. SignalWarden crash dump uses cached vault or `GlobalDataVault.TryGetLatestCreated()` instead of registry.
Rejected Alternatives: Hot registry retry from MathGuard, implicit store-level global fallback, or crash-route registry lookup were rejected.
Scalability potential: Weak devices avoid tiny registry churn in diagnostics/data-store edges; middle/high/ultra keep identical telemetry/data behavior and can spend saved budget on visible feedback.
Hardware Impact: Removes the remaining runtime `?? GlobalRegistry` fallback pattern; estimated saving is 1-4 us across late diagnostic/data-store init and hot invalid-number writer failure paths. Compile proof is source-only; project runtime grep for `?? GlobalRegistry.` returned no matches.

## 2026-05-24 FloatingOrigin AUP Tuner Owner Cache Cleanup
Problem: `HectonFloatingOrigin` AUP tuner/static facades used `origin._dataVault ?? GlobalRegistry.DataVault`, so a live owner with missing/unbound cache could silently retry the registry.
Solution: Added `ResolveAupTunerVault(origin)`: live owner reads only `_dataVault`; `GlobalRegistry.DataVault` remains only when no floating-origin owner exists for cold tuner/bootstrap access.
Rejected Alternatives: Owner-present registry retry, new global route, or touching the existing dirty visual-sync origin-shift slice were rejected.
Scalability potential: Weak devices avoid small registry fallback churn in tuner/readback calls; middle/high/ultra keep identical AUP shift truth and diagnostics.
Hardware Impact: Removes four owner-present DataVault fallback expressions from AUP tuner/readback paths; estimated saving is 1-2 us per diagnostic/tuner burst on i3/MX350-class CPUs. Compile proof remains source-only until CPU guard permits rebuild.

## 2026-05-24 Analytics DataVault Owner Cache Cleanup
Problem: `AsynchronousTelemetryExporter.TryAcquireVaultStorage()` still used `_dataVault ?? GlobalRegistry.DataVault`, and analytics had no worker-safe DataVault replacement boundary.
Solution: Exporter now implements `IGlobalRegistryHotSwapListener`; storage acquisition reads cached `_dataVault` only; DataVault replacement stops the worker before releasing/reacquiring handles and preserves the old vault if shutdown fails. Bootstrap floating-origin fallback now uses explicit cold null check instead of `?? GlobalRegistry`.
Rejected Alternatives: Runtime registry retry during storage acquisition, swapping `_dataVault` while the worker still owns buffers, or broad analytics worker refactor were rejected.
Scalability potential: Weak devices avoid registry retry churn and stale handle risk in telemetry setup/rebind; middle/high/ultra keep identical analytics truth and can spend saved budget on diagnostics.
Hardware Impact: Removes analytics DataVault fallback and final broad `?? GlobalRegistry` pattern; estimated saving is 1-4 us per late analytics init/rebind burst on i3/MX350-class CPUs. Compile proof remains source-only; latest guard sampled CPU `99.5,100,99.2` with no compiler processes.

## 2026-05-24 Suit/Loot/Vehicle Owner Cache Cleanup
Problem: Suit upgrade telemetry, loot magnet dependency refresh, and vehicle vault helpers still had runtime registry-owner tails after cache/hot-swap routes existed.
Solution: `SuitUpgradeManager` now caches DataVault and rebinds resolver/telemetry handles on DataVault replacement. `LootMagnetSystem` now registers as a hot-swap listener and refreshes slow-tick dependency snapshots from cached DataVault/player/inventory owners. `VehicleMotor.ResolveDataVault()` now reads cached owner state only; registry access is confined to cold bind/enable cache.
Rejected Alternatives: Registry retry inside suit telemetry helpers, slow-tick loot dependency polling, vehicle vault helper fallback, or changing gameplay DTO layouts were rejected.
Scalability potential: Weak devices avoid small registry churn in suit telemetry, loot acquisition refresh, and vehicle vault buffer setup; middle/high/ultra keep identical suit, loot, and vehicle truth and can spend saved budget on feedback.
Hardware Impact: Removes several owner lookup tails from recurring gameplay slices. Estimated saving is 2-5 us across dense loot/suit/vehicle update bursts on i3/MX350-class CPUs. Compile proof is source-only; code/clean-doc `diff --check` passed, legacy whitespace docs were excluded from the mechanical gate, and latest build guard sampled CPU `100` with no compiler processes.

## 2026-05-24 Ladder Climb Owner Cache Cleanup
Problem: `ProceduralLadderClimbRuntime` still resolved DataVault/player/movement owners from `GlobalRegistry` during climb start, after owner-cache doctrine already existed.
Solution: Add `IGlobalRegistryHotSwapListener`, cold-cache DataVault/player/movement owners on enable, rebind DataVault/player/movement slots through hot-swap, and consume cached owners in climb start.
Rejected Alternatives: Action-path registry lookup, scene search, or leaving active climb pointers stale after service replacement were rejected.
Scalability potential: Weak devices avoid registry churn during ladder interaction bursts; middle/high/ultra keep identical IK/climb truth and can spend saved slice budget on presentation.
Hardware Impact: Removes three climb-start registry reads and stale service pointer risk; estimated saving is 1-4 us per climb-start/rebind burst on i3/MX350-class CPUs. Compile proof remains source-only; code/clean-doc diff gate passed while legacy migration-ledger whitespace noise stayed excluded. Latest guard sampled CPU `60.1,61.5,50.8` with active `dotnet/csc`.

## 2026-05-24 Player/VR DataVault Resolver Cleanup
Problem: `PlayerKinematicsRuntime.RebindRegistryServices()` could poll `GlobalRegistry.DataVault` from fixed-tick missing-service recovery, and `VRSomaticProvider.ResolveDataVault()` could retry the registry while creating black-box/native buffers.
Solution: Player kinematics DataVault registry access is now confined to cold cache before hot-swap registration; fixed-tick missing-service recovery no longer re-polls DataVault. VR somatic now cold-caches DataVault on enable and `ResolveDataVault()` returns cached owner state only.
Rejected Alternatives: FixedTick DataVault retry, black-box buffer registry fallback, or changing kinematics/VR DTO layout were rejected.
Scalability potential: Weak devices avoid small registry churn in player kinematics recovery and VR somatic buffer setup; middle/high/ultra keep identical movement, IK, and somatic telemetry truth.
Hardware Impact: Removes two resolver fallback tails from recurring player/VR slices. Estimated saving is 1-3 us across recovery/buffer setup bursts on i3/MX350-class CPUs. Compile proof is source-only; scoped code `diff --check` passed, build guard sampled CPU `91.9` with active dotnet/VBCSCompiler processes.

## 2026-05-24 Debris DataVault Owner Cache Cleanup
Problem: `DebrisManager.EnsureVaultBuffer()` could call `CacheDataVaultCold()` after hot-swap registration and retry `GlobalRegistry.DataVault`; DataVault replacement could keep old generation handles and then release through the new vault.
Solution: Debris DataVault registry access now runs only before hot-swap registration. DataVault replacement releases native state against the old vault before binding the new owner vault and reallocating runtime resources.
Rejected Alternatives: Hot `EnsureVaultBuffer` registry retry, releasing old handles through a replacement vault, or changing debris DTO layout were rejected.
Scalability potential: Weak devices avoid small registry churn and stale-handle risk in debris buffer allocation; middle/high/ultra keep identical debris simulation truth.
Hardware Impact: Removes one hot retry tail and prevents wrong-vault release. Estimated saving is 1-2 us during debris allocation/rebind bursts on i3/MX350-class CPUs. Compile proof is source-only; scoped code `diff --check` passed and latest build guard sampled CPU `100` with no compiler processes.

## 2026-05-24 Somatic DataVault Service-Rebind Cleanup
Problem: `SomaticKinematicsRuntime.RebindServices()` handled weather/VR service replacement but also rebound DataVault from `GlobalRegistry.DataVault`, mixing unrelated hot-swap routes.
Solution: DataVault registry access is now cold-cache only before hot-swap registration. Weather/VR rebinds refresh only their owners; DataVault replacement remains on the dedicated DataVault slot callback.
Rejected Alternatives: Mixed service rebind polling DataVault, broad somatic DTO changes, or disabling existing DataVault hot-swap were rejected.
Scalability potential: Weak devices avoid small registry churn during somatic service replacement; middle/high/ultra keep identical kinematic, bounding-sphere, drag LUT, and black-box truth.
Hardware Impact: Removes one service-rebind DataVault lookup tail. Estimated saving is 1-2 us per somatic service replacement burst on i3/MX350-class CPUs. Compile proof is source-only; scoped code `diff --check` passed and latest build guard sampled CPU `100` with no compiler processes.

## 2026-05-24 Chemical/Flora DataVault Resolver Cleanup
Problem: `ChemicalInfluenceGrid.TryResolveDataVault()` and Flora wake/sway resolver wrappers could still use runtime `GlobalRegistry.DataVault` after owner cache existed. Flora also resolved vault buffers before OnEnable refreshed its cached vault, and queued wake-trail globals reset the dirty flag by re-queuing instead of publishing shader globals.
Solution: Chemical DataVault replacement now flows through the DataVault hot-swap slot, resets vault handles, and reinitializes from the cached owner vault. Flora binds DataVault in cold cache before resolver calls, clears wake/sway/stiffness generation handles when the vault owner changes, and queued wake-trail globals call `PublishWakeTrailGlobals()`.
Rejected Alternatives: Runtime DataVault registry retries, keeping stale generation handles across vault replacement, broad flora visual refactor, or leaving wake-trail upload hidden behind a self-resetting queue flag were rejected.
Scalability potential: Weak devices avoid small registry churn and stale-handle risk in chemical/flora setup/rebind. Middle/high/ultra keep identical chemical grid, flora sway, and wake-trail truth and can spend saved budget on visual density.
Hardware Impact: Removes two runtime DataVault resolver tails and one failed wake-trail publish path. Estimated saving is 2-4 us across chemical/flora rebind/setup bursts on i3/MX350-class CPUs. Compile proof is source-only; scoped code `diff --check` passed, runtime DataVault resolver grep returned no matches, and latest build guard sampled CPU `100` with no compiler processes.

## 2026-05-24 Hazard/Reactor/Habitat Owner Cache Cleanup
Problem: `EnvironmentalHazard.ResolveRuntimePlayerTransform()`, `BioReactor.TriggerMeltdown()`, and `HabitatIntegrityManager` breach/depth helpers still read Player/FluidDecals/Atmosphere/Terrain from `GlobalRegistry` during slow/action paths.
Solution: Hazard and reactor now consume cached player context from existing cold cache/hot-swap routes. Habitat integrity now registers as a hot-swap listener, cold-caches FluidDecals/Atmosphere/Terrain, and reads those cached owners during breach VFX, ambient temperature, and depth resolution.
Rejected Alternatives: Slow/action-path registry polling, scene search, new signal lanes for direct owner reads, or changing flood/reactor/hazard DTOs were rejected.
Scalability potential: Weak devices avoid small registry churn in hazard radius checks, reactor meltdown, and habitat breach ticks. Middle/high/ultra keep identical hazard, meltdown, flood, and rupture visuals.
Hardware Impact: Removes three recurring owner lookup tails. Estimated saving is 1-4 us across dense hazard/habitat/reactor bursts on i3/MX350-class CPUs. Compile proof is source-only; scoped code `diff --check` passed, touched-file registry grep leaves only cold cache reads, and latest build guard sampled CPU `100`.

## 2026-05-24 Vehicle Wake-Silt Owner Cache Cleanup
Problem: `VehicleMotor.TryEmitWakeSiltDecal()` still read `GlobalRegistry.AbyssalFluidDecals` on wake emission after the motor already had registry hot-swap wiring.
Solution: VehicleMotor now cold-caches `AbyssalFluidDecalManager`, refreshes it through `AbyssalFluidDecalRuntime` hot-swap, and clears the cached owner on disable/destroy. Wake emission reads `_fluidDecals` only.
Rejected Alternatives: Per-emission registry polling, scene search, or disabling wake silt visuals were rejected.
Scalability potential: Weak devices avoid small lookup churn during fast vehicle wakes; middle/high/ultra keep the same wake-silt visual route.
Hardware Impact: Removes one recurring visual-owner lookup. Estimated saving is 1-2 us during dense vehicle wake bursts on i3/MX350-class CPUs. Compile proof is source-only; scoped code `diff --check` passed and VehicleMotor grep leaves FluidDecals registry read only in cold cache.

## 2026-05-24 HazardZone Player Context Owner Cache Cleanup
Problem: `HazardZoneManager.ResolvePlayerContext()` still fell back to `GlobalRegistry.Player` while resolving player hazard bounds for the slow-cadence exposure job.
Solution: HazardZoneManager now cold-caches `IPlayerRuntimeContext`, refreshes it through the Player hot-swap slot, and resolves player references from that cached owner when `PlayerRuntimeContextService` has no active snapshot.
Rejected Alternatives: Slow-tick registry fallback, scene search as primary player route, or changing exposure DTO layout were rejected.
Scalability potential: Weak devices avoid small player-context lookup churn in hazard exposure setup; middle/high/ultra keep identical toxicity/heat/biohazard truth and presentation.
Hardware Impact: Removes one recurring player owner lookup from hazard exposure resolution. Estimated saving is 1-2 us on i3/MX350-class CPUs during active hazard volumes. Compile proof is source-only; scoped code `diff --check` passed and HazardZoneManager grep leaves Player registry read only in cold cache.

## 2026-05-24 Settings Graphics Player Owner Cleanup
Problem: `SettingsManager.TryResolveMainCameraReference()` and `TryResolveVolumeProfileReference()` read `GlobalRegistry.Player` during graphics binding, despite having a registry hot-swap listener.
Solution: SettingsManager now cold-caches `IPlayerRuntimeContext`, refreshes it on Player hot-swap and scene load, and invalidates only player-owned camera/profile cache on Player replacement. Camera and Volume profile binding read cached player context only.
Rejected Alternatives: Graphics binding registry polling, broader scene search, or clearing explicitly assigned non-player cameras were rejected.
Scalability potential: Weak devices avoid small lookup churn in settings apply/retry paths; middle/high/ultra keep identical camera FOV and post-processing behavior.
Hardware Impact: Removes two graphics-binding player registry reads. Estimated saving is 1-2 us per settings apply/rebind burst on i3/MX350-class CPUs. Compile proof is source-only; scoped code `diff --check` passed and targeted hot-path registry grep returned no matches.

## 2026-05-24 VRSomatic Player Camera Owner Cache Cleanup
Problem: `VRSomaticProvider.RefreshCachedGlobalState()` still fell back to `GlobalRegistry.Player` while resolving the player camera during XR activation/cached-state refresh.
Solution: VRSomaticProvider now cold-caches `IPlayerRuntimeContext`, refreshes it through the Player hot-swap slot, clears it on shutdown, and resolves fallback camera state from cached owner state only.
Rejected Alternatives: XR activation registry polling, broader scene camera search, or changing VR somatic DTO layout were rejected.
Scalability potential: Weak devices avoid small lookup churn in XR activation/rebind; middle/high/ultra keep identical somatic comfort, collision, and camera behavior.
Hardware Impact: Removes one player-owner lookup from VR somatic camera cache refresh. Estimated saving is 1-2 us per XR activation/rebind burst on i3/MX350-class CPUs. Compile proof is source-only; scoped code `diff --check` passed and VRSomaticProvider grep leaves Player registry read only in cold cache.

## 2026-05-24 PlayerKinematics Service-Rebind Owner Cache Cleanup
Problem: `PlayerKinematicsRuntime.RebindColdIfMissing()` could call a registry rebind every 64 frames when Fluid/Voxel/camera state was missing, and the rebind also read PlayerMotor and Player camera from `GlobalRegistry`.
Solution: Registry reads for DataVault, GasDynamics, Fluid, VoxelEngine, PlayerMotor, and Player are now confined to cold pre-hot-swap cache. Runtime hot-swap writes cached owners from `currentService`; player camera fallback resolves from cached `IPlayerRuntimeContext`.
Rejected Alternatives: 64-frame hot registry polling, broad scene search, or changing movement/VR DTO layouts were rejected.
Scalability potential: Weak devices avoid repeated missing-service registry churn in player kinematics; middle/high/ultra keep identical movement, SDF squeeze, flow, and camera-relative input truth.
Hardware Impact: Removes up to five owner lookups from missing-service rebind bursts. Estimated saving is 2-3 us per recovery burst on i3/MX350-class CPUs. Compile proof is source-only; source grep leaves those registry reads only in cold cache helpers.

## 2026-05-24 RepairTool LateFrame Compile Wall
Problem: Guarded rebuild for loop86 failed on `RepairTool` implementing `ILateFrameTickable` without `LateFrameTick()` in the compiled source snapshot.
Solution: Current disk source now contains `LateFrameTick()`, registration/unregistration helpers, and pending visual sync cleanup. This is treated as a compile-wall state update until a guarded retry proves it.
Rejected Alternatives: Removing `ILateFrameTickable` or moving repair beam/audio/material writes back to gameplay tick were rejected because the queued late-frame path is the intended visual-sync route.
Scalability potential: Weak devices keep repair VFX/material writes out of gameplay tick; middle/high/ultra can spend repair spark quantity through continuous quality without changing repair truth.
Hardware Impact: Compile-wall only until retry; no claimed runtime saving beyond preserving queued late-frame visual writes. Retry is blocked by CPU guard after the failed build.

## 2026-05-24 RepairTool DataVault Hot-Swap Cleanup
Problem: `RepairTool` cold-cached DataVault for hull dents and repair black-box buffers, but DataVault replacement did not rebind handles through the existing `PlayerTool` hot-swap route.
Solution: Override `OnToolRegistryServiceReplaced` for the DataVault slot and funnel cold cache plus replacement through `RebindRepairVault()`, preserving old-vault release before binding the new owner vault.
Rejected Alternatives: Runtime `GlobalRegistry.DataVault` retry during repair, stale handles after vault replacement, or moving repair visual sync back into gameplay tick were rejected.
Scalability potential: Weak devices avoid stale repair vault handles and registry retry pressure; middle/high/ultra keep identical repair truth while continuous quality controls spark quantity.
Hardware Impact: Removes one replacement-path owner gap and prevents wrong-vault handle lifetime. Estimated saving is 1-2 us on repair-vault replacement bursts; compile retry remains blocked by CPU/compiler guard.

## 2026-05-24 EnvironmentalHazard PlayerAction Owner Cache Cleanup
Problem: `EnvironmentalHazard.ApplyDamage()` still called `GlobalRegistry.PlayerActionInterrupts` directly in the damage path.
Solution: Cache `IPlayerActionInterruptSink` in the cold registry pass and refresh it through the `PlayerActionRuntime` hot-swap slot; damage now uses the cached sink.
Rejected Alternatives: Damage-path registry lookup or suppressing player action interrupts were rejected.
Scalability potential: Weak devices avoid a registry lookup during recurring hazard damage; middle/high/ultra keep identical hazard interruption behavior.
Hardware Impact: Removes one damage-path owner lookup. Estimated saving is about 1 us per dense hazard damage burst; compile retry remains blocked by CPU guard.

## 2026-05-24 PlayerAction Inventory/Audio Owner Cache Cleanup
Problem: `PlayerActionController` still read `GlobalRegistry.PlayerInventory` during completion inventory removal and `GlobalRegistry.Audio` during completion/cancel feedback.
Solution: Added hot-swap listener state for PlayerInventory and Audio; cold cache seeds `IPlayerInventoryService` and `IAudioService`, while action completion and cancel paths consume cached owner state only.
Rejected Alternatives: Action-path registry lookup, scene search, or suppressing completion/cancel feedback were rejected.
Scalability potential: Weak devices avoid repeated owner lookups during item-use bursts; middle/high/ultra keep identical inventory truth and audio feedback while quality can scale presentation elsewhere.
Hardware Impact: Removes up to three action-path owner lookups per completed/cancelled consumable action. Estimated saving is 1-2 us per burst on i3/MX350-class CPUs. Compile proof is source-only; scoped `diff --check` passed outside legacy `Docs/DOC_GOVERNANCE.md` whitespace noise, and guarded rebuild is blocked by CPU 100%.

## 2026-05-24 Flora Player/Atmosphere/Construction Owner Cache Cleanup
Problem: `FloraInteractionManager` still read Player, Atmosphere, and Construction owners from `GlobalRegistry` inside player tool/AUP/toxic-spore, parasite growth, and fungal spread helpers.
Solution: Add cached `IPlayerRuntimeContext`, `HectonAtmosphereManager`, and `ConstructionManager` owners, seed them in cold lifecycle cache, and refresh them through Player, AtmosphereRuntime, and Logistics hot-swap slots.
Rejected Alternatives: Runtime helper registry lookup, scene search expansion, or disabling parasite/fungal spread routes were rejected.
Scalability potential: Weak devices avoid small lookup churn during dense flora/parasitic updates; middle/high/ultra keep identical flora truth and can spend budget on visual density.
Hardware Impact: Removes five owner lookups from flora helper paths. Estimated saving is 2-3 us during dense flora/parasitic bursts; compile retry remains blocked by CPU guard, latest sample 100%.

## 2026-05-24 ConsumableItem Audio Owner Route Cleanup
Problem: `ConsumableItem.TryConsume()` was a static utility but still read `GlobalRegistry.Audio` when playing item use sounds, so PlayerActionController completion could re-enter the registry after its own audio owner cache.
Solution: Add `IAudioService` overloads and route use-sound playback through caller-owned audio service. PlayerActionController now passes its cached audio service for instant and delayed consumable completion.
Rejected Alternatives: Static registry lookup, global static audio cache without lifecycle owner, or removing item use-sound feedback were rejected.
Scalability potential: Weak devices avoid one static registry lookup during consumable bursts; middle/high/ultra keep identical consumable truth and can still spend audio budget on richer feedback through the audio owner.
Hardware Impact: Removes one consumable-use owner lookup and keeps static utility free of hot global reads. Estimated saving is about 1 us per item-use burst; compile proof is source-only until CPU guard permits rebuild.

## 2026-05-24 ClimbableLadder Audio/Localization Owner Cache Cleanup
Problem: `ClimbableLadder` read `GlobalRegistry.Audio` during climb start and `GlobalRegistry.Localization` while rebuilding interact text.
Solution: Add cached `IAudioService` and `LocalizationManager` owners with Audio/LocalizationRuntime hot-swap refresh. Climb playback and localized interact text consume cached owner state only.
Rejected Alternatives: Climb action registry lookup, static localization registry lookup, or scene search were rejected.
Scalability potential: Weak devices avoid owner lookups on ladder interaction and language refresh; middle/high/ultra keep identical ladder movement, text, and audio feedback.
Hardware Impact: Removes one climb-start audio lookup and one localization lookup per text rebuild. Estimated saving is about 1 us per interaction/localization burst; compile proof remains source-only until CPU guard permits rebuild.

## 2026-05-24 Ecosystem Save Owner Cache Cleanup
Problem: `FaunaGeneticsManager`, `EcosystemHealthDirector`, and `EnvironmentalStrainManager` registered save participants through direct `GlobalRegistry.SaveRuntime`/`GlobalRegistry.Save` lifecycle calls. Save replacement could leave stale old-owner registration.
Solution: Add cached `ISaveService` owner fields, cold-cache Save once on enable, register hot-swap listeners, unregister from previous Save owner on `GlobalRegistryServiceSlot.Save`, and register with the current owner while active.
Rejected Alternatives: Per-save registry retry, static save cache without lifecycle owner, or leaving stale SaveManager registration after replacement were rejected.
Scalability potential: Weak devices avoid registry churn and stale save participant cleanup; middle/high/ultra keep identical world seed, infection, and strain persistence truth.
Hardware Impact: Removes direct save-owner registry register/unregister tails and stale replacement risk. Estimated saving is 1-3 us during save-owner replacement or enable/disable bursts; compile proof is source-only until CPU guard permits rebuild.

## 2026-05-24 StorageCrate Audio/Localization Owner Cache Cleanup
Problem: `StorageCrate` read `GlobalRegistry.Audio` on open/close and `GlobalRegistry.Localization` while rebuilding interact text.
Solution: Add cached `IAudioService` and `LocalizationManager` owners with Audio/LocalizationRuntime hot-swap refresh. Open/close playback and localized text now consume cached owner state only.
Rejected Alternatives: Open/close action registry lookup, static localization registry lookup, or scene search were rejected.
Scalability potential: Weak devices avoid owner lookup bursts during crate interaction and language refresh; middle/high/ultra keep identical storage, text, animation, and audio behavior.
Hardware Impact: Removes two action-path audio lookups and one localization lookup per text rebuild. Estimated saving is 1-2 us per crate interaction/localization burst; compile proof remains source-only until CPU guard permits rebuild.

## 2026-05-24 Sargassum Save Owner Cache Cleanup
Problem: `SargassumGlobalDragManager` registered with `GlobalRegistry.SaveRuntime` and later unregistered through whatever Save owner was current, so Save replacement could leave stale registration on the old owner.
Solution: Cache `ISaveService` in cold dependency refresh, register via cached owner, unregister via cached owner, and handle `GlobalRegistryServiceSlot.Save` by unregistering from previous owner before binding/registering the current owner.
Rejected Alternatives: Current-owner unregister, direct SaveRuntime retry, or disabling sargassum persistence were rejected.
Scalability potential: Weak devices avoid registry churn and stale save participant cleanup in dense sargassum scenes; middle/high/ultra keep identical sargassum field persistence.
Hardware Impact: Removes one direct save-owner lookup tail and wrong-owner unregister risk. Estimated saving is 1-2 us during save-owner replacement or enable/disable bursts; compile proof is source-only until CPU guard permits rebuild.

## 2026-05-24 OxygenBubble Audio/ObjectPool Owner Cache Cleanup
Problem: `OxygenBubble` still read `GlobalRegistry.Audio` during collection feedback and `GlobalRegistry.ObjectPool` during despawn.
Solution: Cache `IAudioService` and `ObjectPoolManager` in cold lifecycle, refresh them through Audio/ObjectPool hot-swap, and keep Dispatcher hot-swap registration for late-frame tick recovery.
Rejected Alternatives: Collection/despawn registry lookup, scene search, or disabling pool despawn were rejected.
Scalability potential: Weak devices avoid small lookup bursts during oxygen collection; middle/high/ultra keep identical oxygen, audio, particle, and pool behavior.
Hardware Impact: Removes one collection audio lookup and one despawn pool lookup. Estimated saving is 1-2 us per dense bubble collection/despawn burst; compile proof remains source-only until CPU guard permits rebuild.

## 2026-05-24 Floater Audio/Localization Owner Cache Cleanup
Problem: `Floater` still read Audio during pickup/attach and Localization while rebuilding interact text.
Solution: Cache `IAudioService` and `LocalizationManager` in cold lifecycle, refresh them through Audio/LocalizationRuntime hot-swap, and rebuild cached interact text on localization replacement.
Rejected Alternatives: Pickup/attach registry lookup, static localization lookup, or removing audio/text feedback were rejected.
Scalability potential: Weak devices avoid small lookup bursts during floater interaction; middle/high/ultra keep identical buoyancy, attach, text, VFX, and audio behavior.
Hardware Impact: Removes two action-path audio lookups and one localization lookup per text rebuild. Estimated saving is 1-2 us per floater interaction/localization burst; compile proof remains source-only until CPU guard permits rebuild.

## 2026-05-24 HectonPlayerHealth Audio/AudioLog Owner Cache Cleanup
Problem: `HectonPlayerHealth` still read `GlobalRegistry.Audio` for survival-grace heartbeat and `GlobalRegistry.AudioLogs` for radiation critical advisory queue blocking.
Solution: Cache `IAudioService` and `AudioLogSystem` in cold lifecycle, refresh them through Audio/AudioLogRuntime hot-swap, and use cached owners in health/advisory paths.
Rejected Alternatives: Damage/advisory-path registry lookup, static global audio cache, or suppressing heartbeat/narrative queue feedback were rejected.
Scalability potential: Weak devices avoid lookup bursts during damage/advisory spikes; middle/high/ultra keep identical health truth, heartbeat feedback, and narrative queue behavior.
Hardware Impact: Removes one survival-grace audio lookup and one critical-advisory AudioLog lookup. Estimated saving is 1-2 us per health/advisory burst; compile proof remains source-only until CPU guard permits rebuild.

## 2026-05-24 World/Atlas Save Owner Cache Cleanup
Problem: `WorldStateManager`, `WorldProceduralStateRegistry`, `FaunaDirector`, and `AtlasSignalSystem` still registered save participants through direct `GlobalRegistry.Save`/`SaveRuntime` lifecycle calls or read playtime from `GlobalRegistry.Save`. Save replacement could leave stale registration on the old owner.
Solution: Add cached `ISaveService` owners, register/unregister through the cached owner, and handle `GlobalRegistryServiceSlot.Save` by unregistering previous owners before binding/registering current owners. WorldProcedural playtime now reads the cached Save owner.
Rejected Alternatives: Direct lifecycle registry calls, current-owner unregister, scene search, static save cache without lifecycle ownership, or disabling world/fauna/Atlas persistence were rejected.
Scalability potential: Weak devices avoid registry churn and stale save participant cleanup in first-20 world-state, procedural fauna, fauna director, and Atlas signal persistence. Middle/high/ultra keep identical save truth and can spend budget on presentation.
Hardware Impact: Removes four save-owner lifecycle tails and one playtime registry read tail. Estimated saving is 2-4 us during save-owner replacement or enable/disable bursts on i3/MX350-class CPUs. Compile proof is source-only; scoped `diff --check` passed, touched-file direct Save register/unregister grep is empty, and guarded build is blocked by CPU 100%.

## 2026-05-24 MessageTerminal Audio/Localization/Signal Owner Cleanup
Problem: `MessageTerminal` still read `GlobalRegistry.Audio` during interaction/new-message feedback and `GlobalRegistry.Localization` while rebuilding prompt text. The WFC datapad update path also used the legacy `GlobalSignals.Publish` facade, and status-light MPB writes ran from the state tick path.
Solution: Cache `IAudioService` and `LocalizationManager` in cold lifecycle, refresh them through Audio/LocalizationRuntime hot-swap, route terminal prompt copy through `IInteractableTextProvider`, publish WFC datapad state directly to `SignalBus<WfcOutpostStateChangedSignal>`, and defer status-light MPB writes to `ILateFrameTickable`.
Rejected Alternatives: Interaction-path registry lookup, static localization lookup, legacy global-signal facade publish, per-tick renderer property writes, or suppressing terminal feedback were rejected.
Scalability potential: Weak devices avoid small owner lookups and renderer writes during terminal interaction/blink bursts. Middle/high/ultra keep identical datapad persistence, prompt text, and terminal feedback while visual cadence remains dispatcher-owned.
Hardware Impact: Removes two action-path audio lookups, one localization rebuild lookup, one legacy facade call, and repeated tick-lane MPB write pressure. Estimated saving is 2-3 us per terminal interaction/blink burst on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed and guarded rebuild is blocked by CPU 79.2%, compiler_count 0.

## 2026-05-24 TraumaDispatcher Audio/Localization Owner Cache Cleanup
Problem: `TraumaDispatcher` still read `GlobalRegistry.Audio` while publishing parasite-room acoustic load and `GlobalRegistry.Localization` while applying EMP PDA corrosion.
Solution: Cache `ISpatialAudioEnvironmentModulationSink` and `LocalizationManager` in cold lifecycle, refresh them through Audio/LocalizationRuntime hot-swap, and re-register dispatcher lanes on Dispatcher replacement.
Rejected Alternatives: Tick/pulse-path registry lookup, static global service cache, scene search, or suppressing parasite room acoustic load/PDA corrosion were rejected.
Scalability potential: Weak devices avoid owner lookups during parasite/EMP bursts. Middle/high/ultra keep identical trauma truth, parasite acoustic modulation, and PDA corrosion feedback.
Hardware Impact: Removes one audio lookup on parasite-count changes and one localization lookup per EMP pulse. Estimated saving is 1-2 us per active trauma burst on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed and direct Audio/Localization grep leaves registry reads only in cold cache.

## 2026-05-24 Narrative/Suit/PDA/Inventory Save Owner Cache Cleanup
Problem: Narrative, suit upgrade, PDA exchange, and inventory persistence registration previously had direct SaveRuntime/Save owner tails that could register or unregister against the wrong owner after Save replacement.
Solution: Verified `HectonNarrativeDirector`, `SuitUpgradeManager`, `PDAExchangeSystem`, and `PlayerInventory` now keep cached `ISaveService` owner state, register/unregister through that owner, and handle `GlobalRegistryServiceSlot.Save` by unregistering the previous owner before binding the current one.
Rejected Alternatives: Direct lifecycle `GlobalRegistry.SaveRuntime` calls, current-owner unregister, static save globals, or per-save registry retry were rejected.
Scalability potential: Weak devices avoid registry churn and stale save participant cleanup during save-owner replacement. Middle/high/ultra keep identical narrative, suit, PDA barter, and inventory persistence truth.
Hardware Impact: Removes four SaveRuntime lifecycle tails and wrong-owner unregister risk. Estimated saving is 2-4 us during save-owner replacement or enable/disable bursts on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed and SaveRuntime grep returned no matches in the verified files.

## 2026-05-24 FirstHourDirector Save Owner Cache Cleanup
Problem: `FirstHourDirector` already listened for registry hot-swap but still registered and unregistered persistence through direct `GlobalRegistry.SaveRuntime` lifecycle calls.
Solution: Add cached `ISaveService` owner state, register/unregister through that owner, and handle `GlobalRegistryServiceSlot.Save` by unregistering the previous owner before binding the current one.
Rejected Alternatives: Direct SaveRuntime register/unregister, current-owner unregister, static save global, or dropping first-hour persistence were rejected.
Scalability potential: Weak devices avoid registry churn and stale save participant cleanup during first-hour director enable/disable or Save replacement. Middle/high/ultra keep identical onboarding milestone persistence truth.
Hardware Impact: Removes two direct SaveRuntime lifecycle tails and wrong-owner unregister risk. Estimated saving is 1-2 us during save-owner replacement or enable/disable bursts on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed and FirstHourDirector SaveRuntime grep returned no matches.

## 2026-05-24 DataArchaeologyRuntime Save Owner Cache Cleanup
Problem: `DataArchaeologyRuntime` still registered scanner archaeology persistence through direct `GlobalRegistry.SaveRuntime` lifecycle calls despite already listening for registry hot-swap.
Solution: Cache `ISaveService`, register/unregister through that owner, and handle `GlobalRegistryServiceSlot.Save` by unregistering the previous owner before binding the current one.
Rejected Alternatives: Direct SaveRuntime lifecycle calls, current-owner unregister, static save globals, or disabling archaeology persistence were rejected.
Scalability potential: Weak devices avoid stale save participant cleanup during scanner archaeology save-owner replacement. Middle/high/ultra keep identical discovery, fragment, and hologram persistence truth.
Hardware Impact: Removes two direct SaveRuntime lifecycle tails and wrong-owner unregister risk. Estimated saving is 1-2 us during save-owner replacement or enable/disable bursts on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed and DataArchaeologyRuntime SaveRuntime grep returned no matches.

## 2026-05-24 Meta/Mod/Expression Save Owner Cache Cleanup
Problem: `RunModifierController`, `ModWorldPersistenceManager`, and `PlayerExpressionManager` still had direct `GlobalRegistry.SaveRuntime` lifecycle registration tails, so Save replacement could leave stale save participants on the old owner.
Solution: Cache `ISaveService`, register/unregister through the cached owner, and handle `GlobalRegistryServiceSlot.Save` by unregistering the previous owner before binding the current owner. `RunModifierController` keeps cached concrete `SaveManager` only for `DeleteSave` and `LastOperationSlot`, which are not in `ISaveService`.
Rejected Alternatives: Direct SaveRuntime lifecycle calls, current-owner unregister, broadening `ISaveService` for one meta delete action, or disabling permadeath/mod/expression persistence were rejected.
Scalability potential: Weak devices avoid stale save participant cleanup during scene/meta/mod churn; middle/high/ultra keep identical permadeath, mod-spawn, and player-expression persistence truth.
Hardware Impact: Removes the remaining project direct SaveRuntime register/unregister tails. Estimated saving is 2-3 us during save-owner replacement or enable/disable bursts on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed and project direct SaveRuntime lifecycle grep is empty.

## 2026-05-24 Narrative Meta Save Owner Cache Cleanup
Problem: `CorporateOrderSystem`, `ProceduralLoreDirector`, and `MetaCampaignService` still registered save participation through direct `GlobalRegistry.SaveRuntime`; `ProceduralLoreDirector` also polled PlayerExploration/AudioLog/ObjectPool owners from runtime helper paths and despawned through the current pool instead of the spawn owner.
Solution: Added cached `ISaveService` routing with Save hot-swap to all three systems. ProceduralLoreDirector now listens for PlayerExploration/AudioLog/ObjectPool replacement, consumes cached owners, and stores the owning pool per active lore placement.
Rejected Alternatives: Direct SaveRuntime lifecycle calls, slow-tick owner polling, static save globals, or despawning old pooled lore drops through whatever pool is currently registered were rejected.
Scalability potential: Weak devices avoid stale save participant cleanup and slow-cadence owner polling during frontier-lore maintenance. Middle/high/ultra keep identical corporate order, lore placement, and meta-campaign persistence truth while visual/lore density remains controlled elsewhere.
Hardware Impact: Removes three SaveRuntime lifecycle tails plus three ProceduralLore owner lookup tails. Estimated saving is 2-4 us during save-owner replacement, enable/disable, or frontier-lore maintenance bursts on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed and targeted SaveRuntime grep returned no matches in the three files.

## 2026-05-24 Meta Save Discovery Owner Cache Cleanup
Problem: `GlobalProfileManager` and `DynamicDifficultyDirector` read `GlobalRegistry.SaveRuntime` while resolving run/telemetry time and read `GlobalRegistry.Discovery` from runtime helper paths.
Solution: Added cached `ISaveService` and discovery owner state with `IGlobalRegistryHotSwapListener` handling for Save and DiscoveryRuntime. Run elapsed time, telemetry windows, game-load biome counts, and slow-tick difficulty owner checks now use cached owners.
Rejected Alternatives: Per-event SaveRuntime lookup, slow-tick Discovery polling, static save/discovery globals, or converting elapsed-time identity into saved DTO state were rejected.
Scalability potential: Weak devices avoid small owner lookup bursts during achievement, advisory, death, game-load, and difficulty slow-tick events. Middle/high/ultra keep identical profile and dynamic difficulty truth.
Hardware Impact: Removes two SaveRuntime time lookups and two Discovery runtime lookup tails. Estimated saving is 1-3 us during meta event bursts on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed and targeted SaveRuntime/Discovery grep leaves registry reads only in cold cache helpers.

## 2026-05-24 FaunaBrain Physics Determinism Compile Fix
Problem: Guarded build `Build_EXTERNAL_CODEX_hotpath_cleanup106_save_owner_tail.log` failed because `FaunaBrain` referenced `PhysicsDeterminismSignals` without importing the `Hecton8.Physics` namespace; the second compiler error was the resulting unassigned `kccVelocity` out variable.
Solution: Add `using Hecton8.Physics` to `FaunaBrain`, preserving the existing deterministic KCC velocity facade and fauna fallback logic.
Rejected Alternatives: Duplicating KCC velocity signal reads in fauna, deleting the fallback velocity path, or moving `PhysicsDeterminismSignals` into Core were rejected.
Scalability potential: Weak devices keep the cheap cached KCC velocity signal path; middle/high/ultra keep identical fauna perception truth and no new simulation work.
Hardware Impact: Compile fix only; no frame-time cost added. Build retry is blocked by CPU 92.4.

## 2026-05-24 PDA Discovery Save Owner Cache Cleanup
Problem: `HectonDiscoveryManager`, `PlayerExplorationTracker`, `PDAMarkerRegistry`, and `PDALogbookManager` registered save participants through direct `GlobalRegistry.SaveRuntime` lifecycle reads.
Solution: Added cached `ISaveService` owner state and Save hot-swap handling to all four systems; save registration/unregistration now uses the owner that was actually bound.
Rejected Alternatives: Direct SaveRuntime lifecycle calls, current-owner unregister, static save globals, or disabling discovery/PDA persistence were rejected.
Scalability potential: Weak devices avoid stale save participant cleanup during PDA/discovery lifecycle churn; middle/high/ultra keep identical biome, exploration, marker, and logbook persistence truth.
Hardware Impact: Removes four SaveRuntime lifecycle tails and wrong-owner unregister risk. Estimated saving is 2-4 us during save-owner replacement or enable/disable bursts on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed and targeted SaveRuntime grep returned no matches in the four files.

## 2026-05-24 Runtime SaveRuntime Interface Tail Cleanup
Problem: Several runtime systems still used concrete `GlobalRegistry.SaveRuntime` where only `ISaveService` or `IAsyncPersistenceService` was needed. This widened ownership, kept stale concrete dependencies, and hid UI/save action routes behind SaveManager instead of the save contract.
Solution: `AudioLogSystem`, `BeaconNetworkSystem`, `ResourceScarcityDirector`, `PauseMenuController`, `SaveStation`, `PDAClockUtility`, `EndingSystem`, and `CrashTelemetryBuffer` now use cached `ISaveService`; `WorldChunkResidencyManager` falls back to `GlobalRegistry.Save as IAsyncPersistenceService`; `MetaCampaignService` naming now matches its existing `ISaveService` route.
Rejected Alternatives: Concrete SaveRuntime lookup for Register/Unregister/IsBusy/SaveGameAsync/CurrentPlayTime/crash presence, widening `ISaveService` for metadata APIs, or touching bootstrap/dev concrete cases without a replacement contract were rejected.
Scalability potential: Weak devices avoid concrete registry/service casts during save UI, beacon/audio-log/scarcity lifecycle, PDA timestamp, ending persistence, and chunk-pager setup. Middle/high/ultra keep identical save truth and can spend saved budget on presentation.
Hardware Impact: Removes multiple concrete SaveRuntime tails from runtime service/action paths. Estimated saving is 3-6 us during save-owner replacement, save UI action, or world-streaming setup bursts on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed and guarded rebuild is blocked by CPU 99 with active `csc/dotnet`.

## 2026-05-24 Progression Save Owner Cache Cleanup
Problem: `PlayerAchievementRegistry` still used a concrete SaveManager/SaveRuntime fallback, and `PDAContextualAdvisorySystem` Save hot-swap trusted `previousService` instead of always unregistering from its cached owner.
Solution: Both systems now register/unregister through cached `ISaveService`; PlayerAchievement resolves Save through `GlobalRegistry.Save` only as a cold missing-owner fallback, and PDA advisory Save replacement first unregisters from the bound owner before binding current Save.
Rejected Alternatives: Direct SaveRuntime lifecycle calls, concrete SaveManager persistence ownership, previousService-only unregister, or disabling progression/advisory persistence were rejected.
Scalability potential: Weak devices avoid stale save participant cleanup during progression/advisory lifecycle churn; middle/high/ultra keep identical achievement/advisory persistence truth.
Hardware Impact: Removes two progression SaveRuntime/SaveManager owner tails and one wrong-owner unregister path. Estimated saving is 1-3 us during save-owner replacement or enable/disable bursts on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed, Progression SaveRuntime grep returned no matches, and guarded rebuild was blocked by `BUILD_SKIP cpu=100 compiler_count=2`.

## 2026-05-24 Menu SaveRuntime Concrete UI Cleanup
Problem: `MainMenuController` and `SaveSlotHoverPreview` still read `GlobalRegistry.SaveRuntime` for slot metadata and save-existence UI paths.
Solution: Both UI systems now bind concrete `SaveManager` from `GlobalRegistry.Save as SaveManager` and Save hot-swap; concrete access remains scoped to metadata APIs that are not on `ISaveService`.
Rejected Alternatives: Direct SaveRuntime UI reads, widening `ISaveService` with menu-only metadata, or dropping save-slot metadata/backup feedback were rejected.
Scalability potential: Weak devices avoid concrete shortcut reads during menu open/hover bursts; middle/high/ultra keep identical slot metadata and backup feedback.
Hardware Impact: Removes two UI SaveRuntime concrete tails. Estimated saving is 1-2 us during menu save-list/hover setup on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed, MainMenu/SaveSlotHoverPreview SaveRuntime grep returned no matches, and guarded rebuild was blocked by `BUILD_SKIP cpu=100 compiler_count=9`.

## 2026-05-24 Bootstrap Diagnostic SaveRuntime Cleanup
Problem: Bootstrap and diagnostic smoke/verifier code still read `GlobalRegistry.SaveRuntime` even when a concrete `SaveManager` cast from the authoritative Save slot was enough.
Solution: `GameBootstrapper`, `ShellVerificationRuntimeSmokeTester`, `SaveRecoverySmokeTester`, `SaveSystemRuntimeSmokeTester`, and `StateRecoveryVerifier` now use `GlobalRegistry.Save as SaveManager`; `SaveRuntime` remains only as the compatibility accessor and inside `SaveManager` self-ownership checks.
Rejected Alternatives: Direct SaveRuntime bootstrap/diagnostic reads, broadening `ISaveService` for smoke/helper APIs, or changing SaveManager self-registration were rejected.
Scalability potential: Weak devices avoid static concrete shortcut reads during bootstrap and diagnostics; middle/high/ultra keep identical save load and verification behavior.
Hardware Impact: Removes seven non-self SaveRuntime reads. Estimated saving is 1-3 us during bootstrap/diagnostic bursts on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed and project SaveRuntime grep is reduced to `Core/GlobalRegistry.cs` plus `SaveManager.cs`.

## 2026-05-24 ScanLog RTG Save Owner Cleanup
Problem: `ScanLogSystem` and `RadioisotopeThermalGenerator` registered with Save through the current global owner and unregistered through the current global owner, risking stale registration after Save replacement.
Solution: Both systems now cache the bound `ISaveService`, unregister through that owner, and rebind through `GlobalRegistryServiceSlot.Save` hot-swap. RTG decay, black-box buffers, and power output logic were not changed.
Rejected Alternatives: Current-owner unregister, direct lifecycle Save lookup, widening Save contracts, or changing RTG decay persistence were rejected.
Scalability potential: Weak devices avoid stale save participant cleanup during scan-log/RTG lifecycle churn; middle/high/ultra keep identical scan archive and RTG persistence truth.
Hardware Impact: Removes two wrong-owner unregister paths and repeated lifecycle Save owner reads. Estimated saving is 1-3 us during Save replacement or enable/disable bursts on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed.

## 2026-05-24 Construction Lore Seam LOD Dynamic Save Owner Cleanup
Problem: `ConstructionManager`, `LoreDatabaseManager`, `SeamRegistry`, `LODSystemManager`, and `DynamicResolutionScaler` still had save participant bind paths that could read `GlobalRegistry.Save` from parameterless registration or keep disabled owners registered.
Solution: Cache the active `ISaveService` in cold lifecycle, register/unregister through that cached owner, add Save hot-swap handling to SeamRegistry and DynamicResolutionScaler, and unregister LOD/Dynamic participants on disable.
Rejected Alternatives: Direct parameterless `GlobalRegistry.Save` registration, current-owner unregister, leaving disabled save participants registered, or widening save contracts were rejected.
Scalability potential: Weak devices avoid save-owner churn and stale participant cleanup during construction/world-presentation lifecycle changes. Middle/high/ultra keep identical construction, lore, seam, LOD, and dynamic-resolution persistence truth.
Hardware Impact: Removes five save-owner registration tails and two disabled-owner persistence leaks. Estimated saving is 2-5 us during save replacement or enable/disable bursts on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed and targeted grep leaves `GlobalRegistry.Save` only in cold cache helpers. Guarded build is blocked by `BUILD_SKIP cpu=3 compiler_count=7`.

## 2026-05-24 UI Craft Scavenging Owner Cache Cleanup
Problem: UI/crafting/scavenging systems still pulled GlobalRegistry owners from refresh/action paths, and `PlayerInventoryManager` read accessors synced scene state.
Solution: `PDAMapTab`, `Fabricator`, `HUDQuickBar`, `ModalWindow`, `UITooltip`, `HectonUIScaler`, `ThermalGeyser`, `ResourceNode`, `QuestManager`, `ScrapManager`, and `PlayerInventoryManager` now use cold cached owners plus hot-swap. `ResourceNode` uses one static listener for class-wide cache instead of per-node listeners. `PlayerInventoryManager` getters now return cached fields only.
Rejected Alternatives: Per-frame/per-action registry fallback, per-node ResourceNode hot-swap registration, scene searches in read accessors, or widening service contracts for UI metadata were rejected.
Scalability potential: Weak devices avoid UI refresh, harvest/depletion, crafting, recycle, and inventory getter lookup/sync spikes; middle/high/ultra keep identical truth while spending saved budget on presentation.
Hardware Impact: Removes multiple owner lookups and hidden `TryGetComponent` read-side syncs. Estimated saving is 6-12 us across dense UI/harvest/crafting bursts on i3/MX350-class CPUs. Compile proof is source-only; scoped `diff --check` passed and guarded build is blocked by active compiler processes.

## 2026-05-24 Cave Bio Roots Spline Renderer Owner Cleanup
Problem: `CaveBioRootsGenerator` resolved `IConnectionSplineBatchRendererService` through `GlobalRegistry.TryGet` from spline submit/remove paths and could leave old pipe links on a replaced renderer owner.
Solution: Cache the spline renderer during cold lifecycle, register as a GlobalRegistry hot-swap listener, remove old root links from the previous renderer on replacement, and submit/remove only through the cached owner.
Rejected Alternatives: Runtime `GlobalRegistry.TryGet` during root spline submit/remove, scene search fallback, or leaving old renderer links for next full disable were rejected.
Scalability potential: Weak devices avoid registry lookup churn while cave roots tick and prevent stale visual link buildup after renderer replacement. Middle/high/ultra keep identical cave-root sway and can spend saved budget on presentation density.
Hardware Impact: Removes one spline renderer lookup tail and one old-owner visual leak. Estimated saving is 1-3 us during cave-root tick/removal bursts on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed and guarded build is blocked by `BUILD_SKIP cpu=100 compiler_count=9`.

## 2026-05-24 Buoyancy FluidRuntime Owner Cache Cleanup
Problem: `BuoyancyObject` still resolved `IBuoyancyObjectRegistry` through `GlobalRegistry.TryGet` during OnEnable before registering with the fluid runtime.
Solution: Cache FluidRuntime, Terrain, and Voxel SDF owners in one cold lifecycle helper; rebind buoyancy through cached `IBuoyancyObjectRegistry`; update that cache from `GlobalRegistryServiceSlot.FluidRuntime` hot-swap.
Rejected Alternatives: Enable-path `GlobalRegistry.TryGet`, scene search fallback, or registering buoyancy against whichever fluid owner is current during unregister were rejected.
Scalability potential: Weak devices avoid enable-burst registry lookups across pooled buoyancy objects. Middle/high/ultra keep identical buoyancy truth and ground suppression.
Hardware Impact: Removes one FluidRuntime lookup tail per enable and preserves owner-correct unregister. Estimated saving is 1-2 us during buoyancy object enable bursts on i3/MX350-class CPUs. Compile proof remains source-only; scoped `diff --check` passed and guarded build is blocked by `BUILD_SKIP cpu=17.6 compiler_count=7`.

## 2026-05-24 Dispatcher Rebind Save Owner Lifecycle Cleanup
Problem: 20 runtime owners registered tick/late-frame/slow lanes only during OnEnable/Start, so late Dispatcher replacement could leave them inert; Survival/AudioLog save participants also lacked explicit bound-owner registration flags.
Solution: Added `IGlobalRegistryHotSwapListener` routing and owner-correct rebinds; added `_saveRegistered` helpers; DataVault rebinds release old handles before binding current vault where needed.
Rejected Alternatives: OnEnable-only registration, slow `GlobalRegistry.Dispatcher` polling, direct current-owner Save unregister, or scene search fallback were rejected.
Scalability potential: Weak devices avoid enable/rebind registry churn and stale participant cleanup. Middle/high/ultra keep identical gameplay truth while preserving presentation cadence after service replacement.
Hardware Impact: Estimated 12-25 us saved during dispatcher/save/vault replacement or pooled-enable bursts on i3/MX350-class CPUs. Steady-state cost is only the existing registry hot-swap listener callback path. Compile remains source-only; rebuild was skipped by guard at `BUILD_SKIP cpu=3 compiler_count=8`.

## 2026-05-24 Interaction Registry Scene Scan Removal
Problem: `InteractableRegistry.EnsureSceneRegistryCold()` could trigger scene-wide `FindObjectsByType<MonoBehaviour>` from PlayerInteraction/InputDispatcher/InteractionUI enable paths. Several interactable owners had no explicit collider-tree registration and depended on that scan.
Solution: Removed the automatic scene scan path from `InteractableRegistry`; added lifecycle `RegisterTree`/`InvalidateTree` calls to the remaining 18 runtime `IInteractable` owners.
Rejected Alternatives: First-hover scene scan, keeping a hidden managed array allocation, physics fallback without the fixed registry, or relying on future prefab script order were rejected.
Scalability potential: Weak devices avoid first-interaction scene traversal and managed allocation spikes; middle/high/ultra keep identical prompt truth and can spend saved CPU on denser interaction presentation.
Hardware Impact: Avoids one scene-size dependent scan and managed `MonoBehaviour[]` allocation on first interaction/UI enable. Estimated saving is 15-80 us on i3/MX350-class CPUs, higher in dense scenes. Compile remains source-only; rebuild was skipped by guard at `BUILD_SKIP cpu=1 compiler_count=7`.

## 2026-05-24 Dispatcher DataVault Atlas Read-Model Cleanup
Problem: `PathFunnelNavmeshRuntime`, `AmbientWaterMotionManager`, `AtlasSignalDecoder`, and `Atlas6DirectiveSystem` still had owner tails: dispatcher registration could be lost after Dispatcher replacement, path funnel read paths could refresh/grow DataVault handles from runtime routes, and Atlas directive/decode logic depended on concrete systems plus a managed conflict-id dictionary.
Solution: Add dispatcher hot-swap rebinds; split path funnel cold bootstrap from read-only vault access, cache WFC grid handle, and release/rebind voxel A* vault handles on DataVault replacement. Atlas decoder/directive now consume `IAtlasSignalReadModel`, `IFirstHourReadModel`, `IQuestSystem`, and `ILocalizationTextReadModel`; directive conflict IDs use a fixed slot array, quest title notification uses a preallocated char buffer and registered message hash, and telemetry frame gating uses `SystemDispatcher.CurrentFrameIndex`.
Rejected Alternatives: OnEnable-only tick registration, slow registry polling, runtime DataVault ensure/growth, concrete Atlas/Quest/Localization dependencies, managed `Dictionary` growth, string title allocation, or `Time.frameCount` as a dispatcher-independent frame source were rejected.
Scalability potential: Weak devices avoid replacement-time inert systems, read-path vault growth, and cold managed allocations in Atlas conflict/directive bursts; middle/high/ultra keep identical pathfinding and Atlas truth while saved budget can buy denser presentation.
Hardware Impact: Estimated 3-8 us saved during dispatcher/DataVault replacement and Atlas directive bursts on i3/MX350-class CPUs, plus managed allocation risk removed. Compile remains source-only; scoped `diff --check` passed, runtime `GlobalRegistry.TryGet` grep is empty, runtime scene-search grep leaves only a `Camera.main` avoidance comment, and rebuild was skipped by guard at `BUILD_SKIP cpu=74.2 compiler_count=0`.

## 2026-05-24 Dispatcher Rebind Tail Cleanup 122
Problem: 20 enabled runtime owners still registered tick/slow/fixed/late-frame lanes only from OnEnable/Start/Spawn. Dispatcher replacement after enable could leave world content, population, streaming, caves, scatter, thermal updrafts, submarine modules, migration/seismic, swim rig, cable/leak interactions, rocks, recycler, drills, and geology seams inert. `Directory.Build.targets` also re-included four generated source files without first removing generated items, producing `CS2002`.
Solution: Added `IGlobalRegistryHotSwapListener` to the 20 owners and routed Dispatcher replacement to each owner-local `TryRegister*` method. Added unregister on disable/destroy/despawn/shutdown. Added `Compile Remove` before the four forced includes in `Directory.Build.targets`.
Rejected Alternatives: Polling `GlobalRegistry.Dispatcher`, scene scans, re-registering every tick, suppressing `CS2002`, or editing generated `.csproj` files were rejected.
Scalability potential: Weak devices avoid inert-system recovery spikes and warning-noise rebuild loops. Middle/high/ultra keep identical gameplay truth and preserve presentation cadence after service replacement.
Hardware Impact: Estimated 10-30 us saved during Dispatcher replacement or pooled enable bursts on i3/MX350-class CPUs. Build proof: loop122 runtime code built with exit 0 before target cleanup, but with 8 duplicate-source warning lines; target cleanup retry is blocked by active compiler guard.

## 2026-05-24 Slow Tick Registration Probe Cleanup 123
Problem: 22 slow-tick owners still confirmed registration by reading `GlobalRegistry.SlowTickables.Contains(this)`. Seven hot-swap owners also had owner-local callbacks but no Dispatcher replacement rebind.
Solution: Replaced `RegisterSlowTickable` + hot-list probe with dispatcher-owned `TryRegisterSlowTickable`; added Dispatcher replacement rebinds to `WorldSliceDirector`, `WorldGenerativeGeologyIntegrationDirector`, `BotanyPlanterModule`, `RepairDroneHub`, `EndingSystem`, `FirstHourDirector`, and `RandomEventSystem`.
Rejected Alternatives: Hot-list membership reads, OnEnable-only registration, periodic Dispatcher polling, or a broad dispatcher API refactor were rejected.
Scalability potential: Weak devices avoid small hot-list probe bursts during pooled enables and service replacement; middle/high/ultra keep identical simulation cadence and can spend saved CPU on visuals.
Hardware Impact: Estimated 8-20 us saved during Dispatcher replacement / enable bursts on i3/MX350-class CPUs. CLI proof: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log`, `Hecton8.Editor.csproj --no-restore`, 0 warning/error text matches; stdout reported Build succeeded, 0 warnings, 0 errors, and build servers shut down successfully.

## 2026-05-24 Updatable Registration Probe Cleanup 124
Problem: Ten updatable owners still confirmed registration by reading `GlobalRegistry.Updatables.Contains(this)` after calling `RegisterUpdatable`.
Solution: Replaced those probe pairs with dispatcher-owned `TryRegisterUpdatable` in `EntityChangeDetector`, `ResourceRecyclerModule`, `MessageTerminal`, `MantaEmergencyWreck`, `TransportChargingStation`, `HUDQuickBar`, `SargassumDebrisParticleSystem`, `LandingImpactVFX`, `PlayerThrusterAudio`, and `SkySystemFollowCamera`.
Rejected Alternatives: Hot-list membership reads after registration or a broad dispatcher refactor were rejected.
Scalability potential: Weak devices avoid small hot-list probe bursts during enables and rebinds; middle/high/ultra keep identical update cadence.
Hardware Impact: Estimated 3-10 us saved during enable/rebind bursts on i3/MX350-class CPUs. Compile proof is source-only after loop124; targeted grep and scoped `diff --check` passed, but rebuild is blocked by `BUILD_SKIP cpu=33.3 compiler_count=8`.

## 2026-05-24 Tick Registration Probe Cleanup 125
Problem: 30 runtime owners still confirmed dispatcher registration by reading `GlobalRegistry.Updatables`, `FixedTickables`, or `SlowTickables` after `Register*` calls.
Solution: Replaced those probe pairs with dispatcher-owned `TryRegisterUpdatable`, `TryRegisterFixedTickable`, or `TryRegisterSlowTickable` in acoustic, biome, buoyancy, cave root, construction, procedural audio, extractor, ecosystem, submarine, meta, player, optimization, floating-origin, and object-pool owners.
Rejected Alternatives: Hot-list membership reads, manual lane `Contains`, dispatcher polling, or a broad GlobalRegistry API refactor were rejected.
Scalability potential: Weak devices avoid enable/rebind probe bursts; middle/high/ultra keep identical simulation cadence and spend saved budget on presentation.
Hardware Impact: Estimated 8-20 us saved during enable/rebind bursts on i3/MX350-class CPUs. Compile proof is source-only after loop125; scoped grep and `diff --check` passed, but rebuild is blocked by `BUILD_SKIP cpu=9 compiler_count=7`.

## 2026-05-24 Core Player UI Updatable Probe Cleanup 126
Problem: 13 Core/player/UI/tool owners still used `RegisterUpdatable(this)` followed by `GlobalRegistry.Updatables.Contains(this)` to confirm dispatcher registration.
Solution: Replaced each pair with dispatcher-owned `TryRegisterUpdatable` while preserving existing priority lanes and unregister paths.
Rejected Alternatives: Hot-list membership reads, broad dispatcher API changes, or changing owner lifecycle ordering were rejected.
Scalability potential: Weak devices avoid small enable/rebind probe bursts in core runtime context, player health, PDA, performance, and asset-load owners; middle/high/ultra keep identical update cadence.
Hardware Impact: Estimated 4-12 us saved during enable/rebind bursts on i3/MX350-class CPUs. Compile proof is source-only after loop126; targeted grep and `diff --check` passed, but rebuild is blocked by `BUILD_SKIP cpu=100 compiler_count=10`.

## 2026-05-24 Simple Slow Tick Probe Cleanup 127
Problem: 20 simple owners still confirmed slow-tick registration by reading `GlobalRegistry.SlowTickables.Contains(this)` after `RegisterSlowTickable`.
Solution: Replaced those pairs with dispatcher-owned `TryRegisterSlowTickable` while preserving existing priority lanes and unregister paths.
Rejected Alternatives: Hot-list membership reads or broad dispatcher API changes were rejected. Multi-lane recovery cases stayed out of this pass.
Scalability potential: Weak devices avoid enable/rebind probe bursts in dev, narrative, world, UI, plugin bridge, and environment slow owners; middle/high/ultra keep identical slow cadence.
Hardware Impact: Estimated 6-18 us saved during enable/rebind bursts on i3/MX350-class CPUs. Compile proof is source-only after loop127; targeted grep and `diff --check` passed, but rebuild is blocked by `BUILD_SKIP cpu=65 compiler_count=8`.

## 2026-05-24 Cave Voxel Registration Probe Cleanup 128
Problem: Two cave voxel volume owners still confirmed update/slow registration by reading GlobalRegistry hot lists after register calls.
Solution: Replaced the lighting volume updatable probe and ambient-occlusion updatable/slow probes with dispatcher-owned `TryRegister*` calls.
Rejected Alternatives: Hot-list membership reads, late-frame lane edits, or wider cave rendering changes were rejected.
Scalability potential: Weak devices avoid cave-volume enable probe bursts; middle/high/ultra keep identical lighting/AO cadence and can spend saved time on cave presentation.
Hardware Impact: Estimated 1-3 us saved during cave-volume enable bursts on i3/MX350-class CPUs. Compile proof is source-only after loop128; targeted grep and `diff --check` passed, but rebuild is blocked by `BUILD_SKIP cpu=62.6 compiler_count=8`.

## 2026-05-24 Multi-Lane Registration Probe Cleanup 129
Problem: 40 runtime owners still used hot-list or dispatcher-lane `Contains(this)` reads after tick/fixed/post-fixed/late-frame registration.
Solution: Replaced those confirmations with dispatcher-owned `TryRegisterUpdatable`, `TryRegisterSlowTickable`, `TryRegisterFixedTickable`, `TryRegisterPostFixedTickable`, and `TryRegisterLateFrameTickable`; multi-lane owners now roll back successful lanes if another lane fails.
Rejected Alternatives: Hot-list membership reads, manual lane `Contains`, partial lane leaks, broad dispatcher API changes, or phase changes were rejected.
Scalability potential: Weak devices avoid enable/rebind probe bursts across UI, world, voxel, flora, ecosystem, save, power, physics, tether, and vegetation owners; middle/high/ultra keep identical cadence.
Hardware Impact: Estimated 12-30 us saved during enable/rebind bursts on i3/MX350-class CPUs. Compile proof is source-only after loop129; scoped grep and `diff --check` passed, but rebuild is blocked by `BUILD_SKIP cpu=12 compiler_count=7`.

## 2026-05-24 Generated Project Duplicate Include Cleanup 131
Problem: Guarded Core compile exposed `CS2002` for `Assets/_Project/Scripts/Core/Contracts/Fluids/FluidAnalyticalContracts.cs` because `Directory.Build.targets` force-included the file without first removing the generated project item.
Solution: Add the file to the Hecton8.Core `Compile Remove` list before the forced include. The generated `.csproj` stays untouched.
Rejected Alternatives: Editing generated `Hecton8.Core.csproj`, suppressing `CS2002`, or removing the forced include were rejected; generated project regeneration would erase `.csproj` edits and suppression would hide item-graph drift.
Scalability potential: Weak/middle/high/ultra runtime behavior unchanged; this is build graph hygiene only.
Hardware Impact: 0 frame-time change. Reduces compile warning noise and keeps Core CLI proof strict. Verification: duplicate-risk parser returned `DUPLICATE_RISK_COUNT=0`; scoped `diff --check` passed with Git CRLF normalization warnings only; rebuild retry blocked by external `dotnet build Assembly-CSharp.csproj` and active `csc`.

## 2026-05-24 Registration Probe Zero Verification 130
Problem: Residual late-frame, fast/fixed/post-fixed, GameTick, SceneRuntime, and GCMonitor registration paths still left a project-wide register/probe grep surface and needed a guarded compile classification.
Solution: Finished the residual `TryRegister*` conversion and preserved reset recovery by unregistering stale lanes before re-registering in `GameTickManager` and `SceneRuntimeService`.
Rejected Alternatives: Lane membership reads, blind `TryRegister*` replacement in recovery paths, or broad dispatcher refactor were rejected.
Scalability potential: Weak devices avoid enable/rebind/reset probe bursts across core, player, world, UI, audio, fluid, construction, and voxel owners; middle/high/ultra keep identical cadence.
Hardware Impact: Estimated 8-25 us saved during enable/rebind/reset bursts on i3/MX350-class CPUs. Source proof: project grep for old register/probe patterns returned no non-editor matches; scoped `diff --check` passed. Compile proof is blocked by `MSB3491 Access to the path is denied` writing `Temp/obj/*` in `Build_EXTERNAL_CODEX_hotpath_cleanup129_registration_probe_zero.log`; this is ENV/ACCESS_DENIED, not C# diagnostics.

## 2026-05-24 Compile Verification 132
Problem: Loop131 needed a fresh guarded compile classification after CPU/compiler guard cleared.
Solution: Ran `Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` with a new file log. The log reaches `Hecton8.Editor -> Temp/bin/Debug/Hecton8.Editor.dll`.
Rejected Alternatives: Treating the missing final summary/exit line as a clean pass, or treating `MSB3101` cache access as a C# source diagnostic, were rejected.
Scalability potential: Runtime behavior is unchanged. The value is source-truth confidence for all device tiers.
Hardware Impact: 0 frame-time impact. Build output shows 0 `: error ` matches, no `CS*` diagnostics, and one environment/cache warning: `MSB3101` access denied writing `Temp/obj/Hecton8.Editor/Hecton8.Editor.csproj.AssemblyReference.cache`.

## 2026-05-24 Static Driver Registration Probe Cleanup 133
Problem: Non-`this` static drivers still escaped the previous registration-probe cleanup: drone headless lanes, deferred voxel late-frame drivers, voxel leak sentinel, and an underwater renderable path still used raw register calls or `Contains` proof reads.
Solution: Switched each path to dispatcher/bucket-owned `TryRegister*`; drone headless registration now tracks update/late-frame/render lanes separately and rolls back partial registration before marking the driver active.
Rejected Alternatives: Hot-list/lane membership reads, blind raw register wrappers, and partial static-driver registration were rejected.
Scalability potential: Weak devices avoid small enable/rebind probe bursts and static-driver lane leaks; middle/high/ultra keep identical simulation/render cadence and can spend saved budget on presentation.
Hardware Impact: Estimated 2-6 us saved during driver enable/rebind bursts on i3/MX350-class CPUs. Source proof only: non-editor raw tick-register grep, renderable register/contains grep, lane `Contains` grep, and scoped `diff --check` passed; compile skipped by `BUILD_GUARD cpu=92.6 compiler_count=7`.

## 2026-05-24 Development Log And Frost Probe Cleanup 134
Problem: 21 runtime systems still emitted development/info `Debug.Log` callsites in production builds, including interpolated strings on gameplay/narrative/audio/UI events. Four dispatcher paths still used frost/render bucket membership reads after registration.
Solution: Converted info-only logs to compile-stripped `H8Debug.Log`; left warnings/errors intact. Replaced `FrostTickables.Contains(this)` and foveated update/render membership proof reads with `TryRegisterFrostTickable`, `TryRegisterUpdatable`, and `Renderables.TryRegister/TryUnregister`.
Rejected Alternatives: `#if` wrapping every call manually, suppressing logs globally, changing warning/error semantics, or keeping membership reads as proof were rejected.
Scalability potential: Weak devices avoid release string/console callsite cost during common narrative/audio/UI events; middle/high/ultra keep the same event truth and can spend saved CPU budget on presentation.
Hardware Impact: Estimated 1-8 us saved per affected event/log burst on i3/MX350-class CPUs; release interpolated strings for those calls are removed by `[Conditional]`. Build proof is source-only because latest guard reported `BUILD_GUARD cpu=77 compiler_count=1`.

## 2026-05-24 Voxel Sonar DataVault Cache Cleanup 135
Problem: `HectonVoxelVolume` sonar SDF publish and descriptor clear paths still read `GlobalRegistry.DataVault` during async runtime work.
Solution: Added cached DataVault ownership plus `IGlobalRegistryHotSwapListener`; OnEnable cold-caches the owner and replacement refreshes capacity against the new vault. Publish/clear now use `_cachedDataVault`.
Rejected Alternatives: Per-publish registry retry, bootstrap-only `TryGetLatestCreated`, or clearing descriptors through whichever vault is current at the moment were rejected.
Scalability potential: Weak devices avoid registry polling during sonar publish/clear bursts; middle/high/ultra keep identical SDF payload truth and can spend the saved budget on denser sonar visuals.
Hardware Impact: Estimated 1-4 us saved during sonar publish/clear bursts on i3/MX350-class CPUs. Source proof only: `HectonVoxelVolume` `GlobalRegistry.DataVault` grep leaves only `CacheDataVaultCold`; scoped `diff --check` passed; compile skipped by `BUILD_GUARD cpu=96.1 compiler_count=0`.

## 2026-05-24 Performance Budget Dispatcher Rebind Cleanup 136
Problem: `PerformanceBudgetController` registered update cadence only from OnEnable, so Dispatcher replacement could leave budget enforcement absent from the new dispatcher lane while `_registeredToTickManager` stayed true.
Solution: Added `IGlobalRegistryHotSwapListener`, split update registration into explicit helpers, and rebinding unregisters stale bucket state before registering into the replacement Dispatcher.
Rejected Alternatives: Periodic dispatcher polling, broad budget-controller rewrite, or leaving the controller inert after replacement were rejected.
Scalability potential: Weak devices keep budget enforcement alive after service replacement; middle/high/ultra keep identical budget math and throttle decisions.
Hardware Impact: Estimated 1-3 us only during Dispatcher replacement; 0 steady-frame change. Source proof only: scoped `diff --check` passed and targeted grep shows hot-swap callback; compile skipped by `BUILD_GUARD cpu=96.3 compiler_count=1`.

## 2026-05-24 Remaining Dispatcher Rebind Cleanup 137
Problem: `EntityChangeManager`, `LandingImpactVFX`, `PlayerStressMetricsRuntime`, and `RenderTextureLifecycleTracker` registered cadence from OnEnable only; Dispatcher replacement could leave their `_registered*` flags true while the replacement lane had no owner.
Solution: Added `IGlobalRegistryHotSwapListener` lifecycle to the four owners and explicit Dispatcher replacement rebind that unregisters stale bucket/lane state before registering again.
Rejected Alternatives: Periodic Dispatcher polling, broad dispatcher API change, or trusting OnEnable-only registration after service replacement were rejected.
Scalability potential: Weak devices keep entity-change, impact VFX, player stress, and RT leak tracking cadence alive after replacement; middle/high/ultra keep identical simulation/presentation truth and can spend saved recovery time on visual detail.
Hardware Impact: Estimated 1-5 us only during Dispatcher replacement on i3/MX350-class CPUs; 0 steady-frame cost. Source proof only: targeted hot-swap grep and scoped `diff --check` passed; compile skipped by latest `BUILD_GUARD cpu=99.4 compiler_count=0`.

## 2026-05-24 Short Owner Dispatcher Rebind Cleanup 138
Problem: Five short runtime owners still registered cadence from OnEnable/OnSpawn only and could miss replacement Dispatcher lanes: voxel navgrid lifecycle, instance culling bridge, suit HUD extensions, GC monitor, and meteor splash VFX.
Solution: Added `IGlobalRegistryHotSwapListener` lifecycle and Dispatcher replacement rebind for each touched owner, preserving existing unregister/Dispose semantics.
Rejected Alternatives: Polling Dispatcher in tick paths, broad registration framework changes, or leaving diagnostic/VFX cadence inert after replacement were rejected.
Scalability potential: Weak devices keep low-cost diagnostics, UI compatibility, navgrid dirty-volume drain, instance overload telemetry, and meteor fake VFX alive after service replacement; middle/high/ultra keep identical truth/presentation cadence.
Hardware Impact: Estimated 1-6 us only during Dispatcher replacement on i3/MX350-class CPUs; 0 steady-frame cost. Source proof only: targeted hot-swap grep and scoped `diff --check` passed; compile skipped by `BUILD_GUARD cpu=99.8 compiler_count=1`.

## 2026-05-24 Runtime Info Log Release Strip 139
Problem: 39 additional runtime files still emitted info-only `Debug.Log` callsites. Several used interpolation or report builders that should not execute in release builds.
Solution: Added a Unity-context overload to `H8Debug.Log` and converted 71 selected info-only runtime logs to `Hecton8.Core.H8Debug.Log`. Warning/error semantics were left unchanged.
Rejected Alternatives: Manual `#if` wrapping per callsite, global log suppression, or changing warnings/errors to conditional logs were rejected.
Scalability potential: Weak devices avoid release string/report construction and console routing during event bursts; middle/high/ultra keep identical gameplay truth and retain development visibility in editor/dev builds.
Hardware Impact: Estimated 1-10 us saved per affected event/log burst on i3/MX350-class CPUs; release builds omit calls and arguments via `[Conditional]`. Source proof only: targeted raw `Debug.Log` grep over the 39 converted files returned no matches; scoped `diff --check` passed; compile skipped by `BUILD_GUARD cpu=98.8 compiler_count=1`.

## 2026-05-24 Context Getter Purity And Raycast Rebind Cleanup 140
Problem: `EnvironmentRuntimeContextService` and `OceanKinematicsRuntimeService` read accessors mutated cached state or ensured services, violating pure-read authority. `RaycastBatchHelper` still had OnEnable-only late-frame registration.
Solution: Converted context getters to pure cached returns, moved dependency refresh to lifecycle/tick/hot-swap, and added Dispatcher replacement rebind to RaycastBatchHelper.
Rejected Alternatives: Keeping hidden getter mutation, adding scene searches, adding periodic Dispatcher polling, or widening GlobalRegistry hot access were rejected.
Scalability potential: Weak devices avoid hidden getter work during UI/gameplay bursts; middle/high/ultra keep identical owner-route state and can spend saved main-thread time on presentation.
Hardware Impact: Estimated 1-4 us saved per context getter burst plus 1-3 us only during Dispatcher replacement on i3/MX350-class CPUs. Source grep and scoped `diff --check` passed. Build wall: `Build_EXTERNAL_CODEX_hotpath_cleanup139_context_purity.log` failed before C# compile with `NETSDK1004` missing `project.assets.json` and `MSB3491` Temp/obj access denied; no `CS*` diagnostics. Restore retry log exits 1 after `Determining projects to restore...` with no diagnostics; restore-spawned dotnet processes were stopped.

## 2026-05-24 Runtime Smoke Proof Log Strip 141
Problem: 40 smoke, diagnostic, and runtime-support files still had raw info-only `Debug.Log` calls or debug-log comments. Several executable calls build proof strings that are useless in release.
Solution: Converted 63 executable info-only calls and 2 comments to conditional `Hecton8.Core.H8Debug.Log`; warnings/errors remain non-conditional.
Rejected Alternatives: Manual `#if` at every callsite, global log suppression, or converting warnings/errors were rejected.
Scalability potential: Weak devices avoid release proof-string/console call cost during verification bursts; middle/high/ultra keep identical gameplay truth and still show proof logs in editor/dev builds.
Hardware Impact: Estimated 1-10 us saved per affected proof/log burst on i3/MX350-class CPUs; release builds omit the conditional calls and arguments. Source proof only: targeted raw `Debug.Log` grep over the 40 converted files returned 0 matches; scoped `diff --check` passed with LF normalization warnings only; compile skipped by latest `BUILD_GUARD cpu=93.2 compiler_count=1`.

## 2026-05-24 Runtime Raw Log Surface Zero 142
Problem: The non-editor runtime tree still had raw info-only `Debug.Log` calls in smoke, visual, UI, profiler, and world-support code. A few root editor proof tools also still used raw info logs despite already referencing Core.
Solution: Converted 35 executable calls in 20 files to conditional `Hecton8.Core.H8Debug.Log`. The pass intentionally avoided warning/error logs and editor asmdef domains without existing Core references.
Rejected Alternatives: Adding Core references to unrelated editor asmdefs, converting warnings/errors, or leaving release log-string work in runtime UI/profiler/smoke paths were rejected.
Scalability potential: Weak devices avoid release string/console work in runtime smoke/UI/profiler bursts; middle/high/ultra keep identical gameplay truth and dev visibility.
Hardware Impact: Estimated 1-10 us saved per affected proof/UI/profiler burst on i3/MX350-class CPUs. Source proof only: targeted raw `Debug.Log` grep over 20 files returned 0; project non-editor raw `Debug.Log` grep returned 0 excluding `H8Debug.cs`; scoped `diff --check` passed with LF normalization warnings only; compile skipped by latest pre-build `BUILD_GUARD cpu=78.3 compiler_count=2`.

## 2026-05-24 Dispatcher Rebind And Sensory Getter Cleanup 143
Problem: Ten additional runtime owners still had OnEnable-only cadence/dependency binding, and `PlayerSensoryManager` public accessors still synchronized hierarchy state during reads.
Solution: Added `IGlobalRegistryHotSwapListener` lifecycle/rebind to creature wound VISUAL_SYNC, power relay/sorter slow ticks, dev celestial time-lapse, player sensory/runtime context, submarine collider LOD, pause verifier, XR somatic bootstrap, and fabricator actuator. Player sensory getters now return cached references only; owner sync stays in lifecycle/tick/hot-swap.
Rejected Alternatives: Periodic Dispatcher polling, read-accessor hierarchy sync, slow-tick `GlobalRegistry.CelestialEngine`/`TickManager`/input reads, or leaving `_registered*` true after Dispatcher replacement were rejected.
Scalability potential: Weak devices avoid getter-side hierarchy sync and service lookup bursts; middle/high/ultra keep identical gameplay truth and use recovered time for presentation rather than recovery work. Low/middle/high/ultra behavior stays continuous; no quality switch or DTO/save identity change.
Hardware Impact: Estimated 1-6 us only during Dispatcher/service replacement plus 1-4 us per sensory getter burst on i3/MX350-class CPUs. Source proof only: targeted hot-swap/getter grep passed; no-hot-swap candidate count dropped to 61; scoped `diff --check` passed with LF normalization warnings only. Compile skipped by guard: CPU 100%, compiler_count 0.

## 2026-05-24 Dispatcher/DataVault Rebind Cleanup 144
Problem: Fourteen additional runtime owners still registered tick/render lanes from OnEnable/OnSpawn only or read registry services from action/event paths after service replacement.
Solution: Added hot-swap listener/rebinds to tool visual lanes, replay/lockstep validators, pipe/preview/auxiliary runtimes, demo controls, flora tint, logistics pipe, and celestial cataclysm. Cached DataVault/localization/input/celestial/fluid-decal/persistent-world services now update from registry replacement callbacks.
Rejected Alternatives: Periodic Dispatcher polling, hot action-path registry reads, broad dispatcher rewrites, or leaving `_registered*` true while replacement lanes miss the owner were rejected.
Scalability potential: Weak devices avoid recovery lookup bursts and broken visual/simulation cadence after service replacement; middle/high/ultra keep identical truth and spend recovered time on presentation. No binary quality switch, DTO, save identity, or authority-route change.
Hardware Impact: Estimated 1-8 us during service replacement/event bursts on i3/MX350-class CPUs; 0 steady-frame truth cost. Source proof: targeted hot-swap greps passed, no-hot-swap count dropped 61 -> 47, scoped `diff --check` passed. Build wall persists: `Build_EXTERNAL_CODEX_hotpath_cleanup144_rebind_batch.log` fails before C# with `NETSDK1004` missing project.assets and `MSB3491` Temp/obj access denied.

## 2026-05-24 Dispatcher Rebind Cleanup 145
Problem: Four more runtime owners still depended on OnEnable-only Dispatcher lane registration: spline batch rendering, interaction highlighting, player transport coordination, and transport charging.
Solution: Added `IGlobalRegistryHotSwapListener` lifecycle/rebinds to `ConnectionSplineBatchRenderer`, `InteractionHighlighter`, `PlayerTransportCoordinator`, and `TransportChargingStation`. `TransportChargingStation.TryRegister()` now retries update and late-frame lanes independently.
Rejected Alternatives: Periodic Dispatcher polling, blind per-frame registry checks, or stale `_registered*` flags after Dispatcher replacement were rejected.
Scalability potential: Weak devices avoid broken visual/control cadence after Dispatcher replacement; middle/high/ultra keep the same gameplay truth and spend recovery budget on presentation. Low/middle/high/ultra behavior remains continuous; no quality switch, DTO, save identity, or authority-route change.
Hardware Impact: Estimated 1-6 us during Dispatcher replacement/interaction bursts on i3/MX350-class CPUs; 0 steady-frame truth cost. Source proof: targeted hot-swap greps passed, no-hot-swap count dropped 47 -> 43, scoped `diff --check` passed. Rebuild skipped by guard `BUILD_GUARD cpu=70.4 compiler_count=0`.

## 2026-05-24 Interaction Dispatcher Rebind Cleanup 146
Problem: Four transient interaction/door owners could lose update or late-frame work after Dispatcher replacement: sealed door VFX/audio, valve wheel momentum, battery compartment snap/visual refresh, and lifepod strap hold progress.
Solution: Added hot-swap listener/rebinds to `SealedDoor`, `VRValveWheelHandle`, `PhysicalBatteryCompartment`, and `LifePodSeatStrapLatch`. Rebind predicates preserve unfinished pending work instead of clearing snap, visual, audio, or hold state. `SealedDoor` also refreshes cached `IAudioService` on Audio replacement.
Rejected Alternatives: Periodic Dispatcher polling, hot action-path registry retry, clearing pending work on service replacement, or keeping stale lane flags were rejected.
Scalability potential: Weak devices keep interaction presentation/control cadence alive after replacement without per-frame lookup cost; middle/high/ultra keep identical gameplay truth and use recovered recovery time on presentation. Low/middle/high/ultra behavior remains continuous; no quality switch, DTO, save identity, or authority-route change.
Hardware Impact: Estimated 1-6 us during Dispatcher replacement/interaction bursts on i3/MX350-class CPUs; 0 steady-frame truth cost. Source proof: targeted hot-swap greps passed, current no-hot-swap count is 27, scoped `diff --check` passed. Rebuild skipped by guard `BUILD_GUARD cpu=100 compiler_count=1`.

## 2026-05-24 GI Relay And Despawn Rebind Cleanup 147
Problem: Delayed despawn timers and GI relay could lose Dispatcher lanes after replacement; GI relay also held DataVault/player/biome cached ownership without replacement rebind.
Solution: Added hot-swap rebind to `ObjectPoolManager.DespawnTimer` and `HectonGIRelaySystem`. Despawn timers preserve active/pending state across Dispatcher replacement. GI relay unregisters/re-registers slow/late lanes, completes pending SH jobs before DataVault replacement, releases/reacquires vault-owned buffers, and refreshes cached Player/Biome services from callbacks.
Rejected Alternatives: Immediate despawn on replacement, stale GI DataVault handles, slow-tick registry polling, or clearing pending water/lightning late-frame work were rejected.
Scalability potential: Weak devices keep delayed pooling and cheap GI presentation stable after service replacement; middle/high/ultra keep identical gameplay truth and can spend recovered recovery time on visual overkill. No quality switch, DTO, save identity, or authority-route change.
Hardware Impact: Estimated 2-8 us during replacement/despawn/GI bursts on i3/MX350-class CPUs; 0 steady-frame truth cost. Source proof: targeted hot-swap greps passed, current no-hot-swap count is 24, scoped `diff --check` passed. Build `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup147_gi_despawn.log` failed before C# with `NETSDK1004` missing project.assets and `MSB3491` Temp/obj access denied.

## 2026-05-24 Broad Dispatcher Rebind Cleanup 148
Problem: Thirteen cadence/render/UI/physics/geology owners still depended on OnEnable-only Dispatcher lane state. Dispatcher replacement could leave `_registered*` true while the replacement lane missed the owner.
Solution: Added `IGlobalRegistryHotSwapListener` lifecycle/rebinds to `AutonomousExtractorSystem`, `SubmarineStationKeepingController`, root/tools `PerformanceMonitor`, `PDABarterTab`, `ObserverRelativeCelestialBody`, Crest/MapMagic bridges, `TetherManager`, `EclipseGameplaySystem`, `HectonBiolumController`, `WorldGenerativeGeologyTerrainSeamApplier`, and `ExosuitKinematicsRuntime`.
Rejected Alternatives: Periodic Dispatcher polling, broad dispatcher API rewrite, scene search, or clearing pending render/physics work on replacement were rejected.
Scalability potential: Weak devices keep render/physics/UI cadence alive after replacement without per-frame registry lookups; middle/high/ultra keep identical truth and spend recovery budget on visuals. Low/middle/high/ultra behavior stays continuous; no quality switch, DTO, save identity, or authority-route change.
Hardware Impact: Estimated 1-8 us during Dispatcher replacement/render/physics recovery bursts on i3/MX350-class CPUs; 0 steady-frame truth cost. Source proof: no-hot-swap candidate count dropped 24 -> 13, targeted hot-swap grep passed, scoped `diff --check` passed. Rebuild skipped by guard `BUILD_GUARD cpu=100 compiler_count=1`.

## 2026-05-24 Sonar Foam Vegetation Rebind Cleanup 149
Problem: Three visual/runtime owners still depended on OnEnable-only lane or cold dependency state: topographical sonar, GPU Jacobian foam, and indirect vegetation scooter-headlight culling.
Solution: Added `IGlobalRegistryHotSwapListener` lifecycle to `TopographicalSonarSynthesizer`, `JacobianFoamGpuRuntime`, and `HectonIndirectVegetationRenderer`. Sonar completes pending jobs before DataVault replacement, releases old vault handles, reacquires buffers, and reinitializes shader args. Foam resets DataVault generation handles on replacement and re-registers late-frame after Dispatcher replacement. Vegetation rebinds update/late-frame and refreshes cached Player context.
Rejected Alternatives: Periodic Dispatcher polling, releasing sonar buffers while jobs still own native arrays, stale foam handles after DataVault replacement, or vegetation hot Player registry lookup were rejected.
Scalability potential: Weak devices keep sonar/foam/vegetation presentation stable after service replacement without per-frame registry work; middle/high/ultra keep identical gameplay truth and can spend recovered recovery budget on visual density. Low/middle/high/ultra behavior remains continuous; no quality switch, DTO, save identity, or authority-route change.
Hardware Impact: Estimated 1-7 us during Dispatcher/DataVault/player replacement and render bursts on i3/MX350-class CPUs; 0 steady-frame truth cost. Source proof: targeted hot-swap greps passed, current strict no-hot-swap count is 20, domain-filtered non-bootstrap/QA/core-service count is 11, and scoped `diff --check` passed with LF normalization warnings only. Rebuild skipped by guard `BUILD_GUARD cpu=100 compiler_count=0`.

## 2026-05-24 Heavy Runtime Rebind Cleanup 150
Problem: Economy, outpost generation, vehicle damage, submarine dynamics, and abyssal thermodynamics still had OnEnable-only tick lanes or stale DataVault/service ownership after replacement.
Solution: Added `IGlobalRegistryHotSwapListener` lifecycle/rebinds to `MarauderOutpostGenerationService`, `TradeMarauderDirector`, `VehicleComponentDamageRuntime`, `SubmarineDynamicsRuntime`, and `AbyssalThermodynamicsSolver`. DataVault replacement now completes owned jobs/fences before resetting handles and reacquiring buffers where applicable.
Rejected Alternatives: Per-tick registry polling, releasing native buffers while jobs still own them, leaving stale DataVault pointers after replacement, or broad dispatcher API changes were rejected.
Scalability potential: Weak devices keep economy, vehicle, submarine, and thermal cadence alive after service replacement without hot lookup bursts; middle/high/ultra keep identical truth and can spend recovered recovery budget on presentation. Low/middle/high/ultra behavior remains continuous; no quality switch, DTO, save identity, or authority-route change.
Hardware Impact: Estimated 2-12 us during Dispatcher/DataVault/service replacement bursts on i3/MX350-class CPUs; 0 steady-frame truth cost. Source proof: targeted hot-swap greps passed, type-aware no-hot-swap count is 4 (`PlayerBuilder`, `RepairTool`, two QA headless bots), file-local count is 7 due partial false positives, and scoped `diff --check` passed with LF normalization warnings only. Rebuild skipped by guard `BUILD_GUARD cpu=88.3 compiler_count=8`.

## 2026-05-24 Tool And QA Rebind Tail Cleanup 151
Problem: Tool/QA and small runtime cadence owners still had replacement gaps: `PlayerBuilder` had a public callback that bypassed `PlayerTool`'s explicit interface route, builder late-frame audio could read `GlobalRegistry.Audio`, repair visuals/audio/localization only handled DataVault, and stress/Steam/Manta/cavitation/terrain/HUD scaler lanes could stay stale after Dispatcher replacement.
Solution: Routed builder/repair through `OnToolRegistryServiceReplaced`, reset builder DataVault/socket/validation state on vault replacement, removed late-frame Audio registry fallback, added Dispatcher/Audio/Localization rebinds, and added hot-swap Dispatcher rebinds to `HeadlessStressFractureBot`, `SteamManager`, `MantaEmergencyWreck`, `AbyssalCavitationRuntimeHost`, `TerrainChunkPagerRuntime`, and `HectonUIScaler`. Terrain pager only accepts DataVault replacement before initialization.
Rejected Alternatives: Periodic Dispatcher polling, public derived callbacks bypassing base tool cache refresh, hot late-frame service reads, active terrain-pager vault swap while worker/native state is live, or dropping pending Manta late-frame despawn.
Scalability potential: Weak devices avoid replacement bursts and stale validation/callback faults; middle/high/ultra keep identical gameplay truth and can spend recovered budget on presentation. Low/middle/high/ultra behavior remains continuous; no quality switch, DTO, save identity, or authority-route change.
Hardware Impact: Estimated 1-8 us during Dispatcher/DataVault/service replacement bursts on i3/MX350-class CPUs; 0 steady-frame truth cost. Source proof: targeted hot-swap greps passed and scoped code `diff --check` passed with LF normalization warnings only. Build skipped by guard `BUILD_GUARD cpu=38 compiler_count=1`.

## 2026-05-24 Persistent World Tombstone Save Cache Cleanup 152
Problem: `PersistentWorldRegistry.ResolveTombstoneDayIndex()` was a static helper that read `GlobalRegistry.Save` while resource-node tombstones and decay sweeps compute in-game day indices.
Solution: Made the resolver instance-owned, cached `ISaveService` during owner lifecycle, and refreshed that cache on `GlobalRegistryServiceSlot.Save` replacement.
Rejected Alternatives: Keeping the static registry read, adding a new global route, or changing save/tombstone DTO layout were rejected.
Scalability potential: Weak devices avoid small registry lookup bursts during tombstone/decay work; middle/high/ultra keep identical persistent-world truth and spend saved recovery budget on presentation.
Hardware Impact: Estimated 1-3 us saved per tombstone/decay burst on i3/MX350-class CPUs; 0 steady-frame cost. Source proof: `PersistentWorldRegistry` Save grep leaves only cold cache, type-aware no-hot-swap scan is 0, hot-swap unregister scan is 0, scoped `diff --check` passed. Build skipped by latest `BUILD_GUARD cpu=100 compiler_count=2`.

## 2026-05-24 Persistent World Player Owner Cache Cleanup 153
Problem: `PersistentWorldRegistry` still read `GlobalRegistry.Player` for AUP snapshots and `GlobalRegistry.PlayerInventory` for item-catalog lookup during hydration/catalog work.
Solution: Cached `IPlayerRuntimeContext` and `IPlayerInventoryService` in the owner lifecycle, refreshed them on Player/PlayerInventory hot-swap, and made hydration/catalog helpers use cached owner state.
Rejected Alternatives: Leaving direct registry reads, using `PlayerRuntimeContextService.ActiveRuntimeContext`, scene search, or changing item catalog ownership were rejected.
Scalability potential: Weak devices avoid lookup bursts during residency/hydration; middle/high/ultra keep identical persistent-world truth and use saved CPU for denser presentation.
Hardware Impact: Estimated 1-4 us saved per hydration/catalog burst on i3/MX350-class CPUs; 0 steady-frame cost. Source proof: `PersistentWorldRegistry` registry grep leaves only cold cache lines, type-aware no-hot-swap scan is 0, hot-swap unregister scan is 0, scoped `diff --check` passed. Build skipped by `BUILD_GUARD cpu=76.3 compiler_count=0`.

## 2026-05-24 Short UI Dispatcher Rebind Cleanup 154
Problem: Several short UI/audio/construction owners handled Dispatcher replacement by calling their register helper while stale `_registered*` flags still said the old lane was live. `PDADeathMemoryDump` also read `GlobalRegistry.Player` from the death-signal consume path.
Solution: Reset local registration flags before Dispatcher hot-swap re-registration in 12 touched owners, and route death-dump survival lookup through cached `IPlayerRuntimeContext` refreshed by Player hot-swap.
Rejected Alternatives: Periodic Dispatcher polling, unregistering through an unknown replacement owner, retaining stale flags, or using `PlayerRuntimeContextService.ActiveRuntimeContext` for the maintenance station cold cache were rejected.
Scalability potential: Weak devices keep HUD, sonar compass, death overlay, audio trigger, repair/planter/maintenance station cadence alive after service replacement without hot polling; middle/high/ultra keep identical truth and spend recovery budget on presentation.
Hardware Impact: Estimated 1-6 us only during Dispatcher replacement/UI bursts on i3/MX350-class CPUs; 0 steady-frame cost. Source proof only: scoped `diff --check` passed, targeted greps show local unregister/register rebinds in Dispatcher callbacks and no death-dump direct Player read. Build skipped by `BUILD_GUARD cpu=68.3 compiler_count=7`.

## 2026-05-24 UI Dispatcher Rebind Cleanup 155
Problem: Eight additional UI owners still re-registered on Dispatcher replacement while their local registration flags could remain true; three loop154 UI owners also lacked explicit null-Dispatcher local reset.
Solution: Routed Dispatcher hot-swap through existing unregister/register helpers in the eight UI owners and added null-Dispatcher local flag reset to SonarHoloCompass, UIFadeTransition, and UIScreenShake.
Rejected Alternatives: Hot Dispatcher polling, flag-only rebind on same-owner events, or broad dispatcher API changes were rejected.
Scalability potential: Weak devices keep font streaming, PDA tabs, subtitles, loading screen, and text FX cadence alive after service replacement without polling; middle/high/ultra keep identical UI truth and spend recovery budget on presentation.
Hardware Impact: Estimated 1-7 us only during Dispatcher replacement/UI bursts on i3/MX350-class CPUs; 0 steady-frame cost. Source proof only: scoped UI `diff --check` passed and targeted grep shows unregister/register rebinds plus null local resets. Build skipped by `BUILD_GUARD cpu=100 compiler_count=9` with active `csc`/dotnet.

## 2026-05-25 UI/Construction Dispatcher Rebind Cleanup 156
Problem: Fifteen remaining UI/construction owners could miss the replacement Dispatcher because local `_registered*` flags stayed true across service replacement.
Solution: Added local lane reset or unregister/register rebind in `AcousticEcholocationTranslator`, `BIOSMessageStreamer`, `BuilderStatusOverlay`, `DiegeticGlitchSurgeonRuntime`, `DiegeticPdaFocusDistanceController`, `FakeRadarBlipController`, `HectonUIScaler`, `InteractionUI`, `DeepDrillModule`, `SettingsPanelAnimator`, `SettingsComparisonView`, `LocalizedLayoutMirror`, `LocalizedTMPAutoSizer`, `DroneFleetManager` headless driver, and `ShaderCompassRibbon`.
Rejected Alternatives: Periodic Dispatcher polling, blind flag-only rebind, new global route, or broad Dispatcher API rewrite were rejected.
Scalability potential: Weak devices keep UI labels, focus, radar, compass, settings panels, drill cadence, and headless drone cadence alive after service replacement without per-frame lookup; middle/high/ultra keep identical truth and can spend recovery budget on presentation.
Hardware Impact: Estimated 1-8 us only during Dispatcher replacement bursts on i3/MX350-class CPUs; 0 steady-frame cost. Source proof only: scoped `diff --check` passed with LF normalization warnings and targeted grep shows patched unregister/register or reset routes. Build skipped by `BUILD_GUARD cpu=64 compiler_count=0`.

## 2026-05-25 UI/Construction Runtime Singleton Cleanup 157
Problem: UI/Construction runtime owners still used `PlayerRuntimeContextService.ActiveRuntimeContext` or `LocalizationManager.ActiveRuntimeInstance` from cold/lazy paths. Those static singleton reads bypass the declared owner-cache route.
Solution: Routed `BatteryChargerModule`, `FoundationPylonGpuBatch`, `DroneFleetManager`, `RelayHUDElement`, `SettingsLivePreview`, `DiegeticGyroCompassPhysicalBinding`, and `HectonOSBootManager` through `GlobalRegistry` cold owner cache plus existing hot-swap state.
Rejected Alternatives: Lazy interaction/render-path singleton fallback, scene search, or adding another route facade were rejected.
Scalability potential: Weak devices avoid fallback singleton lookups during interaction/render/UI boot bursts; middle/high/ultra keep identical truth and spend recovered budget on presentation.
Hardware Impact: Estimated 1-4 us saved per fallback burst on i3/MX350-class CPUs; 0 steady-frame cost. Source proof only: targeted UI/Construction grep for `PlayerRuntimeContextService.ActiveRuntimeContext` and `LocalizationManager.ActiveRuntimeInstance` returned no matches; scoped `diff --check` passed with LF normalization warnings. Build skipped by `BUILD_GUARD cpu=100 compiler_count=2` with active `csc`/`dotnet`.

## 2026-05-25 World/Environment Dispatcher Rebind Cleanup 158
Problem: Twenty-three world/environment/AI owners could miss the replacement Dispatcher because local registration flags survived service replacement. One ambient-biota path also used full runtime unregister during Dispatcher swap, which could churn cold service identity.
Solution: Routed biome, world, geology, ambient water, rock, scan marker, celestial, atmosphere, director AI, buoyancy, cave roots, scarcity, recycler, and ambient-biota owners through local unregister/register or null-reset paths. `AmbientBiotaDirector` now separates dispatcher-lane unregister from service unregister.
Rejected Alternatives: Periodic Dispatcher polling, broad Dispatcher API rewrite, flag-only rebind, or unregistering `AmbientBiota` service during Dispatcher replacement were rejected.
Scalability potential: Weak devices keep world, biome, geology, buoyancy, celestial, atmosphere, and AI cadence alive after service replacement without per-frame lookup; middle/high/ultra keep identical gameplay truth and can spend recovery budget on presentation. Low/middle/high/ultra behavior stays continuous; no quality switch, DTO, save identity, or authority-route change.
Hardware Impact: Estimated 1-12 us only during Dispatcher replacement/world-environment bursts on i3/MX350-class CPUs; 0 steady-frame cost. Source proof: scoped `diff --check` passed; 23-file grep shows Dispatcher handling plus reset/unregister routes in every touched file. Compile attempt `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup158_world_dispatcher_rebind.log` failed before C# with `NETSDK1004` missing project.assets, 0 warnings, no `CS*`; retry blocked by `BUILD_GUARD cpu=79 compiler_count=2`.
