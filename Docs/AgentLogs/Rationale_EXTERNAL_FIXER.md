# Rationale_EXTERNAL_FIXER
Date: 2026-05-23
Status: VERIFIED - TWENTIETH HOT-PATH REGISTRY TRANCHE

## Decision 1

Problem: User requested broad autonomous repair across a dirty Unity project with many concurrent-agent edits.
Solution: Start with evidence-backed defects that can be fixed locally without changing public contracts or crossing ownership boundaries.
Rejected Alternatives: Global refactor sweep was rejected because it would collide with active agents and violate owner boundaries. Pure audit report was rejected because user explicitly requested fixes, not empty findings.
Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected until a concrete runtime path is patched; each patch must preserve continuous quality-weight doctrine if it touches runtime fidelity.
Hardware Impact: 0 us runtime gain until code changes land and are measured; no profiler proof claimed.

## Decision 2

Problem: Broad static scans can produce false positives from comments, docs, generated files, and editor-only paths.
Solution: Treat text hits as candidate defects only, inspect source context before patching, and label evidence as STATIC_SOURCE unless compile/runtime artifacts are produced.
Rejected Alternatives: Counting `rg` hits as proof was rejected by QA evidence law. Running Unity/dotnet blindly was rejected by CPU/compiler guard.
Scalability potential: Prevents noisy reports from hiding actual hot-path or compile defects.
Hardware Impact: 0 us runtime gain; reduces process risk.

## Decision 3

Problem: ScavengePopulator.ProcessSpawnQueue and DespawnChunk pulled ObjectPoolManager and WorldStateManager from GlobalRegistry during the slow-tick spawn/despawn path.
Solution: Cache ObjectPoolManager and WorldStateManager in lifecycle wiring and refresh them through IGlobalRegistryHotSwapListener for ObjectPool and WorldStateRuntime slots.
Rejected Alternatives: Per-spawn registry lookup was rejected as hot polling. Hard dependency injection constructor was rejected because MonoBehaviour lifecycle and active multi-agent scene wiring would make it wider than the defect.
Scalability potential: Low tier avoids repeated registry reads during resource streaming; Middle/High/Ultra keep the same spawn truth and can spend saved budget on visual density without changing DTO/save identity.
Hardware Impact: STATIC estimate only: removes 2 registry reads per active spawn-queue slow tick and 1 registry read per chunk despawn. No profiler microsecond claim.

## Decision 4

Problem: VoxelDeltaProcessor.EmitCaveInDustDecal resolved AbyssalFluidDecals from GlobalRegistry inside the carve commit side-effect path.
Solution: Cache AbyssalFluidDecalManager on enable, refresh through IGlobalRegistryHotSwapListener, and use the cached pointer during dust emission.
Rejected Alternatives: Leaving a static resolver was rejected because carve commit is a runtime path. Replacing the dust system with a new signal was rejected as a cross-domain route change outside this fix tranche.
Scalability potential: Low tier keeps cave-in dust optional and cheap; Middle/High/Ultra retain the same decal hook for richer cave-in visuals when the service exists.
Hardware Impact: STATIC estimate only: removes 1 registry read per cave-in dust emission. No profiler microsecond claim.

## Decision 5

Problem: Atlas6DirectiveSystem and AtlasSignalDecoder resolved AtlasSignal and FirstHour through GlobalRegistry in slow-tick/pulse/narrative decision paths.
Solution: Cache AtlasSignalSystem and FirstHourDirector in lifecycle wiring, refresh through IGlobalRegistryHotSwapListener, and read cached references in runtime decisions.
Rejected Alternatives: Replacing AtlasSignalSystem reads with a new DTO route was rejected because existing logic needs CurrentRevealStage and CurrentStrength and the current first-party service already owns those facts.
Scalability potential: Low tier reduces registry traffic on narrative polling; Middle/High/Ultra preserve the same signal truth and decode gates.
Hardware Impact: STATIC estimate only: removes 1 registry read per Atlas6 slow tick and 1 registry read per AtlasSignalDecoder slow tick/pulse sync. No profiler microsecond claim.

## Decision 6

Problem: The repository is heavily dirty, including pre-existing edits in VoxelDeltaProcessor and many unrelated files.
Solution: Verify only touched compile surface with Hecton8.Core.csproj and avoid reverting or staging unrelated changes.
Rejected Alternatives: Full repo cleanup or broad commit staging was rejected because it would capture other agents' changes and violate shared-worktree ownership.
Scalability potential: Keeps the repair tranche local and mergeable while preserving other domain work in progress.
Hardware Impact: 0 us runtime gain; process-risk reduction only.

## Decision 7

Problem: CameraRTManager, PostFXRTManager, UIRTManager, and VisorRTManager resolved RenderTextureLifecycle through GlobalRegistry inside SlowTick measurement.
Solution: Cache RenderTextureLifecycleTracker during cold lifecycle wiring, refresh it through IGlobalRegistryHotSwapListener for RenderTextureLifecycleRuntime, and use the cached reference inside memory measurement.
Rejected Alternatives: A shared RT-budget base class was rejected because the active dirty workspace makes wide manager refactors higher risk than the repeated local patch. Leaving registry reads in SlowTick was rejected by the GlobalRegistry cold-DI mandate.
Scalability potential: Low tier keeps RT budget checks cheap and predictable; Middle tier keeps the same accounting; High and Ultra can spend saved cadence budget on higher RT counts or richer visor/camera/post FX without changing truth ownership.
Hardware Impact: STATIC estimate only: removes 2 registry reads per manager slow tick, 8 reads per complete RT budget sweep across Camera/PostFX/UI/Visor managers. No profiler microsecond claim.

## Decision 8

Problem: VRAMMonitor.ReadRenderTextureMemoryBytes fell back to GlobalRegistry.RenderTextureLifecycle inside the slow-tick measurement path when the platform profiler counter did not expose RT memory.
Solution: Cache RenderTextureLifecycleTracker during cold lifecycle wiring and refresh it through IGlobalRegistryHotSwapListener for RenderTextureLifecycleRuntime; keep the profiler counter as the first path and use cached tracker only as fallback.
Rejected Alternatives: Removing the fallback was rejected because some platforms lack a usable RenderTexture profiler counter. Touching TetherManager first was rejected because that file already contained unrelated dirty HarpoonTension/Vault work from another agent.
Scalability potential: Low tier keeps budget pressure checks predictable on devices with weak profiler coverage; Middle/High/Ultra keep identical accounting and can scale RT usage without changing the authority route.
Hardware Impact: STATIC estimate only: removes 1 registry read per VRAM slow tick when the profiler RT counter is unavailable. No profiler microsecond claim.

## Decision 9

Problem: SkySystemFollowCamera could reach GlobalRegistry.Atmosphere through ResolveSeaLevelY during its per-frame Tick path when no explicit atmosphere owner was assigned.
Solution: Preserve the explicit inspector atmosphere owner as first priority, cache the registry atmosphere fallback during cold lifecycle wiring, and refresh the fallback through IGlobalRegistryHotSwapListener for AtmosphereRuntime.
Rejected Alternatives: Writing the fallback into the serialized atmosphereManager field was rejected because explicit scene ownership and runtime fallback should stay distinguishable. Reworking camera discovery was rejected as wider than the verified defect.
Scalability potential: Low tier avoids registry fallback during sky follow; Middle/High/Ultra preserve identical sea-level truth while keeping the route ready for richer sky rigs.
Hardware Impact: STATIC estimate only: removes 1 registry read from the sky follow tick path when sea-level lock needs atmosphere fallback. No profiler microsecond claim.

