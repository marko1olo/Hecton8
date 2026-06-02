# Status_13AI

ID: 13AI
Domain: AI logic for fish, creatures, neutral drones, hostile drones.
Prompt source: direct user assignment. CLI extraction found no `<AGENT_PROMPT id="13AI">` in `Docs/Tasks/CURRENT_BATCH.md`; XML task count is 0.
Status hygiene: no pre-existing `Status_13AI.md` content found at session start.
Runtime verification status: PENDING VERIFICATION. Static diff whitespace checks passed with CRLF warnings only. Compile attempts ran only after gate samples allowed them. Latest residual-debt build-gate sample passed at CPU 45% with 0 active `dotnet/csc` processes. `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` still failed on external Candice SQLite dependency (`Mono.Data` / `SqliteDataReader`); no 13AI-owned error was visible in the observed error set. No Unity import, Play Mode, profiler, GCMonitor, fault injection, or player build proof yet.

## Mandates Selected

- `AI_Creature_Cognition_States.txt`
- `AI_Director_Encounter_Manager.txt`
- `AI_DYNAMIC_NAVGRID_SDF_INTEGRATION.txt`
- `AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt`
- `AI_Navigation_AStar_Funnel_Smoothing_Pathfinding.txt`
- `ARCH_Execution_Phases.txt`
- `ARCH_Signal_Lane_Segregation.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

Supplemental reference read: `ARCH_Global_Registry_ServiceLocator_DI_Init.txt` for dependency route validation only; not counted as selected mandate.

## Checklist

- [x] Task 01: Prompt extraction and hygiene check | DOD: CLI regex over `Docs/Tasks/CURRENT_BATCH.md`, no 13AI XML block found; status/rationale absent at start | Alternative rejected: reading neighboring batch prompts or stale archived prompts | Estimate: 0 runtime us.
- [x] Task 02: Domain boundary read | DOD: read `Docs/Actual Domains of Project.txt`; 13AI scope maps to ECHELON 3 AI domains 21-27 plus ECHELON 6 drone commander 59 | Alternative rejected: broad edits outside AI without interface proof | Estimate: 0 runtime us.
- [x] Task 03: Taste authority read | DOD: read `TASTE.md`; creature AI must pressure behavior through sound, visibility, route cost, oxygen, salvage risk | Alternative rejected: monster gallery/random patrol behavior | Estimate: 0 runtime us.
- [x] Task 04: Mandate selection | DOD: selected 8 AI/nav/phase/signal/zero-GC mandates before code; read Registry/DI as supplemental dependency reference | Alternative rejected: general Unity AI assumptions or exceeding selected mandate cap | Estimate: 0 runtime us.
- [x] Task 05: Source inventory and dependency map | DOD: mapped AI sensory, fauna spawn, ambient biota, pathfinding, construction drone fleet, world residency consumers, and shader/compute dependencies | Alternative rejected: editing from subagent claims without local file reads | Estimate: 0 verified runtime us; inventory only.
- [x] Task 06: Static violation scan | DOD: targeted rg scans found hot `Try*` ownership leaks in acoustic echo, fixed quality bypass in stress spawn, phantom drone draw-count discard, ambient read alias race, and dormant real-drone GPU culling path | Alternative rejected: broad project-wide grep dump without ownership triage | Estimate: 0 verified runtime us; static audit only.
- [x] Task 07: Code-path inspection loop 1 | DOD: traced acoustic producer/consumer path, `FaunaDirector` tick phase, DataVault handle use, black-box writes, and `SignalBus` intake | Alternative rejected: moving acoustic data through HectonEventBus or new global polling route | Estimate: removes unverified 5-30 us cold owner work from predator echo reads when hit; measured proof absent.
- [x] Task 08: Code-path inspection loop 2 | DOD: traced drone procedural args, phantom compute capacity, culling compute contracts, ambient biota world consumers, and stress spawn jobs | Alternative rejected: public contract signature changes or compacting drone state on CPU without proof | Estimate: 0 verified runtime us; contract validation.
- [x] Task 09: Implement scoped fixes | DOD: patched owner-phase acoustic update, no-acquire acoustic enqueue, continuous stress quality budgets, phantom draw/dispatch count, ambient read gate during jobs, and real-drone GPU append culling activation | Alternative rejected: architecture rewrite, new packages, or changing `IAmbientBiotaService` signatures | Estimate: 0 verified us; static work reduction includes 16 fewer hidden probes at min quality and up to 500 fewer phantom instances at quality 0.
- [x] Task 10: Verification and report | DOD: `git diff --check` passed with CRLF warnings; dotnet build correctly skipped due CPU 100% plus 8 dotnet/csc processes; final report appended to `Docs/AgentLogs/LOG_13AI.md` | Alternative rejected: forbidden build under load and chat-only reporting | Estimate: 0 verified runtime us; profiler proof pending.
- [x] Task 11: Hecton boid Black Box patch | DOD: added fixed 300-entry DataVault ring `BoidBlackBoxEntry[300]`, owner-phase LateFrame writes, fault flags, state hash, and `Docs/AgentLogs/Dump_13AI.bin` dump on fault | Alternative rejected: managed ring array, persistent owner-local NativeArray outside Vault, or reader-side telemetry writes | Estimate: 0 verified us; normal path adds one bounded Vault write of 128B per owner frame.
- [x] Task 12: Route collision cleanup | DOD: scan found first chosen buffer id `71990` collided with `ShinobuParasiteProfileCount`; boid route moved to free `71979`, and acoustic/boid dumps aligned to required `Dump_13AI.bin` proof path | Alternative rejected: leaving system-specific dump names or sharing VFX-owned `71990` | Estimate: avoids undefined DataVault aliasing; no runtime us claim.
- [x] Task 13: Compile verification `[BLOCKED BY DEPENDENCY]` | DOD: build gate passed, one `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` attempt failed on `Assets/Candice AI for Games/.../CandiceSQLiteProvider.cs` missing `Mono.Data`/`SqliteDataReader`; no 13AI file appeared in the reported errors | Alternative rejected: editing vendor SQLite dependency from AI domain or repeating builds without a source fix | Estimate: 0 runtime us.
- [x] Task 14: RepairDroneHub hot-path hygiene patch | DOD: replaced static managed `List<RepairDroneHub>` with fixed `RepairDroneHub[32]`, kept stable removal order for `GetActiveHubAt(0)` fallback behavior, moved airlock component search to lifecycle cold cache, cached repair supply hash ids, and moved runtime target diagnostics to integer id with `module.name` editor-only | Alternative rejected: DataVault for Unity object references, swap-remove order mutation, public `DroneFleetManager` contract changes | Estimate: 0 verified us; removes managed list growth risk, repeated default/legacy hash compute, player `module.name` string access, and reader-side scene search.
- [x] Task 15: PathFunnelNavmeshRuntime no-op decision | DOD: delegated deep dive found `TryReadVoxelPathResult` is bounded to capacity 1024, read-only, no allocation, no GlobalRegistry polling, no hidden `.Complete()`; requester-indexed lookup would require result ownership changes | Alternative rejected: speculative ABI change without profiler proof | Estimate: 0 runtime us, no patch.
- [x] Task 16: Post-patch static verification | DOD: `git diff --check` on touched 13AI files returned only CRLF normalization warnings; brace count and `#if/#endif` count matched for `RepairDroneHub`; scans found no `List`/Add/Remove/Clear residue in hub registry | Alternative rejected: launching build under CPU 100% and 8 dotnet/csc processes | Estimate: 0 runtime us.
- [x] Task 17: DroneFleet hull-repair signal contract audit | DOD: compared `RepairTool` and `DroneFleetManager` producers of `HullRepairedSignal`; found `RepairTool` writes continuous `QualityTier` byte 0..255 while drones wrote `HectonQualityTier` enum values | Alternative rejected: changing shared `HullRepairedSignal` layout or atmosphere/hull consumers | Estimate: 0 runtime us; contract audit only.
- [x] Task 18: DroneFleet quality-route naming cleanup | DOD: replaced misleading `ResolveAuthoritativeQualityWeight()` calls with `ResolveDroneSimulationQualityWeight()` for cadence/budget/heuristic/probe Math LOD routes that legitimately consume continuous `GlobalQualityWeight` | Alternative rejected: hard-pinning drone pathing to Ultra or treating cadence/fidelity scaling as gameplay identity | Estimate: 0 verified us; prevents future authority-route misuse.
- [x] Task 19: DroneFleet repair signal byte patch | DOD: `HullRepairedSignal.QualityTier` from drones now uses continuous `round(GlobalQualityWeight * 255)` byte, matching `RepairTool`; no lane ABI, flag, room, or source hash changed | Alternative rejected: enum tiers in a byte lane with existing continuous producer | Estimate: 0 verified us; fixes cross-producer metadata inconsistency.
- [x] Task 20: Compile/static verification after drone signal patch `[BLOCKED BY DEPENDENCY]` | DOD: `DroneFleetManager.cs` brace count 729/729; `git diff --check` returned only CRLF warning; targeted scan found no `ResolveAuthoritativeQualityWeight`, no `ResolveDroneRepairQualityTier`, no `HectonQualityTier` in `DroneFleetManager`; build gate CPU 8%, dotnet/csc 0, build failed on external Candice SQLite `Mono.Data`/`SqliteDataReader` | Alternative rejected: editing Candice vendor save dependency from 13AI domain or repeating build after same wall | Estimate: 0 runtime us.
- [x] Task 21: FaunaSensorSuite registry-poll audit | DOD: traced obstacle avoidance and player line-of-sight nav-grid sampling from `FaunaBrain.Tick()` into `FaunaSensorSuite.TrySampleClosedNavGridCell()` and found hot `GlobalRegistry.ResourceDistribution` lookup per sample | Alternative rejected: treating the lookup as harmless because the accessor name is `Try*`; project doctrine requires hot reads to be pure and pre-bound | Estimate: 0 runtime us; static audit only.
- [x] Task 22: FaunaSensorSuite brine read-model cache patch | DOD: added cold-bound `IBrineFluidDensityReadModel` cache, bound it from `FaunaBrain.RefreshColdRegistryDependencies()` through `GlobalRegistry.BrineFluidDensity` and `ResourceDistributionRuntime` hot-swap notifications, and removed direct `GlobalRegistry.ResourceDistribution` reads from sensory probes | Alternative rejected: direct `ResourceDistributionDirector.ActiveRuntimeInstance` fallback, per-probe registry lookup, new request/response signal lane, or deleting brine-density obstacle semantics | Estimate: 0 verified us; removes up to 4 global registry reads per active fauna sensory tick in the inspected path.
- [x] Task 23: FaunaDirector slow-tick registry cache cleanup | DOD: removed slow-tick fallback polling of `GlobalRegistry.DepthZoneReadModel` and `GlobalRegistry.VegetationThreat`, then removed the dead no-op vegetation helper/calls; runtime services now come from cold refresh/hot-swap routes or serialized fallback only | Alternative rejected: lazy registry repair from `SlowTick()` helpers because slow cadence is still a hot owner loop and should not hide dependency lookup | Estimate: 0 verified us; removes two recurring global registry lookups from the fauna director slow path when services are absent.
- [x] Task 24: Post-fauna static verification `[BLOCKED BY DEPENDENCY]` | DOD: targeted `rg` shows fauna sensor no longer reads `GlobalRegistry.ResourceDistribution` and fauna director depth/vegetation registry reads are limited to cold refresh; brace counts matched for touched files; build gate passed at CPU 37% and active `dotnet/csc` count 0; `dotnet build` failed on external Candice SQLite `Mono.Data`/`SqliteDataReader` | Alternative rejected: editing Candice vendor save dependency from 13AI domain or repeating builds without fixing that dependency | Estimate: 0 runtime us.
- [x] Task 25: Encounter director hot dependency audit | DOD: re-extracted `CURRENT_BATCH.md` by CLI (`NO_13AI_PROMPT`) and traced `HectonDirectorAI.Tick()` plus predator sight contacts; found hot `HectonMapMagicVegetationBridge.ActiveRuntimeInstance` use and `GlobalRegistry.MapMagic` terrain lookup inside `IsPredatorSightTerrainBlocked()` | Alternative rejected: ignoring singleton reads because they are throttled elsewhere; this contact loop is AI hot work | Estimate: 0 runtime us; static audit only.
- [x] Task 26: Encounter director terrain/vegetation cache patch | DOD: added cached `ITerrainProvider` and cached `HectonMapMagicVegetationBridge`, bound them from cold registry refresh and registry hot-swap notifications, and made predator LOS terrain sampling consume the cached terrain interface | Alternative rejected: direct `GlobalRegistry.MapMagic` in LOS helper, direct `ActiveRuntimeInstance` in tick, new SignalBus request/response for synchronous terrain samples, or removing terrain LOS semantics | Estimate: 0 verified us; removes one singleton read per director tick and up to three registry terrain lookups per processed predator sight contact.
- [x] Task 27: Encounter director static verification | DOD: targeted `rg` shows no `HectonMapMagicVegetationBridge.ActiveRuntimeInstance` and no `GlobalRegistry.MapMagic` in `HectonDirectorAI`; remaining `GlobalRegistry.Terrain` and `GlobalRegistry.MapMagicVegetation` reads are cold refresh only; brace count 187/187, paren count 718/718, `git diff --check` returned CRLF warning only | Alternative rejected: claiming profiler savings without Unity profiler proof | Estimate: 0 runtime us.
- [x] Task 28: Encounter director compile gate `[COMPILE GATED]` | DOD: build gate sampled CPU 100% with 8 active `dotnet/csc` processes, so `dotnet build` was not launched under AGENTS rules | Alternative rejected: forbidden compile under load or killing other agents' dotnet work | Estimate: 0 runtime us.
- [x] Task 29: Encounter/Fauna residual hot-dependency audit | DOD: subagent and local scans traced remaining `HectonDirectorAI` player reference self-repair, predator sight DataVault acquisition route, transform-forward cognition input, tiny spatial-hash job, and `FaunaBrain` active vegetation/voxel singletons | Alternative rejected: treating the previous cache patch as complete without rereading the hot call sites | Estimate: 0 runtime us; audit only.
- [x] Task 30: Encounter director player reference route patch | DOD: `RefreshRuntimeReferences(false)` now applies cached `IPlayerRuntimeContext` references and returns before scene/component fallbacks; player position/snapshot reads use `_playerRuntimeContext.TryGetMovementRuntimeState()` and `TryGetLookRuntimeState()` | Alternative rejected: one-second scene lookup from `Tick()` or public player-runtime ABI changes | Estimate: 0 verified us; removes hidden non-force scene/component fallback from director tick.
- [x] Task 31: Encounter director predator sight route patch | DOD: predator sight tick now opens existing spatial hash DataVault views only, acquisition remains cold/hot-swap; 64-row spatial cell fill is inline under lock/finally; sight cone forward uses `IFaunaSpatialContact.ResolveContactForward()` instead of `Transform.forward` | Alternative rejected: DataVault `EnsureGenerationHandle` reachable from tick, tiny job schedule/readback without profiler proof, transform-state gameplay truth | Estimate: 0 verified us; removes acquisition jitter risk and one tiny job route.
- [x] Task 32: FaunaBrain vegetation/voxel cache patch | DOD: cached `_vegetationBridge` and `_voxelEngine` via cold refresh and registry hot-swap; replaced inspected active singleton reads in predator fear, voxel guidance, burrow ambush, director hunt, corpse floor, and compatibility chemical-pack logic | Alternative rejected: deleting behavior, broad interface expansion in this pass, or keeping `ActiveRuntimeInstance` in cognition | Estimate: 0 verified us; removes six active singleton reads from inspected fauna routes.
- [x] Task 33: Post-hot-dependency static verification | DOD: targeted `rg` found no edited-file residue for `HectonMapMagicVegetationBridge.ActiveRuntimeInstance`, `HectonVoxelEngine.ActiveRuntimeInstance`, `GlobalRegistry.MapMagic`, `contact.Transform.forward`, or tiny predator spatial-hash schedule; brace/paren counts matched for touched code files | Alternative rejected: claiming all AI singleton debt gone; residual `FaunaBrain` player static lookup and non-touched domain debts remain recorded | Estimate: 0 runtime us.
- [x] Task 34: Compile gate after hot-dependency pass `[COMPILE GATED]` | DOD: latest build gate sampled CPU 95% and 3 active `dotnet/csc` processes, so `dotnet build` was not launched under AGENTS rules | Alternative rejected: forbidden build under load or claiming compile from static checks | Estimate: 0 runtime us.
- [x] Task 35: Residual AI dependency audit | DOD: re-extracted `CURRENT_BATCH.md` by CLI (`NO_13AI_PROMPT`), reused 13AI disk state, and traced remaining fauna, predator, drone, and ambient quality debts with local checks plus read-only subagent findings | Alternative rejected: assuming previous passes exhausted the domain | Estimate: 0 runtime us; audit only.
- [x] Task 36: FaunaBrain player runtime snapshot patch | DOD: replaced the remaining `PlayerRuntimeContextService.TryGetActiveRuntimeContext()` fallback in fauna cognition with a frame-local snapshot sourced only from cached `IPlayerRuntimeContext` | Alternative rejected: static player service fallback, scene search, or changing player-runtime ABI | Estimate: 0 verified us; removes hidden static player lookup from active fauna brain reads.
- [x] Task 37: Predator threat voxel source cache patch | DOD: `PredatorCognitionDomain.RefreshThreatVoxelSnapshot()` now reads owner-published vegetation and cave voxel-lighting sources; `HectonMapMagicVegetationBridge` and `HectonCaveVoxelLightingVolume` bind/clear those sources from lifecycle/reset paths | Alternative rejected: active singleton polling from predator cognition or new request/response signal for synchronous SDF samples | Estimate: 0 verified us; removes two active singleton reads from threat-voxel refresh.
- [x] Task 38: Drone fleet environment source cache patch | DOD: `DroneFleetManager` now consumes cached `FloraInteractionManager` and `HectonMapMagicVegetationBridge` references bound by owners and registry hot-swap routes; inspected active singleton reads are gone | Alternative rejected: flora/MapMagic active singleton fallback inside assignment, parasite, or headless task-map paths | Estimate: 0 verified us; removes four active singleton reads from inspected drone routes.
- [x] Task 39: Ambient macro hydration continuous quality patch | DOD: ecosystem caller encodes macro quality as 0..255 byte, ambient hydration decodes it as continuous 0..1, and spawn signal metadata is re-encoded as continuous byte after stress attenuation | Alternative rejected: legacy 0..3 tier byte, binary low/ultra branch, or using quality to change authority layout | Estimate: 0 verified us; removes 4-step quality stair-stepping from macro spawn job inputs.
- [x] Task 40: Residual pass static verification | DOD: targeted `rg` confirms no inspected active singleton/static player fallback residue in 13AI-owned hot files; ambient quality resolver no longer has `ResolveMacroVisualQualityByte`; brace counts matched on touched files; `git diff --check` returned only CRLF warnings | Alternative rejected: claiming full project cleanliness; two MapMagic player static lookups remain in world-domain residual debt | Estimate: 0 runtime us.
- [x] Task 41: Residual pass compile verification `[BLOCKED BY DEPENDENCY]` | DOD: build gate passed at CPU 45% and 0 active `dotnet/csc`; `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` failed on external Candice SQLite `Mono.Data` / `SqliteDataReader`; no 13AI-owned error was visible in the observed error set | Alternative rejected: editing Candice vendor save dependency from 13AI domain or repeating build after the same wall | Estimate: 0 runtime us.

