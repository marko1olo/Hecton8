# Rationale_13AI

## Session Start

Problem: 13AI has no active XML batch prompt in `Docs/Tasks/CURRENT_BATCH.md`, but user provided a direct AI-domain audit/fix assignment.
Solution: Treat direct user assignment as controlling scope, record XML task count as 0, and create disk-backed state files before source edits.
Rejected Alternatives: Reading archived batch prompts or adjacent active prompts would violate strict parsing and contaminate architectural decisions.
Scalability potential: Applies to AI systems only; no runtime cost.
Hardware Impact: 0 us runtime. Administrative proof only.

Problem: AI domain touches fish, creature cognition, swarms, navigation, encounter pacing, and neutral/hostile drone behavior.
Solution: Select 8 mandates covering cognition, director, SDF nav, flocking, funnel pathfinding, execution phases, SignalBus lanes, and Zero-GC policy; read Registry/DI as a supplemental dependency reference, not as a selected mandate.
Rejected Alternatives: Generic Unity NavMesh/MonoBehaviour AI patterns are rejected because the project requires Burst/data-local/SDF-driven authority; counting supplemental Registry/DI as a ninth selected mandate would violate the 2-8 mandate cap.
Scalability potential: Low uses cadence/stride/path LOD and visual/acoustic fakes; Middle uses full nearby truth; High/Ultra spend saved time on richer presentation and sensory response, not new gameplay truth.
Hardware Impact: Mandate selection only; later fixes must target MX350/i3 frame budget and zero GC.

## Scoped Fixes

Problem: `AcousticEchoLocationRuntime.TryUpdatePredatorEcho()` and echo producer routes could bootstrap/refresh DataVault state from `Try*` call sites. That violates read-accessor purity and risks hidden owner work from creature AI consumers.
Solution: Added `TickOwnerFrame()` and called it from `FaunaDirector.Tick()` after acoustic signal drains. `TryUpdatePredatorEcho()` now reads cached trail state only. `TryEnqueueEchoTap()` uses a no-acquire pending tap view and records invalid producer data as a primitive pending fault drained by the owner phase.
Rejected Alternatives: Leaving `EnsureInitialized()` in the reader was rejected because it hides DataVault setup and job scheduling behind a query. Dumping black-box files directly from producer rejection was rejected because file I/O belongs to owner/fault phase, not enqueue path. HectonEventBus was rejected because this is a first-party hot route.
Scalability potential: Low tier avoids first-call ownership spikes during predator cognition. Middle keeps acoustic breadcrumbs without changing truth ownership. High/Ultra can spend the saved main-thread slack on richer acoustic head-sweep presentation while the trail state remains one owner/one route.
Hardware Impact: 0 verified us saved; profiler proof absent. Static estimate: removes one DataVault bootstrap/ensure plus possible owner refresh from each predator query path, a 5-30 us cold-spike risk on i3/MX350 and 0 B/frame GC target maintained.

Problem: `StressDrivenSpawnDirector` used a constant `AuthoritativeQualityWeight = 1f` in places that should scale cadence/capacity/probe work, effectively forcing Ultra spawn work on weak devices while blurring quality truth versus behavior truth.
Solution: Renamed the fixed behavior scalar to `AuthoritativeBehaviorWeight` and kept it only for gameplay-truth cognition values. Candidate scoring, budget selection, spawn probability bias, hidden spawn radius/probe count, cull radius, and debug reporting now consume continuous `input.GlobalQualityWeight`.
Rejected Alternatives: Binary low/ultra branches were rejected. Changing behavior truth with device quality was rejected because quality must not alter authority route or gameplay identity.
Scalability potential: Low uses 3 hidden probes and lower spawn pressure. Middle interpolates budgets and radius. High increases probes and density. Ultra spends available CPU on richer encounter placement rather than changing species truth.
Hardware Impact: 0 verified us saved; profiler proof absent. Static work reduction at quality 0: hidden probes drop from up to 19 to 3, 16 fewer placement probes per spawn attempt, 84.2% less probe work on that branch.

Problem: Phantom drone quality produced `phantomDrawCount` but indirect args, dispatch groups, and compute capacity still used full `PhantomDroneCount`, so weak hardware paid for invisible/low-priority swarm work.
Solution: `UpdatePhantomDroneArgs(int phantomDrawCount)` writes the actual instance count. `RenderPhantomSwarm()` dispatches only the continuous draw count and passes that as compute capacity.
Rejected Alternatives: Keeping full dispatch and clipping in shader was rejected because it wastes compute and vertex work. A binary disable path was rejected because `GlobalQualityWeight` must scale continuously.
Scalability potential: Low reaches zero phantom draw/dispatch. Middle draws a proportional swarm. High and Ultra restore dense phantom overkill without code path switches.
Hardware Impact: 0 verified us saved; profiler proof absent. Static work reduction: quality 0 draws/dispatches 0 instead of 500 phantom drones; quality 0.5 draws about 250 instead of 500.

Problem: Real headless drone rendering had append/culling resources but `TryRenderGpuCulledFleet()` returned false, so procedural draw used full `HeadlessDroneCapacity` and relied on vertex/fragment clipping for inactive drones.
Solution: Activated the GPU culling path: reset append counters, bind culling state/matrix/render-instance buffers, upload frustum planes and distance, dispatch the cull compute, copy visible count into indirect args, and draw from the compacted visible matrix buffer. Fallback full-capacity draw remains if compute/camera resources are missing.
Rejected Alternatives: CPU-compacting active drone matrices was rejected because slot sparsity would require extra per-frame CPU copy work. Setting instance count to active managed count was rejected because active slots are not guaranteed dense. Enabling the existing compute clear kernel was rejected after inspection because `CopyCount` overwrites the indirect instance count without binding the args buffer as `RWByteAddressBuffer`.
Scalability potential: Low avoids drawing offscreen/far/inactive drones when compute is available. Middle scales by actual visible fleet. High/Ultra can keep drone count high without forcing every drone through the vertex path.
Hardware Impact: 0 verified us saved; profiler proof absent. Static work reduction: visible count replaces 512-capacity draw; if 40 drones are visible, vertex instances drop by 472, a 92.2% instance reduction on that draw.