## Decision 10

Problem: HectonCaveVoxelAmbientOcclusionController.SlowTick could call TryResolveViewerReferences and poll GlobalRegistry.Player whenever viewerCamera was unresolved.
Solution: Cache IPlayerRuntimeContext during cold lifecycle wiring and refresh it through IGlobalRegistryHotSwapListener for the Player slot; viewer resolution reads only the cached player context.
Rejected Alternatives: Runtime scene searches and ownership mutation from the read path were rejected because they would allocate/search or blur owner responsibility. Editing dirty TetherManager first was rejected because it has unrelated active edits from another agent.
Scalability potential: Low tier keeps cave AO cadence cheap when viewer binding is missing; Middle keeps the same AO gating; High and Ultra preserve richer cave AO headroom without changing viewer authority.
Hardware Impact: STATIC estimate only: removes 1 registry read per cave AO slow tick while viewer camera resolution is unresolved. No profiler microsecond claim.

## Decision 11

Problem: AudioLogSystem playback and narrative radio interference paths pulled Audio and Player services from GlobalRegistry during runtime playback/preview routing.
Solution: Cache IAudioService, SpatialAudioManager, and IPlayerRuntimeContext during cold lifecycle wiring, refresh them through IGlobalRegistryHotSwapListener for Audio and Player slots, and keep playback/interference routes on cached owner interfaces.
Rejected Alternatives: A new SignalBus lane for private immediate audio playback was rejected because the caller needs direct command/query behavior, not a broadcast. Static AudioLogs self-registration checks were left as lifecycle authority checks, not playback cadence work.
Scalability potential: Low tier keeps encrypted preview and interference routing cheap; Middle keeps current audio behavior; High and Ultra can preserve richer spatial/bitcrush narration without registry polling in the route.
Hardware Impact: STATIC estimate only: removes up to 3 registry reads per encrypted preview playback and up to 2 registry reads per full log playback with interference. No profiler microsecond claim.

## Decision 12

Problem: EmergencyServiceRelay hid `GlobalRegistry` reads inside discovery, localization, audio-log playback, and inventory resolution helpers used by interaction/UI routes.
Solution: Cache `HectonNarrativeDirector`, `AudioLogSystem`, `IPlayerRuntimeContext`, and `LocalizationManager` during cold lifecycle wiring, refresh them through `IGlobalRegistryHotSwapListener`, and keep relay interaction queries on cached owner interfaces.
Rejected Alternatives: A new `SignalBus<T>` lane for linked-log playback and reward inventory resolution was rejected because these are private immediate owner-interface queries, not broadcast state changes. Leaving `ResolveLocalized` and `TryResolveInventory` as static registry readers was rejected because read-looking helpers should not hide service-locator polling.
Scalability potential: Low tier keeps relay hover/interaction and reward fallback predictable; Middle keeps the same authored relay behavior; High and Ultra preserve richer linked-log/localized relay presentation without adding registry polling to the interaction route.
Hardware Impact: STATIC estimate only: removes up to 1 registry read per discovery read, 2 registry reads per linked-log relay activation, 1 registry read per reward inventory fallback, and 1 registry read per localized fallback call. No profiler microsecond claim.

## Decision 13

Problem: EmergencyServiceRelayDirector route gating and fallback text helpers pulled FirstHour, AtlasSignal, and Localization services directly from `GlobalRegistry` during breadcrumb decisions and relay activation handling.
Solution: Cache `FirstHourDirector`, `AtlasSignalSystem`, and `LocalizationManager` during cold lifecycle wiring, refresh them through `IGlobalRegistryHotSwapListener`, and keep breadcrumb decisions on cached owner interfaces.
Rejected Alternatives: A new event/signal lane for private breadcrumb gating was rejected because the director needs immediate owner-interface queries, not a broadcast. Leaving fallback localization as a static registry helper was rejected because read-looking helpers must not hide registry polling.
Scalability potential: Low tier keeps route guidance checks cheap and predictable; Middle keeps the same first-hour relay flow; High and Ultra preserve richer relay/Atlas/localized presentation without adding service-locator reads to route decisions.
Hardware Impact: STATIC estimate only: removes 1 registry read per route contact registration, up to 2 registry reads per breadcrumb gate check, and 1 registry read per fallback localization. No profiler microsecond claim.

## Decision 14

Problem: DepthZoneDirector used `GlobalRegistry` from `SlowTick()` and read-looking localization helpers for quest depth context, suit hull warnings, first-hour notification gating, and localized depth-zone messages.
Solution: Cache `QuestManager`, `SuitUpgradeManager`, `FirstHourDirector`, and `LocalizationManager` during lifecycle wiring, refresh them through `IGlobalRegistryHotSwapListener`, and rebuild localized message caches when localization runtime changes.
Rejected Alternatives: Leaving `ResolveUnknownZoneLabel` and `ResolveZoneEnterFallback` as static registry helpers was rejected because helper names imply pure reads. A new event lane for quest depth context was rejected for this tranche because the existing immediate owner interface already owns the context update and the narrower fix avoids route-contract expansion.
Scalability potential: Low tier keeps depth SlowTick predictable on weak CPUs; Middle keeps identical zone/hull behavior; High and Ultra preserve richer localized route cues without turning registry polling into depth-cadence cost.
Hardware Impact: STATIC estimate only: removes 1 registry read per depth slow tick, 1 registry read per hull-warning check, 1 registry read per first-hour notification gate, and localization registry reads during cache rebuild/fallback. No profiler microsecond claim.

## Decision 15

Problem: WorldReadabilityDirector called `GlobalRegistry.FirstHour` from its readability gate and `GlobalRegistry.DepthZone` from `ResolveReferences()`, a helper called by `SlowTick()`.
Solution: Cache `FirstHourDirector` and the depth-zone fallback during lifecycle wiring, refresh them through `IGlobalRegistryHotSwapListener`, and preserve explicit inspector references as first priority.
Rejected Alternatives: Repeated registry fallback inside `ResolveReferences()` was rejected because the method is called from slow tick. Adding a new signal lane was rejected because this is private immediate read of owner state, not a broadcast.
Scalability potential: Low tier keeps world-readability cadence cheap; Middle keeps the same authored guidance; High and Ultra preserve richer contextual notifications without turning service lookup into cadence cost.
Hardware Impact: STATIC estimate only: removes 1 registry read per readability gate check and 1 registry read per depth-zone auto-resolve fallback attempt. No profiler microsecond claim.

## Decision 16

Problem: Five visor `ScriptableRendererFeature` state-build helpers pulled `GlobalRegistry.Player` from per-camera render setup paths: BIOS diagnostic loot refresh, noir depth fog surface bypass, atmosphere soot camera gate, retina distortion camera gate, and VR brownout camera gate.
Solution: Cache `IPlayerRuntimeContext` during feature `Create()`, refresh it through `IGlobalRegistryHotSwapListener` for the `Player` slot, and keep render-feature state-build helpers on cached owner context.
Rejected Alternatives: Leaving static helpers to poll the registry was rejected because render feature state build runs per camera/frame. Rewriting the features into a shared base class was rejected because the five-file local patch is smaller and safer in a dirty multi-agent workspace.
Scalability potential: Low tier avoids service-locator reads in VISUAL_SYNC gates; Middle keeps the same render gating; High and Ultra preserve richer visor/noir/VR presentation headroom without changing player truth ownership or shader DTO layout.
Hardware Impact: STATIC estimate only: removes 1 registry read per BIOS loot cache refresh, 1 registry read per noir surface-readability check, 1 registry read per atmosphere soot state build, 1 registry read per retina state build, and 1 registry read per VR brownout state build. No profiler microsecond claim.