## Iteration Log

- Loop 1 complete: prompt/domain/mandates and no-XML task count recorded.
- Loop 2 complete: acoustic echo ownership split; `TryUpdatePredatorEcho` is read-only over cached owner state; `TryEnqueueEchoTap` no longer bootstraps DataVault.
- Loop 3 complete: stress-driven spawn quality routes use continuous `GlobalQualityWeight` for budgets/probes/cull radii while behavior truth remains fixed.
- Loop 4 complete: drone phantom swarm uses continuous draw count for args, dispatch, and compute capacity; real drone GPU culling path no longer hard-returns false.
- Loop 5 complete: ambient biota public read views fail closed while owner jobs are pending; world consumers see default rather than racing writable buffers.
- Loop 6 complete: static verification done; compile gate blocked by CPU load and active dotnet processes per AGENTS.
- Loop 7 complete: `HectonBoidController` now records 300 owner frames into DataVault and dumps the ring on invalid dt/target/bounds/grid/clock/population/acoustic or missing runtime buffers.
- Loop 8 complete: route collision scan rejected `71990` and moved the boid Black Box to `71979`; both acoustic and boid fault dumps now use `Docs/AgentLogs/Dump_13AI.bin`.
- Loop 9 complete: build gate honored; compile proof blocked by external Candice SQLite dependency before owned 13AI files compiled cleanly or dirty.
- Loop 10 complete: pathfinding deep dive left `PathFunnelNavmeshRuntime.TryReadVoxelPathResult` untouched because current bounded read-only scan is cheaper and safer than changing ownership ABI without profiler proof.
- Loop 11 complete: `RepairDroneHub` no longer uses a static managed list for active hubs; the fixed array preserves original order so fallback hub 0 behavior remains stable.
- Loop 12 complete: `RepairDroneHub.DockingAirlock` no longer searches the scene from a read accessor; airlock resolution is cold lifecycle work only.
- Loop 13 complete: compile gate rechecked and build was not launched because CPU was 100% with 8 active dotnet processes.
- Loop 14 complete: `HullRepairedSignal` producer contract checked; drone producer had enum-quality metadata while repair tool used continuous byte metadata.
- Loop 15 complete: `DroneFleetManager` quality helper renamed away from `Authoritative` for Math LOD cadence/capacity/probe routes.
- Loop 16 complete: drone hull repair signal now writes continuous quality byte 0..255; build attempt repeated only under allowed gate and remains blocked by external Candice SQLite.
- Loop 17 complete: `FaunaSensorSuite` brine/nav-grid obstacle sampling no longer polls `GlobalRegistry.ResourceDistribution` from sensory hot paths; it consumes a cold-bound `IBrineFluidDensityReadModel`.
- Loop 18 complete: `FaunaDirector` slow-tick dependency helpers no longer repair missing depth/vegetation services by polling `GlobalRegistry`; the dead vegetation helper was removed, and cold refresh/hot-swap notification own those bindings.
- Loop 19 complete: static source checks passed for touched fauna files; compile gate later passed, and `dotnet build` remains blocked by external Candice SQLite `Mono.Data`/`SqliteDataReader`.
- Loop 20 complete: `HectonDirectorAI` no longer polls vegetation via `ActiveRuntimeInstance` from `Tick()` and no longer reads `GlobalRegistry.MapMagic` from predator line-of-sight contact processing.
- Loop 21 complete: encounter-director static checks passed; compile was correctly skipped because CPU and active `dotnet/csc` processes violated the build gate.
- Loop 22 complete: `HectonDirectorAI` non-force runtime refresh no longer enters scene/component fallback discovery, and player runtime snapshot reads use the cached `IPlayerRuntimeContext`.
- Loop 23 complete: predator sight no longer acquires DataVault buffers from tick, no longer schedules the 64-row spatial-hash helper job, and no longer reads `Transform.forward` as cognition truth.
- Loop 24 complete: `FaunaBrain` vegetation/voxel cognition routes now consume cold/hot-swap caches instead of `ActiveRuntimeInstance`; static checks passed and compile remains gated by CPU/dotnet load.
- Loop 25 complete: residual scan found remaining fauna player static fallback, predator threat-voxel active singletons, drone flora/vegetation active singletons, and ambient macro quality quantization.
- Loop 26 complete: `FaunaBrain` player runtime reads now use a cached per-frame primitive snapshot from `IPlayerRuntimeContext`, with no `PlayerRuntimeContextService` fallback in the inspected file.
- Loop 27 complete: `PredatorCognitionDomain` threat voxel refresh now consumes owner-published vegetation/cave sources instead of reading active singletons.
- Loop 28 complete: `DroneFleetManager` inspected environment routes now consume owner-published flora and vegetation bridge caches instead of active singletons.
- Loop 29 complete: ambient macro hydration uses continuous 0..255 quality bytes across `EcosystemDirector` and `AmbientBiotaDirector`; build ran under an allowed gate and remains blocked by external Candice SQLite.