Problem: `AmbientBiotaDirector` exposed DataVault read-only views while `_jobPending` could still be writing the same buffers. `ReadOnly` does not mean phase-safe.
Solution: Public `BiotaAups`, `BiotaVelocities`, and `BiotaStates` now fail closed to default while the owner job is pending. Existing `IAmbientBiotaService` signatures remain unchanged.
Rejected Alternatives: Changing `IAmbientBiotaService` was rejected because it is a cross-domain contract. Completing the job from the getter was rejected because hidden `.Complete()` in a read accessor violates execution-phase doctrine.
Scalability potential: Low/Middle avoid race reads during drift/spawn jobs. High/Ultra retain full ambient density after LateFrame completion without exposing mutable in-flight buffers.
Hardware Impact: 0 verified us saved; correctness fix. It may skip one residency/ecosystem read during an active owner job instead of risking a race.

## Boid Black Box Route

Problem: `HectonBoidController` controlled GPU fish flock behavior but had no 300-frame native Black Box in the 13AI lane. A crash/NaN in target, grid, acoustic ping, dispatch count, or runtime buffers would leave no local forensic trail.
Solution: Added `BoidBlackBoxEntry[300]` in DataVault with `(BufferID)71979`, owner `SystemID.AIEcology`, and `LateFrameTick()` owner writes. Each row records frame, flags, boid count, target, bounds, spatial grid, dispatch group count, predator count, foveated tier, continuous quality weights, acoustic ping vectors, and a quantized state hash. Fault flags trigger a one-shot dump to `Docs/AgentLogs/Dump_13AI.bin`.
Rejected Alternatives: A managed `Queue`/array was rejected for GC and ownership reasons. A persistent owner-local `NativeArray` was rejected because project doctrine says cross-domain native ownership belongs in DataVault. Writing telemetry from getters/readers was rejected as read-accessor impurity. The first chosen `(BufferID)71990` was rejected after scan because `H8Memory` already owns it as `ShinobuParasiteProfileCount`.
Scalability potential: Low tier pays one bounded 128B row write after owner work and gets crash forensics. Middle/High/Ultra retain the same truth route and can spend saved bug time on richer fish presentation; `GlobalQualityWeight` is recorded continuously but does not alter telemetry ABI, save identity, or authority.
Hardware Impact: 0 verified us saved. Estimated added normal-frame cost is a fixed DataVault write of 128B plus primitive hash math; suspicious until profiler proves it below 0.1 ms, but bounded and owner-phase. On i3/MX350 this is preferable to post-crash blind debugging.

Problem: 13AI fault dumps used system-specific paths (`Dump_ACOUSTIC_ECHO_LOCATION_AI.bin` and initial boid `Dump_13AI_HectonBoidController.bin`) while the session rule requires `Docs/AgentLogs/Dump_[YourID].bin`.
Solution: Aligned acoustic echo and boid fault paths to `Docs/AgentLogs/Dump_13AI.bin`.
Rejected Alternatives: Keeping system-specific names was rejected because the CTO-facing proof artifact would fail the literal protocol. Writing multiple dump aliases was rejected for now because it adds extra fault-path I/O without a consumer contract.
Scalability potential: Same across Low/Middle/High/Ultra; the dump is fault-only and does not affect normal presentation scaling.
Hardware Impact: 0 us normal runtime. Fault-path file I/O remains intentionally exceptional.

Route Card: Hecton boid Black Box.
Fact owner: `HectonBoidController`.
Route: `LateFrameTick()` -> `RunBoidVisualSync()` -> `WriteBoidBlackBoxFrame()`.
Data owner: GlobalDataVault, `(BufferID)71979`, `BoidBlackBoxEntry[300]`, `SystemID.AIEcology`.
Readers: none in hot gameplay. Fault dump reads the same ring under write lock only after a fault flag.
Dump artifact: `Docs/AgentLogs/Dump_13AI.bin`.
Invariant: `GlobalQualityWeight` is recorded as continuous telemetry only; it does not change gameplay truth, DTO layout, or authority route.

## Verification Blocker

Problem: Build verification is blocked outside the 13AI domain. `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` failed on `Assets/Candice AI for Games/Scripts/Libs/Candice Save System/Overrides/CandiceSQLiteProvider.cs`: missing `Mono.Data` namespace and `SqliteDataReader`.
Solution: Record `[BLOCKED BY DEPENDENCY]` and stop build retries. The touched 13AI files passed static diff checks, dump-path scans, BufferID collision scans, and brace/paren balance checks.
Rejected Alternatives: Editing the Candice vendor save-system SQLite provider was rejected as outside AI fish/creature/drone authority. Repeating `dotnet build` without changing that dependency was rejected as useless load.
Scalability potential: None; compile gate proof only.
Hardware Impact: 0 runtime us. The build blocker prevents profiler/GCMonitor proof for the new boid Black Box cost.

## Repair Drone Hub Hot-Path Cleanup

Problem: `RepairDroneHub` kept active hubs in a static managed `List<RepairDroneHub>`. The list was small, but it still carried managed growth/remove semantics in a first-party drone route used by `DroneFleetManager` and `BasePollutionManager`.
Solution: Replaced it with fixed `RepairDroneHub[32]` plus explicit count. Unregister uses a bounded stable shift instead of swap-remove because `DroneFleetManager` has fallback reads of `GetActiveHubAt(0)`, and preserving original order is safer than saving a few assignments.
Rejected Alternatives: DataVault storage for Unity object references was rejected because object identity/lifetime belongs to MonoBehaviour lifecycle, not native cross-domain buffers. Swap-remove was rejected after dependency inspection because it could change fallback anchor behavior. Keeping `List<T>` was rejected because the project forbids casual managed runtime containers in hot/shared routes.
Scalability potential: Low tier gets bounded predictable scans with no list growth. Middle/High/Ultra keep the same hub truth and can add more drone visuals without changing hub authority. The route does not use `GlobalQualityWeight` because hub identity is gameplay truth, not fidelity.
Hardware Impact: 0 verified us. Static impact: active hub unregister remains at most 31 reference moves, but removes managed list capacity/growth risk and preserves deterministic fallback ordering on i3/MX350-class hardware.