## Decision 17

Problem: `FirstHourDirector` mixed first-hour slow-tick guidance with direct `GlobalRegistry` reads for Quest, AtlasSignal, EmergencyRelay, AudioLogs, Player, and Localization services.
Solution: Cache those owner services during lifecycle wiring, refresh them through `IGlobalRegistryHotSwapListener`, and keep slow-tick guidance, quest synchronization, event callbacks, inventory fallback, and localization fallback on cached owner references.
Rejected Alternatives: Leaving `TryGetRuntimeInventory()` and `ResolveLocalized()` as static registry helpers was rejected because they are called by first-hour guidance paths. Patching `EndingSystem` in the same commit was rejected to keep compile attribution narrow.
Scalability potential: Low tier keeps first-hour guidance cadence cheap and predictable; Middle keeps identical quest/route behavior; High and Ultra preserve richer first-hour guidance, relay, and localization presentation without adding service-locator polling to cadence paths.
Hardware Impact: STATIC estimate only: removes Quest/Atlas/Relay/AudioLogs/Player/Localization registry reads from first-hour slow-tick and guidance paths. No profiler microsecond claim.

## Decision 18

Problem: `BasePollutionManager.SlowTick()` resolved `GlobalRegistry.EnvironmentalStrain` every slow-tick before accumulating industrial strain.
Solution: Cache `EnvironmentalStrainManager` during lifecycle wiring, refresh it through `IGlobalRegistryHotSwapListener` for `EnvironmentalStrainRuntime`, and use the cached owner service in the accumulation path.
Rejected Alternatives: A new signal lane was rejected because this is immediate owner-service accumulation, not broadcast state. Patching `EndingSystem` in the same commit was rejected to keep compile attribution narrow.
Scalability potential: Low tier keeps base-pollution cadence cheap; Middle keeps identical strain math; High and Ultra preserve more industrial/ambient presentation headroom without changing pollution truth ownership.
Hardware Impact: STATIC estimate only: removes 1 registry read per base-pollution slow tick. No profiler microsecond claim.

## Decision 19

Problem: `EndingSystem` ending-condition, execution, quest, Atlas directive, and localization paths still used `GlobalRegistry` at runtime; the file also contained a pre-existing dirty partial cache patch for Quest and Atlas signal.
Solution: Work with the dirty partial patch and complete the cache route for `AtlasSignalSystem`, `Atlas6DirectiveSystem`, `QuestManager`, and `LocalizationManager`, with `IGlobalRegistryHotSwapListener` updates for all four slots.
Rejected Alternatives: Reverting the pre-existing dirty partial patch was rejected because it was relevant and would destroy another agent/user edit. A new signal lane was rejected because ending execution needs immediate owner-service commands, not broadcast state. Staging unrelated dirty files was rejected.
Scalability potential: Low tier keeps ending slow checks cheap; Middle keeps identical quest/Atlas behavior; High and Ultra preserve richer ending presentation without adding service-locator reads to ending execution.
Hardware Impact: STATIC estimate only: removes registry reads from ending condition checks, quest activation/completion, Atlas shutdown/amplify execution, and fallback localization. No profiler microsecond claim.

## Decision 20

Problem: `HectonSonarPointCloudFeature.RecordRenderGraph()` and `HectonFluidAdvectionRenderFeature.RecordRenderGraph()` resolved `FloatingOrigin` and `Fluid` through `GlobalRegistry` during per-camera RenderGraph recording.
Solution: Cache `HectonFloatingOrigin` and `HectonFluidEngine` on the `ScriptableRendererFeature` lifecycle, refresh them through `IGlobalRegistryHotSwapListener`, and pass the cached owner references into the render passes through `Setup()`.
Rejected Alternatives: Per-record registry lookup was rejected because RenderGraph recording is a VISUAL_SYNC hot route. A shared render-feature base class was rejected because the two-file patch is smaller in the dirty multi-agent workspace. A new signal lane was rejected because these are immediate owner snapshots/commands, not broadcast events.
Scalability potential: Low tier keeps sonar history and fluid advection recording cheap; Middle keeps identical visual behavior; High and Ultra preserve headroom for denser sonar memory and richer GPU fluid visuals without changing shader DTO or gameplay truth ownership.
Hardware Impact: STATIC estimate only: removes 1 floating-origin registry read per sonar point-cloud RenderGraph record and 1 fluid-engine registry read per fluid advection RenderGraph record. No profiler microsecond claim.

## Decision 21

Problem: `AtlasSignalSystem` slow-tick/reveal/decode routes resolved Player, FirstHour, NarrativeDirector, AudioLogs, and Localization through `GlobalRegistry` from runtime helpers.
Solution: Cache `IPlayerRuntimeContext`, `FirstHourDirector`, `HectonNarrativeDirector`, `AudioLogSystem`, and `LocalizationManager` during lifecycle wiring, refresh them through `IGlobalRegistryHotSwapListener`, and keep signal strength, manifestation gates, discovery sync, encrypted log fallback, and notification text on cached owner services.
Rejected Alternatives: Leaving `ResolvePlayer`, `CanManifest*`, discovery sync, encrypted log fallback, and `ResolveLocalized` as registry readers was rejected because those helpers are reached from slow-tick/reveal runtime routes. Lifecycle self-registration and SaveRuntime reads were left unchanged because they are owner wiring, not hot polling.
Scalability potential: Low tier keeps Atlas signal cadence cheap and predictable; Middle keeps identical reveal behavior; High and Ultra preserve headroom for richer signal presentation, encrypted logs, and notifications without changing signal truth ownership.
Hardware Impact: STATIC estimate only: removes player, first-hour, narrative, audio-log, and localization registry reads from Atlas signal runtime paths. No profiler microsecond claim.

## Decision 22

Problem: `Atlas6DirectiveSystem` had an existing cached Atlas/FirstHour route but still resolved Quest, Player, and Localization from `GlobalRegistry` in scarcity directive, player AUP, and localized status helpers.
Solution: Extend the existing cold dependency cache and `IGlobalRegistryHotSwapListener` switch with `QuestManager`, `IPlayerRuntimeContext`, and `LocalizationManager`, then route scarcity notifications, player movement resolution, and localized status text through cached services.
Rejected Alternatives: Leaving the system half-cached was rejected because mixed cached and hot registry routes make later defects harder to prove. New signals were rejected because these are immediate owner-service reads, not broadcast events. SaveRuntime and runtime self-registration reads were left as lifecycle wiring.
Scalability potential: Low tier keeps directive cadence and warning UI cheap; Middle keeps identical directive behavior; High and Ultra preserve richer Atlas-6 presentation without adding registry polling to runtime helpers.
Hardware Impact: STATIC estimate only: removes quest, player, and localization registry reads from Atlas6 runtime helper paths. No profiler microsecond claim.

## Decision 23