## Route Cards

### Hecton Boid Black Box

Fact owner: `HectonBoidController`.
Route: `LateFrameTick()` -> `RunBoidVisualSync()` -> `WriteBoidBlackBoxFrame()`.
Vault lane: `(BufferID)71979`, `BoidBlackBoxEntry[300]`, `SystemID.AIEcology`, 128 bytes per row.
Hot-read contract: no public read accessor and no scene search. Normal frame only writes a bounded row through `TryAcquireWriteLock`; fault path dumps to disk once.
Fault artifact: `Docs/AgentLogs/Dump_13AI.bin`. Current source can overwrite this same agent-level dump path from acoustic or boid fault lanes; the file header/layout identifies the writer by system format.
Scalability: Low/Middle/High/Ultra keep identical truth ownership; quality values are telemetry fields only and do not change DTO layout or authority.

### Repair Drone Hub Registry

Fact owner: `RepairDroneHub`.
Route: lifecycle register/unregister -> fixed `RepairDroneHub[32]` -> `DroneFleetManager` and `BasePollutionManager` bounded scans.
Hot-read contract: `ActiveHubCount`, `GetActiveHubAt`, and `DockingAirlock` are bounded/pure reads. `CacheDockingAirlockCold()` owns the scene search in `Awake`/`OnEnable`/`OnSpawn`.
Diagnostics contract: runtime target identity is `_debugCurrentTargetId`; `_debugCurrentTargetName` is editor-only assignment.
Scalability: Low/Middle/High/Ultra keep identical hub truth and fixed capacity. Device quality does not change repair authority or object identity.