Problem: `RepairDroneHub.DockingAirlock` called `ResolveDockingAirlock()`, which could run `TryGetComponent` / `GetComponentInParent` when the cache was empty. That made a read accessor capable of scene search from consumers such as `DroneFleetManager`.
Solution: Moved airlock resolution to `CacheDockingAirlockCold()` in `Awake`, `OnEnable`, and `OnSpawn`. `ResolveDockingAirlock()` now returns the serialized value or cached value only.
Rejected Alternatives: Completing lazy scene lookup from the accessor was rejected by read-purity doctrine. Adding a new registry route was rejected because the dependency is local component identity, not a global service.
Scalability potential: Low/Middle avoid first-access spikes during drone docking logic. High/Ultra keep the same docking behavior and spend saved frame budget on richer drone presentation, not extra truth.
Hardware Impact: 0 verified us. Static estimate: removes one `GetComponentInParent<BaseAirlock>()` miss from a possible runtime read path; cold lifecycle still pays it once if authoring omitted the reference.

Problem: Repair supply hash and target name diagnostics did avoidable runtime string/hash work. `ResolveAvailableRepairSupplyHashId()` recomputed default/legacy `LocHash` values, and `TryDispatchDrone()` assigned `task.Module.name` on player path.
Solution: Cached default/legacy repair hash ids statically, cached the selected `repairSupplyItem` hash per item reference, added `_debugCurrentTargetId`, and wrapped `_debugCurrentTargetName` assignment/clear behind `UNITY_EDITOR`.
Rejected Alternatives: Removing diagnostics entirely was rejected because designers still need editor visibility. Changing logistics APIs to accept item objects was rejected because hash ids are the existing first-party route.
Scalability potential: Low avoids string/name/hash overhead in drone dispatch. Middle/High/Ultra retain editor diagnostics and continuous visual scaling elsewhere; supply identity remains gameplay truth and is not quality-scaled.
Hardware Impact: 0 verified us. Static estimate: eliminates repeated default/legacy hash compute on logistics checks and player-runtime `module.name` string access during sortie launch.

Problem: `PathFunnelNavmeshRuntime.TryReadVoxelPathResult` was a suspected read-scan debt.
Solution: Deep dive left it unchanged: the scan is bounded to 1024, read-only, allocation-free, has no registry polling, and does not complete jobs. A faster keyed lookup needs request/result ownership changes and a profiler case.
Rejected Alternatives: Speculative ABI change was rejected because it risks path ownership corruption for no measured win.
Scalability potential: Low/Middle/High/Ultra keep current deterministic read route. Future keyed lookup should only be added if path-result capacity or profiler data proves scan cost over budget.
Hardware Impact: 0 us changed. Current worst-case is bounded primitive scan; no patch means no new risk on weak devices.

## Drone Fleet Repair Signal Contract

Problem: `HullRepairedSignal.QualityTier` had two producer meanings. `RepairTool` encoded the continuous `GlobalQualityWeight` as a 0..255 byte, while `DroneFleetManager` encoded `HectonQualityTier` enum values into the same lane. The helper name `ResolveAuthoritativeQualityWeight()` also falsely suggested gameplay authority while its call sites were cadence, capacity, probe, heuristic, and presentation metadata Math LOD routes.
Solution: Kept the shared signal layout unchanged and made the drone producer encode `QualityTier` as `round(GlobalQualityWeight * 255)`, matching `RepairTool`. Renamed the local helper to `ResolveDroneSimulationQualityWeight()` so future edits do not confuse quality-scaled work budgets with repair truth ownership.
Rejected Alternatives: Changing `HullRepairedSignal` ABI was rejected because it is a cross-domain unmanaged signal already consumed by atmosphere and hull systems. Changing consumers was rejected because observed consumers use flags/AUP/room/source data and do not need a new contract. Hard-pinning drone cadence and path budgets to 1.0 was rejected because continuous `GlobalQualityWeight` may scale fidelity, cadence, capacity, and optional telemetry. Keeping enum values in the byte lane was rejected because the lane already has an existing continuous producer.
Scalability potential: Low/Middle/High/Ultra keep identical hull repair completion authority, signal size, flags, source hash, room route, and dent data. Low writes a low continuous metadata byte; Middle and High interpolate; Ultra writes 255, allowing presentation-side overkill without changing repair truth.
Hardware Impact: 0 verified us saved. The byte encode is primitive math and not claimed as an optimization. The value is correctness debt removal: it prevents future consumers from treating mixed enum/continuous metadata as authoritative quality.

## Fauna Sensor And Director Registry Cache

Problem: `FaunaSensorSuite.TrySampleClosedNavGridCell()` polled `GlobalRegistry.ResourceDistribution` inside obstacle avoidance and player line-of-sight sensory probes. That path is reached from `FaunaBrain.Tick()` and can run multiple samples per active fauna brain frame. `FaunaDirector.SlowTick()` also repaired missing depth-zone and vegetation-threat dependencies by polling `GlobalRegistry.DepthZoneReadModel` and `GlobalRegistry.VegetationThreat` from helper methods.
Solution: Added a cold-bound `IBrineFluidDensityReadModel` field and `BindBrineDensityReadModel()` to `FaunaSensorSuite`. `FaunaBrain.RefreshColdRegistryDependencies()` binds `GlobalRegistry.BrineFluidDensity` once, and `OnGlobalRegistryServiceReplaced(ResourceDistributionRuntime)` updates the cached interface on service swaps. `FaunaDirector` now resolves depth/vegetation dependencies through cold refresh, hot-swap callbacks, and serialized local fallback only; slow-tick helper methods no longer poll `GlobalRegistry`, and the dead vegetation resolver/calls were removed.
Rejected Alternatives: Direct `ResourceDistributionDirector.ActiveRuntimeInstance` fallback was rejected because it is another global hot lookup. Per-probe registry lookup was rejected by read-accessor purity. A new SignalBus request/response lane was rejected because sensory sampling is a direct environmental read model, not an event. Removing brine-density checks was rejected because it would change perception semantics instead of fixing dependency routing. Completing or forcing service discovery from slow tick was rejected because slow cadence is still runtime work.
Scalability potential: Low devices avoid repeated global lookup overhead in sensory work while retaining the same obstacle/visibility truth. Middle devices keep full perception with cleaner dependency flow. High and Ultra devices can spend their AI/render budget on denser fauna presentation or richer acoustic/visual reactions; quality never changes the DTO layout or the owner route. Low, Middle, High, and Ultra all use the same cached dependency path, with fidelity/cadence scaling left to existing continuous `GlobalQualityWeight` consumers.
Hardware Impact: 0 verified us saved; no profiler proof. Static impact: the inspected fauna sensory path removes up to four registry reads per active brain tick (line-of-sight plus three obstacle probes), and the director slow path removes two recurring registry reads when those services are missing. On i3/MX350-class hardware this reduces cold lookup jitter risk without adding allocations or new global state.

