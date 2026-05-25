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