### Drone Fleet Repair Signal

Fact owner: `DroneFleetManager`.
Route: drone repair completion -> `HullRepairedSignal` -> atmosphere/hull repair consumers.
Hot-read contract: signal payload remains unmanaged 64 bytes and unchanged. `QualityTier` is producer-consistent continuous quality byte metadata, matching `RepairTool`.
Authority contract: `CompletedFlag`, `HitAup`, `RoomId`, `SourceHash`, dent fields, and repair completion route are unchanged. `GlobalQualityWeight` only affects metadata/cadence/capacity/fidelity, not save identity or signal layout.
Scalability: Low/Middle/High/Ultra keep identical repair completion authority. Low receives low metadata byte; Ultra receives 255, allowing presentation-side overkill without changing hull repair truth.

### Fauna Sensor Brine/Nav Obstacle Cache

Fact owner: `FaunaBrain` owns dependency binding; `FaunaSensorSuite` owns sensory reads.
Route: `FaunaBrain.RefreshColdRegistryDependencies()` through `GlobalRegistry.BrineFluidDensity` and `OnGlobalRegistryServiceReplaced(ResourceDistributionRuntime)` -> `FaunaSensorSuite.BindBrineDensityReadModel()` -> obstacle/line-of-sight sampling.
Hot-read contract: sensory probes read the cached `IBrineFluidDensityReadModel` interface only. They do not poll `GlobalRegistry`, allocate, complete jobs, or mutate global state.
Authority contract: brine density remains environmental read-model input only. It changes obstacle/visibility perception, not save identity, DTO layout, or service ownership.
Scalability: Low/Middle/High/Ultra keep the same perception truth route. Device quality can scale sensor cadence elsewhere, but a probe does not choose a different dependency route by tier.

### Fauna Director Slow-Tick Dependency Cache

Fact owner: `FaunaDirector`.
Route: lifecycle/registry callback binding -> cached depth-zone and vegetation-threat services -> `SlowTick()`.
Hot-read contract: slow tick no longer calls `GlobalRegistry` when a service is null and no longer calls a dead vegetation resolver. Serialized depth-zone fallback remains local authoring data.
Authority contract: no change to spawn, pressure, vegetation, or depth-zone DTOs. Only the dependency acquisition route changed.
Scalability: Low/Middle/High/Ultra share one deterministic route. Weak devices avoid hidden lookup repair inside AI cadence work; high tiers spend budget on richer fauna presentation, not global polling.

### Encounter Director Terrain/Vegetation Cache

Fact owner: `HectonDirectorAI`.
Route: lifecycle/registry callback binding -> cached `ITerrainProvider` and cached `HectonMapMagicVegetationBridge` -> director pressure and predator line-of-sight sampling.
Hot-read contract: director tick and predator sight contact processing read cached interfaces/objects only. Terrain/vegetation dependency acquisition is cold refresh or hot-swap callback work.
Authority contract: acoustic pressure, vegetation threat, and terrain LOS truth are unchanged. The patch changes dependency route only.
Scalability: Low/Middle/High/Ultra share one route. Device quality may scale encounter cadence elsewhere, but terrain/vegetation lookup ownership does not branch by tier.