Problem: Post-fauna compile verification is blocked outside the 13AI domain.
Solution: Ran `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` only after gate allowed it (CPU 37%, active `dotnet/csc` count 0). The build failed on `Assets/Candice AI for Games/Scripts/Libs/Candice Save System/Overrides/CandiceSQLiteProvider.cs`: missing `Mono.Data` and `SqliteDataReader`. Record `[BLOCKED BY DEPENDENCY]` and do not touch the vendor save dependency from the AI fauna/drone domain.
Rejected Alternatives: Repeating the build after the same dependency wall was rejected. Editing Candice SQLite provider from 13AI was rejected as domain violation without a cross-domain directive.
Scalability potential: None; compile gate proof only.
Hardware Impact: 0 runtime us. The external compile wall prevents Unity/profiler/GC proof for this patch.

## Encounter Director Terrain And Vegetation Cache

Problem: `HectonDirectorAI.Tick()` read `HectonMapMagicVegetationBridge.ActiveRuntimeInstance` every director frame for acoustic vegetation pressure. Predator sight contact processing also called `GlobalRegistry.MapMagic` inside `IsPredatorSightTerrainBlocked()` before sampling terrain heights. Both routes violate the doctrine that `GlobalRegistry` is cold dependency injection only and hot AI consumers read cached owner data.
Solution: Added cached `_terrainProvider` and `_vegetationBridge` fields. `RefreshColdRegistryReferences()` binds `GlobalRegistry.Terrain` and `GlobalRegistry.MapMagicVegetation` once, and `OnGlobalRegistryServiceReplaced()` updates those references on `TerrainProviderRuntime` and `MapMagicVegetationRuntime` swaps. `Tick()` reads the cached vegetation bridge, and predator line-of-sight terrain tests read the cached `ITerrainProvider`.
Rejected Alternatives: Keeping `ActiveRuntimeInstance` in tick was rejected because it hides dependency resolution in an owner cadence loop. Keeping `GlobalRegistry.MapMagic` in the LOS helper was rejected because it can run per processed predator contact. A new SignalBus request/response lane was rejected because this is synchronous spatial sampling, not an event. Removing terrain LOS checks was rejected because that changes predator perception semantics instead of fixing dependency routing.
Scalability potential: Low devices avoid singleton/registry lookup jitter in encounter pressure and predator sight loops. Middle keeps full terrain/vegetation perception with the same truth route. High and Ultra can spend budget on richer director presentation or more visible pressure cues; `GlobalQualityWeight` does not change the dependency route, DTO layout, save identity, or predator authority.
Hardware Impact: 0 verified us saved; no profiler proof. Static impact: removes one vegetation singleton read per director tick and up to three terrain registry reads per processed predator sight contact. On i3/MX350-class hardware this is a jitter-risk removal, not a claimed measured frame-time win.

Problem: Encounter-director compile proof could not be attempted after this patch under session rules.
Solution: Sampled the build gate and found CPU 100% with 8 active `dotnet/csc` processes. Skipped `dotnet build` and kept verification to targeted `rg`, brace/paren balance, and `git diff --check`.
Rejected Alternatives: Launching a forbidden compile under active load was rejected. Killing or interrupting other agents' compiler processes was rejected because this session is concurrent with 20+ agents.
Scalability potential: None; compile gate proof only.
Hardware Impact: 0 runtime us. Build remains previously blocked by external Candice SQLite when the gate allows compile; this specific follow-up is compile-gated by load.

## Encounter Director Player And Predator Sight Cache

Problem: The first encounter-director dependency patch still left `Tick()` calling `RefreshRuntimeReferences(false)`, and the non-force branch could enter `WorldRuntimeReferenceUtility` / `TryGetComponent` fallback discovery. Predator sight processing could also route through `EnsurePredatorSpatialHashBuffersAllocated()` from the tick path, which may acquire DataVault generation handles. Predator sight cone truth read `contact.Transform.forward`, binding cognition to scene transform state instead of the fauna spatial-contact logic pose. The predator spatial hash builder scheduled a tiny 64-item job and required a later completion/unlock route.
Solution: `RefreshRuntimeReferences(false)` now only applies cached `IPlayerRuntimeContext` references and refreshes the meta-campaign service before returning; cold scene/component fallback discovery remains in the explicit `force` path. Player position/snapshot resolution now reads the cached `_playerRuntimeContext` interface directly. Predator sight tick processing now opens already-owned spatial-hash DataVault views with `TryResolvePredatorSpatialHashBuffers()`; acquisition stays in cold `OnEnable()` and DataVault hot-swap handling. Spatial hash cell coordinates are filled inline under the existing write lock with a `finally` unlock, removing the tiny schedule/readback route. Predator forward now comes from `IFaunaSpatialContact.ResolveContactForward()` and is quantized through `ResolveDominantAxisDirection()`.
Rejected Alternatives: Keeping one-second self-repair scene lookup inside `Tick()` was rejected because slow cadence is still hot owner work. Changing the public player-runtime ABI was rejected because existing `IPlayerRuntimeContext` already exposes pure movement/look reads. Keeping `EnsureGenerationHandle` reachable from predator sight tick was rejected because DataVault acquisition belongs to cold owner setup. Keeping the tiny job was rejected without profiler proof because the batch is 64 primitive rows and same-frame scheduling would spend scheduler cost to avoid trivial math. Transform forward was rejected as an unstable gameplay truth source for AI cognition.
Scalability potential: Low/Middle/High/Ultra share one cached dependency route. Weak devices avoid hidden scene lookup and DataVault acquisition jitter in encounter cadence work. High/Ultra spend budget on denser predator presentation and pressure feedback, not extra truth routes. `GlobalQualityWeight` does not affect player identity, predator contact DTOs, DataVault handle layout, or perception authority.
Hardware Impact: 0 verified us saved; profiler proof absent. Static impact: removes non-force scene/component fallback from director tick, removes DataVault generation-handle acquisition risk from predator sight tick, removes a 64-row job schedule/completion route, and removes transform-state dependence from predator sight forward. On i3/MX350-class hardware this is mainly jitter-risk removal, not a claimed measured frame-time win.

## Fauna Brain Vegetation And Voxel Cache