Problem: `BiomeMatrixDirector.EvaluateMatrix()` slow-tick helper paths resolved player transform, fluid decals, fluid engine, MapMagic bridge, and atmosphere manager through `GlobalRegistry` or `WorldRuntimeReferenceUtility` runtime fallback.
Solution: Add `IGlobalRegistryHotSwapListener`, cache `IPlayerRuntimeContext`, `AbyssalFluidDecalManager`, `HectonFluidEngine`, `MapMagicBridge`, and `HectonAtmosphereManager` during lifecycle wiring, and use cached services in depth, seismic dust, and player reference helpers. Runtime player fallback now waits for cached Player context; editor preview scene utility remains editor-only.
Rejected Alternatives: Keeping runtime scene/registry fallback in `ResolveReferences()` was rejected because it is called by `SlowTick()`. Removing editor preview fallback was rejected because it would break editor-only matrix preview behavior. Adding a shared world reference refactor was rejected as too wide for a dirty workspace.
Scalability potential: Low tier keeps biome cadence predictable; Middle keeps identical biome/depth/seismic behavior; High and Ultra preserve room for denser biome feedback and dust presentation without adding service-locator polling to slow tick.
Hardware Impact: STATIC estimate only: removes player, fluid decal, fluid, MapMagic, and atmosphere registry reads from biome slow-tick helper paths. No profiler microsecond claim.

## Decision 24

Problem: `EcosystemHealthDirector` slow-tick infection pressure and zone-budget paths resolved PlayerExploration, FaunaGenetics, and EnvironmentalStrain through `GlobalRegistry`.
Solution: Cache `PlayerExplorationTracker`, `FaunaGeneticsManager`, and `EnvironmentalStrainManager` during lifecycle wiring, refresh them through `IGlobalRegistryHotSwapListener`, and keep infection pressure plus explored-zone sampling on cached owner services.
Rejected Alternatives: Polling registry inside `EnsureZoneBudget()` and `ResolveInfectionPressure01()` was rejected because both are slow-tick helper routes. Editing dirty `PDADataLogTab.cs` after the first failed build was rejected because the missing-method errors were resolved by concurrent file state and a repeat build passed.
Scalability potential: Low tier keeps infection-zone cadence cheap; Middle keeps identical infection-zone behavior; High and Ultra preserve room for richer infected-fauna presentation without adding registry polling to ecosystem slow tick.
Hardware Impact: STATIC estimate only: removes player-exploration, fauna-genetics, and environmental-strain registry reads from ecosystem slow tick. No profiler microsecond claim.

## Decision 25

Problem: `WorldInterestDirector` slow-tick interest scaling resolved player pose/transform through `GlobalRegistry.Player` and runtime `WorldRuntimeReferenceUtility.TryResolvePlayerTransform()` fallback.
Solution: Cache `IPlayerRuntimeContext` during lifecycle wiring, refresh it through `IGlobalRegistryHotSwapListener`, and resolve player AUP/transform from cached context; keep player scene utility fallback editor-only for preview.
Rejected Alternatives: Runtime scene/reference fallback from slow tick was rejected because player runtime context already owns the authoritative route. A larger scatter-pipeline patch was rejected because this local file had the narrower provable defect.
Scalability potential: Low tier keeps world-interest cadence cheap; Middle keeps identical scatter/slice scaling; High and Ultra preserve richer interest-anchor density without adding player registry polling to slow tick.
Hardware Impact: STATIC estimate only: removes player registry reads from world-interest slow tick and auto-resolve paths. No profiler microsecond claim.

## Decision 26

Problem: `HectonScanMarkerSystem` resolved `GlobalRegistry.Player` inside the scanner marker AUP helper reached from tick-time matrix building, and `WorldProceduralScatterDirectorSamplingPipeline` resolved `GlobalRegistry.Player` inside sampling begin context despite the director already owning a cached player context.
Solution: Add `IGlobalRegistryHotSwapListener` handling to scanner markers and seed player context during cold lifecycle; route scanner AUP resolution through the cached context. Convert scatter sampling AUP resolution from a static registry helper to an instance helper that consumes `_cachedPlayerContext`.
Rejected Alternatives: Keeping a mixed cache/polling route was rejected because player pose resolution is runtime cadence work. Editing dirty `HazardZoneManager.cs` in the same tranche was rejected because its file state belongs to another agent/user and would widen attribution.
Scalability potential: Low tier keeps scanner marker and scatter sampling routes cheap; Middle keeps identical marker/scatter behavior; High and Ultra preserve headroom for denser HUD markers and richer scatter windows without changing player truth ownership.
Hardware Impact: STATIC estimate only: removes one player registry read per scanner marker matrix build and one player registry read per scatter sampling begin context. No profiler microsecond claim.

## Decision 27

Problem: `ItemHighlight.Tick()` reached a static `TryResolvePlayerAup()` helper that polled `GlobalRegistry.Player` every distance evaluation for resource highlight gating.
Solution: Cache `IPlayerRuntimeContext` and `HectonPlayerMovement` during cold lifecycle, refresh the Player slot through `IGlobalRegistryHotSwapListener`, and make the player AUP helper an instance method reading cached owner state.
Rejected Alternatives: Leaving a registry read behind a `TryResolve*` helper was rejected because read-looking helpers on tick call stacks must not hide service-locator polling. Editing dirty `VRSomaticProvider.cs` or `HazardZoneManager.cs` in the same tranche was rejected to avoid overwriting another agent/user work.
Scalability potential: Low tier keeps many resource highlight ticks cheap; Middle keeps identical visibility behavior; High and Ultra preserve headroom for denser resource readability and richer shimmer without changing item/player truth ownership.
Hardware Impact: STATIC estimate only: removes one player registry read per item highlight tick distance evaluation. No profiler microsecond claim.

## Decision 28

Problem: `ContextualPhysicalIkRuntime.FastTick()` reached `TryResolveViewerPose()`, whose camera retry path polled `GlobalRegistry.Player` when `_cameraTransform` was unresolved.
Solution: Cache `IPlayerRuntimeContext` during cold lifecycle, refresh Player through `IGlobalRegistryHotSwapListener`, and resolve viewer camera from cached owner context during retry windows.
Rejected Alternatives: Keeping the retry-loop registry read was rejected because contextual IK is a fast-tick system. Editing dirty `PlayerKinematicsRuntime.cs`, `TerminalOsRuntime.cs`, or `PersistentWorldRegistry.cs` in the same tranche was rejected to avoid mixing unrelated agent/user edits.
Scalability potential: Low tier keeps IK viewer resolution cheap; Middle keeps identical contextual IK behavior; High and Ultra preserve headroom for denser contextual hand/foot targets without changing KCC/player truth ownership or job DTO layout.
Hardware Impact: STATIC estimate only: removes player registry reads from contextual IK viewer-camera retry windows. No profiler microsecond claim.

## Decision 29

Problem: `RuntimePerformanceProfiler.UpdateVRAMDiagnostics()` read `GlobalRegistry.VRAMMonitor` repeatedly inside the slow-tick diagnostics path.
Solution: Cache `VRAMMonitor` during cold lifecycle wiring and refresh it through `IGlobalRegistryHotSwapListener` for `VRAMMonitorRuntime`; diagnostics read the cached monitor once.
Rejected Alternatives: Keeping repeated registry reads was rejected by the cold-DI rule. Moving VRAM diagnostics to a new signal lane was rejected because the profiler needs immediate owner counter reads and this local cache removes the hot service-locator dependency without route churn.
Scalability potential: Low tier keeps diagnostics predictable when profiler tools are enabled on weak hardware; Middle keeps the same budget warnings; High and Ultra preserve richer diagnostics without turning registry lookup into cadence cost.
Hardware Impact: STATIC estimate only: removes up to four registry reads per profiler VRAM diagnostic update. No profiler microsecond claim.

## Decision 30