### Encounter Director Player/Predator Sight Cache

Fact owner: `HectonDirectorAI`.
Route: cold registry/hot-swap binding -> cached `IPlayerRuntimeContext` and pre-owned predator spatial-hash DataVault handles -> encounter tick reads immutable runtime snapshots and existing NativeArray views.
Hot-read contract: non-force runtime refresh no longer scene-searches or component-searches. Predator sight tick does not acquire DataVault generation handles and does not schedule a same-frame tiny job. Predator forward is read through `IFaunaSpatialContact.ResolveContactForward()`.
Authority contract: player position, look direction, predator contact identity, and terrain sight blocking remain the same gameplay truth. Only the dependency and math route changed.
Scalability: Low/Middle/High/Ultra share one cached route. Device quality may scale encounter cadence elsewhere, but not player identity, predator contact DTOs, or DataVault ownership.

### Fauna Brain Vegetation/Voxel Cache

Fact owner: `FaunaBrain`.
Route: cold registry/hot-swap binding -> cached `HectonMapMagicVegetationBridge` and `HectonVoxelEngine` -> fear pressure, voxel guidance, burrow ambush, director hunt, corpse floor, and compatibility logic.
Hot-read contract: inspected cognition routes no longer call `ActiveRuntimeInstance`. They read cached owner references only.
Authority contract: vegetation threat, voxel route, burrow ambush, and corpse floor semantics are unchanged. Quality does not change DTO layout, save identity, or dependency ownership.
Scalability: Low/Middle/High/Ultra share one deterministic cache route. Weak devices avoid singleton lookup jitter; high tiers can spend budget on richer creature presentation rather than dependency polling.

### Fauna Player Runtime Snapshot Cache

Fact owner: `FaunaBrain`.
Route: cold registry/hot-swap binding -> cached `IPlayerRuntimeContext` -> frame-local `FaunaPlayerRuntimeContextSnapshot` -> active fauna cognition reads.
Hot-read contract: inspected `FaunaBrain` player runtime reads no longer call `PlayerRuntimeContextService.TryGetActiveRuntimeContext()` and no longer depend on a `PlayerRuntimeContext` object fallback. The snapshot is refreshed once per dispatcher frame and contains only cached references plus movement/look states.
Authority contract: player transform, movement, look, flashlight, and tool-manager truth still come from the player runtime owner. No DTO layout, save identity, or gameplay authority changed.
Scalability: Low/Middle/High/Ultra share one snapshot route. Quality may scale fauna cadence elsewhere, but not player identity or dependency ownership.

### Predator Threat Voxel Source Cache

Fact owner: `PredatorCognitionDomain`.
Route: `HectonMapMagicVegetationBridge` and `HectonCaveVoxelLightingVolume` lifecycle/reset -> `Bind*Source` / `Clear*Source` -> `RefreshThreatVoxelSnapshot()`.
Hot-read contract: threat-voxel refresh reads cached owner-published sources only. It no longer polls vegetation or cave active singletons from predator cognition.
Authority contract: vegetation threat voxel and cave SDF semantics are unchanged. The patch changes dependency route only.
Scalability: Low/Middle/High/Ultra share one route. Cheap devices avoid singleton lookup jitter; top-tier devices can spend budget on richer predator presentation rather than alternate truth.

### Drone Fleet Environment Source Cache

Fact owner: `DroneFleetManager`.
Route: `FloraInteractionManager` lifecycle/reset and `HectonMapMagicVegetationBridge` lifecycle/registry route -> cached manager/bridge fields -> assignment, parasite, headless task-map, and abyssal flow payload logic.
Hot-read contract: inspected drone routes no longer read `FloraInteractionManager.ActiveRuntimeInstance` or `HectonMapMagicVegetationBridge.ActiveRuntimeInstance`.
Authority contract: drone tasks, parasite attacks, abyssal flow payloads, and flora interaction semantics are unchanged. Global quality still only scales cadence/capacity/fidelity elsewhere.
Scalability: Low/Middle/High/Ultra share one cached environment route. Weak devices avoid lookup jitter; high and ultra tiers spend saved slack on visual density and drone presentation.

### Ambient Macro Hydration Quality Route

Fact owner: `EcosystemDirector` publishes macro quality input; `AmbientBiotaDirector` consumes it for macro hydration.
Route: continuous `GlobalQualityWeight` -> 0..255 quality byte -> `IAmbientBiotaService.TryHydrateMacroSwarms()` -> continuous `QualityWeight01` in `AmbientBiotaMacroHydrationJob` -> continuous `EntitySpawnSignal.QualityTier` metadata after stress attenuation.
Hot-read contract: no new registry lookup, allocation, scene search, or hidden job completion was added.
Authority contract: swarm DTO layout and spawn signal layout are unchanged. Quality scales fidelity/survival pressure presentation inputs continuously; it does not create a new gameplay owner route.
Scalability: Low/Middle/High/Ultra interpolate across the same byte/float route instead of a 4-step tier staircase. Ultra can hit visual-overkill metadata without changing spawn authority.

## Residual Debt

- `HectonBoidController` Black Box is patched but runtime proof is pending. No fault injection has generated `Docs/AgentLogs/Dump_13AI.bin` yet.
- `PathFunnelNavmeshRuntime.TryReadVoxelPathResult` still scans result capacity. It is bounded at 1024 and read-only; replacing it with requester-indexed lookup requires result ownership changes and was left untouched.
- `RepairDroneHub` still exposes active hub identity through a static fixed array outside DataVault. This is an intentional Unity-object reference route, bounded to 32, not a managed list.
- GPU culling activation needs Unity/RenderDoc proof. Static code path now exists; runtime readiness remains unverified.
- Fauna sensor and slow-tick registry-cache patches need Unity runtime/profiler proof. Static checks prove route removal only; no frame-time, GC, or behavior capture has been produced.
- `HectonDirectorAI.RefreshRuntimeReferences(force: false)` still runs from `Tick()` behind a one-second retry gate, but its non-force branch no longer enters scene/component fallback discovery. Residual risk is the continued retry cadence itself and serialized `faunaDirector` cold fallback.
- `FaunaBrain` player-runtime fallback debt from `TryResolveCachedPlayerRuntimeContext()` is fixed in the inspected file. Runtime behavior proof is still pending.
- `PredatorCognitionDomain.RefreshThreatVoxelSnapshot()` active singleton debt is fixed by owner-published vegetation/cave source caches. Runtime behavior proof is still pending.
- `DroneFleetManager` inspected flora/MapMagic active singleton reads are fixed by owner-published caches. Runtime behavior proof is still pending.
- `FaunaDirector.EnsureRuntimeStateInitialized()` can still allocate runtime state if cold initialization was missed or invalidated; needs lifecycle proof before patching.
- `AmbientBiotaDirector` macro hydration 4-step quality quantization is fixed for the inspected ecosystem call path. Runtime/profiler proof is still pending.
- `HectonMapMagicVegetationBridge` still has two world-domain static player-context lookups at lines observed around 2849 and 6125. These were not patched in 13AI because they are world/terrain view-camera routes, not the inspected AI fish/creature/drone hot path.
- `PredatorCognitionDomain.cs` naive paren count reads 3432/3433 because the source contains a non-code parenthesis imbalance in comments/strings or pre-existing syntax context; brace count is balanced. Full compiler proof is still blocked by the build gate.