Problem: `FaunaBrain` and its compatibility partial still read `HectonMapMagicVegetationBridge.ActiveRuntimeInstance` from predator fear pressure, voxel path guidance, director hunt routing, corpse floor height, and chemical-pack compatibility logic. Burrow ambush guidance also read `HectonVoxelEngine.ActiveRuntimeInstance`. These are cognition hot paths and should not poll active singletons while evaluating fauna behavior.
Solution: Added `_vegetationBridge` and `_voxelEngine` caches to `FaunaBrain`. Cold refresh binds them from `GlobalRegistry.MapMagicVegetation` and `GlobalRegistry.VoxelEngine`, and registry hot-swap callbacks update them for `MapMagicVegetationRuntime` and `VoxelEngineRuntime`. The inspected hot cognition, compatibility, immediate route, corpse floor, and burrow paths now consume the cached fields.
Rejected Alternatives: Expanding new interfaces for every vegetation bridge method was rejected in this pass because the existing bridge is already the registered runtime owner and this fix needed a narrow route correction. Deleting vegetation/voxel behavior was rejected because it would change perception and route semantics. Keeping `ActiveRuntimeInstance` was rejected because it hides global discovery in fauna cognition.
Scalability potential: Low devices avoid singleton lookup jitter inside active fauna brain decisions. Middle/High/Ultra keep the same sensory and route truth, with richer creature behavior presentation funded by existing continuous quality/cadence controls rather than global polling. Quality does not change DTO layout, save identity, or the owner route.
Hardware Impact: 0 verified us saved; profiler proof absent. Static impact: removes six active-singleton reads from inspected fauna cognition/compatibility routes. Residual debt remains in `FaunaBrain.TryResolveCachedPlayerRuntimeContext()` static player lookup and in `PredatorCognitionDomain.RefreshThreatVoxelSnapshot()` active vegetation/cave lighting lookups.

## Follow-Up Verification Gate

Problem: Compile proof after the second hot-dependency pass is gated by machine state, not by a known 13AI compile result.
Solution: Re-ran targeted `rg`, brace/paren balance checks, and the build gate. `rg` shows the edited files no longer contain `HectonMapMagicVegetationBridge.ActiveRuntimeInstance`, `HectonVoxelEngine.ActiveRuntimeInstance`, `GlobalRegistry.MapMagic`, `contact.Transform.forward`, or the tiny `PredatorSpatialHashContactCapacity` schedule. Remaining hits are cold `GlobalRegistry.MapMagicVegetation` binds and the known residual `FaunaBrain` static player-context route. Brace/paren counts match. Build was not launched because the latest gate sampled CPU 95% with 3 active `dotnet/csc` processes.
Rejected Alternatives: Launching `dotnet build` under CPU >50% or while `dotnet` is active was rejected by AGENTS. Claiming a green compile from static checks was rejected because no compiler ran after this pass.
Scalability potential: None; verification route only.
Hardware Impact: 0 runtime us. Current patch remains statically checked but compile-gated. Earlier allowed builds still hit the external Candice SQLite `Mono.Data` / `SqliteDataReader` dependency wall before proving the full project.

## Fauna Player Runtime Snapshot Cache

Problem: `FaunaBrain` still had a player runtime fallback through `PlayerRuntimeContextService.TryGetActiveRuntimeContext()`. That keeps a static global route reachable from active fauna cognition when the cached player interface is stale or missing.
Solution: Replaced the fallback with a frame-local `FaunaPlayerRuntimeContextSnapshot` sourced only from cached `IPlayerRuntimeContext`. The snapshot stores player transform, movement, look, flashlight, and tool-manager references once per dispatcher frame.
Rejected Alternatives: Static player-service fallback was rejected because it hides dependency discovery behind cognition reads. Scene search was rejected outright. Changing `IPlayerRuntimeContext` ABI was rejected because the current interface already exposes pure movement/look snapshots.
Scalability potential: Low/Middle/High/Ultra share one cached player route. Weak devices avoid static lookup jitter; high and ultra tiers spend budget on richer creature reaction presentation, not alternate player truth.
Hardware Impact: 0 verified us. Static impact: removes the remaining inspected static player-context fallback from `FaunaBrain`; no allocations or DTO changes were added.

## Predator Threat Voxel Source Cache

Problem: `PredatorCognitionDomain.RefreshThreatVoxelSnapshot()` read `HectonMapMagicVegetationBridge.ActiveRuntimeInstance` and `HectonCaveVoxelLightingVolume.ActiveRuntimeInstance` from the predator cognition path.
Solution: Added cached vegetation and cave voxel-lighting sources in `PredatorCognitionDomain`. `HectonMapMagicVegetationBridge` and `HectonCaveVoxelLightingVolume` now bind/clear those sources from lifecycle/reset paths.
Rejected Alternatives: Active singleton polling was rejected by hot-read purity doctrine. A new SignalBus request route was rejected because threat voxel refresh is synchronous read-model sampling, not an event. Replacing the SDF with heavier physical sight simulation was rejected by the cinematic cheat protocol.
Scalability potential: Low/Middle/High/Ultra keep the same threat and SDF semantics. Low avoids lookup jitter; Ultra can spend saved slack on predator visual overkill without changing perception authority.
Hardware Impact: 0 verified us. Static impact: removes two active singleton reads from threat voxel refresh. No measured microsecond claim.

## Drone Fleet Environment Source Cache

Problem: `DroneFleetManager` still read `FloraInteractionManager.ActiveRuntimeInstance` and `HectonMapMagicVegetationBridge.ActiveRuntimeInstance` in inspected assignment, parasite, headless task-map, and abyssal flow payload routes.
Solution: Added cached flora and vegetation bridge fields. Flora and vegetation owners publish lifecycle/reset bindings, and drone registry cache updates the vegetation bridge from the existing `MapMagicVegetationRuntime` slot.
Rejected Alternatives: Keeping active singleton fallback in slow/headless drone routes was rejected because slow cadence is still runtime work. Creating a broad new environment facade was rejected for this pass because the existing owners already expose the needed read-model methods.
Scalability potential: Low/Middle/High/Ultra share one drone environment route. Quality still scales drone cadence/capacity/fidelity elsewhere, not environment ownership or task truth.
Hardware Impact: 0 verified us. Static impact: removes four active singleton reads from inspected drone paths; no profiler proof.

## Ambient Macro Hydration Continuous Quality

