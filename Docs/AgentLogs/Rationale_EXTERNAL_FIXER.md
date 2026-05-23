# Rationale_EXTERNAL_FIXER
Date: 2026-05-23
Status: VERIFIED - NINTH HOT-PATH REGISTRY TRANCHE

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