## Current Pass 2026-05-27

- [x] Task 42: Residual predator/mesofauna/foveated audit. DOD: traced mesofauna quality, retinal cadence, steering flags, blackbox dump paths, and foveated damage-lock service route. Rejected alternative: changing behavior truth with device quality. Estimate: 0 runtime us; audit only.
- [x] Task 43: Split mesofauna behavior truth from cadence quality. DOD: `ResolveMesofaunaGlobalQualityWeight()` remains authoritative for behavior math while `ResolveMesofaunaCadenceQualityWeight()` drives slice modulo from continuous `HomeostasisBrain.GlobalQualityWeight`. Rejected alternative: one scalar controlling both gameplay truth and work cadence. Estimate: at quality 0, full mesofauna FSM work can be reduced toward 1/10 active slots per frame; exact us unmeasured.
- [x] Task 44: Restore continuous predator retinal cadence and high-tier steering gating. DOD: predator retinal interval now lerps 1.0s to 0.5s by continuous quality, alpha cadence remains 0.1s, and the job consumes `CognitionInputFlags.HighTierSmoothSteering`. Rejected alternative: hardcoded high-tier math for every predator input. Estimate: up to 50% fewer non-alpha utility evaluations at minimum quality; exact us unmeasured.
- [x] Task 45: Normalize 13AI blackbox dump and foveated director route. DOD: retinal, alpha, and mesofauna fault dumps write `Docs/AgentLogs/Dump_13AI.bin`, and foveated combat damage lock reads a cached `IFoveatedSimulationDirector` bound by cold refresh/hot-swap. Rejected alternative: subsystem-specific dump filenames and direct registry read from damage-lock path. Estimate: 0 normal-frame us for dump route; one registry read removed per damage-lock notification.
- [x] Task 46: Static verification after source patch. DOD: `rg` confirmed old disabled cadence/always-high steering/dump-name/direct-registry residues are gone; brace counts are balanced; `git diff --check` reported CRLF warnings only. Rejected alternative: treating source edits as proved without targeted scans. Estimate: verification only.
- [x] Task 47: Compile verification attempt. DOD: build gate sampled CPU 21% and 0 active `dotnet/csc`, then `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` was run. Result: `[BLOCKED BY DEPENDENCY]` on external Candice SQLite `Mono.Data` / `SqliteDataReader`; no 13AI-owned compile error was visible in the observed error set. Rejected alternative: editing vendor save dependency from the 13AI domain. Estimate: 0 runtime us.

## Iteration Notes

- Loop 30 complete: residual scan found mesofauna cadence tied to authoritative quality, predator retinal cadence disabled, high-tier steering forced true, subsystem-specific blackbox dump files, and a direct foveated registry read.
- Loop 31 complete: predator cognition now separates behavior truth from continuous cadence, gates expensive steering by input flags, restores continuous retinal cadence, and writes mandated `Dump_13AI.bin` fault artifact.
- Loop 32 complete: foveated damage-lock routing now uses cached cold/hot-swap dependency; static checks passed for inspected residues; full compile remains blocked by external Candice SQLite.

## Route Cards

### Predator Cognition Cadence And Steering

Fact owner: `PredatorCognitionDomain`.
Route: continuous `HomeostasisBrain.GlobalQualityWeight` -> mesofauna cadence quality -> `ResolveMesofaunaSliceModulo()`; authoritative behavior quality remains `1.0` for mesofauna behavior/tuning math. Predator retinal interval lerps between low cadence and utility cadence; alpha predator cadence is unchanged. Apex smooth steering is enabled only through `CognitionInputFlags.HighTierSmoothSteering`.
Hot-read contract: no new registry lookup, allocation, scene search, or job completion was added. Quality changes work cadence and optional steering fidelity only.
Authority contract: mesofauna behavior truth, DTO layout, save identity, predator roles, and alpha authority are unchanged.
Scalability: Low runs the cheapest cadence path and dominant-axis/basic steering where flags allow. Middle interpolates cadence. High/Ultra can spend work on smooth steering and denser visual response without changing AI truth.

### Predator/Fauna Blackbox Dump

Fact owner: `PredatorCognitionDomain` for retinal, alpha, and mesofauna blackbox lanes.
Route: NaN/fault detection -> subsystem writer -> `Docs/AgentLogs/Dump_13AI.bin`.
Hot-read contract: normal frame writes remain bounded circular telemetry only; disk write is fault-only.
Authority contract: dump file is diagnostic only. It does not change cognition, save identity, DTO layout, or signal routes.
Scalability: Low/Middle/High/Ultra share the same dump artifact. Header/layout identifies the writer; no quality switch changes telemetry ownership.

### Fauna Foveated Damage-Lock Director Cache

Fact owner: `FaunaBrain` owns dependency binding; `IFoveatedSimulationDirector` owns foveated combat lock truth.
Route: `RefreshColdRegistryDependencies()` and `OnGlobalRegistryServiceReplaced(FoveatedSimulationDirector)` -> `_foveatedSimulationDirector` -> `NotifyFoveatedCombatDamageLock()`.
Hot-read contract: damage-lock notification reads a cached interface. It does not poll `GlobalRegistry`, search the scene, allocate, or complete jobs.
Authority contract: foveated damage-lock semantics are unchanged.
Scalability: Low/Middle/High/Ultra share one route; quality can scale foveated cadence elsewhere, not dependency ownership.

## Residual Debt Update

- Full compiler proof is still blocked by external Candice SQLite `Mono.Data` / `SqliteDataReader`. This is outside the 13AI domain and should be owned by the save/vendor integration lane.
- Runtime/profiler proof is still missing for predator cadence and foveated cache changes. Static checks prove route correction only.
- Fault-injection proof for `Docs/AgentLogs/Dump_13AI.bin` is still missing for retinal, alpha, and mesofauna writers.
- `FaunaDirector.EnsureRuntimeStateInitialized()` can still allocate runtime state if cold initialization was missed or invalidated; lifecycle proof is required before patching.
- `HectonMapMagicVegetationBridge` world-domain static player-context lookups remain outside this 13AI pass.

## Current Pass 2026-05-27 - Fauna/ambient residual route cleanup