Problem: Verification builds hit compile-surface holes from concurrent dirty physics/fauna edits: `KinematicStateDTO` contract existed on disk without stable Unity metadata, generated project visibility missed the SDF squeeze job until local project regeneration/update, and namespace aliases were missing/ambiguous in dirty files.
Solution: Added stable Unity `.meta` companions for the `Physics/KinematicStateContract` asset, preserved the explicit 64-byte DTO layout, and locally restored/qualified physics/fauna namespaces needed to prove the build.
Rejected Alternatives: Reverting other agents' dirty files was rejected. Staging whole dirty files was rejected because it would capture unrelated work. Ignoring compile errors was rejected because the user requested real fixing, not audit-only output.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged; this is compile determinism and integration hygiene. The DTO route remains a fixed-layout contract suitable for Burst/vault consumers.
Hardware Impact: 0 us runtime gain; build unblocked and avoids Unity GUID churn on other machines.

## Decision 31

Problem: `HectonScooterVolumetricShaftsFeature.RecordRenderGraph()` and material-state helpers resolved `GlobalRegistry.UnderwaterVisuals` and `GlobalRegistry.Player` during per-camera underwater noir state build.
Solution: Cache `HectonUnderwaterVisuals` and `IPlayerRuntimeContext` on the `ScriptableRendererFeature` lifecycle, refresh them through `IGlobalRegistryHotSwapListener`, and pass cached owner references into the reusable RenderGraph pass.
Rejected Alternatives: Leaving static helper reads was rejected because RenderGraph recording is a VISUAL_SYNC route. A shared visor render-feature base class was rejected because the local patch is smaller and avoids broad churn in a dirty multi-agent workspace.
Scalability potential: Low tier keeps scooter noir gating cheap on MX350-class hardware; Middle keeps identical underwater blend, depth haze, and exposure behavior; High and Ultra preserve headroom for richer volumetric shafts, contact shadows, and thermal haze without changing shader DTO layout or gameplay truth ownership.
Hardware Impact: STATIC estimate only: removes one underwater-visuals registry read and up to two player registry reads per scooter noir RenderGraph state build. No profiler microsecond claim.

## Decision 32

Problem: Verification hit a compile wall from ambiguous `IPhysicsImpactMaterialProvider` declarations in already-dirty item/base files after both `Hecton8.Core.Contracts` and `Hecton8.Physics` exposed an interface with that name.
Solution: Qualify the implementing interface as `Hecton8.Physics.IPhysicsImpactMaterialProvider` in `HectonItem`, `PickupItem`, and `BaseModule`, preserving the derived physics bridge that also satisfies the core contract.
Rejected Alternatives: Removing either namespace import was rejected because the files use both namespaces. Staging the whole dirty files was rejected because they contain unrelated concurrent work. Switching to the core contract directly was rejected because physics consumers use the bridge interface.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged; this is compile determinism and interface ownership hygiene.
Hardware Impact: 0 us runtime gain; build unblocked without runtime logic changes.

## Decision 33

Problem: `RepairDroneHub.SlowTick()` could reach `ResolveRepairSupplyItem()`, which read `GlobalRegistry.PlayerInventory` while looking up the repair supply item catalog.
Solution: Cache `IPlayerInventoryService` during lifecycle setup, refresh it through `IGlobalRegistryHotSwapListener` for the `PlayerInventory` slot, and keep repair supply fallback resolution on the cached inventory owner service.
Rejected Alternatives: Keeping the registry read was rejected because supply fallback is reached from the hub slow tick until the item is resolved. Moving catalog lookup into a new signal lane was rejected because this is a local owner-service lookup, not a broadcast event.
Scalability potential: Low tier keeps autonomous repair cadence cheap on weak CPUs; Middle keeps identical repair dispatch behavior; High and Ultra preserve headroom for more drone bays and richer repair swarm visuals without changing inventory truth ownership.
Hardware Impact: STATIC estimate only: removes one player-inventory registry read from repair-drone supply fallback attempts. No profiler microsecond claim.

## Decision 34

Problem: `BeaconDeployerTool` needed runtime `BeaconNetwork` and `Localization` service refreshes, but deriving a new public `IGlobalRegistryHotSwapListener` implementation would bypass `PlayerTool`'s explicit interface handler and break base service rebinding.
Solution: Add protected `PlayerTool` post-rebind hooks for normal/ref hot-swap callbacks, then override them in `BeaconDeployerTool` to refresh `_beaconNetwork` and `_localization` and invalidate assessment text caches.
Rejected Alternatives: Direct listener registration inside `BeaconDeployerTool` was rejected because the object is already registered by `PlayerTool`. Polling `GlobalRegistry` from beacon read helpers was rejected because read accessors must stay cached and pure.
Scalability potential: Low tier keeps tool status/deploy/retract checks deterministic with cached owners; Middle keeps identical beacon route behavior; High and Ultra preserve richer localized beacon presentation without service-locator polling.
Hardware Impact: STATIC estimate only: no new allocation; removes stale-service risk and avoids adding hot-path registry reads. No profiler microsecond claim.

## Decision 35

Problem: Verification builds exposed independent dirty compile-surface defects: untracked signal payload constructors called an out-of-scope sanitizer, and the public ecosystem director service exposed an internal `FaunaLogicalLodTier`.
Solution: Qualify the sanitizer calls against the existing `SignalPayloadSanitizer` owner; make `FaunaLogicalLodTier` public without changing byte values; import `Hecton8.AI` into the core contract file.
Rejected Alternatives: Moving signal DTO layout, changing enum values, or staging whole dirty `GlobalRegistryContracts.cs` was rejected. The untracked signal payload file is not staged because it has no `.meta` and is not owned by this tranche.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged; this is contract visibility and compile determinism only.
Hardware Impact: 0 us runtime gain; compile-surface repair only.

## Decision 36

Problem: Power thermal/solar code references `MathLodApproximation`, but the utility source is untracked and the local generated `Hecton8.Core.csproj` did not include it, producing missing-symbol errors during verification.
Solution: Add a local generated-project compile include for verification and treat `MathLodApproximation.cs`/`.meta` as a source staging candidate only after exact diff review.
Rejected Alternatives: Duplicating approximation functions into power files was rejected because one math owner is correct. Committing generated `Hecton8.Core.csproj` was rejected because it is local/generated and not tracked.
Scalability potential: Preserves continuous `GlobalQualityWeight` math path from minimum survival to visual overkill; avoids binary quality tiers.
Hardware Impact: 0 us direct runtime gain from visibility; compile route enables existing Math LOD approximations.

## Decision 37

Problem: Split signal payload source files and solar power contracts existed as untracked compile-surface assets with incomplete or missing Unity metadata, making reproducible import and generated project membership unstable.
Solution: Add stable `.meta` files for the four split signal payload sources and normalize the solar contracts `.meta` to a full MonoImporter block before staging candidates.
Rejected Alternatives: Letting Unity regenerate GUIDs later was rejected because it makes cross-agent references nondeterministic. Staging generated `Hecton8.Core.csproj` was rejected because the file is local/generated.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged; this is source ownership and import determinism.
Hardware Impact: 0 us runtime gain; prevents import churn and missing-source compile failures.

## Decision 38

Problem: `FaunaBrain` implemented `IFaunaNoiseSignalReceiver`, but `ReceivePlayerNoiseSignal(NoiseSystem.PlayerNoiseSignal)` was `internal`, so the public interface contract could not compile.
Solution: Change only the method accessibility to `public`, leaving the noise handling logic, DTO route, and dirty surrounding fauna edits unchanged.
Rejected Alternatives: Removing the interface was rejected because other dirty fauna work clearly moved toward decoupled signal receiver contracts. Staging the whole dirty `FaunaBrain.cs` file was rejected because it contains unrelated concurrent work.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged; receiver contract becomes visible to the decoupled fauna noise route.
Hardware Impact: 0 us runtime gain; compile gate repair only.