Problem: Ecosystem macro hydration encoded quality as 0..3 and `AmbientBiotaDirector` decoded it as `0, 1/3, 2/3, 1`. That discrete value fed macro spawn offsets, velocity, scale, survival pressure, and spawn flags, so continuous `GlobalQualityWeight` was being quantized before gameplay-adjacent spawn work.
Solution: `EcosystemDirector` now encodes macro quality as a continuous 0..255 byte. `AmbientBiotaDirector` decodes that byte as `qualityByte / 255`, applies stress attenuation in float, feeds the job with continuous `QualityWeight01`, and re-encodes spawn signal metadata as 0..255.
Rejected Alternatives: Keeping the 0..3 tier byte was rejected because the project forbids binary/tiered quality switches for algorithms. Supporting ambiguous legacy bytes 0..3 in the same lane was rejected after inspection because low continuous values 1..3 would decode as high quality. Changing the `IAmbientBiotaService` signature was rejected because the existing byte ABI can carry continuous 0..255 data.
Scalability potential: Low/Middle/High/Ultra now interpolate on one continuous route. Low can collapse visual complexity smoothly; Middle/High can ramp without cliffs; Ultra reaches visual-overkill metadata without changing spawn authority.
Hardware Impact: 0 verified us. Static impact: removes 4-step quality stair-stepping from macro hydration. The byte encode/decode cost is primitive math; no runtime allocation or new job was added.

## Residual Pass Verification Gate

Problem: Full compiler proof still cannot be completed after the residual pass because the project fails on an external save-system dependency before a clean build can be established.
Solution: Ran targeted `rg`, brace/paren counts, and `git diff --check`. `rg` shows no inspected active singleton/static player fallback residue in `DroneFleetManager`, `FaunaBrain`, or `PredatorCognitionDomain`; ambient macro quality no longer uses `ResolveMacroVisualQualityByte`; `git diff --check` returned CRLF warnings only. Build gate later passed at CPU 45% with 0 active `dotnet/csc`, so one `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` was run. It failed on external Candice SQLite `Mono.Data` / `SqliteDataReader`; no 13AI-owned error was visible in the observed error set.
Rejected Alternatives: Editing the Candice vendor save dependency was rejected as outside the AI fish/creature/drone domain. Repeating builds after the same wall was rejected.
Scalability potential: None; verification route only.
Hardware Impact: 0 runtime us. Compile/profiler proof remains pending behind the external Candice SQLite dependency wall.

## Predator Cadence, Steering, And Dump Route Cleanup

Problem: `PredatorCognitionDomain` mixed gameplay truth quality with cadence quality. Mesofauna behavior used the authoritative `1.0` scalar, but the same scalar also drove slice modulo, so `GlobalQualityWeight` could not reduce mesofauna work. Predator retinal low-cadence mode was disabled by a constant false helper. The predator job forced high-tier smooth steering for every input even though the input flags already carried `HighTierSmoothSteering`. Retinal, alpha, and mesofauna blackbox fault writers used subsystem-specific filenames instead of the mandated 13AI dump artifact.
Solution: Kept mesofauna behavior/tuning quality authoritative at `1.0` and added `ResolveMesofaunaCadenceQualityWeight()` from continuous `HomeostasisBrain.GlobalQualityWeight` for slice modulo only. Replaced disabled retinal low mode with a continuous interval lerp from 1.0s to 0.5s while leaving alpha cadence at 0.1s. Changed the job to honor `CognitionInputFlags.HighTierSmoothSteering`. Routed retinal, alpha, and mesofauna fault dumps to `Docs/AgentLogs/Dump_13AI.bin`.
Rejected Alternatives: Scaling mesofauna behavior truth with device quality was rejected because quality must not change gameplay authority. Keeping binary low/high cadence was rejected because the project requires continuous `GlobalQualityWeight`. Keeping smooth steering forced true was rejected because it spends expensive math on non-apex/non-high-tier paths without a proof budget. Keeping separate dump filenames was rejected because critical AI systems need one mandated agent fault artifact.
Scalability potential: Low uses sparse mesofauna work cadence, low predator retinal cadence, and cheaper steering unless flags request high-tier smoothing. Middle interpolates cadence. High and Ultra can run tighter predator evaluation and smooth steering where authorized, using saved cycles for visual-overkill reactions without changing predator or mesofauna truth.
Hardware Impact: 0 verified us; profiler proof absent. Static estimate: minimum-quality mesofauna can avoid full FSM work for up to nine of ten modulo slots, non-alpha predator retinal utility can run at half the previous always-0.5s cadence, and non-high-tier inputs no longer pay the smooth-steering branch.

## Fauna Foveated Damage-Lock Cache

Problem: `FaunaBrain.Foveated.NotifyFoveatedCombatDamageLock()` read `GlobalRegistry.FoveatedSimulationDirector` directly from a combat damage-lock route. That violates the cold-registry-only doctrine and leaves a global dependency lookup reachable from active combat feedback.
Solution: Added cached `_foveatedSimulationDirector` binding in `FaunaBrain.RefreshColdRegistryDependencies()` and updated it from `OnGlobalRegistryServiceReplaced(FoveatedSimulationDirector)`. The damage-lock notification now reads that cached interface.
Rejected Alternatives: Keeping a direct registry read was rejected because combat damage lock is runtime work. Adding a SignalBus request for this direct notification was rejected because the foveated director is already the owner interface and the route is a direct command, not a broadcast fact.
Scalability potential: Low/Middle/High/Ultra share one cached dependency route. Quality can scale foveated update cadence or presentation elsewhere; it does not alter the foveated director ownership route.
Hardware Impact: 0 verified us. Static impact: removes one registry read from each damage-lock notification path, with no allocation, no new DTO, and no authority change.

## Predator Residual Compile Gate

Problem: Full compile proof after the predator/foveated residual pass still hits an external dependency wall.
Solution: Build was launched only after the gate allowed it (CPU 21%, 0 active `dotnet/csc`). `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` failed on `Assets/Candice AI for Games/Scripts/Libs/Candice Save System/Overrides/CandiceSQLiteProvider.cs` missing `Mono.Data` and `SqliteDataReader`. Marked `[BLOCKED BY DEPENDENCY]`.
Rejected Alternatives: Editing the Candice SQLite provider from 13AI was rejected as a domain violation. Repeating the same build wall was rejected because it gives no new signal and wastes shared CPU.
Scalability potential: None; verification route only.
Hardware Impact: 0 runtime us. The external compile wall prevents runtime/profiler/GC proof for this pass.