- [x] Task 48: Residual fauna presentation/director/ambient dump audit. DOD: re-extracted `CURRENT_BATCH.md` by CLI (`NO_13AI_PROMPT`) and scanned fauna spawn presentation, director player/time reads, stress spawn dump path, and ambient biota blackbox artifact names. Rejected alternative: assuming previous predator pass covered spawn/ambient routes. Estimate: 0 runtime us; static audit only.
- [x] Task 49: Fauna spawn presentation dependency cache patch. DOD: `FaunaPresentationService` now receives `FaunaGeneticsManager` and `EcosystemHealthDirector` via `FaunaDirector` cold refresh and hot-swap callbacks; `ConfigureSpawnedCreature()` no longer reads `GlobalRegistry`. Rejected alternative: per-spawn registry lookup hidden behind presentation helper. Estimate: removes two global registry reads per spawned creature; exact us unmeasured.
- [x] Task 50: FaunaDirector player/dispatcher route patch. DOD: `FaunaDirector` now caches `IPlayerRuntimeContext` and `SystemDispatcher`, refreshes a primitive per-frame player snapshot, and uses cached dispatcher time instead of `SystemDispatcher.ActiveRuntimeInstance`. Rejected alternative: static player runtime fallback or global dispatcher singleton in spawn logic. Estimate: removes one static dispatcher singleton read per inspected director time query and the static player-context fallback from spawn/player pose reads; exact us unmeasured.
- [x] Task 51: FaunaBrain dispatcher and stress/ambient dump cleanup. DOD: `FaunaBrain` dispatcher time reads now use cached `SystemDispatcher`; `StressDrivenSpawnDirector` and `AmbientBiotaDirector` fault dumps now resolve to `Docs/AgentLogs/Dump_13AI.bin`. Rejected alternative: subsystem-specific dump filenames and active dispatcher singleton reads from fauna logic. Estimate: 0 normal-frame us for dump path; removes active dispatcher singleton reads from inspected fauna brain time path.
- [x] Task 52: Static verification after fauna/ambient patch. DOD: targeted `rg` found no `PlayerRuntimeContextService.TryGetActiveRuntimeContext`, no `SystemDispatcher.ActiveRuntimeInstance`, no `Dump_SHINOBU_253`, and no `Dump_AMBIENT_BIOTA_DIRECTOR` in touched files; brace/paren counts matched; `git diff --check` returned CRLF warnings only. Rejected alternative: reporting source edits without residue scans. Estimate: verification only.
- [x] Task 53: Compile verification `[COMPILE GATED]`. DOD: build gate sampled CPU 36% but 1 active `dotnet` process, so `dotnet build` was not launched under AGENTS rules. Rejected alternative: starting another compiler beside an active build process. Estimate: 0 runtime us.

## Iteration Notes

- Loop 33 complete: residual scan found per-spawn registry dependency in `FaunaPresentationService`, static player/dispatcher fallback in `FaunaDirector`, active dispatcher singleton in `FaunaBrain`, and non-13AI blackbox paths in stress/ambient AI.
- Loop 34 complete: fauna spawn presentation is now bound by the director owner route, fauna director/player time reads use cached interfaces, and stress/ambient dump artifacts point at `Dump_13AI.bin`.
- Loop 35 complete: targeted static verification passed for the inspected residues; compile was correctly skipped because another `dotnet` process was active.

## Route Cards

### Fauna Spawn Presentation Cache

Fact owner: `FaunaDirector`.
Route: cold registry refresh / hot-swap callback -> `_faunaGenetics` + `_ecosystemHealth` -> `FaunaPresentationService.Bind()` -> `ConfigureSpawnedCreature()`.
Hot-read contract: spawned-creature presentation wiring no longer polls `GlobalRegistry`; it consumes cached owner references only.
Authority contract: genetics traits and ecosystem-health configuration semantics are unchanged. Spawn identity, archetype data, and chunk coordinates are unchanged.
Scalability: Low/Middle/High/Ultra share one dependency route. Quality can scale spawn cadence elsewhere, not the genetics/ecosystem ownership path.

### Fauna Director Player And Dispatcher Cache

Fact owner: `FaunaDirector`.
Route: `GlobalRegistry.Player` and `GlobalRegistry.Dispatcher` cold/hot-swap binding -> per-frame `FaunaDirectorPlayerRuntimeContextSnapshot` -> spawn player transform, view, AUP, and dispatcher time reads.
Hot-read contract: inspected director reads no longer call `PlayerRuntimeContextService.TryGetActiveRuntimeContext()` or `SystemDispatcher.ActiveRuntimeInstance`. Scene player fallback remains only as a degraded cold resolver when the runtime owner is absent.
Authority contract: player transform, movement, look, and dispatcher time truth remain owned by their runtime services. No DTO layout, save identity, or spawn authority changed.
Scalability: Low avoids static lookup jitter in spawn/culling logic. Middle/High/Ultra can spend budget on denser creature presentation without alternate player truth routes.

### Fauna/ambient 13AI Dump Artifact

Fact owners: `StressDrivenSpawnDirector`, `AmbientBiotaDirector`, and existing predator/fauna blackbox writers.
Route: fault/NaN/sanitized-state detection -> 300-frame telemetry ring -> `Docs/AgentLogs/Dump_13AI.bin`.
Hot-read contract: normal frame writes remain bounded telemetry only; disk write is fault-only.
Authority contract: dump file is diagnostic. It does not alter AI state, spawn DTOs, save identity, or signal payload layout.
Scalability: Low/Middle/High/Ultra share the same forensic artifact. Headers/source hashes identify writer layout; quality does not change the dump route.

## Residual Debt Update

- Compile was not launched in this pass because one `dotnet` process was already active. Earlier allowed builds still hit the external Candice SQLite `Mono.Data` / `SqliteDataReader` wall.
- `FaunaDirector.FindPlayer()` still has `WorldRuntimeReferenceUtility.TryResolvePlayerTransform()` as a degraded fallback if the player runtime owner is missing. This is recorded, not removed, because deleting it could break bootstrap/editor scenes without a replacement route.
- `FaunaBrain.ResolveCorpseBloatShaderClockSeconds()` still uses `Time.timeSinceLevelLoad`. It appears tied to shader `_Time` presentation semantics, so it was not replaced with dispatcher time without shader proof.
- `World/EcosystemDirector.cs` still has world-domain active singleton/registry reads around mutation scalar sampling. This pass did not patch it because broad ecosystem ownership is outside the narrow fauna/ambient AI route fixed here.

## Current Pass 2026-05-27 - AI blackbox dump route consolidation

- [x] Task 54: Residual dump-route audit. DOD: re-extracted `CURRENT_BATCH.md` by CLI (`NO_13AI_PROMPT`) and scanned AI/fauna/pathfinding/procedural creature dump names against the 13AI blackbox rule. Rejected alternative: mass-renaming 1300/1301/1306 owner artifacts without their route cards. Estimate: 0 runtime us; static audit only.
- [x] Task 55: Fauna kinematics dump artifact alignment. DOD: `FaunaKinematicsRuntime` solver and bite telemetry dump constants now target `Docs/AgentLogs/Dump_13AI.bin`. Rejected alternative: subsystem-specific files for solver/bite writers. Estimate: 0 normal-frame us; fault-path target only.
- [x] Task 56: Predator, pathfinding, and procedural creature dump artifact alignment. DOD: predator steering, predator acoustic SDF, path funnel, voxel A*, Leviathan tentacle IK, and procedural crab leg IK fault paths now target `Docs/AgentLogs/Dump_13AI.bin`; steering dump path is anchored to project root instead of raw process cwd. Rejected alternative: relative `FileStream` path and legacy SHINOBU/subsystem filenames. Estimate: 0 normal-frame us; fault-path target only.
- [x] Task 57: Architecture route-card update. DOD: `SHINOBU_303_LEVIATHAN_STEERING_ROUTE.md` and `SHINOBU_311_ACOUSTIC_SENSORY_ROUTE.md` now document `Docs/AgentLogs/Dump_13AI.bin`. Rejected alternative: leaving docs inconsistent with runtime forensic route. Estimate: 0 runtime us.
- [x] Task 58: Static verification after dump consolidation. DOD: targeted `rg` found no old targeted dump names in AI/fauna/pathfinding route docs and source; `git diff --check` returned CRLF warnings only; braces matched for all inspected source files. Rejected alternative: reporting path edits without residue scan. Estimate: verification only.
- [x] Task 59: Compile verification `[COMPILE GATED]`. DOD: build gate sampled CPU 51%, then 65%, then 63% with no active `dotnet/csc`; `dotnet build` was not launched because CPU stayed above the 50% AGENTS threshold. Rejected alternative: forbidden compiler launch under load. Estimate: 0 runtime us.