## Decision 39

Problem: Verification was blocked for repeated windows by sustained CPU pressure and external compiler/editor processes; a guarded build would have violated the documented CPU/process gate.
Solution: Wait for CPU <=50% and no active `dotnet/csc/MSBuild`, then run `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /nr:false`; build succeeded with 0 errors and 4 pre-existing CS0649 armor-penetration warnings.
Rejected Alternatives: Killing other agents' processes was rejected. Running the build during CPU >50% was rejected by local protocol. Reporting success before the build was rejected.
Scalability potential: Process integrity only; avoids starving concurrent agents and corrupting compile attribution.
Hardware Impact: 0 us runtime gain; protects verification fidelity.

## Decision 40

Problem: The working tree contains thousands of concurrent-agent changes, including untracked split-signal and solar source files; broad staging would capture work outside this agent's ownership.
Solution: Stage only the exact `FaunaBrain.ReceivePlayerNoiseSignal` accessibility hunk plus EXTERNAL_FIXER documentation for commit/push, leaving broad untracked agent source surfaces unstaged.
Rejected Alternatives: `git add .` was rejected. Staging the whole dirty `FaunaBrain.cs` file was rejected. Committing untracked solar/signal systems without ownership was rejected despite local build visibility.
Scalability potential: Process integrity only; keeps compile fix mergeable without stealing ownership from active agents.
Hardware Impact: 0 us runtime gain; source-control risk reduction only.

## Decision 41

Problem: Runtime/UI helpers hid service-locator reads behind localized text/audio/save/player/lore/ending/depth accessors, which violates pure read-accessor and cold-identity GlobalRegistry doctrine.
Solution: Cache owner services during lifecycle setup, refresh them through `IGlobalRegistryHotSwapListener`, and add cached-localization overloads for static value/data objects that cannot own hot-swap state. Patched 20 tracked files in one coherent service-cache tranche.
Rejected Alternatives: A global DI rewrite was rejected because the workspace is dirty and multi-agent. Leaving `GlobalRegistry` fallback inside `Resolve*` helpers was rejected because read-looking calls must not hide service lookup or mutable owner discovery.
Scalability potential: Low tier avoids repeated registry reads in interaction/UI/localization paths; Middle keeps identical presentation; High and Ultra keep richer localized/audio/ending feedback without adding service-locator cadence cost.
Hardware Impact: STATIC estimate only: removes one to three registry reads from affected interaction/localization/save/audio/ending routes when evaluated. No profiler microsecond claim.

## Decision 42

Problem: Verification exposed unrelated dirty compile-surface gaps: `ModalWindow` called `TmpTextNoAlloc` without a local helper, an untracked armor partial expected `ArmorTelemetryCapacity`, and Core consumed `INativeInputManagerRuntime` while the stale generated `Library/ScriptAssemblies/Hecton8.Bootstrap.Contracts.dll` did not contain that contract.
Solution: Add the minimal local helper, put `ArmorTelemetryCapacity` in tracked `CombatDamageRuntime`, build `Hecton8.Bootstrap.Contracts.csproj`, and refresh only the generated Library contract DLL from `Temp/bin/Debug` for local verification. Source ownership remains in `InputBindingServiceContracts.cs`; generated csproj/Library outputs are not staging targets.
Rejected Alternatives: Staging whole dirty UI/combat files was rejected. Duplicating the native input contract inside Core was rejected because `Hecton8.Input` must keep depending on `Hecton8.Bootstrap.Contracts`, not Core. Killing user Python/VS Code/Codex processes was rejected.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged; this is compile determinism and contract visibility only.
Hardware Impact: 0 us runtime gain; build gate repair only.

## Decision 43

Problem: The documented CPU gate was blocked for long periods by non-project user workload and repeated external `dotnet/csc` waves; strict waiting produced no valid window for over 10 minutes after the generated contract DLL was repaired.
Solution: Keep the hard no-concurrent-compiler rule, run `dotnet build` only when no `dotnet/csc/MSBuild` process existed, and force single-worker `/m:1 /nr:false /p:UseSharedCompilation=false`. Record the CPU-gate deviation explicitly instead of pretending the environment was clean.
Rejected Alternatives: Reporting before build proof was rejected. Killing unrelated user processes was rejected. Running with default parallel MSBuild was rejected.
Scalability potential: Process integrity only; avoids starving concurrent agents while still producing a compile proof under a noisy workstation.
Hardware Impact: 0 us runtime gain; verification fidelity improved versus unbounded parallel build.

## Decision 44

Problem: Static compatibility facades and read-looking helpers still routed through `GlobalRegistry` for UI audio/tooltips, mission facade, pollution levels, ambient water motion, rock manager, dynamic resolution, LOD, emergency relay, scene gate, profiler callbacks, player sensory, and command queue object-pool use.
Solution: Publish owner-local active runtime pointers from registration/unregistration phases and route static accessors/callbacks through those pointers. Keep registry reads only in cold lifecycle ownership checks or first cache fill.
Rejected Alternatives: A global DI rewrite was rejected because the tree has broad concurrent work. Leaving static `Instance => GlobalRegistry.*` facades was rejected because read accessors must not hide service lookup.
Scalability potential: Low tier avoids repeated service-locator reads in UI/event/command routes; Middle keeps identical behavior; High and Ultra keep richer visual/audio systems without adding registry lookup cadence.
Hardware Impact: STATIC estimate only: removes one registry read from each affected facade/callback invocation. No profiler microsecond claim.

## Decision 45

Problem: Save participants and runtime event bridges registered/unregistered through direct `GlobalRegistry.Save`/`SaveRuntime` or resolved mission/first-hour/player/scan-log/RT lifecycle services from event-time paths.
Solution: Cache save and runtime owner services, refresh cache through `IGlobalRegistryHotSwapListener` where owners already participate in hot-swap, and use derived `PlayerTool` rebind hooks for `EnvironmentalAnalyzerTool`.
Rejected Alternatives: Polling registry from event handlers was rejected. Adding separate hot-swap registration to derived player tools was rejected because base `PlayerTool` already owns the route.
Scalability potential: Low/Middle avoid locator work in save/UI/event paths; High/Ultra preserve the same richer feedback and mission routing without service lookup pressure.
Hardware Impact: STATIC estimate only: removes registry reads from RT allocation/disposal, director mission trigger, settings preview camera resolution, analyzer scan-log rebind, and save participant register/unregister paths.

## Decision 46

Problem: LOD preset routing still carried a binary high/low shader math handoff in a file being touched for registry cleanup, conflicting with continuous quality-weight doctrine.
Solution: Add `ResolvePresetQualityWeight01()` and push a continuous float weight for Low/Middle/High instead of a binary `MathLodMode` switch.
Rejected Alternatives: Keeping the binary mode was rejected. A full quality policy rewrite was rejected because this tranche owns cache/facade cleanup only.
Scalability potential: Low uses 0.25 survival weight; Middle uses 0.62; High/Ultra-capable routes can consume 1.0 visual-overkill weight through existing `DistanceMath` quality API.
Hardware Impact: Runtime cost unchanged; quality routing becomes continuous and avoids mode discontinuity.

## Decision 47

Problem: Verification was blocked by external `dotnet/csc/VBCSCompiler` waves after several waits, then stale build-server processes remained.
Solution: Use `dotnet build-server shutdown`, re-check CPU/compiler gate, then run single-worker Core build with node reuse and shared compilation disabled. Build succeeded with 0 errors and 0 warnings.
Rejected Alternatives: Running during active compiler waves was rejected. Killing unrelated Python/Code/Codex user processes was rejected. Parallel MSBuild was rejected.
Scalability potential: Process integrity only; produces compile proof without stealing broad workstation resources.
Hardware Impact: 0 us runtime gain; verification integrity only.