## Fauna Spawn Presentation And Director Runtime Cache

Problem: `FaunaPresentationService.ConfigureSpawnedCreature()` pulled `GlobalRegistry.FaunaGenetics` and `GlobalRegistry.EcosystemHealth` on every spawned creature. Spawn presentation is runtime work, and hiding registry reads inside the helper violates the cold DI route doctrine.
Solution: Added explicit `FaunaPresentationService.Bind(FaunaGeneticsManager, EcosystemHealthDirector)`. `FaunaDirector` binds these services from cold registry refresh and updates the binding through `FaunaGeneticsRuntime` and `EcosystemHealthRuntime` hot-swap callbacks. The presentation helper now only consumes cached owner references.
Rejected Alternatives: Leaving registry lookup per spawn was rejected because spawn bursts can magnify two global reads into repeated hidden dependency work. Moving genetics/ecosystem calls into simulation was rejected because presentation-side wiring must stay out of core simulation. Adding a new SignalBus route was rejected because these are direct owner services, not broadcast facts.
Scalability potential: Low devices avoid hidden lookup jitter during spawn bursts. Middle keeps the same trait/ecosystem setup. High and Ultra can spend spawn budget on richer creature presentation without creating alternate genetics truth. `GlobalQualityWeight` does not affect trait ownership, chunk identity, or ecosystem health routing.
Hardware Impact: 0 verified us. Static impact: removes two global registry reads per spawned creature from the inspected path; exact microseconds require Unity profiler proof.

Problem: `FaunaDirector` still used a `PlayerRuntimeContextService.TryGetActiveRuntimeContext()` fallback and `SystemDispatcher.ActiveRuntimeInstance` for spawn/player pose and time reads. These are runtime AI routes, not bootstrap-only dependency acquisition.
Solution: Added cached `_playerRuntimeContext` and `_dispatcherRuntime` fields sourced from cold registry refresh and hot-swap callbacks. Replaced the cached `PlayerRuntimeContext` object with a primitive `FaunaDirectorPlayerRuntimeContextSnapshot` refreshed once per dispatcher frame from `IPlayerRuntimeContext`. `ReadDispatcherTimeSeconds()` now reads the cached dispatcher.
Rejected Alternatives: Static player runtime fallback was rejected because it hides service discovery in player pose reads. Direct dispatcher singleton reads were rejected because dispatcher identity is a dependency, not a hot global. Removing the `WorldRuntimeReferenceUtility` player fallback entirely was rejected because bootstrap/editor scenes may still require degraded transform resolution until a stronger owner route exists.
Scalability potential: Low avoids singleton lookup jitter in spawn visibility and logic-pose checks. Middle/High/Ultra keep the same player truth and can spend budget on denser fauna reactions. Quality scales cadence/fidelity elsewhere, not player identity, dispatcher identity, DTO layout, or save authority.
Hardware Impact: 0 verified us. Static impact: removes the active dispatcher singleton from inspected director time reads and removes the static player-context fallback from director player pose/view reads.

Problem: `FaunaBrain.ReadDispatcherTimeSeconds()` used `SystemDispatcher.ActiveRuntimeInstance`. This kept a global dispatcher singleton read inside active fauna logic.
Solution: Added `_dispatcherRuntime`, bound it through cold registry refresh and `Dispatcher` hot-swap callbacks, and changed `ReadDispatcherTimeSeconds()` to read the cached dispatcher.
Rejected Alternatives: Keeping the singleton was rejected for hot-read purity. Routing through Unity `Time` was rejected because dispatcher time is already the project-owned clock.
Scalability potential: Low/Middle/High/Ultra share the same clock owner. Cheap devices avoid global lookup jitter; top-tier devices use saved slack on presentation, not alternate time truth.
Hardware Impact: 0 verified us. Static impact: removes active dispatcher singleton reads from the inspected fauna brain time route.

## Fauna And Ambient Blackbox Artifact Alignment

Problem: `StressDrivenSpawnDirector` and `AmbientBiotaDirector` maintained valid blackbox/telemetry dump paths, but their file names were subsystem or legacy agent-specific (`Dump_SHINOBU_253.bin`, `Dump_AMBIENT_BIOTA_DIRECTOR.bin`) instead of the mandated `Docs/AgentLogs/Dump_13AI.bin`.
Solution: Normalized both fault dump routes to `Docs/AgentLogs/Dump_13AI.bin`. Existing source hashes and headers remain the way to identify writer layout.
Rejected Alternatives: Keeping subsystem filenames was rejected because the session protocol requires the agent-level artifact. Writing duplicate aliases was rejected because it adds fault-path I/O without a declared consumer. Changing telemetry DTOs was rejected because the problem is route naming, not payload layout.
Scalability potential: Low/Middle/High/Ultra share one forensic artifact. The dump remains fault-only and does not alter AI truth, spawn identity, signal layout, or quality behavior.
Hardware Impact: 0 normal-frame us. Fault-path file I/O is unchanged except for target path; no runtime allocation or recurring work was added.

## Fauna/Ambient Residual Verification Gate

Problem: Full compiler proof after the fauna/ambient route cleanup could not be run under AGENTS build rules.
Solution: Ran targeted `rg`, brace/paren balance, and `git diff --check`. The inspected files no longer contain `PlayerRuntimeContextService.TryGetActiveRuntimeContext`, `SystemDispatcher.ActiveRuntimeInstance`, `Dump_SHINOBU_253`, or `Dump_AMBIENT_BIOTA_DIRECTOR`; cold `GlobalRegistry` reads remain in lifecycle refresh only. Build gate sampled CPU 36% but found one active `dotnet` process, so no build was launched.
Rejected Alternatives: Starting a second `dotnet build` while another `dotnet` process was active was rejected. Claiming compile success from static checks was rejected.
Scalability potential: None; verification only.
Hardware Impact: 0 runtime us. Runtime/profiler/GC proof remains pending.

## AI Blackbox Dump Route Consolidation