## Iteration Notes

- Loop 36 complete: residual dump audit found legacy SHINOBU/subsystem dump filenames inside active 13AI subdomain files plus stale route-card text.
- Loop 37 complete: dump artifact routes now converge on the mandated `Docs/AgentLogs/Dump_13AI.bin`; steering writer no longer depends on Unity process working directory.
- Loop 38 complete: targeted residue scan and whitespace checks passed; compile proof remains gated by machine load.

## Route Cards

### 13AI Blackbox Dump Consolidation

Fact owners: `PredatorCognitionDomain`, `FaunaKinematicsRuntime`, `PathFunnelNavmeshRuntime`, `LeviathanTentacleVerletSolver`, and `ProceduralCrabLegIKRuntime`.
Route: fault/NaN/budget breach -> existing 300-frame telemetry ring -> `Docs/AgentLogs/Dump_13AI.bin`.
Hot-read contract: normal frames still write bounded telemetry only. Disk output remains fault-only and does not add a hot allocation, scene search, registry read, or hidden job completion.
Authority contract: dump path is diagnostic only. AI truth, DTO layout, save identity, signal payloads, and pathfinding authority are unchanged.
Scalability: Low/Middle/High/Ultra share one forensic artifact. Quality may scale cadence/fidelity elsewhere, but not diagnostic ownership or fault route identity.

## Residual Debt Update

- Full compiler proof for this dump-route pass is `[COMPILE GATED]` because CPU stayed above 50% during three build-gate samples.
- Runtime fault-injection proof for `Docs/AgentLogs/Dump_13AI.bin` is still missing for the newly aligned writers.
- Non-13AI 1300/1301/1306 dump routes remain untouched because they are separate active owner lanes and require their own route-card review before consolidation.
- `PredatorCognitionDomain_Steering.cs` still has a naive parenthesis counter mismatch caused by non-code text/strings or pre-existing context; braces are balanced and `git diff --check` did not report syntax whitespace errors. Compiler proof is still required.

## Current Pass 2026-05-27 - telemetry truth and symbiosis Math LOD

- [x] Task 60: Residual high-tier/quality audit. DOD: re-read status/rationale and re-extracted `CURRENT_BATCH.md` by CLI (`NO_13AI_PROMPT`); scanned 13AI files for forced high-tier steering, binary quality leftovers, hot registry reads, and legacy dumps. Rejected alternative: trusting earlier reports without rereading source. Estimate: 0 runtime us; audit only.
- [x] Task 61: Alpha telemetry high-tier truth patch. DOD: alpha blackbox telemetry now reads `CognitionInputFlags.HighTierSmoothSteering` instead of hardcoded `true`, matching the actual predator steering route. Rejected alternative: telemetry claiming SDF dive/high-tier path on minimum-quality frames. Estimate: 0 normal-frame us saved; correctness/forensic fix.
- [x] Task 62: Fish/flora symbiosis micro-exchange Math LOD patch. DOD: `ShinobuFloraFaunaSymbiosisSolver` now uses existing `ResolveMicroExchangeWeight()` plus deterministic `ResolveDitheredFrameGate()` to choose micro neighbor exchange frames; low quality uses macro approximation, high quality reaches full micro exchange. Rejected alternative: `runMicroExchangeFrame = true` forcing neighbor sampling every cold tick. Estimate: at quality below `MacroThreshold`, avoids one flora spatial hash job and skips micro neighbor sampling for mock/ambient fish; exact us unmeasured.
- [x] Task 63: Symbiosis dump artifact cleanup. DOD: symbiosis blackbox writer now targets `Docs/AgentLogs/Dump_13AI.bin` and no longer writes duplicate `Dump_SYMBIOSIS.bin`. Rejected alternative: duplicate fault-path I/O and legacy SHINOBU_62 artifact. Estimate: 0 normal-frame us; fault-path I/O reduced by one file write on dump.
- [x] Task 64: Static verification after telemetry/symbiosis patch. DOD: targeted `rg` found no `highTierSmoothSteering = true`, no `bool runMicroExchangeFrame = true`, no `Dump_SHINOBU_62`, and no `Dump_SYMBIOSIS` in touched files; braces matched; `git diff --check` returned CRLF warnings only. Rejected alternative: claiming source edits without residue scan. Estimate: verification only.
- [x] Task 65: Compile verification `[COMPILE GATED]`. DOD: build gate sampled CPU 100%, waited 30 seconds, then CPU 100% again with no active `dotnet/csc`; `dotnet build` was not launched because CPU stayed above the 50% AGENTS threshold. Rejected alternative: forbidden compiler launch under load. Estimate: 0 runtime us.

## Iteration Notes

- Loop 39 complete: forced alpha telemetry high-tier flag was separated from behavior and identified as a forensic truth bug.
- Loop 40 complete: symbiosis already had continuous micro-exchange weight/dither helpers but bypassed them with a constant `true`; patch restored stochastic continuous Math LOD.
- Loop 41 complete: static residue checks passed; compile proof remains gated by machine load.

## Route Cards

### Alpha Telemetry High-Tier Truth

Fact owner: `PredatorCognitionDomain`.
Route: `CognitionInputFlags.HighTierSmoothSteering` -> alpha telemetry flags -> 300-frame alpha blackbox -> `Docs/AgentLogs/Dump_13AI.bin`.
Hot-read contract: no new lookup, allocation, scene search, signal publish, or job completion was added.
Authority contract: predator behavior is unchanged. The patch makes telemetry reflect existing steering input truth instead of hardcoded high-tier truth.
Scalability: Low/Middle/High/Ultra share the same behavior owner. Low no longer records false SDF/high-tier telemetry; High/Ultra still record SDF dive when the input flag authorizes it.

### Symbiosis Micro-Exchange Math LOD

Fact owner: `ShinobuFloraFaunaSymbiosisSolver`.
Route: continuous `GlobalQualityWeight` -> `ResolveMicroExchangeWeight(quality, MacroThreshold)` -> deterministic frame dither -> micro neighbor exchange or macro average fallback.
Hot-read contract: no registry lookup, scene search, allocation, or hidden `.Complete()` was added. The low-quality branch skips the flora spatial hash job and uses the existing macro average path.
Authority contract: symbiosis DTO layout, save identity, flora/fish flags, and telemetry ABI are unchanged. Quality changes solver cadence/detail only, not species identity or ownership.
Scalability: Low uses macro approximation. Middle stochastically blends micro frames by continuous weight. High/Ultra run dense micro exchange and can spend budget on richer fish/flora presentation.

## Residual Debt Update

- Full compiler proof for this pass is `[COMPILE GATED]` because CPU stayed at 100% across two build-gate samples.
- Runtime profiler proof is still missing for the symbiosis micro-exchange LOD. Static proof only shows job/branch gating.
- Symbiosis fault-injection proof for `Docs/AgentLogs/Dump_13AI.bin` is still missing.
- `EcosystemPopulationBalancer` still uses a subsystem-specific dump path and a serialized `enableTier1FleeDown` boolean. It was not patched in this pass because it may be a separate ecosystem owner lane and needs route-card review before changing behavior/dump ownership.