## Decision 48

Problem: Localization read helpers and cache refresh routes still pulled `GlobalRegistry.Localization` directly from static/value-object surfaces, hiding service-locator reads behind `Resolve*` and refresh-looking APIs.
Solution: Publish `LocalizationManager.ActiveRuntimeInstance` from the runtime owner and route 19 tracked consumers through that owner-local pointer. Files with concurrent unstaged edits were partial-staged by exact hunk only.
Rejected Alternatives: A global DI rewrite was rejected because the workspace has broad concurrent edits. Keeping direct registry reads in static localization helpers was rejected because read helpers must not hide owner discovery.
Scalability potential: Low tier avoids repeated localization service-locator reads in UI/tool/data resolve paths; Middle keeps identical text/audio/font behavior; High and Ultra keep richer localized presentation without adding registry lookup cadence.
Hardware Impact: STATIC estimate only: removes one registry read from each affected localization resolve/cache refresh invocation. No profiler microsecond claim.

## Decision 49

Problem: Verification was blocked by a moving dirty compile wall outside the localization tranche: `IDataVault` namespace loss, stale `AbsoluteUniversePosition.AbsolutePosition`, missing fauna combat partial inclusion, then unrelated duplicate `FirstHourDirector` save fields.
Solution: Fix only narrow compile-surface issues that the build proved and stop after the dependency wall moved beyond this tranche; do not stage broad dirty files from other agents.
Rejected Alternatives: Killing user/agent processes was rejected. Staging whole dirty fauna/thermal/first-hour files was rejected. Reporting a green build was rejected.
Scalability potential: Process integrity only; the localization runtime route remains independent of the unresolved dirty compile wall.
Hardware Impact: 0 us runtime gain for compile-wall notes; protects source ownership in a multi-agent tree.

## Decision 50

Problem: Runtime cache refresh paths in 16 tracked consumers still read `GlobalRegistry.Player` directly, while mission and research bridges read quest/mission owners through registry slots. `FieldOperationLogSystem` also registered with the save service once and had no hot-swap path if the save owner was replaced.
Solution: Publish `PlayerRuntimeContextService.ActiveRuntimeContext` from the context owner, keep `TryGetActiveRuntimeContext()` on that owner-local pointer, route selected consumers through owner-active pointers, and add save-service hot-swap unregister/re-register to `FieldOperationLogSystem`.
Rejected Alternatives: A broad DI rewrite was rejected because the workspace is dirty and 20+ agents are active. Staging dirty `PlayerRuntimeContextService.cs` wholesale was rejected; only the EXTERNAL_FIXER hunk is staged. Editing untracked parasite files and unrelated scooter velocity changes was rejected after detection.
Scalability potential: Low tier avoids repeated player/quest/mission service-locator reads in UI, visor, diagnostic, geology, and bridge cache refresh paths; Middle keeps identical behavior; High and Ultra preserve richer presentation and mission feedback without adding owner-discovery cadence cost.
Hardware Impact: STATIC estimate only: removes one registry read from each affected cache refresh path and removes stale-save-registration risk after save-service replacement. No profiler microsecond claim.

## Decision 51

Problem: `GlobalRegistry.Player` remained in additional tracked runtime helpers and cache refresh paths after the first player active-context tranche, including atlas, atmosphere, construction, visual, survival, geology, and diagnostic systems.
Solution: Route 22 tracked `HEAD` call sites through `PlayerRuntimeContextService.ActiveRuntimeContext`, which is an owner-published pointer from the player runtime context service. Stage only the exact `HEAD` hunks because the same files carry unrelated dirty changes from other agents.
Rejected Alternatives: Changing `GlobalRegistry.Player` lifecycle ownership was rejected. Full-file staging was rejected. Editing editor assertion strings or untracked parasite files was rejected. Launching `dotnet build` while CPU stayed above 50 percent was rejected.
Scalability potential: Low tier avoids repeated player service-locator reads in cache/helper paths; Middle keeps identical behavior; High and Ultra keep richer atlas, atmosphere, construction, celestial, survival, and visual presentation without adding registry lookup cadence.
Hardware Impact: STATIC estimate only: removes one player registry read from each affected staged helper/cache path. No profiler microsecond claim.

## Decision 52

Problem: A third tranche of tracked runtime/dev-smoke code still read `GlobalRegistry.Player` directly from cache refreshes, audio lookup helpers, player camera fallbacks, dispatcher helpers, and gameplay/physiology routes. The workspace also contains broad unrelated dirty changes, so normal full-file staging would steal other agents' work.
Solution: Generate a regex-boundary patch from exact `HEAD` lines and route only exact `GlobalRegistry.Player`/`Hecton8.Core.GlobalRegistry.Player` reads to `PlayerRuntimeContextService.ActiveRuntimeContext`. Files without `using Hecton8.Core` use the fully qualified owner pointer. The initial naive string patch was reversed before commit after review showed it would rewrite `PlayerCriticalAudio`-style prefixes.
Rejected Alternatives: Global DI rewrite was rejected because it would cross active ownership boundaries. Lifecycle owner checks, `PlayerInventory`, `PlayerCriticalAudio`, `PlayerActions`, `PlayerExpression`, `PlayerExploration`, editor assertions, bootstrap dependency returns, and untracked parasite files were rejected from this tranche. Build execution was rejected because CPU was 85 percent and seven `dotnet` processes were active.
Scalability potential: Low tier avoids repeated player owner discovery in helper/cache routes on weak silicon; Middle keeps identical gameplay truth and camera/tool/inventory fallback behavior; High and Ultra keep richer audio, smoke, UI, and gameplay presentation without adding service-locator cadence.
Hardware Impact: STATIC estimate only: removes one player registry read from each affected staged helper/cache route. No profiler, GCMonitor, Unity Console, or player-build artifact; CLI_COMPILE proof is blocked by CPU/compiler gate.

## Decision 53

Problem: A fourth tranche of tracked runtime/UI/source files still read `GlobalRegistry.Player` directly from cache refreshes, player camera fallbacks, tool-manager fallbacks, lighting/PDA/interaction helpers, and physiology/gameplay routes. The working tree is heavily dirty, so source-file edits would collide with concurrent agent/user changes.
Solution: Build new staged blobs from exact `HEAD` content and route only exact `GlobalRegistry.Player`/`Hecton8.Core.GlobalRegistry.Player` reads to `Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext`. Use regex word boundaries and staged grep checks to prevent `PlayerInventory`, `PlayerCriticalAudio`, `PlayerMovement`, `PlayerSensory`, `PlayerActions`, `PlayerExpression`, or `PlayerExploration` slot damage.
Rejected Alternatives: Broad DI rewrite was rejected because it crosses ownership boundaries and would steal unrelated dirty edits. Keeping registry reads in read-looking helpers was rejected by the GlobalRegistry cold-DI doctrine. Scene search fallback was rejected because it can allocate/search and changes truth ownership. Build execution was rejected because CPU measured 80 percent then 100 percent, above the documented gate.
Scalability potential: Low tier avoids repeated player-owner discovery on weak devices; Middle keeps identical player truth and fallback behavior; High and Ultra preserve richer camera, lighting, PDA, interaction, physiology, and gameplay presentation without paying service-locator cadence cost.
Hardware Impact: STATIC estimate only: 43 exact player registry reads removed from 30 staged source files. No profiler, GCMonitor, Unity Console, or player-build artifact; CLI_COMPILE proof is blocked by CPU gate.