Problem: Several 13AI-domain critical systems still routed fault dumps to legacy SHINOBU or subsystem-specific files: predator steering, predator acoustic SDF, path funnel, voxel A*, fauna kinematics, fauna bite IK, Leviathan tentacle IK, and procedural crab leg IK. The steering writer also used a raw relative `FileStream` path, making the artifact location depend on process working directory.
Solution: Normalized those fault-path constants and inline writer paths to `Docs/AgentLogs/Dump_13AI.bin`. Updated the predator steering writer to resolve the dump directory from `Application.dataPath` to project root before file creation. Updated the matching SHINOBU 303/311 architecture route cards so docs and code name the same forensic artifact.
Rejected Alternatives: Keeping subsystem-specific files was rejected because the active protocol requires `Dump_[YourID].bin` for critical AI fault forensics. Writing duplicate aliases was rejected because it adds extra fault-path I/O without a consumer contract. Mass-renaming 1300/1301/1306 dumps was rejected because those are separate owner lanes and not proven safe to consolidate from the 13AI prompt alone.
Scalability potential: Low/Middle/High/Ultra use the same forensic route. Runtime quality scaling does not change telemetry layout, fault ownership, DTO layout, save identity, or AI authority. Cheap devices avoid path ambiguity during crash forensics; high-end devices keep the same diagnostic route while spending budget on visual overkill elsewhere.
Hardware Impact: 0 normal-frame us. The change only affects fault-path file targets and one project-root path resolution before dump I/O. No new hot allocation, registry lookup, scene search, or job completion was added.

## Dump Route Verification Gate

Problem: Source/doc consistency needed proof after artifact consolidation, but full compiler proof could not be started under the shared-machine build rules.
Solution: Ran targeted residue scans, brace counts, and `git diff --check`. No old targeted dump names remained in the inspected AI/fauna/pathfinding sources or route docs. Braces matched in all inspected source files. `git diff --check` returned only CRLF normalization warnings. Build gate samples stayed above the allowed CPU threshold: 51%, 65%, then 63%, with no active `dotnet/csc`; build was not launched.
Rejected Alternatives: Launching `dotnet build` above 50% CPU was rejected by AGENTS. Claiming compile success from static scans was rejected. Repeating waits indefinitely was rejected because it blocks shared-agent work without new technical signal.
Scalability potential: None; verification route only.
Hardware Impact: 0 runtime us. Runtime/profiler/fault-injection proof remains pending.

## Alpha Telemetry High-Tier Truth

Problem: `PredatorCognitionDomain.UpdateAlphaLeviathanPostEvaluationTelemetry()` hardcoded `highTierSmoothSteering = true` while the actual predator evaluation route already reads `CognitionInputFlags.HighTierSmoothSteering`. That made telemetry report SDF/high-tier behavior on frames where the solver used survival fallback or lower-fidelity steering.
Solution: Changed alpha telemetry to derive `highTierSmoothSteering` from `input.Flags & CognitionInputFlags.HighTierSmoothSteering`, the same route used by predator steering.
Rejected Alternatives: Keeping telemetry forced high was rejected because blackbox data becomes false evidence. Recomputing quality from `HomeostasisBrain.GlobalQualityWeight` inside telemetry was rejected because the input flag is the owner-approved fact for this evaluation.
Scalability potential: Low/Middle/High/Ultra keep one steering authority route. Low records fallback accurately. High and Ultra record SDF dive/high-tier flags only when the input route authorizes them.
Hardware Impact: 0 verified us. No new hot allocation, lookup, scene search, or job completion. This is forensic correctness, not a performance claim.

## Fish/Flora Symbiosis Micro-Exchange Math LOD

Problem: `ShinobuFloraFaunaSymbiosisSolver` had continuous helpers `ResolveMicroExchangeWeight()` and `ResolveDitheredFrameGate()`, but `runMicroExchangeFrame` was hardcoded to `true`. That forced the flora spatial hash job and micro neighbor sampling every cold tick, even when `GlobalQualityWeight` was below the macro approximation threshold.
Solution: Replaced the constant with `ResolveMicroExchangeWeight(quality, activeTuning.MacroThreshold)` and deterministic frame dither using the sector/frame seed. Low quality now uses `ApplyMacroAverage`; high quality reaches full `ApplyMicroExchange`.
Rejected Alternatives: A binary low/high branch was rejected because quality must be continuous. Removing micro exchange was rejected because High/Ultra need dense fish/flora interaction. Random nondeterministic gating was rejected because symbiosis must remain predictable and replayable.
Scalability potential: Low uses macro biomass approximation and skips hash/micro sampling. Middle stochastically blends micro frames by continuous quality weight. High/Ultra spend budget on richer local symbiosis reactions while preserving the same DTOs and owner route.
Hardware Impact: 0 verified us. Static estimate: below `MacroThreshold`, one flora spatial hash job is skipped and `MockFish + AmbientFish` neighbor sampling is replaced by macro stride loops. Exact microseconds require profiler proof.

## Symbiosis Blackbox Artifact Cleanup

Problem: `ShinobuFloraFaunaSymbiosisSolver` wrote `Dump_SHINOBU_62.bin` and a duplicate `Dump_SYMBIOSIS.bin` on fault. That violates the active agent-level blackbox artifact rule and adds duplicate fault-path I/O.
Solution: Normalized the writer to `Docs/AgentLogs/Dump_13AI.bin` and removed the duplicate symbiosis alias write.
Rejected Alternatives: Keeping duplicate files was rejected because the file header/source hash already identifies the writer and the protocol requires one `Dump_13AI.bin` artifact. Mass-changing other ecosystem dump owners was rejected without their route-card review.
Scalability potential: Low/Middle/High/Ultra share one forensic artifact. Quality does not alter dump ABI, fish/flora truth, or save identity.
Hardware Impact: 0 normal-frame us. Fault-path I/O is reduced by one file write when a dump occurs; no runtime allocation or recurring work was added.

## Telemetry/Symbiosis Verification Gate

Problem: Compile proof could not be run after the telemetry/symbiosis patch under shared-machine rules.
Solution: Ran targeted residue scans, brace counts, and `git diff --check`. No forced high-tier telemetry, constant micro-exchange frame gate, or legacy symbiosis dump names remain in touched files. Braces matched. `git diff --check` returned CRLF warnings only. Build gate sampled CPU 100%, waited, and sampled CPU 100% again; no `dotnet/csc` process was active, but CPU exceeded the allowed threshold.
Rejected Alternatives: Launching `dotnet build` at 100% CPU was rejected. Claiming compiler success from static checks was rejected.
Scalability potential: None; verification route only.
Hardware Impact: 0 runtime us. Runtime/profiler/fault-injection proof remains pending.