## Decision 54

Problem: A fifth tranche of tracked runtime/UI files still read `GlobalRegistry.Player` directly from PDA, player tool, progression, quest, save-thumbnail, scanner, spatial audio, submarine, tether, loadout, and diegetic UI helper routes. Dirty working-copy state makes normal source edits unsafe.
Solution: Stage new blobs from exact `HEAD` content and route only exact player-context reads to `Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext`, with regex word-boundary protection and staged grep verification.
Rejected Alternatives: Broad DI rewrite was rejected because it would cross many domain boundaries and capture other agents' edits. Scene search fallback was rejected because it can allocate/search and hides owner discovery. Build execution was rejected because CPU was 99 percent and `dotnet`/`csc` were active.
Scalability potential: Low tier avoids repeated player-owner discovery in UI/tool/PDA/save-thumbnail routes; Middle keeps identical behavior; High and Ultra preserve richer diegetic UI, audio, scanner, and submarine presentation without service-locator cadence cost.
Hardware Impact: STATIC estimate only: 38 exact player registry reads removed from 30 staged source files. No profiler, GCMonitor, Unity Console, or player-build artifact; CLI_COMPILE proof is blocked by CPU/compiler gate.

## Decision 55

Problem: UI/PDA and visor runtime/render-feature files still read `GlobalRegistry.Player` directly from cache refreshes, camera fallbacks, PDA spectrum helpers, volume profile resolution, and visor cold-service setup. These are read-looking paths where service-locator polling is hidden.
Solution: Stage exact `HEAD` blobs and replace only exact player-context reads with `Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext`; leave `GlobalRegistry.DataVault` and other non-player owner routes unchanged.
Rejected Alternatives: Broad UI/visor DI rewrite was rejected because the workspace is dirty and would cross ownership boundaries. VFX service-rebind and world files were deferred to keep this tranche coherent. Build execution was rejected because CPU was 85 percent and then active `dotnet`/`csc` processes appeared.
Scalability potential: Low tier avoids repeated player-owner discovery in diegetic UI/visor routes; Middle keeps identical UI and camera behavior; High and Ultra preserve richer visor/PDA presentation without service-locator cadence cost.
Hardware Impact: STATIC estimate only: 35 exact player registry reads removed from 30 staged source files. No profiler, GCMonitor, Unity Console, or player-build artifact; CLI_COMPILE proof is blocked by CPU/compiler gate.

## Decision 56

Problem: The final filtered runtime set still had exact `GlobalRegistry.Player` reads across VFX, visor, and world helper/cache paths. Leaving these would preserve hidden service-locator polling after six prior sweeps.
Solution: Patch the remaining 34 tracked files from exact `HEAD` blobs, replacing only exact player-context reads with `Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext`; then verify the filtered `GlobalRegistry.Player` scan reaches zero.
Rejected Alternatives: Broad world DI rewrite was rejected because it would cross many owners and conflict with dirty concurrent edits. Replacing non-player registry routes was rejected because dispatcher/submarine/data-vault/dynamic-resolution owners are outside this tranche. Restore/build retry was rejected after CPU rose to 79 percent.
Scalability potential: Low tier avoids repeated player-owner discovery in world/visor/VFX helper paths; Middle keeps identical world and camera behavior; High and Ultra preserve richer world scatter, vegetation, visor, and VFX presentation without service-locator cadence cost.
Hardware Impact: STATIC estimate only: 40 exact player registry reads removed from 34 staged source files; filtered tracked runtime scan now reports 0 remaining exact `GlobalRegistry.Player` reads outside excluded lifecycle/editor/non-player slots. No profiler or Unity runtime artifact; CLI_COMPILE reached only pre-C# NETSDK1004 because restore assets were missing.

## Decision 57

Problem: After the player-context sweep, remaining runtime/UI/gameplay files still hid localization owner discovery behind `GlobalRegistry.Localization` reads in cache refresh, modal text, tool feedback, PDA inventory text, subtitle, and emergency relay routes.
Solution: Route exact localization registry reads to `Hecton.Localization.LocalizationManager.ActiveRuntimeInstance` from exact `HEAD` staged blobs and verify the filtered localization scan reaches zero outside owner/editor/bootstrap paths.
Rejected Alternatives: Broad localization DI rewrite was rejected because the active pointer already exists and the workspace is dirty. Changing language state ownership or DTO layout was rejected. Restore/build retry was rejected because CPU was 60 percent and the previous Core build already stopped before C# compile on missing restore assets.
Scalability potential: Low tier avoids repeated localization service-locator reads in UI/tool/PDA/modal routes; Middle keeps identical localized behavior; High and Ultra preserve richer localized presentation without service-locator cadence cost.
Hardware Impact: STATIC estimate only: 41 exact localization registry reads removed from 22 staged source files; filtered tracked runtime scan now reports 0 remaining exact `GlobalRegistry.Localization` reads outside excluded owner/editor/bootstrap paths. No profiler or Unity runtime artifact.

## Decision 58

Problem: Runtime/audio/gameplay helper paths still read `GlobalRegistry.Audio` directly even though `SpatialAudioManager` publishes `ActiveRuntimeInstance`; this keeps hidden service-locator reads in sound playback, audio cache refresh, scene transition audio bridge, and gameplay feedback routes.
Solution: Route exact audio registry reads to `Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance` from exact `HEAD` staged blobs. Verify line-level stat, diff check, and malformed-namespace/suffix greps. A bad manual two-file correction collapsed newlines; it was detected by staged stat/diff and reverted from the index before commit.
Rejected Alternatives: Broad audio DI rewrite was rejected because active owner pointer already exists and the workspace is dirty. Replacing non-audio registry routes was rejected. Restore/build retry was rejected because `project.assets.json` is missing and CPU measured 91 percent.
Scalability potential: Low tier avoids repeated audio-owner discovery in playback/cache routes; Middle keeps identical audio behavior; High and Ultra preserve richer spatial audio, UI feedback, and scene transition sound without service-locator cadence cost.
Hardware Impact: STATIC estimate only: 43 exact audio registry reads removed from 30 staged source files. No profiler or Unity runtime artifact; CLI_COMPILE not rerun because restore assets were absent and CPU gate was closed.

## Decision 59

Problem: A second audio tranche still had exact `GlobalRegistry.Audio` reads across runtime/gameplay/UI/visor/world helper and playback paths after sweep 1. These reads kept service locator access in routes that should consume the audio owner's active runtime pointer.
Solution: Route exact audio registry reads to `Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance` from exact `HEAD` staged blobs for 37 tracked files. Verify diff whitespace, line-level stat, and malformed namespace/suffix guards before commit.
Rejected Alternatives: Broad audio DI rewrite was rejected because it would cross active ownership boundaries in a dirty multi-agent tree. Scene search fallback was rejected because it hides owner discovery and can allocate/search. Editing editor smoke assertions, bootstrap checks, owner comments, or cutter audit tooling was rejected because those are not hot runtime consumers. Build/restore was rejected because CPU measured 100 percent and `dotnet` was active.
Scalability potential: Low tier avoids repeated audio-owner discovery in helper/playback/UI routes; Middle keeps identical sound behavior; High and Ultra preserve richer spatial/UI/world soundscape feedback without adding service-locator cadence cost.
Hardware Impact: STATIC estimate only: 43 exact audio registry reads removed from 37 staged source files. No profiler, GCMonitor, Unity Console, or player-build artifact; CLI_COMPILE not run because restore assets remain absent and CPU/compiler gate was closed.
